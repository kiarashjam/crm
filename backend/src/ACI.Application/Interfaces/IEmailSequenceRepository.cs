using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// Repository interface for email sequence operations.
/// </summary>
public interface IEmailSequenceRepository
{
    /// <summary>
    /// Gets all sequences accessible by the user.
    /// </summary>
    Task<IReadOnlyList<EmailSequence>> GetSequencesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a sequence by ID.
    /// </summary>
    Task<EmailSequence?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a sequence by ID with steps included.
    /// </summary>
    Task<EmailSequence?> GetByIdWithStepsAsync(Guid id, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Adds a new sequence.
    /// </summary>
    Task<EmailSequence> AddAsync(EmailSequence sequence, CancellationToken ct = default);
    
    /// <summary>
    /// Updates an existing sequence.
    /// </summary>
    Task<EmailSequence?> UpdateAsync(EmailSequence sequence, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes a sequence and all related data.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Adds a step to a sequence.
    /// </summary>
    Task<EmailSequenceStep> AddStepAsync(EmailSequenceStep step, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a step from a sequence.
    /// </summary>
    Task<bool> RemoveStepAsync(Guid sequenceId, Guid stepId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets the maximum step order for a sequence.
    /// </summary>
    Task<int> GetMaxStepOrderAsync(Guid sequenceId, CancellationToken ct = default);
    
    // Enrollment operations
    
    /// <summary>
    /// Gets enrollments for a user, optionally filtered by sequence.
    /// </summary>
    Task<IReadOnlyList<EmailSequenceEnrollment>> GetEnrollmentsAsync(Guid userId, Guid? sequenceId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets an enrollment by ID.
    /// </summary>
    Task<EmailSequenceEnrollment?> GetEnrollmentByIdAsync(Guid enrollmentId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Adds a new enrollment.
    /// </summary>
    Task<EmailSequenceEnrollment> AddEnrollmentAsync(EmailSequenceEnrollment enrollment, CancellationToken ct = default);
    
    /// <summary>
    /// Updates an enrollment's status.
    /// </summary>
    Task<EmailSequenceEnrollment?> UpdateEnrollmentAsync(EmailSequenceEnrollment enrollment, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Removes an enrollment.
    /// </summary>
    Task<bool> RemoveEnrollmentAsync(Guid enrollmentId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Counts active enrollments for a sequence.
    /// </summary>
    Task<int> CountActiveEnrollmentsAsync(Guid sequenceId, CancellationToken ct = default);
}
