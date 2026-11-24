using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Invitations to a club event with a reply link of each member's own (see
/// <see cref="IEventInvitations"/> for the rules). This part is the staff's: the letter, its
/// preview and test, the send, the list of who was invited, and the one reminder.
/// </summary>
/// <remarks>
/// <para>
/// Who is invited is the member audience's to say (<see cref="IMemberAudience"/>, as for
/// campaigns), the same way for the counts the dialog shows and for the send: members on the
/// event's list already, or invited to it already, are left out besides the usual reasons.
/// </para>
/// <para>
/// Each member's invitation is a row with their reply link's hash, a line in their
/// communications (MemberMessage) and an email in the outbox, written together (one
/// transaction on SQL Server), so a link in an email always answers. The emails are spread
/// over the minutes after the send, <see cref="PerMinute"/> a minute, as a newsletter's are.
/// The send's id is the browser's, and the same press sent twice sends nothing more.
/// </para>
/// </remarks>
public sealed partial class EventInvitationService : IEventInvitations
{
    /// <summary>The most members one send invites: past it, the audience is to be narrowed.</summary>
    public const int MaxRecipients = 2000;

    /// <summary>Emails a minute the mail server is given.</summary>
    public const int PerMinute = 60;

    private const int MaxSubject = MemberMessageService.MaxSubject;
    private const int MaxBody = MemberMessageService.MaxBody;

    /// <summary>Someone to show the letter for when no member is chosen.</summary>
    private static readonly (string First, string Last, string Email) Sample = ("Camille", "Laurent", "camille@example.com");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppDbContext _db;
    private readonly IMemberAudience _audience;
    private readonly IClubEvents _events;
    private readonly IOutbox _outbox;
    private readonly IOutreachMail _outreach;
    private readonly IUserRepository _users;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceModules _modules;
    private readonly EmailSettings _email;
    private readonly ILogger<EventInvitationService> _logger;

    public EventInvitationService(
        AppDbContext db, IMemberAudience audience, IClubEvents events, IOutbox outbox, IOutreachMail outreach, IUserRepository users,
        ITenantContext tenant, IWorkspaceModules modules, IOptions<EmailSettings> email, ILogger<EventInvitationService> logger)
    {
        _db = db;
        _audience = audience;
        _events = events;
        _outbox = outbox;
        _outreach = outreach;
        _users = users;
        _tenant = tenant;
        _modules = modules;
        _email = email.Value;
        _logger = logger;
    }

    // ── what it can do ───────────────────────────────────────────────────────

    public async Task<Result<InvitationSetupDto>> SetupAsync(Guid organizationId, Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var sender = await _outreach.SenderAsync(organizationId, userId, ct);
        var club = await ClubAsync(organizationId, ct);
        var defaults = AppLanguages.All.ToDictionary(l => l, DefaultLetter);
        return new InvitationSetupDto(defaults, InvitationTemplate.Fields, sender.WorkspaceName, sender.ReplyTo, club.Zone.Id,
            ReplyPage() == null ? NoReplyPage : null, Closed(ev), MaxRecipients);
    }

    // ── who it goes to ───────────────────────────────────────────────────────

    public async Task<Result<MemberAudienceDto>> AudienceAsync(Guid organizationId, Guid userId, Guid eventId, MemberAudienceFilter? audience, CancellationToken ct = default)
    {
        if (await EventAsync(organizationId, eventId, ct) == null) return DomainErrors.ClubEvent.NotFound;
        var plan = await PlanAsync(organizationId, userId, eventId, audience, ct);
        if (plan.IsFailure) return plan.Error;
        return MemberAudienceDto.Of(plan.Value);
    }

    /// <summary>Invitations are marketing, and leave out who is on the event's list or invited to it already.</summary>
    private Task<Result<AudiencePlan>> PlanAsync(Guid organizationId, Guid userId, Guid eventId, MemberAudienceFilter? audience, CancellationToken ct) =>
        _audience.PlanAsync(organizationId, new AudienceAsk(audience ?? new MemberAudienceFilter(), ConsentPurposes.Marketing, userId, eventId), ct);

    // ── the letter ───────────────────────────────────────────────────────────

    public async Task<Result<InvitationPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, Guid eventId, InvitationPreviewRequest request, CancellationToken ct = default)
    {
        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        if (LettersProblem(request.Letters, sending: false) is { } problem) return problem;
        var member = request.SampleMemberId is Guid id
            ? await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct)
            : null;
        if (request.SampleMemberId != null && member == null) return DomainErrors.Club.MemberNotFound;
        var language = member != null ? AppLanguages.ForMember(member.PreferredLanguage) : AppLanguages.Normalise(request.Language) ?? AppLanguages.English;
        var club = await ClubAsync(organizationId, ct);
        var sender = await _outreach.SenderAsync(organizationId, userId, ct);
        var letter = LetterIn(request.Letters, language);
        var values = ValuesFor(ev, club, language, member?.FirstName ?? Sample.First, member?.LastName ?? Sample.Last, SampleLink());
        return new InvitationPreviewDto(
            language,
            OneLine(InvitationTemplate.Fill(letter.Subject, values)),
            InvitationTemplate.Fill(letter.Body, values),
            $"{member?.FirstName ?? Sample.First} {member?.LastName ?? Sample.Last}".Trim(),
            member == null ? Sample.Email : string.IsNullOrWhiteSpace(member.Email) ? null : member.Email.Trim(),
            InvitationTemplate.Unknown(letter.Subject, letter.Body),
            sender.WorkspaceName, sender.ReplyTo);
    }

    public async Task<Result<InvitationTestDto>> SendTestAsync(Guid organizationId, Guid userId, Guid eventId, InvitationPreviewRequest request, CancellationToken ct = default)
    {
        var to = (await _users.GetByIdAsync(userId, ct))?.Email?.Trim() ?? "";
        if (!ValidationHelper.IsValidEmail(to)) return new Error("Invitation.NoTestAddress", "Your account has no email address to send the test to.");
        var preview = await PreviewAsync(organizationId, userId, eventId, request, ct);
        if (preview.IsFailure) return preview.Error;
        var email = preview.Value;
        if (email.Unknown.Count > 0) return Unfilled(email.Unknown);
        if (!InvitationTemplate.HasReplyLink(LetterIn(request.Letters, email.Language).Body)) return NoReplyLink(email.Language);

        var note = $"This is a test of the invitation, as {email.SampleName} will receive it ({LanguageName(email.Language)}). "
            + "Its reply link is a sample: it answers for nobody. Members' copies end with the link that stops news and invitations.";
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.InvitationTest,
            SourceId = eventId,
            To = [to],
            Language = email.Language,
            Subject = $"[Test] {email.Subject}",
            Body = $"{note}\n\n{email.Body}",
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            ForOwnTeam = true,
            PersonWaiting = true,
        }, ct);
        if (queued.IsFailure) return queued.Error;

        // Not the request's token: a browser that hangs up must not cut the attempt off halfway.
        var attempt = await _outbox.SendNowAsync(queued.Value, CancellationToken.None);
        return attempt.Result switch
        {
            OutboxAttemptResult.NotConfigured => new Error("Email.NotConfigured", "Sending email is not set up on the server yet."),
            OutboxAttemptResult.Suppressed => new Error(attempt.Code ?? "Email.Suppressed", attempt.Error ?? $"{to} is on the suppression list, so the test was not sent."),
            OutboxAttemptResult.Failed => new Error("Email.SendFailed", $"The test was not sent. {attempt.Error ?? "The mail server refused it."}"),
            _ => new InvitationTestDto(to, attempt.Result == OutboxAttemptResult.Sent ? "sent" : "queued", email.Language),
        };
    }

    // ── sending ──────────────────────────────────────────────────────────────

    public async Task<Result<InvitationSendDto>> SendAsync(Guid organizationId, Guid userId, Guid eventId, InvitationSendRequest request, CancellationToken ct = default)
    {
        if (request.SendId == Guid.Empty) return new Error("Invitation.SendId", "The send has no id: reload the page and send again.");
        // Sent already: the same press again (a double click, a retry), which sends nothing more.
        if (await _db.ClubEventInvitationSends.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SendId && s.OrganizationId == organizationId, ct) is { } earlier)
            return Dto(earlier, alreadySent: true);

        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        if (Closed(ev) is { } closed) return new Error("Invitation.Closed.Conflict", $"{closed} Nothing was sent.");
        var page = ReplyPage();
        if (page == null) return new Error("Invitation.NoReplyPage", NoReplyPage);
        if (LettersProblem(request.Letters, sending: true) is { } problem) return problem;

        var plan = await PlanAsync(organizationId, userId, eventId, request.Audience, ct);
        if (plan.IsFailure) return plan.Error;
        var recipients = plan.Value.Recipients;
        var leftOut = plan.Value.Chosen.Count - recipients.Count;
        if (recipients.Count == 0) return new Error("Invitation.Nobody", "Nobody the filters choose can be invited, so nothing was sent.");
        if (recipients.Count > MaxRecipients)
            return new Error("Invitation.TooMany", $"This would invite {recipients.Count:N0} members; one send invites at most {MaxRecipients:N0}. Narrow the filters.");
        if (request.ExpectedRecipients is int expected && expected != recipients.Count)
            return new Error("Invitation.Changed.Conflict",
                $"Who it goes to has changed since you confirmed: it would now invite {recipients.Count:N0} member{(recipients.Count == 1 ? "" : "s")}, not {expected:N0}. Check the list and confirm again.");

        var now = DateTime.UtcNow;
        var send = new ClubEventInvitationSend
        {
            Id = request.SendId,
            OrganizationId = organizationId,
            EventId = eventId,
            AudienceJson = Clip(JsonSerializer.Serialize(MemberAudienceRules.Clean(request.Audience), Json), 8000),
            LettersJson = JsonSerializer.Serialize(AppLanguages.All.ToDictionary(l => l, l => LetterIn(request.Letters, l)), Json),
            Status = ClubEventInvitationSendStatuses.Sending,
            LeftOut = leftOut,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        };
        // The claim: of two requests with the same id, the database lets one in.
        if (!await TryAddSendAsync(send, ct))
            return await _db.ClubEventInvitationSends.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SendId && s.OrganizationId == organizationId, CancellationToken.None) is { } first
                ? Dto(first, alreadySent: true)
                : new Error("Invitation.Changed.Conflict", "These invitations are being sent from another window.");
        _db.ChangeTracker.Clear();

        var club = await ClubAsync(organizationId, CancellationToken.None);
        int queued = 0, refusedNow = 0;
        for (var i = 0; i < recipients.Count; i++)
        {
            var member = recipients[i].Member;
            // Not the request's token: the send is claimed, and a browser that hangs up must not leave it half sent.
            if (await InviteAsync(organizationId, userId, ev, club, send.Id, member, recipients[i].Email!, request.Letters, page,
                    now.AddMinutes(i / PerMinute), CancellationToken.None))
                queued++;
            else refusedNow++;
        }

        var saved = await _db.ClubEventInvitationSends.FirstAsync(s => s.Id == send.Id, CancellationToken.None);
        saved.Recipients = queued;
        saved.LeftOut = leftOut + refusedNow;
        saved.Status = ClubEventInvitationSendStatuses.Sent;
        saved.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("Invitations {SendId} to event {EventId}: {Queued} queued, {LeftOut} left out", send.Id, eventId, queued, saved.LeftOut);
        return Dto(saved, alreadySent: false);
    }

    /// <summary>
    /// One member's invitation: its row with the reply link's hash, the line in their
    /// communications and the email, together. False when the outbox refused the email (consent
    /// withdrawn, an address that bounced, since the count) or the member was invited meanwhile.
    /// </summary>
    private async Task<bool> InviteAsync(Guid organizationId, Guid userId, ClubEvent ev, Club club, Guid sendId, ClubMember member, string address,
        InvitationLetters? letters, string page, DateTime notBefore, CancellationToken ct)
    {
        var language = AppLanguages.ForMember(member.PreferredLanguage);
        var token = InvitationReplyToken.Create();
        var letter = LetterIn(letters, language);
        var values = ValuesFor(ev, club, language, member.FirstName, member.LastName, ReplyLink(page, token));
        var subject = Clip(OneLine(InvitationTemplate.Fill(letter.Subject, values)), MaxSubject);
        var body = InvitationTemplate.Fill(letter.Body, values);
        var now = DateTime.UtcNow;
        var invitation = new ClubEventInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EventId = ev.Id,
            MemberId = member.Id,
            SendId = sendId,
            MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200),
            Email = Clip(address, 320),
            Language = language,
            TokenHash = InvitationReplyToken.Hash(token),
            Answer = ClubEventInvitationAnswers.None,
            InvitedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var log = MessageRow(organizationId, userId, member, address, subject, body);

        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            _db.ClubEventInvitations.Add(invitation);
            _db.MemberMessages.Add(log);
            await _db.SaveChangesAsync(ct);
            var result = await _outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.Invitation,
                SourceId = invitation.Id,
                To = [address],
                RecipientName = invitation.MemberName,
                Language = language,
                Subject = subject,
                Body = body,
                CreatedByUserId = userId,
                ReplyToUserId = userId,
                MemberId = member.Id,
                Purpose = ConsentPurposes.Marketing,
                NotBeforeUtc = notBefore,
            }, ct);
            if (result.IsFailure)
            {
                // Someone who said stop, or bounced, between the count and now: the outbox refused
                // them, and they are not invited (their link would answer for an email never sent).
                _logger.LogInformation("Invitation to event {EventId}: member {MemberId} was not emailed: {Reason}", ev.Id, member.Id, result.Error.Description);
                if (transaction != null) await transaction.RollbackAsync(ct);
                else
                {
                    _db.ClubEventInvitations.Remove(invitation);
                    _db.MemberMessages.Remove(log);
                    await _db.SaveChangesAsync(ct);
                }
                return false;
            }
            invitation.OutboundMessageId = result.Value.Id;
            log.OutboundMessageId = result.Value.Id;
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Rolled back, row, line and email together: most likely another send invited the
            // member at the same moment (one invitation per member and event).
            _logger.LogWarning(ex, "Invitation to event {EventId}: member {MemberId} could not be invited", ev.Id, member.Id);
            if (transaction != null) await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
            // Each member is a unit of its own: nothing of this one stays tracked for the next.
            _db.ChangeTracker.Clear();
        }
    }

    /// <summary>The line in the member's communications that says they were invited, and how the email went.</summary>
    private static MemberMessage MessageRow(Guid organizationId, Guid userId, ClubMember member, string address, string subject, string body) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        MemberId = member.Id,
        MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200),
        Email = Clip(address, 256),
        Channel = CampaignChannels.Email,
        Category = "Event",
        Purpose = ConsentPurposes.Marketing,
        Subject = subject,
        Preview = Preview(body),
        Status = MemberMessageStatuses.Queued,
        CreatedByUserId = userId,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private async Task<bool> TryAddSendAsync(ClubEventInvitationSend send, CancellationToken ct)
    {
        // Across workspaces, on purpose: the id is the key of the whole table, so one taken in
        // any workspace cannot be added again. Only whether it exists is read.
        if (await _db.ClubEventInvitationSends.IgnoreQueryFilters().AnyAsync(s => s.Id == send.Id, ct)) return false;
        _db.ClubEventInvitationSends.Add(send);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            // Another request with the same id saved first, between the look and the save.
            _db.Entry(send).State = EntityState.Detached;
            if (await _db.ClubEventInvitationSends.IgnoreQueryFilters().AnyAsync(s => s.Id == send.Id, CancellationToken.None)) return false;
            throw;
        }
    }

    // ── who was invited ──────────────────────────────────────────────────────

    public async Task<Result<EventInvitationsDto>> ListAsync(Guid organizationId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var invitations = await _db.ClubEventInvitations.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.EventId == eventId)
            .ToListAsync(ct);
        var list = await RegistrationsAsync(organizationId, eventId, ct);
        var outboundIds = invitations.Where(i => i.OutboundMessageId != null).Select(i => i.OutboundMessageId!.Value).ToList();
        var deliveries = (await _db.OutboundMessages.AsNoTracking()
                .Where(m => outboundIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Status, m.DeliveryStatus, Opened = m.OpenedAtUtc != null })
                .ToListAsync(ct))
            .ToDictionary(m => m.Id);
        var waiting = list.Where(r => r.Status == RegistrationStatuses.Waitlisted)
            .OrderBy(r => r.RegisteredAtUtc).ThenBy(r => r.CreatedAtUtc).ThenBy(r => r.Id)
            .Select((r, index) => (r.Id, Position: index + 1))
            .ToDictionary(x => x.Id, x => x.Position);

        var rows = invitations.Select(i =>
        {
            var registration = LatestOf(list, i.MemberId);
            var state = StateOf(i, registration);
            var delivery = i.OutboundMessageId is Guid id ? deliveries.GetValueOrDefault(id) : null;
            return new EventInvitationDto(
                i.Id, i.MemberId, i.MemberName, i.Email, i.Language, state, i.InvitedAtUtc, i.AnsweredAtUtc, i.RemindedAtUtc,
                delivery == null ? null : MemberMessageStates.Of(MemberMessageStatuses.Queued, delivery.Status, delivery.DeliveryStatus, delivery.Opened),
                state == InvitationStates.Waitlist && registration != null ? waiting.GetValueOrDefault(registration.Id) : null);
        }).OrderBy(r => r.MemberName, StringComparer.OrdinalIgnoreCase).ToList();

        var open = Closed(ev) == null;
        int Count(string state) => rows.Count(r => r.State == state);
        var counts = new EventInvitationCountsDto(
            rows.Count, Count(InvitationStates.Coming), Count(InvitationStates.Waitlist), Count(InvitationStates.Declined),
            Count(InvitationStates.NoAnswer), Count(InvitationStates.Cancelled), Count(InvitationStates.Came), Count(InvitationStates.NoShow),
            invitations.Count(i => i.RemindedAtUtc != null),
            open ? rows.Count(r => r.State == InvitationStates.NoAnswer && r.RemindedAtUtc == null) : 0);
        return new EventInvitationsDto(rows, counts, open);
    }

    /// <summary>
    /// Where an invited member stands: their latest place on the event's list says it when it
    /// holds one (a seat, the waitlist, the door), so a registration staff made counts too;
    /// otherwise what they answered.
    /// </summary>
    private static string StateOf(ClubEventInvitation invitation, ClubEventRegistration? registration) => registration?.Status switch
    {
        RegistrationStatuses.CheckedIn => InvitationStates.Came,
        RegistrationStatuses.NoShow => InvitationStates.NoShow,
        RegistrationStatuses.Reserved => InvitationStates.Coming,
        RegistrationStatuses.Waitlisted => InvitationStates.Waitlist,
        _ => invitation.Answer switch
        {
            ClubEventInvitationAnswers.Declined => InvitationStates.Declined,
            // "I'll come", and the place was cancelled since: by the club, or with the event.
            ClubEventInvitationAnswers.Coming => InvitationStates.Cancelled,
            _ => InvitationStates.NoAnswer,
        },
    };

    /// <summary>A member's latest place on the event's list, any status.</summary>
    private static ClubEventRegistration? LatestOf(IEnumerable<ClubEventRegistration> list, Guid memberId) =>
        list.Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RegisteredAtUtc).ThenByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .FirstOrDefault();

    private Task<List<ClubEventRegistration>> RegistrationsAsync(Guid organizationId, Guid eventId, CancellationToken ct) =>
        _db.ClubEventRegistrations.AsNoTracking().Where(r => r.OrganizationId == organizationId && r.EventId == eventId).ToListAsync(ct);

    // ── the one reminder ─────────────────────────────────────────────────────

    /// <summary>The in-memory database the HTTP tests run on cannot claim with a conditional update; one claim at a time stands in.</summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    public async Task<Result<InvitationRemindDto>> RemindAsync(Guid organizationId, Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        if (Closed(ev) is { } closed) return new Error("Invitation.Closed.Conflict", $"{closed} No reminder was sent.");
        var page = ReplyPage();
        if (page == null) return new Error("Invitation.NoReplyPage", NoReplyPage);

        var invitations = await _db.ClubEventInvitations.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.EventId == eventId && i.Answer == ClubEventInvitationAnswers.None)
            .ToListAsync(ct);
        var list = await RegistrationsAsync(organizationId, eventId, ct);
        // Not answered, and not on the list some other way (staff put them there): those are the ones to remind.
        var waiting = invitations.Where(i => StateOf(i, LatestOf(list, i.MemberId)) == InvitationStates.NoAnswer).ToList();
        var already = waiting.Count(i => i.RemindedAtUtc != null);
        var members = (await _db.ClubMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && waiting.Select(i => i.MemberId).Contains(m.Id))
                .ToListAsync(ct))
            .ToDictionary(m => m.Id);
        var club = await ClubAsync(organizationId, ct);

        int reminded = 0, notSent = 0, index = 0;
        foreach (var invitation in waiting.Where(i => i.RemindedAtUtc == null).OrderBy(i => i.InvitedAtUtc))
        {
            // The claim: of two presses (or two windows), one reminds each member, once ever.
            if (!await ClaimReminderAsync(invitation.Id, DateTime.UtcNow, CancellationToken.None))
            {
                already++;
                continue;
            }
            if (!members.TryGetValue(invitation.MemberId, out var member) || member.ErasedAtUtc != null
                || !ValidationHelper.IsValidEmail((member.Email ?? "").Trim()))
            {
                notSent++;
                continue;
            }
            var sent = await RemindOneAsync(organizationId, userId, ev, club, invitation.Id, member, page, DateTime.UtcNow.AddMinutes(index / PerMinute), CancellationToken.None);
            if (sent) { reminded++; index++; }
            else notSent++;
        }
        _logger.LogInformation("Event {EventId}: {Reminded} reminder(s) queued, {Already} had one already, {NotSent} not sent", eventId, reminded, already, notSent);
        return new InvitationRemindDto(reminded, already, notSent);
    }

    /// <summary>
    /// Takes the member's one reminder: true for the caller who set it, false when it was taken
    /// already, or the member answered meanwhile.
    /// </summary>
    private async Task<bool> ClaimReminderAsync(Guid invitationId, DateTime now, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            return await _db.ClubEventInvitations
                .Where(i => i.Id == invitationId && i.RemindedAtUtc == null && i.Answer == ClubEventInvitationAnswers.None)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.RemindedAtUtc, now).SetProperty(i => i.UpdatedAtUtc, now), ct) == 1;
        }
        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.ClubEventInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct);
            if (row == null || row.RemindedAtUtc != null || row.Answer != ClubEventInvitationAnswers.None) return false;
            row.RemindedAtUtc = now;
            row.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            _db.ChangeTracker.Clear();
            InMemoryClaims.Release();
        }
    }

    /// <summary>The reminder with a reply link of its own (the first email's still works), its line in the member's communications, together.</summary>
    private async Task<bool> RemindOneAsync(Guid organizationId, Guid userId, ClubEvent ev, Club club, Guid invitationId, ClubMember member, string page,
        DateTime notBefore, CancellationToken ct)
    {
        var language = AppLanguages.ForMember(member.PreferredLanguage);
        var address = member.Email.Trim();
        var token = InvitationReplyToken.Create();
        var values = ValuesFor(ev, club, language, member.FirstName, member.LastName, ReplyLink(page, token));
        var subject = Clip(OneLine(InvitationTemplate.Fill(MemberTexts.T(language, "invite.reminderSubject"), values)), MaxSubject);
        var body = InvitationTemplate.Fill(MemberTexts.T(language, "invite.reminderBody"), values);
        var log = MessageRow(organizationId, userId, member, address, subject, body);
        var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var invitation = await _db.ClubEventInvitations.FirstAsync(i => i.Id == invitationId, ct);
            invitation.ReminderTokenHash = InvitationReplyToken.Hash(token);
            invitation.UpdatedAtUtc = DateTime.UtcNow;
            _db.MemberMessages.Add(log);
            await _db.SaveChangesAsync(ct);
            var result = await _outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.Invitation,
                SourceId = invitationId,
                To = [address],
                RecipientName = invitation.MemberName,
                Language = language,
                Subject = subject,
                Body = body,
                CreatedByUserId = userId,
                ReplyToUserId = userId,
                MemberId = member.Id,
                Purpose = ConsentPurposes.Marketing,
                NotBeforeUtc = notBefore,
            }, ct);
            if (result.IsFailure)
            {
                _logger.LogInformation("Event {EventId}: the reminder to member {MemberId} was not sent: {Reason}", ev.Id, member.Id, result.Error.Description);
                if (transaction != null) await transaction.RollbackAsync(ct);
                else
                {
                    // The claim stays (at most one reminder, and it was tried); the link and the line go.
                    invitation.ReminderTokenHash = null;
                    _db.MemberMessages.Remove(log);
                    await _db.SaveChangesAsync(ct);
                }
                return false;
            }
            invitation.ReminderMessageId = result.Value.Id;
            log.OutboundMessageId = result.Value.Id;
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Event {EventId}: the reminder to member {MemberId} could not be queued", ev.Id, member.Id);
            if (transaction != null) await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
            _db.ChangeTracker.Clear();
        }
    }

    // ── pieces ───────────────────────────────────────────────────────────────

    /// <summary>The workspace's name, region and time zone, as a letter and the reply page write dates and prices in.</summary>
    private sealed record Club(string? Name, string? Locale, TimeZoneInfo Zone);

    private async Task<Club> ClubAsync(Guid organizationId, CancellationToken ct)
    {
        // Organizations are not filtered to a workspace (they are how a person finds theirs).
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Name, o.Locale, o.TimeZone }).FirstOrDefaultAsync(ct);
        return new Club(org?.Name, org?.Locale, RegionalSettings.ZoneOf(org?.TimeZone));
    }

    private Task<ClubEvent?> EventAsync(Guid organizationId, Guid eventId, CancellationToken ct) =>
        _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == organizationId, ct);

    /// <summary>Why members can no longer be invited to the event, or answer: a draft, cancelled, over or started. Null while they can.</summary>
    private static string? Closed(ClubEvent ev) => ev.Status switch
    {
        ClubEventStatuses.Cancelled => "This event is cancelled.",
        ClubEventStatuses.Completed => "This event is over.",
        ClubEventStatuses.Draft => "This event is a draft: publish it before inviting members.",
        _ => DateTime.UtcNow >= ev.StartAtUtc ? "This event has started, so members can no longer answer an invitation to it." : null,
    };

    /// <summary>A member's values, in their language: the date in the club's time zone, the price in its number format.</summary>
    private static InvitationTemplate.Values ValuesFor(ClubEvent ev, Club club, string language, string? first, string? last, string replyLink)
    {
        var english = language == AppLanguages.English;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ev.StartAtUtc, DateTimeKind.Utc), club.Zone);
        var when = local.ToString(MemberTexts.T(language, "event.when"),
            english ? CultureInfo.GetCultureInfo("en-GB") : MemberTexts.Culture(language, club.Locale));
        var price = ev.MemberPrice <= 0
            ? MemberTexts.T(language, "invite.free")
            : $"{ev.Currency} {ev.MemberPrice.ToString("N2", english ? CultureInfo.InvariantCulture : NumberCulture(club.Locale))}";
        var firstName = (first ?? "").Trim();
        return new InvitationTemplate.Values(firstName, $"{firstName} {(last ?? "").Trim()}".Trim(), ev.Name, when,
            string.IsNullOrWhiteSpace(ev.Location) ? null : ev.Location.Trim(), price, replyLink, club.Name?.Trim());
    }

    /// <summary>The workspace's number format, or the default one when its locale is not known here.</summary>
    private static CultureInfo NumberCulture(string? locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(locale) ? RegionalSettings.DefaultLocale : locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(RegionalSettings.DefaultLocale);
        }
    }

    private static InvitationLetterDto DefaultLetter(string language) =>
        new(MemberTexts.T(language, "invite.subject"), MemberTexts.T(language, "invite.body"));

    /// <summary>The letter staff wrote in a language, its default where they left it blank.</summary>
    private static InvitationLetterDto LetterIn(InvitationLetters? letters, string language)
    {
        var written = language switch
        {
            AppLanguages.German => letters?.De,
            AppLanguages.French => letters?.Fr,
            _ => letters?.En,
        };
        var fallback = DefaultLetter(language);
        return new InvitationLetterDto(
            string.IsNullOrWhiteSpace(written?.Subject) ? fallback.Subject : written.Subject.Trim(),
            string.IsNullOrWhiteSpace(written?.Body) ? fallback.Body : written.Body.Trim());
    }

    /// <summary>What is wrong with the letters, in a sentence; when sending, each language must carry its reply link and fill every placeholder.</summary>
    private static Error? LettersProblem(InvitationLetters? letters, bool sending)
    {
        foreach (var language in AppLanguages.All)
        {
            var letter = LetterIn(letters, language);
            if (OneLine(letter.Subject).Length > MaxSubject)
                return new Error("Invitation.TooLong", $"The {LanguageName(language)} subject can be at most {MaxSubject} characters.");
            if ((letter.Body ?? "").Length > MaxBody)
                return new Error("Invitation.TooLong", $"The {LanguageName(language)} letter can be at most {MaxBody:N0} characters.");
            if (!sending) continue;
            if (InvitationTemplate.Unknown(letter.Subject, letter.Body) is { Count: > 0 } unknown) return Unfilled(unknown, language);
            if (!InvitationTemplate.HasReplyLink(letter.Body)) return NoReplyLink(language);
        }
        return null;
    }

    private static Error Unfilled(IReadOnlyList<string> unknown, string? language = null) => new("Invitation.Unfilled",
        $"Nothing fills {string.Join(", ", unknown)}{(language == null ? "" : $" in the {LanguageName(language)} letter")}. "
        + "Use one of the listed fields, or give it a fallback, like {{eventPlace|the club}}.");

    private static Error NoReplyLink(string language) => new("Invitation.NoReplyLink",
        $"The {LanguageName(language)} letter has no {{{{replyLink}}}}: members invited in it could not answer. Put it back in the letter.");

    private static string LanguageName(string language) => language switch
    {
        AppLanguages.German => "German",
        AppLanguages.French => "French",
        _ => "English",
    };

    private const string NoReplyPage =
        "Reply links open a page of this app, and the server does not know the app's public address (Email:FrontendBaseUrl). Ask whoever runs the server to set it.";

    /// <summary>The app's own address, where the reply page is: the same rule as the unsubscribe page's. Null when it is not a usable one.</summary>
    private string? ReplyPage()
    {
        var page = (_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/');
        return Uri.TryCreate(page, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host)
            ? page
            : null;
    }

    private static string ReplyLink(string page, string token) => $"{page}/invitation/{Uri.EscapeDataString(token)}";

    /// <summary>The link a preview and a test show: the reply page says it is a sample, and it answers for nobody.</summary>
    private string SampleLink() => $"{ReplyPage() ?? "https://example.com"}/invitation/sample";

    private static InvitationSendDto Dto(ClubEventInvitationSend s, bool alreadySent) =>
        new(s.Id, s.Status, s.Recipients, s.LeftOut, alreadySent, s.CreatedAtUtc);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>A subject on one line, as the outbox keeps it.</summary>
    private static string OneLine(string? text) =>
        string.Join(' ', (text ?? "").Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();

    /// <summary>The start of what it said, for the member's communications, as a campaign's line keeps it.</summary>
    private static string Preview(string body) => body.Length <= 140 ? body : body[..140];
}
