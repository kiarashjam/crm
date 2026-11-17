using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Meetings leads book on the workspace's website (with the link from a newsletter, or
/// without one), reported back here and put on the lead.
/// </summary>
public interface IMeetingBookingService
{
    /// <summary>
    /// A booking or a cancellation the website posted, with its raw body and the two signing
    /// headers. Anything not signed by the workspace's onboarding secret, within ten minutes,
    /// is refused with General.Unauthorized and nothing more said.
    /// </summary>
    Task<Result<MeetingReportReceipt>> ReportAsync(string body, string? timestamp, string? signature, CancellationToken ct = default);
}

/// <remarks>
/// <para>
/// The website signs its post as the CRM signs its handoffs (<see cref="WebhookSigning"/>): the
/// HMAC-SHA256, with the workspace's onboarding secret, of <c>"{timestamp}.{body}"</c>. The
/// organization in the body says whose secret to check it with; until the signature is
/// checked, nothing is read or written for it but its settings.
/// </para>
/// <para>
/// The meeting goes on the lead named by its id, else the lead with its email, else a new
/// lead. It is written on the lead's timeline as a meeting at the time it is booked for, so
/// it shows on the calendar there, with the code and what the person wrote; the lead's
/// pipeline records the meeting (a lead past that step keeps its pipeline as it is), and the
/// lead's owner is told. One row per booking the website names makes a report sent again
/// change nothing; a cancellation marks the meeting cancelled and tells the owner.
/// </para>
/// </remarks>
public sealed class MeetingBookingService : IMeetingBookingService
{
    /// <summary>How far the website's clock may be from ours, either way, for its post to count.</summary>
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMeetingBookingRepository _bookings;
    private readonly IOrganizationRepository _organizations;
    private readonly IOnboardingRepository _onboarding;
    private readonly ILeadRepository _leads;
    private readonly ILeadService _leadService;
    private readonly ILeadStatusSync _statusSync;
    private readonly IActivityRepository _activities;
    private readonly INotificationService _notifications;
    private readonly INotificationRepository _sentNotifications;
    private readonly ITenantContext _tenant;
    private readonly ILogger<MeetingBookingService> _logger;

    public MeetingBookingService(
        IMeetingBookingRepository bookings,
        IOrganizationRepository organizations,
        IOnboardingRepository onboarding,
        ILeadRepository leads,
        ILeadService leadService,
        ILeadStatusSync statusSync,
        IActivityRepository activities,
        INotificationService notifications,
        INotificationRepository sentNotifications,
        ITenantContext tenant,
        ILogger<MeetingBookingService> logger)
    {
        _bookings = bookings;
        _organizations = organizations;
        _onboarding = onboarding;
        _leads = leads;
        _leadService = leadService;
        _statusSync = statusSync;
        _activities = activities;
        _notifications = notifications;
        _sentNotifications = sentNotifications;
        _tenant = tenant;
        _logger = logger;
    }

    private static readonly Error Refused = new("General.Unauthorized", "Unauthorized");

    public async Task<Result<MeetingReportReceipt>> ReportAsync(string body, string? timestamp, string? signature, CancellationToken ct = default)
    {
        // ── Who is asking: the same refusal for every way of being wrong ──
        MeetingReportRequest? report;
        try
        {
            report = JsonSerializer.Deserialize<MeetingReportRequest>(body, Json);
        }
        catch (JsonException)
        {
            return Refused;
        }
        if (report?.OrganizationId is not Guid organizationId) return Refused;
        if (!long.TryParse(timestamp?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var sentAt)) return Refused;
        if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - sentAt) > (long)MaxSkew.TotalSeconds) return Refused;
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        if (workspace == null) return Refused;

        // Nobody signs in here: the secret named the workspace, and the meeting is written as its owner.
        using var acting = _tenant.ActFor(workspace.Id, workspace.OwnerUserId);
        var settings = await _onboarding.GetSettingsAsync(workspace.Id, ct);
        if (string.IsNullOrWhiteSpace(settings?.WebhookSecret) || !WebhookSigning.IsSignature(settings.WebhookSecret, sentAt, body, signature))
            return Refused;

        // ── What it says ──
        if (Problem(report) is { } problem) return problem;
        var bookingId = report.BookingId!.Trim();
        var cancelled = report.Status!.Trim().Equals(MeetingBookingStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
        // A test email's invitation (no lead): the website may take the booking; the CRM records nothing.
        if (report.LeadId == Guid.Empty) return new MeetingReportReceipt("test", null, null);

        var site = Site(settings.BookingPageUrl);
        var existing = await _bookings.FindAsync(workspace.Id, bookingId, ct);
        if (existing != null)
        {
            // Claimed by an earlier report that did not finish (it failed, or the website gave up
            // waiting): this one, the website's retry, finishes it.
            if (existing.ActivityId == null)
            {
                var claimedFor = await _leads.GetByIdAsync(existing.LeadId, workspace.OwnerUserId, workspace.Id, CancellationToken.None);
                if (claimedFor == null) return new MeetingReportReceipt("unchanged", existing.LeadId, null);
                await CompleteAsync(workspace, existing, claimedFor, report, site, created: false);
                if (cancelled && existing.Status != MeetingBookingStatuses.Cancelled) return await CancelAsync(workspace, existing, report, ct);
                return new MeetingReportReceipt(existing.Status == MeetingBookingStatuses.Cancelled ? "cancelled" : "recorded", claimedFor.Id, existing.ActivityId);
            }
            if (!cancelled || existing.Status == MeetingBookingStatuses.Cancelled)
                return new MeetingReportReceipt("unchanged", existing.LeadId, existing.ActivityId);
            return await CancelAsync(workspace, existing, report, ct);
        }

        var (lead, created) = await LeadAsync(workspace, report, site, createIfNone: !cancelled, ct);
        if (lead == null) return new MeetingReportReceipt("unchanged", null, null);

        var now = DateTime.UtcNow;
        var starts = report.StartsAt!.Value;
        var booking = new MeetingBooking
        {
            Id = Guid.NewGuid(),
            OrganizationId = workspace.Id,
            LeadId = lead.Id,
            BookingId = bookingId,
            Code = Clip(Blank(report.Code), 16),
            StartsAtUtc = starts.UtcDateTime,
            OffsetMinutes = (int)starts.Offset.TotalMinutes,
            DurationMinutes = report.DurationMinutes,
            Status = cancelled ? MeetingBookingStatuses.Cancelled : MeetingBookingStatuses.Booked,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _bookings.Add(booking);
        try
        {
            // The claim: a report sent twice at once finds the booking (unique per workspace) and stops here.
            await _bookings.SaveAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (await _bookings.FindAsync(workspace.Id, bookingId, CancellationToken.None) is not { } first) throw;
            _logger.LogInformation(ex, "Meeting booking {BookingId} was reported twice at once; the second report changed nothing", bookingId);
            return new MeetingReportReceipt("unchanged", first.LeadId, first.ActivityId);
        }

        await CompleteAsync(workspace, booking, lead, report, site, created);
        return new MeetingReportReceipt(cancelled ? "cancelled" : "recorded", lead.Id, booking.ActivityId, created);
    }

    /// <summary>
    /// What follows the claim, each step safe to take again: the meeting on the lead's timeline
    /// (its id is the booking's, so a second try finds it), the lead's pipeline, the owner told
    /// (once: the notification's key is the booking's), and last the booking marked done. A report
    /// that stops part-way leaves the booking not done, and the website's retry finishes it.
    /// </summary>
    /// <remarks>
    /// Not the request's token: the booking is claimed, and a website that stops waiting must not
    /// leave it half recorded.
    /// </remarks>
    private async Task CompleteAsync(Organization workspace, MeetingBooking booking, Lead lead, MeetingReportRequest report, string? site, bool created)
    {
        var ct = CancellationToken.None;
        var cancelled = booking.Status == MeetingBookingStatuses.Cancelled;
        var owner = await OwnerAsync(workspace, lead, ct);
        var name = NameOf(report, lead);
        if (await _activities.GetByIdAsync(booking.Id, owner, workspace.Id, ct) == null)
        {
            await _activities.AddAsync(new Activity
            {
                Id = booking.Id,
                UserId = owner,
                OrganizationId = workspace.Id,
                Type = "meeting",
                // At the time it is booked for, so the calendar shows it there; the rule that an
                // activity is never ahead of now is for what people log by hand.
                OccurredAtUtc = booking.StartsAtUtc,
                DurationMinutes = booking.DurationMinutes,
                Outcome = cancelled ? "cancelled" : null,
                Subject = Clip((cancelled ? "Cancelled: " : "") + Title(name, booking.Code), 200),
                Body = Details(report, booking, site, cancelled),
                Participants = Clip(string.IsNullOrWhiteSpace(report.Email) ? name : $"{name} <{report.Email.Trim()}>", 500),
                LeadId = lead.Id,
                CreatedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        await RecordInPipelineAsync(workspace, lead.Id, owner, booking, cancelled, ct);
        await NotifyAsync(workspace, owner, lead.Id, booking,
            cancelled ? $"Meeting cancelled: {name}" : $"Meeting booked: {name}",
            $"{When(booking)}{(booking.Code == null ? "" : $" · code {booking.Code}")}{(created ? " · a new lead" : "")}",
            ct);

        booking.ActivityId = booking.Id;
        booking.UpdatedAtUtc = DateTime.UtcNow;
        await _bookings.SaveAsync(ct);
    }

    /// <summary>The meeting marked cancelled on the timeline, the pipeline told, and the owner too.</summary>
    private async Task<Result<MeetingReportReceipt>> CancelAsync(Organization workspace, MeetingBooking booking, MeetingReportRequest report, CancellationToken ct)
    {
        booking.Status = MeetingBookingStatuses.Cancelled;
        booking.UpdatedAtUtc = DateTime.UtcNow;
        await _bookings.SaveAsync(ct);

        var lead = await _leads.GetByIdAsync(booking.LeadId, workspace.OwnerUserId, workspace.Id, CancellationToken.None);
        var name = lead == null ? NameOf(report, null) : NameOf(report, lead);
        Guid owner = lead == null ? workspace.OwnerUserId : await OwnerAsync(workspace, lead, CancellationToken.None);
        if (booking.ActivityId is Guid activityId
            && await _activities.GetByIdAsync(activityId, owner, workspace.Id, CancellationToken.None) is { } meeting)
        {
            meeting.Outcome = "cancelled";
            if (meeting.Subject?.StartsWith("Cancelled: ", StringComparison.Ordinal) != true)
                meeting.Subject = Clip("Cancelled: " + (meeting.Subject ?? Title(name, booking.Code)), 200);
            meeting.Body = Clip($"{meeting.Body}\n\nCancelled on the website on {DateTime.UtcNow.ToString("d MMMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture)}.", 20_000);
            meeting.UpdatedAtUtc = DateTime.UtcNow;
            await _activities.UpdateAsync(meeting, owner, workspace.Id, CancellationToken.None);
        }
        if (lead != null)
        {
            await RecordInPipelineAsync(workspace, lead.Id, owner, booking, cancelled: true, CancellationToken.None);
            await NotifyAsync(workspace, owner, lead.Id, booking, $"Meeting cancelled: {name}",
                $"{When(booking)}{(booking.Code == null ? "" : $" · code {booking.Code}")}", CancellationToken.None);
        }
        return new MeetingReportReceipt("cancelled", booking.LeadId, booking.ActivityId);
    }

    /// <summary>The lead named by its id, else the one with the email, else (for a booking) a new one.</summary>
    private async Task<(Lead? Lead, bool Created)> LeadAsync(Organization workspace, MeetingReportRequest report, string? site, bool createIfNone, CancellationToken ct)
    {
        if (report.LeadId is Guid leadId && await _leads.GetByIdAsync(leadId, workspace.OwnerUserId, workspace.Id, ct) is { } byId)
            return (byId, false);
        var email = Blank(report.Email);
        if (email != null && await _bookings.LeadByEmailAsync(workspace.Id, email, ct) is { } byEmail)
            return (byEmail, false);
        if (!createIfNone || email == null) return (null, false);

        var starts = report.StartsAt!.Value;
        var created = await _leadService.CreateAsync(workspace.OwnerUserId, workspace.Id, new CreateLeadRequest
        {
            Name = Clip(NameOf(report, null), 200)!,
            Email = email,
            Phone = Clip(Blank(report.Phone), 50),
            Status = "New",
            Source = Clip(site == null ? "Rendez-vous" : $"Rendez-vous {site}", 64),
            Description = Clip(Blank(report.Message), 2000),
            // The meeting it booked is its first step, and its status follows from it.
            PipelineState = Pipeline(null, starts, cancelled: false),
        }, ct);
        if (created.IsSuccess && await _leads.GetByIdAsync(created.Value.Id, workspace.OwnerUserId, workspace.Id, ct) is { } lead)
            return (lead, true);
        // Made a moment ago by a report sent twice at once: that lead, then.
        if (await _bookings.LeadByEmailAsync(workspace.Id, email, ct) is { } meanwhile) return (meanwhile, false);
        _logger.LogWarning("A meeting booked on the website could not make its lead: {Error}", created.IsFailure ? created.Error.Description : "not found after it was made");
        return (null, false);
    }

    /// <summary>The meeting in the lead's pipeline, so its sales tracker and status show it; a lead past that step is left as it is.</summary>
    private async Task RecordInPipelineAsync(Organization workspace, Guid leadId, Guid actor, MeetingBooking booking, bool cancelled, CancellationToken ct)
    {
        try
        {
            var starts = new DateTimeOffset(DateTime.SpecifyKind(booking.StartsAtUtc, DateTimeKind.Utc)).ToOffset(TimeSpan.FromMinutes(booking.OffsetMinutes));
            for (var attempt = 1; ; attempt++)
            {
                var lead = await _leads.GetByIdAsync(leadId, actor, workspace.Id, ct);
                if (lead == null) return;
                var state = Pipeline(lead.PipelineState, starts, cancelled);
                if (state == null || state == lead.PipelineState) return;
                lead.PipelineState = state;
                try
                {
                    await _statusSync.SaveAsync(lead, actor, workspace.Id, changedByUserId: null, ct);
                    return;
                }
                catch (ConcurrencyConflictException) when (attempt < 3)
                {
                    // Saved by someone else meanwhile: the steps go on the newer copy.
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The meeting is on the timeline; the pipeline is a summary of it.
            _logger.LogWarning(ex, "Meeting booking {BookingId}: lead {LeadId}'s pipeline could not be brought up to date", booking.BookingId, leadId);
        }
    }

    /// <summary>
    /// The lead's pipeline with the meeting in it (contacted, meeting scheduled, on its day), or
    /// without it once cancelled; null when the lead is past the meeting step, or the pipeline
    /// cannot be read, and is left as it is.
    /// </summary>
    public static string? Pipeline(string? current, DateTimeOffset starts, bool cancelled)
    {
        JsonObject state;
        try
        {
            state = string.IsNullOrWhiteSpace(current) ? new JsonObject() : JsonNode.Parse(current)?.AsObject() ?? new JsonObject();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
        // Met already, or on to the contract: this booking is another meeting, which the timeline holds.
        if (state["meetingAttended"] is JsonValue || state["contractStatus"] is JsonValue) return null;
        var day = starts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cancelled)
        {
            if (state["meetingDate"]?.GetValueKind() != JsonValueKind.String || state["meetingDate"]!.GetValue<string>() != day) return null;
            state.Remove("meetingDate");
            state["contactOutcome"] = "follow_up";
            return state.ToJsonString();
        }
        state["outreachStatus"] = "contacted";
        if (state["outreachDate"] is not JsonValue) state["outreachDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        state["contactOutcome"] = "meeting_scheduled";
        state["meetingDate"] = day;
        return state.ToJsonString();
    }

    /// <summary>The lead's owner while they are in the workspace, else whoever made it, else the workspace's owner.</summary>
    private async Task<Guid> OwnerAsync(Organization workspace, Lead lead, CancellationToken ct)
    {
        foreach (var candidate in new[] { lead.AssignedToUserId, (Guid?)lead.UserId })
            if (candidate is Guid id && await _organizations.GetMemberRoleAsync(id, workspace.Id, ct) != null) return id;
        return workspace.OwnerUserId;
    }

    private async Task NotifyAsync(Organization workspace, Guid owner, Guid leadId, MeetingBooking booking, string title, string message, CancellationToken ct)
    {
        try
        {
            var key = $"meeting:{booking.Id:N}:{booking.Status}";
            // Told once: a retry that finishes a report finds the first one's notification.
            if ((await _sentNotifications.GetExistingSourceKeysAsync(owner, [key], ct)).Count > 0) return;
            await _notifications.CreateAsync(owner, workspace.Id, new CreateNotificationRequest(
                "lead", Clip(title, 200)!, Clip(message, 1000), $"/leads/{leadId}", SourceKey: key), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Meeting booking {BookingId}: the lead's owner could not be told", booking.BookingId);
        }
    }

    private static Error? Problem(MeetingReportRequest r)
    {
        var bookingId = r.BookingId?.Trim() ?? "";
        if (bookingId.Length == 0 || bookingId.Length > 100) return new Error("Meeting.BookingId", "bookingId is required, at most 100 characters.");
        var status = r.Status?.Trim().ToLowerInvariant();
        if (status is not (MeetingBookingStatuses.Booked or MeetingBookingStatuses.Cancelled))
            return new Error("Meeting.Status", "status is \"booked\" or \"cancelled\".");
        if (r.StartsAt is not DateTimeOffset starts || starts.Year < 2000 || starts.Year > 2100)
            return new Error("Meeting.StartsAt", "startsAt is the meeting's start, in ISO 8601 with its offset.");
        if (r.DurationMinutes is int minutes && (minutes < 0 || minutes > ActivityFacts.MaxDurationMinutes))
            return new Error("Meeting.Duration", "durationMinutes is between 0 and 1440.");
        if (Blank(r.Email) is { } email && !ValidationHelper.IsValidEmail(email))
            return new Error("Meeting.Email", "email is not an email address.");
        if ((r.Message?.Length ?? 0) > 4000) return new Error("Meeting.Message", "message is at most 4000 characters.");
        if (r.LeadId == null && Blank(r.Email) == null)
            return new Error("Meeting.Who", "leadId or email is required.");
        return null;
    }

    /// <summary>The booking page's site, as people say it: "pavillon46.ch".</summary>
    private static string? Site(string? bookingPage)
    {
        if (!Uri.TryCreate(bookingPage?.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return null;
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static string NameOf(MeetingReportRequest r, Lead? lead)
    {
        var given = string.Join(' ', new[] { Blank(r.FirstName), Blank(r.LastName) }.Where(p => p != null));
        if (given.Length > 0) return given;
        if (lead != null) return LeadKindRules.PersonOf(lead).Name ?? lead.Name;
        var email = Blank(r.Email) ?? "";
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : "Someone";
    }

    private static string Title(string name, string? code) => code == null ? $"Meeting with {name}" : $"Meeting with {name} ({code})";

    /// <summary>"Thursday 3 December 2026, 14:30–15:15 (UTC+01:00)": as the person booked it.</summary>
    private static string When(MeetingBooking b)
    {
        var offset = TimeSpan.FromMinutes(b.OffsetMinutes);
        var start = new DateTimeOffset(DateTime.SpecifyKind(b.StartsAtUtc, DateTimeKind.Utc)).ToOffset(offset);
        var text = start.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        if (b.DurationMinutes is int minutes && minutes > 0) text += "–" + start.AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"{text} (UTC{sign}{offset.Duration():hh\\:mm})";
    }

    private static string Details(MeetingReportRequest r, MeetingBooking b, string? site, bool cancelled)
    {
        var lines = new List<string?>
        {
            cancelled ? $"Booked and then cancelled on {site ?? "the website"}." : $"Booked on {site ?? "the website"}.",
            $"When: {When(b)}",
            b.Code == null ? null : $"Booking code: {b.Code}",
            Blank(r.Email) is { } email ? $"Email: {email}" : null,
            Blank(r.Phone) is { } phone ? $"Phone: {phone}" : null,
            Blank(r.Language) is { } language ? $"Language: {(language.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? "French" : language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "English" : language)}" : null,
            Blank(r.Message) is { } message ? $"Message:\n{message}" : null,
            $"Booking reference: {b.BookingId}",
        };
        return Clip(string.Join("\n", lines.Where(l => l != null)), 20_000)!;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Clip(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}
