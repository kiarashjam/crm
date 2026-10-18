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
/// POST /api/email/send — the composer on lead and contact pages. It used to post
/// to a route that did not exist, and the app reported "Email sent" regardless.
/// </summary>
public class ComposedEmailTests
{
    /// <summary>The app with a mail server that takes everything, and remembers it.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly List<ComposedEmail> Sent = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
                    .Callback((ComposedEmail email, CancellationToken _) => Sent.Add(email))
                    .ReturnsAsync(ComposedEmailOutcome.Sent);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private static object Email(string to = "jean@example.ch") =>
        new { to, subject = "Votre visite", body = "Bonjour Jean", leadId = Guid.NewGuid() };

    private static async Task<(HttpClient Client, Guid Org)> Member(CustomWebApplicationFactory factory, string email = "kia@club.ch")
    {
        var (client, user) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, user, "Pavillon 46");
        return (client.In(org), org);
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
        var sent = factory.Sent.Should().ContainSingle().Subject;
        sent.To.Should().Equal("jean@example.ch");
        sent.FromName.Should().Be("Pavillon 46", "sent under the workspace's name");
    }

    [Fact]
    public async Task WithoutAMailServerItSaysSo_AndNothingIsSent()
    {
        // The test host, like a machine with no Email:SmtpHost, has no mail server.
        await using var factory = new CustomWebApplicationFactory();
        var (client, _) = await Member(factory);

        var res = await client.PostAsync("/api/email/send", Body(Email()));

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Sending email is not set up on the server yet.");
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
        factory.Sent.Should().BeEmpty();
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

    [Fact]
    public void TheMessageCarriesEveryRecipientAndBothParts()
    {
        var message = SmtpEmailSender.BuildComposedMessage(
            new ComposedEmail(["jean@example.ch", "anne@example.ch"], ["boss@club.ch"], ["archive@club.ch"], "Votre visite", "Bonjour", "<p>Bonjour</p>"),
            "Pavillon 46", "info@pavillon46.ch");

        message.From.ToString().Should().Contain("info@pavillon46.ch");
        message.To.Mailboxes.Select(m => m.Address).Should().Equal("jean@example.ch", "anne@example.ch");
        message.Cc.Mailboxes.Select(m => m.Address).Should().Equal("boss@club.ch");
        message.Bcc.Mailboxes.Select(m => m.Address).Should().Equal("archive@club.ch");
        message.TextBody.Should().Be("Bonjour");
        message.HtmlBody.Should().Be("<p>Bonjour</p>");
    }
}
