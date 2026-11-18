using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The monitoring job's log events, which the Azure alert rules query: one
/// "monitoring.alert {Signal} {Count} {Detail}" per signal that is on, again only after an
/// hour while it stays on, and "monitoring.resolved {Signal}" once when it clears.
/// </summary>
public class MonitoringSignalsTests
{
    private sealed record World(
        CustomWebApplicationFactory App, JobHeartbeats Heartbeats, PortalPings Pings,
        CapturingLogger<MonitoringSignalsBackgroundService> Log, MonitoringSignalsBackgroundService Job);

    private static World Start(CustomWebApplicationFactory app)
    {
        var heartbeats = new JobHeartbeats();
        var pings = new PortalPings();
        var log = new CapturingLogger<MonitoringSignalsBackgroundService>();
        var job = new MonitoringSignalsBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), heartbeats, pings, log);
        return new World(app, heartbeats, pings, log, job);
    }

    private static List<CapturingLogger<MonitoringSignalsBackgroundService>.Entry> Alerts(World w, string? signal = null) =>
        w.Log.Entries.Where(e => e.Template == MonitoringSignalsBackgroundService.AlertTemplate
            && (signal == null || (string?)e.Values["Signal"] == signal)).ToList();

    private static List<CapturingLogger<MonitoringSignalsBackgroundService>.Entry> Resolved(World w, string? signal = null) =>
        w.Log.Entries.Where(e => e.Template == MonitoringSignalsBackgroundService.ResolvedTemplate
            && (signal == null || (string?)e.Values["Signal"] == signal)).ToList();

    [Fact]
    public void The_format_is_the_one_the_alert_rules_query()
    {
        MonitoringSignalsBackgroundService.AlertTemplate.Should().Be("monitoring.alert {Signal} {Count} {Detail}");
        MonitoringSignalsBackgroundService.ResolvedTemplate.Should().Be("monitoring.resolved {Signal}");
        MonitoringSignalsBackgroundService.Signals.Should().Equal(
            "job.silent", "job.failing", "outbox.backlog", "outbox.failed", "handoff.undelivered", "payments.failed", "portal.ping.failed");
    }

    [Fact]
    public async Task A_failing_job_is_raised_once_again_after_an_hour_and_resolved_once()
    {
        await using var app = new CustomWebApplicationFactory();
        var w = Start(app);
        var t0 = DateTime.UtcNow;
        w.Heartbeats.Register("outbox", TimeSpan.FromHours(1));
        w.Heartbeats.Beat("outbox");
        w.Heartbeats.Register("automations", TimeSpan.FromHours(1));
        w.Heartbeats.Failed("automations", new InvalidOperationException("The database went away"));

        await w.Job.EvaluateAsync(t0, default);

        var alert = Alerts(w).Should().ContainSingle().Subject;
        alert.Level.Should().Be(LogLevel.Warning);
        alert.Values["Signal"].Should().Be("job.failing");
        alert.Values["Count"].Should().BeOfType<int>().And.Be(1);
        alert.Values["Detail"].Should().Be("1 failing: automations (1 in a row)");
        alert.Message.Should().Be("monitoring.alert job.failing 1 1 failing: automations (1 in a row)");

        await w.Job.EvaluateAsync(t0.AddMinutes(5), default);
        await w.Job.EvaluateAsync(t0.AddMinutes(55), default);
        Alerts(w).Should().HaveCount(1, "a signal that stays on is not raised again within the hour");

        await w.Job.EvaluateAsync(t0.AddMinutes(61), default);
        Alerts(w, "job.failing").Should().HaveCount(2, "after an hour it is raised again");

        w.Heartbeats.Beat("automations");
        await w.Job.EvaluateAsync(t0.AddMinutes(66), default);
        await w.Job.EvaluateAsync(t0.AddMinutes(71), default);
        var resolved = Resolved(w).Should().ContainSingle().Subject;
        resolved.Level.Should().Be(LogLevel.Information);
        resolved.Values["Signal"].Should().Be("job.failing");
        Alerts(w).Should().HaveCount(2);

        // Three intervals with no sign of life: both jobs have stopped, which is an error.
        await w.Job.EvaluateAsync(t0.AddHours(4), default);
        var silent = Alerts(w, "job.silent").Should().ContainSingle().Subject;
        silent.Level.Should().Be(LogLevel.Error);
        silent.Values["Count"].Should().Be(2);
        silent.Values["Detail"].Should().Be("2 silent: automations, outbox");
    }

    [Fact]
    public async Task Failures_across_workspaces_are_raised_with_counts_and_ids_but_nobodys_details()
    {
        await using var app = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(app, "owner@alpine.example");
        var org = await CreateAsync(app, ownerId, "Alpine");
        var (_, otherId) = await SignInAsync(app, "owner@bernina.example");
        var other = await CreateAsync(app, otherId, "Bernina");
        var w = Start(app);
        var now = DateTime.UtcNow;
        // Already failed before this instance started: learnt on the first pass, not raised again.
        var before = HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, now.AddDays(-1), m => m.To = "before@example.ch");
        await HealthWorld.SaveAsync(app, before);

        await w.Job.EvaluateAsync(now, default);
        w.Log.Entries.Should().BeEmpty("nothing is on, and what failed before the start is not news to this instance");

        var failedEmail = HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, now.AddMinutes(-20),
            m => { m.To = "jean.dupont@private.example"; m.Subject = "Jean's renewal"; m.LastError = "550 no such user jean.dupont"; });
        var failedHook = HealthWorld.Message(other, OutboundChannels.Webhook, OutboundSources.Webhook, OutboundStatuses.Failed, now.AddMinutes(-20), m => m.EventName = "lead.created");
        var lateEmail = HealthWorld.Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Queued, now.AddMinutes(-45),
            m => { m.To = "anna@private.example"; m.Attempts = 1; m.NextAttemptAtUtc = now.AddMinutes(10); });
        var lateHookOnly = HealthWorld.Message(org, OutboundChannels.Webhook, OutboundSources.Webhook, OutboundStatuses.Queued, now.AddHours(-2), m => m.Attempts = 2);
        var deal = new Deal { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Membership", Value = "1", CreatedAtUtc = now };
        var handoff = HealthWorld.Handoff(org, ownerId, deal.Id, "Marie Curie", OnboardingStatuses.Queued, now.AddHours(-2));
        var handoffMessage = HealthWorld.Message(org, OutboundChannels.Webhook, OutboundSources.Onboarding, OutboundStatuses.Queued, now.AddHours(-2),
            m => { m.SourceId = handoff.Id; m.Attempts = 3; m.NextAttemptAtUtc = now.AddMinutes(5); });
        handoff.OutboundMessageId = handoffMessage.Id;
        var stripe = new ClubProviderEvent { Id = Guid.NewGuid(), OrganizationId = other, EventId = "evt_123", Type = "payment_intent.succeeded", Status = "failed", Attempts = 1, ReceivedAtUtc = now.AddMinutes(-3) };
        await HealthWorld.SaveAsync(app, failedEmail, failedHook, lateEmail, lateHookOnly, deal, handoff, handoffMessage, stripe);
        w.Pings.Record(other, new PortalPingResult(false, now, 503, "The receiver answered 503"));
        w.Pings.Record(org, new PortalPingResult(true, now, 200, null));

        await w.Job.EvaluateAsync(now.AddSeconds(5), default);

        var alerts = Alerts(w).ToDictionary(a => (string)a.Values["Signal"]!);
        alerts.Keys.Should().BeEquivalentTo("outbox.failed", "outbox.backlog", "handoff.undelivered", "payments.failed", "portal.ping.failed");
        alerts.Values.Should().OnlyContain(a => a.Template == "monitoring.alert {Signal} {Count} {Detail}" && a.Values["Count"] is int);

        alerts["outbox.failed"].Level.Should().Be(LogLevel.Warning);
        alerts["outbox.failed"].Values["Count"].Should().Be(2);
        ((string)alerts["outbox.failed"].Values["Detail"]!).Should().Contain("1 email").And.Contain("1 webhook")
            .And.Contain(failedEmail.Id.ToString()).And.NotContain(before.Id.ToString());
        alerts["outbox.backlog"].Values["Count"].Should().Be(1, "emails only: a webhook waiting is not an email backlog");
        alerts["handoff.undelivered"].Values["Count"].Should().Be(1);
        alerts["handoff.undelivered"].Values["Detail"].Should().Be("1 handoffs not taken by the portal after 1 h, in 1 workspaces");
        alerts["payments.failed"].Level.Should().Be(LogLevel.Error);
        alerts["payments.failed"].Values["Detail"].Should().Be("1 newly failed Stripe events: evt_123");
        alerts["portal.ping.failed"].Values["Count"].Should().Be(1);
        ((string)alerts["portal.ping.failed"].Values["Detail"]!).Should().Contain(other.ToString());

        var everything = string.Join("\n", w.Log.Entries.Select(e => e.Message));
        foreach (var personal in new[] { "jean", "Jean", "anna@", "Marie", "private.example", "550" })
            everything.Should().NotContain(personal, "an alert says counts, job names and ids, never anyone's details");

        // Five minutes on: nothing new has failed, so those clear; the rest stay on, and are not raised again yet.
        w.Log.Clear();
        await w.Job.EvaluateAsync(now.AddMinutes(5), default);
        Alerts(w).Should().BeEmpty();
        Resolved(w).Select(r => (string)r.Values["Signal"]!).Should().BeEquivalentTo("outbox.failed", "payments.failed");
        Resolved(w).Should().OnlyContain(r => r.Level == LogLevel.Information);
    }
}
