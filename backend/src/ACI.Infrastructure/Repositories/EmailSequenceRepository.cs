using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for email sequence operations.
/// </summary>
public sealed class EmailSequenceRepository : IEmailSequenceRepository
{
    private readonly AppDbContext _db;

    public EmailSequenceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmailSequence>> GetSequencesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        return await _db.EmailSequences
            .Include(s => s.Steps)
            .Where(s => s.UserId == userId || 
                       (organizationId != null && s.OrganizationId == organizationId && s.IsSharedWithOrganization))
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<EmailSequence?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _db.EmailSequences
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
    }

    public async Task<EmailSequence?> GetByIdWithStepsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _db.EmailSequences
            .Include(s => s.Steps.OrderBy(st => st.StepOrder))
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
    }

    public async Task<EmailSequence> AddAsync(EmailSequence sequence, CancellationToken ct = default)
    {
        _db.EmailSequences.Add(sequence);
        await _db.SaveChangesAsync(ct);
        return sequence;
    }

    public async Task<EmailSequence?> UpdateAsync(EmailSequence sequence, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.EmailSequences
            .FirstOrDefaultAsync(s => s.Id == sequence.Id && s.UserId == userId, ct);
        
        if (existing == null) return null;
        
        existing.Name = sequence.Name;
        existing.Description = sequence.Description;
        existing.IsActive = sequence.IsActive;
        existing.IsSharedWithOrganization = sequence.IsSharedWithOrganization;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var sequence = await _db.EmailSequences
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        
        if (sequence == null) return false;
        
        _db.EmailSequences.Remove(sequence);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmailSequenceStep> AddStepAsync(EmailSequenceStep step, CancellationToken ct = default)
    {
        _db.Set<EmailSequenceStep>().Add(step);
        await _db.SaveChangesAsync(ct);
        return step;
    }

    public async Task<bool> RemoveStepAsync(Guid sequenceId, Guid stepId, Guid userId, CancellationToken ct = default)
    {
        var sequence = await _db.EmailSequences
            .FirstOrDefaultAsync(s => s.Id == sequenceId && s.UserId == userId, ct);
        
        if (sequence == null) return false;
        
        var step = await _db.Set<EmailSequenceStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.SequenceId == sequenceId, ct);
        
        if (step == null) return false;
        
        _db.Set<EmailSequenceStep>().Remove(step);
        
        // Update the parent sequence's UpdatedAtUtc
        sequence.UpdatedAtUtc = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> GetMaxStepOrderAsync(Guid sequenceId, CancellationToken ct = default)
    {
        var maxOrder = await _db.Set<EmailSequenceStep>()
            .Where(s => s.SequenceId == sequenceId)
            .MaxAsync(s => (int?)s.StepOrder, ct);
        
        return maxOrder ?? 0;
    }

    public async Task<IReadOnlyList<EmailSequenceEnrollment>> GetEnrollmentsAsync(Guid userId, Guid? sequenceId, CancellationToken ct = default)
    {
        var query = _db.EmailSequenceEnrollments
            .Include(e => e.Sequence)
            .Where(e => e.UserId == userId);
        
        if (sequenceId.HasValue)
            query = query.Where(e => e.SequenceId == sequenceId.Value);
        
        return await query.OrderByDescending(e => e.EnrolledAtUtc).ToListAsync(ct);
    }

    public async Task<EmailSequenceEnrollment?> GetEnrollmentByIdAsync(Guid enrollmentId, Guid userId, CancellationToken ct = default)
    {
        return await _db.EmailSequenceEnrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct);
    }

    public async Task<EmailSequenceEnrollment> AddEnrollmentAsync(EmailSequenceEnrollment enrollment, CancellationToken ct = default)
    {
        _db.EmailSequenceEnrollments.Add(enrollment);
        await _db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task<EmailSequenceEnrollment?> UpdateEnrollmentAsync(EmailSequenceEnrollment enrollment, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.EmailSequenceEnrollments
            .FirstOrDefaultAsync(e => e.Id == enrollment.Id && e.UserId == userId, ct);
        
        if (existing == null) return null;
        
        existing.Status = enrollment.Status;
        existing.CurrentStep = enrollment.CurrentStep;
        existing.LastSentAtUtc = enrollment.LastSentAtUtc;
        existing.NextSendAtUtc = enrollment.NextSendAtUtc;
        existing.CompletedAtUtc = enrollment.CompletedAtUtc;
        
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> RemoveEnrollmentAsync(Guid enrollmentId, Guid userId, CancellationToken ct = default)
    {
        var enrollment = await _db.EmailSequenceEnrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct);
        
        if (enrollment == null) return false;
        
        _db.EmailSequenceEnrollments.Remove(enrollment);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CountActiveEnrollmentsAsync(Guid sequenceId, CancellationToken ct = default)
    {
        return await _db.EmailSequenceEnrollments
            .CountAsync(e => e.SequenceId == sequenceId && e.Status == EnrollmentStatus.Active, ct);
    }
}
