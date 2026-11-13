using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's figures, worked out on the server: money from the ledger, events from their
/// guest lists, the day from the members, visits, bookings and tabs.
/// </summary>
/// <remarks>
/// <para>
/// Club Reports and Today at the Club downloaded every member, charge, visit and loyalty
/// entry and added them up in the browser. Revenue was what fully paid charges came to, so a
/// member paying dues in three instalments showed nothing for two months and everything in
/// the third; amounts were labelled in dollars; and a request that failed left a dashboard
/// of zeros that read as a dead month.
/// </para>
/// <para>
/// Here every sum is the database's. Money is never added across currencies: each figure is
/// listed per currency, and the workspace's own (Organization.Currency, what house accounts
/// are kept in) comes first. Periods are the workspace's days, weeks (Monday to Sunday) and
/// months, in its time zone.
/// </para>
/// </remarks>
public interface IClubReports
{
    /// <summary>
    /// The figures for <see cref="ClubReportQuery.FromUtc"/> up to <see cref="ClubReportQuery.ToUtc"/>:
    /// cash and accrual revenue by period, event fill and no-shows; and as the books stood at the
    /// end of it (or now, for a period not over yet): receivables by age, what is left on gift
    /// cards, the loyalty points members hold, and what subscriptions bring in each month.
    /// </summary>
    Task<Result<ClubReportDto>> ReportAsync(Guid organizationId, ClubReportQuery query, CancellationToken ct = default);

    /// <summary>
    /// What the club's day holds, on the workspace's calendar: check-ins, today's bookings, open
    /// tabs, applications waiting for a decision, what is past due, this week's events and the
    /// anniversaries of the next fortnight.
    /// </summary>
    Task<Result<ClubTodayDto>> TodayAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>What a report covers. Left out: the last twelve months, by month.</summary>
/// <param name="FromUtc">The first instant (the browser sends its workspace's midnight).</param>
/// <param name="ToUtc">The instant after the last.</param>
/// <param name="Grain">day, week or month; left out, the one that suits the length of the period.</param>
public sealed record ClubReportQuery(DateTime? FromUtc, DateTime? ToUtc, string? Grain);

/// <summary>Every figure on Club Reports.</summary>
/// <param name="Currency">The workspace's currency: house accounts are kept in it, and loyalty points are worth it.</param>
/// <param name="TimeZone">The workspace's time zone: where its days, weeks and months begin.</param>
/// <param name="AsOfUtc">When the figures that stand at a moment (receivables, gift cards, points) are read: the end of the period, or now if that is later.</param>
/// <param name="Periods">The days, weeks or months of the period, in order; every list by period follows this order.</param>
/// <param name="Cash">Money received and paid back, per currency: cash revenue.</param>
/// <param name="Accrual">What was charged and voided, per currency: accrual revenue. Opening balances and corrections by hand are not sales and are left out.</param>
/// <param name="AccrualByKind">Accrual revenue per kind of charge (Dues, Dining, Bar, …), per currency.</param>
/// <param name="Receivables">What members owe, by how long it has been due, per currency.</param>
/// <param name="GiftCards">What is left on gift cards, per currency: the club's debt to their holders.</param>
/// <param name="Points">The loyalty points members hold, and what they are worth at the redemption rate.</param>
/// <param name="Mrr">What running subscriptions bring in each month, per currency.</param>
/// <param name="Events">How full the period's events were, and how many who had a seat did not come.</param>
/// <param name="TopSpenders">The members whose charges in the period came to most (accrual, voids taken off), per currency.</param>
/// <param name="Members">Who the club's members are now, and who joined in the period.</param>
/// <param name="Visits">Check-ins in the period, by venue.</param>
public sealed record ClubReportDto(
    string Currency,
    string TimeZone,
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime AsOfUtc,
    string Grain,
    IReadOnlyList<ClubReportPeriodDto> Periods,
    IReadOnlyList<ClubMoneyFlowDto> Cash,
    IReadOnlyList<ClubMoneyFlowDto> Accrual,
    IReadOnlyList<ClubKindRevenueDto> AccrualByKind,
    IReadOnlyList<ClubAgeingDto> Receivables,
    IReadOnlyList<ClubGiftCardsOutstandingDto> GiftCards,
    ClubPointsOutstandingDto Points,
    IReadOnlyList<ClubMrrDto> Mrr,
    ClubEventRatesDto Events,
    IReadOnlyList<ClubSpenderDto> TopSpenders,
    ClubMembershipFiguresDto Members,
    ClubVisitFiguresDto Visits);

/// <summary>A member and what their charges in the period came to.</summary>
public sealed record ClubSpenderDto(Guid MemberId, string MemberName, string Currency, decimal Amount);

/// <param name="Active">Members active now.</param>
/// <param name="Lapsed">Members lapsed now.</param>
/// <param name="ActiveByTier">Active members per tier, most first.</param>
/// <param name="Joined">Members who joined in the period.</param>
/// <param name="JoinedByPeriod">The same per period, in the order of the report's periods.</param>
public sealed record ClubMembershipFiguresDto(int Active, int Lapsed, IReadOnlyList<ClubCountDto> ActiveByTier, int Joined, IReadOnlyList<int> JoinedByPeriod);

/// <param name="CheckIns">Check-ins in the period.</param>
/// <param name="ByVenue">The same per venue, most first.</param>
public sealed record ClubVisitFiguresDto(int CheckIns, IReadOnlyList<ClubCountDto> ByVenue);

public sealed record ClubCountDto(string Name, int Count);

/// <summary>One day, week or month of a report.</summary>
/// <param name="Start">Its first day on the workspace's calendar, yyyy-MM-dd.</param>
/// <param name="StartUtc">When it begins.</param>
/// <param name="EndUtc">When the next one begins (or the report ends).</param>
public sealed record ClubReportPeriodDto(string Start, DateTime StartUtc, DateTime EndUtc);

/// <summary>Money in and back out in one currency, in all and per period.</summary>
/// <param name="In">Cash: received. Accrual: charged.</param>
/// <param name="Out">Cash: paid back (refunds, payments taken back). Accrual: voided. A positive amount.</param>
/// <param name="Net"><paramref name="In"/> less <paramref name="Out"/>.</param>
/// <param name="ByPeriod">The same per period, in the order of the report's periods.</param>
public sealed record ClubMoneyFlowDto(string Currency, decimal In, decimal Out, decimal Net, IReadOnlyList<ClubPeriodAmountDto> ByPeriod);

public sealed record ClubPeriodAmountDto(decimal In, decimal Out, decimal Net);

public sealed record ClubKindRevenueDto(string Currency, string Kind, decimal Charged, decimal Voided, decimal Net);

/// <summary>What members owe in one currency, by how many days past its due date it is (the day it was posted when it has none).</summary>
public sealed record ClubAgeingDto(
    string Currency,
    decimal Current,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total,
    int OpenCharges);

public sealed record ClubGiftCardsOutstandingDto(string Currency, decimal Outstanding, int Cards);

/// <param name="Points">Points members hold (a member below zero counts as none).</param>
/// <param name="Members">Members holding any.</param>
/// <param name="PointsPerUnit">Points that make one unit of <paramref name="Currency"/>, as the Loyalty page states.</param>
/// <param name="Value">What they are worth at that rate.</param>
public sealed record ClubPointsOutstandingDto(int Points, int Members, int PointsPerUnit, decimal Value, string Currency);

/// <param name="Monthly">Monthly recurring revenue: each running subscription's amount per month (a quarter's a third, a year's a twelfth).</param>
/// <param name="Yearly">Twelve times that.</param>
public sealed record ClubMrrDto(string Currency, decimal Monthly, decimal Yearly, int Subscriptions);

/// <param name="Events">Events in the period that took place or will (published or completed).</param>
/// <param name="LimitedEvents">Those with a limit on seats, which the fill rate is about.</param>
/// <param name="Capacity">Their seats.</param>
/// <param name="SeatsTaken">Seats taken on them: reserved, checked in or not come.</param>
/// <param name="FillRate"><paramref name="SeatsTaken"/> over <paramref name="Capacity"/>; none without a limited event.</param>
/// <param name="Ended">Events of the period that are over, which the no-show rate is about.</param>
/// <param name="CheckedIn">People checked in at them.</param>
/// <param name="NoShows">People marked as not come.</param>
/// <param name="NotMarked">People with a seat who were neither checked in nor marked not come.</param>
/// <param name="NoShowRate"><paramref name="NoShows"/> over those checked in and not come; none when nobody was either.</param>
public sealed record ClubEventRatesDto(
    int Events,
    int LimitedEvents,
    int Capacity,
    int SeatsTaken,
    decimal? FillRate,
    int Ended,
    int CheckedIn,
    int NoShows,
    int NotMarked,
    decimal? NoShowRate);

/// <summary>What Today at the Club shows.</summary>
/// <param name="Date">Today on the workspace's calendar, yyyy-MM-dd.</param>
public sealed record ClubTodayDto(
    string Date,
    string TimeZone,
    string Currency,
    int CheckIns,
    ClubTodayListDto<ClubTodayReservationDto> Reservations,
    ClubTodayTabsDto OpenTabs,
    ClubTodayListDto<ClubTodayApplicationDto> Applications,
    ClubTodayPastDueDto PastDue,
    ClubTodayListDto<ClubTodayEventDto> EventsThisWeek,
    IReadOnlyList<ClubTodayAnniversaryDto> Anniversaries);

/// <summary>How many there are, and the first few to show.</summary>
public sealed record ClubTodayListDto<T>(int Count, IReadOnlyList<T> Items);

public sealed record ClubTodayReservationDto(Guid Id, string MemberName, string ResourceName, int PartySize, DateTime StartAtUtc, string Status);

public sealed record ClubTodayTabsDto(int Count, IReadOnlyList<ClubCurrencyAmountDto> Totals, IReadOnlyList<ClubTodayTabDto> Items);

public sealed record ClubTodayTabDto(Guid Id, string MemberName, string Venue, int Items, decimal Total, string Currency);

public sealed record ClubCurrencyAmountDto(string Currency, decimal Amount);

public sealed record ClubTodayApplicationDto(Guid Id, string FirstName, string LastName, string RequestedTier, string? Occupation, string Status);

/// <param name="Charges">Charges past their due date with something still owed.</param>
/// <param name="Owed">What they still owe, per currency.</param>
/// <param name="Subscriptions">Subscriptions billing marked past due.</param>
public sealed record ClubTodayPastDueDto(
    int Charges,
    IReadOnlyList<ClubCurrencyAmountDto> Owed,
    IReadOnlyList<ClubTodayOverdueChargeDto> ChargeItems,
    int Subscriptions,
    IReadOnlyList<ClubTodaySubscriptionDto> SubscriptionItems);

public sealed record ClubTodayOverdueChargeDto(Guid Id, string MemberName, string Description, decimal Owed, string Currency, DateTime DueAtUtc);

public sealed record ClubTodaySubscriptionDto(Guid Id, string MemberName, string Name, decimal Amount, string Currency);

/// <param name="RegisteredCount">Seats taken: reserved and checked in, with the unnamed seats of old RSVPs.</param>
public sealed record ClubTodayEventDto(Guid Id, string Name, string CoverEmoji, DateTime StartAtUtc, int Capacity, int RegisteredCount);

/// <param name="Date">The anniversary on the workspace's calendar, yyyy-MM-dd.</param>
public sealed record ClubTodayAnniversaryDto(Guid MemberId, string FirstName, string LastName, string Tier, string Date, int Years);
