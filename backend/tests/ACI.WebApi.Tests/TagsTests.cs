using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>Tags: one coloured list per workspace, on leads, contacts, companies, deals and members.</summary>
public class TagsTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid OwnerId, Guid Org)> WorkspaceAsync(CustomWebApplicationFactory factory, string email = "pierre@example.ch")
    {
        var (owner, ownerId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, ownerId, org);
    }

    private static async Task<Guid> TagAsync(HttpClient client, string name, string? color = null) =>
        (await Ok(await client.PostAsync("/api/tags", Body(new { name, color })), $"add tag {name}")).GetProperty("id").GetGuid();

    private static async Task<Guid> ContactAsync(HttpClient client, string name) =>
        (await Ok(await client.PostAsync("/api/contacts", Body(new { name, email = $"{Guid.NewGuid():N}@example.ch" })), "add a contact"))
            .GetProperty("id").GetGuid();

    [Fact]
    public async Task RepsMakeAndUseTags_ManagersRenameAndDeleteThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await WorkspaceAsync(factory);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);

        var vip = await TagAsync(rep, "  VIP  ", "amber");
        (await rep.PostAsync("/api/tags", Body(new { name = "vip" }))).StatusCode.Should().Be(HttpStatusCode.Conflict, "names are the workspace's alone, ignoring case");
        (await rep.PostAsync("/api/tags", Body(new { name = "Golf", color = "neon" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var contact = await ContactAsync(rep, "Ines Moreau");
        var set = await Ok(await rep.PutAsync("/api/tags/records", Body(new { entityType = "contact", recordId = contact, tagIds = new[] { vip } })), "tag the contact");
        set.EnumerateArray().Select(t => t.GetGuid()).Should().Equal(vip);

        var listed = (await Ok(await rep.GetAsync("/api/tags"), "list tags")).EnumerateArray().Single();
        listed.GetProperty("name").GetString().Should().Be("VIP");
        listed.GetProperty("color").GetString().Should().Be("amber");
        listed.GetProperty("uses").GetInt32().Should().Be(1);

        (await rep.PutAsync($"/api/tags/{vip}", Body(new { name = "Very important" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rep.DeleteAsync($"/api/tags/{vip}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Ok(await owner.PutAsync($"/api/tags/{vip}", Body(new { name = "Very important", color = "red" })), "rename"))
            .GetProperty("name").GetString().Should().Be("Very important");

        (await owner.DeleteAsync($"/api/tags/{vip}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Ok(await rep.GetAsync($"/api/tags/records?entityType=contact&recordId={contact}"), "the contact's tags")).EnumerateArray()
            .Should().BeEmpty("a deleted tag comes off every record");
    }

    [Fact]
    public async Task ABulkChangeTagsOnlyTheRecordsTheCallerCanSee()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var (other, _, _) = await WorkspaceAsync(factory, "rival@example.ch");
        var golf = await TagAsync(owner, "Golf", "green");
        var lapsed = await TagAsync(owner, "Lapsed");
        var a = await ContactAsync(owner, "Ana");
        var b = await ContactAsync(owner, "Bruno");
        var theirs = await ContactAsync(other, "Not yours");
        (await owner.PutAsync("/api/tags/records", Body(new { entityType = "contact", recordId = b, tagIds = new[] { lapsed } })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await Ok(await owner.PostAsync("/api/tags/bulk", Body(new
        {
            entityType = "contact", recordIds = new[] { a, b, theirs }, add = new[] { golf }, remove = new[] { lapsed },
        })), "bulk tag");
        result.GetProperty("records").GetInt32().Should().Be(2);
        result.GetProperty("added").GetInt32().Should().Be(2);
        result.GetProperty("removed").GetInt32().Should().Be(1);
        result.GetProperty("skipped").GetInt32().Should().Be(1, "another workspace's contact is left alone");

        var batch = await Ok(await owner.PostAsync("/api/tags/records/batch", Body(new { entityType = "contact", recordIds = new[] { a, b, theirs } })), "batch read");
        batch.GetProperty(a.ToString()).EnumerateArray().Select(t => t.GetGuid()).Should().Equal(golf);
        batch.GetProperty(b.ToString()).EnumerateArray().Select(t => t.GetGuid()).Should().Equal(golf);
        batch.TryGetProperty(theirs.ToString(), out _).Should().BeFalse();
        (await factory.Services.Read(db => db.RecordTags.Count(t => t.RecordId == theirs))).Should().Be(0);

        // Running it again changes nothing.
        var again = await Ok(await owner.PostAsync("/api/tags/bulk", Body(new { entityType = "contact", recordIds = new[] { a, b }, add = new[] { golf } })), "again");
        again.GetProperty("added").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AnotherWorkspacesTagsCannotBeSeenUsedOrChanged()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await WorkspaceAsync(factory);
        var (other, otherId, otherOrg) = await WorkspaceAsync(factory, "rival@example.ch");
        var mine = await TagAsync(owner, "VIP");
        var theirContact = await ContactAsync(other, "Theirs");
        var myContact = await ContactAsync(owner, "Mine");
        (await owner.PutAsync("/api/tags/records", Body(new { entityType = "contact", recordId = myContact, tagIds = new[] { mine } })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await Ok(await other.GetAsync("/api/tags"), "their tags")).EnumerateArray().Should().BeEmpty();
        (await other.PutAsync($"/api/tags/{mine}", Body(new { name = "Taken" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/tags/{mine}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PutAsync("/api/tags/records", Body(new { entityType = "contact", recordId = theirContact, tagIds = new[] { mine } })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "a tag of another workspace cannot be put on a record");
        (await owner.PutAsync("/api/tags/records", Body(new { entityType = "contact", recordId = theirContact, tagIds = new[] { mine } })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "nor can a record of another workspace be tagged");

        // Read through the workspace filter: the rival workspace sees none of the tags or their uses.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>();
        using (tenant.ActFor(otherOrg, otherId))
        {
            db.Tags.Count().Should().Be(0);
            db.RecordTags.Count().Should().Be(0);
        }
        using (tenant.ActFor(org, ownerId))
        {
            db.Tags.Count().Should().Be(1);
            db.RecordTags.Count().Should().Be(1);
        }
    }

    [Fact]
    public async Task MembersCarryTags_AndTheMembersListFiltersByThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var board = await TagAsync(owner, "Board");
        async Task<Guid> Member(string first) =>
            (await Ok(await owner.PostAsync("/api/members", Body(new { firstName = first, lastName = "Dupont", email = $"{first}@example.ch", tier = "Gold", status = "Active" })),
                $"add {first}")).GetProperty("id").GetGuid();
        var ana = await Member("ana");
        await Member("bruno");
        (await owner.PutAsync("/api/tags/records", Body(new { entityType = "member", recordId = ana, tagIds = new[] { board } })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = await Ok(await owner.GetAsync($"/api/members?tag={board}"), "members with the tag");
        listed.EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).Should().Equal(ana);
        (await Ok(await owner.GetAsync($"/api/members?notTag={board}"), "members without it")).GetArrayLength().Should().Be(1);
        (await Ok(await owner.GetAsync("/api/members"), "all members")).GetArrayLength().Should().Be(2);
    }
}
