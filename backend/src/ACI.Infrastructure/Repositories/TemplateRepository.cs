using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly AppDbContext _db;

    public TemplateRepository(AppDbContext db) => _db = db;

    private static IQueryable<Template> ApplySearch(IQueryable<Template> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(t => 
            t.Title.ToLower().Contains(q) || 
            (t.Content != null && t.Content.ToLower().Contains(q)) ||
            (t.Category != null && t.Category.ToLower().Contains(q)));
    }

    private static IQueryable<Template> ApplyCategoryFilter(IQueryable<Template> query, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return query;
        return query.Where(t => t.Category != null && t.Category.ToLower() == category.Trim().ToLowerInvariant());
    }

    public async Task<(IReadOnlyList<Template> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? category = null,
        CancellationToken ct = default)
    {
        // Templates available to user: own templates + shared org templates + system templates
        var query = _db.Templates
            .Include(t => t.User)
            .Where(t => t.UserId == userId || 
                        t.IsSystemTemplate ||
                        (organizationId != null && t.OrganizationId == organizationId && t.IsSharedWithOrganization));

        query = ApplySearch(query, search);
        query = ApplyCategoryFilter(query, category);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.UseCount)
            .ThenBy(t => t.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.User)
            .OrderByDescending(t => t.UseCount)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Template>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.User)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.UseCount)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Template>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.User)
            .Where(t => t.OrganizationId == organizationId)
            .OrderByDescending(t => t.UseCount)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Template>> GetSharedTemplatesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.User)
            .Where(t => t.OrganizationId == organizationId && t.IsSharedWithOrganization)
            .OrderByDescending(t => t.UseCount)
            .ThenBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Template> CreateAsync(Template template, CancellationToken ct = default)
    {
        _db.Templates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<Template> UpdateAsync(Template template, CancellationToken ct = default)
    {
        _db.Templates.Update(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.Templates.FindAsync([id], ct);
        if (template != null)
        {
            _db.Templates.Remove(template);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task IncrementUseCountAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.Templates.FindAsync([id], ct);
        if (template != null)
        {
            template.UseCount++;
            await _db.SaveChangesAsync(ct);
        }
    }
}
