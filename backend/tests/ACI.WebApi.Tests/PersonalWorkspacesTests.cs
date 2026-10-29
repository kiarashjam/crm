using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// With no organization, a workspace is the user's own: two such users do not see,
/// match or link each other's records.
/// </summary>
/// <remarks>
/// Several queries scoped a personal workspace by "OrganizationId IS NULL" alone, which
/// is every personal user at once. The duplicate warning named another user's lead, a
/// new lead was linked to another user's contact (and showed that contact's family) or
/// company, check-email said whether someone else had a contact, and the activity
/// lookups returned other users' activities and user ids.
/// </remarks>
public class PersonalWorkspacesTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> NewLead(HttpClient client, string name, string email) =>
        client.PostAsync("/api/leads", Body(new { name, email, status = "New" }));

    [Fact]
    public async Task AnotherUsersLeadIsNotADuplicate()
    {
        using var factory = new CustomWebApplicationFactory();
        var (first, _) = await SignInAsync(factory, "first@example.ch");
        var (second, _) = await SignInAsync(factory, "second@example.ch");
        await Ok(await NewLead(first, "Jane Doe", "jane@example.ch"), "the first user's lead");

        var res = await NewLead(second, "J. Doe", "jane@example.ch");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"it is not a duplicate in the second user's workspace: {body}");
        body.Should().NotContain("Jane Doe", "the warning used to name the other user's lead");

        (await NewLead(second, "Jane again", "jane@example.ch")).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the second user's own lead still counts");
    }

    [Fact]
    public async Task ALeadIsLinkedOnlyToItsOwnersContactAndCompany()
    {
        using var factory = new CustomWebApplicationFactory();
        var (first, firstId) = await SignInAsync(factory, "first@example.ch");
        var (second, secondId) = await SignInAsync(factory, "second@example.ch");

        // The first user's contact Jane, with her husband recorded.
        Guid janeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jane = new Contact { Id = Guid.NewGuid(), UserId = firstId, Name = "Jane Doe", Email = "jane@doe-holding.ch", CreatedAtUtc = DateTime.UtcNow };
            var john = new Contact { Id = Guid.NewGuid(), UserId = firstId, Name = "John Doe", Email = "john@doe-holding.ch", CreatedAtUtc = DateTime.UtcNow };
            db.Contacts.AddRange(jane, john);
            db.ContactRelationships.Add(new ContactRelationship
            {
                Id = Guid.NewGuid(), UserId = firstId, ContactId = jane.Id, RelatedContactId = john.Id, Type = ContactRelationshipTypes.Spouse, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            janeId = jane.Id;
        }
        await Ok(await NewLead(first, "Max", "max@doe-holding.ch"), "the first user's lead, which makes their company");

        var lead = await Ok(await NewLead(second, "Jane", "jane@doe-holding.ch"), "the second user's lead");
        var leadId = lead.GetProperty("id").GetGuid();

        var saved = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId));
        saved.ConvertedToContactId.Should().NotBe(janeId, "the email matched the other user's contact");
        var contact = await factory.Services.Read(db => db.Contacts.Single(c => c.Id == saved.ConvertedToContactId));
        contact.UserId.Should().Be(secondId);
        var company = await factory.Services.Read(db => db.Companies.Single(c => c.Id == saved.CompanyId));
        company.UserId.Should().Be(secondId, "the domain matched the other user's company");
        (await factory.Services.Read(db => db.Contacts.Single(c => c.Id == janeId).ConvertedFromLeadId))
            .Should().BeNull("the other user's contact is not claimed by this lead");

        var list = await second.GetAsync("/api/leads/all");
        (await list.Content.ReadAsStringAsync()).Should().NotContain("John Doe", "the other user's contact's husband");
    }

    [Fact]
    public async Task ALeadAlreadyLinkedToAnotherUsersContactNoLongerShowsTheirFamily()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, firstId) = await SignInAsync(factory, "first@example.ch");
        var (second, secondId) = await SignInAsync(factory, "second@example.ch");

        // Left behind by the old match: the second user's lead points at the first user's contact.
        Guid leadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jane = new Contact { Id = Guid.NewGuid(), UserId = firstId, Name = "Jane Doe", Email = "jane@example.ch", CreatedAtUtc = DateTime.UtcNow };
            var john = new Contact { Id = Guid.NewGuid(), UserId = firstId, Name = "John Doe", Email = "john@example.ch", CreatedAtUtc = DateTime.UtcNow };
            var lead = new Lead { Id = Guid.NewGuid(), UserId = secondId, Name = "Jane", Email = "jane@example.ch", Status = "New", ConvertedToContactId = jane.Id, CreatedAtUtc = DateTime.UtcNow };
            jane.ConvertedFromLeadId = lead.Id;
            db.AddRange(jane, john, lead);
            db.ContactRelationships.Add(new ContactRelationship
            {
                Id = Guid.NewGuid(), UserId = firstId, ContactId = jane.Id, RelatedContactId = john.Id, Type = ContactRelationshipTypes.Spouse, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            leadId = lead.Id;
        }

        (await (await second.GetAsync("/api/leads/all")).Content.ReadAsStringAsync()).Should().NotContain("John Doe");

        // The next save of the lead gives it a contact of its own.
        await Ok(await second.PutAsync($"/api/leads/{leadId}", Body(new { phone = "+41 79 000 00 00" })), "editing the lead");
        var contactId = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).ConvertedToContactId);
        (await factory.Services.Read(db => db.Contacts.Single(c => c.Id == contactId).UserId)).Should().Be(secondId);
    }

    [Fact]
    public async Task AnotherUsersContactEmailIsFreeToUse()
    {
        using var factory = new CustomWebApplicationFactory();
        var (first, _) = await SignInAsync(factory, "first@example.ch");
        var (second, _) = await SignInAsync(factory, "second@example.ch");
        await Ok(await first.PostAsync("/api/contacts", Body(new { name = "Front desk", email = "info@hotel-x.ch" })), "the first user's contact");

        (await Ok(await second.GetAsync("/api/contacts/check-email?email=info@hotel-x.ch"), "check-email"))
            .GetProperty("exists").GetBoolean().Should().BeFalse("it said whether another user had that person");
        await Ok(await second.PostAsync("/api/contacts", Body(new { name = "Reception", email = "info@hotel-x.ch" })), "the second user's contact");
        (await second.PostAsync("/api/contacts", Body(new { name = "Reception", email = "info@hotel-x.ch" }))).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the second user's own contact still counts");
    }

    [Fact]
    public async Task AnotherUsersActivitiesStayTheirs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (first, firstId) = await SignInAsync(factory, "first@example.ch");
        var (second, _) = await SignInAsync(factory, "second@example.ch");
        var lead = await Ok(await NewLead(first, "Jane", "jane@example.ch"), "the first user's lead");
        var leadId = lead.GetProperty("id").GetGuid();
        await Ok(await first.PostAsync("/api/activities", Body(new { type = "call", subject = "Agreed 20% off", leadId })), "logging a call");

        (await Ok(await second.GetAsync($"/api/activities/lead/{leadId}"), "their lead's activities")).GetArrayLength().Should().Be(0);
        (await Ok(await second.GetAsync($"/api/activities/by-leads?ids={leadId}"), "the batched lookup")).GetArrayLength().Should().Be(0);
        var counts = await Ok(await second.GetAsync("/api/activities/org-member-counts"), "the counts");
        counts.TryGetProperty(firstId.ToString(), out _).Should().BeFalse("it listed every personal user with activities");

        (await Ok(await first.GetAsync($"/api/activities/lead/{leadId}"), "the owner's own view")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ContactEmailsAreUniquePerWorkspaceInTheDatabase()
    {
        // The index the startup DDL puts in place, as the model declares it, on a
        // database that enforces it.
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var db = SqliteDb.Open(connection);
        var first = new User { Id = Guid.NewGuid(), Name = "First", Email = "first@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var second = new User { Id = Guid.NewGuid(), Name = "Second", Email = "second@example.ch", CreatedAtUtc = DateTime.UtcNow };
        db.Users.AddRange(first, second);
        var org = new Organization { Id = Guid.NewGuid(), Name = "Pavillon46", OwnerUserId = first.Id, CreatedAtUtc = DateTime.UtcNow };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        Contact Contact(Guid userId, Guid? orgId) => new() { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Name = "Info", Email = "info@hotel-x.ch", CreatedAtUtc = DateTime.UtcNow };

        db.Contacts.AddRange(Contact(first.Id, null), Contact(second.Id, null), Contact(first.Id, org.Id));
        await db.SaveChangesAsync();

        db.Contacts.Add(Contact(second.Id, null));
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("UNIQUE", "one user's own contacts are still unique");
        db.ChangeTracker.Clear();

        db.Contacts.Add(Contact(second.Id, org.Id));
        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>("an organization's contacts are unique whoever adds them");
    }
}
