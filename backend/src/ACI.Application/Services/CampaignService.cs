using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// How a campaign's messages stand. Sent counts every message the mail server took,
/// delivered and opened ones included, and Delivered counts opened ones: a funnel, not
/// shares. Tests sent to staff are not counted.
/// </summary>
public sealed record CampaignTotalsDto(int Recipients, int Queued, int Sent, int Delivered, int Opened, int Bounced, int Suppressed, int Failed, int Skipped, int Cancelled = 0);

/// <param name="Segment">Which members it is for (see <see cref="MemberAudienceFilter"/>): empty lists and filters left out choose everyone.</param>
public sealed record CampaignDto(
    Guid Id, string Name, string Channel, string Category, string Purpose, string Subject, string Body,
    MemberAudienceFilter Segment, string Status, DateTime CreatedAtUtc, DateTime? ScheduledAtUtc,
    DateTime? SendingStartedAtUtc, DateTime? SentAtUtc, int RecipientCount, CampaignTotalsDto? Totals, Guid Version);

/// <summary>A campaign's test copy: where it went, and whether the mail server took it.</summary>
public sealed record CampaignTestDto(Guid MessageId, string To, string Status);

/// <summary>An audience to count for a campaign being written: who it is for, and what it is (marketing or service).</summary>
public sealed class CampaignAudienceRequest
{
    public MemberAudienceFilter? Segment { get; set; }
    public string? Purpose { get; set; }
}

/// <summary>A campaign to write, or the fields of one to change; on a change a field left out is kept and scheduledAtUtc sent as null unschedules it.</summary>
public sealed class CampaignWriteRequest : IMergePatchRequest
{
    public string? Name { get; set; }
    public string? Channel { get; set; }
    public string? Category { get; set; }
    public string? Purpose { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }

    /// <summary>Which members it is for. A saved view it names is kept as it reads now, so a campaign sent later keeps to it.</summary>
    public MemberAudienceFilter? Segment { get; set; }
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

    /// <summary>
    /// Who a campaign for this segment and purpose would reach if it were sent now, and who it
    /// leaves out and why. A saved view it names is read as <paramref name="userId"/> may see it.
    /// </summary>
    Task<Result<MemberAudienceDto>> AudienceAsync(Guid organizationId, Guid? userId, MemberAudienceFilter? segment, string? purpose, CancellationToken ct = default);

    /// <summary>Who a saved campaign would reach if it were sent now: its segment as it was saved, counted as the send counts it.</summary>
    Task<Result<MemberAudienceDto>> CampaignAudienceAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    Task<Result<CampaignDto>> CreateAsync(Guid organizationId, Guid userId, CampaignWriteRequest request, CancellationToken ct = default);
    Task<Result<CampaignDto>> UpdateAsync(Guid organizationId, Guid id, Guid userId, CampaignWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sends it now: claims it and hands it to the scheduler, which queues one email per member
    /// it is for. Answers at once, with the campaign sending.
    /// </summary>
    Task<Result<CampaignDto>> SendAsync(Guid organizationId, Guid id, Guid userId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// Stops a campaign that is sending: members it has not reached get nothing, and its emails
    /// still waiting in the outbox do not go. What already went is kept, with who it went to.
    /// </summary>
    Task<Result<CampaignDto>> StopAsync(Guid organizationId, Guid id, Guid userId, Guid? expectedVersion, CancellationToken ct = default);

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
/// <para>
/// Who it is for is worked out by the member audience (<see cref="IMemberAudience"/>), the one
/// place event invitations ask too: the counts staff see, and the members the send writes to,
/// come from the same plan. A segment of tiers and statuses alone chooses the members it always did.
/// </para>
/// </remarks>
public sealed class CampaignService : ICampaignService
{
    /// <summary>How long a sender holds a campaign before the scheduler may take it up; renewed as it works.</summary>
    internal static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(10);

    /// <summary>Campaigns the scheduler takes up in one pass.</summary>
    private const int DuePerPass = 10;

    /// <summary>
    /// Passes over a campaign's members that may end with someone's message not queued. After
    /// this many, those members are recorded as failed and the campaign finishes: a member whose
    /// message fails every time kept it sending, taken up every minute for ever.
    /// </summary>
    internal const int MaxSendAttempts = 5;

    /// <summary>How often, in members, a sender renews its claim and checks it was not stopped.</summary>
    private const int CheckEvery = 10;

    /// <summary>The segment as stored: a filter left out is not written, so one of tiers and statuses alone reads as it always did.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ICampaignRepository _campaigns;
    private readonly IMemberAudience _audience;
    private readonly IConsentRepository _consent;
    private readonly IOutbox _outbox;
    private readonly IUserRepository _users;
    private readonly ILogger<CampaignService> _logger;
    private readonly ITenantContext? _tenant;
    private readonly ICampaignSignal? _signal;

    public CampaignService(
        ICampaignRepository campaigns,
        IMemberAudience audience,
        IConsentRepository consent,
        IOutbox outbox,
        IUserRepository users,
        ILogger<CampaignService> logger,
        ITenantContext? tenant = null,
        ICampaignSignal? signal = null)
    {
        _campaigns = campaigns;
        _audience = audience;
        _consent = consent;
        _outbox = outbox;
        _users = users;
        _logger = logger;
        _tenant = tenant;
        _signal = signal;
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

    public async Task<Result<MemberAudienceDto>> AudienceAsync(Guid organizationId, Guid? userId, MemberAudienceFilter? segment, string? purpose, CancellationToken ct = default)
    {
        var why = (purpose ?? ConsentPurposes.Marketing).Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(why)) return PurposeInvalid;
        // What the outbox would still refuse as it queues them (OutboxService.RefusalAsync) is
        // counted too: an address on the suppression list, whatever the purpose, and for marketing
        // one a contact marked do not contact holds. Left out, the preview promised emails nobody would get.
        var plan = await _audience.PlanAsync(organizationId, new AudienceAsk(segment ?? new MemberAudienceFilter(), why, userId), ct);
        if (plan.IsFailure) return plan.Error;
        return MemberAudienceDto.Of(plan.Value);
    }

    public async Task<Result<MemberAudienceDto>> CampaignAudienceAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaigns.ReadAsync(organizationId, id, ct);
        if (campaign == null) return NotFound;
        // As the send will work it out: the segment and the view as they were saved.
        var plan = await _audience.PlanAsync(organizationId, new AudienceAsk(ReadSegment(campaign.Segment), campaign.Purpose, RequesterId: null), ct);
        if (plan.IsFailure) return plan.Error;
        return MemberAudienceDto.Of(plan.Value);
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
        var segment = await SegmentAsync(organizationId, userId, request.Segment, stored: null, ct);
        if (segment.IsFailure) return segment.Error;
        if (Apply(campaign, request, segment.Value, userId, now, creating: true) is { } invalid) return invalid;
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
        var segment = await SegmentAsync(organizationId, userId, request.Segment, ReadSegment(campaign.Segment), ct);
        if (segment.IsFailure) return segment.Error;
        if (Apply(campaign, request, segment.Value, userId, now, creating: false) is { } invalid) return invalid;
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
        // Claimed, so no second press, retry or edit gets in, with a claim that has already run
        // out: the scheduler takes it up as one whose sender stopped, and works through its
        // members there. Sent inside the request, a large club's campaign ran past the ~230
        // seconds a front end in Azure waits for an answer.
        if (!await _campaigns.TryClaimToSendAsync(organizationId, id, expectedVersion, userId, now, now, ct))
        {
            var current = await _campaigns.ReadAsync(organizationId, id, ct);
            return current == null ? NotFound : !CampaignStatuses.IsEditable(current.Status) ? AlreadySent : Changed;
        }
        _signal?.Notify();
        return (await GetAsync(organizationId, id, CancellationToken.None))!;
    }

    public async Task<Result<CampaignDto>> StopAsync(Guid organizationId, Guid id, Guid userId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var campaign = await _campaigns.ReadAsync(organizationId, id, ct);
        if (campaign == null) return NotFound;
        if (campaign.Status != CampaignStatuses.Sending) return NotSending(campaign.Status);
        if (!await _campaigns.TryStopAsync(organizationId, id, expectedVersion, DateTime.UtcNow, ct))
        {
            var current = await _campaigns.ReadAsync(organizationId, id, ct);
            return current == null ? NotFound : current.Status != CampaignStatuses.Sending ? NotSending(current.Status) : Changed;
        }
        // A sender still working through its members sees this the next time it renews its
        // claim and stops; the outbox does not send the campaign's emails still waiting.
        _logger.LogInformation("Campaign {CampaignId} stopped by {UserId}", id, userId);
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
        const string tag = "[Test] ";
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.CampaignTest,
            SourceId = campaign.Id,
            To = [address],
            RecipientName = user!.Name,
            Subject = tag + campaign.Subject,
            Body = campaign.Body,
            // As members will see it, under the subject without its tag, with the note on top.
            Design = draft => ClubEmails.Letter(draft.WorkspaceName, AppLanguages.English,
                draft.Subject.StartsWith(tag, StringComparison.Ordinal) ? draft.Subject[tag.Length..] : draft.Subject, draft.Body, title: true, "Test copy", note),
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
        // The members it is for, from the audience as the campaign was saved (its view as it was
        // then): every one its filters and statuses choose, each recorded below, sent or not. Of
        // two members at one address who may both have it, the one who joined first gets the
        // email (greeted by name) and the other is recorded as sharing it: the plan comes in that
        // order, the same answer every time, and one staff can explain.
        var plan = await _audience.PlanAsync(org, new AudienceAsk(ReadSegment(campaign.Segment), campaign.Purpose, RequesterId: null), ct);
        if (plan.IsFailure)
        {
            // Taken up again it would fail the same way every minute, and finished it would say
            // it went to nobody: stopped, it says plainly that it did not go.
            await _campaigns.TryStopAsync(org, campaign.Id, null, DateTime.UtcNow, ct);
            _logger.LogWarning("Campaign {CampaignId} was stopped: who it is for could not be worked out ({Reason})", campaign.Id, plan.Error.Description);
            return;
        }
        var members = plan.Value.Matching.Select(m => m.Member).ToList();
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
        // This pass is the last try for a member whose message will not queue.
        var lastTry = campaign.SendAttempts + 1 >= MaxSendAttempts;
        var failed = new List<(MemberMessage Row, Exception Error)>();
        var worked = 0;

        foreach (var member in members)
        {
            if (done.Contains(member.Id)) continue;
            // Renewed as it goes, and false once staff stopped it: the members after are not sent to.
            if (++worked % CheckEvery == 0 && !await _campaigns.ExtendClaimAsync(campaign.Id, DateTime.UtcNow.Add(ClaimFor), ct))
            {
                _logger.LogInformation("Campaign {CampaignId} was stopped while sending; {Worked} member(s) were worked through this pass", campaign.Id, worked - 1);
                return;
            }
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
                member.ErasedAtUtc != null ? (MemberMessageStatuses.Skipped, "This member's details were erased, so nothing goes to them.")
                : !ValidationHelper.IsValidEmail(address) ? (MemberMessageStatuses.Skipped, "There is no valid email address on the member's record.")
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
                    // What staff wrote, filled for the member, as paragraphs in the club's look.
                    Design = draft => ClubEmails.Letter(draft.WorkspaceName, AppLanguages.ForMember(member.PreferredLanguage), draft.Subject, draft.Body, title: true),
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
                failed.Add((row, ex));
                _logger.LogError(ex, "Campaign {CampaignId}: the message to member {MemberId} could not be queued", campaign.Id, member.Id);
            }
        }

        if (failed.Count > 0 && !lastTry)
        {
            // Tried again a little later each time (1, 2, 4, 8 minutes), until the last try.
            var attempts = campaign.SendAttempts + 1;
            await _campaigns.ReleaseClaimAsync(campaign.Id, DateTime.UtcNow.AddMinutes(Math.Pow(2, attempts - 1)), attempts, ct);
            return;
        }
        foreach (var (row, error) in failed)
        {
            // The last try: the member is recorded as not sent, with why, and the campaign
            // finishes for everyone else instead of staying "sending".
            row.Status = MemberMessageStatuses.Failed;
            row.OutboundMessageId = null;
            row.Reason = $"Its message could not be queued after {MaxSendAttempts} tries ({error.GetType().Name}), so it was not sent.";
            try
            {
                await _campaigns.AddMessageAsync(row, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not even the record of it saves: the log says who it was.
                _campaigns.Forget(row);
                _logger.LogError(ex, "Campaign {CampaignId}: member {MemberId} could not even be recorded as not sent", campaign.Id, row.MemberId);
            }
        }
        var recipients = (await _campaigns.RecipientsOfAsync(campaign.Id, ct)).Count;
        await _campaigns.FinishAsync(campaign.Id, DateTime.UtcNow, recipients, ct);
        if (failed.Count > 0)
            _logger.LogWarning("Campaign {CampaignId} finished after {Tries} tries with {Failed} member(s) whose message could not be queued", campaign.Id, MaxSendAttempts, failed.Count);
        _logger.LogInformation("Campaign {CampaignId} sent: {Recipients} member message(s)", campaign.Id, recipients);
    }

    // ── Rules ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <paramref name="campaign"/> from the request: every field on a new campaign, the
    /// fields sent on a change. Null when it is valid; the refusal otherwise.
    /// </summary>
    private static Error? Apply(Campaign campaign, CampaignWriteRequest request, MemberAudienceFilter? segment, Guid userId, DateTime now, bool creating)
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
            campaign.Segment = JsonSerializer.Serialize(MemberAudienceRules.Clean(segment), Json);

        if (request.ScheduledAtUtc is DateTime at)
        {
            // A campaign that would go by SMS or push (one kept from before) cannot be given a
            // time either: "Send now" refuses it, and the scheduler would have emailed it.
            if (campaign.Channel != CampaignChannels.Email) return ChannelUnavailable(campaign.Channel);
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
        var sending = campaigns.Where(c => c.Status is CampaignStatuses.Sending or CampaignStatuses.Sent or CampaignStatuses.Stopped).Select(c => c.Id).ToList();
        var result = new Dictionary<Guid, CampaignTotalsDto>();
        if (sending.Count == 0) return result;
        foreach (var group in (await _campaigns.TallyAsync(organizationId, sending, ct)).GroupBy(t => t.CampaignId))
        {
            int recipients = 0, queued = 0, sent = 0, delivered = 0, opened = 0, bounced = 0, suppressed = 0, failed = 0, skipped = 0, cancelled = 0;
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
                    case MemberMessageStates.Cancelled: cancelled += tally.Count; break;
                    default: failed += tally.Count; break;
                }
            }
            result[group.Key] = new CampaignTotalsDto(recipients, queued, sent, delivered, opened, bounced, suppressed, failed, skipped, cancelled);
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

    /// <summary>
    /// The segment a request sent, ready to keep: refused when its habits cannot be read, and a
    /// saved view it names looked up as the person may see it and kept as it reads now, so a
    /// campaign sent later by the scheduler keeps to what they saw. Null when none was sent.
    /// </summary>
    /// <param name="stored">The segment the campaign has now, when it is an edit.</param>
    private async Task<Result<MemberAudienceFilter?>> SegmentAsync(Guid organizationId, Guid userId, MemberAudienceFilter? sent, MemberAudienceFilter? stored, CancellationToken ct)
    {
        if (sent == null) return Result.Success<MemberAudienceFilter?>(null);
        var segment = MemberAudienceRules.Clean(sent);
        if (MemberAudienceRules.Problem(segment) is { } problem) return problem;
        if (segment.ViewId is not Guid viewId) return segment with { View = null };
        var view = await _audience.ViewAsync(organizationId, userId, viewId, ct);
        if (view.IsSuccess) return segment with { View = view.Value };
        // The view the campaign was saved with, by a colleague whose own it is: kept as it was
        // saved, so whoever edits the rest of the campaign is not refused for it. Choosing a view
        // the person may not see is refused.
        if (stored?.ViewId == viewId && stored.View != null) return segment with { View = stored.View };
        return view.Error;
    }

    /// <summary>The stored segment; one moved from an old document may hold more, which is left out.</summary>
    private static MemberAudienceFilter ReadSegment(string? json)
    {
        try
        {
            return MemberAudienceRules.Clean(JsonSerializer.Deserialize<MemberAudienceFilter>(string.IsNullOrWhiteSpace(json) ? "{}" : json, Json));
        }
        catch (JsonException)
        {
            return MemberAudienceRules.Clean(null);
        }
    }

    private static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static readonly Error NotFound = new("Campaign.NotFound", "That campaign was not found.");
    private static readonly Error PurposeInvalid = new("Campaign.PurposeInvalid", "A campaign is for marketing or for a service message.");
    private static readonly Error Locked = new("Campaign.Locked.Conflict", "This campaign has started sending, so it can no longer be changed.");
    private static readonly Error AlreadySent = new("Campaign.AlreadySent.Conflict", "This campaign is being sent or has been, so it cannot be sent again.");
    private static readonly Error Changed = new("Campaign.Changed.Conflict", "Someone else changed this campaign at the same moment, so nothing was done. Reload and try again.");

    private static Error NotSending(string status) => new("Campaign.NotSending.Conflict", status switch
    {
        CampaignStatuses.Draft or CampaignStatuses.Scheduled => "This campaign has not started sending, so there is nothing to stop. A scheduled one is unscheduled by editing it.",
        CampaignStatuses.Stopped => "This campaign was already stopped.",
        _ => "This campaign has finished sending, so there is nothing left to stop.",
    });

    private static Error ChannelUnavailable(string channel) => new("Campaign.ChannelUnavailable",
        $"{channel} is not available: CadenceE has no {(string.Equals(channel, CampaignChannels.Push, StringComparison.OrdinalIgnoreCase) ? "push" : "SMS")} provider. Campaigns go by email.");
}
