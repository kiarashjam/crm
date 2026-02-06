using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DealStageRepository : IDealStageRepository
{
    private readonly AppDbContext _db;

    public DealStageRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DealStage>> GetByPipelineIdAsync(Guid pipelineId, CancellationToken ct = default) =>
        await _db.DealStages
            .Where(s => s.PipelineId == pipelineId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<DealStage?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        await _db.DealStages
            .Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.Pipeline.OrganizationId == organizationId, ct);

    public async Task<DealStage> AddAsync(DealStage stage, CancellationToken ct = default)
    {
        _db.DealStages.Add(stage);
        await _db.SaveChangesAsync(ct);
        return stage;
    }

    public async Task<DealStage?> UpdateAsync(DealStage stage, Guid organizationId, CancellationToken ct = default)
    {
        var existing = await _db.DealStages.Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == stage.Id && s.Pipeline.OrganizationId == organizationId, ct);
        if (existing == null) return null;
        existing.Name = stage.Name;
        existing.DisplayOrder = stage.DisplayOrder;
        existing.IsWon = stage.IsWon;
        existing.IsLost = stage.IsLost;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var entity = await _db.DealStages.Include(s => s.Pipeline)
            .FirstOrDefaultAsync(s => s.Id == id && s.Pipeline.OrganizationId == organizationId, ct);
        if (entity == null) return false;
        _db.DealStages.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
