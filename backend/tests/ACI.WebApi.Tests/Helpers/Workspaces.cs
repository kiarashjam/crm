using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// Real accounts and workspaces for tests that go over HTTP.
/// </summary>
/// <remarks>
/// Accounts are registered through the API, so every request carries a real JWT.
/// Workspaces and memberships are written to the database directly: creating one
/// over HTTP backfills records with ExecuteUpdateAsync, which the in-memory provider
/// cannot run. What these tests exercise — routing, authorisation, the
/// organisation and read-only middleware, the controllers — still runs for real.
/// </remarks>
public static class Workspaces
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    /// <summary>Register an account and return a client signed in as it, plus its user id.</summary>
    public static async Task<(HttpClient Client, Guid UserId)> SignInAsync(CustomWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsync("/api/auth/register", Body(new { email, password = "correct horse battery staple", name = email }));
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"registering {email} should work: {body}");
        using var doc = JsonDocument.Parse(body);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("token").GetString());
        return (client, doc.RootElement.GetProperty("user").GetProperty("id").GetGuid());
    }

    /// <summary>A workspace owned by <paramref name="ownerId"/>.</summary>
    public static async Task<Guid> CreateAsync(CustomWebApplicationFactory factory, Guid ownerId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = new Organization { Id = Guid.NewGuid(), Name = name, OwnerUserId = ownerId, CreatedAtUtc = DateTime.UtcNow };
        db.Organizations.Add(org);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id, UserId = ownerId, Role = OrgMemberRole.Owner, JoinedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return org.Id;
    }

    public static async Task AddMemberAsync(CustomWebApplicationFactory factory, Guid orgId, Guid userId, OrgMemberRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = orgId, UserId = userId, Role = role, JoinedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Send requests as a member of this workspace.</summary>
    public static HttpClient In(this HttpClient client, Guid orgId)
    {
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        return client;
    }

    public static async Task<T> Read<T>(this IServiceProvider services, Func<AppDbContext, T> read)
    {
        using var scope = services.CreateScope();
        await Task.CompletedTask;
        return read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
