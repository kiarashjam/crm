using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Welcome &amp; portal: a deal's contact handed to the workspace's portal, signed, with the
/// welcome email; the portal's reports moving the handoff along; the club member added.
/// </summary>
public class OnboardingTests
{
    private const string Portal = "https://portal.example.com/api/integrations/cadence/onboarding";

    private sealed class Receiver : IWebhookTransport
    {
        public readonly List<(Uri Url, string Body, IReadOnlyDictionary<string, string> Headers)> Posts = new();
        public readonly Queue<int> Answers = new();

        public Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            lock (Posts) Posts.Add((url, body, headers));
            var status = Answers.Count > 0 ? Answers.Dequeue() : 200;
            return Task.FromResult(new WebhookPostResult(status, status is >= 200 and < 300 ? null : $"The receiver answered {status}"));
        }
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly Receiver Hook = new();
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IWebhookTransport>(_ => Hook);
                services.AddSingleton(Mail.Mock.Object);
            });
        }
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>A workspace with its portal set up, and a deal for Jean with Jean as its contact.</summary>
    private static async Task<(HttpClient Owner, Guid Org, string Secret, Guid DealId)> SetUpAsync(App app, bool portal = true)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var secret = "";
        if (portal)
        {
            (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = Portal, defaultLanguage = "fr" })))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var made = await owner.PostAsync("/api/onboarding/settings/secret", null);
            made.StatusCode.Should().Be(HttpStatusCode.OK);
            secret = (await JsonAsync(made)).GetProperty("secret").GetString()!;
        }
        var contact = await JsonAsync(await owner.PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@example.ch", phone = "+41 79 000 00 00" })));
        var deal = await JsonAsync(await owner.PostAsync("/api/deals", Body(new { name = "Membership Jean", value = "4800", contactId = contact.GetProperty("id").GetGuid() })));
        return (owner, org, secret, deal.GetProperty("id").GetGuid());
    }

    private static HttpRequestMessage Report(Guid id, string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/public/onboarding/{id}/events") { Content = Body(body) };
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string Hex(string secret, string text) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public async Task ADealsContactIsHandedToThePortal_Signed_WithTheirEmailAndStart()
    {
        await using var app = new App();
        var (owner, org, secret, dealId) = await SetUpAsync(app);

        var started = await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new
        {
            welcomeMessage = "Ravis de vous compter parmi nous <b>!</b>\nÀ bientôt.",
            startsOnUtc = "2027-01-15",
            language = "fr",
            emailSubject = "Bienvenue {{firstName}}",
            emailHtml = "<p>Bonjour {{firstName}},</p><p>{{welcomeMessage}}</p><a href=\"{{setPasswordUrl}}\">Choisir</a><p>{{startDate}}</p>",
        }));
        started.StatusCode.Should().Be(HttpStatusCode.Created, await started.Content.ReadAsStringAsync());
        var handoff = await JsonAsync(started);
        var id = handoff.GetProperty("id").GetGuid();
        handoff.GetProperty("status").GetString().Should().Be("queued");
        app.Hook.Posts.Should().BeEmpty("the outbox posts it, not the request");

        await app.DispatchOutboxAsync();

        var (url, body, headers) = app.Hook.Posts.Should().ContainSingle().Subject;
        url.Should().Be(new Uri(Portal));
        headers["X-Cadence-Event"].Should().Be("deal.onboarding");
        var timestamp = headers["X-Cadence-Timestamp"];
        headers["X-Cadence-Signature-256"].Should().Be("sha256=" + Hex(secret, $"{timestamp}.{body}"));
        long.Parse(timestamp).Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 60);

        using var doc = JsonDocument.Parse(body);
        var payload = doc.RootElement.GetProperty("payload");
        payload.GetProperty("onboardingId").GetGuid().Should().Be(id);
        payload.GetProperty("contact").GetProperty("email").GetString().Should().Be("jean@example.ch");
        payload.GetProperty("contact").GetProperty("firstName").GetString().Should().Be("Jean");
        payload.GetProperty("membership").GetProperty("startsOn").GetString().Should().Be("2027-01-15");
        payload.GetProperty("membership").GetProperty("startUndetermined").GetBoolean().Should().BeFalse();
        payload.GetProperty("language").GetString().Should().Be("fr");
        var email = payload.GetProperty("email");
        email.GetProperty("subject").GetString().Should().Be("Bienvenue Jean");
        var html = email.GetProperty("html").GetString()!;
        html.Should().Contain("Bonjour Jean").And.Contain("15 janvier 2027");
        html.Should().Contain("&lt;b&gt;!&lt;/b&gt;<br>", "what a person typed is text, never markup, and keeps its line breaks");
        html.Should().Contain("{{setPasswordUrl}}", "the portal alone knows the link, and fills it");
        var callback = payload.GetProperty("callback");
        callback.GetProperty("url").GetString().Should().EndWith($"/api/public/onboarding/{id:D}/events");
        callback.GetProperty("token").GetString().Should().Be(Hex(secret, $"cadence-onboarding-callback:{id:N}"));

        var list = await JsonAsync(await owner.GetAsync($"/api/deals/{dealId}/onboarding"));
        list[0].GetProperty("status").GetString().Should().Be("delivered");

        var member = await app.Services.Read(db => db.ClubMembers.Single());
        member.OrganizationId.Should().Be(org);
        member.Email.Should().Be("jean@example.ch");
        member.Status.Should().Be("Pending");
        member.StartsOnUtc.Should().Be(new DateTime(2027, 1, 15));
        member.StartUndetermined.Should().BeFalse();
        member.DealId.Should().Be(dealId);
        member.ContactId.Should().NotBeNull("the member is linked to the deal's contact");
        (await app.Services.Read(db => db.Set<ClubHouseAccount>().IgnoreQueryFilters().Count(a => a.MemberId == member.Id)))
            .Should().Be(1, "a member added by a handoff gets the house account every member has");
        list[0].GetProperty("clubMemberId").GetGuid().Should().Be(member.Id);

        (await owner.GetAsync("/api/webhook/organizations/" + org + "/deliveries")).StatusCode.Should().Be(HttpStatusCode.OK);
        var deliveries = await JsonAsync(await owner.GetAsync($"/api/webhook/organizations/{org}/deliveries"));
        deliveries.EnumerateArray().Should().BeEmpty("a handoff is not one of the workspace's events, and shows on its deal");
    }

    [Fact]
    public async Task ThePortalsReportsMoveItAlong_OnlyWithTheHandoffsToken()
    {
        await using var app = new App();
        var (owner, _, secret, dealId) = await SetUpAsync(app);
        var id = (await JsonAsync(await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { welcomeMessage = "Welcome!" }))))
            .GetProperty("id").GetGuid();
        await app.DispatchOutboxAsync();
        var token = Hex(secret, $"cadence-onboarding-callback:{id:N}");
        using var portal = app.CreateClient();

        (await portal.SendAsync(Report(id, null, new { @event = "account_created" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await portal.SendAsync(Report(id, "0" + token[1..], new { @event = "account_created" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await portal.SendAsync(Report(Guid.NewGuid(), token, new { @event = "account_created" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a handoff that does not exist answers like a wrong token");
        (await portal.SendAsync(Report(id, token, new { @event = "hello" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await portal.SendAsync(Report(id, token, new { @event = "account_created", accountId = "m-42" }));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        (await JsonAsync(created)).GetProperty("status").GetString().Should().Be("account_ready");
        (await portal.SendAsync(Report(id, token, new { @event = "email_sent" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await portal.SendAsync(Report(id, token, new { @event = "email_sent" }))).StatusCode.Should().Be(HttpStatusCode.OK, "a repeat is taken, and changes nothing");
        (await portal.SendAsync(Report(id, token, new { @event = "account_existing" }))).StatusCode.Should().Be(HttpStatusCode.OK);

        var handoff = (await JsonAsync(await owner.GetAsync($"/api/deals/{dealId}/onboarding")))[0];
        handoff.GetProperty("status").GetString().Should().Be("welcomed", "a late account report does not move it back");
        handoff.GetProperty("externalAccountId").GetString().Should().Be("m-42");
        handoff.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("key").GetString())
            .Should().Equal("queued", "delivered", "account_created", "email_sent", "account_existing");

        (await portal.SendAsync(Report(id, token, new { @event = "password_set" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await portal.SendAsync(Report(id, token, new { @event = "failed", message = "late" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        handoff = (await JsonAsync(await owner.GetAsync($"/api/deals/{dealId}/onboarding")))[0];
        handoff.GetProperty("status").GetString().Should().Be("active", "someone who is in stays in");
        handoff.GetProperty("canResend").GetBoolean().Should().BeFalse();
        (await owner.PostAsync($"/api/onboarding/{id}/resend", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var notes = await app.Services.Read(db => db.Activities.Where(a => a.DealId == dealId && a.Type == "system").Select(a => a.Subject).ToList());
        notes.Should().Contain(new[]
        {
            "Welcome & portal: handed over", "Welcome & portal: welcome email sent", "Welcome & portal: they're in",
        });
        notes.Count(n => n == "Welcome & portal: welcome email sent").Should().Be(1, "a repeated report adds nothing to the timeline");
    }

    [Fact]
    public async Task AHandoffThePortalRefusesFails_AndResendTriesAgain()
    {
        await using var app = new App();
        var (owner, _, _, dealId) = await SetUpAsync(app);
        var id = (await JsonAsync(await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { })))).GetProperty("id").GetGuid();
        app.Hook.Answers.Enqueue(410);
        await app.DispatchOutboxAsync();

        var handoff = (await JsonAsync(await owner.GetAsync($"/api/deals/{dealId}/onboarding")))[0];
        handoff.GetProperty("status").GetString().Should().Be("failed");
        handoff.GetProperty("startUndetermined").GetBoolean().Should().BeTrue("no date means still to be decided");
        handoff.GetProperty("canResend").GetBoolean().Should().BeTrue();

        (await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { }))).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "one handoff per deal: sending again is Resend");

        var resent = await owner.PostAsync($"/api/onboarding/{id}/resend", null);
        resent.StatusCode.Should().Be(HttpStatusCode.OK, await resent.Content.ReadAsStringAsync());
        (await owner.PostAsync($"/api/onboarding/{id}/resend", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "the first resend is still on its way");
        await app.DispatchOutboxAsync();

        handoff = (await JsonAsync(await owner.GetAsync($"/api/deals/{dealId}/onboarding")))[0];
        handoff.GetProperty("status").GetString().Should().Be("delivered");
        app.Hook.Posts.Should().HaveCount(2);
        using var second = JsonDocument.Parse(app.Hook.Posts[1].Body);
        second.RootElement.GetProperty("payload").GetProperty("membership").GetProperty("startsOn").ValueKind.Should().Be(JsonValueKind.Null);
        second.RootElement.GetProperty("payload").GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null,
            "with no template of the workspace's own, the portal sends its own design");
    }

    [Fact]
    public async Task NothingIsHandedOverBeforeThePortalIsSetUp_OrWithAPlaceholderNobodyFills()
    {
        await using var app = new App();
        var (owner, _, _, dealId) = await SetUpAsync(app, portal: false);
        var res = await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Settings");

        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = "http://portal.example.com/hook" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "https only");
        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = "https://127.0.0.1/hook" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "never an address inside the network");
        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = Portal, emailHtml = "<p>{{favouriteColour}}</p>" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = Portal }))).StatusCode.Should().Be(HttpStatusCode.OK);
        await owner.PostAsync("/api/onboarding/settings/secret", null);

        var refused = await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { emailHtml = "<p>{{favouriteColour}}</p>" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("{{favouriteColour}}");
        (await app.Services.Read(db => db.DealOnboardings.Count())).Should().Be(0);

        var settings = await JsonAsync(await owner.GetAsync("/api/onboarding/settings"));
        settings.GetProperty("configured").GetBoolean().Should().BeTrue();
        settings.GetProperty("hasSecret").GetBoolean().Should().BeTrue();
        settings.TryGetProperty("webhookSecret", out _).Should().BeFalse("the secret is shown once, when it is made");
    }

    [Fact]
    public async Task ThePreviewFillsCadencesPlaceholders_AndStandsInForThePortals()
    {
        await using var app = new App();
        var (owner, _, _, dealId) = await SetUpAsync(app);
        var preview = await JsonAsync(await owner.PostAsync("/api/onboarding/preview", Body(new
        {
            dealId,
            language = "en",
            startUndetermined = true,
            welcomeMessage = "See you <soon>",
            emailHtml = "<h1>Hi {{firstName}}</h1><p>{{welcomeMessage}}</p><p>{{startDate}}</p><a href=\"{{setPasswordUrl}}\">{{loginEmail}}</a>",
        })));
        var html = preview.GetProperty("html").GetString()!;
        html.Should().Contain("Hi Jean").And.Contain("See you &lt;soon&gt;").And.Contain("to be confirmed");
        html.Should().Contain("href=\"#set-password\"").And.Contain("jean@example.ch");
        preview.GetProperty("usesReceiverDesign").GetBoolean().Should().BeFalse();

        var standIn = await JsonAsync(await owner.PostAsync("/api/onboarding/preview", Body(new { name = "Ana Silva", email = "ana@example.ch", welcomeMessage = "Olá" })));
        standIn.GetProperty("usesReceiverDesign").GetBoolean().Should().BeTrue();
        standIn.GetProperty("subject").GetString().Should().Be("Bienvenue chez Pavillon 46, Ana");
        standIn.GetProperty("html").GetString().Should().Contain("Olá").And.Contain("ana@example.ch");
    }

    [Fact]
    public async Task OnlySomeoneWhoManagesSettingsChangesThem_AndAnotherWorkspaceSeesNothing()
    {
        await using var app = new App();
        var (owner, org, _, dealId) = await SetUpAsync(app);
        var (member, memberId) = await SignInAsync(app, "member@example.ch");
        await AddMemberAsync(app, org, memberId, OrgMemberRole.Member);
        member.In(org);
        (await member.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = "https://evil.example.com/" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync("/api/onboarding/settings/secret", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/onboarding/settings")).StatusCode.Should().Be(HttpStatusCode.OK);

        await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new { }));
        var (stranger, strangerId) = await SignInAsync(app, "stranger@example.ch");
        var other = await CreateAsync(app, strangerId, "Elsewhere");
        stranger.In(other);
        (await stranger.GetAsync($"/api/deals/{dealId}/onboarding")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var id = await app.Services.Read(db => db.DealOnboardings.Single().Id);
        (await stranger.PostAsync($"/api/onboarding/{id}/resend", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
