using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>Which members a campaign is for: empty lists mean every tier, or every status.</summary>
public sealed record CampaignSegmentDto(IReadOnlyList<string>? Tiers, IReadOnlyList<string>? Statuses);

/// <summary>
/// How a campaign's messages stand. Sent counts every message the mail server took,
/// delivered and opened ones included, and Delivered counts opened ones: a funnel, not
/// shares. Tests sent to staff are not counted.
/// </summary>
public sealed record CampaignTotalsDto(int Recipients, int Queued, int Sent, int Delivered, int Opened, int Bounced, int Suppressed, int Failed, int Skipped);

public sealed record CampaignDto(
    Guid Id, string Name, string Channel, string Category, string Purpose, string Subject, string Body,
    CampaignSegmentDto Segment, string Status, DateTime CreatedAtUtc, DateTime? ScheduledAtUtc,
    DateTime? SendingStartedAtUtc, DateTime? SentAtUtc, int RecipientCount, CampaignTotalsDto? Totals, Guid Version);

/// <summary>
/// Who a campaign would reach now: the members it is for, those with a usable address, those
/// who agreed to marketing by email (for a marketing campaign), and how many emails that makes.
/// </summary>
public sealed record CampaignAudienceDto(string Purpose, int Matching, int WithAddress, int Agreed, int WillReceive);

/// <summary>A campaign's test copy: where it went, and whether the mail server took it.</summary>
public sealed record CampaignTestDto(Guid MessageId, string To, string Status);

/// <summary>A campaign to write, or the fields of one to change; on a change a field left out is kept and scheduledAtUtc sent as null unschedules it.</summary>
public sealed class CampaignWriteRequest : IMergePatchRequest
{
    public string? Name { get; set; }
    public string? Channel { get; set; }
    public string? Category { get; set; }
    public string? Purpose { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public CampaignSegmentDto? Segment { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }

    /// <summary>The version the edit was made from; If-Match says the same.</summary>
    public Guid? Version { get; set; }

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}

public interface ICampaignService
{
    Task<IReadOnlyList<CampaignDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<CampaignDto?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>Every member the campaign was for, with where their message stands.</summary>
    Task<Result<IReadOnlyList<MemberMessageDto>>> RecipientsAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>Who a campaign for this segment and purpose would reach if it were sent now.</summary>
    Task<Result<CampaignAudienceDto>> AudienceAsync(Guid organizationId, CampaignSegmentDto? segment, string? purpose, CancellationToken ct = default);

    Task<Result<CampaignDto>> CreateAsync(Guid organizationId, Guid userId, CampaignWriteRequest request, CancellationToken ct = default);
    Task<Result<CampaignDto>> UpdateAsync(Guid organizationId, Guid id, Guid userId, CampaignWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>Sends it now: claims it, then queues one email per member it is for.</summary>
    Task<Result<CampaignDto>> SendAsync(Guid organizationId, Guid id, Guid userId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Sends a copy marked as a test to the signed-in person alone; counted in none of the campaign's figures.</summary>
    Task<Result<CampaignTestDto>> SendTestAsync(Guid organizationId, Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>For the scheduler, in every workspace: sends the campaigns that are due and finishes any whose sender stopped. Returns how many it took up.</summary>
    Task<int> RunDueAsync(DateTime utcNow, CancellationToken ct = default);
}

/// <summary>
/// Campaigns to a club's members, delivered by email through the outbox, one message per member.
/// </summary>
/// <remarks>
/// <para>
/// Sending, whether someone pressed Send or the scheduler found it due, starts with one
/// conditional update that moves the campaign to Sending. Only one caller can win that, so
/// two instances, a double click or a retry send it once; from then on it cannot be edited,
/// rescheduled or sent again. Each member it is for then gets a <see cref="MemberMessage"/>,
/// written with its outbox email in one transaction; the unique index on (campaign, member)
/// means a sending taken up again after it died, by the scheduler once the claim runs out,
/// emails nobody twice.
/// </para>
/// <para>
/// A marketing campaign reaches only members whose standing answer for marketing by email is
/// yes; the others are recorded as not sent, with the reason. Two members at one address get
/// one email. A service campaign (the club closed on Monday) goes to everyone it is for.
/// </para>
/// </remarks>
public sealed class CampaignService : ICampaignService
{
    /// <summary>How long a sender holds a campaign before the scheduler may take it up; renewed as it works.</summary>
    internal static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(10);

    /// <summary>Campaigns the scheduler takes up in one pass.</summary>
    private const int DuePerPass = 10;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICampaignRepository _campaigns;
    private readonly IClubRepository _club;
    private readonly IConsentRepository _consent;
    private readonly IOutbox _outbox;
    private readonly IUserRepository _users;
    private readonly ILogger<CampaignService> _logger;
    private readonly ITenantContext? _tenant;

    public CampaignService(
        ICampaignRepository campaigns,
        IClubRepository club,
        IConsentRepository consent,
        IOutbox outbox,
        IUserRepository users,
        ILogger<CampaignService> logger,
        ITenantContext? tenant = null)
    {
        _campaigns = campaigns;
        _club = club;
        _consent = consent;
        _outbox = outbox;
        _users = users;
        _logger = logger;
        _tenant = tenant;
    }

    // ── Reading ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CampaignDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var campaigns = await _campaigns.ListAsync(organizationId, ct);
        var totals = await TotalsAsync(organizationId, campaigns, ct);
        return campaigns.Select(c => Dto(c, totals.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<CampaignDto?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaigns.ReadAsync(organizationId, id, ct);
        if (campaign == null) return null;
        var totals = await TotalsAsync(organizationId, [campaign], ct);
        return Dto(campaign, totals.GetValueOrDefault(campaign.Id));
    }

    public async Task<Result<IReadOnlyList<MemberMessageDto>>> RecipientsAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        if (await _campaigns.ReadAsync(organizationId, id, ct) == null) return NotFound;
        var rows = await _campaigns.MessagesAsync(organizationId, null, id, 100_000, ct);
        return Result.Success(await MemberMessageService.ToDtosAsync(_campaigns, rows.OrderBy(r => r.MemberName).ToList(), ct));
    }

    public async Task<Result<CampaignAudienceDto>> AudienceAsync(Guid organizationId, CampaignSegmentDto? segment, string? purpose, CancellationToken ct = default)
    {
        var why = (purpose ?? ConsentPurposes.Marketing).Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(why)) return PurposeInvalid;
        var members = Matching(await _club.MembersAsync(organizationId, ct), Clean(segment));
        var withAddress = members.Where(m => ValidationHelper.IsValidEmail((m.Email ?? "").Trim())).ToList();
        var agreed = why == ConsentPurposes.Marketing
            ? (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, withAddress.Select(m => m.Id).ToList(), ConsentChannels.Email, ConsentPurposes.Marketing, ct))
                .Where(c => c.Granted).Select(c => c.SubjectId).ToHashSet()
            : withAddress.Select(m => m.Id).ToHashSet();
        var reached = withAddress.Where(m => agreed.Contains(m.Id))
            .Select(m => Normalize(m.Email)).Distinct().Count();
        return new CampaignAudienceDto(why, members.Count, withAddress.Count, agreed.Count, reached);
    }

    // ── Writing ───────────────────────────────────────────────────────────────

    public async Task<Result<CampaignDto>> CreateAsync(Guid organizationId, Guid userId, CampaignWriteRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
            UpdatedAtUtc = now,
        };
        if (Apply(campaign, request, userId, now, creating: true) is { } invalid) return invalid;
        await _campaigns.AddAsync(campaign, ct);
        return Dto(campaign, null);
    }

    public async Task<Result<CampaignDto>> UpdateAsync(Guid organizationId, Guid id, Guid userId, CampaignWriteRequest request, CancellationToken ct = default)
    {
        var campaign = await _campaigns.GetAsync(organizationId, id, ct);
        if (campaign == null) return NotFound;
        if (!CampaignStatuses.IsEditable(campaign.Status)) return Locked;
        if (request.Version is Guid expected && expected != campaign.Version) return Changed;
        var now = DateTime.UtcNow;
        if (Apply(campaign, request, userId, now, creating: false) is { } invalid) return invalid;
        campaign.UpdatedAtUtc = now;
        try
        {
            // Against the version read here and the one the person edited: the scheduler's
            // claim moves it, so an edit that loses to a send is refused, not saved over it.
            await _campaigns.SaveAsync(campaign, request.Version, ct);
        }
        catch (ConcurrencyConflictException)
        {
            var now2 = await _campaigns.ReadAsync(organizationId, id, ct);
            return now2 != null && !CampaignStatuses.IsEditable(now2.Status) ? Locked : Changed;
        }
        return Dto(campaign, null);
    }

    public async Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        if (await _campaigns.DeleteUnsentAsync(organizationId, id, ct)) return Result.Success();
        return await _campaigns.ReadAsync(organizationId, id, ct) == null
            ? NotFound
            : new Error("Campaign.Kept.Conflict", "A campaign that has started sending is kept, with what went to whom.");
    }

    // ── Sending ───────────────────────────────────────────────────────────────

    public async Task<Result<CampaignDto>> SendAsync(Guid organizationId, Guid id, Guid userId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var campaign = await _campaigns.ReadAsync(organizationId, id, ct);
        if (campaign == null) return NotFound;
        if (campaign.Channel != CampaignChannels.Email) return ChannelUnavailable(campaign.Channel);
        if (!CampaignStatuses.IsEditable(campaign.Status)) return AlreadySent;
        var now = DateTime.UtcNow;
        if (!await _campaigns.TryClaimToSendAsync(organizationId, id, expectedVersion, userId, now, now.Add(ClaimFor), ct))
        {
            var current = await _campaigns.ReadAsync(organizationId, id, ct);
            return current == null ? NotFound : !CampaignStatuses.IsEditable(current.Status) ? AlreadySent : Changed;
        }

        // Not the request's token: once sending has started it is finished here, whether or
        // not the browser that pressed Send is still waiting. If this instance dies part-way,
        // the scheduler takes it up where it stopped once the claim runs out.
        var claimed = await _campaigns.ReadAsync(organizationId, id, CancellationToken.None);
        if (claimed != null) await SendClaimedAsync(claimed, CancellationToken.None);
        return (await GetAsync(organizationId, id, CancellationToken.None))!;
    }

    public async Task<Result<CampaignTestDto>> SendTestAsync(Guid organizationId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var campaign = await _campaigns.ReadAsync(organizationId, id, ct);
        if (campaign == null) return NotFound;
        if (campaign.Channel != CampaignChannels.Email) return ChannelUnavailable(campaign.Channel);
        var user = await _users.GetByIdAsync(userId, ct);
        var address = (user?.Email ?? "").Trim();
        if (!ValidationHelper.IsValidEmail(address))
            return new Error("Campaign.NoTestAddress", "Your account has no email address to send the test to.");

        var note = $"This is a test of the campaign \"{campaign.Name}\", sent to you alone. Members have not received it."
            + (campaign.Purpose == ConsentPurposes.Marketing ? " Their copies end with a link to unsubscribe from marketing." : "");
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.CampaignTest,
            SourceId = campaign.Id,
            To = [address],
            RecipientName = user!.Name,
            Subject = $"[Test] {campaign.Subject}",
            Body = $"{note}\n\n{campaign.Body}",
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            ForOwnTeam = true,
            // The person is waiting: a placeholder nothing fills is said now, before members get it blank.
            PersonWaiting = true,
        }, ct);
        if (queued.IsFailure) return queued.Error;

        // Not the request's token: a browser that hangs up must not cut the attempt off halfway.
        var attempt = await _outbox.SendNowAsync(queued.Value, CancellationToken.None);
        return attempt.Result switch
        {
            OutboxAttemptResult.NotConfigured => new Error("Email.NotConfigured", "Sending email is not set up on the server yet."),
            OutboxAttemptResult.Suppressed => new Error(attempt.Code ?? "Email.Suppressed", attempt.Error ?? "Your address is on the suppression list, so the test was not sent."),
            OutboxAttemptResult.Failed => new Error("Email.SendFailed", $"The test was not sent. {attempt.Error ?? "The mail server refused it."}"),
            _ => new CampaignTestDto(queued.Value.Id, address, attempt.Result == OutboxAttemptResult.Sent ? "sent" : "queued"),
        };
    }

    public async Task<int> RunDueAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var taken = 0;
        foreach (var due in await _campaigns.DueAsync(utcNow, DuePerPass, ct))
        {
            if (!await _campaigns.TryClaimDueAsync(due.Id, utcNow, utcNow.Add(ClaimFor), ct)) continue;
            taken++;
            // The scheduler starts in no workspace: everything this campaign reads and
            // queues (its members, their consent, the outbox) is its own workspace's.
            using var acting = _tenant?.ActFor(due.OrganizationId, null);
            try
            {
                var campaign = await _campaigns.ReadAsync(due.OrganizationId, due.Id, ct);
                if (campaign != null) await SendClaimedAsync(campaign, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Its claim runs out and it is taken up again; the members done so far stay done.
                _logger.LogError(ex, "Campaign {CampaignId} could not be sent this time", due.Id);
            }
        }
        return taken;
    }

    /// <summary>
    /// Works through the members a claimed campaign is for, skipping any it already has a
    /// message for, and marks it sent once every one has one.
    /// </summary>
    private async Task SendClaimedAsync(Campaign campaign, CancellationToken ct)
    {
        var org = campaign.OrganizationId;
        var members = Matching(await _club.MembersAsync(org, ct), ReadSegment(campaign.Segment))
            .OrderBy(m => m.Id)
            .ToList();
        var already = await _campaigns.RecipientsOfAsync(campaign.Id, ct);
        var done = already.Select(r => r.MemberId).ToHashSet();
        // The addresses already emailed: a member taken up again after a stop does not get a second copy at one of them.
        var addresses = already.Where(r => r.Email != null && r.Status == MemberMessageStatuses.Queued).Select(r => Normalize(r.Email)).ToHashSet();
        var marketing = campaign.Purpose == ConsentPurposes.Marketing;
        var standing = marketing
            ? (await _consent.CurrentAsync(org, ConsentSubjects.Member, members.Select(m => m.Id).ToList(), ConsentChannels.Email, ConsentPurposes.Marketing, ct))
                .ToDictionary(c => c.SubjectId)
            : [];
        var preview = MemberMessageService.Preview(campaign.Body);
        var failed = false;
        var worked = 0;

        foreach (var member in members)
        {
            if (done.Contains(member.Id)) continue;
            if (++worked % 50 == 0) await _campaigns.ExtendClaimAsync(campaign.Id, DateTime.UtcNow.Add(ClaimFor), ct);
            var address = (member.Email ?? "").Trim();
            var row = new MemberMessage
            {
                Id = Guid.NewGuid(),
                OrganizationId = org,
                MemberId = member.Id,
                MemberName = MemberMessageService.NameOf(member),
                Email = address.Length == 0 ? null : MemberMessageService.Clip(address, 256),
                CampaignId = campaign.Id,
                CampaignName = campaign.Name,
                Channel = CampaignChannels.Email,
                Category = campaign.Category,
                Purpose = campaign.Purpose,
                Subject = campaign.Subject,
                Preview = preview,
                CreatedByUserId = campaign.SentByUserId,
                CreatedAtUtc = DateTime.UtcNow,
            };

            // In this order: a member who may not have it does not take their address's one
            // email away from a member at the same address who may.
            (string Status, string Reason)? notSent =
                !ValidationHelper.IsValidEmail(address) ? (MemberMessageStatuses.Skipped, "There is no valid email address on the member's record.")
                : marketing && Refusal(standing.GetValueOrDefault(member.Id)) is { } no ? (MemberMessageStatuses.Suppressed, no)
                : !addresses.Add(Normalize(address)) ? (MemberMessageStatuses.Skipped, "Another member at this address gets this campaign.")
                : null;
            try
            {
                if (notSent is { } skip)
                {
                    row.Status = skip.Status;
                    row.Reason = skip.Reason;
                    await _campaigns.AddMessageAsync(row, ct);
                    continue;
                }
                await _campaigns.InTransactionAsync(() => MemberMessageService.QueueAsync(_campaigns, _outbox, row, new OutboundEmailRequest
                {
                    OrganizationId = org,
                    Source = OutboundSources.Campaign,
                    SourceId = campaign.Id,
                    To = [address],
                    RecipientName = row.MemberName,
                    Subject = campaign.Subject,
                    Body = campaign.Body,
                    CreatedByUserId = campaign.SentByUserId,
                    ReplyToUserId = campaign.SentByUserId,
                    MemberId = member.Id,
                    Purpose = campaign.Purpose,
                }, ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Rolled back, row and email together. The member is tried again when the
                // campaign is taken up again; everyone after them still gets theirs now.
                _campaigns.Forget(row);
                failed = true;
                _logger.LogError(ex, "Campaign {CampaignId}: the message to member {MemberId} could not be queued", campaign.Id, member.Id);
            }
        }

        if (failed)
        {
            await _campaigns.ReleaseClaimAsync(campaign.Id, DateTime.UtcNow.AddMinutes(1), ct);
            return;
        }
        var recipients = (await _campaigns.RecipientsOfAsync(campaign.Id, ct)).Count;
        await _campaigns.FinishAsync(campaign.Id, DateTime.UtcNow, recipients, ct);
        _logger.LogInformation("Campaign {CampaignId} sent: {Recipients} member message(s)", campaign.Id, recipients);
    }

    // ── Rules ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <paramref name="campaign"/> from the request: every field on a new campaign, the
    /// fields sent on a change. Null when it is valid; the refusal otherwise.
    /// </summary>
    private static Error? Apply(Campaign campaign, CampaignWriteRequest request, Guid userId, DateTime now, bool creating)
    {
        if (creating || request.Name != null)
        {
            var name = (request.Name ?? "").Trim();
            if (name.Length == 0) return new Error("Campaign.NameRequired", "Give the campaign a name.");
            if (name.Length > 200) return new Error("Campaign.TooLong", "The name can be at most 200 characters.");
            campaign.Name = name;
        }
        if (creating || request.Channel != null)
        {
            var channel = string.IsNullOrWhiteSpace(request.Channel) ? CampaignChannels.Email : request.Channel.Trim();
            // SMS and push have no provider behind them: a campaign that said it would text
            // members, and did not, is what this replaced.
            if (!string.Equals(channel, CampaignChannels.Email, StringComparison.OrdinalIgnoreCase)) return ChannelUnavailable(channel);
            campaign.Channel = CampaignChannels.Email;
        }
        if (creating || request.Category != null)
        {
            var category = MemberMessageService.Category(request.Category ?? "Announcement");
            if (category == null) return MemberMessageService.CategoryInvalid;
            campaign.Category = category;
        }
        if (creating || request.Purpose != null)
        {
            var purpose = (request.Purpose ?? ConsentPurposes.Marketing).Trim().ToLowerInvariant();
            if (!ConsentPurposes.All.Contains(purpose)) return PurposeInvalid;
            campaign.Purpose = purpose;
        }
        if (creating || request.Subject != null)
        {
            var subject = OutboxService.OneLine(request.Subject);
            if (subject.Length == 0) return new Error("Campaign.NoSubject", "Add a subject.");
            if (subject.Length > MemberMessageService.MaxSubject) return new Error("Campaign.TooLong", $"The subject can be at most {MemberMessageService.MaxSubject} characters.");
            campaign.Subject = subject;
        }
        if (creating || request.Body != null)
        {
            var body = (request.Body ?? "").Trim();
            if (body.Length == 0) return new Error("Campaign.NoBody", "Write the message.");
            if (body.Length > MemberMessageService.MaxBody) return new Error("Campaign.TooLong", $"The message can be at most {MemberMessageService.MaxBody:N0} characters.");
            campaign.Body = body;
        }
        if (creating || request.Segment != null || request.Clears(nameof(CampaignWriteRequest.Segment)))
            campaign.Segment = JsonSerializer.Serialize(Clean(request.Segment), Json);

        if (request.ScheduledAtUtc is DateTime at)
        {
            var when = at.Kind == DateTimeKind.Local ? at.ToUniversalTime() : DateTime.SpecifyKind(at, DateTimeKind.Utc);
            if (when <= now.AddMinutes(1)) return new Error("Campaign.ScheduleInPast", "Pick a time in the future to send it, or send it now.");
            campaign.ScheduledAtUtc = when;
            campaign.Status = CampaignStatuses.Scheduled;
            // Whoever schedules it sends it: replies go to them.
            campaign.SentByUserId = userId;
        }
        else if (creating || request.Clears(nameof(CampaignWriteRequest.ScheduledAtUtc)))
        {
            campaign.ScheduledAtUtc = null;
            campaign.Status = CampaignStatuses.Draft;
            campaign.SentByUserId = null;
        }
        return null;
    }

    private async Task<Dictionary<Guid, CampaignTotalsDto>> TotalsAsync(Guid organizationId, IReadOnlyList<Campaign> campaigns, CancellationToken ct)
    {
        var sending = campaigns.Where(c => c.Status is CampaignStatuses.Sending or CampaignStatuses.Sent).Select(c => c.Id).ToList();
        var result = new Dictionary<Guid, CampaignTotalsDto>();
        if (sending.Count == 0) return result;
        foreach (var group in (await _campaigns.TallyAsync(organizationId, sending, ct)).GroupBy(t => t.CampaignId))
        {
            int recipients = 0, queued = 0, sent = 0, delivered = 0, opened = 0, bounced = 0, suppressed = 0, failed = 0, skipped = 0;
            foreach (var tally in group)
            {
                recipients += tally.Count;
                switch (MemberMessageStates.Of(tally.Status, tally.OutboundStatus, tally.DeliveryStatus, tally.Opened))
                {
                    case MemberMessageStates.Queued: queued += tally.Count; break;
                    case MemberMessageStates.Sent: sent += tally.Count; break;
                    case MemberMessageStates.Delivered: sent += tally.Count; delivered += tally.Count; break;
                    case MemberMessageStates.Opened: sent += tally.Count; delivered += tally.Count; opened += tally.Count; break;
                    case MemberMessageStates.Bounced: sent += tally.Count; bounced += tally.Count; break;
                    case MemberMessageStates.Suppressed: suppressed += tally.Count; break;
                    case MemberMessageStates.Skipped: skipped += tally.Count; break;
                    default: failed += tally.Count; break;
                }
            }
            result[group.Key] = new CampaignTotalsDto(recipients, queued, sent, delivered, opened, bounced, suppressed, failed, skipped);
        }
        // A campaign that matched nobody still has figures: all nought.
        foreach (var id in sending.Where(id => !result.ContainsKey(id)))
            result[id] = new CampaignTotalsDto(0, 0, 0, 0, 0, 0, 0, 0, 0);
        return result;
    }

    private static CampaignDto Dto(Campaign c, CampaignTotalsDto? totals)
    {
        var segment = ReadSegment(c.Segment);
        return new CampaignDto(
            c.Id, c.Name, c.Channel, c.Category, c.Purpose, c.Subject, c.Body, segment, c.Status, c.CreatedAtUtc,
            c.ScheduledAtUtc, c.SendingStartedAtUtc, c.SentAtUtc, totals?.Recipients ?? c.RecipientCount, totals, c.Version);
    }

    /// <summary>Why a member may not be sent marketing by email, from their standing answer, or null when they agreed.</summary>
    private static string? Refusal(ConsentRecord? standing)
    {
        if (standing == null) return "This member has not agreed to marketing by email.";
        if (standing.Granted) return null;
        var on = standing.RecordedAtUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return standing.Source == ConsentSources.Unsubscribe
            ? $"This member unsubscribed from marketing by email on {on}."
            : $"This member withdrew consent to marketing by email on {on}.";
    }

    private static List<ClubMember> Matching(IReadOnlyList<ClubMember> members, CampaignSegmentDto segment) =>
        members.Where(m =>
                (segment.Tiers is not { Count: > 0 } tiers || tiers.Contains(m.Tier, StringComparer.OrdinalIgnoreCase))
                && (segment.Statuses is not { Count: > 0 } statuses || statuses.Contains(m.Status, StringComparer.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>The segment as kept: each list trimmed, without blanks or repeats, at most 20 entries.</summary>
    private static CampaignSegmentDto Clean(CampaignSegmentDto? segment)
    {
        static IReadOnlyList<string> List(IReadOnlyList<string>? values) => (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().Length > 32 ? v.Trim()[..32] : v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        return new CampaignSegmentDto(List(segment?.Tiers), List(segment?.Statuses));
    }

    /// <summary>The stored segment; one moved from an old document may hold more, which is left out.</summary>
    private static CampaignSegmentDto ReadSegment(string? json)
    {
        try
        {
            return Clean(JsonSerializer.Deserialize<CampaignSegmentDto>(string.IsNullOrWhiteSpace(json) ? "{}" : json, Json));
        }
        catch (JsonException)
        {
            return Clean(null);
        }
    }

    private static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static readonly Error NotFound = new("Campaign.NotFound", "That campaign was not found.");
    private static readonly Error PurposeInvalid = new("Campaign.PurposeInvalid", "A campaign is for marketing or for a service message.");
    private static readonly Error Locked = new("Campaign.Locked.Conflict", "This campaign has started sending, so it can no longer be changed.");
    private static readonly Error AlreadySent = new("Campaign.AlreadySent.Conflict", "This campaign is being sent or has been, so it cannot be sent again.");
    private static readonly Error Changed = new("Campaign.Changed.Conflict", "Someone else changed this campaign at the same moment, so nothing was done. Reload and try again.");

    private static Error ChannelUnavailable(string channel) => new("Campaign.ChannelUnavailable",
        $"{channel} is not available: Cadence has no {(string.Equals(channel, CampaignChannels.Push, StringComparison.OrdinalIgnoreCase) ? "push" : "SMS")} provider. Campaigns go by email.");
}
