using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The language a club writes to a member in: their statements, reminders, welcome and event
/// emails, the footer of its news, and the public pages they open. Kept on the member, set by
/// staff or by the form they applied on, and followed by every email and PDF the member gets.
/// </summary>
/// <remarks>
/// Everything a member got was in English. A member with no language set still is, so nothing
/// changes for a club until it sets one.
/// </remarks>
public class MemberLanguageTests
{
    /// <summary>The app with a mail server that keeps what it is handed, at public addresses.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FrontendBaseUrl"] = "https://crm.example.ch",
                ["Email:ApiBaseUrl"] = "https://api.example.ch",
            }));
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    private const string CaptureToken = "0123456789abcdef0123456789abcdef";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string? Language(JsonElement value) =>
        value.TryGetProperty("preferredLanguage", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()
        : value.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString()
        : null;

    private static async Task<(HttpClient Owner, Guid OwnerId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, ownerId, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient owner, string first, string? language) =>
        (await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
            preferredLanguage = language,
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task ChargeAsync(HttpClient owner, Guid memberId, decimal amount, DateTime dueAtUtc, string description) =>
        await Json(await owner.PostAsync("/api/charges", Body(new
        {
            memberId, kind = "Dining", description, amount, dueAtUtc = dueAtUtc.ToString("o"),
        })), $"charge {description}");

    [Fact]
    public async Task StaffSetAMembersLanguage_ChangeIt_AndClearIt_AndOnlyOursAreTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);

        var created = await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = "Jeanne", lastName = "Dupont", email = "jeanne@example.ch", tier = "Gold", status = "Active", preferredLanguage = "fr",
        })), "add Jeanne in French");
        Language(created).Should().Be("fr");
        var id = created.GetProperty("id").GetGuid();

        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = "de-CH" })), "switch her to German"))
            .Should().Be("de", "a locale name is read as its language");
        // A save that leaves the language out keeps it.
        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { phone = "+41 79 123 45 67" })), "change her phone"))
            .Should().Be("de");
        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = (string?)null })), "clear it"))
            .Should().BeNull("an explicit null clears it, and she is written to in English again");

        var refused = await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = "it" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());
        var list = await Json(await owner.GetAsync("/api/members"), "list the members");
        list.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id).TryGetProperty("preferredLanguage", out var kept)
            .Should().BeTrue();
        kept.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AStatementIsEmailedAndPrintedInTheMembersLanguage_AndInEnglishForOneWithNone()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        var jeanne = await MemberAsync(owner, "Jeanne", "fr");
        var max = await MemberAsync(owner, "Max", "de");
        var jean = await MemberAsync(owner, "Jean", null);
        foreach (var member in new[] { jeanne, max, jean })
            await ChargeAsync(owner, member, 299, DateTime.UtcNow.AddDays(14), "Gold dues");
        var period = new { from = DateTime.UtcNow.AddDays(-1), to = DateTime.UtcNow.AddDays(1) };

        await Json(await owner.PostAsync($"/api/members/{jeanne}/statement/email", Body(period)), "email Jeanne her statement");
        await Json(await owner.PostAsync($"/api/members/{jean}/statement/email", Body(period)), "email Jean his");
        await app.DispatchOutboxAsync();

        var french = app.Mail.Sent.Single(m => m.To.Contains("jeanne@example.ch"));
        french.Subject.Should().StartWith("Votre relevé de compte pour");
        french.TextBody.Should().StartWith("Bonjour Jeanne,").And.Contain("Montant dû : CHF 299.00")
            .And.Contain("Pour toute question à ce sujet");
        // The PDF's own escapes stand for its accented letters here (\351 for é), so only plain words are looked for.
        PdfText.Of(french.Attachments.Single().Content).Should().Contain(" de compte").And.Contain("Membre Gold").And.Contain("Page 1 sur 1")
            .And.NotContain("Amount due");

        var english = app.Mail.Sent.Single(m => m.To.Contains("jean@example.ch"));
        english.Subject.Should().StartWith("Your statement for");
        english.TextBody.Should().StartWith("Dear Jean,").And.Contain("Amount due: CHF 299.00");

        var from = Uri.EscapeDataString(period.from.ToString("o"));
        var to = Uri.EscapeDataString(period.to.ToString("o"));
        var download = await owner.GetAsync($"/api/members/{max}/statement.pdf?from={from}&to={to}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        PdfText.Of(await download.Content.ReadAsByteArrayAsync()).Should().Contain("Kontoauszug").And.Contain("Offener Betrag: CHF 299.00");
    }

    [Fact]
    public async Task APaymentReminderIsInTheMembersLanguage()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        var max = await MemberAsync(owner, "Max", "de");
        await ChargeAsync(owner, max, 30, DateTime.UtcNow.AddDays(-5), "Bar");
        await Json(await owner.PutAsync("/api/hospitality/settings", Body(new { billing = new { remindersEnabled = true, reminderDays = new[] { 3 } } })),
            "remind three days after the due date");

        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IClubBilling>().RunScheduledAsync(RunTime());
        await app.DispatchOutboxAsync();

        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.Subject.Should().Be("Zahlungserinnerung");
        email.TextBody.Should().StartWith("Guten Tag Max,").And.Contain("Wir erinnern Sie daran")
            .And.Contain("- Bar: CHF 30.00, fällig am").And.Contain("Total überfällig: CHF 30.00");
    }

    [Fact]
    public async Task AnApplicantWhoAppliedInFrench_IsThankedAndWelcomedInFrench_AndTheConsentKeepsTheWordsTheyRead()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var token = (await Json(await owner.PutAsync("/api/applications/form", Body(new { isActive = true })), "set up the form"))
            .GetProperty("publicToken").GetString();
        using var visitor = factory.CreateClient();

        var english = await Json(await visitor.GetAsync($"/api/public/apply/{token}"), "open the form");
        english.GetProperty("consentText").GetString().Should().StartWith("Yes, I would like to hear", "a page that does not say reads English, as before");
        Language(english).Should().Be("de", "the club's own language, from its de-CH locale, for a visitor whose browser speaks none of ours");
        var page = await Json(await visitor.GetAsync($"/api/public/apply/{token}?lang=fr"), "open the form in French");
        page.GetProperty("consentText").GetString().Should().StartWith("Oui, je souhaite recevoir par e-mail");

        var receipt = await Json(await visitor.PostAsync($"/api/public/apply/{token}", Body(new
        {
            firstName = "Lena", lastName = "Weber", email = "lena@example.ch", requestedTier = "Gold", marketingConsent = true, language = "fr",
        })), "apply in French");
        receipt.GetProperty("message").GetString().Should().Be("Merci pour votre demande. Le comité d’admission prendra contact avec vous.");
        var application = await factory.Services.Read(db => db.ClubApplications.Single());
        application.Language.Should().Be("fr");
        (await factory.Services.Read(db => db.ConsentRecords.Single().Detail)).Should().Contain("Oui, je souhaite recevoir par e-mail",
            "the record keeps the words as the applicant read them");

        var approved = await Json(await owner.PostAsync($"/api/applications/{application.Id}/approve", Body(new { })), "approve");
        var memberId = approved.GetProperty("memberId").GetGuid();
        var members = await Json(await owner.GetAsync("/api/members"), "the members");
        Language(members.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == memberId)).Should().Be("fr",
            "the member is written to in the language they applied in");
        var welcome = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Welcome));
        welcome.Subject.Should().Be("Bienvenue chez Pavillon46");
        welcome.Body.Should().StartWith("Bonjour Lena,").And.Contain("Votre demande a été acceptée");
    }

    [Fact]
    public async Task TheCaptureFormShowsItsStandardWordsInTheLanguageAsked_AndAnsweredInIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await ClubAsync(factory);
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.LeadCaptureForms.Add(new LeadCaptureForm
            {
                Id = Guid.NewGuid(), OrganizationId = org, CreatedByUserId = ownerId, Name = "Website", PublicToken = CaptureToken,
                IsActive = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        using var visitor = factory.CreateClient();

        var german = await Json(await visitor.GetAsync($"/api/public/capture/{CaptureToken}?lang=de"), "open the form in German");
        german.GetProperty("marketingConsentText").GetString().Should().StartWith("Ja, ich möchte per E-Mail");
        Language(german).Should().Be("de");
        (await Json(await visitor.GetAsync($"/api/public/capture/{CaptureToken}"), "open it in English"))
            .GetProperty("marketingConsentText").GetString().Should().StartWith("Yes, I would like to hear");

        var thanked = await Json(await visitor.PostAsync($"/api/public/capture/{CaptureToken}", Body(new
        {
            name = "Lena Weber", email = "lena@example.ch", marketingConsent = true, language = "de",
        })), "send it in German");
        thanked.GetProperty("message").GetString().Should().Be("Danke. Wir melden uns bei Ihnen.");
        (await factory.Services.Read(db => db.ConsentRecords.Single().Detail)).Should().Contain("Ja, ich möchte per E-Mail");
        (await Json(await visitor.PostAsync($"/api/public/capture/{CaptureToken}", Body(new { name = "Tom Berg", email = "tom@example.ch" })), "send it in English"))
            .GetProperty("message").GetString().Should().Be("Thanks. We'll be in touch.");

        await Json(await owner.PutAsync("/api/privacy/settings", Body(new
        {
            controllerName = "Pavillon 46 SA", controllerAddress = "Rue du Lac 46, 1006 Lausanne", controllerEmail = "privacy@pavillon46.ch",
            marketingConsent = "required",
        })), "require the box");
        var refused = await visitor.PostAsync($"/api/public/capture/{CaptureToken}", Body(new { name = "Anne Roux", email = "anne@example.ch", language = "fr" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Veuillez cocher la case");
    }

    [Fact]
    public async Task AMembersNewsCarryTheFooterInTheirLanguage_AndTheUnsubscribePageOpensInIt()
    {
        await using var app = new App();
        var (owner, ownerId, org) = await ClubAsync(app);
        var max = await MemberAsync(owner, "Max", "de");
        var jean = await MemberAsync(owner, "Jean", null);
        foreach (var member in new[] { max, jean })
        {
            await Json(await owner.PostAsync($"/api/members/{member}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })),
                "record consent");
            using var scope = app.Services.CreateScope();
            using var acting = scope.ServiceProvider.GetRequiredService<ITenantContext>().ActFor(org, ownerId);
            var queued = await scope.ServiceProvider.GetRequiredService<IOutbox>().QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = org, Source = OutboundSources.Member, To = [member == max ? "max@example.ch" : "jean@example.ch"],
                Subject = "Herbstball", Body = "Hallo {{firstName}},\nBis Freitag.", CreatedByUserId = ownerId, ReplyToUserId = ownerId,
                MemberId = member, Purpose = ConsentPurposes.Marketing,
            });
            queued.IsSuccess.Should().BeTrue(queued.IsSuccess ? "" : queued.Error.Description);
        }
        await app.DispatchOutboxAsync();

        var german = app.Mail.Sent.Single(m => m.To.Contains("max@example.ch"));
        german.TextBody.Should().Contain("Keine Neuigkeiten und Angebote von Pavillon46 mehr per E-Mail? Abmelden: https://crm.example.ch/unsubscribe/");
        german.HtmlBody.Should().Contain(">Abmelden</a>");
        var english = app.Mail.Sent.Single(m => m.To.Contains("jean@example.ch"));
        english.TextBody.Should().Contain("Don't want news and offers from Pavillon46 by email? Unsubscribe: https://crm.example.ch/unsubscribe/");

        using var stranger = app.CreateClient();
        Language(await Json(await stranger.GetAsync($"/api/public/unsubscribe/{TokenFrom(german.TextBody)}"), "Max opens his link"))
            .Should().Be("de", "the page opens in the member's language");
        Language(await Json(await stranger.GetAsync($"/api/public/unsubscribe/{TokenFrom(english.TextBody)}"), "Jean opens his"))
            .Should().BeNull("a member with none set gets the page in their browser's language");
    }

    [Fact]
    public async Task AnEventSeatThatCameFreeIsAnnouncedInTheMembersLanguage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var start = DateTime.UtcNow.AddDays(7);
        var eventId = (await Json(await owner.PostAsync("/api/events", Body(new
        {
            name = "Soirée vins", location = "Bibliothèque", startAtUtc = start, endAtUtc = start.AddHours(3), capacity = 1, memberPrice = 35m, status = "Published",
        })), "add an event")).GetProperty("id").GetGuid();
        var jean = await MemberAsync(owner, "Jean", null);
        var marie = await MemberAsync(owner, "Marie", "fr");
        var first = await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = jean, ticketTier = "Member" })), "Jean takes the seat");
        await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = marie, ticketTier = "Member" })), "Marie waits");

        await Json(await owner.PutAsync($"/api/events/{eventId}/attendees/{first.GetProperty("id").GetString()}", Body(new { status = "Cancelled" })), "Jean cancels");

        var mail = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.OrganizationId == org));
        mail.To.Should().Be("marie@example.ch");
        mail.Subject.Should().Be("Une place pour vous : Soirée vins");
        mail.Body.Should().StartWith("Bonne nouvelle\u00a0: une place s’est libérée pour Soirée vins").And.Contain("CHF 35.00")
            .And.Contain("compte au club");
    }

    private static string TokenFrom(string text)
    {
        var match = Regex.Match(text, @"https://crm\.example\.ch/unsubscribe/([A-Za-z0-9_\-%]+)");
        match.Success.Should().BeTrue($"the email should carry an unsubscribe link:\n{text}");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    /// <summary>A moment today when a workspace's daily run may go (after 03:00 in Zurich).</summary>
    private static DateTime RunTime()
    {
        var zone = RegionalSettings.ZoneOf(RegionalSettings.DefaultTimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        return local.TimeOfDay >= TimeSpan.FromHours(3)
            ? DateTime.UtcNow
            : DateTime.UtcNow.Add(TimeSpan.FromHours(3) - local.TimeOfDay + TimeSpan.FromMinutes(1));
    }
}
