using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly AppDbContext _db;

    public LeadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Leads.Where(l => l.UserId == userId).OrderBy(l => l.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await _db.Leads
            .Where(l => l.UserId == userId && (l.Name.ToLower().Contains(q) || l.Email.ToLower().Contains(q)))
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
    }

    public async Task<Lead?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct);

    public async Task<Lead> AddAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<Lead?> UpdateAsync(Lead lead, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Leads.FirstOrDefaultAsync(l => l.Id == lead.Id && l.UserId == userId, ct);
        if (existing == null) return null;
        existing.Name = lead.Name;
        existing.Email = lead.Email;
        existing.Phone = lead.Phone;
        existing.CompanyId = lead.CompanyId;
        existing.Source = lead.Source;
        existing.Status = lead.Status;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct);
        if (existing == null) return false;
        var linkedTasks = await _db.TaskItems.Where(t => t.LeadId == id).ToListAsync(ct);
        foreach (var t in linkedTasks) t.LeadId = null;
        _db.Leads.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
