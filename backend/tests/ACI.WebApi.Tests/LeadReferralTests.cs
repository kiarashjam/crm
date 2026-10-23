using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Who referred a lead is saved on the server, so the whole team sees it. It used to live
/// only in one browser's storage.
/// </summary>
public class LeadReferralTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> SeedContactAsync(CustomWebApplicationFactory factory, Guid userId, Guid orgId, string name, bool archived = false, Guid? fromLead = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.ch",
            CreatedAtUtc = DateTime.UtcNow,
            IsArchived = archived,
            ConvertedFromLeadId = fromLead,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    [Fact]
    public async Task ATeammateSeesWhoReferredALead_AndTheContactListsWhoTheyReferred()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var sam = await SeedContactAsync(factory, ownerId, org, "Sam Lee");

        // Set at creation.
        var created = await Json(await owner.In(org).PostAsync("/api/leads", Body(new
        {
            name = "Ada Lovelace",
            email = "ada@example.ch",
            status = "New",
            referredByContactId = sam,
        })));
        var leadId = created.GetProperty("id").GetGuid();
        created.GetProperty("referredByContactName").GetString().Should().Be("Sam Lee");

        // Another member of the workspace reads it.
        var seen = await Json(await rep.In(org).GetAsync($"/api/leads/{leadId}"));
        seen.GetProperty("referredByContactId").GetGuid().Should().Be(sam);
        seen.GetProperty("referredByContactName").GetString().Should().Be("Sam Lee");

        var referred = await Json(await rep.In(org).GetAsync($"/api/contacts/{sam}/referred-leads"));
        referred.EnumerateArray().Select(l => l.GetProperty("id").GetGuid()).Should().Equal(leadId);

        // Changed and cleared from the lead page.
        var grace = await SeedContactAsync(factory, ownerId, org, "Grace Hopper");
        var changed = await Json(await rep.In(org).PutAsync($"/api/leads/{leadId}/referral", Body(new { contactId = grace })));
        changed.GetProperty("referredByContactName").GetString().Should().Be("Grace Hopper");
        (await Json(await rep.In(org).GetAsync($"/api/contacts/{sam}/referred-leads"))).GetArrayLength().Should().Be(0);

        var cleared = await Json(await rep.In(org).PutAsync($"/api/leads/{leadId}/referral", Body(new { contactId = (Guid?)null })));
        cleared.GetProperty("referredByContactId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task OnlyAnActiveContactInTheSameWorkspaceCanBeTheReferrer()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var other = await CreateAsync(factory, ownerId, "Other club");
        var outsider = await SeedContactAsync(factory, ownerId, other, "Outside Person");
        var archived = await SeedContactAsync(factory, ownerId, org, "Gone Person", archived: true);

        var lead = await Json(await owner.In(org).PostAsync("/api/leads", Body(new { name = "Ada Lovelace", email = "ada@example.ch", status = "New" })));
        var leadId = lead.GetProperty("id").GetGuid();
        var self = await SeedContactAsync(factory, ownerId, org, "Ada Lovelace", fromLead: leadId);

        foreach (var contact in new[] { outsider, archived, self, Guid.NewGuid() })
        {
            var res = await owner.In(org).PutAsync($"/api/leads/{leadId}/referral", Body(new { contactId = contact }));
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        var createdWithOutsider = await owner.In(org).PostAsync("/api/leads", Body(new { name = "Bea", email = "bea@example.ch", status = "New", referredByContactId = outsider }));
        createdWithOutsider.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Another workspace cannot read who a contact referred.
        (await owner.In(other).GetAsync($"/api/contacts/{archived}/referred-leads")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MergingMovesTheReferralToTheContactKept()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var keep = await SeedContactAsync(factory, ownerId, org, "Sam Lee");
        var drop = await SeedContactAsync(factory, ownerId, org, "Sam Lee (old)");

        var lead = await Json(await owner.In(org).PostAsync("/api/leads", Body(new { name = "Ada Lovelace", email = "ada@example.ch", status = "New", referredByContactId = drop })));
        var leadId = lead.GetProperty("id").GetGuid();

        (await owner.In(org).PostAsync($"/api/contacts/{keep}/merge", Body(new { otherContactId = drop })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var merged = await Json(await owner.In(org).GetAsync($"/api/leads/{leadId}"));
        merged.GetProperty("referredByContactId").GetGuid().Should().Be(keep);

    }

    [Fact]
    public async Task DeletingTheReferrerClearsTheReferral_UnderARealForeignKey()
    {
        // SQLite enforces the foreign key that the in-memory test database ignores.
        using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = SqliteDb.Open(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Owner", Email = "owner@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Pavillon", OwnerUserId = user.Id, CreatedAtUtc = DateTime.UtcNow };
        var sam = new Contact { Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = org.Id, Name = "Sam Lee", Email = "sam@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var lead = new Lead { Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = org.Id, Name = "Ada", Email = "ada@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow, ReferredByContactId = sam.Id };
        db.AddRange(user, org, sam, lead);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new ContactRepository(db).DeleteAsync(sam.Id, user.Id, org.Id)).Should().BeTrue();

        db.ChangeTracker.Clear();
        (await db.Leads.SingleAsync(l => l.Id == lead.Id)).ReferredByContactId.Should().BeNull();
    }
}
