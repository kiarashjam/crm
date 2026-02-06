using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _db;

    public CompanyRepository(AppDbContext db) => _db = db;

    private static IQueryable<Company> FilterByUserAndOrg(IQueryable<Company> q, Guid userId, Guid? organizationId) =>
        q.Where(c => c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId));

    private static IQueryable<Company> ApplySearch(IQueryable<Company> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(c => c.Name.ToLower().Contains(q) 
            || (c.Domain != null && c.Domain.ToLower().Contains(q)) 
            || (c.Industry != null && c.Industry.ToLower().Contains(q)));
    }

    public async Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Companies, userId, organizationId);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        
        return (items, totalCount);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Companies, userId, organizationId);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Company>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .Where(c => c.Name.ToLower().Contains(q) || (c.Domain != null && c.Domain.ToLower().Contains(q)) || (c.Industry != null && c.Industry.ToLower().Contains(q)))
            .OrderBy(c => c.Name)
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task<Company?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await _db.Companies.FirstOrDefaultAsync(c =>
            c.Id == id && c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId), ct);

    public async Task<Company> AddAsync(Company company, CancellationToken ct = default)
    {
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);
        return company;
    }

    public async Task<Company?> UpdateAsync(Company company, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await _db.Companies.FirstOrDefaultAsync(c =>
            c.Id == company.Id && c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId), ct);
        if (existing == null) return null;
        existing.Name = company.Name;
        existing.Domain = company.Domain;
        existing.Industry = company.Industry;
        existing.Size = company.Size;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await _db.Companies.FirstOrDefaultAsync(c =>
            c.Id == id && c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId), ct);
        if (entity == null) return false;
        // Null FKs so delete does not violate referential integrity (Contacts, Deals, Leads reference Company)
        await _db.Contacts.Where(c => c.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CompanyId, (Guid?)null), ct);
        await _db.Deals.Where(d => d.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.CompanyId, (Guid?)null), ct);
        await _db.Leads.Where(l => l.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.CompanyId, (Guid?)null), ct);
        _db.Companies.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
