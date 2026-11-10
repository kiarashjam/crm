using System.Net;
using System.Text.Json;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An update clears a field sent as null and keeps a field left out, as a JSON merge
/// patch does.
/// </summary>
/// <remarks>
/// Every update read null as "not sent". A contact who left Acme stayed linked to it; a
/// deal kept its owner, company and close date after someone chose none; a member who
/// asked for their mobile number to be deleted kept receiving texts on it. Each form said
/// it had saved.
/// </remarks>
public class ClearFieldsTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid UserId, Guid Org)> WorkspaceAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        client.In(org);
        return (factory, client, userId, org);
    }

    private static async Task<Guid> Id(HttpResponseMessage res) => (await Ok(res)).GetProperty("id").GetGuid();

    [Fact]
    public async Task AContactsCompanyPhoneJobTitleAndNotesCanBeTakenOff()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var company = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme" })));
        var id = await Id(await client.PostAsync("/api/contacts", Body(new
        {
            name = "Anna Keller", email = "anna@example.ch", phone = "+41 79 123 45 67", jobTitle = "CFO", companyId = company, description = "Met at the gala",
        })));

        await Ok(await client.PutAsync($"/api/contacts/{id}", Body(new { companyId = (Guid?)null, phone = (string?)null, jobTitle = (string?)null, description = (string?)null })));

        var stored = await factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id));
        stored.CompanyId.Should().BeNull();
        stored.Phone.Should().BeNull();
        stored.JobTitle.Should().BeNull();
        stored.Description.Should().BeNull();
        stored.Name.Should().Be("Anna Keller", "a field the body did not send is kept");
    }

    [Fact]
    public async Task AFieldLeftOutOfAContactEditIsKept()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var company = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme" })));
        var id = await Id(await client.PostAsync("/api/contacts", Body(new
        {
            name = "Anna Keller", email = "anna@example.ch", phone = "+41 79 123 45 67", jobTitle = "CFO", companyId = company,
        })));

        // Built as a dictionary so nothing but the name is in the body.
        await Ok(await client.PutAsync($"/api/contacts/{id}", Body(new Dictionary<string, object?> { ["name"] = "Anna Keller-Muster" })));

        var stored = await factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id));
        stored.Name.Should().Be("Anna Keller-Muster");
        stored.CompanyId.Should().Be(company);
        stored.Phone.Should().Be("+41 79 123 45 67");
        stored.JobTitle.Should().Be("CFO");
    }

    [Fact]
    public async Task ADealsCompanyContactOwnerCloseDateProbabilityAndDescriptionCanBeTakenOff()
    {
        var (factory, client, _, org) = await WorkspaceAsync();
        using var _f = factory;
        var (_, colleagueId) = await SignInAsync(factory, "colleague@example.ch");
        await AddMemberAsync(factory, org, colleagueId, OrgMemberRole.Member);
        var company = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme" })));
        var contact = await Id(await client.PostAsync("/api/contacts", Body(new { name = "Anna Keller", email = "anna@example.ch" })));
        var id = await Id(await client.PostAsync("/api/deals", Body(new
        {
            name = "Autumn gala", amount = 12000, companyId = company, contactId = contact, assigneeId = colleagueId,
            expectedCloseDateUtc = WorkspaceToday.AddDays(30), probability = 60, description = "Venue booked",
        })));

        await Ok(await client.PutAsync($"/api/deals/{id}", Body(new
        {
            companyId = (Guid?)null, contactId = (Guid?)null, assigneeId = (Guid?)null,
            expectedCloseDateUtc = (DateTime?)null, probability = (int?)null, description = (string?)null,
        })));

        var stored = await factory.Services.Read(db => db.Deals.AsNoTracking().Single(d => d.Id == id));
        stored.CompanyId.Should().BeNull();
        stored.ContactId.Should().BeNull();
        stored.AssigneeId.Should().BeNull();
        stored.ExpectedCloseDateUtc.Should().BeNull();
        stored.Probability.Should().BeNull();
        stored.Description.Should().BeNull();
        stored.Amount.Should().Be(12000m, "a field the body did not send is kept");
    }

    [Fact]
    public async Task ADealFieldLeftOutIsKept()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var company = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme" })));
        var id = await Id(await client.PostAsync("/api/deals", Body(new
        {
            name = "Autumn gala", amount = 12000, companyId = company, probability = 60, description = "Venue booked",
            expectedCloseDateUtc = WorkspaceToday.AddDays(30),
        })));

        // A board move sends the stage alone.
        await Ok(await client.PutAsync($"/api/deals/{id}", Body(new Dictionary<string, object?> { ["stage"] = "Proposal" })));

        var stored = await factory.Services.Read(db => db.Deals.AsNoTracking().Single(d => d.Id == id));
        stored.CompanyId.Should().Be(company);
        stored.Probability.Should().Be(60);
        stored.Description.Should().Be("Venue booked");
        stored.ExpectedCloseDateUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ALeadsCompanyAndPhoneCanBeTakenOff()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var company = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme" })));
        var id = await Id(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Muster", email = "jean@example.ch", source = "Website", status = "New", companyId = company, phone = "+41 79 123 45 67",
            description = "From the open day",
        })));

        await Ok(await client.PutAsync($"/api/leads/{id}", Body(new { companyId = (Guid?)null, phone = (string?)null })));

        var stored = await factory.Services.Read(db => db.Leads.AsNoTracking().Single(l => l.Id == id));
        stored.CompanyId.Should().BeNull("\"No company\" takes the company off, as the timeline says");
        stored.Phone.Should().BeNull();
        stored.Description.Should().Be("From the open day", "a field the body did not send is kept");
    }

    [Fact]
    public async Task ACompanyFieldSentAsNullIsTakenOff()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var id = await Id(await client.PostAsync("/api/companies", Body(new { name = "Acme", industry = "Hospitality", location = "Zurich" })));

        await Ok(await client.PutAsync($"/api/companies/{id}", Body(new { industry = (string?)null })));

        var stored = await factory.Services.Read(db => db.Companies.AsNoTracking().Single(c => c.Id == id));
        stored.Industry.Should().BeNull();
        stored.Location.Should().Be("Zurich");
    }

    [Fact]
    public async Task AMembersPhoneAndNotesCanBeTakenOff()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var id = await Id(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", phone = "+41 79 123 45 67", notes = "Prefers texts", tier = "Gold", status = "Active",
        })));

        await Ok(await client.PutAsync($"/api/members/{id}", Body(new { phone = (string?)null, notes = (string?)null })));

        var stored = await factory.Services.Read(db => db.ClubMembers.AsNoTracking().Single(m => m.Id == id));
        stored.Phone.Should().BeNull();
        stored.Notes.Should().BeNull();
        stored.Tier.Should().Be("Gold", "a field the body did not send is kept");

        // And left out, they are kept.
        await Ok(await client.PutAsync($"/api/members/{id}", Body(new { phone = "+41 79 765 43 21" })));
        await Ok(await client.PutAsync($"/api/members/{id}", Body(new Dictionary<string, object?> { ["tier"] = "Platinum" })));
        (await factory.Services.Read(db => db.ClubMembers.AsNoTracking().Single(m => m.Id == id))).Phone.Should().Be("+41 79 765 43 21");
    }

    [Fact]
    public async Task ARequiredFieldSentAsNullIsLeftAsItIs()
    {
        var (factory, client, _, _) = await WorkspaceAsync();
        using var _f = factory;
        var id = await Id(await client.PostAsync("/api/contacts", Body(new { name = "Anna Keller", email = "anna@example.ch" })));

        await Ok(await client.PutAsync($"/api/contacts/{id}", Body(new { name = (string?)null, email = (string?)null, doNotContact = (bool?)null })));

        var stored = await factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id));
        stored.Name.Should().Be("Anna Keller");
        stored.Email.Should().Be("anna@example.ch");
    }

    [Fact]
    public void TheApiReaderNotesTheFieldsSentAsNullAndARequestBuiltInCodeNotesNone()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ACI.WebApi.Extensions.MergePatchJson.TrackNullsSent(options);

        var read = JsonSerializer.Deserialize<UpdateContactRequest>("""{"name":"Anna","companyId":null,"phone":null}""", options)!;
        read.NullsSent.Should().BeEquivalentTo("CompanyId", "Phone");
        read.Name.Should().Be("Anna");

        var member = JsonSerializer.Deserialize<MemberWriteRequest>("""{"notes":null}""", options)!;
        member.NullsSent.Should().BeEquivalentTo("Notes");

        // An import row sets every column it read, nulls included, and must clear nothing.
        new UpdateContactRequest { Phone = null, CompanyId = null }.NullsSent.Should().BeEmpty();
    }
}
