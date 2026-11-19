using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// System health: GET /api/system-health answers those who manage the workspace's settings with
/// each area filled from its own rows, and never with another workspace's.
/// </summary>
public class SystemHealthTests
{
    [Fact]
    public async Task Only_those_who_manage_the_workspaces_settings_see_it()
    {
        await using var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@alpine.example");
        var org = await CreateAsync(app, ownerId, "Alpine");
        var (manager, managerId) = await SignInAsync(app, "manager@alpine.example");
        var (seller, sellerId) = await SignInAsync(app, "seller@alpine.example");
        await AddMemberAsync(app, org, managerId, OrgMemberRole.Manager);
        await AddMemberAsync(app, org, sellerId, OrgMemberRole.Member);

        (await owner.In(org).GetAsync("/api/system-health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await manager.In(org).GetAsync("/api/system-health")).StatusCode.Should().Be(HttpStatusCode.OK);
        var refused = await seller.In(org).GetAsync("/api/system-health");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("system health");

        var (stranger, _) = await SignInAsync(app, "stranger@example.ch");
        (await stranger.GetAsync("/api/system-health")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "it belongs to an organization");
        (await stranger.In(org).GetAsync("/api/system-health")).StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound }, "naming a workspace one is not in reads nothing of it");
        (await app.CreateClient().GetAsync("/api/system-health")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_workspace_with_nothing_wrong_is_all_good_and_lists_the_jobs()
    {
        await using var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@alpine.example");
        var org = await CreateAsync(app, ownerId, "Alpine");
        var heartbeats = app.Services.GetRequiredService<IJobHeartbeats>();
        heartbeats.Register("outbox", TimeSpan.FromSeconds(30));
        heartbeats.Beat("outbox");

        var health = await HealthWorld.Ok(await owner.In(org).GetAsync("/api/system-health"));

        health.GetProperty("overall").GetProperty("status").GetString().Should().Be("ok");
        health.GetProperty("overall").GetProperty("issues").GetInt32().Should().Be(0);
        foreach (var area in new[] { "handoffs", "outbox", "webhooks", "payments", "automations", "email" })
        {
            health.GetProperty(area).GetProperty("count").GetInt32().Should().Be(0, area);
            health.GetProperty(area).GetProperty("items").GetArrayLength().Should().Be(0, area);
        }
        var job = health.GetProperty("jobs").EnumerateArray().Should().ContainSingle().Subject;
        job.GetProperty("name").GetString().Should().Be("outbox");
        job.GetProperty("intervalSeconds").GetInt32().Should().Be(30);
        job.GetProperty("failing").GetBoolean().Should().BeFalse();
        job.GetProperty("silent").GetBoolean().Should().BeFalse();
        health.GetProperty("portal").GetProperty("configured").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Each_area_is_filled_from_its_own_rows_and_another_workspaces_never_show()
    {
        await using var app = new CustomWebApplicationFactory();
        var (alpine, alpineOwner) = await SignInAsync(app, "owner@alpine.example");
        var alpineOrg = await CreateAsync(app, alpineOwner, "Alpine");
        var (bernina, berninaOwner) = await SignInAsync(app, "owner@bernina.example");
        var berninaOrg = await CreateAsync(app, berninaOwner, "Bernina");
        var now = DateTime.UtcNow;
        var a = await HealthWorld.SeedTroubleAsync(app, alpineOrg, alpineOwner, "alpine", now);
        var b = await HealthWorld.SeedTroubleAsync(app, berninaOrg, berninaOwner, "bernina", now);

        var pings = app.Services.GetRequiredService<IPortalPings>();
        pings.Record(alpineOrg, new PortalPingResult(false, now, 503, "The receiver answered 503"));
        pings.Record(berninaOrg, new PortalPingResult(true, now, 200, null));
        var heartbeats = app.Services.GetRequiredService<IJobHeartbeats>();
        heartbeats.Register("automations", TimeSpan.FromMinutes(1));
        heartbeats.Failed("automations", new InvalidOperationException("The database went away\n   at Some.Stack.Frame()"));

        var response = await alpine.In(alpineOrg).GetAsync("/api/system-health");
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var raw = await response.Content.ReadAsStringAsync();
        var health = await HealthWorld.Ok(response, raw);

        foreach (var id in b.Ids.Append(berninaOrg).Append(berninaOwner))
            raw.Should().NotContain(id.ToString(), "nothing of another workspace's shows");
        raw.Should().NotContain("bernina");

        var overall = health.GetProperty("overall");
        overall.GetProperty("status").GetString().Should().Be("failing", "a job is failing and the portal did not take its last ping");
        var counts = overall.GetProperty("counts");
        counts.GetProperty("handoffs").GetInt32().Should().Be(2);
        counts.GetProperty("outbox").GetInt32().Should().Be(4);
        counts.GetProperty("webhooks").GetInt32().Should().Be(1);
        counts.GetProperty("payments").GetInt32().Should().Be(1);
        counts.GetProperty("automations").GetInt32().Should().Be(1);
        counts.GetProperty("email").GetInt32().Should().Be(2);
        counts.GetProperty("jobs").GetInt32().Should().Be(1);
        counts.GetProperty("portal").GetInt32().Should().Be(1);
        overall.GetProperty("issues").GetInt32().Should().Be(8);

        // Handoffs: the one the portal reported failed, and the one still not taken after two hours.
        var handoffs = health.GetProperty("handoffs").GetProperty("items").EnumerateArray().ToList();
        handoffs.Should().HaveCount(2);
        var failed = handoffs.Single(h => h.GetProperty("state").GetString() == "failed");
        failed.GetProperty("id").GetGuid().Should().Be(a.FailedHandoff);
        failed.GetProperty("dealName").GetString().Should().Be("alpine membership");
        failed.GetProperty("person").GetString().Should().Be("alpine Failed Person");
        failed.GetProperty("lastError").GetString().Should().Be("The portal refused the address");
        failed.GetProperty("link").GetString().Should().Be($"/deals/{a.Deal}");
        failed.GetProperty("retry").GetProperty("kind").GetString().Should().Be("handoff");
        failed.GetProperty("retry").GetProperty("id").GetGuid().Should().Be(a.FailedHandoff);
        var late = handoffs.Single(h => h.GetProperty("state").GetString() == "late");
        late.GetProperty("id").GetGuid().Should().Be(a.LateHandoff);
        late.GetProperty("attempts").GetInt32().Should().Be(3);
        late.GetProperty("lastError").GetString().Should().Be("The receiver answered 503");
        late.GetProperty("retry").ValueKind.Should().Be(JsonValueKind.Null, "a handoff still being handed over cannot be sent again yet");

        // Outbox: what is still queued after half an hour, and what failed this week; never what went, or failed long ago.
        var outbox = health.GetProperty("outbox");
        outbox.GetProperty("queued").GetInt32().Should().Be(2);
        outbox.GetProperty("failed").GetInt32().Should().Be(2);
        var messages = outbox.GetProperty("items").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetGuid());
        messages.Keys.Should().BeEquivalentTo(new[] { a.LateEmail, a.LateHandoffMessage, a.FailedEmail, a.FailedWebhook });
        var lost = messages[a.FailedEmail];
        lost.GetProperty("kind").GetString().Should().Be("email");
        lost.GetProperty("state").GetString().Should().Be("failed");
        lost.GetProperty("to").GetString().Should().Be("lost@alpine.example");
        lost.GetProperty("subject").GetString().Should().Be("Your visit");
        lost.GetProperty("lastError").GetString().Should().Be("550 mailbox unavailable");
        lost.GetProperty("link").GetString().Should().Be($"/contacts/{a.Contact}");
        messages[a.LateEmail].GetProperty("state").GetString().Should().Be("queued");
        messages[a.LateHandoffMessage].GetProperty("kind").GetString().Should().Be("handoff");
        messages[a.LateHandoffMessage].GetProperty("link").GetString().Should().Be($"/deals/{a.Deal}");
        messages[a.FailedWebhook].GetProperty("kind").GetString().Should().Be("webhook");
        messages[a.FailedWebhook].GetProperty("to").GetString().Should().Be("lead.created");
        messages[a.FailedWebhook].GetProperty("link").GetString().Should().Be("/settings?tab=integrations");

        var webhook = health.GetProperty("webhooks").GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        webhook.GetProperty("id").GetGuid().Should().Be(a.FailedWebhook);
        webhook.GetProperty("eventName").GetString().Should().Be("lead.created");
        webhook.GetProperty("lastStatusCode").GetInt32().Should().Be(500);

        var payment = health.GetProperty("payments").GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        payment.GetProperty("eventId").GetString().Should().Be("evt_alpine_failed");
        payment.GetProperty("note").GetString().Should().Be("No such member");

        var run = health.GetProperty("automations").GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        run.GetProperty("runId").GetGuid().Should().Be(a.FailedRun);
        run.GetProperty("ruleName").GetString().Should().Be("alpine welcome rule");
        run.GetProperty("link").GetString().Should().Be($"/automations?rule={a.Rule}");
        run.GetProperty("retry").GetProperty("kind").GetString().Should().Be("automation");
        run.GetProperty("retry").GetProperty("id").GetGuid().Should().Be(a.FailedRun);

        var email = health.GetProperty("email");
        email.GetProperty("bounces").GetInt32().Should().Be(1);
        email.GetProperty("spamReports").GetInt32().Should().Be(1);
        email.GetProperty("newSuppressions").GetInt32().Should().Be(1);
        email.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("kind").GetString()).Should().BeEquivalentTo("bounce", "spam_report");

        var job = health.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("name").GetString() == "automations");
        job.GetProperty("failing").GetBoolean().Should().BeTrue();
        job.GetProperty("lastError").GetString().Should().Be("InvalidOperationException: The database went away", "one line, never a stack");

        var portal = health.GetProperty("portal");
        portal.GetProperty("configured").GetBoolean().Should().BeTrue();
        portal.GetProperty("ok").GetBoolean().Should().BeFalse();
        portal.GetProperty("statusCode").GetInt32().Should().Be(503);

        // And Bernina's owner sees Bernina's, as their own.
        var theirsRaw = await (await bernina.In(berninaOrg).GetAsync("/api/system-health")).Content.ReadAsStringAsync();
        foreach (var mine in a.Ids.Append(alpineOrg)) theirsRaw.Should().NotContain(mine.ToString());
        using var theirs = JsonDocument.Parse(theirsRaw);
        theirs.RootElement.GetProperty("handoffs").GetProperty("count").GetInt32().Should().Be(2);
        theirs.RootElement.GetProperty("portal").GetProperty("ok").GetBoolean().Should().BeTrue();
    }
}

/// <summary>Rows of every kind of trouble, and of the things that are fine beside them, in one workspace.</summary>
internal static class HealthWorld
{
    public static async Task<JsonElement> Ok(HttpResponseMessage response, string? body = null)
    {
        body ??= await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task SaveAsync(WebApplicationFactory<Program> app, params object[] rows)
    {
        using var scope = app.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    public sealed record Trouble(
        Guid Deal, Guid Contact, Guid FailedHandoff, Guid LateHandoff, Guid LateHandoffMessage, Guid FailedEmail, Guid LateEmail,
        Guid FailedWebhook, Guid FailedStripeEvent, Guid Rule, Guid FailedRun, Guid Bounce, Guid Spam, IReadOnlyList<Guid> Ids);

    public static async Task<Trouble> SeedTroubleAsync(WebApplicationFactory<Program> app, Guid org, Guid owner, string label, DateTime now)
    {
        var deal = new Deal { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{label} membership", Value = "100", CreatedAtUtc = now.AddDays(-2) };
        var contact = Guid.NewGuid();

        var failedHandoff = Handoff(org, owner, deal.Id, $"{label} Failed Person", OnboardingStatuses.Failed, now.AddHours(-3),
            """[{"key":"queued","atUtc":"2026-01-01T00:00:00Z"},{"key":"failed","atUtc":"2026-01-01T00:05:00Z","detail":"The portal refused the address"}]""");
        var lateHandoff = Handoff(org, owner, deal.Id, $"{label} Late Person", OnboardingStatuses.Queued, now.AddHours(-2));
        var lateHandoffMessage = Message(org, OutboundChannels.Webhook, OutboundSources.Onboarding, OutboundStatuses.Queued, now.AddHours(-2),
            m => { m.SourceId = lateHandoff.Id; m.EventName = "deal.onboarding"; m.Attempts = 3; m.LastError = "The receiver answered 503"; m.NextAttemptAtUtc = now.AddMinutes(10); m.DealId = deal.Id; });
        lateHandoff.OutboundMessageId = lateHandoffMessage.Id;
        // Taken by the portal, not yet reported on: fine.
        var takenHandoff = Handoff(org, owner, deal.Id, $"{label} Taken Person", OnboardingStatuses.Queued, now.AddHours(-5));
        var takenMessage = Message(org, OutboundChannels.Webhook, OutboundSources.Onboarding, OutboundStatuses.Sent, now.AddHours(-5),
            m => { m.SourceId = takenHandoff.Id; m.EventName = "deal.onboarding"; m.SentAtUtc = now.AddHours(-5); m.DealId = deal.Id; });
        takenHandoff.OutboundMessageId = takenMessage.Id;

        var failedEmail = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, now.AddDays(-1),
            m => { m.To = $"lost@{label}.example"; m.Subject = "Your visit"; m.LastError = "550 mailbox unavailable"; m.Attempts = 1; m.ContactId = contact; });
        var lateEmail = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Queued, now.AddHours(-2),
            m => { m.To = $"slow@{label}.example"; m.Subject = "Later"; m.Attempts = 2; m.LastError = "421 try again"; m.NextAttemptAtUtc = now.AddMinutes(5); });
        var failedWebhook = Message(org, OutboundChannels.Webhook, OutboundSources.Webhook, OutboundStatuses.Failed, now.AddDays(-2),
            m => { m.EventName = "lead.created"; m.LastStatusCode = 500; m.LastError = "The receiver answered 500"; m.Attempts = 9; });
        // Fine, or too old to be news.
        var sent = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Sent, now.AddHours(-3), m => { m.To = $"fine@{label}.example"; m.Subject = "Hi"; });
        var justQueued = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Queued, now.AddMinutes(-5), m => { m.To = $"soon@{label}.example"; m.Subject = "Hi"; });
        var heldForLater = Message(org, OutboundChannels.Email, OutboundSources.Newsletter, OutboundStatuses.Queued, now.AddHours(-1),
            m => { m.To = $"later@{label}.example"; m.Subject = "News"; m.NextAttemptAtUtc = now.AddMinutes(20); });
        var oldFailure = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Failed, now.AddDays(-10), m => { m.To = $"old@{label}.example"; m.Subject = "Old"; });

        var bounce = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Sent, now.AddHours(-4),
            m => { m.To = $"bounce@{label}.example"; m.Subject = "Hello"; m.DeliveryStatus = "bounced"; m.DeliveryDetail = "550 5.1.1 no such user"; m.DeliveryUpdatedAtUtc = now.AddHours(-3); });
        var spam = Message(org, OutboundChannels.Email, OutboundSources.Composer, OutboundStatuses.Sent, now.AddHours(-4),
            m => { m.To = $"spam@{label}.example"; m.Subject = "Offer"; m.DeliveryStatus = "spam_report"; m.DeliveryUpdatedAtUtc = now.AddHours(-2); });
        var suppression = new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = org, Email = $"bounce@{label}.example", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = now.AddHours(-3) };

        var failedEvent = new ClubProviderEvent { Id = Guid.NewGuid(), OrganizationId = org, EventId = $"evt_{label}_failed", Type = "payment_intent.succeeded", Status = "failed", Note = "No such member", Attempts = 2, ReceivedAtUtc = now.AddHours(-1) };
        var processedEvent = new ClubProviderEvent { Id = Guid.NewGuid(), OrganizationId = org, EventId = $"evt_{label}_ok", Type = "payment_intent.succeeded", Status = "processed", Attempts = 1, ReceivedAtUtc = now.AddHours(-1) };

        var rule = Guid.NewGuid();
        var ruleRow = new AutomationRule { Id = rule, OrganizationId = org, OwnerUserId = owner, Name = $"{label} welcome rule", Trigger = "lead.created", CreatedAtUtc = now.AddDays(-5), Version = Guid.NewGuid() };
        var failedRun = Run(org, owner, rule, $"{label} welcome rule", AutomationRunStatuses.Failed, now.AddHours(-1));
        var finishedRun = Run(org, owner, rule, $"{label} welcome rule", AutomationRunStatuses.Completed, now.AddHours(-1));

        var settings = new OnboardingSettings
        {
            Id = Guid.NewGuid(), OrganizationId = org, WebhookUrl = $"https://{label}-portal.example.com/hook", WebhookSecret = "s3cret", UpdatedAtUtc = now,
        };

        await SaveAsync(app, deal, failedHandoff, lateHandoff, lateHandoffMessage, takenHandoff, takenMessage, failedEmail, lateEmail, failedWebhook,
            sent, justQueued, heldForLater, oldFailure, bounce, spam, suppression, failedEvent, processedEvent, ruleRow, failedRun, finishedRun, settings);

        return new Trouble(deal.Id, contact, failedHandoff.Id, lateHandoff.Id, lateHandoffMessage.Id, failedEmail.Id, lateEmail.Id, failedWebhook.Id,
            failedEvent.Id, rule, failedRun.Id, bounce.Id, spam.Id,
            [deal.Id, contact, failedHandoff.Id, lateHandoff.Id, lateHandoffMessage.Id, failedEmail.Id, lateEmail.Id, failedWebhook.Id, failedEvent.Id, rule, failedRun.Id, bounce.Id, spam.Id]);
    }

    public static DealOnboarding Handoff(Guid org, Guid owner, Guid deal, string name, string status, DateTime at, string steps = "[]") => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, DealId = deal, Name = name, Email = $"{Guid.NewGuid():N}@example.ch", Status = status,
        StepsJson = steps, CreatedAtUtc = at, UpdatedAtUtc = at, CreatedByUserId = owner, Version = Guid.NewGuid(),
    };

    public static OutboundMessage Message(Guid org, string channel, string source, string status, DateTime created, Action<OutboundMessage>? set = null)
    {
        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(), OrganizationId = org, Channel = channel, Source = source, Status = status, CreatedAtUtc = created,
            NextAttemptAtUtc = status == OutboundStatuses.Queued ? created : null,
        };
        set?.Invoke(message);
        return message;
    }

    public static AutomationRun Run(Guid org, Guid owner, Guid rule, string name, string status, DateTime finished) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, RuleId = rule, RuleName = name, Trigger = "lead.created", SourceKey = Guid.NewGuid().ToString("N"),
        Status = status, ActorUserId = owner, RecordType = "lead", RecordName = "Jean Dupont", LastError = status == AutomationRunStatuses.Failed ? "The email could not be queued" : null,
        StepAttempts = 3, StartedAtUtc = finished.AddMinutes(-30), UpdatedAtUtc = finished, FinishedAtUtc = finished,
    };
}

/// <summary>What a class logged, with each event's template and values: what the alert rules will read.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Template, IReadOnlyDictionary<string, object?> Values, string Message);

    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries
    {
        get { lock (_entries) return _entries.ToList(); }
    }

    public void Clear()
    {
        lock (_entries) _entries.Clear();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = (state as IEnumerable<KeyValuePair<string, object?>>)?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, object?>();
        var template = values.TryGetValue("{OriginalFormat}", out var format) ? format as string ?? "" : "";
        lock (_entries) _entries.Add(new Entry(logLevel, template, values, formatter(state, exception)));
    }
}
