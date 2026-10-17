using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class SequenceRepository : ISequenceRepository
{
    private readonly AppDbContext _db;

    public SequenceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OutreachSequence>> ListAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.OutreachSequences.Include(s => s.Steps)
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<OutreachSequence?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _db.OutreachSequences.Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);

    public async Task SaveAsync(OutreachSequence sequence, CancellationToken ct = default)
    {
        var existing = await _db.OutreachSequences.Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == sequence.Id, ct);
        if (existing == null)
        {
            _db.OutreachSequences.Add(sequence);
        }
        else
        {
            existing.Name = sequence.Name;
            existing.Description = sequence.Description;
            existing.Status = sequence.Status;
            existing.UpdatedAtUtc = sequence.UpdatedAtUtc;
            _db.OutreachSteps.RemoveRange(existing.Steps);
            foreach (var step in sequence.Steps)
            {
                step.SequenceId = existing.Id;
                _db.OutreachSteps.Add(step);
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.OutreachSequences.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);
        if (row == null) return;
        _db.OutreachSequences.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutreachEnrollment>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default)
    {
        var query = _db.OutreachEnrollments.Where(e => e.OrganizationId == organizationId);
        if (sequenceId != null) query = query.Where(e => e.SequenceId == sequenceId);
        return await query.OrderByDescending(e => e.EnrolledAtUtc).ToListAsync(ct);
    }

    public Task<OutreachEnrollment?> GetEnrollmentAsync(Guid id, CancellationToken ct = default) =>
        _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task SaveEnrollmentAsync(OutreachEnrollment enrollment, CancellationToken ct = default)
    {
        var existing = await _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == enrollment.Id, ct);
        if (existing == null) _db.OutreachEnrollments.Add(enrollment);
        else
        {
            existing.Status = enrollment.Status;
            existing.CurrentStep = enrollment.CurrentStep;
            existing.NextActionAtUtc = enrollment.NextActionAtUtc;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteEnrollmentAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (row == null) return;
        _db.OutreachEnrollments.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutreachEnrollment>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.OutreachEnrollments
            .Where(e => e.Status == "active" && e.NextActionAtUtc != null && e.NextActionAtUtc <= utcNow)
            .OrderBy(e => e.NextActionAtUtc)
            .Take(take)
            .ToListAsync(ct);
}
