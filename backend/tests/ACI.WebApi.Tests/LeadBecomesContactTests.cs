using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

public class LeadBecomesContactTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EveryLeadWithAPersonBecomesAContact_AndLookalikesCanMerge()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var registered = await ReadJson(await client.PostAsync("/api/auth/register", Body(new
        {
            email = $"ada-{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            name = "Ada Lovelace",
        })), "register");

        var token = registered.GetProperty("token").GetString();
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orgId = await SeedWorkspaceAsync(factory, userId);
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var existingEmail = $"existing-{Guid.NewGuid():N}@example.com";
        await SeedLeadAsync(factory, userId, orgId, "Existing Person", existingEmail);

        var first = await ReadJson(await client.PostAsync("/api/leads", Body(new
        {
            name = "Ada Example",
            email = $"ada-one-{Guid.NewGuid():N}@example.com",
            phone = "+41 79 111 11 11",
            status = "New",
        })), "create first lead");
        first.GetProperty("isConverted").GetBoolean().Should().BeFalse();

        var second = await ReadJson(await client.PostAsync("/api/leads", Body(new
        {
            name = "Ada Example",
            email = $"ada-two-{Guid.NewGuid():N}@example.com",
            phone = "41791111111",
            status = "New",
        })), "create second lead");
        second.GetProperty("isConverted").GetBoolean().Should().BeFalse();

        var contacts = await ReadJson(await client.GetAsync("/api/contacts?page=1&pageSize=50"), "list contacts");
        var emails = contacts.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("email").GetString())
            .ToArray();
        emails.Should().Contain(existingEmail);
        emails.Should().Contain(first.GetProperty("email").GetString());
        emails.Should().Contain(second.GetProperty("email").GetString());

        var keep = contacts.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("email").GetString() == first.GetProperty("email").GetString());
        var drop = contacts.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("email").GetString() == second.GetProperty("email").GetString());

        var lookalikes = await ReadJson(
            await client.GetAsync($"/api/contacts/{keep.GetProperty("id").GetGuid()}/lookalikes"),
            "lookalikes");
        lookalikes.GetArrayLength().Should().BeGreaterThan(0);
        var match = lookalikes.EnumerateArray()
            .First(item => item.GetProperty("id").GetGuid() == drop.GetProperty("id").GetGuid());
        var fields = match.GetProperty("fields").EnumerateArray().ToArray();
        fields.Should().Contain(field => field.GetProperty("field").GetString() == "Name" && field.GetProperty("same").GetBoolean());
        fields.Should().Contain(field => field.GetProperty("field").GetString() == "Phone" && field.GetProperty("same").GetBoolean());
        fields.Should().Contain(field => field.GetProperty("field").GetString() == "Email" && !field.GetProperty("same").GetBoolean());

        var merge = await client.PostAsync(
            $"/api/contacts/{keep.GetProperty("id").GetGuid()}/merge",
            Body(new { otherContactId = drop.GetProperty("id").GetGuid() }));
        merge.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ReadJson(await client.GetAsync("/api/contacts?page=1&pageSize=50"), "list after merge");
        var remaining = after.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("email").GetString())
            .ToArray();
        remaining.Should().Contain(first.GetProperty("email").GetString());
        remaining.Should().NotContain(second.GetProperty("email").GetString());
    }

    [Fact]
    public async Task MergingKeepsDoNotContact_WhenOnlyTheDroppedRecordHadIt()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var registered = await ReadJson(await client.PostAsync("/api/auth/register", Body(new
        {
            email = $"grace-{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            name = "Grace Hopper",
        })), "register");
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registered.GetProperty("token").GetString());
        var orgId = await SeedWorkspaceAsync(factory, userId);
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var keepId = Guid.NewGuid();
        var dropId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Contacts.Add(new Contact
            {
                Id = keepId, UserId = userId, OrganizationId = orgId,
                Name = "Pat Example", Email = $"pat-{Guid.NewGuid():N}@example.com", CreatedAtUtc = DateTime.UtcNow,
            });
            db.Contacts.Add(new Contact
            {
                Id = dropId, UserId = userId, OrganizationId = orgId,
                Name = "Pat Example", Email = $"pat-old-{Guid.NewGuid():N}@example.com", CreatedAtUtc = DateTime.UtcNow,
                DoNotContact = true,
            });
            await db.SaveChangesAsync();
        }

        var merge = await client.PostAsync($"/api/contacts/{keepId}/merge", Body(new { otherContactId = dropId }));
        merge.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var kept = await ReadJson(await client.GetAsync($"/api/contacts/{keepId}"), "kept contact");
        kept.GetProperty("doNotContact").GetBoolean().Should().BeTrue();
    }

    private static async Task<Guid> SeedWorkspaceAsync(CustomWebApplicationFactory factory, Guid ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Same Person Club",
            OwnerUserId = ownerUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organizations.Add(org);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = ownerUserId,
            Role = OrgMemberRole.Owner,
            JoinedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return org.Id;
    }

    private static async Task SeedLeadAsync(
        CustomWebApplicationFactory factory, Guid userId, Guid organizationId, string name, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Leads.Add(new Lead
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = name,
            Email = email,
            Status = "New",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
}
