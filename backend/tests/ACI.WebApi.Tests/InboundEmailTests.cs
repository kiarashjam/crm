using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What comes back after the outbox sends: replies through SendGrid Inbound Parse, and
/// bounces and spam reports through its Event Webhook.
/// </summary>
/// <remarks>
/// Nothing came back before. A reply reached the rep's inbox at best, and the sequence
/// carried on until somebody logged an activity by hand; a bounced address went on
/// bouncing, and a spam complaint went unheard.
/// </remarks>
public class InboundEmailTests
{
    private const string ParseSecret = "parse-secret-123";
    private const string EventsSecret = "events-secret-456";
    private const string ReplyDomain = "reply.cadence.example";

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();
        public Dictionary<string, string?> Settings { get; } = new()
        {
            ["Email:InboundReplyDomain"] = ReplyDomain,
            ["Email:InboundParseSecret"] = ParseSecret,
            ["Email:EventWebhookSecret"] = EventsSecret,
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(Settings));
            builder.ConfigureTestServices(services =>
            {
                services.Remove(services.Single(d => d.ImplementationType == typeof(ACI.WebApi.Services.LeadFollowUpBackgroundService)));
                services.AddSingleton(Mail.Mock.Object);
            });
        }

        public async Task RunDueStepsAsync()
        {
            using (var scope = Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ACI.Application.Services.ISequenceService>().RunDueAsync();
            await this.DispatchOutboxAsync();
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A lead in a two-email sequence whose first email has gone.</summary>
    private static async Task<(HttpClient Owner, Guid OwnerId, Guid LeadId, Guid EnrollmentId, OutboxEmail First)> EmailedLeadAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = (await Json(await owner.PostAsync("/api/sequences", Body(new
        {
            name = "New lead outreach", status = "active",
            steps = new[]
            {
                new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi {{firstName}}" },
                new { type = "email", dayOffset = 3, subject = "Still there?", body = "Hi again" },
            },
        })), "create sequence")).GetProperty("id").GetGuid();
        var leadId = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();
        var enrollmentId = (await Json(await owner.PostAsync("/api/sequences/enroll", Body(new
        {
            sequenceId, targetType = "lead", targetId = leadId, targetName = "Jean Dupont",
        })), "enroll")).GetProperty("id").GetGuid();
        await app.RunDueStepsAsync();
        return (owner, ownerId, leadId, enrollmentId, app.Mail.Sent.Single());
    }

    private static MultipartFormDataContent Reply(string to, string text = "Yes please, call me tomorrow.", string? headers = null, string from = "Jean Dupont <jean@example.ch>")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(from), "from" },
            { new StringContent(to), "to" },
            { new StringContent("Re: Hello"), "subject" },
            { new StringContent(text), "text" },
            { new StringContent(JsonSerializer.Serialize(new { to = new[] { to }, from = "jean@example.ch" })), "envelope" },
        };
        if (headers != null) form.Add(new StringContent(headers), "headers");
        return form;
    }

    private static async Task<string?> EnrollmentStatusAsync(HttpClient client, Guid enrollmentId) =>
        (await Json(await client.GetAsync("/api/sequences/enrollments"), "list enrollments")).EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == enrollmentId).GetProperty("status").GetString();

    [Fact]
    public async Task AReplyToTheMessagesOwnAddressGoesOnTheTimeline_StopsTheSequence_AndTellsTheOwner()
    {
        await using var app = new App();
        var (owner, ownerId, leadId, enrollmentId, first) = await EmailedLeadAsync(app);
        var replyAddress = first.ReplyTo.Single(a => a.EndsWith("@" + ReplyDomain));
        first.ReplyTo.Should().Contain("owner@example.ch", "the reply still reaches the person, as before");

        var res = await app.CreateClient().PostAsync($"/api/public/email/inbound/{ParseSecret}", Reply(replyAddress,
            "Yes please, call me tomorrow.\n\nOn Mon, 9 Nov 2026 at 10:00, Pavillon46 <info@pavillon46.ch> wrote:\n> Hi Jean",
            "Message-ID: <reply-1@mail.example.ch>\r\nSubject: Re: Hello"));

        (await Json(res, "inbound")).GetProperty("matched").GetBoolean().Should().BeTrue();
        var reply = await app.Services.Read(db => db.Activities.Single(a => a.LeadId == leadId && a.Subject!.StartsWith("Reply")));
        reply.Subject.Should().Be("Reply from Jean Dupont: Re: Hello");
        reply.Body.Should().Be("Yes please, call me tomorrow.", "the quoted conversation is left out");
        reply.Participants.Should().Be("jean@example.ch");
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped", "they answered, so the next email must not go");
        (await app.Services.Read(db => db.OutboundMessages.Single().RepliedAtUtc)).Should().NotBeNull();
        var notice = await app.Services.Read(db => db.Notifications.Single(n => n.UserId == ownerId && n.Title == "Jean Dupont replied"));
        notice.Link.Should().Be($"/leads/{leadId}");
        (await app.Services.Read(db => db.InboundEmails.Single().MatchedBy)).Should().Be("token");
        (await app.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseAtUtc))
            .Should().BeNull("the prospect writing is not somebody here answering them");
    }

    [Fact]
    public async Task TheSameReplyPostedTwiceIsRecordedOnce()
    {
        await using var app = new App();
        var (_, _, leadId, _, first) = await EmailedLeadAsync(app);
        var replyAddress = first.ReplyTo.Single(a => a.EndsWith("@" + ReplyDomain));
        var stranger = app.CreateClient();

        for (var i = 0; i < 2; i++)
            await Json(await stranger.PostAsync($"/api/public/email/inbound/{ParseSecret}", Reply(replyAddress, headers: "Message-ID: <reply-1@mail.example.ch>")), "inbound");

        (await app.Services.Read(db => db.Activities.Count(a => a.LeadId == leadId && a.Subject!.StartsWith("Reply")))).Should().Be(1);
    }

    [Fact]
    public async Task AReplyWhoseMessageIdIsTooLongToKeepIsStillTakenIn()
    {
        // InboundEmails keeps a Message-ID of up to 255 characters. Saving a longer one
        // fails on SQL Server, and the failure read as "taken in already": the reply was
        // dropped. It is taken in without its Message-ID instead.
        await using var app = new App();
        var (_, _, leadId, _, first) = await EmailedLeadAsync(app);
        var replyAddress = first.ReplyTo.Single(a => a.EndsWith("@" + ReplyDomain));

        await Json(await app.CreateClient().PostAsync($"/api/public/email/inbound/{ParseSecret}",
            Reply(replyAddress, headers: $"Message-ID: <{new string('m', 300)}@mail.example.ch>")), "inbound");

        (await app.Services.Read(db => db.InboundEmails.Single().MessageIdHeader)).Should().BeNull();
        (await app.Services.Read(db => db.Activities.Count(a => a.LeadId == leadId && a.Subject!.StartsWith("Reply")))).Should().Be(1);
    }

    [Fact]
    public async Task AReplyIsMatchedByInReplyTo_WhenTheReplyAddressIsNotOnIt()
    {
        // A rep forwarding the reply on, say: the token address is gone, the thread is not.
        await using var app = new App();
        var (owner, _, leadId, enrollmentId, first) = await EmailedLeadAsync(app);

        var res = await app.CreateClient().PostAsync($"/api/public/email/inbound/{ParseSecret}", Reply(
            $"crm@{ReplyDomain}", headers: $"Message-ID: <reply-2@mail.example.ch>\r\nIn-Reply-To: {first.MessageId}"));

        (await Json(res, "inbound")).GetProperty("matched").GetBoolean().Should().BeTrue();
        (await app.Services.Read(db => db.InboundEmails.Single().MatchedBy)).Should().Be("in-reply-to");
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped");
        (await app.Services.Read(db => db.Activities.Any(a => a.LeadId == leadId && a.Subject!.StartsWith("Reply")))).Should().BeTrue();
    }

    [Fact]
    public async Task MailThatAnswersNothingIsDropped()
    {
        await using var app = new App();
        await EmailedLeadAsync(app);

        var res = await app.CreateClient().PostAsync($"/api/public/email/inbound/{ParseSecret}", Reply(
            $"reply+notarealtoken0000000@{ReplyDomain}", headers: "In-Reply-To: <made-up@example.ch>"));

        (await Json(res, "inbound")).GetProperty("matched").GetBoolean().Should().BeFalse();
        (await app.Services.Read(db => db.InboundEmails.Count())).Should().Be(0);
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("parse-secret-12")]
    public async Task AWrongSecretIsNotFound(string secret)
    {
        await using var app = new App();

        (await app.CreateClient().PostAsync($"/api/public/email/inbound/{secret}", Reply("x@example.ch"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.CreateClient().PostAsync($"/api/public/email/events/{secret}", new StringContent("[]"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithNothingConfiguredTheEndpointsAreNotThere()
    {
        await using var app = new App();
        app.Settings["Email:InboundParseSecret"] = "";
        app.Settings["Email:EventWebhookSecret"] = "";

        (await app.CreateClient().PostAsync("/api/public/email/inbound/anything", Reply("x@example.ch"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.CreateClient().PostAsync("/api/public/email/events", new StringContent("[]"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static StringContent Events(params object[] events) =>
        new(JsonSerializer.Serialize(events), Encoding.UTF8, "application/json");

    [Fact]
    public async Task ABounceSuppressesTheAddress_StopsTheSequence_AndTheComposerRefusesIt()
    {
        await using var app = new App();
        var (owner, _, leadId, enrollmentId, first) = await EmailedLeadAsync(app);

        var res = await app.CreateClient().PostAsync($"/api/public/email/events/{EventsSecret}", Events(new Dictionary<string, object>
        {
            ["email"] = "jean@example.ch", ["event"] = "bounce", ["type"] = "bounce", ["timestamp"] = 1794200000,
            ["reason"] = "550 5.1.1 The email account that you tried to reach does not exist", ["cadence_message_id"] = first.OutboundMessageId.ToString(),
        }));

        (await Json(res, "events")).GetProperty("matched").GetInt32().Should().Be(1);
        var suppression = await app.Services.Read(db => db.EmailSuppressions.Single());
        suppression.Email.Should().Be("jean@example.ch");
        suppression.Reason.Should().Be("bounced");
        (await app.Services.Read(db => db.OutboundMessages.Single().DeliveryStatus)).Should().Be("bounced");
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped");

        var again = await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello?", body = "Hi", leadId }));
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await again.Content.ReadAsStringAsync()).Should().Contain("jean@example.ch bounced on");
    }

    [Fact]
    public async Task AManagerSeesTheBouncedAddress_AndCanLiftIt_ARepCannot()
    {
        await using var app = new App();
        var (owner, _, leadId, _, first) = await EmailedLeadAsync(app);
        await Json(await app.CreateClient().PostAsync($"/api/public/email/events/{EventsSecret}", Events(new Dictionary<string, object>
        {
            ["email"] = "jean@example.ch", ["event"] = "bounce", ["type"] = "bounce", ["reason"] = "550 mailbox full for good",
            ["cadence_message_id"] = first.OutboundMessageId.ToString(),
        })), "events");
        var org = await app.Services.Read(db => db.Organizations.Single().Id);
        var (rep, repId) = await SignInAsync(app, "rep@example.ch");
        await AddMemberAsync(app, org, repId, ACI.Domain.Enums.OrgMemberRole.Member);
        rep.In(org);

        var listed = (await Json(await rep.GetAsync($"/api/organizations/{org}/email/suppressions"), "rep lists")).EnumerateArray().Single();
        listed.GetProperty("email").GetString().Should().Be("jean@example.ch");
        listed.GetProperty("reason").GetString().Should().Be("bounced");
        listed.GetProperty("detail").GetString().Should().Be("550 mailbox full for good");
        var id = listed.GetProperty("id").GetGuid();

        (await rep.DeleteAsync($"/api/organizations/{org}/email/suppressions/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.DeleteAsync($"/api/organizations/{org}/email/suppressions/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/organizations/{org}/email/suppressions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "New address?", body = "Hi", leadId })), "the composer may write again");
    }

    [Fact]
    public async Task ASpamReportFoundByItsMessageIdSuppressesTheAddress()
    {
        await using var app = new App();
        var (_, _, _, _, first) = await EmailedLeadAsync(app);

        await Json(await app.CreateClient().PostAsync($"/api/public/email/events/{EventsSecret}", Events(
            new Dictionary<string, object> { ["email"] = "jean@example.ch", ["event"] = "delivered", ["timestamp"] = 1794200000, ["smtp-id"] = first.MessageId },
            new Dictionary<string, object> { ["email"] = "jean@example.ch", ["event"] = "spamreport", ["timestamp"] = 1794200100, ["smtp-id"] = first.MessageId })), "events");

        (await app.Services.Read(db => db.EmailSuppressions.Single().Reason)).Should().Be("spam_report");
        (await app.Services.Read(db => db.OutboundMessages.Single().DeliveryStatus)).Should().Be("spam_report");
    }

    [Fact]
    public async Task ADeliveryIsRecorded_AndABlockedMessageDoesNotSuppressTheAddress()
    {
        await using var app = new App();
        var (_, _, _, _, first) = await EmailedLeadAsync(app);
        var id = first.OutboundMessageId.ToString();

        await Json(await app.CreateClient().PostAsync($"/api/public/email/events/{EventsSecret}", Events(
            new Dictionary<string, object> { ["email"] = "jean@example.ch", ["event"] = "bounce", ["type"] = "blocked", ["reason"] = "421 try later", ["cadence_message_id"] = id },
            new Dictionary<string, object> { ["email"] = "jean@example.ch", ["event"] = "open", ["timestamp"] = 1794200200, ["cadence_message_id"] = id })), "events");

        var message = await app.Services.Read(db => db.OutboundMessages.Single());
        message.DeliveryStatus.Should().Be("blocked");
        message.OpenedAtUtc.Should().NotBeNull();
        (await app.Services.Read(db => db.EmailSuppressions.Count())).Should().Be(0, "a block is about now, not the address");
    }

    [Fact]
    public async Task AReportAboutMailNobodyHereSentChangesNothing()
    {
        await using var app = new App();
        await EmailedLeadAsync(app);

        var res = await app.CreateClient().PostAsync($"/api/public/email/events/{EventsSecret}", Events(new Dictionary<string, object>
        {
            ["email"] = "jean@example.ch", ["event"] = "bounce", ["type"] = "bounce", ["cadence_message_id"] = Guid.NewGuid().ToString(),
        }));

        (await Json(res, "events")).GetProperty("matched").GetInt32().Should().Be(0);
        (await app.Services.Read(db => db.EmailSuppressions.Count())).Should().Be(0);
    }

    [Fact]
    public async Task ASignedEventWebhookIsCheckedWithTheKeySendGridShows()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var app = new App();
        app.Settings["Email:EventWebhookSecret"] = "";
        app.Settings["Email:EventWebhookVerificationKey"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var (_, _, _, _, first) = await EmailedLeadAsync(app);
        var body = JsonSerializer.Serialize(new[] { new Dictionary<string, object> { ["email"] = "jean@example.ch", ["event"] = "delivered", ["cadence_message_id"] = first.OutboundMessageId.ToString() } });
        const string timestamp = "1794200000";
        var signature = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(timestamp + body), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        HttpRequestMessage Post(string? sig) => new(HttpMethod.Post, "/api/public/email/events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers =
            {
                { "X-Twilio-Email-Event-Webhook-Timestamp", timestamp },
                { "X-Twilio-Email-Event-Webhook-Signature", sig ?? "MEUCIQDl" },
            },
        };

        (await app.CreateClient().SendAsync(Post(null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await app.Services.Read(db => db.OutboundMessages.Single().DeliveryStatus)).Should().BeNull();
        await Json(await app.CreateClient().SendAsync(Post(signature)), "signed events");
        (await app.Services.Read(db => db.OutboundMessages.Single().DeliveryStatus)).Should().Be("delivered");
    }
}
