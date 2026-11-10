using System.Globalization;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's daily billing run and what it does on its own: bill what is due, set dues past
/// due, remind members of what is overdue, lapse and reinstate them, and send the monthly
/// statements, each once; and statements as PDF, downloaded and emailed.
/// </summary>
/// <remarks>
/// Nothing time-based used to happen in the club: dues were billed when someone pressed a
/// button, charges never became overdue, nobody was chased or lapsed. The run here is the
/// background job's own entry point, called in no workspace, as the job calls it.
/// </remarks>
public class ClubBillingRunTests
{
    /// <summary>The app with a mail server that keeps what it is handed.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "manager@example.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string status = "Active") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status,
        })), "add member")).GetProperty("id").GetGuid();

    /// <summary>The first day of the month <paramref name="monthsAgo"/> months back, at midnight UTC.</summary>
    private static DateTime MonthStart(int monthsAgo)
    {
        var today = DateTime.UtcNow;
        return new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthsAgo);
    }

    private static async Task<JsonElement> SubscriptionAsync(HttpClient client, Guid memberId, DateTime firstPeriod, decimal amount) =>
        await Json(await client.PostAsync("/api/subscriptions", Body(new
        {
            memberId, name = "Gold dues", amount, cadence = "Monthly", nextChargeAtUtc = firstPeriod.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        })), "add subscription");

    private static async Task<List<JsonElement>> ChargesAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .OrderBy(c => c.TryGetProperty("billingPeriodStartUtc", out var p) ? p.GetDateTime() : c.GetProperty("postedAtUtc").GetDateTime())
            .ToList();

    private static DateTime Utc(JsonElement value) => DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    /// <summary>
    /// A moment today when a workspace's daily run may go (after 03:00 in Zurich): now, or
    /// later today when the test runs in the small hours.
    /// </summary>
    private static DateTime RunTime()
    {
        var zone = RegionalSettings.ZoneOf(RegionalSettings.DefaultTimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        return local.TimeOfDay >= TimeSpan.FromHours(3)
            ? DateTime.UtcNow
            : DateTime.UtcNow.Add(TimeSpan.FromHours(3) - local.TimeOfDay + TimeSpan.FromMinutes(1));
    }

    /// <summary>The daily run, as the background job does it: in no workspace, acting for each in turn.</summary>
    private static async Task<int> RunDailyAsync(CustomWebApplicationFactory factory, DateTime? at = null)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClubBilling>().RunScheduledAsync(at ?? RunTime());
    }

    private static async Task BillingSettingsAsync(HttpClient client, object billing) =>
        await Json(await client.PutAsync("/api/hospitality/settings", Body(new { billing })), "save the billing settings");

    private static async Task<string> PostChargeAsync(HttpClient client, Guid memberId, decimal amount, DateTime dueAtUtc, string description) =>
        (await Json(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Dining", description, amount, dueAtUtc = dueAtUtc.ToString("o") })), "post charge"))
            .GetProperty("id").GetString()!;

    private static async Task<JsonElement> MemberRowAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "members")).EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == memberId);

    // ── billing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheDailyRunBillsEveryStartedPeriodOnce_OnlyWhereAutomaticBillingIsOn_AndRecordsWhatItDid()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        // Three periods behind: two months ago, last month and this month.
        await SubscriptionAsync(client, memberId, MonthStart(2), amount: 100);
        var (other, _, _) = await ClubAsync(factory, "other@example.ch");
        var theirMember = await MemberAsync(other, "Paul");
        await SubscriptionAsync(other, theirMember, MonthStart(1), amount: 50);

        (await RunDailyAsync(factory)).Should().Be(0, "nothing bills on its own until a club turns it on");
        (await ChargesAsync(client, memberId)).Should().BeEmpty();

        var preview = await Json(await client.GetAsync("/api/billing/preview"), "what billing now would post");
        (preview.GetProperty("subscriptions").GetInt32(), preview.GetProperty("periods").GetInt32()).Should().Be((1, 3));
        preview.GetProperty("amountByCurrency").GetProperty("CHF").GetDecimal().Should().Be(300);

        await BillingSettingsAsync(client, new { automaticBilling = true });
        (await RunDailyAsync(factory)).Should().Be(1, "one workspace has automatic billing on");
        (await ChargesAsync(client, memberId)).Select(c => Utc(c.GetProperty("billingPeriodStartUtc")))
            .Should().Equal(MonthStart(2), MonthStart(1), MonthStart(0));
        (await ChargesAsync(other, theirMember)).Should().BeEmpty("the other club did not turn automatic billing on");

        (await RunDailyAsync(factory)).Should().Be(0, "today's run is done");
        await Json(await client.PostAsync("/api/billing/run", null), "bill what is due now");
        (await ChargesAsync(client, memberId)).Should().HaveCount(3, "every period is billed once, however often billing runs");

        var runs = (await Json(await client.GetAsync("/api/billing/runs"), "runs")).EnumerateArray().ToList();
        runs.Should().HaveCount(2);
        var manual = runs[0];
        (manual.GetProperty("trigger").GetString(), manual.GetProperty("chargesPosted").GetInt32()).Should().Be(("manual", 0));
        manual.GetProperty("startedByName").GetString().Should().NotBeNullOrEmpty();
        var daily = runs[1];
        daily.GetProperty("trigger").GetString().Should().Be("schedule");
        daily.GetProperty("status").GetString().Should().Be("succeeded");
        (daily.GetProperty("subscriptionsBilled").GetInt32(), daily.GetProperty("chargesPosted").GetInt32(), daily.GetProperty("amountBilled").GetDecimal())
            .Should().Be((1, 3, 300m));
        daily.GetProperty("steps").EnumerateArray().Select(x => x.GetString()).Should().Contain("billing");
        (await Json(await other.GetAsync("/api/billing/runs"), "the other club's runs")).GetArrayLength().Should().Be(0, "a club sees its own runs only");
    }

    [Fact]
    public async Task OnlyMoneyManagersBill_AndTheFrontDeskSeesNoRuns()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var desk = await JoinAsync(factory, client, org, "desk@example.ch", "front_desk");
        await Refused(await desk.PostAsync("/api/billing/run", null), HttpStatusCode.Forbidden, "the front desk billing");
        await Refused(await desk.GetAsync("/api/billing/runs"), HttpStatusCode.Forbidden, "the front desk reading runs");
        var finance = await JoinAsync(factory, client, org, "finance@example.ch", "finance");
        await Json(await finance.PostAsync("/api/billing/run", null), "finance billing");
    }

    // ── overdue, reminders and lapses ────────────────────────────────────────

    [Fact]
    public async Task DuesLeftUnpaidSetTheSubscriptionPastDue_AndItIsActiveAgainOnceTheyArePaid()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        await BillingSettingsAsync(client, new { automaticBilling = true, paymentTermsDays = 0 });
        var memberId = await MemberAsync(client);
        var id = (await SubscriptionAsync(client, memberId, MonthStart(1), amount: 80)).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", null), "bill last month, due at once");

        await RunDailyAsync(factory);
        async Task<string?> Status() => (await Json(await client.GetAsync("/api/subscriptions"), "subscriptions")).EnumerateArray()
            .Single(s => s.GetProperty("id").GetGuid() == id).GetProperty("status").GetString();
        (await Status()).Should().Be("PastDue");

        foreach (var charge in await ChargesAsync(client, memberId))
            await Json(await client.PostAsync("/api/payments", Body(new
            {
                amount = charge.GetProperty("amount").GetDecimal(), method = "Cash", chargeId = charge.GetProperty("id").GetString(),
            })), "pay");
        await Json(await client.PostAsync("/api/billing/run", null), "run again");
        (await Status()).Should().Be("Active");
    }

    [Fact]
    public async Task AMemberIsRemindedOnceAtEachStep_InOneEmailForAllTheirOverdueCharges_AndOnlyAtTheLatestStepWhenRemindersBegin()
    {
        await using var app = new App();
        var (client, _, _) = await ClubAsync(app);
        var memberId = await MemberAsync(client);
        var other = await MemberAsync(client, "Lea");
        await PostChargeAsync(client, memberId, 120, DateTime.UtcNow.AddDays(-40), "Wine dinner");
        await PostChargeAsync(client, memberId, 30, DateTime.UtcNow.AddDays(-5), "Bar tab");
        await PostChargeAsync(client, memberId, 50, DateTime.UtcNow.AddDays(5), "Not due yet");
        await PostChargeAsync(client, other, 10, DateTime.UtcNow.AddDays(-1), "Locker");

        await BillingSettingsAsync(client, new { remindersEnabled = true, reminderDays = new[] { 3, 14, 30 } });
        await RunDailyAsync(app);
        await app.DispatchOutboxAsync();

        var email = app.Mail.Sent.Should().ContainSingle("one email for Jean; Lea's charge is not three days late yet").Subject;
        email.To.Should().Equal("jean@example.ch");
        email.Subject.Should().Be("Final payment reminder", "the wine dinner is past every step");
        email.TextBody.Should().Contain("Wine dinner").And.Contain("Bar tab").And.NotContain("Not due yet");
        email.UnsubscribeUrl.Should().BeNull("a payment reminder is a service message");
        var reminders = await app.Services.Read(db => db.ClubPaymentReminders.OrderBy(r => r.Step).ToList());
        reminders.Where(r => r.Status == ReminderStatuses.Queued).Select(r => r.Step).Should().BeEquivalentTo([1, 3],
            "the bar tab at its first step, the wine dinner at its last");
        reminders.Where(r => r.Status == ReminderStatuses.Skipped).Select(r => r.Step).Should().Equal(1, 2);
        (await Json(await client.GetAsync($"/api/communications?memberId={memberId}"), "Jean's messages")).EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("subject").GetString() == "Final payment reminder", "the reminder is in the communications log");

        // The next day's run, and a run started by hand, remind nobody again.
        await RunDailyAsync(app, RunTime().AddDays(1));
        await Json(await client.PostAsync("/api/billing/run", null), "a run started by hand");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().HaveCount(1);
        (await app.Services.Read(db => db.ClubPaymentReminders.Count())).Should().Be(4);
    }

    [Fact]
    public async Task AnActiveMemberLapsesAfterTheGracePeriod_AndIsReinstatedByThePaymentThatSettlesIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var pending = await MemberAsync(client, "Pia", status: "Pending");
        var late = await PostChargeAsync(client, memberId, 300, DateTime.UtcNow.AddDays(-45), "Gold dues");
        await PostChargeAsync(client, pending, 300, DateTime.UtcNow.AddDays(-45), "Gold dues");
        var recent = await PostChargeAsync(client, memberId, 20, DateTime.UtcNow.AddDays(-2), "Bar tab");

        await BillingSettingsAsync(client, new { lapseEnabled = true, gracePeriodDays = 30 });
        await RunDailyAsync(factory);
        var lapsed = await MemberRowAsync(client, memberId);
        lapsed.GetProperty("status").GetString().Should().Be("Lapsed");
        lapsed.GetProperty("billingLapsedAtUtc").ValueKind.Should().Be(JsonValueKind.String);
        (await MemberRowAsync(client, pending)).GetProperty("status").GetString().Should().Be("Pending", "only an active member is lapsed");

        await Json(await client.PostAsync("/api/payments", Body(new { amount = 300, method = "Wire", chargeId = late })), "pay the late dues");
        (await MemberRowAsync(client, memberId)).GetProperty("status").GetString().Should().Be("Lapsed", "the bar tab is still overdue");
        await Json(await client.PostAsync("/api/payments", Body(new { amount = 20, method = "Cash", chargeId = recent })), "pay the bar tab");
        var back = await MemberRowAsync(client, memberId);
        back.GetProperty("status").GetString().Should().Be("Active", "nothing is overdue any more, so the payment puts them back");
        back.GetProperty("billingLapsedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreditAMemberAlreadyHoldsCoversWhatIsOverdue_SoTheyAreRemindedOfAndLapsedForOnlyWhatItDoesNot()
    {
        await using var app = new App();
        var (client, _, _) = await ClubAsync(app);
        // Jean and Max joined with credit brought over from before (an opening balance), which
        // no charge has been paid from yet: the charges page shows their dues as owing until
        // someone applies the credit, but Jean owes the club nothing and Max owes 200.
        async Task<Guid> WithCreditAsync(string first, decimal credit) =>
            (await Json(await client.PostAsync("/api/members", Body(new
            {
                firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
                houseAccountBalance = credit,
            })), $"add {first}")).GetProperty("id").GetGuid();
        var jean = await WithCreditAsync("Jean", 500);
        var max = await WithCreditAsync("Max", 100);
        var lea = await MemberAsync(client, "Lea");
        foreach (var member in new[] { jean, max, lea })
            await PostChargeAsync(client, member, 300, DateTime.UtcNow.AddDays(-45), "Gold dues");
        await BillingSettingsAsync(client, new { remindersEnabled = true, reminderDays = new[] { 3 }, lapseEnabled = true, gracePeriodDays = 30 });

        await RunDailyAsync(app);
        await app.DispatchOutboxAsync();

        app.Mail.Sent.Select(m => m.To.Single()).Should().BeEquivalentTo(["max@example.ch", "lea@example.ch"], "Jean's credit covers his dues");
        app.Mail.Sent.Single(m => m.To.Single() == "max@example.ch").TextBody.Should().Contain("CHF 200.00", "his credit pays 100 of the 300");
        (await MemberRowAsync(client, jean)).GetProperty("status").GetString().Should().Be("Active", "a member in credit is not lapsed");
        (await MemberRowAsync(client, max)).GetProperty("status").GetString().Should().Be("Lapsed");
        (await MemberRowAsync(client, lea)).GetProperty("status").GetString().Should().Be("Lapsed");
        (await Json(await client.GetAsync("/api/billing/runs"), "runs"))[0].GetProperty("overdueCharges").GetInt32().Should().Be(2);
    }

    /// <summary>The app with a billing that cannot put a member back: its database went away after the payment was saved.</summary>
    private sealed class AppThatCannotReinstate : CustomWebApplicationFactory
    {
        private sealed class CannotReinstate : IClubBilling
        {
            public Task ReinstateIfSettledAsync(Guid organizationId, Guid memberId, CancellationToken ct = default) =>
                throw new InvalidOperationException("The database went away.");

            public Task<ACI.Application.Common.Result<BillingRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<BillingPreviewDto> PreviewAsync(Guid organizationId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<BillingRunDto>> RunsAsync(Guid organizationId, int take = 20, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ACI.Application.Common.Result<StatementFile>> StatementPdfAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ACI.Application.Common.Result<StatementDeliveryDto>> EmailStatementAsync(Guid organizationId, Guid userId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => throw new NotSupportedException();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IClubBilling, CannotReinstate>());
        }
    }

    [Fact]
    public async Task APaymentThatWasSavedIsAnsweredAsSaved_EvenWhenPuttingTheMemberBackFails()
    {
        using var factory = new AppThatCannotReinstate();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var charge = await PostChargeAsync(client, memberId, 300, DateTime.UtcNow.AddDays(-45), "Gold dues");

        // Answered as a failure, the payment would be entered again: paid twice.
        await Json(await client.PostAsync("/api/payments", Body(new { amount = 100, method = "Wire", chargeId = charge })), "pay part of the dues");
        (await ChargesAsync(client, memberId)).Single().GetProperty("paidAmount").GetDecimal().Should().Be(100);
        var voidable = await PostChargeAsync(client, memberId, 20, DateTime.UtcNow.AddDays(-45), "Posted twice");
        await Json(await client.PostAsync($"/api/charges/{voidable}/void", Body(new { reason = "posted twice" })), "void a charge");
    }

    [Fact]
    public async Task AMemberLapsedByTheBillingButSetByHandIsLeftAsSet()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var late = await PostChargeAsync(client, memberId, 300, DateTime.UtcNow.AddDays(-45), "Gold dues");
        await BillingSettingsAsync(client, new { lapseEnabled = true, gracePeriodDays = 30 });
        await RunDailyAsync(factory);
        (await MemberRowAsync(client, memberId)).GetProperty("status").GetString().Should().Be("Lapsed");
        await Json(await client.PutAsync($"/api/members/{memberId}", Body(new { status = "Cancelled" })), "the member resigns");

        await Json(await client.PostAsync("/api/payments", Body(new { amount = 300, method = "Wire", chargeId = late })), "they settle up");
        await RunDailyAsync(factory, RunTime().AddDays(1));
        (await MemberRowAsync(client, memberId)).GetProperty("status").GetString().Should().Be("Cancelled", "a status someone set is theirs");
    }

    // ── statements ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AStatementDownloadsAsAPdfBuiltFromTheLedger_AndAnotherClubCannotFetchIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        await PostChargeAsync(client, memberId, 299, DateTime.UtcNow.AddDays(14), "Gold dues");
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("o"));

        var res = await client.GetAsync($"/api/members/{memberId}/statement.pdf?from={from}&to={to}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        res.Content.Headers.ContentDisposition!.FileName.Should().Contain("statement-").And.Contain("jean-dupont");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");

        var (other, _, _) = await ClubAsync(factory, "other@example.ch");
        await Refused(await other.GetAsync($"/api/members/{memberId}/statement.pdf?from={from}&to={to}"), HttpStatusCode.NotFound,
            "another club's member's statement");
    }

    [Fact]
    public async Task AStatementIsEmailedWithItsPdf_AsAServiceMessage_ToAMemberWithAnAddress()
    {
        await using var app = new App();
        var (client, _, org) = await ClubAsync(app);
        var memberId = await MemberAsync(client);
        await PostChargeAsync(client, memberId, 299, DateTime.UtcNow.AddDays(14), "Gold dues");
        var period = new { from = DateTime.UtcNow.AddDays(-1), to = DateTime.UtcNow.AddDays(1) };

        var delivery = await Json(await client.PostAsync($"/api/members/{memberId}/statement/email", Body(period)), "email the statement");
        (delivery.GetProperty("status").GetString(), delivery.GetProperty("amountDue").GetDecimal()).Should().Be(("queued", 299m));
        await app.DispatchOutboxAsync();
        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.To.Should().Equal("jean@example.ch");
        email.Subject.Should().StartWith("Your statement for");
        email.TextBody.Should().Contain("Amount due: CHF");
        var pdf = email.Attachments.Should().ContainSingle().Subject;
        pdf.ContentType.Should().Be("application/pdf");
        System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 5).Should().Be("%PDF-");
        email.UnsubscribeUrl.Should().BeNull("a statement is a service message: it carries no marketing unsubscribe");
        (await Json(await client.GetAsync($"/api/communications?memberId={memberId}"), "Jean's messages")).EnumerateArray()
            .Should().ContainSingle(m => m.GetProperty("subject").GetString()!.StartsWith("Your statement for"));

        var noAddress = (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Max", lastName = "Muster", email = "not an address", tier = "Gold",
        })), "a member with no usable address")).GetProperty("id").GetGuid();
        await Refused(await client.PostAsync($"/api/members/{noAddress}/statement/email", Body(period)), HttpStatusCode.BadRequest, "emailing a member with no address");
        var desk = await JoinAsync(app, client, org, "desk@example.ch", "front_desk");
        await Refused(await desk.PostAsync($"/api/members/{memberId}/statement/email", Body(period)), HttpStatusCode.Forbidden, "the front desk emailing a statement");
    }

    [Fact]
    public async Task TheMonthlyRunSendsLastMonthsStatementOncePerMember_ToThoseWithActivityOrABalance()
    {
        await using var app = new App();
        var (client, _, _) = await ClubAsync(app);
        var owing = await MemberAsync(client);
        await MemberAsync(client, "Lea");
        await PostChargeAsync(client, owing, 299, DateTime.UtcNow.AddDays(14), "Gold dues");
        await BillingSettingsAsync(client, new { monthlyStatements = true, statementDay = 1 });

        // Next month's run, early on its first day: Jean owes, so he gets this month's
        // statement; Lea had nothing on her account, and gets none.
        var zone = RegionalSettings.ZoneOf(RegionalSettings.DefaultTimeZone);
        var today = RegionalSettings.TodayIn(zone, DateTime.UtcNow);
        var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        var at = RegionalSettings.StartOfDayUtc(nextMonth, zone).AddHours(4);
        await RunDailyAsync(app, at);
        await app.DispatchOutboxAsync();
        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.To.Should().Equal("jean@example.ch");
        email.Subject.Should().Be($"Your statement for {new DateTime(today.Year, today.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture)}");
        email.Attachments.Should().ContainSingle();

        await RunDailyAsync(app, at.AddDays(1));
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().HaveCount(1, "a member gets a month's statement once");
        (await app.Services.Read(db => db.ClubStatementDeliveries.Count(d => d.Source == StatementSources.Monthly))).Should().Be(1);
    }
}
