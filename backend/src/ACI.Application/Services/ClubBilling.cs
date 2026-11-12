using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's billing and receivables: bills subscriptions once per period, sets dues past
/// due, reminds members of what is overdue, lapses a member who has not paid after the
/// grace period (and reinstates them once nothing is overdue), and sends statements as PDF,
/// on request and once a month. Every run is recorded for the club to see.
/// </summary>
/// <remarks>
/// <para>
/// The daily run (ClubBillingBackgroundService) does for each workspace what its settings
/// switch on (<see cref="BillingSettings"/>), once per day in the workspace's time zone. It
/// acts for one workspace at a time: everything it reads and writes is that workspace's.
/// </para>
/// <para>
/// Everything it does once is guarded by a key the database will not take twice: a charge
/// per subscription and period (on the ledger), a reminder per charge and step, a monthly
/// statement per member and month, a daily run per workspace and day. So a run repeated,
/// retried after a crash, or started on two servers at once never bills, reminds or sends
/// anything a second time.
/// </para>
/// </remarks>
public interface IClubBilling
{
    /// <summary>
    /// Bills now every period that has started, for every running subscription: what the
    /// daily run does when automatic billing is on, started by a person. Recorded as a run.
    /// </summary>
    Task<Result<BillingRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>What billing now would post, without posting anything.</summary>
    Task<BillingPreviewDto> PreviewAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>The workspace's latest runs, newest first.</summary>
    Task<IReadOnlyList<BillingRunDto>> RunsAsync(Guid organizationId, int take = 20, CancellationToken ct = default);

    /// <summary>
    /// The daily run, for every workspace whose settings switch something on and whose day
    /// has not been run yet. Returns how many workspaces it ran for.
    /// </summary>
    Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>A member's statement for [fromUtc, toUtc) as a PDF, built from the ledger.</summary>
    Task<Result<StatementFile>> StatementPdfAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// An invoice for one charge as a PDF: what it was for, its VAT, what has been paid and what
    /// is owed, with a Swiss QR-bill for that amount whose reference pays this charge first. It
    /// goes to the account the charge is billed to, and a household member's bill rolled up to
    /// the primary says whose it was.
    /// </summary>
    Task<Result<InvoiceFile>> ChargeInvoicePdfAsync(Guid organizationId, Guid chargeId, CancellationToken ct = default);

    /// <summary>Emails the member their statement for [fromUtc, toUtc), the PDF attached, through the outbox.</summary>
    Task<Result<StatementDeliveryDto>> EmailStatementAsync(Guid organizationId, Guid userId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Puts back a member the billing lapsed, once nothing of theirs is overdue: called after
    /// a payment so they are reinstated at once rather than by the next daily run. Does
    /// nothing for a member the billing did not lapse.
    /// </summary>
    Task ReinstateIfSettledAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// A household's statement for [fromUtc, toUtc) as a PDF: the primary's account, where the
    /// bills of the members who roll up land, each line saying whose it was; then what each
    /// person in the household ran up in the period, and what anyone billed on their own owes.
    /// When something is due and the club's bank details are set, it carries the QR-bill of the
    /// primary's own statement for the period, with the same reference.
    /// </summary>
    Task<Result<StatementFile>> HouseholdStatementPdfAsync(Guid organizationId, Guid primaryMemberId, string householdName,
        IReadOnlyList<HouseholdStatementMember> others, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

/// <summary>A member of a household other than its primary, for the household statement.</summary>
/// <param name="RollsUp">Whether their bills go on the primary's account now.</param>
public sealed record HouseholdStatementMember(Guid MemberId, string Name, string Role, bool RollsUp);

/// <summary>A statement as a file to download or attach.</summary>
public sealed record StatementFile(string FileName, byte[] Content, AccountStatementDto Statement, decimal AmountDue);

/// <summary>An invoice for one charge as a file to download.</summary>
public sealed record InvoiceFile(string FileName, byte[] Content, decimal AmountDue);

public sealed record BillingRunFailureDto(string What, Guid? Id, string Name, string Error);

public sealed record BillingRunDto
{
    public Guid Id { get; init; }
    public string Trigger { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateOnly? ScheduledFor { get; init; }

    /// <summary>running, succeeded, partly_failed, failed; or stopped, for one still marked running long after it began.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime StartedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? FinishedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StartedByName { get; init; }
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    public int SubscriptionsBilled { get; init; }
    public int ChargesPosted { get; init; }
    public decimal AmountBilled { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int OverdueCharges { get; init; }
    public int SubscriptionsPastDue { get; init; }
    public int RemindersQueued { get; init; }
    public int MembersLapsed { get; init; }
    public int MembersReinstated { get; init; }
    public int StatementsQueued { get; init; }
    public IReadOnlyList<BillingRunFailureDto> Failures { get; init; } = Array.Empty<BillingRunFailureDto>();
}

/// <summary>One subscription with periods to bill now.</summary>
public sealed record BillingPreviewItemDto(
    Guid SubscriptionId, Guid MemberId, string MemberName, string Name, decimal Amount, string Currency,
    string Cadence, IReadOnlyList<DateTime> Periods);

public sealed record BillingPreviewDto(
    int Subscriptions, int Periods, IReadOnlyDictionary<string, decimal> AmountByCurrency, IReadOnlyList<BillingPreviewItemDto> Items);

public sealed record StatementDeliveryDto(
    Guid Id, Guid MemberId, DateTime PeriodStartUtc, DateTime PeriodEndUtc, string Status, string? Reason,
    decimal AmountDue, string Currency, Guid? OutboundMessageId);
