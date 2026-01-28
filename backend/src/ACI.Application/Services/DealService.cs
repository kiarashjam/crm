using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class DealService : IDealService
{
    private readonly IDealRepository _repository;

    public DealService(IDealRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DealDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        var list = await _repository.SearchAsync(userId, query.Trim(), ct);
        return list.Select(Map).ToList();
    }

    public async Task<DealDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        return entity == null ? null : Map(entity);
    }

    private static DealDto Map(Deal e) =>
        new DealDto(e.Id, e.Name, e.Value, e.Stage, e.CompanyId);
}
