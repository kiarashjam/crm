using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly AppDbContext _db;

    public LeadRepository(AppDbContext db) => _db = db;

    private static IQueryable<Lead> FilterByUserAndOrg(IQueryable<Lead> q, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? q.Where(l => l.UserId == userId && l.OrganizationId == null)
            : q.Where(l => l.OrganizationId == organizationId);

    private static IQueryable<Lead> ApplySearch(IQueryable<Lead> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(l =>
            l.Name.ToLower().Contains(q) ||
            l.Email.ToLower().Contains(q) ||
            (l.Phone != null && l.Phone.Contains(q)));
    }

    private static IQueryable<Lead> ApplyFilters(IQueryable<Lead> query, LeadQueryOptions? options)
    {
        if (options == null) return query;

        if (!string.IsNullOrWhiteSpace(options.Status) &&
            !string.Equals(options.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var status = options.Status.Trim();
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(options.Source) &&
            !string.Equals(options.Source, "all", StringComparison.OrdinalIgnoreCase))
        {
            var source = options.Source.Trim();
            query = query.Where(l => l.Source == source);
        }

        if (options.IsConverted.HasValue)
        {
            query = options.IsConverted.Value
                ? query.Where(l => l.IsConverted)
                : query.Where(l => !l.IsConverted);
        }

        return query;
    }

    private static IQueryable<Lead> ApplySort(IQueryable<Lead> query, LeadQueryOptions? options)
    {
        var sortBy = options?.SortBy?.Trim().ToLowerInvariant() ?? "createdat";
        var desc = string.Equals(options?.SortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "email" => desc
                ? query.OrderByDescending(l => l.Email).ThenBy(l => l.Name)
                : query.OrderBy(l => l.Email).ThenBy(l => l.Name),
            "status" => desc
                ? query.OrderByDescending(l => l.Status).ThenBy(l => l.Name)
                : query.OrderBy(l => l.Status).ThenBy(l => l.Name),
            "createdat" => desc
                ? query.OrderByDescending(l => l.CreatedAtUtc).ThenBy(l => l.Name)
                : query.OrderBy(l => l.CreatedAtUtc).ThenBy(l => l.Name),
            _ => desc
                ? query.OrderByDescending(l => l.Name)
                : query.OrderBy(l => l.Name),
        };
    }

    private static IQueryable<Lead> BuildFilteredQuery(
        AppDbContext db,
        Guid userId,
        Guid? organizationId,
        LeadQueryOptions? options) =>
        ApplySort(
            ApplyFilters(
                ApplySearch(
                    FilterByUserAndOrg(db.Leads.AsNoTracking(), userId, organizationId),
                    options?.Search),
                options),
            options);

    public async Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        LeadQueryOptions? options = null,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(_db, userId, organizationId, options);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(l => l.Company)
            .Include(l => l.AssignedToUser)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<LeadStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId);
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

        var total = await query.CountAsync(ct);
        var converted = await query.CountAsync(l => l.IsConverted, ct);
        var active = total - converted;
        var newLeads = await query.CountAsync(l => l.Status == "New", ct);
        var contacted = await query.CountAsync(l =>
            l.Status == "Contacted" || l.Status == "Attempted Contact" || l.Status == "Connected", ct);
        // "Qualified or beyond". Spans both status vocabularies on purpose: the
        // older list used a literal "Qualified", the current one replaces it with
        // Contract Pending / Awaiting Signature / Signed. Matching only the old
        // label would silently report zero for every organisation on the new one.
        var qualified = await query.CountAsync(l =>
            l.Status == "Qualified" || l.Status == "Contract Pending"
            || l.Status == "Awaiting Signature" || l.Status == "Signed", ct);
        var thisWeek = await query.CountAsync(l => l.CreatedAtUtc >= oneWeekAgo, ct);
        var hotLeads = await query.CountAsync(l => !l.IsConverted && l.LeadScore >= 70, ct);
        var conversionRate = total > 0 ? (int)Math.Round((double)converted / total * 100) : 0;

        return new LeadStatsDto(total, converted, active, newLeads, contacted, qualified, conversionRate, thisWeek, hotLeads);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = ApplySearch(FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId), search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await ApplySort(
                ApplySearch(FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId), null),
                new LeadQueryOptions { SortBy = "name", SortDir = "asc" })
            .Include(l => l.Company)
            .Include(l => l.AssignedToUser)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await ApplySort(
                FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId)
                    .Where(l => l.Name.ToLower().Contains(q) || l.Email.ToLower().Contains(q) ||
                                (l.Phone != null && l.Phone.Contains(q))),
                new LeadQueryOptions { SortBy = "name", SortDir = "asc" })
            .Include(l => l.Company)
            .Include(l => l.AssignedToUser)
            .ToListAsync(ct);
    }

    public async Task<Lead?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId)
            .Include(l => l.Company)
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<Lead> AddAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<Lead?> UpdateAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == lead.Id, ct);
        if (existing == null) return null;
        existing.Name = lead.Name;
        existing.Email = lead.Email;
        existing.Phone = lead.Phone;
        existing.CompanyId = lead.CompanyId;
        existing.Source = lead.Source;
        existing.Status = lead.Status;
        existing.LeadSourceId = lead.LeadSourceId;
        existing.LeadStatusId = lead.LeadStatusId;
        existing.LeadScore = lead.LeadScore;
        existing.LastContactedAt = lead.LastContactedAt;
        existing.Description = lead.Description;
        existing.LifecycleStage = lead.LifecycleStage;
        existing.PipelineState = lead.PipelineState;
        existing.IsConverted = lead.IsConverted;
        existing.ConvertedAtUtc = lead.ConvertedAtUtc;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<Lead?> AssignAsync(Guid id, Guid userId, Guid? organizationId, Guid? assignedToUserId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (existing == null) return null;
        existing.AssignedToUserId = assignedToUserId;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        // Re-read with the assignee navigation so the returned DTO carries the name.
        return await GetByIdAsync(id, userId, organizationId, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (existing == null) return false;
        await _db.TaskItems.Where(t => t.LeadId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LeadId, (Guid?)null), ct);
        await _db.Activities.Where(a => a.LeadId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LeadId, (Guid?)null), ct);
        await _db.Contacts.Where(c => c.ConvertedFromLeadId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConvertedFromLeadId, (Guid?)null), ct);
        _db.Leads.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
