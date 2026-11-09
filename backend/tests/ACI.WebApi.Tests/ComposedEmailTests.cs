using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// POST /api/email/send — the composer on lead and contact pages, and the sales
/// assistant's confirmed emails. It used to post to a route that did not exist, and
/// the app reported "Email sent" regardless.
/// </summary>
public class ComposedEmailTests
{
    /// <summary>The app with a mail server that remembers what it took.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    private static object Email(string to = "jean@example.ch") =>
        new { to, subject = "Votre visite", body = "Bonjour Jean" };

    private static async Task<(HttpClient Client, Guid Org)> Member(CustomWebApplicationFactory factory, string email = "rep@example.ch")
    {
        var (client, user) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, user, "Pavillon 46");
        return (client.In(org), org);
    }

    private static async Task<Guid> LeadAsync(HttpClient client, string name = "Jean Dupont", string email = "jean@example.ch")
    {
        var res = await client.PostAsync("/api/leads", Body(new { name, email, status = "New" }));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task SendsAndSaysSo()
    {
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);

        var res = await client.PostAsync("/api/email/send", Body(Email()));

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("sent");
        var sent = factory.Mail.Sent.Should().ContainSingle().Subject;
        sent.To.Should().Equal("jean@example.ch");
        sent.FromName.Should().Be("Pavillon 46", "sent under the workspace's name");
        sent.ReplyTo.Should().Equal(new[] { "rep@example.ch" }, "a reply reaches the person who wrote it, not the shared sender address");
        // One id for the message, which the outbox keeps and the Message-ID header carries.
        var id = doc.RootElement.GetProperty("id").GetGuid();
        sent.OutboundMessageId.Should().Be(id);
        sent.MessageId.Should().Be($"<{id:N}@pavillon46.ch>", "from the configured sender's domain");
        (await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Id == id).Status)).Should().Be("sent");
    }

    [Fact]
    public async Task TheEmailIsOnTheRecordsTimelineOnce_WrittenByTheServer()
    {
        // The browser wrote the timeline entry after a send, and the assistant wrote
        // none, so its emails never showed on the lead and nobody knew one had gone.
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);
        var leadId = await LeadAsync(client);

        var res = await client.PostAsync("/api/email/send", Body(new
        {
            to = "jean@example.ch", subject = "Following up", body = "Hi {{firstName}}", leadId, source = "assistant",
        }));

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        factory.Mail.Sent.Single().TextBody.Should().StartWith("Hi Jean", "the placeholder is filled from the lead");
        var activity = await factory.Services.Read(db => db.Activities.Single(a => a.LeadId == leadId && a.Type == "email"));
        activity.Subject.Should().Be("Email: Following up");
        activity.Body.Should().Be("Hi Jean");
        (await factory.Services.Read(db => db.OutboundMessages.Single().Source)).Should().Be("assistant");
        // A person's email is their response, as when they log one by hand.
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseAtUtc)).Should().NotBeNull();
    }

    [Fact]
    public async Task APlaceholderWithNothingToFillItIsRefused_NotSentAsHiComma()
    {
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);

        var res = await client.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi {{firstName}}, about {{company}}" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("{{company}}");
        factory.Mail.Attempts.Should().BeEmpty();

        // A fallback fills it instead.
        (await client.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi {{firstName|there}}" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Mail.Sent.Single().TextBody.Should().StartWith("Hi there").And.NotContain("{{");
    }

    [Fact]
    public async Task WhenTheServerDoesNotTakeItYet_ItIsQueuedAndRetried_NotLost()
    {
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);
        factory.Mail.Answer = MailServer.NotNow();

        var res = await client.PostAsync("/api/email/send", Body(Email()));

        res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("queued");
        var id = doc.RootElement.GetProperty("id").GetGuid();
        var queued = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Id == id));
        queued.Status.Should().Be("queued");
        queued.NextAttemptAtUtc.Should().BeAfter(DateTime.UtcNow, "it waits before the next attempt");

        factory.Mail.Answer = OutboxSendResult.Sent;
        await factory.MakeQueuedDueAsync();
        await factory.DispatchOutboxAsync();

        factory.Mail.Sent.Should().ContainSingle();
        (await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Id == id).Status)).Should().Be("sent");
    }

    [Fact]
    public async Task AQueuedEmailCanBeCancelledBeforeItGoes()
    {
        await using var factory = new WithMailServer();
        var (client, org) = await Member(factory);
        factory.Mail.Answer = MailServer.NotNow();
        var id = JsonDocument.Parse(await (await client.PostAsync("/api/email/send", Body(Email()))).Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();
        var (colleague, colleagueId) = await SignInAsync(factory, "colleague@club.ch");
        await AddMemberAsync(factory, org, colleagueId, OrgMemberRole.Member);

        (await colleague.In(org).PostAsync($"/api/email/{id}/cancel", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "only whoever sent it, or a manager");
        (await client.PostAsync($"/api/email/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        factory.Mail.Answer = OutboxSendResult.Sent;
        await factory.MakeQueuedDueAsync();
        await factory.DispatchOutboxAsync();
        factory.Mail.Sent.Should().BeEmpty("it was cancelled");
        (await client.PostAsync($"/api/email/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ARefusalForGoodIsNamed_AndTheEmailNeverGoes()
    {
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);
        factory.Mail.Answer = MailServer.Never("550 5.1.1 No such user");

        var res = await client.PostAsync("/api/email/send", Body(Email()));

        res.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await res.Content.ReadAsStringAsync()).Should().Contain("550 5.1.1 No such user");
        (await factory.Services.Read(db => db.OutboundMessages.Single().Status)).Should().Be("failed");
    }

    [Fact]
    public async Task WithoutAMailServerItSaysSo_AndNothingIsSentLater()
    {
        // The test host, like a machine with no Email:SmtpHost, has no mail server.
        await using var factory = new CustomWebApplicationFactory();
        var (client, _) = await Member(factory);

        var res = await client.PostAsync("/api/email/send", Body(Email()));

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Sending email is not set up on the server yet.");
        // The person was told it was not sent and may send it another way.
        (await factory.Services.Read(db => db.OutboundMessages.Single().Status)).Should().Be("failed");
    }

    [Fact]
    public async Task AViewerCannotSend_WithOrWithoutTheWorkspaceHeader()
    {
        await using var factory = new WithMailServer();
        var (_, org) = await Member(factory);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@club.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);

        (await viewer.PostAsync("/api/email/send", Body(Email()))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "no workspace, no email");
        (await viewer.In(org).PostAsync("/api/email/send", Body(Email()))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        factory.Mail.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task AWrongAddressIsNamed()
    {
        await using var factory = new WithMailServer();
        var (client, _) = await Member(factory);

        var res = await client.PostAsync("/api/email/send", Body(Email("jean@example")));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("detail").GetString().Should().Be("\"jean@example\" is not a valid email address.");
    }

    [Fact]
    public async Task OnePersonCannotTurnItIntoAMassMailer()
    {
        await using var factory = new WithMailServer();
        var (client, org) = await Member(factory);
        var (colleague, colleagueId) = await SignInAsync(factory, "colleague@club.ch");
        await AddMemberAsync(factory, org, colleagueId, OrgMemberRole.Member);

        for (var i = 0; i < 20; i++)
            (await client.PostAsync("/api/email/send", Body(Email()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/api/email/send", Body(Email()))).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // The limit is per person: a colleague can still send.
        (await colleague.In(org).PostAsync("/api/email/send", Body(Email()))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static OutboxEmail Outbox(
        IReadOnlyList<string> to, IReadOnlyList<string>? cc = null, IReadOnlyList<string>? bcc = null,
        string? fromName = null, string? fromAddress = null, IReadOnlyList<string>? replyTo = null, string? recipientName = null,
        string? unsubscribe = null) =>
        new(fromName, fromAddress, replyTo ?? [], to, cc ?? [], bcc ?? [], recipientName, "Votre visite", "Bonjour", "<p>Bonjour</p>",
            "<0f8fad5bd9cb469fa16570867728950e@pavillon46.ch>", unsubscribe, Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));

    [Fact]
    public void TheMessageCarriesEveryRecipientAndBothParts()
    {
        var message = SmtpEmailSender.BuildOutboxMessage(
            Outbox(["jean@example.ch", "anne@example.ch"], ["boss@club.ch"], ["archive@club.ch"]), "Cadence", "info@pavillon46.ch");

        message.From.ToString().Should().Contain("info@pavillon46.ch");
        message.To.Mailboxes.Select(m => m.Address).Should().Equal("jean@example.ch", "anne@example.ch");
        message.Cc.Mailboxes.Select(m => m.Address).Should().Equal("boss@club.ch");
        message.Bcc.Mailboxes.Select(m => m.Address).Should().Equal("archive@club.ch");
        message.TextBody.Should().Be("Bonjour");
        message.HtmlBody.Should().Be("<p>Bonjour</p>");
        message.ReplyTo.Mailboxes.Should().BeEmpty("no one was named to reply to");
        message.MessageId.Should().Be("0f8fad5bd9cb469fa16570867728950e@pavillon46.ch", "a reply names it in In-Reply-To");
        message.Headers.Contains("X-SMTPAPI").Should().BeFalse("only SendGrid reads it; any other relay would pass it on");
    }

    [Fact]
    public void AReplyGoesToThePersonNamed_AndToTheReplyAddress_FromTheVerifiedAddress()
    {
        var message = SmtpEmailSender.BuildOutboxMessage(
            Outbox(["jean@example.ch"], fromName: "Acme Rentals", replyTo: ["anna@acme.example", "reply+abc@reply.cadence.example"]),
            "Cadence", "info@pavillon46.ch");

        var from = message.From.Mailboxes.Single();
        from.Name.Should().Be("Acme Rentals", "not the product's name");
        from.Address.Should().Be("info@pavillon46.ch", "the relay only sends from the address it has verified");
        message.ReplyTo.Mailboxes.Select(m => m.Address).Should().Equal("anna@acme.example", "reply+abc@reply.cadence.example");
    }

    [Fact]
    public void AWorkspaceOnAVerifiedDomainSendsFromItsOwnAddress()
    {
        var message = SmtpEmailSender.BuildOutboxMessage(
            Outbox(["jean@example.ch"], fromName: "Acme Rentals", fromAddress: "hello@acme.example", recipientName: "Jean Dupont"),
            "Cadence", "info@pavillon46.ch");

        message.From.Mailboxes.Single().Address.Should().Be("hello@acme.example");
        message.To.Mailboxes.Single().Name.Should().Be("Jean Dupont");
    }

    [Fact]
    public void SendGridIsToldWhichMessageItIs_AndMailAppsHowToUnsubscribe()
    {
        var message = SmtpEmailSender.BuildOutboxMessage(
            Outbox(["jean@example.ch"], unsubscribe: "https://crm.example.ch/unsubscribe/t"), "Cadence", "info@pavillon46.ch", tagForSendGrid: true);

        message.Headers["X-SMTPAPI"].Should().Be("{\"unique_args\":{\"cadence_message_id\":\"0f8fad5b-d9cb-469f-a165-70867728950e\"}}");
        message.Headers["List-Unsubscribe"].Should().Be("<https://crm.example.ch/unsubscribe/t>");
    }
}
