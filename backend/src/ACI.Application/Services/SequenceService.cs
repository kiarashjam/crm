using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

public interface ISequenceService
{
    Task<IReadOnlyList<SequenceDto>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<SequenceDto?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<SequenceDto> CreateAsync(Guid userId, Guid? organizationId, SequenceWriteRequest request, CancellationToken ct = default);
    Task<SequenceDto?> UpdateAsync(Guid id, Guid? organizationId, SequenceWriteRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default);
    Task<Result<EnrollmentDto>> EnrollAsync(Guid userId, Guid? organizationId, EnrollRequest request, CancellationToken ct = default);
    Task<Result> SetStatusAsync(Guid id, Guid? organizationId, string status, CancellationToken ct = default);
    Task<bool> UnenrollAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task RunDueAsync(CancellationToken ct = default);
}

/// <summary>
/// Outreach sequences: steps sent or set to someone on a schedule.
/// </summary>
/// <remarks>
/// <para>
/// This runs unattended and writes to real people, so each step re-checks the
/// things that make sending wrong instead of trusting what was true at enrollment:
/// the sequence still exists, the person still exists in this workspace, they are
/// not converted, lost, signed, archived or marked do-not-contact, and the address
/// is the one on their record today.
/// </para>
/// <para>
/// A step only advances once it has actually happened. A send that fails is tried
/// again later rather than recorded as sent, and an email is only logged against
/// the lead when it really went.
/// </para>
/// </remarks>
public sealed class SequenceService : ISequenceService
{
    /// <summary>How long a claimed step is held before another instance may retry it.</summary>
    internal static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(15);
    /// <summary>When a send fails, try again after this long.</summary>
    internal static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    /// <summary>The two states a person can set. The rest belong to the runner.</summary>
    private static readonly HashSet<string> SettableStatuses = new(StringComparer.OrdinalIgnoreCase) { "active", "paused" };

    /// <summary>
    /// Lead statuses that end outreach: they said no, or they signed. Includes the
    /// retired labels, which some leads still carry.
    /// </summary>
    private static readonly HashSet<string> FinishedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lost / Not Interested", "Signed", "Lost", "Unqualified", "Open Deal",
    };

    private readonly ISequenceRepository _sequences;
    private readonly IEmailSender _email;
    private readonly IOutreachMail _outreach;
    private readonly IActivityService _activities;
    private readonly ITaskService _tasks;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<SequenceService> _logger;

    public SequenceService(
        ISequenceRepository sequences,
        IEmailSender email,
        IOutreachMail outreach,
        IActivityService activities,
        ITaskService tasks,
        IOptions<EmailSettings> emailSettings,
        ILogger<SequenceService> logger)
    {
        _sequences = sequences;
        _email = email;
        _outreach = outreach;
        _activities = activities;
        _tasks = tasks;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SequenceDto>> ListAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _sequences.ListAsync(organizationId, ct)).Select(ToDto).ToList();

    public async Task<SequenceDto?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _sequences.GetAsync(id, organizationId, ct);
        return row == null ? null : ToDto(row);
    }

    public async Task<SequenceDto> CreateAsync(Guid userId, Guid? organizationId, SequenceWriteRequest request, CancellationToken ct = default)
    {
        var sequence = new OutreachSequence
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = (request.Name ?? "").Trim(),
            Description = request.Description?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "draft" : request.Status.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Steps = Steps(Guid.Empty, request.Steps),
        };
        foreach (var step in sequence.Steps) step.SequenceId = sequence.Id;
        await _sequences.SaveAsync(sequence, ct);
        return ToDto(sequence);
    }

    public async Task<SequenceDto?> UpdateAsync(Guid id, Guid? organizationId, SequenceWriteRequest request, CancellationToken ct = default)
    {
        var existing = await _sequences.GetAsync(id, organizationId, ct);
        if (existing == null) return null;
        existing.Name = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim();
        if (request.Description != null) existing.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status)) existing.Status = request.Status.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Steps != null)
        {
            existing.Steps = Steps(existing.Id, request.Steps);
        }
        await _sequences.SaveAsync(existing, ct);
        var saved = await _sequences.GetAsync(id, organizationId, ct);
        return saved == null ? ToDto(existing) : ToDto(saved);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await _sequences.GetAsync(id, organizationId, ct);
        if (existing == null) return false;
        // Stop the people in it first. Left "active", they were re-polled every hour
        // for ever against a sequence that no longer existed.
        await _sequences.StopEnrollmentsAsync(id, ct);
        await _sequences.DeleteAsync(id, organizationId, ct);
        return true;
    }

    public async Task<IReadOnlyList<EnrollmentDto>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default) =>
        (await _sequences.EnrollmentsAsync(organizationId, sequenceId, ct)).Select(ToDto).ToList();

    public async Task<Result<EnrollmentDto>> EnrollAsync(Guid userId, Guid? organizationId, EnrollRequest request, CancellationToken ct = default)
    {
        var sequence = await _sequences.GetAsync(request.SequenceId, organizationId, ct);
        if (sequence == null) return new Error("Sequence.NotFound", "That sequence does not exist.");

        var targetType = request.TargetType == "contact" ? "contact" : "lead";
        // The target is read from THIS workspace, and its address taken from its
        // record — never from the request, which could otherwise point a sequence at
        // any address at all under the workspace's name.
        var target = await _sequences.TargetStateAsync(targetType, request.TargetId, organizationId, ct);
        if (!target.Exists) return new Error("Sequence.Target.NotFound", $"That {targetType} does not exist.");
        if (StopReason(target) is { } reason) return new Error("Sequence.TargetStopped", $"Not enrolled: {reason}.");

        // Enrolling twice sent every email twice. Enrolling again is now a no-op that
        // returns the enrollment already running.
        var open = await _sequences.FindOpenEnrollmentAsync(sequence.Id, request.TargetId, ct);
        if (open != null) return ToDto(open);

        var first = sequence.Steps.OrderBy(s => s.SortOrder).FirstOrDefault();
        var enrollment = new OutreachEnrollment
        {
            Id = Guid.NewGuid(),
            SequenceId = sequence.Id,
            OrganizationId = organizationId,
            EnrolledByUserId = userId,
            TargetType = targetType,
            TargetId = request.TargetId,
            TargetName = (target.Name ?? request.TargetName ?? "").Trim(),
            TargetEmail = target.Email?.Trim(),
            Status = "active",
            CurrentStep = 0,
            EnrolledAtUtc = DateTime.UtcNow,
            NextActionAtUtc = DateTime.UtcNow.AddDays(first?.DayOffset ?? 0),
        };
        await _sequences.SaveEnrollmentAsync(enrollment, ct);
        return ToDto(enrollment);
    }

    public async Task<Result> SetStatusAsync(Guid id, Guid? organizationId, string status, CancellationToken ct = default)
    {
        var wanted = (status ?? "").Trim().ToLowerInvariant();
        if (!SettableStatuses.Contains(wanted))
            return new Error("Sequence.InvalidStatus", "An enrollment can be set to active or paused.");
        var row = await _sequences.GetEnrollmentAsync(id, organizationId, ct);
        if (row == null) return new Error("Sequence.Enrollment.NotFound", "That enrollment does not exist.");
        if (row.Status is "completed" or "stopped")
            return new Error("Sequence.EnrollmentFinished", "That enrollment has finished and cannot be restarted.");
        row.Status = wanted;
        await _sequences.SaveEnrollmentAsync(row, ct);
        return Result.Success();
    }

    public Task<bool> UnenrollAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _sequences.DeleteEnrollmentAsync(id, organizationId, ct);

    public async Task RunDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _sequences.DueAsync(now, 50, ct);
        foreach (var enrollment in due)
        {
            if (enrollment.NextActionAtUtc is not { } dueAt) continue;
            // Exactly one runner gets each step. Another instance that read the same
            // row moves on; so does this one, if it lost.
            var claimedUntil = now.Add(ClaimFor);
            if (!await _sequences.TryClaimAsync(enrollment.Id, enrollment.CurrentStep, dueAt, claimedUntil, ct)) continue;
            enrollment.NextActionAtUtc = claimedUntil;

            try
            {
                await RunStepAsync(enrollment, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad row must not stop the rest. The claim lapses and it is
                // tried again after ClaimFor.
                _logger.LogError(ex, "Sequence enrollment {EnrollmentId} step {Step} failed", enrollment.Id, enrollment.CurrentStep);
            }
        }
    }

    private async Task RunStepAsync(OutreachEnrollment enrollment, DateTime now, CancellationToken ct)
    {
        var sequence = await _sequences.GetAsync(enrollment.SequenceId, enrollment.OrganizationId, ct);
        if (sequence == null)
        {
            await StopAsync(enrollment, "the sequence no longer exists", ct);
            return;
        }
        if (!string.Equals(sequence.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            // Paused or draft: hold, and pick up again once it is active.
            enrollment.NextActionAtUtc = now.AddHours(1);
            await _sequences.SaveEnrollmentAsync(enrollment, ct);
            return;
        }

        var steps = sequence.Steps.OrderBy(s => s.SortOrder).ToList();
        if (enrollment.CurrentStep >= steps.Count)
        {
            Complete(enrollment);
            await _sequences.SaveEnrollmentAsync(enrollment, ct);
            return;
        }

        var target = await _sequences.TargetStateAsync(enrollment.TargetType, enrollment.TargetId, enrollment.OrganizationId, ct);
        if (StopReason(target) is { } reason)
        {
            await StopAsync(enrollment, reason, ct);
            return;
        }

        var step = steps[enrollment.CurrentStep];
        var outcome = await ExecuteStepAsync(sequence, enrollment, step, target, ct);
        if (outcome == StepOutcome.Retry)
        {
            enrollment.NextActionAtUtc = now.Add(RetryAfter);
            await _sequences.SaveEnrollmentAsync(enrollment, ct);
            return;
        }
        if (outcome == StepOutcome.Stop)
        {
            await StopAsync(enrollment, "there is no email address to send to", ct);
            return;
        }

        enrollment.CurrentStep += 1;
        if (enrollment.CurrentStep >= steps.Count) Complete(enrollment);
        else enrollment.NextActionAtUtc = now.AddDays(Math.Max(0, steps[enrollment.CurrentStep].DayOffset));
        await _sequences.SaveEnrollmentAsync(enrollment, ct);
    }

    /// <summary>Why outreach to this person must stop now, or null when it may continue.</summary>
    internal static string? StopReason(SequenceTargetState target)
    {
        if (!target.Exists) return "they are no longer in this workspace";
        if (target.DoNotContact) return "they are marked do not contact";
        if (target.IsArchived) return "they are archived";
        if (target.IsConverted) return "they have been converted";
        if (target.Status is { } status && FinishedStatuses.Contains(status.Trim())) return $"their status is {status.Trim()}";
        return null;
    }

    private enum StepOutcome { Done, Retry, Stop }

    private async Task<StepOutcome> ExecuteStepAsync(
        OutreachSequence sequence, OutreachEnrollment enrollment, OutreachStep step, SequenceTargetState target, CancellationToken ct)
    {
        var type = step.Type.Trim().ToLowerInvariant();
        if (type == "wait") return StepOutcome.Done;

        if (type == "email")
        {
            var to = target.Email?.Trim();
            if (string.IsNullOrWhiteSpace(to)) return StepOutcome.Stop;

            var name = target.Name ?? enrollment.TargetName;
            var subject = Fill(string.IsNullOrWhiteSpace(step.Subject) ? sequence.Name : step.Subject, name, target.CompanyName);
            var text = Fill(step.Body ?? "", name, target.CompanyName);
            var html = HtmlFor(text, enrollment.TargetType == "lead" ? TrackingUrl(enrollment.TargetId) : null);
            // Under the workspace's name, and a reply reaches whoever works the lead,
            // or else whoever enrolled them.
            var sender = await _outreach.SenderAsync(enrollment.OrganizationId, target.OwnerUserId ?? enrollment.EnrolledByUserId, ct);

            if (!await _email.SendOutreachAsync(new OutreachEmail(to, name, subject, text, html, sender.WorkspaceName, sender.ReplyTo), ct))
            {
                _logger.LogWarning("Sequence email for enrollment {EnrollmentId} was not delivered; retrying later", enrollment.Id);
                return StepOutcome.Retry;
            }
            await LogAsync(enrollment, "email", subject, text, ct);
            return StepOutcome.Done;
        }

        if (type is "task" or "call")
        {
            // The call goes to whoever works the lead now. It used to land on the
            // person who enrolled them, so the rep the lead was routed to never saw it.
            // With no owner, the enroller keeps it, as before.
            var created = await _tasks.CreateAsync(enrollment.EnrolledByUserId, enrollment.OrganizationId, new CreateTaskRequest
            {
                Title = string.IsNullOrWhiteSpace(step.TaskTitle) ? step.Type : step.TaskTitle.Trim(),
                Description = step.Note,
                DueDateUtc = DateTime.UtcNow,
                LeadId = enrollment.TargetType == "lead" ? enrollment.TargetId : null,
                ContactId = enrollment.TargetType == "contact" ? enrollment.TargetId : null,
                AssigneeId = target.OwnerUserId,
            }, ct);
            return created.IsSuccess ? StepOutcome.Done : StepOutcome.Retry;
        }

        await LogAsync(enrollment, "note", step.Subject ?? step.TaskTitle ?? sequence.Name, step.Body, ct);
        return StepOutcome.Done;
    }

    /// <summary>Record what the sequence did, as automated — it is not a person responding.</summary>
    private async Task LogAsync(OutreachEnrollment enrollment, string type, string? subject, string? body, CancellationToken ct)
    {
        await _activities.CreateAsync(enrollment.EnrolledByUserId, enrollment.OrganizationId, new CreateActivityRequest
        {
            Type = type,
            Subject = subject,
            Body = body,
            LeadId = enrollment.TargetType == "lead" ? enrollment.TargetId : null,
            ContactId = enrollment.TargetType == "contact" ? enrollment.TargetId : null,
            IsAutomated = true,
        }, ct);
    }

    private async Task StopAsync(OutreachEnrollment enrollment, string reason, CancellationToken ct)
    {
        _logger.LogInformation("Stopping sequence enrollment {EnrollmentId}: {Reason}", enrollment.Id, reason);
        enrollment.Status = "stopped";
        enrollment.NextActionAtUtc = null;
        await _sequences.SaveEnrollmentAsync(enrollment, ct);
    }

    private static void Complete(OutreachEnrollment enrollment)
    {
        enrollment.Status = "completed";
        enrollment.NextActionAtUtc = null;
    }

    /// <summary>
    /// Fills {{firstName}}, {{name}} and {{company}}. Only {{firstName}} used to be
    /// filled, and only in the body: "{{company}}" — which the editor suggests — and
    /// any placeholder in a subject went out to the prospect literally.
    /// </summary>
    internal static string Fill(string template, string? name, string? company) =>
        template
            .Replace("{{firstName}}", FirstName(name ?? ""), StringComparison.OrdinalIgnoreCase)
            .Replace("{{name}}", (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{company}}", (company ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The email-open pixel URL, or null when this API has no public address to give.</summary>
    internal string? TrackingUrl(Guid leadId)
    {
        // The setting defaults to http://localhost:5160, and nothing in production
        // sets it. A pixel pointing there can never be fetched by a recipient, so it
        // is left out rather than shipped broken.
        if (!Uri.TryCreate((_emailSettings.ApiBaseUrl ?? "").Trim(), UriKind.Absolute, out var api)) return null;
        if (api.IsLoopback || api.Scheme != Uri.UriSchemeHttps) return null;
        return $"{api.GetLeftPart(UriPartial.Authority)}{api.AbsolutePath.TrimEnd('/')}/api/public/track/{leadId}";
    }

    /// <summary>
    /// The HTML part: the same words, escaped, with line breaks kept — and the
    /// tracking image, which exists only here. It used to be appended to the text
    /// body, and since the message was sent as plain text every recipient saw
    /// &lt;img src="http://localhost:5160/api/public/track/…"&gt; at the bottom.
    /// </summary>
    internal static string HtmlFor(string text, string? trackingUrl) => PlainTextEmail.ToHtml(text, trackingUrl);

    private static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }

    private static List<OutreachStep> Steps(Guid sequenceId, IReadOnlyList<SequenceStepDto>? steps) =>
        (steps ?? Array.Empty<SequenceStepDto>()).Select((s, i) => new OutreachStep
        {
            Id = Guid.NewGuid(),
            SequenceId = sequenceId,
            SortOrder = i,
            Type = string.IsNullOrWhiteSpace(s.Type) ? "email" : s.Type.Trim(),
            DayOffset = Math.Max(0, s.DayOffset),
            Subject = s.Subject,
            Body = s.Body,
            TaskTitle = s.TaskTitle,
            Note = s.Note,
        }).ToList();

    private static SequenceDto ToDto(OutreachSequence s) =>
        new(s.Id, s.Name, s.Description, s.Status, s.CreatedAtUtc, s.UpdatedAtUtc,
            s.Steps.OrderBy(x => x.SortOrder).Select(x => new SequenceStepDto(x.Id, x.SortOrder, x.Type, x.DayOffset, x.Subject, x.Body, x.TaskTitle, x.Note)).ToList());

    private static EnrollmentDto ToDto(OutreachEnrollment e) =>
        new(e.Id, e.SequenceId, e.TargetType, e.TargetId, e.TargetName, e.TargetEmail, e.Status, e.CurrentStep, e.EnrolledAtUtc, e.NextActionAtUtc);
}
