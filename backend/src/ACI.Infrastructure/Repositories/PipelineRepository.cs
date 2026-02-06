using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class PipelineRepository : IPipelineRepository
{
    private readonly AppDbContext _db;

    public PipelineRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Pipeline>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Pipelines
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Include(p => p.DealStages.OrderBy(s => s.DisplayOrder))
            .ToListAsync(ct);

    public async Task<Pipeline?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        await _db.Pipelines
            .Include(p => p.DealStages.OrderBy(s => s.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

    public async Task<Pipeline> AddAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        _db.Pipelines.Add(pipeline);
        await _db.SaveChangesAsync(ct);
        return pipeline;
    }

    public async Task<Pipeline?> UpdateAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        var existing = await _db.Pipelines.FirstOrDefaultAsync(p => p.Id == pipeline.Id && p.OrganizationId == pipeline.OrganizationId, ct);
        if (existing == null) return null;
        existing.Name = pipeline.Name;
        existing.DisplayOrder = pipeline.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var entity = await _db.Pipelines.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);
        if (entity == null) return false;
        _db.Pipelines.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
