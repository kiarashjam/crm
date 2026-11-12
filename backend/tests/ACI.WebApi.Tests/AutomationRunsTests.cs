using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Automations v2: conditions on the record, waits and branches carried on by the runner,
/// send windows, retries, the run log, and a step that never runs twice.
/// </summary>
public class AutomationRunsTests
{
    /// <summary>A clock a test moves on, so a wait of two days is over in a moment.</summary>
    public sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
        public void Set(DateTime utc) => _now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    /// <summary>Whether the outbox refuses automation emails, as if its database were down.</summary>
    public sealed class Flakiness
    {
        public volatile bool FailAutomationEmails;
    }

    /// <summary>The outbox, failing automation emails while told to.</summary>
    private sealed class FlakyOutbox : IOutbox
    {
        private readonly OutboxService _inner;
        private readonly Flakiness _flakiness;

        public FlakyOutbox(OutboxService inner, Flakiness flakiness)
        {
            _inner = inner;
            _flakiness = flakiness;
        }

        public Task<Result<OutboundMessage>> QueueEmailAsync(OutboundEmailRequest request, CancellationToken ct = default) =>
            _flakiness.FailAutomationEmails && request.Source == OutboundSources.Automation
                ? throw new InvalidOperationException("The outbox could not be reached.")
                : _inner.QueueEmailAsync(request, ct);

        public Task<OutboxAttempt> SendNowAsync(OutboundMessage message, CancellationToken ct = default) => _inner.SendNowAsync(message, ct);
        public Task ProcessAsync(DueOutboundMessage due, CancellationToken ct = default) => _inner.ProcessAsync(due, ct);
        public Task<Result> CancelAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct = default) => _inner.CancelAsync(id, organizationId, userId, ct);
        public Task<Result<IReadOnlyList<WebhookDeliveryDto>>> WebhookDeliveriesAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
            _inner.WebhookDeliveriesAsync(organizationId, userId, ct);
        public Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default) => _inner.HasQueuedAsync(sourceId, ct);
    }

    public sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();
        public readonly TestClock Clock = new();
        public readonly Flakiness Flakiness = new();

        /// <summary>Holds runners that have seen a run due until both have, so they race for its claim.</summary>
        public readonly RaceFactory.ReadGate Gate = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Mail.Mock.Object);
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton(Flakiness);
                services.AddSingleton<ILedgerReadObserver>(Gate);
                services.AddScoped<IOutbox>(sp => new FlakyOutbox(sp.GetRequiredService<OutboxService>(), sp.GetRequiredService<Flakiness>()));
            });
        }

        public Task<int> RunDueAsync() => Services.GetRequiredService<AutomationRunner>().RunDueAsync();
        public Task<int> ScanRenewalsAsync() => Services.GetRequiredService<AutomationRunner>().ScanRenewalsAsync();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid OwnerId, Guid Org)> WorkspaceAsync(App app, string email = "owner@example.ch")
    {
        var (owner, ownerId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, ownerId, org);
    }

    private static async Task<Guid> RuleAsync(HttpClient client, object rule) =>
        (await Json(await client.PostAsync("/api/automations", Body(rule)), "add the rule")).GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client, string name = "Jean Dupont", string email = "jean@example.ch", string? source = null) =>
        (await Json(await client.PostAsync("/api/leads", Body(new { name, email, status = "New", source })), "add a lead")).GetProperty("id").GetGuid();

    private static Task<List<TaskItem>> TasksAsync(App app, Guid leadId) =>
        app.Services.Read(db => db.TaskItems.Where(t => t.LeadId == leadId).ToList());

    private static Task<AutomationRun> RunOfAsync(App app, Guid ruleId) =>
        app.Services.Read(db => db.AutomationRuns.Single(r => r.RuleId == ruleId));

    private static async Task<JsonElement> LogAsync(HttpClient client, Guid runId) =>
        await Json(await client.GetAsync($"/api/automations/runs/{runId}"), "read the run's log");

    private static object Task(string title, string due = "1") => new { type = "create_task", config = new { title, dueInDays = due } };
    private static object Email(string subject, string body = "Hi {{firstName}}") => new { type = "send_email", config = new { subject, body } };
    private static object Wait(int amount, string unit) => new { type = "wait", config = new { amount = amount.ToString(), unit } };

    // ── conditions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ARuleWithConditionsRunsOnlyForTheRecordsThatMeetThem_OneWithoutRunsForAll()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var website = await RuleAsync(owner, new
        {
            name = "Website leads", trigger = "lead.created",
            conditions = new { match = "all", conditions = new[] { new { field = "source", op = "is", value = "website" } } },
            actions = new[] { Task("Call the website lead") },
        });
        await RuleAsync(owner, new { name = "Everyone", trigger = "lead.created", actions = new[] { Task("Say hello") } });

        var fromWebsite = await LeadAsync(owner, "Ana Silva", "ana@example.ch", source: "Website");
        var referred = await LeadAsync(owner, "Paul Weber", "paul@example.ch", source: "Referral");

        (await TasksAsync(app, fromWebsite)).Select(t => t.Title).Should().BeEquivalentTo(["Call the website lead", "Say hello"]);
        (await TasksAsync(app, referred)).Select(t => t.Title).Should().Equal(["Say hello"], "the referral does not meet the condition");
        (await app.Services.Read(db => db.AutomationRuns.Count(r => r.RuleId == website))).Should().Be(1, "a record that does not meet the conditions starts no run");
    }

    [Fact]
    public async Task AStatusRuleCanAskWhichStatusTheLeadCameFrom()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        await RuleAsync(owner, new
        {
            name = "Back from lost", trigger = "lead.status_changed",
            conditions = new { match = "all", conditions = new[] { new { field = "fromStatus", op = "is", value = "Lost / Not Interested" } } },
            actions = new[] { Task("They came back") },
        });
        var leadId = await LeadAsync(owner);
        await Json(await owner.PutAsync($"/api/leads/{leadId}", Body(new { status = "Contacted" })), "New to Contacted");
        (await TasksAsync(app, leadId)).Should().BeEmpty("it came from New");
        await Json(await owner.PutAsync($"/api/leads/{leadId}", Body(new { status = "Lost / Not Interested" })), "to Lost");
        await Json(await owner.PutAsync($"/api/leads/{leadId}", Body(new { status = "Attempted Contact" })), "back from Lost");
        (await TasksAsync(app, leadId)).Select(t => t.Title).Should().Equal(["They came back"]);
    }

    [Fact]
    public async Task SavingARuleChecksItsConditionsAndStepsAgainstItsTrigger()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        async Task<string> Refused(object rule)
        {
            var res = await owner.PostAsync("/api/automations", Body(rule));
            var body = await res.Content.ReadAsStringAsync();
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
            return body;
        }
        (await Refused(new
        {
            name = "Bad", trigger = "lead.created",
            conditions = new { match = "all", conditions = new[] { new { field = "tier", op = "is", value = "Gold" } } },
            actions = new[] { Task("x") },
        })).Should().Contain("not something");
        (await Refused(new { name = "Bad", trigger = "member.created", actions = new object[] { new { type = "assign", config = new { userId = Guid.NewGuid().ToString() } } } }))
            .Should().Contain("cannot run the step");
        (await Refused(new { name = "Bad", trigger = "lead.created", actions = new object[] { new { type = "wait", config = new { mode = "until", date = "start" } } } }))
            .Should().Contain("no date to wait for");
        (await Refused(new { name = "Bad", trigger = "lead.created", actions = new object[] { Wait(0, "days") } }))
            .Should().Contain("how long to wait");
        (await Refused(new
        {
            name = "Bad", trigger = "lead.created", actions = new object[] { new { type = "branch", then = new[] { Task("x") } } },
        })).Should().Contain("needs a condition");
        (await Refused(new
        {
            name = "Bad", trigger = "lead.created", sendWindow = new { days = new[] { 1 }, start = "18:00", end = "08:00" }, actions = new[] { Task("x") },
        })).Should().Contain("close after it opens");
        (await Refused(new { name = "Bad", trigger = "lead.created", triggerConfig = new { stageId = "x" }, actions = new[] { Task("x") } }))
            .Should().Contain("give it conditions");
    }

    // ── waits, the runner and the log ────────────────────────────────────────

    [Fact]
    public async Task AWaitHoldsTheRestOfTheRun_ThenTheRunnerCarriesItOn_AndTheLogSaysWhatEachStepDid()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new
        {
            name = "Nurture", trigger = "lead.created",
            actions = new object[] { Task("Call them"), Wait(2, "days"), Email("Still interested, {{firstName}}?") },
        });

        var leadId = await LeadAsync(owner);

        // The task runs in the save, as before; the email waits for the wait.
        (await TasksAsync(app, leadId)).Should().ContainSingle(t => t.Title == "Call them");
        var run = await RunOfAsync(app, ruleId);
        run.Status.Should().Be(AutomationRunStatuses.Waiting);
        run.WaitingFor.Should().StartWith("2 days");
        (await app.Services.Read(db => db.OutboundMessages.Count())).Should().Be(0);

        // Not yet due: the runner leaves it.
        app.Clock.Advance(TimeSpan.FromDays(1));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.OutboundMessages.Count())).Should().Be(0);

        app.Clock.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
        (await app.RunDueAsync()).Should().Be(1);
        var email = await app.Services.Read(db => db.OutboundMessages.Single());
        email.Subject.Should().Be("Still interested, Jean?");
        email.Source.Should().Be(OutboundSources.Automation);
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Completed);

        await app.DispatchOutboxAsync();
        var log = await LogAsync(owner, run.Id);
        var steps = log.GetProperty("steps").EnumerateArray().ToList();
        steps.Select(s => s.GetProperty("stepType").GetString()).Should().Equal("create_task", "wait", "wait", "send_email");
        steps.Select(s => s.GetProperty("outcome").GetString()).Should().Equal("done", "waiting", "done", "done");
        steps[0].GetProperty("taskId").GetGuid().Should().Be((await TasksAsync(app, leadId)).Single().Id);
        steps[3].GetProperty("outboundMessageId").GetGuid().Should().Be(email.Id);
        steps[3].GetProperty("emailStatus").GetString().Should().Be("sent", "the log reads where the email stands in the outbox");
        app.Mail.SentTo.Should().Equal("jean@example.ch");
    }

    [Fact]
    public async Task ABranchDecidesOnTheRecordAsItIsWhenTheBranchIsReached()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        await RuleAsync(owner, new
        {
            name = "Follow up in an hour", trigger = "lead.created",
            actions = new object[]
            {
                Wait(1, "hours"),
                new
                {
                    type = "branch",
                    condition = new { match = "all", conditions = new[] { new { field = "status", op = "is", value = "Contacted" } } },
                    then = new[] { Task("Thank them") },
                    @else = new[] { Task("Chase them") },
                },
                Task("Review the lead"),
            },
        });
        var answered = await LeadAsync(owner, "Ana Silva", "ana@example.ch");
        var silent = await LeadAsync(owner, "Paul Weber", "paul@example.ch");
        // Within the hour, one of them is reached.
        await Json(await owner.PutAsync($"/api/leads/{answered}", Body(new { status = "Contacted" })), "reach Ana");

        app.Clock.Advance(TimeSpan.FromMinutes(61));
        await app.RunDueAsync();

        (await TasksAsync(app, answered)).Select(t => t.Title).Should().BeEquivalentTo(["Thank them", "Review the lead"]);
        (await TasksAsync(app, silent)).Select(t => t.Title).Should().BeEquivalentTo(["Chase them", "Review the lead"],
            "after the branch's steps, the run goes on with the step after the branch");
    }

    [Fact]
    public async Task AWaitForADateOnTheRecordIsReadAgainWhenItComesDue()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        await RuleAsync(owner, new
        {
            name = "Before the close date", trigger = "deal.stage_changed",
            actions = new object[]
            {
                new { type = "wait", config = new { mode = "until", date = "expectedClose", offsetAmount = "-2", offsetUnit = "days" } },
                Task("Close date in two days"),
            },
        });
        var close = app.Clock.GetUtcNow().UtcDateTime.Date.AddDays(10);
        var deal = await Json(await owner.PostAsync("/api/deals", Body(new { name = "Gold membership", value = "12000", stage = "Qualification", expectedCloseDateUtc = close })), "add a deal");
        var dealId = deal.GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/deals/{dealId}", Body(new { stage = "Proposal" })), "move it");

        // Eight days on is two days before the close date — but the date has been moved since.
        app.Clock.Advance(TimeSpan.FromDays(4));
        await Json(await owner.PutAsync($"/api/deals/{dealId}", Body(new { expectedCloseDateUtc = close.AddDays(7) })), "the close date moves a week");
        app.Clock.Advance(TimeSpan.FromDays(4).Add(TimeSpan.FromHours(1)));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.TaskItems.Count(t => t.DealId == dealId))).Should().Be(0, "the wait follows the moved date");

        app.Clock.Advance(TimeSpan.FromDays(7));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.TaskItems.Count(t => t.DealId == dealId && t.Title == "Close date in two days"))).Should().Be(1);
    }

    // ── send windows ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AnEmailDueOutsideTheSendWindowWaitsForItsNextOpening()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var zurich = RegionalSettings.ZoneOf("Europe/Zurich");
        // A Saturday at 20:00 in Zurich; the window is weekdays 09:00 to 17:00.
        app.Clock.Set(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 14, 20, 0, 0), zurich));
        var ruleId = await RuleAsync(owner, new
        {
            name = "Welcome in office hours", trigger = "lead.created",
            sendWindow = new { days = new[] { 1, 2, 3, 4, 5 }, start = "09:00", end = "17:00" },
            actions = new object[] { Task("Call them"), Email("Welcome") },
        });

        var leadId = await LeadAsync(owner);

        (await TasksAsync(app, leadId)).Should().ContainSingle("a task is not held by the window: only emails are");
        var run = await RunOfAsync(app, ruleId);
        run.Status.Should().Be(AutomationRunStatuses.Waiting);
        run.NextAttemptAtUtc.Should().Be(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 16, 9, 0, 0), zurich), "Monday at nine, Zurich time");
        run.WaitingFor.Should().Contain("send window");

        app.Clock.Set(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 16, 8, 59, 0), zurich));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.OutboundMessages.Count())).Should().Be(0);

        app.Clock.Set(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 16, 9, 1, 0), zurich));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.OutboundMessages.Count())).Should().Be(1);
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Completed);
    }

    // ── retries and failures ─────────────────────────────────────────────────

    [Fact]
    public async Task AFailedStepIsTriedAgainLater_ThenTheRunIsMarkedFailed_AndCanBeTriedAgainFromTheRulesPage()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Welcome", trigger = "lead.created", actions = new object[] { Task("Call them"), Email("Welcome") } });
        app.Flakiness.FailAutomationEmails = true;

        var leadId = await LeadAsync(owner);

        var run = await RunOfAsync(app, ruleId);
        run.Status.Should().Be(AutomationRunStatuses.Retrying);
        run.StepAttempts.Should().Be(1);
        run.LastError.Should().Contain("could not be reached");
        run.NextAttemptAtUtc.Should().BeCloseTo(app.Clock.GetUtcNow().UtcDateTime.AddMinutes(1), TimeSpan.FromSeconds(5));

        foreach (var gap in new[] { 1, 5, 30, 120 })
        {
            app.Clock.Advance(TimeSpan.FromMinutes(gap).Add(TimeSpan.FromSeconds(1)));
            await app.RunDueAsync();
        }
        run = await RunOfAsync(app, ruleId);
        run.Status.Should().Be(AutomationRunStatuses.Failed, "after the fifth try");
        run.StepAttempts.Should().Be(5);
        run.NextAttemptAtUtc.Should().BeNull();
        (await TasksAsync(app, leadId)).Should().ContainSingle("the steps before the failed one are not run again");

        // The rules page lists it among the failures, with the reason.
        var failures = (await Json(await owner.GetAsync("/api/automations/runs?status=failed"), "failures")).EnumerateArray().ToList();
        failures.Should().ContainSingle(r => r.GetProperty("id").GetGuid() == run.Id && r.GetProperty("lastError").GetString()!.Contains("could not be reached"));
        var rule = (await Json(await owner.GetAsync("/api/automations"), "rules")).EnumerateArray().Single();
        rule.GetProperty("failedRuns").GetInt32().Should().Be(1);
        var outcomes = (await LogAsync(owner, run.Id)).GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("outcome").GetString()).ToList();
        outcomes.Should().Equal("done", "retrying", "retrying", "retrying", "retrying", "failed");

        // Fixed, and tried again: it carries on from the failed step.
        app.Flakiness.FailAutomationEmails = false;
        (await Json(await owner.PostAsync($"/api/automations/runs/{run.Id}/retry", null), "try it again")).GetProperty("status").GetString().Should().Be("retrying");
        await app.RunDueAsync();
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Completed);
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.LeadId == leadId))).Should().Be(1);
        (await TasksAsync(app, leadId)).Should().ContainSingle();
        (await owner.PostAsync($"/api/automations/runs/{run.Id}/retry", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "only a failed run is tried again");
    }

    [Fact]
    public async Task AnEmailTheOutboxRefusesIsSkippedWithTheReason_AndTheRunGoesOn()
    {
        await using var app = new App();
        var (owner, _, org) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Welcome", trigger = "lead.created", actions = new object[] { Email("Welcome"), Task("Call them") } });
        // Their address bounced before: the outbox will not write to it again.
        using (var scope = app.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.EmailSuppressions.Add(new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = org, Email = "jean@example.ch", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var leadId = await LeadAsync(owner);

        var run = await RunOfAsync(app, ruleId);
        run.Status.Should().Be(AutomationRunStatuses.Completed, "trying again would not make the address take mail");
        (await TasksAsync(app, leadId)).Should().ContainSingle();
        var first = (await LogAsync(owner, run.Id)).GetProperty("steps").EnumerateArray().First();
        first.GetProperty("outcome").GetString().Should().Be("skipped");
        first.GetProperty("summary").GetString().Should().Contain("No email was sent");
    }

    // ── claims ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoRunnersTakingTheSameDueRunAtOnceRunItsStepOnce()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Later", trigger = "lead.created", actions = new object[] { Wait(10, "minutes"), Task("Call them") } });
        var leadId = await LeadAsync(owner);
        app.Clock.Advance(TimeSpan.FromMinutes(11));

        var run = await RunOfAsync(app, ruleId);
        var due = new DueAutomationRun(run.Id, run.ClaimCount);
        // Two instances saw it due at the same moment: each waits for the other to have seen
        // it too, then both try to take it.
        app.Gate.Arm("automations", readers: 2);
        using var first = app.Services.CreateScope();
        using var second = app.Services.CreateScope();
        await System.Threading.Tasks.Task.WhenAll(
            first.ServiceProvider.GetRequiredService<IAutomationEngine>().ResumeAsync(due),
            second.ServiceProvider.GetRequiredService<IAutomationEngine>().ResumeAsync(due));

        (await TasksAsync(app, leadId)).Should().ContainSingle("only the runner that claimed it ran the step");
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Completed);
        // And a pass of two runners side by side finds nothing left to do.
        await System.Threading.Tasks.Task.WhenAll(app.RunDueAsync(), app.RunDueAsync());
        (await TasksAsync(app, leadId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ARuleTurnedOffKeepsItsRunsWaiting_AndADeletedRuleTakesThemWithIt()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Later", trigger = "lead.created", actions = new object[] { Wait(1, "hours"), Task("Call them") } });
        var leadId = await LeadAsync(owner);

        await Json(await owner.PutAsync($"/api/automations/{ruleId}", Body(new { enabled = false })), "turn it off");
        app.Clock.Advance(TimeSpan.FromHours(2));
        await app.RunDueAsync();
        (await TasksAsync(app, leadId)).Should().BeEmpty("a rule that is off does nothing");
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Waiting);

        await Json(await owner.PutAsync($"/api/automations/{ruleId}", Body(new { enabled = true })), "turn it on again");
        await app.RunDueAsync();
        (await TasksAsync(app, leadId)).Should().ContainSingle();

        await LeadAsync(owner, "Ana Silva", "ana@example.ch");
        (await owner.DeleteAsync($"/api/automations/{ruleId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.Read(db => db.AutomationRuns.Count())).Should().Be(0);
        (await app.Services.Read(db => db.AutomationRunSteps.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AWaitingRunCanBeStopped()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Later", trigger = "lead.created", actions = new object[] { Wait(1, "days"), Email("Hello") } });
        await LeadAsync(owner);
        var run = await RunOfAsync(app, ruleId);

        (await Json(await owner.PostAsync($"/api/automations/runs/{run.Id}/cancel", null), "stop it")).GetProperty("status").GetString().Should().Be("cancelled");
        app.Clock.Advance(TimeSpan.FromDays(2));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.OutboundMessages.Count())).Should().Be(0);
        (await owner.PostAsync($"/api/automations/runs/{run.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── who sees and changes what ────────────────────────────────────────────

    [Fact]
    public async Task AnEditOfAnOlderCopyOfARuleIsRefused()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var created = await Json(await owner.PostAsync("/api/automations", Body(new { name = "Welcome", trigger = "lead.created", actions = new[] { Task("Call") } })), "add");
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetGuid();

        var first = await Json(await owner.PutAsync($"/api/automations/{id}", Body(new { name = "Welcome new leads", version })), "one manager's edit");
        first.GetProperty("version").GetGuid().Should().NotBe(version);

        var stale = await owner.PutAsync($"/api/automations/{id}", Body(new { name = "Hello", version }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadAsStringAsync()).Should().Contain("Automation.Changed.Conflict");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/automations/{id}") { Content = Body(new { name = "Hello" }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        (await owner.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Conflict, "If-Match is checked the same way");
        (await app.Services.Read(db => db.AutomationRules.Single().Name)).Should().Be("Welcome new leads");
    }

    [Fact]
    public async Task RunsAreForWhoeverManagesTheRules_AndAnotherWorkspacesRunsAreNotThere()
    {
        await using var app = new App();
        var (owner, _, org) = await WorkspaceAsync(app);
        var ruleId = await RuleAsync(owner, new { name = "Later", trigger = "lead.created", actions = new object[] { Wait(1, "days"), Task("Call") } });
        await LeadAsync(owner);
        var run = await RunOfAsync(app, ruleId);

        var (member, memberId) = await SignInAsync(app, "member@example.ch");
        await AddMemberAsync(app, org, memberId, OrgMemberRole.Member);
        member.In(org);
        (await member.GetAsync("/api/automations/runs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync($"/api/automations/runs/{run.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (stranger, _, _) = await WorkspaceAsync(app, "stranger@example.ch");
        (await Json(await stranger.GetAsync("/api/automations/runs"), "their runs")).GetArrayLength().Should().Be(0);
        (await stranger.GetAsync($"/api/automations/runs/{run.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/automations/runs/{run.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await RunOfAsync(app, ruleId)).Status.Should().Be(AutomationRunStatuses.Waiting);
    }

    [Fact]
    public async Task TheCatalogSaysWhatEachTriggerCanTestWaitForAndDo()
    {
        await using var app = new App();
        var (owner, _, _) = await WorkspaceAsync(app);
        var catalog = await Json(await owner.GetAsync("/api/automations/catalog"), "the catalog");
        var triggers = catalog.GetProperty("triggers").EnumerateArray().ToDictionary(t => t.GetProperty("key").GetString()!);
        triggers.Keys.Should().Contain(["lead.created", "membership.renewal_due", "reservation.created", "event.registered",
            "application.approved", "member.created", "payment.failed", "contract.signed", "contract.countersigned"]);
        triggers["membership.renewal_due"].GetProperty("takesDaysBefore").GetBoolean().Should().BeTrue();
        triggers["reservation.created"].GetProperty("dates").EnumerateArray().Select(d => d.GetProperty("key").GetString()).Should().Contain("start");
        triggers["member.created"].GetProperty("actions").EnumerateArray().Select(a => a.GetString()).Should().NotContain("assign");
        catalog.GetProperty("ops").GetProperty("money").EnumerateArray().Select(o => o.GetString()).Should().Contain("between");
    }
}
