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
        var items = await MaterializeListAsync(query.Skip(skip).Take(take), ct);

        return (items, totalCount);
    }

    public async Task<LeadStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId);
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

        // One grouped query instead of eight separate COUNT round-trips.
        // "Qualified or beyond" spans both status vocabularies: the older list
        // used a literal "Qualified", the current one uses Contract Pending /
        // Awaiting Signature / Signed.
        var row = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Converted = g.Count(l => l.IsConverted),
                NewLeads = g.Count(l => l.Status == "New"),
                Contacted = g.Count(l =>
                    l.Status == "Contacted" || l.Status == "Attempted Contact" || l.Status == "Connected"),
                Qualified = g.Count(l =>
                    l.Status == "Qualified" || l.Status == "Contract Pending"
                    || l.Status == "Awaiting Signature" || l.Status == "Signed"),
                ThisWeek = g.Count(l => l.CreatedAtUtc >= oneWeekAgo),
                HotLeads = g.Count(l => !l.IsConverted && l.LeadScore >= 70),
            })
            .FirstOrDefaultAsync(ct);

        if (row == null)
            return new LeadStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0);

        var active = row.Total - row.Converted;
        var conversionRate = row.Total > 0 ? (int)Math.Round((double)row.Converted / row.Total * 100) : 0;
        return new LeadStatsDto(
            row.Total, row.Converted, active, row.NewLeads, row.Contacted, row.Qualified, conversionRate, row.ThisWeek, row.HotLeads);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = ApplySearch(FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId), search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await MaterializeListAsync(
            ApplySort(
                ApplySearch(FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId), null),
                new LeadQueryOptions { SortBy = "name", SortDir = "asc" }),
            ct);

    public async Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            if (take is not > 0)
                return await GetByUserIdAsync(userId, organizationId, ct);
            return await MaterializeListAsync(
                ApplySort(
                    FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId),
                    new LeadQueryOptions { SortBy = "name", SortDir = "asc" })
                .Take(take.Value),
                ct);
        }

        var q = query.Trim().ToLowerInvariant();
        var filtered = ApplySort(
            FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId)
                .Where(l => l.Name.ToLower().Contains(q) || l.Email.ToLower().Contains(q) ||
                            (l.Phone != null && l.Phone.Contains(q))),
            new LeadQueryOptions { SortBy = "name", SortDir = "asc" });
        if (take is > 0)
            filtered = filtered.Take(take.Value);
        return await MaterializeListAsync(filtered, ct);
    }

    public async Task<Lead?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var matches = await MaterializeListAsync(
            FilterByUserAndOrg(_db.Leads.AsNoTracking(), userId, organizationId).Where(l => l.Id == id),
            ct);
        return matches.Count > 0 ? matches[0] : null;
    }

    /// <summary>
    /// Loads lead rows plus company and assignee names only.
    /// Avoids joining the full User row (password hash, 2FA secret, and the rest).
    /// </summary>
    private static async Task<List<Lead>> MaterializeListAsync(IQueryable<Lead> query, CancellationToken ct)
    {
        var rows = await query
            .Select(l => new
            {
                Lead = l,
                CompanyName = l.Company != null ? l.Company.Name : null,
                AssigneeName = l.AssignedToUser != null ? l.AssignedToUser.Name : null,
            })
            .ToListAsync(ct);

        var list = new List<Lead>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Lead.CompanyId is Guid companyId && row.CompanyName != null)
                row.Lead.Company = new Company { Id = companyId, Name = row.CompanyName };
            if (row.Lead.AssignedToUserId is Guid assigneeId && row.AssigneeName != null)
                row.Lead.AssignedToUser = new User { Id = assigneeId, Name = row.AssigneeName };
            list.Add(row.Lead);
        }
        return list;
    }

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
