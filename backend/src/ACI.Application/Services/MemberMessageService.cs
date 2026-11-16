using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Where a message to a member stands, worked out from the row and the outbox message behind it.
/// </summary>
/// <remarks>
/// queued: waiting in the outbox. sent: the mail server took it. delivered: the mail service
/// reported it delivered. opened: reported opened (only where open tracking is on at the mail
/// service). bounced: refused by the receiving server or dropped by the mail service.
/// suppressed: not sent, because of consent, an unsubscribe, do not contact or the
/// suppression list. failed: the outbox gave up, or a campaign could not queue it after
/// several tries. cancelled: taken back before it went (its campaign was stopped, or the
/// member removed). skipped: not sent, for want of a usable address. logged: recorded by
/// staff, not sent by Cadence.
/// </remarks>
public static class MemberMessageStates
{
    public const string Queued = "queued";
    public const string Sent = "sent";
    public const string Delivered = "delivered";
    public const string Opened = "opened";
    public const string Bounced = "bounced";
    public const string Suppressed = "suppressed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Logged = "logged";
    public const string Cancelled = "cancelled";

    public static string Of(string rowStatus, string? outboundStatus, string? deliveryStatus, bool opened)
    {
        if (rowStatus != MemberMessageStatuses.Queued) return rowStatus;
        return outboundStatus switch
        {
            OutboundStatuses.Queued => Queued,
            OutboundStatuses.Suppressed => Suppressed,
            // Taken back on purpose (the campaign stopped, the member removed): not a failure.
            OutboundStatuses.Cancelled => Cancelled,
            OutboundStatuses.Sent => deliveryStatus is "bounced" or "dropped" or "blocked" ? Bounced
                : opened ? Opened
                : deliveryStatus is "delivered" or "spam_report" ? Delivered
                : Sent,
            // Given up on, taken back, or (with no outbox message at all) never going to go.
            _ => Failed,
        };
    }
}

/// <summary>One message to one member, as the communications log and a campaign's recipients show it.</summary>
/// <param name="Status">See <see cref="MemberMessageStates"/>.</param>
/// <param name="Reason">Why it was not sent or did not arrive, when it was not or did not.</param>
public sealed record MemberMessageDto(
    Guid Id, Guid MemberId, string MemberName, string? Email, Guid? CampaignId, string? CampaignName,
    string Channel, string Category, string? Purpose, string Subject, string? Preview,
    string Status, string? Reason, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime? OpenedAtUtc);

/// <summary>An email staff write to one member.</summary>
public sealed record SendMemberMessageRequest
{
    public Guid? MemberId { get; init; }
    public string? Category { get; init; }

    /// <summary>marketing or service, said every time: a service message goes whatever the member said about marketing.</summary>
    public string? Purpose { get; init; }

    public string? Subject { get; init; }
    public string? Body { get; init; }
}

/// <summary>A message that went out some other way (a call, an SMS from a phone, a letter), recorded for the team.</summary>
public sealed record LogMemberMessageRequest
{
    public Guid? MemberId { get; init; }
    public string? Channel { get; init; }
    public string? Category { get; init; }
    public string? Subject { get; init; }
    public string? Preview { get; init; }
    public string? CampaignName { get; init; }
}

public interface IMemberMessageService
{
    /// <summary>The newest messages, the club's or one member's.</summary>
    Task<IReadOnlyList<MemberMessageDto>> ListAsync(Guid organizationId, Guid? memberId, int take, CancellationToken ct = default);

    /// <summary>Emails one member through the outbox; a refusal (no consent, do not contact) is recorded and answered as suppressed.</summary>
    Task<Result<MemberMessageDto>> SendAsync(Guid organizationId, Guid userId, SendMemberMessageRequest request, CancellationToken ct = default);

    /// <summary>Records a message that went out some other way. Nothing is sent.</summary>
    Task<Result<MemberMessageDto>> LogAsync(Guid organizationId, Guid userId, LogMemberMessageRequest request, CancellationToken ct = default);

    /// <summary>Deletes a message staff logged; what Cadence sent is kept as the record of what went to whom.</summary>
    Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// The communications log: the messages each member was sent, by campaigns and by staff,
/// with what happened to each, and the messages staff record that went out another way.
/// </summary>
/// <remarks>
/// "Send message" used to write a log entry marked Sent and email nobody. An email to a
/// member now goes through the outbox, one message per member, and its status is the
/// outbox's, as the mail service reports it back.
/// </remarks>
public sealed class MemberMessageService : IMemberMessageService
{
    public const int MaxSubject = 300;
    public const int MaxBody = 20_000;

    public static readonly IReadOnlyList<string> Categories =
        ["Welcome", "Dues", "Renewal", "Event", "Reservation", "Loyalty", "Announcement", "Application", "Other"];

    private readonly ICampaignRepository _messages;
    private readonly IClubRepository _club;
    private readonly IOutbox _outbox;
    private readonly ILogger<MemberMessageService> _logger;

    public MemberMessageService(ICampaignRepository messages, IClubRepository club, IOutbox outbox, ILogger<MemberMessageService> logger)
    {
        _messages = messages;
        _club = club;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemberMessageDto>> ListAsync(Guid organizationId, Guid? memberId, int take, CancellationToken ct = default)
    {
        var rows = await _messages.MessagesAsync(organizationId, memberId, null, Math.Clamp(take, 1, 1000), ct);
        return await ToDtosAsync(_messages, rows, ct);
    }

    public async Task<Result<MemberMessageDto>> SendAsync(Guid organizationId, Guid userId, SendMemberMessageRequest request, CancellationToken ct = default)
    {
        var purpose = (request.Purpose ?? "").Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(purpose))
            return new Error("MemberMessage.PurposeRequired", "Say whether this is marketing or a service message: marketing goes only to members who agreed to it.");
        var category = Category(request.Category);
        if (category == null) return CategoryInvalid;
        var subject = OutboxService.OneLine(request.Subject);
        if (subject.Length == 0) return new Error("MemberMessage.NoSubject", "Add a subject.");
        if (subject.Length > MaxSubject) return new Error("MemberMessage.TooLong", $"The subject can be at most {MaxSubject} characters.");
        var body = (request.Body ?? "").Trim();
        if (body.Length == 0) return new Error("MemberMessage.NoBody", "Write the message.");
        if (body.Length > MaxBody) return new Error("MemberMessage.TooLong", $"The message can be at most {MaxBody:N0} characters.");
        if (request.MemberId is not Guid memberId) return DomainErrors.Club.MemberNotFound;
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var address = (member.Email ?? "").Trim();
        if (!ValidationHelper.IsValidEmail(address))
            return new Error("MemberMessage.NoAddress", "This member has no valid email address on their record.");

        var row = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Email = address,
            Channel = CampaignChannels.Email,
            Category = category,
            Purpose = purpose,
            Subject = subject,
            Preview = Preview(body),
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _messages.InTransactionAsync(async () =>
        {
            await QueueAsync(_messages, _outbox, row, new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.Member,
                To = [address],
                RecipientName = row.MemberName,
                Subject = subject,
                Body = body,
                CreatedByUserId = userId,
                ReplyToUserId = userId,
                MemberId = member.Id,
                Purpose = purpose,
            }, ct);
            return true;
        }, ct);
        _logger.LogInformation("A {Purpose} email to a member is {Status}", purpose, row.Status);
        return (await ToDtosAsync(_messages, [row], ct))[0];
    }

    public async Task<Result<MemberMessageDto>> LogAsync(Guid organizationId, Guid userId, LogMemberMessageRequest request, CancellationToken ct = default)
    {
        if (request.MemberId is not Guid memberId) return DomainErrors.Club.MemberNotFound;
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var channel = CampaignChannels.All.FirstOrDefault(c => string.Equals(c, request.Channel?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? CampaignChannels.Email;
        var category = Category(request.Category);
        if (category == null) return CategoryInvalid;
        var subject = OutboxService.OneLine(request.Subject);
        if (subject.Length == 0) return new Error("MemberMessage.NoSubject", "Add a subject.");
        if (subject.Length > MaxSubject) return new Error("MemberMessage.TooLong", $"The subject can be at most {MaxSubject} characters.");
        var row = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Channel = channel,
            Category = category,
            Subject = subject,
            Preview = Preview((request.Preview ?? "").Trim()),
            CampaignName = Clip(request.CampaignName?.Trim(), 200),
            Status = MemberMessageStatuses.Logged,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _messages.AddMessageAsync(row, ct);
        return (await ToDtosAsync(_messages, [row], ct))[0];
    }

    public async Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var row = await _messages.GetMessageAsync(organizationId, id, ct);
        if (row == null) return new Error("MemberMessage.NotFound", "That message was not found.");
        if (row.Status != MemberMessageStatuses.Logged)
            return new Error("MemberMessage.Kept.Conflict", "Messages CadenceE sent, or did not send for a reason, are kept as the record of what went to whom.");
        await _messages.RemoveMessageAsync(row, ct);
        return Result.Success();
    }

    // ── Shared with campaigns, and with the events service (a seat come free, an event cancelled) ──

    /// <summary>
    /// Adds <paramref name="row"/> and hands its email to the outbox, inside the caller's
    /// transaction, so a message and the row recording it are written together or not at all.
    /// A refusal from the outbox (no consent, do not contact, a bounced address) is recorded
    /// on the row as the reason it was not sent. False when the row was refused because its
    /// campaign already has one for the member: then nothing was queued.
    /// </summary>
    public static async Task<bool> QueueAsync(ICampaignRepository messages, IOutbox outbox, MemberMessage row, OutboundEmailRequest email, CancellationToken ct)
    {
        // The row first: for a campaign, the unique index on (campaign, member) is what stops
        // a second sender emailing the member again, and it must stop it before the email exists.
        row.Status = MemberMessageStatuses.Queued;
        if (!await messages.AddMessageAsync(row, ct)) return false;
        var queued = await outbox.QueueEmailAsync(email, ct);
        if (queued.IsSuccess)
        {
            row.OutboundMessageId = queued.Value.Id;
        }
        else
        {
            row.Status = queued.Error.Code == "Email.InvalidAddress" ? MemberMessageStatuses.Skipped : MemberMessageStatuses.Suppressed;
            row.Reason = Clip(queued.Error.Description, 500);
        }
        await messages.SaveMessageAsync(row, ct);
        return true;
    }

    /// <summary>The rows with where each stands now, read from the outbox in one query.</summary>
    internal static async Task<IReadOnlyList<MemberMessageDto>> ToDtosAsync(ICampaignRepository messages, IReadOnlyList<MemberMessage> rows, CancellationToken ct)
    {
        var ids = rows.Where(r => r.OutboundMessageId != null).Select(r => r.OutboundMessageId!.Value).Distinct().ToList();
        var deliveries = (await messages.DeliveriesAsync(ids, ct)).ToDictionary(d => d.Id);
        return rows.Select(r =>
        {
            var delivery = r.OutboundMessageId is Guid id ? deliveries.GetValueOrDefault(id) : null;
            var status = MemberMessageStates.Of(r.Status, delivery?.Status, delivery?.DeliveryStatus, delivery?.OpenedAtUtc != null);
            var reason = status switch
            {
                // Not handed to the outbox: the row says why.
                MemberMessageStates.Skipped or MemberMessageStates.Suppressed when delivery == null => r.Reason,
                // The outbox stopped it as it went, took it back, or gave up on it.
                MemberMessageStates.Suppressed or MemberMessageStates.Failed or MemberMessageStates.Cancelled =>
                    delivery?.LastError ?? r.Reason ?? (delivery == null ? "The outbox has no record of it, so it will not be sent." : null),
                MemberMessageStates.Bounced => delivery?.DeliveryDetail ?? "The receiving mail server did not take it.",
                _ => null,
            };
            return new MemberMessageDto(
                r.Id, r.MemberId, r.MemberName, r.Email, r.CampaignId, r.CampaignName, r.Channel, r.Category, r.Purpose,
                r.Subject, r.Preview, status, reason, r.CreatedAtUtc, delivery?.SentAtUtc, delivery?.OpenedAtUtc);
        }).ToList();
    }

    internal static readonly Error CategoryInvalid = new("MemberMessage.CategoryInvalid",
        "A message is one of Welcome, Dues, Renewal, Event, Reservation, Loyalty, Announcement, Application or Other.");

    /// <summary>The category as listed, or Other when none is given; null when it is not one of the list.</summary>
    internal static string? Category(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Other";
        return Categories.FirstOrDefault(c => string.Equals(c, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static string NameOf(ClubMember member) => Clip($"{member.FirstName} {member.LastName}".Trim(), 200) ?? "";

    internal static string Preview(string body) => body.Length <= 140 ? body : body[..140];

    internal static string? Clip(string? text, int max) => text == null ? null : text.Length <= max ? text : text[..max];
}
