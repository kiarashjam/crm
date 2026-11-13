using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's POS, connected: Lightspeed Restaurant K-Series, whose closed tickets are read
/// from its Financial API, imported once each, and posted to the members they belong to as
/// sales through the ledger (<see cref="IClubLedger.PostSaleAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// Why Lightspeed K-Series and not SumUp: a closed K-Series sale carries its lines (with the
/// tax rate and amount of each), its payments by payment method, the staff member, the table
/// and the customer on it, and a club can give it a payment method that puts the bill on the
/// house account. SumUp's public API reports card and cash payments with their products, but no
/// table, no customer for a payment taken in person, and no way of leaving a bill on an
/// account; its webhooks only cover checkouts started through its own API. K-Series began as
/// iKentoo, in Geneva, and is common in Swiss restaurants and clubs.
/// </para>
/// <para>
/// K-Series has no webhook that reports every closed ticket (the notifications it documents
/// belong to its ordering and reservation-platform APIs, for the orders placed through them), so
/// tickets come in by polling, which claims the workspace for one import at a time (a lease on
/// the settings row, taken by one conditional update), so two servers or a retry never import
/// at once. Each import reads a day at a time from a few hours before where the last one had
/// read in full, as a till that was offline sends its tickets late; the POS's id for a ticket
/// is unique in the workspace, so what was already read adds nothing.
/// </para>
/// <para>
/// A ticket is a member's when the POS recorded one of their codes (a member number, or the
/// code on their card or its QR code; <see cref="MemberCodesAsync"/>), when its customer is one
/// staff already matched to them, or by the customer's email when exactly one member has it.
/// Put on the house account, it is posted as owing; paid at the POS, it is recorded as paid;
/// either way it earns the points the club's settings give. A ticket that needs a member and
/// names none waits for staff (<see cref="TicketsAsync"/>, <see cref="MatchAsync"/>); a ticket
/// paid at the POS with nobody on it is not a member's, and is skipped.
/// </para>
/// </remarks>
public interface IClubPos
{
    /// <summary>Whether a POS is connected and importing, for every member of the workspace: the house-charge screen says so.</summary>
    Task<PosStatusDto> StatusAsync(Guid organizationId, CancellationToken ct = default);

    Task<PosSettingsDto> SettingsAsync(Guid organizationId, CancellationToken ct = default);

    Task<Result<PosSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, PosSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>The address to send the browser to, to let Cadence read the club's Lightspeed account.</summary>
    Task<Result<string>> ConnectUrlAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lightspeed sends the browser back to the POS tickets page with a code and the state it was
    /// given, and the page, signed in, passes both on: the code is exchanged for the account's
    /// tokens, kept encrypted. Only the person who started connecting finishes it, in the
    /// workspace they started it in.
    /// </summary>
    /// <remarks>
    /// The state alone used to be enough, sent to an anonymous callback. Anyone could then start
    /// connecting their own workspace and have someone else finish it on Lightspeed's page (a
    /// link is all it takes): that person's restaurant account, its sales and its customers,
    /// would have been read into the sender's workspace.
    /// </remarks>
    Task<Result<PosSettingsDto>> CompleteConnectAsync(Guid organizationId, Guid userId, string? state, string? code, CancellationToken ct = default);

    /// <summary>Forgets the tokens and stops importing. Tickets already imported stay.</summary>
    Task<Result<PosSettingsDto>> DisconnectAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>The business locations the connected account has, for choosing whose tickets to import.</summary>
    Task<Result<IReadOnlyList<PosLocationDto>>> LocationsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Imports now what the POS closed since the last import, and posts what it can. Refused (409) while another import runs.</summary>
    Task<Result<PosImportDto>> ImportNowAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The scheduled import, in every workspace that has turned it on and has its club on.
    /// Returns how many tickets came in.
    /// </summary>
    Task<int> ImportDueAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Tickets, newest first: "attention" (the default: unmatched and failed), one status, or
    /// "all".
    /// </summary>
    Task<IReadOnlyList<PosTicketDto>> TicketsAsync(Guid organizationId, string? status, int take = 100, CancellationToken ct = default);

    /// <summary>
    /// Says whose a ticket is and posts it to them. With <see cref="PosMatchRequest.Remember"/>,
    /// the POS's customer on it is remembered as theirs, so their next tickets find them.
    /// </summary>
    Task<Result<PosTicketDto>> MatchAsync(Guid organizationId, Guid userId, Guid ticketId, PosMatchRequest request, CancellationToken ct = default);

    /// <summary>Tries a ticket again: its member found again from the codes as they are now, and posted.</summary>
    Task<Result<PosTicketDto>> RetryAsync(Guid organizationId, Guid ticketId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Sets a ticket aside as not a member's, with why.</summary>
    Task<Result<PosTicketDto>> IgnoreAsync(Guid organizationId, Guid userId, Guid ticketId, string? reason, Guid? expectedVersion, CancellationToken ct = default);

    // ── members' codes ───────────────────────────────────────────────────────

    Task<Result<IReadOnlyList<MemberCodeDto>>> MemberCodesAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Gives the member a code: the one sent (their member number, a card's code) or, with none, a new one.</summary>
    Task<Result<MemberCodeDto>> AddMemberCodeAsync(Guid organizationId, Guid memberId, MemberCodeRequest request, CancellationToken ct = default);

    Task<Result> RemoveMemberCodeAsync(Guid organizationId, Guid memberId, Guid codeId, CancellationToken ct = default);
}

/// <summary>
/// A POS whose closed tickets the club imports. The account's own tokens are passed with each
/// call, as every workspace connects its own account.
/// </summary>
public interface IPosProvider
{
    /// <summary>lightspeed-k.</summary>
    string Name { get; }

    /// <summary>Whether this server has the client id and secret the POS gave Cadence.</summary>
    bool IsConfigured { get; }

    /// <summary>Where to send the browser to let Cadence read the account; it comes back to <paramref name="redirectUri"/> with a code.</summary>
    string AuthorizeUrl(string state, string redirectUri);

    Task<ProviderResult<PosTokens>> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default);

    Task<ProviderResult<PosTokens>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task<ProviderResult<IReadOnlyList<PosLocationDto>>> LocationsAsync(string accessToken, CancellationToken ct = default);

    /// <summary>A page of the tickets closed at a location from <paramref name="fromUtc"/> to <paramref name="toUtc"/>.</summary>
    Task<ProviderResult<PosTicketPage>> ClosedTicketsAsync(string accessToken, string locationId, DateTime fromUtc, DateTime toUtc, string? pageToken, CancellationToken ct = default);
}

/// <summary>An account's tokens: a refresh token comes back only when the POS gives a new one.</summary>
public sealed record PosTokens(string AccessToken, string? RefreshToken, DateTime ExpiresAtUtc);

public sealed record PosTicketPage(IReadOnlyList<PosTicketData> Tickets, string? NextPageToken);

/// <summary>A line of a ticket as the POS has it: VAT included, its discount already taken off <see cref="Total"/>.</summary>
public sealed record PosTicketLine(string Name, string? Sku, decimal Quantity, decimal Total, decimal? VatRate, decimal Discount, string? DiscountName);

/// <summary>A payment on a ticket: the POS's method (code and name), the amount it paid of the ticket, and any tip on top.</summary>
public sealed record PosTicketPayment(string? Code, string? Name, decimal Amount, decimal Tip);

/// <summary>A closed ticket, as read from the POS.</summary>
/// <param name="ExternalId">The POS's id for it, with its location: unique in the account.</param>
/// <param name="CustomerCodes">What the POS recorded that could be a member's code: the customer's card or reference numbers, the name given to the bill.</param>
public sealed record PosTicketData(
    string ExternalId,
    string? ReceiptNumber,
    string? LocationId,
    DateTime ClosedAtUtc,
    string? Currency,
    bool Cancelled,
    IReadOnlyList<PosTicketLine> Lines,
    IReadOnlyList<PosTicketPayment> Payments,
    string? StaffName,
    string? TableName,
    string? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    IReadOnlyList<string> CustomerCodes);

public sealed record PosLocationDto(string Id, string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Currency);

/// <summary>What every member of the workspace may know of the POS: whether one is connected and importing, and how many tickets wait.</summary>
public sealed record PosStatusDto(
    string Provider,
    bool ServerConfigured,
    bool Connected,
    bool Importing,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LocationName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? LastImportAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastImportError,
    int NeedsAttention);

/// <summary>The POS settings as a screen sees them: never a token, only whether the account is connected.</summary>
public sealed record PosSettingsDto
{
    public string Provider { get; init; } = string.Empty;

    /// <summary>Whether this server has Cadence's Lightspeed client id and secret: without them, nobody can connect.</summary>
    public bool ServerConfigured { get; init; }

    public bool Connected { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ConnectedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ConnectedByName { get; init; }

    /// <summary>The saved tokens can no longer be read on this server: connect again.</summary>
    public bool TokenUnreadable { get; init; }

    public bool Enabled { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LocationId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LocationName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ImportFromUtc { get; init; }

    /// <summary>The POS's payment methods by code or name, and what each means: HouseAccount, Card, Cash, Twint or Skip.</summary>
    public IReadOnlyDictionary<string, string> MethodMap { get; init; } = new Dictionary<string, string>();

    public string DefaultVenue { get; init; } = "Dining";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastImportAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LastImportError { get; init; }
    public int LastImportCount { get; init; }

    /// <summary>Where Lightspeed sends the browser back after connecting (the POS tickets page): it is registered with Lightspeed for Cadence's client.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? UpdatedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? UpdatedByName { get; init; }
    public Guid Version { get; init; }
}

/// <summary>A change to the POS settings: a field left out is kept.</summary>
public sealed class PosSettingsRequest
{
    public bool? Enabled { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? ImportFromUtc { get; set; }
    public Dictionary<string, string>? MethodMap { get; set; }
    public string? DefaultVenue { get; set; }
    public Guid? Version { get; set; }
}

public sealed record PosImportDto(int Read, int Imported, int Posted, int Unmatched, int Failed, int Skipped, DateTime FinishedAtUtc);

public sealed record PosTicketLineDto(string Name, decimal Quantity, decimal Total, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? VatRate,
    decimal Discount, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DiscountName);

public sealed record PosTicketPaymentDto(string Method, string Means, decimal Amount, decimal Tip);

public sealed record PosTicketDto
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ReceiptNumber { get; init; }
    public DateTime ClosedAtUtc { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public decimal OnAccountAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal TipAmount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Tenders { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StaffName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TableName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CustomerName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CustomerReference { get; init; }
    public bool HasCustomer { get; init; }
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Note { get; init; }
    public int Attempts { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MatchedBy { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? SaleId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PointsAwarded { get; init; }
    public DateTime ImportedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? PostedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ResolvedByName { get; init; }
    public IReadOnlyList<PosTicketLineDto> Lines { get; init; } = Array.Empty<PosTicketLineDto>();
    public IReadOnlyList<PosTicketPaymentDto> Payments { get; init; } = Array.Empty<PosTicketPaymentDto>();
    public Guid Version { get; init; }
}

/// <summary>What Lightspeed sent the browser back with, passed on by the POS tickets page.</summary>
public sealed class PosConnectRequest
{
    public string? Code { get; set; }
    public string? State { get; set; }
}

public sealed class PosMatchRequest
{
    public Guid? MemberId { get; set; }

    /// <summary>Remember the POS's customer on the ticket as this member's, so their next tickets find them.</summary>
    public bool Remember { get; set; } = true;

    public Guid? Version { get; set; }
}

public sealed class PosIgnoreRequest
{
    public string? Reason { get; set; }
    public Guid? Version { get; set; }
}

public sealed record MemberCodeDto(Guid Id, Guid MemberId, string Kind, string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label,
    DateTime CreatedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CreatedByName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? QrRows);

public sealed class MemberCodeRequest
{
    /// <summary>The member number or card code as the POS records it; empty for Cadence to make one.</summary>
    public string? Code { get; set; }

    public string? Label { get; set; }
}
