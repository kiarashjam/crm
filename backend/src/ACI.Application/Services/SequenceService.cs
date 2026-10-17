using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
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
    Task<EnrollmentDto?> EnrollAsync(Guid userId, Guid? organizationId, EnrollRequest request, CancellationToken ct = default);
    Task<bool> SetStatusAsync(Guid id, string status, CancellationToken ct = default);
    Task<bool> UnenrollAsync(Guid id, CancellationToken ct = default);
    Task RunDueAsync(CancellationToken ct = default);
}

public sealed class SequenceService : ISequenceService
{
    private readonly ISequenceRepository _sequences;
    private readonly IEmailSender _email;
    private readonly IActivityService _activities;
    private readonly ITaskService _tasks;
    private readonly EmailSettings _emailSettings;

    public SequenceService(
        ISequenceRepository sequences,
        IEmailSender email,
        IActivityService activities,
        ITaskService tasks,
        IOptions<EmailSettings> emailSettings)
    {
        _sequences = sequences;
        _email = email;
        _activities = activities;
        _tasks = tasks;
        _emailSettings = emailSettings.Value;
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
            Name = request.Name.Trim(),
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
        await _sequences.DeleteAsync(id, organizationId, ct);
        return true;
    }

    public async Task<IReadOnlyList<EnrollmentDto>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default) =>
        (await _sequences.EnrollmentsAsync(organizationId, sequenceId, ct)).Select(ToDto).ToList();

    public async Task<EnrollmentDto?> EnrollAsync(Guid userId, Guid? organizationId, EnrollRequest request, CancellationToken ct = default)
    {
        var sequence = await _sequences.GetAsync(request.SequenceId, organizationId, ct);
        if (sequence == null) return null;
        var first = sequence.Steps.OrderBy(s => s.SortOrder).FirstOrDefault();
        var enrollment = new OutreachEnrollment
        {
            Id = Guid.NewGuid(),
            SequenceId = sequence.Id,
            OrganizationId = organizationId,
            EnrolledByUserId = userId,
            TargetType = request.TargetType == "contact" ? "contact" : "lead",
            TargetId = request.TargetId,
            TargetName = request.TargetName.Trim(),
            TargetEmail = request.TargetEmail?.Trim(),
            Status = "active",
            CurrentStep = 0,
            EnrolledAtUtc = DateTime.UtcNow,
            NextActionAtUtc = DateTime.UtcNow.AddDays(first?.DayOffset ?? 0),
        };
        await _sequences.SaveEnrollmentAsync(enrollment, ct);
        return ToDto(enrollment);
    }

    public async Task<bool> SetStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var row = await _sequences.GetEnrollmentAsync(id, ct);
        if (row == null) return false;
        row.Status = status.Trim();
        await _sequences.SaveEnrollmentAsync(row, ct);
        return true;
    }

    public async Task<bool> UnenrollAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _sequences.GetEnrollmentAsync(id, ct);
        if (row == null) return false;
        await _sequences.DeleteEnrollmentAsync(id, ct);
        return true;
    }

    public async Task RunDueAsync(CancellationToken ct = default)
    {
        var due = await _sequences.DueAsync(DateTime.UtcNow, 50, ct);
        foreach (var enrollment in due)
        {
            var sequence = await _sequences.GetAsync(enrollment.SequenceId, enrollment.OrganizationId, ct);
            if (sequence == null || !string.Equals(sequence.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                enrollment.NextActionAtUtc = DateTime.UtcNow.AddHours(1);
                await _sequences.SaveEnrollmentAsync(enrollment, ct);
                continue;
            }

            var steps = sequence.Steps.OrderBy(s => s.SortOrder).ToList();
            if (enrollment.CurrentStep >= steps.Count)
            {
                enrollment.Status = "completed";
                enrollment.NextActionAtUtc = null;
                await _sequences.SaveEnrollmentAsync(enrollment, ct);
                continue;
            }

            var step = steps[enrollment.CurrentStep];
            await ExecuteStepAsync(sequence, enrollment, step, ct);
            enrollment.CurrentStep += 1;
            if (enrollment.CurrentStep >= steps.Count)
            {
                enrollment.Status = "completed";
                enrollment.NextActionAtUtc = null;
            }
            else
            {
                var next = steps[enrollment.CurrentStep];
                enrollment.NextActionAtUtc = DateTime.UtcNow.AddDays(Math.Max(0, next.DayOffset));
            }
            await _sequences.SaveEnrollmentAsync(enrollment, ct);
        }
    }

    private async Task ExecuteStepAsync(OutreachSequence sequence, OutreachEnrollment enrollment, OutreachStep step, CancellationToken ct)
    {
        var type = step.Type.Trim().ToLowerInvariant();
        if (type == "wait") return;

        if (type == "email" && !string.IsNullOrWhiteSpace(enrollment.TargetEmail))
        {
            var body = (step.Body ?? "").Replace("{{firstName}}", FirstName(enrollment.TargetName), StringComparison.OrdinalIgnoreCase);
            var api = (_emailSettings.ApiBaseUrl ?? "").Trim().TrimEnd('/');
            if (enrollment.TargetType == "lead" && api.Length > 0)
                body += $"\n\n<img src=\"{api}/api/public/track/{enrollment.TargetId}\" width=\"1\" height=\"1\" alt=\"\" />";
            await _email.SendMessageAsync(enrollment.TargetEmail, enrollment.TargetName, step.Subject ?? sequence.Name, body, ct);
        }

        if (type is "task" or "call")
        {
            await _tasks.CreateAsync(enrollment.EnrolledByUserId, enrollment.OrganizationId, new CreateTaskRequest
            {
                Title = string.IsNullOrWhiteSpace(step.TaskTitle) ? step.Type : step.TaskTitle.Trim(),
                Description = step.Note,
                DueDateUtc = DateTime.UtcNow,
                LeadId = enrollment.TargetType == "lead" ? enrollment.TargetId : null,
                ContactId = enrollment.TargetType == "contact" ? enrollment.TargetId : null,
            }, ct);
            return;
        }

        if (enrollment.TargetType == "lead")
        {
            await _activities.CreateAsync(enrollment.EnrolledByUserId, enrollment.OrganizationId, new CreateActivityRequest
            {
                Type = type == "email" ? "email" : "note",
                Subject = step.Subject ?? step.TaskTitle ?? sequence.Name,
                Body = step.Body,
                LeadId = enrollment.TargetId,
            }, ct);
        }
    }

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
