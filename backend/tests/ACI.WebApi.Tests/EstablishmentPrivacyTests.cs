using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A person who is an establishment's contact person, under the privacy requests: what the
/// place holds about them is in their export, an erasure takes them off it, and an import
/// does not bring them back through it. The establishment itself is a business, not theirs,
/// and stays with its history.
/// </summary>
/// <remarks>
/// The lead kinds (people or establishments) and the privacy requests were made side by side
/// and met in a merge: the erasure deleted a person's leads and searched by a lead's own
/// email, and an establishment keeps its person in contact person columns of its own.
/// </remarks>
public class EstablishmentPrivacyTests
{
    private const string MarcoEmail = "marco@roma.ch";

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A workspace of establishments with Pizzeria Roma, whose contact person is Marco, and a note about the place.</summary>
    private static async Task<(CustomWebApplicationFactory App, HttpClient Owner, Guid Org, Guid PlaceId, Guid ContactId)> PizzeriaAsync()
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@bonap.ch");
        var org = await CreateAsync(app, ownerId, "Bonap");
        owner.In(org);
        await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { leadKinds = "establishment" })), "set lead kinds");
        var place = await Ok(await owner.PostAsync("/api/leads", Body(new
        {
            name = "Pizzeria Roma", status = "New", email = "info@roma.ch", city = "Zürich",
            contactPersonName = "Marco Rossi", contactPersonRole = "Owner", contactPersonEmail = MarcoEmail, contactPersonPhone = "+41 44 000 00 00",
        })), "add the establishment");
        var placeId = place.GetProperty("id").GetGuid();
        var contactId = await app.Services.Read(db => db.Contacts.Single(c => c.OrganizationId == org && c.Email == MarcoEmail).Id);
        await Ok(await owner.PostAsync("/api/activities", Body(new { type = "note", subject = "Menu samples", body = "Left the spring menu samples at the bar.", leadId = placeId })),
            "a note about the place");
        return (app, owner, org, placeId, contactId);
    }

    [Fact]
    public async Task FindingTheContactPerson_DoesNotOfferTheirEstablishmentAsTheirs_AndStartingFromItSaysWhatItIs()
    {
        var (app, owner, _, placeId, contactId) = await PizzeriaAsync();
        using var _app = app;

        var found = await Ok(await owner.PostAsync("/api/privacy/find", Body(new { email = MarcoEmail })), "find Marco");
        found.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("id").GetGuid())
            .Should().Equal([contactId], "the contact made for him is his; the place he speaks for is a business, not his");

        var fromPlace = await Ok(await owner.PostAsync("/api/privacy/find", Body(new { leadId = placeId })), "find from the establishment");
        var start = fromPlace.GetProperty("records").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == placeId);
        start.GetProperty("email").GetString().Should().Be(MarcoEmail, "the person behind an establishment is its contact person");
        start.GetProperty("foundBy").GetString().Should().Contain("an establishment").And.Contain("contact person");
        fromPlace.GetProperty("addresses").EnumerateArray().Select(a => a.GetString()).Should().Equal([MarcoEmail],
            "the place's own address is the business's, not his");
    }

    [Fact]
    public async Task TheExportHoldsWhatTheEstablishmentKeepsAboutThem()
    {
        var (app, owner, _, _, contactId) = await PizzeriaAsync();
        using var _app = app;

        var res = await owner.PostAsync("/api/privacy/export", Body(new { contactIds = new[] { contactId }, addresses = new[] { MarcoEmail } }));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var zip = new ZipArchive(await res.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        var entry = zip.GetEntry("records/establishments-as-contact-person.json");
        entry.Should().NotBeNull("the place names him as its contact person");
        using var reader = new StreamReader(entry!.Open());
        var text = await reader.ReadToEndAsync();
        text.Should().Contain("Pizzeria Roma").And.Contain(MarcoEmail).And.Contain("Owner").And.Contain("+41 44 000 00 00");
        text.Should().NotContain("info@roma.ch", "the business's own details are not his");
    }

    [Fact]
    public async Task ErasingTheContactPerson_TakesThemOffTheEstablishment_AndTheEstablishmentStays()
    {
        var (app, owner, org, placeId, contactId) = await PizzeriaAsync();
        using var _app = app;
        var selection = new { contactIds = new[] { contactId }, addresses = new[] { MarcoEmail } };

        var plan = await Ok(await owner.PostAsync("/api/privacy/erasure/preview", Body(selection)), "plan");
        var line = plan.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("area").GetString() == "Establishments they are the contact person of");
        line.GetProperty("count").GetInt32().Should().Be(1);

        await Ok(await owner.PostAsync("/api/privacy/erasure", Body(new { contactIds = new[] { contactId }, addresses = new[] { MarcoEmail }, confirm = "ERASE" })),
            "erase Marco");

        var place = await app.Services.Read(db => db.Leads.Single(l => l.Id == placeId));
        place.Name.Should().Be("Pizzeria Roma", "the business stays");
        place.Email.Should().Be("info@roma.ch");
        (place.ContactPersonName, place.ContactPersonRole, place.ContactPersonEmail, place.ContactPersonPhone)
            .Should().Be(((string?)null, (string?)null, (string?)null, (string?)null), "what it held about him as its contact person goes");
        (await app.Services.Read(db => db.Activities.Count(a => a.LeadId == placeId && a.Subject == "Menu samples"))).Should().Be(1, "its history stays");
        (await app.Services.Read(db => db.Contacts.Count(c => c.OrganizationId == org && c.Email == MarcoEmail))).Should().Be(0);
        (await app.Services.Read(db => db.EmailSuppressions.Count(s => s.OrganizationId == org && s.Email == ErasedAddresses.Key(MarcoEmail))))
            .Should().Be(1, "his address is suppressed as a fingerprint");
        (await app.Services.Read(db => db.EmailSuppressions.Count(s => s.OrganizationId == org && s.Email.Contains("roma.ch"))))
            .Should().Be(0, "the place's own address is not his, and is not suppressed");

        // Saving the place again makes no contact of anyone: it has no contact person now.
        (await owner.PutAsync($"/api/leads/{placeId}", Body(new { phone = "+41 44 111 11 11" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await app.Services.Read(db => db.Contacts.Count(c => c.OrganizationId == org && c.Email == MarcoEmail))).Should().Be(0);
    }

    [Fact]
    public async Task TheRetentionRunRemovesAStaleLead_ButLeavesAPlaceStillInTouchWithTheSameContactPerson()
    {
        var (app, owner, org, placeId, _) = await PizzeriaAsync();
        using var _app = app;
        await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { leadKinds = "both" })), "people and establishments");
        var staleId = Guid.NewGuid();
        await using (var db = app.Services.EveryWorkspace())
        {
            // Seeded as history: the audit trail would date it today.
            db.RecordsAuditTrail = false;
            var ownerId = await db.OrganizationMembers.Where(m => m.OrganizationId == org).Select(m => m.UserId).SingleAsync();
            // Without the contact made for him, nothing outside the stale lead uses his address but
            // the place's contact person: the run must still leave the place alone.
            (await db.Leads.SingleAsync(l => l.Id == placeId)).ConvertedToContactId = null;
            db.Contacts.Remove(await db.Contacts.SingleAsync(c => c.OrganizationId == org && c.Email == MarcoEmail));
            db.Leads.Add(new Lead
            {
                Id = staleId, UserId = ownerId, OrganizationId = org, Name = "Marco Rossi", Email = MarcoEmail, Status = "New",
                CreatedAtUtc = DateTime.UtcNow.AddYears(-3),
            });
            await db.SaveChangesAsync();
        }
        await Ok(await owner.PutAsync("/api/privacy/settings", Body(new { marketingConsent = "optional", leadRetentionMonths = 24 })), "keep leads two years");

        var run = await Ok(await owner.PostAsync("/api/privacy/retention/run", null), "run now");
        run.GetProperty("leadsDeleted").GetInt32().Should().Be(1, "the old lead nobody worked on goes");

        (await app.Services.Read(db => db.Leads.Any(l => l.Id == staleId))).Should().BeFalse();
        var place = await app.Services.Read(db => db.Leads.Single(l => l.Id == placeId));
        (place.ContactPersonName, place.ContactPersonEmail).Should().Be(("Marco Rossi", MarcoEmail),
            "the place is in touch: its contact person is not someone nobody has heard from");
    }

    [Fact]
    public async Task AnImportDoesNotBringAnErasedContactPersonBackThroughAnotherEstablishment()
    {
        var (app, owner, org, _, contactId) = await PizzeriaAsync();
        using var _app = app;
        await Ok(await owner.PostAsync("/api/privacy/erasure", Body(new { contactIds = new[] { contactId }, addresses = new[] { MarcoEmail }, confirm = "ERASE" })),
            "erase Marco");

        const string csv = "Establishment,City,Contact person,Contact email\n"
            + "Trattoria Marco,Lugano,Marco Rossi,Marco@Roma.ch\n"
            + "Café Lena,Bern,Lena Frei,lena@cafe-lena.ch\n";
        string?[] columns = ["name", "city", "contactPersonName", "contactPersonEmail"];
        var preview = await OkJson(await Preview(owner, "lead", csv, columns), "preview");
        Action(preview, 2).Should().Be("skip");
        Message(preview, 2).Should().Contain("contact person's address").And.Contain("erased at their request");
        var imported = await OkJson(await Import(owner, "lead", csv, columns), "import");
        Action(imported, 2).Should().Be("skip");
        Action(imported, 3).Should().Be("create", "everyone else is imported as before");
        (await app.Services.Read(db => db.Leads.Count(l => l.OrganizationId == org && l.ContactPersonEmail != null && l.ContactPersonEmail.ToLower() == MarcoEmail)))
            .Should().Be(0);
    }
}
