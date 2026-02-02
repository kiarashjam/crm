using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class DealService : IDealService
{
    private readonly IDealRepository _repository;
    private readonly IActivityRepository _activityRepository;

    public DealService(IDealRepository repository, IActivityRepository activityRepository)
    {
        _repository = repository;
        _activityRepository = activityRepository;
    }

    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(userId, list.Select(d => d.Id), ct);
        return list.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : (DateTime?)null)).ToList();
    }

    public async Task<IReadOnlyList<DealDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        var list = await _repository.SearchAsync(userId, query.Trim(), ct);
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(userId, list.Select(d => d.Id), ct);
        return list.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : (DateTime?)null)).ToList();
    }

    public async Task<DealDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        if (entity == null) return null;
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(userId, new[] { id }, ct);
        return Map(entity, lastByDeal.TryGetValue(id, out var last) ? last : (DateTime?)null);
    }

    public async Task<DealDto?> CreateAsync(Guid userId, CreateDealRequest request, CancellationToken ct = default)
    {
        var entity = new Deal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Value = request.Value,
            Stage = request.Stage,
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            ExpectedCloseDateUtc = request.ExpectedCloseDateUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return Map(entity, null);
    }

    public async Task<DealDto?> UpdateAsync(Guid id, Guid userId, UpdateDealRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, userId, ct);
        if (existing == null) return null;
        if (request.Name != null) existing.Name = request.Name;
        if (request.Value != null) existing.Value = request.Value;
        if (request.Stage != null) existing.Stage = request.Stage;
        if (request.CompanyId != null) existing.CompanyId = request.CompanyId;
        if (request.ContactId.HasValue) existing.ContactId = request.ContactId;
        if (request.ExpectedCloseDateUtc != null) existing.ExpectedCloseDateUtc = request.ExpectedCloseDateUtc;
        if (request.IsWon != null) existing.IsWon = request.IsWon;
        existing = await _repository.UpdateAsync(existing, userId, ct);
        return existing == null ? null : Map(existing, null);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _repository.DeleteAsync(id, userId, ct);
    }

    private static DealDto Map(Deal e, DateTime? lastActivityAtUtc) =>
        new DealDto(e.Id, e.Name, e.Value, e.Stage, e.CompanyId, e.ContactId, e.ExpectedCloseDateUtc, e.IsWon, lastActivityAtUtc);
}
