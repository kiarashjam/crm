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
/// Who is whose spouse, partner, parent and so on, so the couples package can be given
/// to the right two people even when they do not share a last name.
/// </summary>
public class ContactRelationshipTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> SeedContactAsync(CustomWebApplicationFactory factory, Guid userId, Guid orgId, string name, bool archived = false)
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
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    [Fact]
    public async Task ASpouseSetOnTheLeadPage_IsSeenFromBothSides_AndCanBeChangedAndRemoved()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var marc = await SeedContactAsync(factory, ownerId, org, "Marc Favre");

        var lead = await Json(await owner.In(org).PostAsync("/api/leads", Body(new { name = "Claire Dubois", email = "claire@example.ch", status = "New" })));
        var leadId = lead.GetProperty("id").GetGuid();

        var none = await Json(await rep.In(org).GetAsync($"/api/leads/{leadId}/relationships"));
        var claire = none.GetProperty("contactId").GetGuid();
        none.GetProperty("items").GetArrayLength().Should().Be(0);

        var added = await Json(await owner.In(org).PostAsync($"/api/leads/{leadId}/relationships", Body(new { relatedContactId = marc, type = "Spouse" })));
        added.GetProperty("type").GetString().Should().Be("spouse");
        added.GetProperty("relatedContactName").GetString().Should().Be("Marc Favre");
        var relationshipId = added.GetProperty("id").GetGuid();

        // A teammate sees it on the lead, and on Marc's contact from the other side.
        var onLead = await Json(await rep.In(org).GetAsync($"/api/leads/{leadId}/relationships"));
        onLead.GetProperty("items").EnumerateArray().Single().GetProperty("relatedContactId").GetGuid().Should().Be(marc);
        var onMarc = await Json(await rep.In(org).GetAsync($"/api/contacts/{marc}/relationships"));
        var fromMarc = onMarc.GetProperty("items").EnumerateArray().Single();
        fromMarc.GetProperty("relatedContactId").GetGuid().Should().Be(claire);
        fromMarc.GetProperty("relatedLeadId").GetGuid().Should().Be(leadId);
        fromMarc.GetProperty("type").GetString().Should().Be("spouse");

        // Not married, but partners: changed from Marc's side.
        var changed = await Json(await rep.In(org).PutAsync($"/api/contacts/{marc}/relationships/{relationshipId}", Body(new { relatedContactId = claire, type = "partner" })));
        changed.GetProperty("type").GetString().Should().Be("partner");
        (await Json(await owner.In(org).GetAsync($"/api/leads/{leadId}/relationships")))
            .GetProperty("items")[0].GetProperty("type").GetString().Should().Be("partner");

        (await rep.In(org).DeleteAsync($"/api/leads/{leadId}/relationships/{relationshipId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Json(await owner.In(org).GetAsync($"/api/contacts/{marc}/relationships"))).GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AParentIsSeenAsAChildFromTheOtherSide()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var harry = await SeedContactAsync(factory, ownerId, org, "Harry Martin");
        var anne = await SeedContactAsync(factory, ownerId, org, "Anne Martin");

        // Anne is Harry's parent.
        await Json(await owner.In(org).PostAsync($"/api/contacts/{harry}/relationships", Body(new { relatedContactId = anne, type = "parent" })));

        var fromAnne = await Json(await owner.In(org).GetAsync($"/api/contacts/{anne}/relationships"));
        fromAnne.GetProperty("items")[0].GetProperty("type").GetString().Should().Be("child");
        fromAnne.GetProperty("items")[0].GetProperty("relatedContactName").GetString().Should().Be("Harry Martin");
    }

    [Fact]
    public async Task OnlyOneSpouseOrPartner_OnlyOneRelationshipPerPair_AndOnlyActiveContactsInTheWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var other = await CreateAsync(factory, ownerId, "Other club");
        var claire = await SeedContactAsync(factory, ownerId, org, "Claire Dubois");
        var marc = await SeedContactAsync(factory, ownerId, org, "Marc Favre");
        var paul = await SeedContactAsync(factory, ownerId, org, "Paul Roux");
        var gone = await SeedContactAsync(factory, ownerId, org, "Gone Person", archived: true);
        var outsider = await SeedContactAsync(factory, ownerId, other, "Outside Person");

        await Json(await owner.In(org).PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = marc, type = "spouse" })));

        foreach (var (related, type) in new[] { (claire, "spouse"), (gone, "friend"), (outsider, "friend"), (Guid.NewGuid(), "friend"), (paul, "neighbour") })
        {
            var res = await owner.In(org).PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = related, type }));
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{type} with {related}");
        }

        // Claire already has a spouse, and so does Marc from the other side.
        (await owner.In(org).PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = paul, type = "partner" })))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.In(org).PostAsync($"/api/contacts/{paul}/relationships", Body(new { relatedContactId = marc, type = "spouse" })))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        // The two are related already, whichever side asks.
        (await owner.In(org).PostAsync($"/api/contacts/{marc}/relationships", Body(new { relatedContactId = claire, type = "friend" })))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Anything that is not a couple is fine alongside one.
        await Json(await owner.In(org).PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = paul, type = "sibling" })));

        // Another workspace cannot read them.
        (await owner.In(other).GetAsync($"/api/contacts/{claire}/relationships")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AViewerCanReadButNotChangeThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        var claire = await SeedContactAsync(factory, ownerId, org, "Claire Dubois");
        var marc = await SeedContactAsync(factory, ownerId, org, "Marc Favre");

        (await viewer.In(org).PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = marc, type = "spouse" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await Json(await viewer.In(org).GetAsync($"/api/contacts/{claire}/relationships"));
    }

    [Fact]
    public async Task MergingMovesRelationshipsToTheContactKept_AndDeletingRemovesThem_UnderRealForeignKeys()
    {
        // SQLite enforces the foreign keys that the in-memory test database ignores.
        using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = SqliteDb.Open(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Owner", Email = "owner@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Pavillon", OwnerUserId = user.Id, CreatedAtUtc = DateTime.UtcNow };
        Contact Person(string name) => new() { Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = org.Id, Name = name, Email = $"{Guid.NewGuid():N}@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var keep = Person("Marc Favre");
        var drop = Person("Marc Favre (old)");
        var claire = Person("Claire Dubois");
        var other = Person("Someone Else");
        var harry = Person("Harry Favre");
        ContactRelationship Link(Contact a, Contact b, string type) => new()
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = org.Id, ContactId = a.Id, RelatedContactId = b.Id, Type = type, CreatedAtUtc = DateTime.UtcNow,
        };
        db.AddRange(user, org, keep, drop, claire, other, harry);
        db.AddRange(
            Link(keep, claire, ContactRelationshipTypes.Spouse),
            Link(drop, other, ContactRelationshipTypes.Partner), // a second partner: removed
            Link(claire, drop, ContactRelationshipTypes.Friend), // Claire is related to the kept one already: removed
            Link(drop, keep, ContactRelationshipTypes.Sibling),  // the person and themselves: removed
            Link(harry, drop, ContactRelationshipTypes.Parent)); // moves
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var contacts = new ContactRepository(db);
        (await contacts.MergeIntoAsync(keep.Id, drop.Id, user.Id, org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        var left = await db.ContactRelationships.AsNoTracking().ToListAsync();
        left.Should().HaveCount(2);
        left.Should().Contain(r => r.ContactId == keep.Id && r.RelatedContactId == claire.Id && r.Type == ContactRelationshipTypes.Spouse);
        left.Should().Contain(r => r.ContactId == harry.Id && r.RelatedContactId == keep.Id && r.Type == ContactRelationshipTypes.Parent);

        (await contacts.DeleteAsync(keep.Id, user.Id, org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();
        (await db.ContactRelationships.CountAsync()).Should().Be(0);
    }
}
