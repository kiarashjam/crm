using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing email sequences (drip campaigns).
/// </summary>
public class EmailSequenceService : IEmailSequenceService
{
    private readonly IEmailSequenceRepository _repository;
    private readonly ILogger<EmailSequenceService> _logger;

    public EmailSequenceService(IEmailSequenceRepository repository, ILogger<EmailSequenceService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmailSequenceDto>> GetSequencesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting email sequences for user {UserId}, org {OrganizationId}", userId, organizationId);
        
        var sequences = await _repository.GetSequencesAsync(userId, organizationId, ct);
        var result = new List<EmailSequenceDto>();
        
        foreach (var s in sequences)
        {
            var activeEnrollments = await _repository.CountActiveEnrollmentsAsync(s.Id, ct);
            result.Add(MapToDto(s, activeEnrollments: activeEnrollments));
        }
        
        _logger.LogInformation("Retrieved {Count} email sequences for user {UserId}", result.Count, userId);
        return result;
    }

    public async Task<EmailSequenceDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting email sequence {SequenceId} for user {UserId}", id, userId);
        
        var sequence = await _repository.GetByIdWithStepsAsync(id, userId, ct);
        if (sequence == null)
        {
            _logger.LogWarning("Email sequence {SequenceId} not found for user {UserId}", id, userId);
            return null;
        }
        
        var activeEnrollments = await _repository.CountActiveEnrollmentsAsync(id, ct);
        return MapToDto(sequence, includeSteps: true, activeEnrollments: activeEnrollments);
    }

    public async Task<EmailSequenceDto> CreateAsync(Guid userId, Guid? organizationId, CreateEmailSequenceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating email sequence '{Name}' for user {UserId}", request.Name, userId);
        
        var sequence = new EmailSequence
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            UserId = userId,
            OrganizationId = organizationId,
            IsActive = false,
            IsSharedWithOrganization = request.IsSharedWithOrganization,
            CreatedAtUtc = DateTime.UtcNow,
            Steps = (request.Steps ?? new List<CreateSequenceStepRequest>())
                .Select((s, i) => new EmailSequenceStep
                {
                    Id = Guid.NewGuid(),
                    StepOrder = i + 1,
                    Subject = s.Subject,
                    Body = s.Body,
                    CopyTypeId = Enum.TryParse<ACI.Domain.Enums.CopyTypeId>(s.CopyTypeId.Replace("-", ""), true, out var typeId) 
                        ? typeId 
                        : ACI.Domain.Enums.CopyTypeId.SalesEmail,
                    DelayDays = s.DelayDays,
                    DelayHours = s.DelayHours,
                    StopOnReply = s.StopOnReply,
                })
                .ToList(),
        };

        await _repository.AddAsync(sequence, ct);
        
        _logger.LogInformation("Created email sequence {SequenceId} with {StepCount} steps for user {UserId}", 
            sequence.Id, sequence.Steps.Count, userId);
        
        return MapToDto(sequence, includeSteps: true, activeEnrollments: 0);
    }

    public async Task<EmailSequenceDto> UpdateAsync(Guid userId, Guid id, UpdateEmailSequenceRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("Updating email sequence {SequenceId} for user {UserId}", id, userId);
        
        var sequence = await _repository.GetByIdWithStepsAsync(id, userId, ct);
        if (sequence == null)
        {
            _logger.LogWarning("Email sequence {SequenceId} not found for update by user {UserId}", id, userId);
            throw new InvalidOperationException("Sequence not found");
        }

        if (request.Name != null) sequence.Name = request.Name;
        if (request.Description != null) sequence.Description = request.Description;
        if (request.IsActive.HasValue) sequence.IsActive = request.IsActive.Value;
        if (request.IsSharedWithOrganization.HasValue) sequence.IsSharedWithOrganization = request.IsSharedWithOrganization.Value;

        var updated = await _repository.UpdateAsync(sequence, userId, ct);
        
        _logger.LogInformation("Updated email sequence {SequenceId} for user {UserId}", id, userId);
        
        var activeEnrollments = await _repository.CountActiveEnrollmentsAsync(id, ct);
        return MapToDto(updated!, includeSteps: true, activeEnrollments: activeEnrollments);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting email sequence {SequenceId} for user {UserId}", id, userId);
        
        var deleted = await _repository.DeleteAsync(id, userId, ct);
        
        if (deleted)
            _logger.LogInformation("Deleted email sequence {SequenceId} for user {UserId}", id, userId);
        else
            _logger.LogWarning("Email sequence {SequenceId} not found for deletion by user {UserId}", id, userId);
    }

    public async Task<EmailSequenceDto> AddStepAsync(Guid userId, Guid sequenceId, CreateSequenceStepRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("Adding step to email sequence {SequenceId} for user {UserId}", sequenceId, userId);
        
        var sequence = await _repository.GetByIdWithStepsAsync(sequenceId, userId, ct);
        if (sequence == null)
        {
            _logger.LogWarning("Email sequence {SequenceId} not found for adding step by user {UserId}", sequenceId, userId);
            throw new InvalidOperationException("Sequence not found");
        }

        var maxOrder = await _repository.GetMaxStepOrderAsync(sequenceId, ct);
        
        var step = new EmailSequenceStep
        {
            Id = Guid.NewGuid(),
            SequenceId = sequenceId,
            StepOrder = maxOrder + 1,
            Subject = request.Subject,
            Body = request.Body,
            CopyTypeId = Enum.TryParse<ACI.Domain.Enums.CopyTypeId>(request.CopyTypeId.Replace("-", ""), true, out var typeId) 
                ? typeId 
                : ACI.Domain.Enums.CopyTypeId.SalesEmail,
            DelayDays = request.DelayDays,
            DelayHours = request.DelayHours,
            StopOnReply = request.StopOnReply,
        };

        await _repository.AddStepAsync(step, ct);
        
        // Re-fetch with updated steps
        sequence = await _repository.GetByIdWithStepsAsync(sequenceId, userId, ct);
        
        _logger.LogInformation("Added step {StepId} to email sequence {SequenceId}", step.Id, sequenceId);
        
        var activeEnrollments = await _repository.CountActiveEnrollmentsAsync(sequenceId, ct);
        return MapToDto(sequence!, includeSteps: true, activeEnrollments: activeEnrollments);
    }

    public async Task RemoveStepAsync(Guid userId, Guid sequenceId, Guid stepId, CancellationToken ct = default)
    {
        _logger.LogDebug("Removing step {StepId} from sequence {SequenceId} for user {UserId}", stepId, sequenceId, userId);
        
        var removed = await _repository.RemoveStepAsync(sequenceId, stepId, userId, ct);
        
        if (removed)
            _logger.LogInformation("Removed step {StepId} from email sequence {SequenceId}", stepId, sequenceId);
        else
            _logger.LogWarning("Step {StepId} not found in sequence {SequenceId} for user {UserId}", stepId, sequenceId, userId);
    }

    public async Task<IReadOnlyList<EmailSequenceEnrollmentDto>> GetEnrollmentsAsync(Guid userId, Guid? sequenceId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting enrollments for user {UserId}, sequence {SequenceId}", userId, sequenceId);
        
        var enrollments = await _repository.GetEnrollmentsAsync(userId, sequenceId, ct);
        
        _logger.LogInformation("Retrieved {Count} enrollments for user {UserId}", enrollments.Count, userId);
        
        return enrollments.Select(e => MapEnrollmentToDto(e)).ToList();
    }

    public async Task<EmailSequenceEnrollmentDto> EnrollAsync(Guid userId, EnrollInSequenceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Enrolling recipient in sequence {SequenceId} for user {UserId}", request.SequenceId, userId);
        
        var enrollment = new EmailSequenceEnrollment
        {
            Id = Guid.NewGuid(),
            SequenceId = request.SequenceId,
            UserId = userId,
            ContactId = request.ContactId,
            LeadId = request.LeadId,
            RecipientEmail = request.RecipientEmail,
            RecipientName = request.RecipientName,
            CurrentStep = 1,
            Status = EnrollmentStatus.Active,
            EnrolledAtUtc = DateTime.UtcNow,
            NextSendAtUtc = DateTime.UtcNow,
        };

        await _repository.AddEnrollmentAsync(enrollment, ct);
        
        _logger.LogInformation("Created enrollment {EnrollmentId} in sequence {SequenceId}", enrollment.Id, request.SequenceId);
        
        // Re-fetch to include sequence name
        var fullEnrollment = await _repository.GetEnrollmentByIdAsync(enrollment.Id, userId, ct);
        var sequence = await _repository.GetByIdAsync(request.SequenceId, userId, ct);
        
        return MapEnrollmentToDto(fullEnrollment!, sequence?.Name);
    }

    public async Task<EmailSequenceEnrollmentDto> PauseEnrollmentAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default)
    {
        _logger.LogDebug("Pausing enrollment {EnrollmentId} for user {UserId}", enrollmentId, userId);
        
        var enrollment = await _repository.GetEnrollmentByIdAsync(enrollmentId, userId, ct);
        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment {EnrollmentId} not found for pause by user {UserId}", enrollmentId, userId);
            throw new InvalidOperationException("Enrollment not found");
        }

        enrollment.Status = EnrollmentStatus.Paused;
        await _repository.UpdateEnrollmentAsync(enrollment, userId, ct);
        
        _logger.LogInformation("Paused enrollment {EnrollmentId}", enrollmentId);
        
        return MapEnrollmentToDto(enrollment);
    }

    public async Task<EmailSequenceEnrollmentDto> ResumeEnrollmentAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default)
    {
        _logger.LogDebug("Resuming enrollment {EnrollmentId} for user {UserId}", enrollmentId, userId);
        
        var enrollment = await _repository.GetEnrollmentByIdAsync(enrollmentId, userId, ct);
        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment {EnrollmentId} not found for resume by user {UserId}", enrollmentId, userId);
            throw new InvalidOperationException("Enrollment not found");
        }

        enrollment.Status = EnrollmentStatus.Active;
        await _repository.UpdateEnrollmentAsync(enrollment, userId, ct);
        
        _logger.LogInformation("Resumed enrollment {EnrollmentId}", enrollmentId);
        
        return MapEnrollmentToDto(enrollment);
    }

    public async Task UnenrollAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing enrollment {EnrollmentId} for user {UserId}", enrollmentId, userId);
        
        var removed = await _repository.RemoveEnrollmentAsync(enrollmentId, userId, ct);
        
        if (removed)
            _logger.LogInformation("Removed enrollment {EnrollmentId}", enrollmentId);
        else
            _logger.LogWarning("Enrollment {EnrollmentId} not found for removal by user {UserId}", enrollmentId, userId);
    }

    private static EmailSequenceDto MapToDto(EmailSequence s, bool includeSteps = false, int activeEnrollments = 0) => new(
        s.Id,
        s.Name,
        s.Description,
        s.IsActive,
        s.IsSharedWithOrganization,
        s.Steps.Count,
        activeEnrollments,
        s.CreatedAtUtc,
        s.UpdatedAtUtc,
        includeSteps ? s.Steps.OrderBy(st => st.StepOrder).Select(st => new EmailSequenceStepDto(
            st.Id,
            st.StepOrder,
            st.Subject,
            st.Body,
            st.CopyTypeId.ToString(),
            st.DelayDays,
            st.DelayHours,
            st.StopOnReply
        )).ToList() : null
    );

    private static EmailSequenceEnrollmentDto MapEnrollmentToDto(EmailSequenceEnrollment e, string? sequenceName = null) => new(
        e.Id,
        e.SequenceId,
        sequenceName ?? e.Sequence?.Name,
        e.ContactId,
        e.LeadId,
        e.RecipientEmail,
        e.RecipientName,
        e.CurrentStep,
        e.Status.ToString(),
        e.EnrolledAtUtc,
        e.LastSentAtUtc,
        e.NextSendAtUtc,
        e.CompletedAtUtc
    );
}
