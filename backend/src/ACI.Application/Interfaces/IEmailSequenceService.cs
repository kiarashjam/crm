using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IEmailSequenceService
{
    Task<IReadOnlyList<EmailSequenceDto>> GetSequencesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<EmailSequenceDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<EmailSequenceDto> CreateAsync(Guid userId, Guid? organizationId, CreateEmailSequenceRequest request, CancellationToken ct = default);
    Task<EmailSequenceDto> UpdateAsync(Guid userId, Guid id, UpdateEmailSequenceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<EmailSequenceDto> AddStepAsync(Guid userId, Guid sequenceId, CreateSequenceStepRequest request, CancellationToken ct = default);
    Task RemoveStepAsync(Guid userId, Guid sequenceId, Guid stepId, CancellationToken ct = default);
    
    // Enrollment management
    Task<IReadOnlyList<EmailSequenceEnrollmentDto>> GetEnrollmentsAsync(Guid userId, Guid? sequenceId, CancellationToken ct = default);
    Task<EmailSequenceEnrollmentDto> EnrollAsync(Guid userId, EnrollInSequenceRequest request, CancellationToken ct = default);
    Task<EmailSequenceEnrollmentDto> PauseEnrollmentAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default);
    Task<EmailSequenceEnrollmentDto> ResumeEnrollmentAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default);
    Task UnenrollAsync(Guid userId, Guid enrollmentId, CancellationToken ct = default);
}
