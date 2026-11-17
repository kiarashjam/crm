using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A newsletter to chosen leads (Pavillon 46 inviting its prospects to book a meeting): the
/// server counts who it goes to and who is left out and why, sends each lead one email of its
/// own with a signed booking link and code and the unsubscribe footer, honours the suppression
/// list, do not contact and meetings already booked, sends a send id once, and sends a test to the
/// caller, or to the one address they type.
/// </summary>
public class LeadNewsletterTests
{
    private const string BookingPage = "https://pavillon46.ch/rendez-vous";

    private class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    /// <summary>A server without the app's public address, so it has no unsubscribe page to link to.</summary>
    private sealed class AppWithoutUnsubscribePage : App
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Email:FrontendBaseUrl"] = "" }));
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected}, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org, string Secret);

    /// <summary>Pavillon 46, with its booking page and the signing secret it shares with its website.</summary>
    private static async Task<Club> ClubAsync(App app, bool bookingPage = true)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        await Json(await owner.PutAsync("/api/onboarding/settings", Body(new { defaultLanguage = "fr", bookingPageUrl = bookingPage ? BookingPage : "" })), "settings");
        var secret = (await Json(await owner.PostAsync("/api/onboarding/settings/secret", null), "secret")).GetProperty("secret").GetString()!;
        return new Club(owner, ownerId, org, secret);
    }

    private static async Task<Guid> LeadAsync(Club club, string name, string? email, string status = "New") =>
        (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name, email, status })), $"lead {name}"))
            .GetProperty("id").GetGuid();

    /// <summary>
    /// Léa and Marc can be emailed; Nina is not interested and Tom lost; Eva asked not to be
    /// contacted, Bruno's address bounced and Noémie has none; Paul is qualified.
    /// </summary>
    private static async Task<Dictionary<string, Guid>> BookAsync(App app, Club club)
    {
        var leads = new Dictionary<string, Guid>
        {
            ["Léa"] = await LeadAsync(club, "Léa Favre", "lea@example.ch"),
            ["Marc"] = await LeadAsync(club, "Marc Dubois", "marc@example.ch", "Contacted"),
            ["Nina"] = await LeadAsync(club, "Nina Roth", "nina@example.ch", "Not interested"),
            ["Tom"] = await LeadAsync(club, "Tom Blanc", "tom@example.ch", "Lost"),
            ["Eva"] = await LeadAsync(club, "Eva Muster", "eva@example.ch"),
            ["Bruno"] = await LeadAsync(club, "Bruno Keller", "bruno@example.ch"),
            ["Paul"] = await LeadAsync(club, "Paul Meier", "paul@example.ch", "Qualified"),
        };
        // Noémie came with no address (from a phone call, say).
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var acting = db.ActFor(club.Org, club.OwnerId);
            var noemie = new Lead
            {
                Id = Guid.NewGuid(), UserId = club.OwnerId, OrganizationId = club.Org, Name = "Noémie Bovet", Email = "", Status = "New",
                CreatedAtUtc = DateTime.UtcNow, Version = Guid.NewGuid(),
            };
            db.Leads.Add(noemie);
            db.EmailSuppressions.Add(new EmailSuppression
            {
                Id = Guid.NewGuid(), OrganizationId = club.Org, Email = "bruno@example.ch", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            leads["Noémie"] = noemie.Id;
        }
        await StopAsync(app, club, leads["Eva"]);
        return leads;
    }

    /// <summary>The lead's person says stop: their contact is marked do not contact (an unsubscribe does the same).</summary>
    private static async Task StopAsync(App app, Club club, Guid leadId)
    {
        var contactId = await app.Services.Read(db => db.Contacts.Single(c => c.ConvertedFromLeadId == leadId).Id);
        await Json(await club.Owner.PutAsync($"/api/contacts/{contactId}", Body(new { doNotContact = true })), "says stop");
    }

    private static readonly object NotLostOrUninterested = new { excludeStatuses = new[] { "Not interested", "Lost" } };

    private const string Html = """
        <!DOCTYPE html><html><body style="background:#F5F0EA">
        <p style="font-family:'Cormorant Garamond',serif;color:#2C5545">Bonjour {{firstName}},</p>
        <a href="{{bookingUrl}}" style="background:#2C5545;color:#F5F0EA">Réserver mon rendez-vous</a>
        <p>Votre code : <strong>{{bookingCode}}</strong> · {{workspace}}</p>
        </body></html>
        """;

    private static object Send(Guid sendId, object? audience = null, string html = Html, int? expected = null) => new
    {
        sendId, language = "fr", subject = "{{firstName}}, votre rendez-vous au Pavillon", html,
        audience = audience ?? NotLostOrUninterested, expectedRecipients = expected,
    };

    [Fact]
    public async Task TheAudience_IsCountedByTheServer_WithWhoIsLeftOutAndWhy()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await BookAsync(app, club);

        // New and contacted leads: Léa and Marc get it; Eva, Bruno and Noémie are left out, each for a reason.
        var chosen = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience",
            Body(new { includeStatuses = new[] { "new", "Contacted" }, excludeStatuses = new[] { "Not interested", "Lost" } })), "new and contacted");
        chosen.GetProperty("matching").GetInt32().Should().Be(5);
        chosen.GetProperty("willReceive").GetInt32().Should().Be(2);
        chosen.GetProperty("noEmail").GetInt32().Should().Be(1);
        chosen.GetProperty("doNotContact").GetInt32().Should().Be(1);
        chosen.GetProperty("suppressed").GetInt32().Should().Be(1);
        chosen.GetProperty("recipients").EnumerateArray().Select(r => r.GetProperty("name").GetString()).Should().BeEquivalentTo("Léa Favre", "Marc Dubois");
        chosen.GetProperty("leftOut").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("reason").GetString())
            .Should().BeEquivalentTo(new Dictionary<string, string?>
            {
                ["Eva Muster"] = "do_not_contact", ["Bruno Keller"] = "suppressed", ["Noémie Bovet"] = "no_email",
            });

        // Every status but the two left out: Paul, qualified, comes in too.
        var allButLost = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience", Body(NotLostOrUninterested)), "all but lost");
        (allButLost.GetProperty("matching").GetInt32(), allButLost.GetProperty("willReceive").GetInt32()).Should().Be((6, 3));

        // Only the uninterested: Nina alone.
        var nina = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience", Body(new { includeStatuses = new[] { "Not interested" } })), "only Nina");
        (nina.GetProperty("matching").GetInt32(), nina.GetProperty("willReceive").GetInt32()).Should().Be((1, 1));

        // Created after today: nobody.
        var later = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience",
            Body(new { createdFrom = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd") })), "created later");
        later.GetProperty("matching").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ALeadWithAMeetingStillToCome_IsLeftOut_ButNotOneWhoCancelledOrHasAlreadyMet()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var leads = await BookAsync(app, club);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var acting = db.ActFor(club.Org, club.OwnerId);
            MeetingBooking Meeting(Guid lead, string id, DateTime at, string status) => new()
            {
                Id = Guid.NewGuid(), OrganizationId = club.Org, LeadId = lead, BookingId = id, StartsAtUtc = at, OffsetMinutes = 60,
                DurationMinutes = 45, Status = status, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            };
            db.MeetingBookings.AddRange(
                Meeting(leads["Léa"], "b-lea", DateTime.UtcNow.AddDays(3), MeetingBookingStatuses.Booked),         // to come: left out
                Meeting(leads["Marc"], "b-marc", DateTime.UtcNow.AddDays(3), MeetingBookingStatuses.Cancelled),    // cancelled: invited again
                Meeting(leads["Paul"], "b-paul", DateTime.UtcNow.AddDays(-3), MeetingBookingStatuses.Booked));     // already met: up to the filters
            await db.SaveChangesAsync();
        }

        var audience = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience", Body(NotLostOrUninterested)), "all but lost");
        audience.GetProperty("alreadyBooked").GetInt32().Should().Be(1);
        audience.GetProperty("recipients").EnumerateArray().Select(r => r.GetProperty("name").GetString()).Should().BeEquivalentTo("Marc Dubois", "Paul Meier");
        audience.GetProperty("leftOut").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Léa Favre")
            .GetProperty("reason").GetString().Should().Be("already_booked");

        await Json(await club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(Guid.NewGuid(), expected: 2))), "send");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.SelectMany(m => m.To).Should().BeEquivalentTo("marc@example.ch", "paul@example.ch");
    }

    [Fact]
    public async Task Sending_QueuesOnePersonalisedEmailPerLead_WithItsOwnSignedBookingLink_AndTheUnsubscribeFooter()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var leads = await BookAsync(app, club);
        var sendId = Guid.NewGuid();

        var sent = await Json(await club.Owner.PostAsync("/api/leads/newsletter/send",
            Body(Send(sendId, new { includeStatuses = new[] { "New", "Contacted" } }, expected: 2))), "send");
        (sent.GetProperty("recipients").GetInt32(), sent.GetProperty("leftOut").GetInt32(), sent.GetProperty("alreadySent").GetBoolean()).Should().Be((2, 3, false));
        await app.DispatchOutboxAsync();

        app.Mail.Sent.SelectMany(m => m.To).Should().BeEquivalentTo("lea@example.ch", "marc@example.ch");
        foreach (var (who, name, address) in new[] { ("Léa", "Léa Favre", "lea@example.ch"), ("Marc", "Marc Dubois", "marc@example.ch") })
        {
            var mail = app.Mail.Sent.Single(m => m.To.Single() == address);
            mail.Subject.Should().Be($"{name.Split(' ')[0]}, votre rendez-vous au Pavillon");
            mail.HtmlBody.Should().Contain($"Bonjour {name.Split(' ')[0]},").And.Contain("Pavillon 46");

            // Their own link: the booking page with an invitation signed with the workspace's secret.
            var link = Regex.Match(mail.HtmlBody, "href=\"(https://pavillon46\\.ch/rendez-vous\\?invite=([A-Za-z0-9_-]+)\\.([A-Za-z0-9_-]+))\"");
            link.Success.Should().BeTrue($"{who}'s email links to the booking page: {mail.HtmlBody}");
            var (first, signature) = (link.Groups[2].Value, link.Groups[3].Value);
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(club.Secret), Encoding.ASCII.GetBytes(first));
            MeetingInvitation.FromBase64Url(signature).Should().Equal(expected, "the invitation is signed with the onboarding secret");
            using var payload = JsonDocument.Parse(MeetingInvitation.FromBase64Url(first));
            var p = payload.RootElement;
            p.EnumerateObject().Select(x => x.Name).Should().Equal("v", "o", "l", "n", "e", "c", "u", "x");
            p.GetProperty("v").GetInt32().Should().Be(1);
            p.GetProperty("o").GetString().Should().Be(club.Org.ToString("D"));
            p.GetProperty("l").GetString().Should().Be(leads[who].ToString("D"));
            p.GetProperty("n").GetString().Should().Be(name);
            p.GetProperty("e").GetString().Should().Be(address);
            var code = p.GetProperty("c").GetString()!;
            code.Should().MatchRegex("^RDV-[ABCDEFGHJKMNPQRSTUVWXYZ2-9]{4}$").And.Be(MeetingInvitation.Code(leads[who]));
            p.GetProperty("u").GetString().Should().StartWith("http");
            DateTimeOffset.FromUnixTimeSeconds(p.GetProperty("x").GetInt64()).Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(60), TimeSpan.FromMinutes(5));
            MeetingInvitation.Read(club.Secret, $"{first}.{signature}")!.LeadId.Should().Be(leads[who]);
            MeetingInvitation.Read("another secret", $"{first}.{signature}").Should().BeNull();
            mail.HtmlBody.Should().Contain($"Votre code : <strong>{code}</strong>");

            // The unsubscribe footer, inside the email's body, and the List-Unsubscribe header's address.
            var footer = mail.HtmlBody.IndexOf("/unsubscribe/", StringComparison.Ordinal);
            footer.Should().BeGreaterThan(link.Index, "the footer comes at the foot of the design");
            footer.Should().BeLessThan(mail.HtmlBody.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase));
            mail.UnsubscribeUrl.Should().NotBeNullOrEmpty();
            mail.HtmlBody.Should().Contain(mail.UnsubscribeUrl!.Replace("&", "&amp;"));
            // A plain-text part goes with it, with the link and the footer.
            mail.TextBody.Should().Contain($"Bonjour {name.Split(' ')[0]}").And.Contain("invite=").And.Contain("/unsubscribe/");

            // On the lead's timeline once it has gone.
            var timeline = await Json(await club.Owner.GetAsync($"/api/activities/lead/{leads[who]}"), $"{who}'s timeline");
            timeline.EnumerateArray().Should().Contain(a => a.GetProperty("type").GetString() == "email"
                && a.GetProperty("subject").GetString()!.Contains("votre rendez-vous au Pavillon"));
        }

        // The same press again sends nothing more.
        var again = await Json(await club.Owner.PostAsync("/api/leads/newsletter/send",
            Body(Send(sendId, new { includeStatuses = new[] { "New", "Contacted" } }))), "the same send again");
        (again.GetProperty("alreadySent").GetBoolean(), again.GetProperty("recipients").GetInt32()).Should().Be((true, 2));
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().HaveCount(2);
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.SourceId == sendId))).Should().Be(2);
        (await app.Services.Read(db => db.LeadNewsletters.Single(n => n.Id == sendId).Status)).Should().Be(LeadNewsletterStatuses.Sent);
    }

    [Fact]
    public async Task ADoubleClick_SendsOnce()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await BookAsync(app, club);
        var sendId = Guid.NewGuid();

        var presses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(sendId)))));
        presses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.SourceId == sendId))).Should().Be(3, "Léa, Marc and Paul, once each");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.SelectMany(m => m.To).Should().BeEquivalentTo("lea@example.ch", "marc@example.ch", "paul@example.ch");
    }

    [Fact]
    public async Task AnAddressThatSaysStopAfterTheCount_IsStillNotEmailed()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var leads = await BookAsync(app, club);
        var audience = await Json(await club.Owner.PostAsync("/api/leads/newsletter/audience", Body(NotLostOrUninterested)), "the count");
        audience.GetProperty("willReceive").GetInt32().Should().Be(3);

        // Marc unsubscribes between the count and the press: what the person confirmed no longer holds.
        await StopAsync(app, club, leads["Marc"]);
        await Json(await club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(Guid.NewGuid(), expected: 3))), "send as confirmed", HttpStatusCode.Conflict);
        await Json(await club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(Guid.NewGuid(), expected: 2))), "send again");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.SelectMany(m => m.To).Should().BeEquivalentTo("lea@example.ch", "paul@example.ch");
    }

    [Fact]
    public async Task ATest_GoesToTheCallerAlone_MarkedAsATest_WithATestInvitation()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var leads = await BookAsync(app, club);

        var test = await Json(await club.Owner.PostAsync("/api/leads/newsletter/test",
            Body(new { language = "fr", subject = "{{firstName}}, votre rendez-vous", html = Html, sampleLeadId = leads["Léa"] })), "test");
        test.GetProperty("to").GetString().Should().Be("owner@pavillon46.ch");

        var mail = app.Mail.Sent.Should().ContainSingle().Subject;
        mail.To.Should().Equal("owner@pavillon46.ch");
        mail.Subject.Should().Be("[Test] Léa, votre rendez-vous");
        mail.HtmlBody.Should().Contain("Ceci est un test").And.Contain("Bonjour Léa,");
        mail.HtmlBody.Should().Contain($"<strong>{MeetingInvitation.Code(leads["Léa"])}</strong>");
        var token = Regex.Match(mail.HtmlBody, "invite=([A-Za-z0-9_.-]+)").Groups[1].Value;
        MeetingInvitation.Read(club.Secret, token)!.LeadId.Should().Be(Guid.Empty, "a test's invitation books for nobody");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().HaveCount(1, "no lead was emailed");
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.LeadId != null))).Should().Be(0);

        // To a colleague, or a second inbox, as typed.
        var colleague = await Json(await club.Owner.PostAsync("/api/leads/newsletter/test",
            Body(new { language = "fr", subject = "{{firstName}}, votre rendez-vous", html = Html, sampleLeadId = leads["Léa"], to = " pierre@pavillon46.ch " })), "test to a colleague");
        colleague.GetProperty("to").GetString().Should().Be("pierre@pavillon46.ch");
        app.Mail.Sent.Last().To.Should().Equal("pierre@pavillon46.ch");
        app.Mail.Sent.Last().Subject.Should().StartWith("[Test] ");

        var wrong = await Json(await club.Owner.PostAsync("/api/leads/newsletter/test",
            Body(new { language = "fr", subject = "Bonjour", html = Html, to = "pierre" })), "not an address", HttpStatusCode.BadRequest);
        wrong.ToString().Should().Contain("not an email address");
        app.Mail.Sent.Should().HaveCount(2);
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.LeadId != null))).Should().Be(0, "a test is never a lead's email");
    }

    [Fact]
    public async Task WithoutABookingPage_TheBookingLinkIsRefused_ButANewsletterWithoutItGoes()
    {
        await using var app = new App();
        var club = await ClubAsync(app, bookingPage: false);
        await LeadAsync(club, "Léa Favre", "lea@example.ch");

        var setup = await Json(await club.Owner.GetAsync("/api/leads/newsletter/setup"), "setup");
        setup.GetProperty("bookingLinkAvailable").GetBoolean().Should().BeFalse();
        setup.GetProperty("bookingProblem").GetString().Should().Contain("booking page");

        var preview = await Json(await club.Owner.PostAsync("/api/leads/newsletter/preview", Body(new { language = "fr", subject = "Bonjour", html = Html })), "preview");
        preview.GetProperty("bookingProblem").GetString().Should().Contain("Settings");

        var refused = await Json(await club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(Guid.NewGuid()))), "with a booking link", HttpStatusCode.BadRequest);
        refused.ToString().Should().Contain("bookingUrl");
        await Json(await club.Owner.PostAsync("/api/leads/newsletter/send",
            Body(Send(Guid.NewGuid(), html: "<p>Bonjour {{firstName}}, appelez-nous pour votre code {{bookingCode}}.</p>"))), "without one");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().ContainSingle().Which.HtmlBody.Should().Contain("Bonjour Léa");

        // Placeholders nothing fills are said before anything goes.
        var unknown = await Json(await club.Owner.PostAsync("/api/leads/newsletter/send",
            Body(Send(Guid.NewGuid(), html: "<p>{{phone}}</p>"))), "an unknown field", HttpStatusCode.BadRequest);
        unknown.ToString().Should().Contain("{{phone}}");
    }

    [Fact]
    public async Task TheBookingPage_IsAnHttpsAddress()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { bookingPageUrl = "http://pavillon46.ch/rendez-vous" })), "http", HttpStatusCode.BadRequest);
        await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { bookingPageUrl = "https://localhost/rendez-vous" })), "this machine", HttpStatusCode.BadRequest);
        var kept = await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { defaultLanguage = "en" })), "left out");
        kept.GetProperty("bookingPageUrl").GetString().Should().Be(BookingPage, "a save that leaves it out keeps it");
        var cleared = await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { bookingPageUrl = "" })), "cleared");
        cleared.GetProperty("bookingPageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task TheOutbox_StillRefusesHtmlOfItsOwn_ForAnythingButATestOrAnEmailToOneLead()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        using var scope = app.Services.CreateScope();
        using var acting = scope.ServiceProvider.GetRequiredService<AppDbContext>().ActFor(club.Org, club.OwnerId);
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();

        var refused = await outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = club.Org, Source = OutboundSources.Composer, To = ["someone@example.ch"], Subject = "Hi", Body = "Hi", Html = "<p>Hi</p>",
        });
        refused.IsFailure.Should().BeTrue();
        refused.Error.Code.Should().Be("Email.HtmlForTestsOnly");
    }

    [Fact]
    public async Task ADesignedEmailToALead_ThatCannotCarryItsUnsubscribeLink_IsNotSent()
    {
        await using var app = new AppWithoutUnsubscribePage();
        var club = await ClubAsync(app);
        await LeadAsync(club, "Léa Favre", "lea@example.ch");

        var sent = await Json(await club.Owner.PostAsync("/api/leads/newsletter/send", Body(Send(Guid.NewGuid()))), "send");
        (sent.GetProperty("recipients").GetInt32(), sent.GetProperty("leftOut").GetInt32()).Should().Be((0, 1));
        await app.DispatchOutboxAsync();
        app.Mail.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void TheBookingCode_IsShortReadableAndTheSameForTheSameLead()
    {
        var lead = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        MeetingInvitation.Code(lead).Should().Be(MeetingInvitation.Code(lead)).And.MatchRegex("^RDV-[ABCDEFGHJKMNPQRSTUVWXYZ2-9]{4}$");
        Enumerable.Range(0, 200).Select(_ => MeetingInvitation.Code(Guid.NewGuid())).Distinct().Should().HaveCountGreaterThan(190);
        MeetingInvitation.BookingUrl("https://pavillon46.ch/rdv?lang=fr#form", "a.b").Should().Be("https://pavillon46.ch/rdv?lang=fr&invite=a.b#form");
    }

    /// <summary>
    /// The contract with the website, as a fixed example worked out independently of this code
    /// (Python's hashlib, hmac and base64): the website's side can check itself against it.
    /// </summary>
    [Fact]
    public void TheInvitation_MatchesTheDocumentedExample()
    {
        var lead = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        MeetingInvitation.Code(lead).Should().Be("RDV-CK8V");
        var token = MeetingInvitation.Token("test-secret", new MeetingInvitationPayload(
            1, Guid.Parse("0b5f2c1e-1111-4a2b-9c3d-123456789abc"), lead, "Lea Favre", "lea@example.ch", "RDV-CK8V", "https://api.example.com", 1767225600));
        token.Should().Be(
            "eyJ2IjoxLCJvIjoiMGI1ZjJjMWUtMTExMS00YTJiLTljM2QtMTIzNDU2Nzg5YWJjIiwibCI6IjZmOTYxOWZmLThiODYtZDAxMS1iNDJkLTAwYzA0ZmM5NjRmZiIsIm4iOiJMZWEgRmF2cmUiLCJlIjoibGVhQGV4YW1wbGUuY2giLCJjIjoiUkRWLUNLOFYiLCJ1IjoiaHR0cHM6Ly9hcGkuZXhhbXBsZS5jb20iLCJ4IjoxNzY3MjI1NjAwfQ"
            + ".M7fHdZmCJk0cRbVXpGx9PG1jZRsW2AD5zGLT47ZVAxQ");
        MeetingInvitation.Read("test-secret", token)!.Name.Should().Be("Lea Favre");
    }
}
