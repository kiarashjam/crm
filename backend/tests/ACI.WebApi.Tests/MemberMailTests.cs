using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Email to a club member through the outbox: marketing only with their consent and with a
/// link that withdraws it, service messages whatever they said, and both asked again when
/// the email goes.
/// </summary>
/// <remarks>
/// Nothing sent members anything: campaigns and the communications log only wrote "Sent".
/// Before they deliver, the outbox has to know what a member agreed to, and the member has
/// to have a way out that works without an account, from the email itself and from their
/// mail app's own Unsubscribe button.
/// </remarks>
public class MemberMailTests
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

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org, Guid MemberId);

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Club> ClubAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "olga@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        var member = await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
        })), "add member");
        return new Club(owner, ownerId, org, member.GetProperty("id").GetGuid());
    }

    private static async Task ConsentAsync(Club club, bool granted) =>
        await Json(await club.Owner.PostAsync($"/api/members/{club.MemberId}/consent", Body(new
        {
            channel = "email", purpose = "marketing", granted, source = "form",
        })), granted ? "record consent" : "record a withdrawal");

    private static async Task<Result<OutboundMessage>> QueueAsync(App app, Club club, string purpose, string subject = "The autumn gala")
    {
        using var scope = app.Services.CreateScope();
        using var acting = scope.ServiceProvider.GetRequiredService<ITenantContext>().ActFor(club.Org, club.OwnerId);
        return await scope.ServiceProvider.GetRequiredService<IOutbox>().QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = club.Org, Source = OutboundSources.Member, To = ["jean@example.ch"], RecipientName = "Jean Dupont",
            Subject = subject, Body = "Dear {{firstName}},\nJoin us on Friday.", CreatedByUserId = club.OwnerId, ReplyToUserId = club.OwnerId,
            MemberId = club.MemberId, Purpose = purpose,
        });
    }

    private static string TokenFrom(string text)
    {
        var match = Regex.Match(text, @"https://crm\.example\.ch/unsubscribe/([A-Za-z0-9_\-%]+)");
        match.Success.Should().BeTrue($"the email should carry an unsubscribe link:\n{text}");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static async Task<string> ConsentStateAsync(Club club) =>
        (await Json(await club.Owner.GetAsync($"/api/members/{club.MemberId}/consent"), "read consent"))
            .GetProperty("current").EnumerateArray()
            .Single(s => s.GetProperty("channel").GetString() == "email" && s.GetProperty("purpose").GetString() == "marketing")
            .GetProperty("state").GetString()!;

    [Fact]
    public async Task MarketingGoesOnlyToAMemberWhoAgreed()
    {
        await using var app = new App();
        var club = await ClubAsync(app);

        var refused = await QueueAsync(app, club, ConsentPurposes.Marketing);
        refused.IsSuccess.Should().BeFalse("nobody has recorded that Jean agreed");
        refused.Error.Code.Should().Be("Consent.Missing");

        await ConsentAsync(club, granted: true);
        (await QueueAsync(app, club, ConsentPurposes.Marketing)).IsSuccess.Should().BeTrue();
        await app.DispatchOutboxAsync();

        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.To.Should().Equal("jean@example.ch");
        email.TextBody.Should().StartWith("Dear Jean,").And.Contain("Don't want news and offers from Pavillon46 by email? Unsubscribe: https://crm.example.ch/unsubscribe/");
        email.HtmlBody.Should().Contain(">Unsubscribe</a>");
        email.UnsubscribeUrl.Should().StartWith("https://api.example.ch/api/public/unsubscribe/").And.EndWith("/one-click");
        email.OneClickUnsubscribe.Should().BeTrue();

        var mime = SmtpEmailSender.BuildOutboxMessage(email, "Cadence", "info@pavillon46.ch");
        mime.Headers["List-Unsubscribe"].Should().Be($"<{email.UnsubscribeUrl}>", "one https address, for the mail app's own button");
        mime.Headers["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task TheLinkInAMarketingEmailWithdrawsConsentAndLeavesServiceMessagesComing()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await ConsentAsync(club, granted: true);
        (await QueueAsync(app, club, ConsentPurposes.Marketing)).IsSuccess.Should().BeTrue();
        await app.DispatchOutboxAsync();
        var token = TokenFrom(app.Mail.Sent.Single().TextBody);
        var stranger = app.CreateClient();

        // Opening the link changes nothing: mail scanners open every link.
        var preview = await Json(await stranger.GetAsync($"/api/public/unsubscribe/{token}"), "open the link");
        preview.GetProperty("workspaceName").GetString().Should().Be("Pavillon46");
        preview.GetProperty("email").GetString().Should().Be("jean@example.ch");
        preview.GetProperty("scope").GetString().Should().Be("marketing");
        (await ConsentStateAsync(club)).Should().Be("granted");

        (await Json(await stranger.PostAsync($"/api/public/unsubscribe/{token}", null), "unsubscribe"))
            .GetProperty("scope").GetString().Should().Be("marketing");

        (await ConsentStateAsync(club)).Should().Be("withdrawn");
        var withdrawal = await app.Services.Read(db => db.ConsentRecords.Single(c => c.SubjectId == club.MemberId && c.IsCurrent));
        withdrawal.Source.Should().Be(ConsentSources.Unsubscribe, "recorded as the member's own act, with its source");
        withdrawal.RecordedByUserId.Should().BeNull("nobody on the staff did it");
        withdrawal.RecordedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        withdrawal.Address.Should().Be("jean@example.ch");

        var again = await QueueAsync(app, club, ConsentPurposes.Marketing);
        again.Error.Code.Should().Be("Consent.Withdrawn");
        again.Error.Description.Should().Contain("unsubscribed");

        (await QueueAsync(app, club, ConsentPurposes.Service, "Your October statement")).IsSuccess.Should().BeTrue(
            "a statement is part of the membership, whatever the member said about news");
        (await app.Services.Read(db => db.Contacts.Count())).Should().Be(0,
            "a member's unsubscribe is a consent answer, not a do-not-contact contact made for them");

        // Followed twice, it is recorded once.
        await Json(await stranger.PostAsync($"/api/public/unsubscribe/{token}", null), "unsubscribe again");
        (await app.Services.Read(db => db.ConsentRecords.Count(c => c.SubjectId == club.MemberId))).Should().Be(2);
    }

    [Fact]
    public async Task AMailAppsOwnButtonUnsubscribesInOneClick()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await ConsentAsync(club, granted: true);
        (await QueueAsync(app, club, ConsentPurposes.Marketing)).IsSuccess.Should().BeTrue();
        await app.DispatchOutboxAsync();
        var oneClick = new Uri(app.Mail.Sent.Single().UnsubscribeUrl!).AbsolutePath;
        var mailApp = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Opened as a link, it goes to the confirmation page and changes nothing.
        var opened = await mailApp.GetAsync(oneClick);
        opened.StatusCode.Should().Be(HttpStatusCode.Redirect);
        opened.Headers.Location!.ToString().Should().StartWith("https://crm.example.ch/unsubscribe/");
        (await ConsentStateAsync(club)).Should().Be("granted");

        // RFC 8058: a POST of "List-Unsubscribe=One-Click", no cookies, no page.
        var posted = await mailApp.PostAsync(oneClick, new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }));
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConsentStateAsync(club)).Should().Be("withdrawn");
    }

    [Fact]
    public async Task AWithdrawalBeforeTheEmailGoesStopsIt()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await ConsentAsync(club, granted: true);
        var queued = await QueueAsync(app, club, ConsentPurposes.Marketing);
        queued.IsSuccess.Should().BeTrue();

        await ConsentAsync(club, granted: false);
        await app.DispatchOutboxAsync();

        app.Mail.Attempts.Should().BeEmpty("the outbox asks again as the email goes, and a no wins over one waiting");
        var message = await app.Services.Read(db => db.OutboundMessages.Single(m => m.Id == queued.Value.Id));
        message.Status.Should().Be(OutboundStatuses.Suppressed);
        message.LastError.Should().Contain("withdrew consent to marketing by email");
    }

    [Fact]
    public async Task ServiceMessagesGoWhateverTheMemberSaidAboutMarketingAndCarryNoUnsubscribe()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await ConsentAsync(club, granted: false);
        // A sales contact with the member's address was marked do not contact: an objection
        // to sales outreach, which does not stop the member's statements.
        await Json(await club.Owner.PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@example.ch", doNotContact = true })), "a do-not-contact contact");

        (await QueueAsync(app, club, ConsentPurposes.Service, "Your October statement")).IsSuccess.Should().BeTrue();
        await app.DispatchOutboxAsync();

        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.Subject.Should().Be("Your October statement");
        email.TextBody.Should().NotContain("nsubscribe");
        email.HtmlBody.Should().NotContain("nsubscribe");
        email.UnsubscribeUrl.Should().BeNull();
        SmtpEmailSender.BuildOutboxMessage(email, "Cadence", "info@pavillon46.ch").Headers.Contains("List-Unsubscribe").Should().BeFalse();

        (await QueueAsync(app, club, ConsentPurposes.Marketing)).Error.Code.Should().Be("Consent.Withdrawn",
            "the same do-not-contact address, with marketing withdrawn, gets no news");
    }

    [Fact]
    public async Task AnAddressTheMailServiceReportedIsNotEmailedEvenForService()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.EmailSuppressions.Add(new EmailSuppression
            {
                Id = Guid.NewGuid(), OrganizationId = club.Org, Email = "jean@example.ch", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var refused = await QueueAsync(app, club, ConsentPurposes.Service, "Your October statement");

        refused.Error.Code.Should().Be("Email.Suppressed", "the mail would not arrive");
    }

    [Fact]
    public async Task AnEmailToAMemberRemovedMeanwhileDoesNotGo()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var queued = await QueueAsync(app, club, ConsentPurposes.Service, "Your booking");
        queued.IsSuccess.Should().BeTrue();

        (await club.Owner.DeleteAsync($"/api/members/{club.MemberId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await app.DispatchOutboxAsync();

        app.Mail.Attempts.Should().BeEmpty();
        (await app.Services.Read(db => db.OutboundMessages.Single(m => m.Id == queued.Value.Id).Status)).Should().Be(OutboundStatuses.Cancelled);
    }

    [Fact]
    public async Task AnEmailWithAPurposeGoesToOneMemberAlone()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        using var scope = app.Services.CreateScope();
        using var acting = scope.ServiceProvider.GetRequiredService<ITenantContext>().ActFor(club.Org, club.OwnerId);

        var refused = await scope.ServiceProvider.GetRequiredService<IOutbox>().QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = club.Org, Source = OutboundSources.Member, To = ["jean@example.ch"], Cc = ["other@example.ch"],
            Subject = "Hi", Body = "Hi", MemberId = club.MemberId, Purpose = ConsentPurposes.Service,
        });

        refused.Error.Code.Should().Be("Email.PurposeNeedsMember", "one member's unsubscribe link must not reach anyone else");
    }
}
