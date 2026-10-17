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

    public Task<OutreachEnrollment?> GetEnrollmentAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        // Scoped by organisation: by id alone, anyone who knew an enrollment id —
        // a Viewer, a former member, another workspace — could pause or delete it.
        _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId, ct);

    public Task<OutreachEnrollment?> FindOpenEnrollmentAsync(Guid sequenceId, Guid targetId, CancellationToken ct = default) =>
        _db.OutreachEnrollments.FirstOrDefaultAsync(e =>
            e.SequenceId == sequenceId && e.TargetId == targetId && (e.Status == "active" || e.Status == "paused"), ct);

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

    public async Task<bool> DeleteEnrollmentAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId, ct);
        if (row == null) return false;
        _db.OutreachEnrollments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task StopEnrollmentsAsync(Guid sequenceId, CancellationToken ct = default)
    {
        var open = await _db.OutreachEnrollments
            .Where(e => e.SequenceId == sequenceId && (e.Status == "active" || e.Status == "paused"))
            .ToListAsync(ct);
        foreach (var row in open)
        {
            row.Status = "stopped";
            row.NextActionAtUtc = null;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutreachEnrollment>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.OutreachEnrollments
            .Where(e => e.Status == "active" && e.NextActionAtUtc != null && e.NextActionAtUtc <= utcNow)
            .OrderBy(e => e.NextActionAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<bool> TryClaimAsync(Guid enrollmentId, int step, DateTime dueAtUtc, DateTime claimedUntilUtc, CancellationToken ct = default)
    {
        // The background runner runs once per app instance. Without a claim, two
        // instances read the same due rows and both sent the email. One conditional
        // UPDATE is atomic on the database: the row moves only if nobody has moved it
        // since it was read, so exactly one caller sees a row affected. Pushing
        // NextActionAtUtc forward also takes it out of every other instance's due list.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.OutreachEnrollments
                .Where(e => e.Id == enrollmentId && e.Status == "active" && e.CurrentStep == step && e.NextActionAtUtc == dueAtUtc)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.NextActionAtUtc, claimedUntilUtc), ct);
            return moved == 1;
        }

        // The in-memory provider used by tests has no SQL. Same rule, without the
        // atomicity, which only the database can supply.
        var row = await _db.OutreachEnrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);
        if (row is not { Status: "active" } || row.CurrentStep != step || row.NextActionAtUtc != dueAtUtc) return false;
        row.NextActionAtUtc = claimedUntilUtc;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SequenceTargetState> TargetStateAsync(string targetType, Guid targetId, Guid? organizationId, CancellationToken ct = default)
    {
        if (targetType == "contact")
        {
            var contact = await _db.Contacts.AsNoTracking()
                .Where(c => c.Id == targetId && c.OrganizationId == organizationId)
                .Select(c => new { c.Name, c.Email, Company = c.Company != null ? c.Company.Name : null, c.DoNotContact, c.IsArchived })
                .FirstOrDefaultAsync(ct);
            return contact == null
                ? SequenceTargetState.Missing
                : new SequenceTargetState(true, contact.Name, contact.Email, contact.Company,
                    DoNotContact: contact.DoNotContact, IsArchived: contact.IsArchived);
        }

        var lead = await _db.Leads.AsNoTracking()
            .Where(l => l.Id == targetId && l.OrganizationId == organizationId)
            .Select(l => new { l.Name, l.Email, Company = l.Company != null ? l.Company.Name : null, l.IsConverted, l.Status })
            .FirstOrDefaultAsync(ct);
        return lead == null
            ? SequenceTargetState.Missing
            : new SequenceTargetState(true, lead.Name, lead.Email, lead.Company, IsConverted: lead.IsConverted, Status: lead.Status);
    }
}
