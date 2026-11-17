using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly AppDbContext _db;

    public EmailTemplateRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmailTemplate>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.EmailTemplates.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId)
            .Include(t => t.Versions)
            .OrderBy(t => t.Category).ThenBy(t => t.Name)
            .ToListAsync(ct);

    public Task<EmailTemplate?> GetAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        _db.EmailTemplates.Include(t => t.Versions).FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId, ct);

    public Task<bool> NameTakenAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct = default)
    {
        var wanted = name.Trim().ToLower();
        return _db.EmailTemplates.AnyAsync(t => t.OrganizationId == organizationId && t.Name.ToLower() == wanted && t.Id != exceptId, ct);
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken ct = default)
    {
        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(EmailTemplate template, IReadOnlyList<EmailTemplateVersion> versions, CancellationToken ct = default)
    {
        // Each language kept is changed in place, so its row (and its id) stays.
        foreach (var gone in template.Versions.Where(v => versions.All(n => n.Language != v.Language)).ToList())
        {
            template.Versions.Remove(gone);
            _db.EmailTemplateVersions.Remove(gone);
        }
        foreach (var version in versions)
        {
            var kept = template.Versions.FirstOrDefault(v => v.Language == version.Language);
            if (kept == null)
            {
                version.EmailTemplateId = template.Id;
                template.Versions.Add(version);
                _db.EmailTemplateVersions.Add(version);
            }
            else
            {
                kept.Subject = version.Subject;
                kept.Body = version.Body;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var template = await GetAsync(id, organizationId, ct);
        if (template == null) return false;
        _db.EmailTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SeedAsync(Guid organizationId, IReadOnlyList<EmailTemplate> starters, DateTime at, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null || org.EmailTemplatesSeededAtUtc != null) return false;
        org.EmailTemplatesSeededAtUtc = at;
        _db.EmailTemplates.AddRange(starters);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another request gave the set at the same moment (its starter keys are unique
            // per workspace, so this one's were refused). Leave its rows to it.
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }
}
