using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Custom fields of every kind (money, multi select, a link to a record), required and
/// default values, and the typed rows they are kept in.
/// </summary>
public class CustomFieldKindsTests
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

    private static async Task<JsonElement> DefineAsync(HttpClient client, object definition) =>
        await Ok(await client.PostAsync("/api/custom-fields", Body(definition)), "define a field");

    private static async Task<Guid> ContactAsync(HttpClient client, string name) =>
        (await Ok(await client.PostAsync("/api/contacts", Body(new { name, email = $"{Guid.NewGuid():N}@example.ch" })), "add a contact"))
            .GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, string type, Guid record, object values) =>
        client.PutAsync("/api/custom-fields/values", Body(new { entityType = type, recordId = record, values }));

    private static async Task<JsonElement> ValuesAsync(HttpClient client, string type, Guid record) =>
        await Ok(await client.GetAsync($"/api/custom-fields/values?entityType={type}&recordId={record}"), "read values");

    [Fact]
    public async Task EachKindIsKeptInItsTypedRowAndReadBackAsTheApiWritesIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var company = (await Ok(await owner.PostAsync("/api/companies", Body(new { name = "Banque Léman" })), "add a company")).GetProperty("id").GetGuid();
        var contact = await ContactAsync(owner, "Ines Moreau");

        var fee = await DefineAsync(owner, new { entityType = "contact", label = "Joining fee", type = "money" });
        fee.GetProperty("currency").GetString().Should().Be("CHF", "a money field is in the workspace's currency unless it says otherwise");
        await DefineAsync(owner, new { entityType = "contact", label = "Interests", type = "multiselect", options = new[] { "Golf", "Tennis", "Wine" } });
        await DefineAsync(owner, new { entityType = "contact", label = "Employer", type = "link", linkEntityType = "company" });
        await DefineAsync(owner, new { entityType = "contact", label = "Joined", type = "date" });
        await DefineAsync(owner, new { entityType = "contact", label = "Shares", type = "number" });

        (await SaveAsync(owner, "contact", contact, new
        {
            joining_fee = new { amount = 2500.555, currency = "eur" },
            interests = new[] { "wine", "Golf", "golf" },
            employer = company.ToString(),
            joined = "2026-03-01",
            shares = "12.5",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var values = await ValuesAsync(owner, "contact", contact);
        values.GetProperty("joining_fee").GetProperty("amount").GetDecimal().Should().Be(2500.56m, "money is kept to the cent");
        values.GetProperty("joining_fee").GetProperty("currency").GetString().Should().Be("EUR");
        values.GetProperty("interests").EnumerateArray().Select(v => v.GetString()).Should().Equal(new[] { "Golf", "Wine" },
            "options are matched ignoring case, kept once, in the field's order");
        values.GetProperty("employer").GetProperty("id").GetGuid().Should().Be(company);
        values.GetProperty("employer").GetProperty("name").GetString().Should().Be("Banque Léman");
        values.GetProperty("joined").GetString().Should().Be("2026-03-01");
        values.GetProperty("shares").GetDecimal().Should().Be(12.5m);

        var rows = await factory.Services.Read(db => db.CustomFieldEntries.Where(e => e.RecordId == contact).ToList());
        rows.Should().HaveCount(6, "one row each, and one per option chosen");
        rows.Single(r => r.NumberValue == 2500.56m).Currency.Should().Be("EUR");
        rows.Where(r => r.TextKey != null).Select(r => r.TextKey).Should().BeEquivalentTo(new[] { "Golf", "Wine" });
        rows.Single(r => r.LinkId != null).LinkId.Should().Be(company);
        rows.Single(r => r.DateValue != null).DateValue.Should().Be(new DateOnly(2026, 3, 1));

        // Choosing fewer options removes the rows of the ones left out.
        (await SaveAsync(owner, "contact", contact, new { interests = new[] { "Tennis" } })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValuesAsync(owner, "contact", contact)).GetProperty("interests").EnumerateArray().Select(v => v.GetString()).Should().Equal("Tennis");
        (await factory.Services.Read(db => db.CustomFieldEntries.Count(e => e.RecordId == contact))).Should().Be(5);

        (await SaveAsync(owner, "contact", contact, new { interests = new[] { "Squash" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SaveAsync(owner, "contact", contact, new { joining_fee = "a lot" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ARequiredFieldWithAValueCannotBeEmptied_ButAnEmptyOneDoesNotBlockTheRest()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var contact = await ContactAsync(owner, "Ines Moreau");
        await DefineAsync(owner, new { entityType = "contact", label = "Tier", type = "select", options = new[] { "Gold", "Silver" }, required = true });
        await DefineAsync(owner, new { entityType = "contact", label = "Notes", type = "text" });

        // Still empty from before: sending it empty with another field saves the other.
        (await SaveAsync(owner, "contact", contact, new { tier = "", notes = "Lake view" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValuesAsync(owner, "contact", contact)).GetProperty("notes").GetString().Should().Be("Lake view");

        (await SaveAsync(owner, "contact", contact, new { tier = "Gold" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refused = await SaveAsync(owner, "contact", contact, new { tier = (string?)null });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Tier is required");
        (await ValuesAsync(owner, "contact", contact)).GetProperty("tier").GetString().Should().Be("Gold");
    }

    [Fact]
    public async Task ANewRecordStartsWithItsFieldsDefaults_HoweverItIsMade()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var tier = await DefineAsync(owner, new { entityType = "contact", label = "Tier", type = "select", options = new[] { "Gold", "Silver" }, defaultValue = "Silver" });
        tier.GetProperty("defaultValue").GetString().Should().Be("Silver");
        await DefineAsync(owner, new { entityType = "lead", label = "Newsletter", type = "checkbox", defaultValue = true });
        (await owner.PostAsync("/api/custom-fields", Body(new { entityType = "contact", label = "Bad", type = "select", options = new[] { "A" }, defaultValue = "B" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a default is checked like any value");

        var contact = await ContactAsync(owner, "Ines Moreau");
        (await ValuesAsync(owner, "contact", contact)).GetProperty("tier").GetString().Should().Be("Silver");

        var lead = (await Ok(await owner.PostAsync("/api/leads", Body(new { name = "Marc Weber", email = "marc@example.ch", status = "New" })), "add a lead"))
            .GetProperty("id").GetGuid();
        (await ValuesAsync(owner, "lead", lead)).GetProperty("newsletter").GetBoolean().Should().BeTrue();

        // Taking the default off leaves records made afterwards empty, and those before as they are.
        (await owner.PutAsync($"/api/custom-fields/{tier.GetProperty("id").GetGuid()}", Body(new { defaultValue = (string?)null })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var later = await ContactAsync(owner, "Marie Dupont");
        (await ValuesAsync(owner, "contact", later)).TryGetProperty("tier", out _).Should().BeFalse();
        (await ValuesAsync(owner, "contact", contact)).GetProperty("tier").GetString().Should().Be("Silver");
    }

    [Fact]
    public async Task AFieldWithValuesCannotChangeKind_AndDeletingItTakesItsValues()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var contact = await ContactAsync(owner, "Ines Moreau");
        var tier = await DefineAsync(owner, new { entityType = "contact", label = "Tier", type = "text" });
        var id = tier.GetProperty("id").GetGuid();

        // No values yet: the kind can change.
        (await owner.PutAsync($"/api/custom-fields/{id}", Body(new { type = "select", options = new[] { "Gold", "Silver" } })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SaveAsync(owner, "contact", contact, new { tier = "Gold" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PutAsync($"/api/custom-fields/{id}", Body(new { type = "number" })))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "its values would no longer read");
        (await owner.PutAsync($"/api/custom-fields/{id}", Body(new { label = "Membership tier", options = new[] { "Gold", "Silver", "Bronze" } })))
            .StatusCode.Should().Be(HttpStatusCode.OK, "a new label and options keep the kind");

        (await owner.DeleteAsync($"/api/custom-fields/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await factory.Services.Read(db => db.CustomFieldEntries.Count())).Should().Be(0);

        // Added again under the same label, as a dropdown with other options: it starts empty,
        // so the old typed text cannot come back and block saving another field.
        await DefineAsync(owner, new { entityType = "contact", label = "Tier", type = "select", options = new[] { "A", "B" } });
        await DefineAsync(owner, new { entityType = "contact", label = "Notes", type = "text" });
        (await ValuesAsync(owner, "contact", contact)).TryGetProperty("tier", out _).Should().BeFalse();
        (await SaveAsync(owner, "contact", contact, new { notes = "Fine" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ALinkPointsOnlyAtARecordOfTheWorkspace_AndGoesWhenThatRecordIsDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var (other, _, _) = await WorkspaceAsync(factory, "rival@example.ch");
        var theirs = await ContactAsync(other, "Not yours");
        var contact = await ContactAsync(owner, "Ines Moreau");
        var spouse = await ContactAsync(owner, "Marc Moreau");
        await DefineAsync(owner, new { entityType = "contact", label = "Partner", type = "link", linkEntityType = "contact" });
        (await owner.PostAsync("/api/custom-fields", Body(new { entityType = "contact", label = "Bad", type = "link" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a link names the kind of record it points at");

        (await SaveAsync(owner, "contact", contact, new { partner = theirs })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SaveAsync(owner, "contact", contact, new { partner = new { id = spouse } })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValuesAsync(owner, "contact", contact)).GetProperty("partner").GetProperty("name").GetString().Should().Be("Marc Moreau");

        // (A contact's delete runs bulk updates the in-memory test database cannot; a deal's does not.)
        await DefineAsync(owner, new { entityType = "contact", label = "Membership deal", type = "link", linkEntityType = "deal" });
        var deal = (await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Gold membership", value = "2400" })), "add a deal")).GetProperty("id").GetGuid();
        (await SaveAsync(owner, "contact", contact, new { membership_deal = deal })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/deals/{deal}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await ValuesAsync(owner, "contact", contact);
        after.TryGetProperty("membership_deal", out _).Should().BeFalse("a link to a deleted record goes with it");
        after.GetProperty("partner").GetProperty("id").GetGuid().Should().Be(spouse);
    }

    [Fact]
    public async Task TheBatchReadAnswersOnlyForRecordsTheCallerCanSee()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await WorkspaceAsync(factory);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        var (other, _, _) = await WorkspaceAsync(factory, "rival@example.ch");

        await DefineAsync(owner, new { entityType = "deal", label = "Board seat", type = "checkbox" });
        var open = (await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Open deal", value = "1000" })), "add a deal")).GetProperty("id").GetGuid();
        // A private deal of the owner's, which a rep without "every deal" cannot open (as PrivateDealsTests makes one).
        var hidden = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            db.Deals.Add(new Deal
            {
                Id = hidden, UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Secret", Value = "1000", Currency = "CHF",
                IsPrivate = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        (await SaveAsync(owner, "deal", open, new { board_seat = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SaveAsync(owner, "deal", hidden, new { board_seat = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var request = Body(new { entityType = "deal", recordIds = new[] { open, hidden } });
        var seen = await Ok(await rep.PostAsync("/api/custom-fields/values/batch", request), "the rep reads the board's values");
        seen.TryGetProperty(open.ToString(), out var values).Should().BeTrue();
        values.GetProperty("board_seat").GetBoolean().Should().BeTrue();
        seen.TryGetProperty(hidden.ToString(), out _).Should().BeFalse("a private deal's values stay with it");

        var foreign = await Ok(await other.PostAsync("/api/custom-fields/values/batch", Body(new { entityType = "deal", recordIds = new[] { open } })),
            "another workspace asks");
        foreign.EnumerateObject().Should().BeEmpty();
        (await SaveAsync(other, "deal", open, new { board_seat = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherWorkspacesValuesAreNotReadThroughTheWorkspaceFilter()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await WorkspaceAsync(factory);
        var contact = await ContactAsync(owner, "Ines Moreau");
        await DefineAsync(owner, new { entityType = "contact", label = "Tier", type = "text" });
        (await SaveAsync(owner, "contact", contact, new { tier = "Gold" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (_, otherId, otherOrg) = await WorkspaceAsync(factory, "rival@example.ch");

        using var scope = factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
        using (tenant.ActFor(otherOrg, otherId))
            (await db.CustomFieldEntries.CountAsync()).Should().Be(0, "another workspace sees none of these values");
        using (tenant.ActFor(org, ownerId))
            (await db.CustomFieldEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MembersCarryFieldsInAnOrganizationOnly()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        await DefineAsync(owner, new { entityType = "member", label = "Locker", type = "text", defaultValue = "Unassigned" });
        var member = (await Ok(await owner.PostAsync("/api/members", Body(new { firstName = "Ana", lastName = "Silva", email = "ana@example.ch", tier = "Gold", status = "Active" })),
            "add a member")).GetProperty("id").GetGuid();
        (await ValuesAsync(owner, "member", member)).GetProperty("locker").GetString().Should().Be("Unassigned");
        (await SaveAsync(owner, "member", member, new { locker = "B12" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValuesAsync(owner, "member", member)).GetProperty("locker").GetString().Should().Be("B12");

        var (personal, _) = await SignInAsync(factory, "solo@example.ch");
        (await personal.PostAsync("/api/custom-fields", Body(new { entityType = "member", label = "Locker", type = "text" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "members exist only in an organization");
    }

    [Fact]
    public async Task FieldsCanBePutInOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var a = (await DefineAsync(owner, new { entityType = "lead", label = "A", type = "text" })).GetProperty("id").GetGuid();
        var b = (await DefineAsync(owner, new { entityType = "lead", label = "B", type = "text" })).GetProperty("id").GetGuid();
        var ordered = await Ok(await owner.PutAsync("/api/custom-fields/order", Body(new { entityType = "lead", ids = new[] { b, a } })), "reorder");
        ordered.EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).Should().Equal(b, a);
        (await owner.PutAsync("/api/custom-fields/order", Body(new { entityType = "lead", ids = new[] { b } })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "every field, each once");
    }
}
