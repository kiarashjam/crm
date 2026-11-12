using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;
using App = ACI.WebApi.Tests.AutomationRunsTests.App;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's triggers: a membership coming up for renewal, a booking, a place on an event's
/// list, an approved application and a new member, a contract signed and countersigned. (A
/// payment that fails is in StripePaymentsTests, which can play Stripe.)
/// </summary>
public class AutomationClubTriggersTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid OwnerId, Guid Org)> ClubAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, ownerId, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string tier = "Gold") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier, status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task RuleAsync(HttpClient client, object rule) => await Json(await client.PostAsync("/api/automations", Body(rule)), "add the rule");

    private static object Task(string title, string? assigneeId = null) =>
        new { type = "create_task", config = assigneeId == null ? (object)new { title } : new { title, assigneeId } };

    private static object Condition(string field, string op, string? value = null) =>
        new { match = "all", conditions = new[] { new { field, op, value } } };

    [Fact]
    public async Task ARenewalComingUpSetsOffItsRule_OnceForEachPeriod_WithinTheDaysTheRuleSays()
    {
        await using var app = new App();
        var (owner, _, org) = await ClubAsync(app);
        var jean = await MemberAsync(owner, "Jean");
        var now = app.Clock.GetUtcNow().UtcDateTime;
        Guid Subscription(DateTime nextCharge, string cadence)
        {
            using var scope = app.Services.CreateScope();
            using var db = scope.ServiceProvider.EveryWorkspace();
            var id = Guid.NewGuid();
            db.ClubSubscriptions.Add(new ClubSubscription
            {
                Id = id, OrganizationId = org, MemberId = jean, MemberName = "Jean Dupont", Name = $"Gold dues ({cadence})", Amount = 299m, Currency = "CHF",
                Cadence = cadence, Status = "Active", StartedAtUtc = now.AddMonths(-11), NextChargeAtUtc = nextCharge, CreatedAtUtc = now.AddMonths(-11),
            });
            db.SaveChanges();
            return id;
        }
        Subscription(now.AddDays(10), "Annual");
        Subscription(now.AddDays(30), "Annual");
        Subscription(now.AddDays(5), "Monthly");
        await RuleAsync(owner, new
        {
            name = "Renewal call", trigger = "membership.renewal_due", triggerConfig = new { daysBefore = "14" },
            conditions = Condition("cadence", "is", "Annual"),
            actions = new object[]
            {
                Task("Call about the renewal"),
                new { type = "send_email", config = new { subject = "Your membership renews soon", body = "Dear {{firstName}}", purpose = "service" } },
            },
        });

        await app.ScanRenewalsAsync();

        var runs = await app.Services.Read(db => db.AutomationRuns.ToList());
        runs.Should().ContainSingle("only the yearly subscription ends within 14 days; the monthly one is not yearly");
        runs[0].RecordType.Should().Be("subscription");
        var task = await app.Services.Read(db => db.TaskItems.Single());
        task.Title.Should().Be("Call about the renewal");
        task.Description.Should().Contain("Jean Dupont");
        var email = await app.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Automation));
        email.To.Should().Be("jean@example.ch");
        email.MemberId.Should().Be(jean);
        email.Purpose.Should().Be(ConsentPurposes.Service, "a renewal reminder is about the membership, and goes whatever they said about marketing");

        // Looked at again an hour later, and the next day: the same period sets nothing off twice.
        await app.ScanRenewalsAsync();
        app.Clock.Advance(TimeSpan.FromDays(1));
        await app.ScanRenewalsAsync();
        (await app.Services.Read(db => db.AutomationRuns.Count())).Should().Be(1);

        // Seventeen days on, the second yearly subscription is within 14 days.
        app.Clock.Advance(TimeSpan.FromDays(16));
        await app.ScanRenewalsAsync();
        (await app.Services.Read(db => db.AutomationRuns.Count())).Should().Be(2);
    }

    [Fact]
    public async Task ABookingSetsOffItsRule_AndConditionsCanAskWhetherItIsForAMember()
    {
        await using var app = new App();
        var (owner, ownerId, _) = await ClubAsync(app);
        await RuleAsync(owner, new
        {
            name = "Member booked", trigger = "reservation.created",
            conditions = Condition("forMember", "is_true"),
            actions = new object[] { new { type = "notify", config = new { message = "A member booked a space" } } },
        });
        var jean = await MemberAsync(owner, "Jean");
        var court = (await Json(await owner.PostAsync("/api/resources", Body(new { name = "Court", kind = "Court", capacity = 4 })), "add a court")).GetProperty("id").GetString();
        var start = DateTime.UtcNow.AddDays(2);

        await Json(await owner.PostAsync("/api/reservations", Body(new { resourceId = court, memberId = jean, startAtUtc = start, endAtUtc = start.AddHours(1) })), "Jean books");
        await Json(await owner.PostAsync("/api/reservations", Body(new { resourceId = court, memberName = "Claire Guest", startAtUtc = start.AddHours(2), endAtUtc = start.AddHours(3) })), "a guest books");

        var notice = await app.Services.Read(db => db.Notifications.Where(n => n.Title == "Member booked").ToList());
        notice.Should().ContainSingle("the guest's booking is not for a member");
        notice[0].UserId.Should().Be(ownerId);
        notice[0].Link.Should().Be("/reservations");
    }

    [Fact]
    public async Task APlaceOnTheWaitlistSetsOffItsRule_AndTheTaskGoesToWhoeverTheRuleNames()
    {
        await using var app = new App();
        var (owner, _, org) = await ClubAsync(app);
        var (_, directorId) = await SignInAsync(app, "director@pavillon46.ch");
        await AddMemberAsync(app, org, directorId, OrgMemberRole.Manager);
        await RuleAsync(owner, new
        {
            name = "Waitlisted", trigger = "event.registered",
            conditions = Condition("placeStatus", "is", "Waitlisted"),
            actions = new[] { Task("Offer another date", directorId.ToString()) },
        });
        var start = DateTime.UtcNow.AddDays(7);
        var eventId = (await Json(await owner.PostAsync("/api/events", Body(new
        {
            name = "Wine night", startAtUtc = start, endAtUtc = start.AddHours(3), capacity = 1, status = "Published",
        })), "add the event")).GetProperty("id").GetGuid();

        await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = await MemberAsync(owner, "Jean"), ticketTier = "Member" })), "Jean takes the seat");
        await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = await MemberAsync(owner, "Anna"), ticketTier = "Member" })), "Anna is waitlisted");

        var task = await app.Services.Read(db => db.TaskItems.Single());
        task.Title.Should().Be("Offer another date");
        task.AssigneeId.Should().Be(directorId, "the rule names who the task is for");
        task.Description.Should().Contain("Anna Dupont");
    }

    [Fact]
    public async Task AnApprovalSetsOffTheApplicationAndNewMemberRules_Once_AndSoDoesAMemberAddedByHand()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        await RuleAsync(owner, new
        {
            name = "Welcome aboard", trigger = "application.approved",
            actions = new object[] { new { type = "send_email", config = new { subject = "Welcome aboard, {{firstName}}", body = "See you soon.", purpose = "service" } } },
        });
        await RuleAsync(owner, new { name = "Tour", trigger = "member.created", actions = new[] { Task("Book the welcome tour") } });
        var applicationId = (await Json(await owner.PostAsync("/api/applications", Body(new
        {
            firstName = "Claire", lastName = "Martin", email = "claire@example.ch", requestedTier = "Gold",
        })), "apply")).GetProperty("id").GetGuid();

        await Json(await owner.PostAsync($"/api/applications/{applicationId}/approve", Body(new { })), "approve");
        await Json(await owner.PostAsync($"/api/applications/{applicationId}/approve", Body(new { })), "approve again, by mistake");

        var email = await app.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Automation));
        email.Subject.Should().Be("Welcome aboard, Claire");
        email.Purpose.Should().Be(ConsentPurposes.Service);
        (await app.Services.Read(db => db.TaskItems.Count(t => t.Title == "Book the welcome tour"))).Should().Be(1);
        (await app.Services.Read(db => db.AutomationRuns.Count())).Should().Be(2, "the second approval found it approved and set nothing off");

        await MemberAsync(owner, "Jean");
        (await app.Services.Read(db => db.TaskItems.Count(t => t.Title == "Book the welcome tour"))).Should().Be(2);
    }

    [Fact]
    public async Task MembersFromAnImportSetOffNoNewMemberRule_ButOneAddedByHandDoes()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        await RuleAsync(owner, new
        {
            name = "Welcome", trigger = "member.created",
            actions = new object[]
            {
                Task("Book the welcome tour"),
                new { type = "send_email", config = new { subject = "Welcome, {{firstName}}", body = "See you soon.", purpose = "service" } },
            },
        });

        // A members file, row by row, as the import page sends it.
        foreach (var first in new[] { "Eleanor", "Felix" })
            await Json(await owner.PostAsync("/api/members", Body(new
            {
                firstName = first, lastName = "Tanaka", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active", origin = "import",
            })), $"import {first}");

        (await app.Services.Read(db => db.AutomationRuns.Count())).Should().Be(0, "a club moving its list over welcomes nobody on it");
        (await app.Services.Read(db => db.TaskItems.Count())).Should().Be(0);
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Automation))).Should().Be(0);

        await MemberAsync(owner, "Jean");
        (await app.Services.Read(db => db.TaskItems.Count(t => t.Title == "Book the welcome tour"))).Should().Be(1, "someone joining now is welcomed");
        (await owner.PostAsync("/api/members", Body(new { firstName = "Paul", lastName = "Weber", email = "paul@example.ch", origin = "csv" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A rule that emails a day before its record's date: a reminder.</summary>
    private static object Reminder(string trigger, string date, object? triggerConfig = null) => new
    {
        name = "Reminder", trigger, triggerConfig,
        actions = new object[]
        {
            new { type = "wait", config = new { mode = "until", date, offsetAmount = "-1", offsetUnit = "days" } },
            new { type = "send_email", config = new { subject = "See you tomorrow", body = "Dear {{firstName}}", purpose = "service" } },
        },
    };

    private static Task<List<string?>> RemindedAsync(App app) =>
        app.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.Automation).Select(m => m.To).ToList());

    [Fact]
    public async Task ARunAboutABookingCancelledMeanwhileStops_SoItsReminderDoesNotGo()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        await RuleAsync(owner, Reminder("reservation.created", "start"));
        var jean = await MemberAsync(owner, "Jean");
        var anna = await MemberAsync(owner, "Anna");
        var court = (await Json(await owner.PostAsync("/api/resources", Body(new { name = "Court", kind = "Court", capacity = 4 })), "add a court")).GetProperty("id").GetString();
        var start = app.Clock.GetUtcNow().UtcDateTime.AddDays(3);
        await Json(await owner.PostAsync("/api/reservations", Body(new { resourceId = court, memberId = jean, startAtUtc = start, endAtUtc = start.AddHours(1) })), "Jean books");
        var annas = (await Json(await owner.PostAsync("/api/reservations", Body(new { resourceId = court, memberId = anna, startAtUtc = start.AddHours(2), endAtUtc = start.AddHours(3) })), "Anna books"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/reservations/{annas}", Body(new { status = "Cancelled" })), "Anna cancels");

        app.Clock.Advance(TimeSpan.FromDays(2).Add(TimeSpan.FromHours(3)));
        await app.RunDueAsync();

        (await RemindedAsync(app)).Should().Equal(["jean@example.ch"], "a cancelled booking is not one to come to");
        var stopped = await app.Services.Read(db => db.AutomationRuns.Single(r => r.RecordId == annas));
        stopped.Status.Should().Be(AutomationRunStatuses.Cancelled);
        stopped.LastError.Should().Contain("booking was cancelled");
    }

    [Fact]
    public async Task ARunAboutAPlaceCancelledMeanwhileStops_SoItsReminderDoesNotGo()
    {
        await using var app = new App();
        var (owner, _, _) = await ClubAsync(app);
        await RuleAsync(owner, Reminder("event.registered", "eventStart"));
        var start = app.Clock.GetUtcNow().UtcDateTime.AddDays(3);
        var eventId = (await Json(await owner.PostAsync("/api/events", Body(new
        {
            name = "Wine night", startAtUtc = start, endAtUtc = start.AddHours(3), capacity = 10, status = "Published",
        })), "add the event")).GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = await MemberAsync(owner, "Jean"), ticketTier = "Member" })), "Jean registers");
        var annas = (await Json(await owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = await MemberAsync(owner, "Anna"), ticketTier = "Member" })), "Anna registers"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/events/{eventId}/attendees/{annas}", Body(new { status = "Cancelled" })), "Anna cancels");

        app.Clock.Advance(TimeSpan.FromDays(2).Add(TimeSpan.FromHours(3)));
        await app.RunDueAsync();

        (await RemindedAsync(app)).Should().Equal(["jean@example.ch"]);
        (await app.Services.Read(db => db.AutomationRuns.Single(r => r.RecordId == annas))).LastError.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ARenewalRunForASubscriptionCancelledMeanwhileStops_SoNoRenewalReminderGoes()
    {
        await using var app = new App();
        var (owner, _, org) = await ClubAsync(app);
        var jean = await MemberAsync(owner, "Jean");
        var now = app.Clock.GetUtcNow().UtcDateTime;
        var subscription = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.ClubSubscriptions.Add(new ClubSubscription
            {
                Id = subscription, OrganizationId = org, MemberId = jean, MemberName = "Jean Dupont", Name = "Gold dues", Amount = 299m, Currency = "CHF",
                Cadence = "Annual", Status = "Active", StartedAtUtc = now.AddMonths(-11), NextChargeAtUtc = now.AddDays(10), CreatedAtUtc = now.AddMonths(-11),
            });
            await db.SaveChangesAsync();
        }
        await RuleAsync(owner, Reminder("membership.renewal_due", "periodEnd", new { daysBefore = "14" }));
        await app.ScanRenewalsAsync();
        (await app.Services.Read(db => db.AutomationRuns.Single())).Status.Should().Be(AutomationRunStatuses.Waiting);

        await Json(await owner.PutAsync($"/api/subscriptions/{subscription}", Body(new { status = "Cancelled" })), "Jean cancels");
        app.Clock.Advance(TimeSpan.FromDays(9).Add(TimeSpan.FromHours(1)));
        await app.RunDueAsync();

        (await RemindedAsync(app)).Should().BeEmpty("a cancelled subscription does not renew");
        (await app.Services.Read(db => db.AutomationRuns.Single())).LastError.Should().Contain("subscription was cancelled");
    }

    [Fact]
    public async Task ASignatureAndACountersignatureSetOffTheirRules_OnTheContractsLead()
    {
        await using var app = new App();
        var (owner, ownerId, _) = await ClubAsync(app);
        await RuleAsync(owner, new { name = "Countersign", trigger = "contract.signed", actions = new[] { Task("Countersign it") } });
        await RuleAsync(owner, new
        {
            name = "Executed", trigger = "contract.countersigned",
            actions = new object[] { new { type = "notify", config = new { message = "Both sides have signed" } } },
        });
        var leadId = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "add a lead"))
            .GetProperty("id").GetGuid();
        var contractId = (await Json(await owner.PostAsync("/api/contracts/draft", Body(new { leadId, title = "Membership agreement" })), "draft"))
            .GetProperty("id").GetString();
        await Json(await owner.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership agreement", body = "MEMBERSHIP AGREEMENT\n\nCHF 1'450.00 per year.\n", counterpartyName = "Jean Dupont", counterpartyEmail = "jean@example.ch",
        })), "edit");
        var token = (await Json(await owner.PostAsync($"/api/contracts/{contractId}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();

        using var signer = app.CreateClient();
        await Json(await signer.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Jean Dupont", agreed = true })), "Jean signs");

        var task = await app.Services.Read(db => db.TaskItems.Single(t => t.Title == "Countersign it"));
        task.LeadId.Should().Be(leadId, "the rule works on the contract's lead");
        (await app.Services.Read(db => db.Notifications.Count(n => n.Title == "Executed"))).Should().Be(0);

        await Json(await owner.PostAsync($"/api/contracts/{contractId}/countersign", Body(new { signatureName = "Kia Jam", agreed = true })), "countersign");
        var notice = await app.Services.Read(db => db.Notifications.Single(n => n.Title == "Executed"));
        notice.UserId.Should().Be(ownerId);
        notice.Link.Should().Be($"/leads/{leadId}");
    }

    [Fact]
    public async Task AClubRulesRunWaitsWhileTheClubIsTurnedOff_AndCarriesOnWhenItIsBack()
    {
        await using var app = new App();
        var (owner, _, org) = await ClubAsync(app);
        await RuleAsync(owner, new
        {
            name = "A week on", trigger = "member.created",
            actions = new object[] { new { type = "wait", config = new { amount = "7", unit = "days" } }, Task("How is it going?") },
        });
        await MemberAsync(owner, "Jean");
        await Json(await owner.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })), "turn the club off");

        app.Clock.Advance(TimeSpan.FromDays(8));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.TaskItems.Count())).Should().Be(0, "nobody could see the task's member while the club is off");
        var run = await app.Services.Read(db => db.AutomationRuns.Single());
        run.Status.Should().Be(AutomationRunStatuses.Waiting);
        run.WaitingFor.Should().Contain("Club module");

        await Json(await owner.PutAsync($"/api/organizations/{org}/modules", Body(new { club = true })), "turn it back on");
        app.Clock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.TaskItems.Count(t => t.Title == "How is it going?"))).Should().Be(1);
    }
}
