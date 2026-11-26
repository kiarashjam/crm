using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Services.Monitoring;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The daily System health email: from 08:00 Zurich, through the outbox, to the people who
/// manage a workspace's settings, once a day, and only for a workspace with something to report.
/// </summary>
public class HealthDigestTests
{
    /// <summary>09:30 in Zurich (CET) on 18 November 2026: due, covering 17 Nov 08:00 to 18 Nov 08:00.</summary>
    private static readonly DateTime Morning = new(2026, 11, 18, 8, 30, 0, DateTimeKind.Utc);

    private static async Task<int> SendAsync(CustomWebApplicationFactory app, DateTime now)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHealthDigest>().SendDueAsync(now);
    }

    private static Task<List<OutboundMessage>> DigestsAsync(CustomWebApplicationFactory app) =>
        app.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.HealthDigest).ToList());

    [Fact]
    public void It_is_due_from_eight_in_Zurich_and_covers_the_day_before()
    {
        HealthDigestService.WindowFor(new DateTime(2026, 11, 18, 6, 59, 0, DateTimeKind.Utc)).Should().BeNull("07:59 in Zurich");
        var window = HealthDigestService.WindowFor(new DateTime(2026, 11, 18, 7, 0, 0, DateTimeKind.Utc))!.Value;
        window.Day.Should().Be(new DateOnly(2026, 11, 18));
        window.FromUtc.Should().Be(new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Utc));
        window.ToUtc.Should().Be(new DateTime(2026, 11, 18, 7, 0, 0, DateTimeKind.Utc));
        // Summer time: 08:00 in Zurich is 06:00 UTC.
        HealthDigestService.WindowFor(new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc))!.Value.ToUtc
            .Should().Be(new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task The_people_who_manage_settings_get_one_short_email_a_day_with_counts_and_a_link()
    {
        await using var app = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(app, "owner@alpine.example");
        var org = await CreateAsync(app, ownerId, "Alpine");
        var (_, managerId) = await SignInAsync(app, "manager@alpine.example");
        var (_, sellerId) = await SignInAsync(app, "seller@alpine.example");
        await AddMemberAsync(app, org, managerId, OrgMemberRole.Manager);
        await AddMemberAsync(app, org, sellerId, OrgMemberRole.Member);
        // A quiet workspace next door, and Alpine's trouble from the day before.
        var (_, quietOwner) = await SignInAsync(app, "owner@quiet.example");
        var quiet = await CreateAsync(app, quietOwner, "Quiet");
        var rule = Guid.NewGuid();
        await HealthWorld.SaveAsync(app,
            HealthWorld.Run(org, ownerId, rule, "Welcome rule", AutomationRunStatuses.Failed, Morning.AddHours(-6)),
            HealthWorld.Run(org, ownerId, rule, "Welcome rule", AutomationRunStatuses.Failed, Morning.AddDays(-3)),
            HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Sent, Morning.AddHours(-10),
                m => { m.To = "gone@example.ch"; m.Subject = "Hi"; m.DeliveryStatus = "bounced"; m.DeliveryUpdatedAtUtc = Morning.AddHours(-9); }),
            new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = org, Email = "gone@example.ch", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = Morning.AddHours(-9) },
            HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, Morning.AddHours(-5),
                m => { m.To = "lost@example.ch"; m.Subject = "Hi"; }));

        (await SendAsync(app, Morning.AddHours(-2))).Should().Be(0, "07:30 in Zurich: not yet");
        (await SendAsync(app, Morning)).Should().Be(1, "Alpine has something to report; Quiet has nothing");

        var digests = await DigestsAsync(app);
        digests.Select(d => d.To).Should().BeEquivalentTo("owner@alpine.example", "manager@alpine.example");
        digests.Should().OnlyContain(d => d.OrganizationId == org && d.Status == OutboundStatuses.Queued && d.Channel == OutboundChannels.Email);
        digests.Should().OnlyContain(d => d.UnsubscribeUrl == null, "it is for the team, with nothing to unsubscribe from");
        var digest = digests.Single(d => d.To == "owner@alpine.example");
        digest.Subject.Should().Be("System health: 4 things to look at in Alpine");
        digest.Body.Should().Contain("In the last 24 hours in Alpine:")
            .And.Contain("Emails that could not be sent: 1")
            .And.Contain("Automation runs that failed: 1")
            .And.Contain("Emails that bounced: 1")
            .And.Contain("Addresses added to the do-not-send list: 1")
            .And.NotContain("Spam reports", "only what happened is listed")
            .And.Contain("/settings?tab=health")
            .And.NotContain("gone@example.ch").And.NotContain("lost@example.ch");
        // The same in CadenceE's design, sent as it is: for the team, so no unsubscribe footer.
        digest.HtmlBody.Should().StartWith("<!DOCTYPE html>").And.Contain("4 things to look at")
            .And.Contain("Emails that could not be sent").And.Contain(">Open System health<")
            .And.Contain("/settings?tab=health\"")
            .And.NotContain("gone@example.ch").And.NotContain("lost@example.ch").And.NotContain("unsubscribe");

        var notices = await app.Services.Read(db => db.Notifications.Where(n => n.SourceKey == HealthDigestService.SourceKey(org, new DateOnly(2026, 11, 18))).ToList());
        notices.Select(n => n.UserId).Should().BeEquivalentTo(new[] { ownerId, managerId });
        notices.Should().OnlyContain(n => n.Link == "/settings?tab=health");

        // Later the same day, and after a restart (a new job, and a new scope): no second one.
        (await SendAsync(app, Morning.AddHours(3))).Should().Be(0);
        var restarted = new HealthDigestBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(), NullLogger<HealthDigestBackgroundService>.Instance);
        (await restarted.RunAsync(Morning.AddHours(4), default)).Should().Be(0);
        (await DigestsAsync(app)).Should().HaveCount(2);

        // The next morning, with nothing new in the day before: none.
        (await SendAsync(app, Morning.AddDays(1))).Should().Be(0);
        (await DigestsAsync(app)).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_handoff_that_needs_attention_and_an_email_given_up_on_after_retries_are_reported()
    {
        await using var app = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(app, "owner@alpine.example");
        var org = await CreateAsync(app, ownerId, "Alpine");
        var deal = new Deal { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Membership", Value = "1", CreatedAtUtc = Morning.AddDays(-1) };
        // Queued two days ago, given up on yesterday: its sender was told so then.
        var retried = HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, Morning.AddDays(-2), m => m.To = "x@example.ch");
        await HealthWorld.SaveAsync(app, deal,
            HealthWorld.Handoff(org, ownerId, deal.Id, "Jean", OnboardingStatuses.Failed, Morning.AddHours(-4)),
            retried,
            new Notification { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "An email could not be sent", SourceKey = $"outbox:{retried.Id}", CreatedAtUtc = Morning.AddHours(-3) });

        (await SendAsync(app, Morning)).Should().Be(1);

        var digest = (await DigestsAsync(app)).Should().ContainSingle().Subject;
        digest.Body.Should().Contain("Welcome handoffs failed or not delivered: 1").And.Contain("Emails that could not be sent: 1");
    }
}
