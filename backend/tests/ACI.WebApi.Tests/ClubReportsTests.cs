using System.Globalization;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Club Reports and Today at the Club, summed on the server: cash revenue from the money
/// received when it came, accrual revenue from charges with voids in their own period,
/// receivables by age, gift cards, loyalty points and subscriptions as they stood, event fill
/// and no-shows; per currency, on the workspace's calendar, for its own workspace alone.
/// </summary>
/// <remarks>
/// The page added up charges in the browser: revenue was what fully paid charges came to, so
/// a member paying dues in three instalments showed nothing for two months and everything
/// in the third, in dollars.
/// </remarks>
public class ClubReportsTests
{
    internal static readonly TimeZoneInfo ZurichZone = RegionalSettings.ZoneOf("Europe/Zurich");

    /// <summary>A moment on the workspace's calendar (Zurich), as UTC.</summary>
    internal static DateTime Zurich(int year, int month, int day, int hour = 12, int minute = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), ZurichZone);

    internal static string Q(DateTime utc) => Uri.EscapeDataString(utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory App, HttpClient Owner, Guid OwnerId, Guid Org)> ClubAsync(CustomWebApplicationFactory app, string email = "olga@pavillon46.ch")
    {
        var (owner, ownerId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        return (app, owner, ownerId, org);
    }

    private static async Task Seed(CustomWebApplicationFactory app, Action<AppDbContext> add)
    {
        using var scope = app.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        add(db);
        await db.SaveChangesAsync();
    }

    // ── seeds, shared with the SQL Server test ───────────────────────────────

    internal static ClubCharge Charge(Guid org, Guid member, string kind, decimal amount, DateTime postedUtc, DateTime? dueUtc = null,
        string entryType = ClubEntryTypes.Charge, string currency = "CHF", Guid? reverses = null) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, MemberId = member, MemberName = "Anna Keller", EntryType = entryType, Kind = kind,
        Description = $"{kind} {amount}", Amount = amount, Currency = currency, PostedAtUtc = postedUtc, DueAtUtc = dueUtc,
        ReversesChargeId = reverses, CreatedAtUtc = postedUtc,
    };

    internal static ClubPayment Payment(Guid org, Guid member, decimal amount, DateTime receivedUtc, string entryType = ClubEntryTypes.Payment,
        string currency = "CHF", Guid? reverses = null) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, MemberId = member, MemberName = "Anna Keller", EntryType = entryType,
        Method = entryType == ClubEntryTypes.Payment ? "Wire" : null, Amount = amount, Currency = currency, ReceivedAtUtc = receivedUtc,
        ReversesPaymentId = reverses, CreatedAtUtc = receivedUtc,
    };

    internal static ClubPaymentAllocation Allocation(Guid org, ClubPayment payment, ClubCharge charge, decimal amount) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, PaymentId = payment.Id, ChargeId = charge.Id, Amount = amount, CreatedAtUtc = payment.ReceivedAtUtc,
    };

    /// <summary>
    /// Dues of 300 paid in three instalments; a bar charge voided the next month; a cheque that
    /// bounced; a refund; and balances brought over or set by hand, which are not sales or cash.
    /// </summary>
    internal static void SeedRevenue(AppDbContext db, Guid org)
    {
        var member = Guid.NewGuid();
        var dues = Charge(org, member, "Dues", 300m, Zurich(2025, 1, 10), Zurich(2025, 1, 24));
        var bar = Charge(org, member, "Bar", 100m, Zurich(2025, 1, 20));
        var barVoid = Charge(org, member, "Bar", -100m, Zurich(2025, 2, 5), entryType: ClubEntryTypes.Void, reverses: bar.Id);
        var opening = Charge(org, member, "Other", 500m, Zurich(2024, 12, 1), entryType: ClubEntryTypes.Opening);
        var adjustment = Charge(org, member, "Other", 40m, Zurich(2025, 3, 1), entryType: ClubEntryTypes.Adjustment);
        db.ClubCharges.AddRange(dues, bar, barVoid, opening, adjustment);

        var p1 = Payment(org, member, 100m, Zurich(2025, 1, 15));
        var p2 = Payment(org, member, 100m, Zurich(2025, 2, 15));
        var p3 = Payment(org, member, 100m, Zurich(2025, 3, 15));
        var cheque = Payment(org, member, 80m, Zurich(2025, 2, 1));
        var bounced = Payment(org, member, -80m, Zurich(2025, 2, 3), ClubEntryTypes.Void, reverses: cheque.Id);
        var refund = Payment(org, member, -20m, Zurich(2025, 3, 20), ClubEntryTypes.Refund, reverses: p3.Id);
        var credit = Payment(org, member, 50m, Zurich(2025, 1, 2), ClubEntryTypes.Opening);
        var creditTakenBack = Payment(org, member, -50m, Zurich(2025, 3, 1), ClubEntryTypes.Void, reverses: credit.Id);
        db.ClubPayments.AddRange(p1, p2, p3, cheque, bounced, refund, credit, creditTakenBack);
        db.ClubPaymentAllocations.AddRange(Allocation(org, p1, dues, 100m), Allocation(org, p2, dues, 100m), Allocation(org, p3, dues, 100m));
    }

    /// <summary>
    /// Open charges of every age at 1 April 2025, one part paid, one paid in full, one voided
    /// before then and one voided after.
    /// </summary>
    internal static void SeedReceivables(AppDbContext db, Guid org)
    {
        var member = Guid.NewGuid();
        var opening = Charge(org, member, "Other", 500m, Zurich(2024, 12, 1), entryType: ClubEntryTypes.Opening);    // over 90
        var adjustment = Charge(org, member, "Other", 40m, Zurich(2025, 3, 1), entryType: ClubEntryTypes.Adjustment); // 31 days
        var dining = Charge(org, member, "Dining", 60m, Zurich(2025, 3, 10), Zurich(2025, 3, 25));                   // 7 days
        var spa = Charge(org, member, "Spa", 70m, Zurich(2025, 3, 25), Zurich(2025, 4, 8));                          // not yet due
        var evening = Charge(org, member, "Event", 90m, Zurich(2025, 1, 5), Zurich(2025, 1, 19));                    // 72 days, part paid
        var retail = Charge(org, member, "Retail", 25m, Zurich(2025, 3, 28));                                         // voided after
        var retailVoid = Charge(org, member, "Retail", -25m, Zurich(2025, 4, 3), entryType: ClubEntryTypes.Void, reverses: retail.Id);
        var bar = Charge(org, member, "Bar", 15m, Zurich(2025, 2, 1));                                                // voided before
        var barVoid = Charge(org, member, "Bar", -15m, Zurich(2025, 2, 2), entryType: ClubEntryTypes.Void, reverses: bar.Id);
        var paid = Charge(org, member, "Dues", 120m, Zurich(2025, 1, 1), Zurich(2025, 1, 15));                       // paid in full
        db.ClubCharges.AddRange(opening, adjustment, dining, spa, evening, retail, retailVoid, bar, barVoid, paid);
        var part = Payment(org, member, 30m, Zurich(2025, 1, 25));
        var whole = Payment(org, member, 120m, Zurich(2025, 1, 10));
        db.ClubPayments.AddRange(part, whole);
        db.ClubPaymentAllocations.AddRange(Allocation(org, part, evening, 30m), Allocation(org, whole, paid, 120m));
    }

    /// <summary>A card with 70 left, an expired one, points held by two members (one below zero), and subscriptions.</summary>
    internal static void SeedStanding(AppDbContext db, Guid org)
    {
        var card = new ClubGiftCard { Id = Guid.NewGuid(), OrganizationId = org, Code = "GC-1", FaceValue = 100m, Currency = "CHF", IssuedAtUtc = Zurich(2025, 1, 5), CreatedAtUtc = Zurich(2025, 1, 5) };
        var expired = new ClubGiftCard { Id = Guid.NewGuid(), OrganizationId = org, Code = "GC-2", FaceValue = 50m, Currency = "CHF", IssuedAtUtc = Zurich(2024, 1, 5), ExpiresAtUtc = Zurich(2025, 1, 5), CreatedAtUtc = Zurich(2024, 1, 5) };
        db.ClubGiftCards.AddRange(card, expired);
        db.ClubGiftCardTransactions.AddRange(
            new ClubGiftCardTransaction { Id = Guid.NewGuid(), OrganizationId = org, GiftCardId = card.Id, Type = "Issue", Amount = 100m, Currency = "CHF", AtUtc = Zurich(2025, 1, 5), CreatedAtUtc = Zurich(2025, 1, 5) },
            new ClubGiftCardTransaction { Id = Guid.NewGuid(), OrganizationId = org, GiftCardId = card.Id, Type = "Redeem", Amount = -30m, Currency = "CHF", AtUtc = Zurich(2025, 2, 5), CreatedAtUtc = Zurich(2025, 2, 5) },
            // Spent after the report's end: still on the card then.
            new ClubGiftCardTransaction { Id = Guid.NewGuid(), OrganizationId = org, GiftCardId = card.Id, Type = "Redeem", Amount = -10m, Currency = "CHF", AtUtc = Zurich(2025, 5, 5), CreatedAtUtc = Zurich(2025, 5, 5) },
            new ClubGiftCardTransaction { Id = Guid.NewGuid(), OrganizationId = org, GiftCardId = expired.Id, Type = "Issue", Amount = 50m, Currency = "CHF", AtUtc = Zurich(2024, 1, 5), CreatedAtUtc = Zurich(2024, 1, 5) });

        Guid anna = Guid.NewGuid(), bruno = Guid.NewGuid();
        db.LoyaltyLedger.AddRange(
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna", Points = 700, CreatedAtUtc = Zurich(2025, 1, 10) },
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna", Points = -200, Kind = "Redeemed", CreatedAtUtc = Zurich(2025, 2, 10) },
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = bruno, MemberName = "Bruno", Points = -100, Kind = "Redeemed", CreatedAtUtc = Zurich(2025, 2, 10) });

        ClubSubscription Sub(string cadence, decimal amount, string status = "Active") => new()
        {
            Id = Guid.NewGuid(), OrganizationId = org, MemberId = Guid.NewGuid(), MemberName = "M", Name = $"{cadence} dues", Amount = amount,
            Currency = "CHF", Cadence = cadence, Status = status, StartedAtUtc = Zurich(2024, 1, 1), CreatedAtUtc = Zurich(2024, 1, 1),
        };
        db.ClubSubscriptions.AddRange(Sub("Monthly", 120m), Sub("Annual", 1200m), Sub("Quarterly", 300m), Sub("Monthly", 99m, "PastDue"),
            Sub("Monthly", 500m, "Paused"), Sub("Monthly", 500m, "Cancelled"));
    }

    // ── the report ───────────────────────────────────────────────────────────

    private static readonly DateTime From = Zurich(2025, 1, 1, 0);
    private static readonly DateTime To = Zurich(2025, 4, 1, 0);
    private static string Range(string grain = "month") => $"/api/club-reports?from={Q(From)}&to={Q(To)}&grain={grain}";

    private static decimal[] Column(JsonElement flow, string field) =>
        flow.GetProperty("byPeriod").EnumerateArray().Select(p => p.GetProperty(field).GetDecimal()).ToArray();

    [Fact]
    public async Task CashRevenueIsTheMoneyReceivedWhenItCame_AndAccrualIsWhatWasCharged_WithAVoidInItsOwnMonth()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        await Seed(factory, db => SeedRevenue(db, club.Org));

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        report.GetProperty("currency").GetString().Should().Be("CHF");
        report.GetProperty("periods").EnumerateArray().Select(p => p.GetProperty("start").GetString())
            .Should().Equal("2025-01-01", "2025-02-01", "2025-03-01");

        var cash = report.GetProperty("cash").EnumerateArray().Single();
        cash.GetProperty("currency").GetString().Should().Be("CHF");
        Column(cash, "in").Should().Equal(new[] { 100m, 180m, 100m }, "each instalment counts in the month it came; the bounced cheque came in February");
        Column(cash, "out").Should().Equal(new[] { 0m, 80m, 20m }, "the cheque taken back, then the refund; a credit brought over and taken back is no cash");
        cash.GetProperty("net").GetDecimal().Should().Be(280m);

        var accrual = report.GetProperty("accrual").EnumerateArray().Single();
        Column(accrual, "in").Should().Equal(new[] { 400m, 0m, 0m }, "dues and the bar bill are January's sales; an opening balance or a correction is no sale");
        Column(accrual, "out").Should().Equal(new[] { 0m, 100m, 0m }, "the bar bill voided in February takes it back in February");
        accrual.GetProperty("net").GetDecimal().Should().Be(300m);

        var kinds = report.GetProperty("accrualByKind").EnumerateArray().ToDictionary(k => k.GetProperty("kind").GetString()!, k => k);
        kinds["Dues"].GetProperty("net").GetDecimal().Should().Be(300m);
        kinds["Bar"].GetProperty("charged").GetDecimal().Should().Be(100m);
        kinds["Bar"].GetProperty("voided").GetDecimal().Should().Be(100m);
        kinds["Bar"].GetProperty("net").GetDecimal().Should().Be(0m);
        kinds.Should().NotContainKey("Other");
    }

    [Fact]
    public async Task ReceivablesAreAgedByDaysPastDue_AsTheBooksStoodAtTheEndOfThePeriod()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        await Seed(factory, db => SeedReceivables(db, club.Org));

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        report.GetProperty("asOfUtc").GetDateTime().Should().Be(To);
        var ageing = report.GetProperty("receivables").EnumerateArray().Single();
        ageing.GetProperty("current").GetDecimal().Should().Be(70m, "the spa charge is not due until 8 April");
        ageing.GetProperty("days1To30").GetDecimal().Should().Be(85m, "dinner a week late, and the shop charge voided only after the period");
        ageing.GetProperty("days31To60").GetDecimal().Should().Be(40m, "the correction by hand, owed from the day it was posted");
        ageing.GetProperty("days61To90").GetDecimal().Should().Be(60m, "what is left of the evening after the 30 paid");
        ageing.GetProperty("over90").GetDecimal().Should().Be(500m, "the balance brought over in December");
        ageing.GetProperty("total").GetDecimal().Should().Be(755m);
        ageing.GetProperty("openCharges").GetInt32().Should().Be(6, "the dues paid in full and the bar bill voided before are owed by nobody");
    }

    [Fact]
    public async Task GiftCardsPointsAndSubscriptions_StandAsTheyStood_AndPointsAreWorthTheirRedemptionRate()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        await Seed(factory, db => SeedStanding(db, club.Org));

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        var cards = report.GetProperty("giftCards").EnumerateArray().Single();
        cards.GetProperty("outstanding").GetDecimal().Should().Be(70m, "100 issued, 30 spent by then; the expired card owes nothing");
        cards.GetProperty("cards").GetInt32().Should().Be(1);

        var points = report.GetProperty("points");
        points.GetProperty("points").GetInt32().Should().Be(500, "a member taken below zero does not cancel another's points");
        points.GetProperty("members").GetInt32().Should().Be(1);
        points.GetProperty("pointsPerUnit").GetInt32().Should().Be(100);
        points.GetProperty("value").GetDecimal().Should().Be(5.00m);
        points.GetProperty("currency").GetString().Should().Be("CHF");

        var mrr = report.GetProperty("mrr").EnumerateArray().Single();
        mrr.GetProperty("monthly").GetDecimal().Should().Be(419m, "120 a month, 1200 a year, 300 a quarter and 99 past due; paused and cancelled bring nothing");
        mrr.GetProperty("yearly").GetDecimal().Should().Be(5028m);
        mrr.GetProperty("subscriptions").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task EventsShowHowFullTheyWere_AndHowManyWithASeatDidNotCome()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        await Seed(factory, db => SeedEvents(db, club.Org));

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        var events = report.GetProperty("events");
        events.GetProperty("events").GetInt32().Should().Be(2, "the draft and the cancelled event took place for nobody");
        events.GetProperty("limitedEvents").GetInt32().Should().Be(1);
        events.GetProperty("capacity").GetInt32().Should().Be(10);
        events.GetProperty("seatsTaken").GetInt32().Should().Be(7, "two reserved, three checked in, one not come and an unnamed seat; the cancelled and the waitlisted hold none");
        events.GetProperty("fillRate").GetDecimal().Should().Be(0.7m);
        events.GetProperty("ended").GetInt32().Should().Be(2);
        events.GetProperty("checkedIn").GetInt32().Should().Be(4);
        events.GetProperty("noShows").GetInt32().Should().Be(1);
        events.GetProperty("notMarked").GetInt32().Should().Be(2);
        events.GetProperty("noShowRate").GetDecimal().Should().Be(0.2m);
    }

    internal static void SeedEvents(AppDbContext db, Guid org)
    {
        ClubEvent Event(string status, int capacity, int unnamed = 0) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = org, Name = $"{status} evening", Status = status, Capacity = capacity, UnnamedSeats = unnamed,
            StartAtUtc = Zurich(2025, 2, 14, 19), EndAtUtc = Zurich(2025, 2, 14, 23), CreatedAtUtc = Zurich(2025, 1, 1), UpdatedAtUtc = Zurich(2025, 1, 1),
        };
        var gala = Event(ClubEventStatuses.Completed, 10, unnamed: 1);
        var open = Event(ClubEventStatuses.Published, 0);
        var draft = Event(ClubEventStatuses.Draft, 20);
        var called = Event(ClubEventStatuses.Cancelled, 20);
        db.ClubEvents.AddRange(gala, open, draft, called);
        ClubEventRegistration Seat(ClubEvent e, string status) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = org, EventId = e.Id, Name = "Guest", Status = status,
            RegisteredAtUtc = Zurich(2025, 1, 20), CreatedAtUtc = Zurich(2025, 1, 20), UpdatedAtUtc = Zurich(2025, 1, 20),
        };
        db.ClubEventRegistrations.AddRange(
            Seat(gala, RegistrationStatuses.Reserved), Seat(gala, RegistrationStatuses.Reserved),
            Seat(gala, RegistrationStatuses.CheckedIn), Seat(gala, RegistrationStatuses.CheckedIn), Seat(gala, RegistrationStatuses.CheckedIn),
            Seat(gala, RegistrationStatuses.NoShow), Seat(gala, RegistrationStatuses.Cancelled), Seat(gala, RegistrationStatuses.Waitlisted),
            Seat(open, RegistrationStatuses.CheckedIn),
            Seat(draft, RegistrationStatuses.Reserved), Seat(called, RegistrationStatuses.NoShow));
    }

    [Fact]
    public async Task MembersVisitsAndTopSpenders_AreCountedOnTheServer()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        Guid anna = Guid.NewGuid(), bruno = Guid.NewGuid(), spouse = Guid.NewGuid();
        await Seed(factory, db =>
        {
            ClubMember Member(string first, string tier, string status, DateTime joined) => new()
            {
                Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = first, LastName = "Keller",
                Email = $"{first.ToLowerInvariant()}@example.ch", Tier = tier, Status = status, JoinedAtUtc = joined,
            };
            db.ClubMembers.AddRange(
                Member("Anna", "Gold", "Active", Zurich(2023, 5, 1)), Member("Bea", "Gold", "Active", Zurich(2025, 2, 10)),
                Member("Carl", "Silver", "Active", Zurich(2024, 5, 1)), Member("Dora", "Gold", "Lapsed", Zurich(2025, 3, 3)));
            ClubVisit Visit(string venue, DateTime at) => new() { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = anna, MemberName = "Anna", Venue = venue, VisitedAtUtc = at };
            db.ClubVisits.AddRange(Visit("Bar", Zurich(2025, 1, 5)), Visit("Bar", Zurich(2025, 2, 5)), Visit("Spa", Zurich(2025, 3, 5)), Visit("Spa", Zurich(2024, 12, 5)));
            var bar = Charge(club.Org, bruno, "Bar", 100m, Zurich(2025, 1, 8));
            bar.MemberName = "Bruno Meier";
            var dues = Charge(club.Org, anna, "Dues", 300m, Zurich(2025, 1, 9));
            // The spouse's massage, billed to Anna's account because the household rolls up.
            var massage = Charge(club.Org, anna, "Spa", 150m, Zurich(2025, 2, 9));
            massage.IncurredByMemberId = spouse;
            massage.IncurredByName = "Sam Keller";
            var voided = Charge(club.Org, bruno, "Bar", 90m, Zurich(2025, 2, 1));
            voided.MemberName = "Bruno Meier";
            var voidOf = Charge(club.Org, bruno, "Bar", -90m, Zurich(2025, 2, 2), entryType: ClubEntryTypes.Void, reverses: voided.Id);
            voidOf.MemberName = "Bruno Meier";
            db.ClubCharges.AddRange(bar, dues, massage, voided, voidOf);
        });

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        var members = report.GetProperty("members");
        members.GetProperty("active").GetInt32().Should().Be(3);
        members.GetProperty("lapsed").GetInt32().Should().Be(1);
        members.GetProperty("activeByTier").EnumerateArray().Select(t => (t.GetProperty("name").GetString(), t.GetProperty("count").GetInt32()))
            .Should().Equal(("Gold", 2), ("Silver", 1));
        members.GetProperty("joined").GetInt32().Should().Be(2);
        members.GetProperty("joinedByPeriod").EnumerateArray().Select(n => n.GetInt32()).Should().Equal(0, 1, 1);

        var visits = report.GetProperty("visits");
        visits.GetProperty("checkIns").GetInt32().Should().Be(3, "December's is before the period");
        visits.GetProperty("byVenue").EnumerateArray().Select(v => (v.GetProperty("name").GetString(), v.GetProperty("count").GetInt32()))
            .Should().Equal(("Bar", 2), ("Spa", 1));

        report.GetProperty("topSpenders").EnumerateArray().Select(s => (s.GetProperty("memberName").GetString()!, s.GetProperty("amount").GetDecimal()))
            .Should().Equal(new[] { ("Anna Keller", 300m), ("Sam Keller", 150m), ("Bruno Meier", 100m) },
                "a rolled-up charge is the spend of who ran it up, and a voided bill is no spend");
    }

    [Fact]
    public async Task MoneyIsNeverAddedAcrossCurrencies_AndADayBeginsAtTheWorkspacesMidnight()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        var member = Guid.NewGuid();
        await Seed(factory, db => db.ClubPayments.AddRange(
            // 23:30 UTC on 31 January is half past midnight on 1 February in Zurich.
            Payment(club.Org, member, 100m, new DateTime(2025, 1, 31, 23, 30, 0, DateTimeKind.Utc)),
            Payment(club.Org, member, 40m, Zurich(2025, 1, 20), currency: "EUR"),
            Payment(club.Org, member, 60m, Zurich(2025, 1, 20))));

        var report = await Json(await club.Owner.GetAsync(Range()), "the report");
        var cash = report.GetProperty("cash").EnumerateArray().ToList();
        cash.Select(c => c.GetProperty("currency").GetString()).Should().Equal(new[] { "CHF", "EUR" }, "the workspace's own currency comes first");
        Column(cash[0], "in").Should().Equal(new[] { 60m, 100m, 0m }, "the payment just after midnight in Zurich is February's");
        Column(cash[1], "in").Should().Equal(new[] { 40m, 0m, 0m });

        var byDay = await Json(await club.Owner.GetAsync($"/api/club-reports?from={Q(Zurich(2025, 1, 30, 0))}&to={Q(Zurich(2025, 2, 2, 0))}"), "three days");
        byDay.GetProperty("grain").GetString().Should().Be("day", "a short period goes day by day");
        Column(byDay.GetProperty("cash").EnumerateArray().Single(c => c.GetProperty("currency").GetString() == "CHF"), "in")
            .Should().Equal(new[] { 0m, 0m, 100m });
    }

    [Fact]
    public async Task APeriodThatEndsBeforeItStartsOrRunsTooLongIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);

        (await club.Owner.GetAsync($"/api/club-reports?from={Q(To)}&to={Q(From)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await club.Owner.GetAsync($"/api/club-reports?from={Q(From.AddYears(-4))}&to={Q(To)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await club.Owner.GetAsync($"/api/club-reports?from={Q(From.AddYears(-2))}&to={Q(To)}&grain=day")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await club.Owner.GetAsync($"/api/club-reports?from={Q(From)}&to={Q(To)}&grain=hour")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var standing = await Json(await club.Owner.GetAsync("/api/club-reports"), "left out: the last twelve months");
        standing.GetProperty("grain").GetString().Should().Be("month");
        standing.GetProperty("periods").GetArrayLength().Should().Be(12);
    }

    [Fact]
    public async Task AnotherWorkspaceSeesNoneOfIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        await Seed(factory, db =>
        {
            SeedRevenue(db, club.Org);
            SeedReceivables(db, club.Org);
            SeedStanding(db, club.Org);
            SeedEvents(db, club.Org);
        });
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@elsewhere.ch");
        var theirs = await CreateAsync(factory, strangerId, "Elsewhere");
        stranger.In(theirs);

        var report = await Json(await stranger.GetAsync(Range()), "their report");
        report.GetProperty("cash").GetArrayLength().Should().Be(0);
        report.GetProperty("accrual").GetArrayLength().Should().Be(0);
        report.GetProperty("receivables").GetArrayLength().Should().Be(0);
        report.GetProperty("giftCards").GetArrayLength().Should().Be(0);
        report.GetProperty("mrr").GetArrayLength().Should().Be(0);
        report.GetProperty("points").GetProperty("points").GetInt32().Should().Be(0);
        report.GetProperty("events").GetProperty("events").GetInt32().Should().Be(0);

        // Naming the other workspace in the header gets nobody into it.
        stranger.In(club.Org);
        (await stranger.GetAsync(Range())).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheSpreadsheetKeepsFormulasAsText_AndTakesTheExportPermission()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        var member = Guid.NewGuid();
        await Seed(factory, db =>
        {
            SeedRevenue(db, club.Org);
            db.ClubCharges.Add(Charge(club.Org, member, "=SUM(A1)", 10m, Zurich(2025, 2, 10)));
        });

        var res = await club.Owner.GetAsync(Range().Replace("/api/club-reports?", "/api/club-reports/export?"));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        // Named by its days on the workspace's calendar: January to March in Zurich, whose
        // midnight on 1 January is still 31 December in UTC.
        res.Content.Headers.ContentDisposition!.FileName!.Trim('"').Should().Be("club-report-2025-01-01-to-2025-03-31.csv");
        var text = await res.Content.ReadAsStringAsync();
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].TrimStart('﻿').Should().Be("Figure,Period,Currency,Detail,Amount,Count");
        lines.Should().Contain("Cash received,2025-02-01,CHF,,180.00,");
        lines.Should().Contain("Accrual revenue by kind,2025-01-01 to 2025-03-31,CHF,'=SUM(A1),10.00,", "a kind that reads as a formula is kept as text");
        lines.Should().Contain(l => l.StartsWith("Accrual revenue by kind,") && l.Contains(",Bar,0.00,"));

        var (desk, deskId) = await SignInAsync(factory, "desk@pavillon46.ch");
        await AddMemberAsync(factory, club.Org, deskId, OrgMemberRole.Member);
        desk.In(club.Org);
        await Json(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{deskId}/permissions", Body(new { preset = "front_desk" })), "make them the front desk");
        await Json(await desk.GetAsync(Range()), "the front desk reads the report, as it reads the money pages");
        (await desk.GetAsync(Range().Replace("/api/club-reports?", "/api/club-reports/export?"))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "taking the figures away is exporting");
    }

    // ── today ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TodayAtTheClubIsCountedOnTheServer_OnTheWorkspacesCalendar()
    {
        using var factory = new CustomWebApplicationFactory();
        var club = await ClubAsync(factory);
        var now = DateTime.UtcNow;
        var today = RegionalSettings.TodayIn(ZurichZone, now);
        var dayStart = RegionalSettings.StartOfDayUtc(today, ZurichZone);
        var member = Guid.NewGuid();
        await Seed(factory, db =>
        {
            db.ClubVisits.AddRange(
                new ClubVisit { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = member, MemberName = "Anna", Venue = "Bar", VisitedAtUtc = dayStart.AddMinutes(5) },
                new ClubVisit { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = member, MemberName = "Anna", Venue = "Bar", VisitedAtUtc = dayStart.AddMinutes(-5) });
            db.ClubReservations.AddRange(
                new ClubReservation { Id = Guid.NewGuid(), OrganizationId = club.Org, ResourceId = Guid.NewGuid(), ResourceName = "Tennis court", MemberName = "Anna Keller", PartySize = 2, StartAtUtc = dayStart.AddHours(10), EndAtUtc = dayStart.AddHours(11), Status = ReservationStatuses.Confirmed, CreatedAtUtc = now, UpdatedAtUtc = now },
                new ClubReservation { Id = Guid.NewGuid(), OrganizationId = club.Org, ResourceId = Guid.NewGuid(), ResourceName = "Spa", MemberName = "Bruno", PartySize = 1, StartAtUtc = dayStart.AddHours(9), EndAtUtc = dayStart.AddHours(10), Status = ReservationStatuses.Cancelled, CreatedAtUtc = now, UpdatedAtUtc = now });
            var tab = new ClubTab { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = member, MemberName = "Anna Keller", Venue = "Bar", Status = "Open", OpenedAtUtc = now.AddHours(-1), Total = 42.5m, Currency = "CHF", CreatedAtUtc = now };
            db.ClubTabs.Add(tab);
            db.ClubTabItems.AddRange(
                new ClubTabItem { Id = Guid.NewGuid(), OrganizationId = club.Org, TabId = tab.Id, Name = "Negroni", Quantity = 2, UnitPrice = 16m, AddedAtUtc = now },
                new ClubTabItem { Id = Guid.NewGuid(), OrganizationId = club.Org, TabId = tab.Id, Name = "Water", Quantity = 1, UnitPrice = 4m, AddedAtUtc = now, RemovedAtUtc = now });
            db.ClubApplications.AddRange(
                new ClubApplication { Id = Guid.NewGuid(), OrganizationId = club.Org, FirstName = "Theo", LastName = "Kim", Email = "theo@example.ch", RequestedTier = "Gold", Status = ApplicationStatuses.Submitted, SubmittedAtUtc = now },
                new ClubApplication { Id = Guid.NewGuid(), OrganizationId = club.Org, FirstName = "Ida", LastName = "Roth", Email = "ida@example.ch", RequestedTier = "Gold", Status = ApplicationStatuses.Approved, SubmittedAtUtc = now });
            db.ClubCharges.AddRange(
                Charge(club.Org, member, "Dues", 299m, now.AddDays(-40), now.AddDays(-26)),
                Charge(club.Org, member, "Spa", 80m, now.AddDays(-1), now.AddDays(13)));
            db.ClubSubscriptions.Add(new ClubSubscription { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = member, MemberName = "Anna Keller", Name = "Gold dues", Amount = 299m, Currency = "CHF", Cadence = "Monthly", Status = "PastDue", StartedAtUtc = now.AddYears(-1), CreatedAtUtc = now });
            var gala = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = club.Org, Name = "Autumn gala", Status = ClubEventStatuses.Published, Capacity = 40, UnnamedSeats = 2, StartAtUtc = now.AddDays(3), EndAtUtc = now.AddDays(3).AddHours(4), CreatedAtUtc = now, UpdatedAtUtc = now };
            db.ClubEvents.AddRange(gala,
                new ClubEvent { Id = Guid.NewGuid(), OrganizationId = club.Org, Name = "Next month", Status = ClubEventStatuses.Published, StartAtUtc = now.AddDays(30), EndAtUtc = now.AddDays(30).AddHours(2), CreatedAtUtc = now, UpdatedAtUtc = now });
            db.ClubEventRegistrations.AddRange(
                new ClubEventRegistration { Id = Guid.NewGuid(), OrganizationId = club.Org, EventId = gala.Id, Name = "Anna", Status = RegistrationStatuses.Reserved, RegisteredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now },
                new ClubEventRegistration { Id = Guid.NewGuid(), OrganizationId = club.Org, EventId = gala.Id, Name = "Bruno", Status = RegistrationStatuses.Waitlisted, RegisteredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now });
            var joined = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(3).AddYears(-5).ToDateTime(new TimeOnly(10, 0)), ZurichZone);
            db.ClubMembers.AddRange(
                new ClubMember { Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = "Clara", LastName = "Meier", Email = "clara@example.ch", Tier = "Gold", Status = "Active", JoinedAtUtc = joined },
                new ClubMember { Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = "Dora", LastName = "Lapsed", Email = "dora@example.ch", Tier = "Gold", Status = "Lapsed", JoinedAtUtc = joined },
                new ClubMember { Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = "Emil", LastName = "New", Email = "emil@example.ch", Tier = "Gold", Status = "Active", JoinedAtUtc = now.AddDays(-2) });
        });

        var day = await Json(await club.Owner.GetAsync("/api/club-reports/today"), "today");
        day.GetProperty("date").GetString().Should().Be(today.ToString("yyyy-MM-dd"));
        day.GetProperty("checkIns").GetInt32().Should().Be(1, "the one before the workspace's midnight was yesterday's");
        day.GetProperty("reservations").GetProperty("count").GetInt32().Should().Be(1, "a cancelled booking is not today's");
        day.GetProperty("reservations").GetProperty("items")[0].GetProperty("resourceName").GetString().Should().Be("Tennis court");
        var tabs = day.GetProperty("openTabs");
        tabs.GetProperty("count").GetInt32().Should().Be(1);
        tabs.GetProperty("totals")[0].GetProperty("amount").GetDecimal().Should().Be(42.5m);
        tabs.GetProperty("items")[0].GetProperty("items").GetInt32().Should().Be(1, "an item taken off the tab is not on it");
        day.GetProperty("applications").GetProperty("count").GetInt32().Should().Be(1);
        var pastDue = day.GetProperty("pastDue");
        pastDue.GetProperty("charges").GetInt32().Should().Be(1, "the spa charge is not due yet");
        pastDue.GetProperty("owed")[0].GetProperty("amount").GetDecimal().Should().Be(299m);
        pastDue.GetProperty("subscriptions").GetInt32().Should().Be(1);
        var events = day.GetProperty("eventsThisWeek");
        events.GetProperty("count").GetInt32().Should().Be(1);
        events.GetProperty("items")[0].GetProperty("registeredCount").GetInt32().Should().Be(3, "one reserved and two unnamed seats; the waitlist holds none");
        var anniversaries = day.GetProperty("anniversaries").EnumerateArray().ToList();
        anniversaries.Should().ContainSingle("a lapsed member's day and a new member's first weeks are not anniversaries");
        anniversaries[0].GetProperty("firstName").GetString().Should().Be("Clara");
        anniversaries[0].GetProperty("years").GetInt32().Should().Be(5);
        anniversaries[0].GetProperty("date").GetString().Should().Be(today.AddDays(3).ToString("yyyy-MM-dd"));
    }
}
