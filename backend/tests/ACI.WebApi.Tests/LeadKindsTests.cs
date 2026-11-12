using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A lead is a person or an establishment (a restaurant, say), as the workspace says. A
/// workspace of people (a club) works as it always has and takes no establishments from
/// anywhere; one of establishments adds a place with no email, makes it a company, and
/// writes to its contact person.
/// </summary>
public class LeadKindsTests
{
    private const string FormToken = "fedcba9876543210fedcba9876543210";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage res, HttpStatusCode expected, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory App, HttpClient Owner, Guid Org, Guid OwnerId)> WorkspaceAsync(string? leadKinds)
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@example.ch");
        var org = await CreateAsync(app, ownerId, leadKinds == null ? "Pavillon 46" : "Bonap");
        owner.In(org);
        if (leadKinds != null)
            await JsonOf(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { leadKinds })), HttpStatusCode.OK, "set lead kinds");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LeadCaptureForms.Add(new LeadCaptureForm
        {
            Id = Guid.NewGuid(), OrganizationId = org, CreatedByUserId = ownerId, Name = "Website",
            PublicToken = FormToken, IsActive = true, CreatedAtUtc = DateTime.UtcNow, SuccessMessage = "Merci.",
        });
        var row = await db.Organizations.FindAsync(org);
        row!.WebhookApiKeyHash = ACI.Application.Services.OrganizationService.HashApiKey("kinds-key");
        await db.SaveChangesAsync();
        return (app, owner, org, ownerId);
    }

    private static object Pizzeria(string name = "Pizzeria Roma", string city = "Zürich", string contactPersonEmail = "marco@roma.ch") => new
    {
        kind = "establishment",
        status = "New",
        name,
        establishmentType = "restaurant",
        addressLine = "Langstrasse 12",
        postalCode = "8004",
        city,
        country = "Switzerland",
        website = "https://www.pizzeria-roma.ch/menu",
        seats = 60,
        contactPersonName = "Marco Rossi",
        contactPersonRole = "Owner",
        contactPersonEmail,
        contactPersonPhone = "+41 44 000 00 00",
    };

    [Fact]
    public async Task AWorkspaceOfPeople_WorksAsItAlwaysHas_AndTakesNoEstablishmentsFromAnywhere()
    {
        var (app, owner, org, _) = await WorkspaceAsync(null);
        using var _app = app;

        var settings = await JsonOf(await owner.GetAsync($"/api/organizations/{org}"), HttpStatusCode.OK, "read workspace");
        settings.GetProperty("leadKinds").GetString().Should().Be("person");

        // A person, as ever: a name and an email, and the lead says it is a person.
        var person = await JsonOf(await owner.PostAsync("/api/leads", Body(new { name = "Camille Laurent", email = "camille@example.ch", status = "New" })),
            HttpStatusCode.OK, "a person");
        person.GetProperty("kind").GetString().Should().Be("person");
        (await owner.PostAsync("/api/leads", Body(new { name = "No Email", status = "New" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a person still needs an email");

        // No establishment by the app, the webhook, the public form or an import.
        var refused = await JsonOf(await owner.PostAsync("/api/leads", Body(Pizzeria())), HttpStatusCode.BadRequest, "establishment by the app");
        refused.GetProperty("errorCode").GetString().Should().Be("Lead.KindNotUsed");
        (await owner.PutAsync($"/api/leads/{person.GetProperty("id").GetGuid()}", Body(new { kind = "establishment" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "nor by turning a person into one");

        var hook = app.CreateClient();
        hook.DefaultRequestHeaders.Add("X-Api-Key", "kinds-key");
        (await hook.PostAsync("/api/webhook/leads", Body(new { kind = "establishment", name = "Pizzeria Roma" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the webhook refuses one");
        (await hook.PostAsync("/api/webhook/leads", Body(new { name = "Pizzeria Roma" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "and a lead without an email is a person without one");

        var stranger = app.CreateClient();
        var form = await JsonOf(await stranger.GetAsync($"/api/public/capture/{FormToken}"), HttpStatusCode.OK, "public form");
        form.GetProperty("leadKinds").GetString().Should().Be("person");
        var captured = await stranger.PostAsync($"/api/public/capture/{FormToken}", Body(new
        {
            kind = "establishment", name = "Marco Rossi", email = "marco@roma.ch", establishmentName = "Pizzeria Roma", city = "Zürich",
        }));
        captured.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the public form refuses one");
        (await captured.Content.ReadAsStringAsync()).Should().NotContain("Settings", "a stranger is not told about the workspace's settings");

        var fields = (await OkJson(await Analyze(owner, "lead", "Name,Email,City\nAda,ada@example.ch,Bern\n"), "analyze"))
            .GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("key").GetString()).ToList();
        fields.Should().NotContain(new[] { "kind", "city", "establishmentType", "contactPersonName" }, "a workspace of people imports as it always has");
        fields.Should().Contain("email");

        (await app.Services.Read(db => db.Leads.Count(l => l.OrganizationId == org))).Should().Be(1, "only the person was saved");
    }

    [Fact]
    public async Task WhatAWorkspacesLeadsAre_IsOneOfThree()
    {
        var (app, owner, org, _) = await WorkspaceAsync(null);
        using var _app = app;
        var refused = await JsonOf(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { leadKinds = "robots" })),
            HttpStatusCode.BadRequest, "not a mode");
        refused.GetProperty("errorCode").GetString().Should().Be("Organization.InvalidLeadKinds");
        foreach (var mode in new[] { "both", "establishment", "person" })
        {
            var saved = await JsonOf(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { leadKinds = mode })), HttpStatusCode.OK, mode);
            saved.GetProperty("leadKinds").GetString().Should().Be(mode);
        }
        // Another setting saved later leaves it as it is.
        var other = await JsonOf(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" })), HttpStatusCode.OK, "currency");
        other.GetProperty("leadKinds").GetString().Should().Be("person");
    }

    [Fact]
    public async Task AnEstablishment_NeedsNoEmail_IsItsOwnCompany_AndItsContactPersonBecomesTheContact()
    {
        var (app, owner, org, _) = await WorkspaceAsync("both");
        using var _app = app;

        var lead = await JsonOf(await owner.PostAsync("/api/leads", Body(Pizzeria())), HttpStatusCode.OK, "establishment");
        lead.GetProperty("kind").GetString().Should().Be("establishment");
        lead.GetProperty("email").GetString().Should().BeEmpty();
        lead.GetProperty("city").GetString().Should().Be("Zürich");
        lead.GetProperty("seats").GetInt32().Should().Be(60);
        lead.GetProperty("contactPersonName").GetString().Should().Be("Marco Rossi");
        var leadId = lead.GetProperty("id").GetGuid();

        // Another place with no email is not a duplicate of the first (no email is nobody's).
        await JsonOf(await owner.PostAsync("/api/leads", Body(new { kind = "establishment", name = "Café Odeon", city = "Zürich", status = "New" })),
            HttpStatusCode.OK, "a second place without an email");
        // The same place twice is.
        var again = await JsonOf(await owner.PostAsync("/api/leads", Body(Pizzeria(name: "pizzeria roma", city: "zürich"))),
            HttpStatusCode.Conflict, "the same place again");
        again.GetProperty("errorCode").GetString().Should().Be("Lead.Duplicate");
        // A person is still a person here.
        await JsonOf(await owner.PostAsync("/api/leads", Body(new { kind = "person", name = "Ada", email = "ada@example.ch", status = "New" })), HttpStatusCode.OK, "a person");
        (await owner.PostAsync("/api/leads", Body(new { kind = "person", name = "Ada Two", status = "New" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A type it does not know is refused.
        (await owner.PostAsync("/api/leads", Body(new { kind = "establishment", name = "Spaceport", establishmentType = "spaceport", status = "New" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (company, contact) = await app.Services.Read(db =>
        {
            var row = db.Leads.IgnoreQueryFilters().Single(l => l.Id == leadId);
            return (db.Companies.IgnoreQueryFilters().Single(c => c.Id == row.CompanyId),
                db.Contacts.IgnoreQueryFilters().Single(c => c.ConvertedFromLeadId == leadId));
        });
        company.Name.Should().Be("Pizzeria Roma");
        company.Domain.Should().Be("pizzeria-roma.ch");
        company.Industry.Should().Be("Restaurant");
        company.Location.Should().Be("Langstrasse 12, 8004 Zürich, Switzerland");
        contact.Name.Should().Be("Marco Rossi", "the contact is the person at the place, not the place");
        contact.Email.Should().Be("marco@roma.ch");
        contact.JobTitle.Should().Be("Owner");
        contact.CompanyId.Should().Be(company.Id);

        // Edited: the address taken off, the seats changed; an empty email is fine for a place.
        var edited = await JsonOf(await owner.PutAsync($"/api/leads/{leadId}", Body(new { addressLine = (string?)null, seats = 80, email = "" })),
            HttpStatusCode.OK, "edit");
        edited.GetProperty("seats").GetInt32().Should().Be(80);
        edited.GetProperty("addressLine").ValueKind.Should().Be(JsonValueKind.Null);
        // Searching by city finds it.
        var found = await JsonOf(await owner.GetAsync("/api/leads?search=zürich&page=1&pageSize=20"), HttpStatusCode.OK, "search");
        found.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public async Task AnEstablishment_BecomesADealWithoutAPerson_AndAContactOnlyFromItsContactPerson()
    {
        var (app, owner, org, _) = await WorkspaceAsync("establishment");
        using var _app = app;

        // In a workspace of establishments, a lead with no kind is one.
        var bare = await JsonOf(await owner.PostAsync("/api/leads", Body(new { name = "Bäckerei Frei", city = "Bern", establishmentType = "bakery", status = "New" })),
            HttpStatusCode.OK, "no kind given");
        bare.GetProperty("kind").GetString().Should().Be("establishment");
        var bareId = bare.GetProperty("id").GetGuid();
        (await owner.PostAsync("/api/leads", Body(new { kind = "person", name = "Ada", email = "ada@example.ch", status = "New" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a workspace of establishments takes no people");

        var noPerson = await JsonOf(await owner.PostAsync($"/api/leads/{bareId}/convert", Body(new { createContact = true, createDeal = true, dealName = "Bonap" })),
            HttpStatusCode.BadRequest, "a contact with nobody named");
        noPerson.GetProperty("errorCode").GetString().Should().Be("Lead.ContactPersonMissing");
        (await app.Services.Read(db => db.Deals.Count(d => d.OrganizationId == org))).Should().Be(0, "nothing half made");

        var dealOnly = await JsonOf(await owner.PostAsync($"/api/leads/{bareId}/convert", Body(new { createContact = false, createDeal = true, dealName = "Bonap" })),
            HttpStatusCode.OK, "a deal only");
        dealOnly.GetProperty("dealId").GetGuid().Should().NotBeEmpty();
        dealOnly.GetProperty("contactId").ValueKind.Should().Be(JsonValueKind.Null);

        var lead = await JsonOf(await owner.PostAsync("/api/leads", Body(Pizzeria())), HttpStatusCode.OK, "with a contact person");
        var converted = await JsonOf(await owner.PostAsync($"/api/leads/{lead.GetProperty("id").GetGuid()}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Bonap Pro" })), HttpStatusCode.OK, "convert");
        var contactId = converted.GetProperty("contactId").GetGuid();
        var contact = await app.Services.Read(db => db.Contacts.IgnoreQueryFilters().Single(c => c.Id == contactId));
        contact.Name.Should().Be("Marco Rossi");
        contact.Email.Should().Be("marco@roma.ch");
    }

    [Fact]
    public async Task TheWebhookAndThePublicForm_TakeEstablishments_AndKnowAReturningOne()
    {
        var (app, owner, org, _) = await WorkspaceAsync("establishment");
        using var _app = app;

        var hook = app.CreateClient();
        hook.DefaultRequestHeaders.Add("X-Api-Key", "kinds-key");
        var first = await JsonOf(await hook.PostAsync("/api/webhook/leads",
            Body(new { name = "Pizzeria Roma", city = "Zürich", establishmentType = "restaurant", contactPersonName = "Marco Rossi", contactPersonEmail = "marco@roma.ch" })),
            HttpStatusCode.Created, "webhook, no email");
        first.GetProperty("kind").GetString().Should().Be("establishment");
        var again = await JsonOf(await hook.PostAsync("/api/webhook/leads", Body(new { name = "Pizzeria Roma", city = "Zürich", source = "Zapier" })),
            HttpStatusCode.OK, "the same place again");
        again.GetProperty("duplicate").GetBoolean().Should().BeTrue();
        again.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());

        var stranger = app.CreateClient();
        var form = await JsonOf(await stranger.GetAsync($"/api/public/capture/{FormToken}"), HttpStatusCode.OK, "form");
        form.GetProperty("leadKinds").GetString().Should().Be("establishment");
        // Whoever fills the form in is the place's contact person; "website" stays the honeypot.
        await JsonOf(await stranger.PostAsync($"/api/public/capture/{FormToken}", Body(new
        {
            name = "Lena Frei", email = "lena@frei.ch", phone = "+41 31 000 00 00", jobTitle = "Manager",
            establishmentName = "Bäckerei Frei", establishmentType = "bakery", addressLine = "Marktgasse 1", postalCode = "3011", city = "Bern",
            businessWebsite = "frei-beck.ch",
        })), HttpStatusCode.Created, "form, establishment");
        var place = await app.Services.Read(db => db.Leads.IgnoreQueryFilters().Single(l => l.OrganizationId == org && l.Name == "Bäckerei Frei"));
        place.Kind.Should().Be("establishment");
        place.Email.Should().BeEmpty();
        place.ContactPersonName.Should().Be("Lena Frei");
        place.ContactPersonEmail.Should().Be("lena@frei.ch");
        place.ContactPersonRole.Should().Be("Manager");
        place.Website.Should().Be("frei-beck.ch");
        (await stranger.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Lena Frei", email = "lena@frei.ch", city = "Bern" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "an establishment form without the establishment's name");

        // Sent again from the form: recorded on the lead it already has.
        await JsonOf(await stranger.PostAsync($"/api/public/capture/{FormToken}", Body(new
        {
            name = "Lena Frei", email = "lena@frei.ch", establishmentName = "Bäckerei Frei", city = "Bern",
        })), HttpStatusCode.Created, "form again");
        (await app.Services.Read(db => db.Leads.IgnoreQueryFilters().Count(l => l.OrganizationId == org && l.Name == "Bäckerei Frei"))).Should().Be(1);
    }

    [Fact]
    public async Task AFileOfRestaurants_Imports_WithoutEmails_AndMatchesAPlaceByNameAndCity()
    {
        var (app, owner, org, _) = await WorkspaceAsync("establishment");
        using var _app = app;

        const string csv = "Restaurant,Typ,Adresse,PLZ,Ort,Kontaktperson,Contact email,Seats\n"
            + "Pizzeria Roma,Pizzeria,Langstrasse 12,8004,Zürich,Marco Rossi,marco@roma.ch,60\n"
            + "Café Odeon,café,Limmatquai 2,8001,Zürich,,,\n"
            + "Hotel Bellevue,Spaceport,,,Bern,,,lots\n";
        var analysis = await OkJson(await Analyze(owner, "lead", csv), "analyze");
        var suggested = analysis.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToList();
        suggested.Should().Equal("name", "establishmentType", "addressLine", "postalCode", "city", "contactPersonName", "contactPersonEmail", "seats");
        analysis.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("key").GetString() == "email")
            .GetProperty("required").GetBoolean().Should().BeFalse();

        var columns = suggested.ToArray();
        var result = await OkJson(await Import(owner, "lead", csv, columns), "import");
        Action(result, 2).Should().Be("create");
        Action(result, 3).Should().Be("create");
        Action(result, 4).Should().Be("error");
        Message(result, 4).Should().Contain("Seats");

        var roma = await app.Services.Read(db => db.Leads.IgnoreQueryFilters().Single(l => l.OrganizationId == org && l.Name == "Pizzeria Roma"));
        roma.Kind.Should().Be("establishment");
        roma.EstablishmentType.Should().Be("restaurant", "a pizzeria is a restaurant");
        roma.Seats.Should().Be(60);
        roma.ContactPersonEmail.Should().Be("marco@roma.ch");
        (await app.Services.Read(db => db.Leads.IgnoreQueryFilters().Single(l => l.OrganizationId == org && l.Name == "Café Odeon").EstablishmentType))
            .Should().Be("cafe");

        // The same file again: the places are there, matched by name and city.
        var twice = await OkJson(await Preview(owner, "lead", csv, columns), "again");
        Action(twice, 2).Should().Be("skip");
        Message(twice, 2).Should().Contain("name and city");
    }
}
