using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The unsubscribe link at the foot of a workspace's email, followed by the person
/// it was sent to, who has no account.
/// </summary>
/// <remarks>
/// There was no such link, and no List-Unsubscribe header. The only way out of a
/// sequence was to reply and hope someone read it; do not contact could only be set
/// by staff, and for a lead it did nothing at all.
/// </remarks>
public class UnsubscribeTests
{
    /// <summary>The app with a mail server that remembers everything, and due steps run by the test.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly List<OutreachEmail> Outreach = new();
        public readonly List<ComposedEmail> Composed = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.Remove(services.Single(d => d.ImplementationType == typeof(ACI.WebApi.Services.LeadFollowUpBackgroundService)));
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()))
                    .Callback((OutreachEmail email, CancellationToken _) => Outreach.Add(email))
                    .ReturnsAsync(true);
                sender.Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
                    .Callback((ComposedEmail email, CancellationToken _) => Composed.Add(email))
                    .ReturnsAsync(ComposedEmailOutcome.Sent);
                services.AddSingleton(sender.Object);
            });
        }

        public async Task RunDueStepsAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISequenceService>().RunDueAsync();
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The token of the unsubscribe link for this address in a message's text.</summary>
    private static string TokenFor(string text, string? address = null)
    {
        var pattern = address == null
            ? @"/unsubscribe/([A-Za-z0-9_\-%]+)"
            : Regex.Escape(address) + @": \S+/unsubscribe/([A-Za-z0-9_\-%]+)";
        var match = Regex.Match(text, pattern);
        match.Success.Should().BeTrue($"the email should carry an unsubscribe link{(address == null ? "" : " for " + address)}:\n{text}");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static async Task<(HttpClient Owner, Guid Org, Guid LeadId, Guid EnrollmentId)> EnrolledLeadAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        var sequence = await Json(await owner.PostAsync("/api/sequences", Body(new
        {
            name = "New lead outreach", status = "active",
            steps = new[]
            {
                new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi {{firstName}}" },
                new { type = "email", dayOffset = 3, subject = "Still there?", body = "Hi again" },
            },
        })), "create sequence");
        var leadId = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();
        var enrollmentId = (await Json(await owner.PostAsync("/api/sequences/enroll", Body(new
        {
            sequenceId = sequence.GetProperty("id").GetGuid(), targetType = "lead", targetId = leadId, targetName = "Jean Dupont",
        })), "enroll")).GetProperty("id").GetGuid();
        return (owner, org, leadId, enrollmentId);
    }

    private static async Task<string?> EnrollmentStatusAsync(HttpClient client, Guid enrollmentId) =>
        (await Json(await client.GetAsync("/api/sequences/enrollments"), "list enrollments")).EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == enrollmentId).GetProperty("status").GetString();

    [Fact]
    public async Task ASequenceEmailCarriesALinkThatStopsEverything()
    {
        await using var app = new App();
        var (owner, _, leadId, enrollmentId) = await EnrolledLeadAsync(app);
        await app.RunDueStepsAsync();
        var email = app.Outreach.Should().ContainSingle().Subject;
        email.TextBody.Should().StartWith("Hi Jean").And.Contain("Don't want emails from Pavillon46? Unsubscribe: http://localhost:5173/unsubscribe/");
        email.HtmlBody.Should().Contain(">Unsubscribe</a>");
        email.UnsubscribeUrl.Should().StartWith("http://localhost:5173/unsubscribe/", "for the List-Unsubscribe header");
        var token = TokenFor(email.TextBody);
        var stranger = app.CreateClient();

        // Opening the link only says what it will do: mail scanners open every link.
        var preview = await Json(await stranger.GetAsync($"/api/public/unsubscribe/{token}"), "open the link");
        preview.GetProperty("workspaceName").GetString().Should().Be("Pavillon46");
        preview.GetProperty("email").GetString().Should().Be("jean@example.ch");
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("active");

        await Json(await stranger.PostAsync($"/api/public/unsubscribe/{token}", null), "unsubscribe");

        (await app.Services.Read(db => db.Contacts.Single(c => c.ConvertedFromLeadId == leadId).DoNotContact)).Should().BeTrue();
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped");
        (await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "One more thing", body = "Hi", leadId })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the composer must respect it too");
    }

    [Fact]
    public async Task AnAutomationEmailCarriesTheLinkToo()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "send_email", config = new { subject = "Welcome", body = "Hi {{firstName}}" } } },
        })), "add rule");

        await Json(await owner.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "lead arrives");

        var email = app.Outreach.Should().ContainSingle().Subject;
        email.UnsubscribeUrl.Should().NotBeNull();
        email.TextBody.Should().Contain("Don't want emails from Pavillon46? Unsubscribe:");
    }

    [Fact]
    public async Task SomeoneOnlyCopiedOnAComposedEmailCanUnsubscribeToo_AndItHolds()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        await Json(await owner.PostAsync("/api/email/send", Body(new
        {
            to = "jean@example.ch", cc = "partner@example.ch", subject = "Votre visite", body = "Bonjour",
        })), "send");
        var token = TokenFor(app.Composed.Single().TextBody, "partner@example.ch");

        await Json(await app.CreateClient().PostAsync($"/api/public/unsubscribe/{token}", null), "unsubscribe");

        // Nobody had a record for this address, so one now keeps the "stop".
        (await app.Services.Read(db => db.Contacts.Single(c => c.Email == "partner@example.ch").DoNotContact)).Should().BeTrue();
        var again = await owner.PostAsync("/api/email/send", Body(new { to = "partner@example.ch", subject = "Hello", body = "Hi" }));
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // Jean, who was also on the email, is still reachable.
        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "send to Jean");
    }

    [Fact]
    public async Task ALinkOnlyStopsTheWorkspaceThatSentIt()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var (other, otherId) = await SignInAsync(app, "other@example.ch");
        other.In(await CreateAsync(app, otherId, "Club Nautique"));
        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "Pavillon46 writes");

        await Json(await app.CreateClient().PostAsync($"/api/public/unsubscribe/{TokenFor(app.Composed.Single().TextBody)}", null), "unsubscribe");

        await Json(await other.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "Club Nautique still may");
        (await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AMadeUpLinkDoesNothing(string token)
    {
        await using var app = new App();
        var stranger = app.CreateClient();

        (await stranger.GetAsync($"/api/public/unsubscribe/{token}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/public/unsubscribe/{token}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TheMessageAsksMailAppsToOfferUnsubscribe()
    {
        var message = SmtpEmailSender.BuildOutreachMessage(
            new OutreachEmail("jean@example.ch", "Jean", "Hello", "Hi", "<p>Hi</p>", "Pavillon46", null, "https://crm.example.ch/unsubscribe/tok"),
            "Cadence", "info@pavillon46.ch");

        message.Headers["List-Unsubscribe"].Should().Be("<https://crm.example.ch/unsubscribe/tok>");
    }
}
