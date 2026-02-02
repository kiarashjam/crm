using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class ActivityService : IActivityService
{
    private readonly IActivityRepository _repository;

    public ActivityService(IActivityRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<ActivityDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ActivityDto>> GetByContactIdAsync(Guid contactId, Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByContactIdAsync(contactId, userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ActivityDto>> GetByDealIdAsync(Guid dealId, Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByDealIdAsync(dealId, userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ActivityDto?> CreateAsync(Guid userId, CreateActivityRequest request, CancellationToken ct = default)
    {
        var entity = new Activity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = request.Type ?? "note",
            Subject = request.Subject,
            Body = request.Body,
            ContactId = request.ContactId,
            DealId = request.DealId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _repository.DeleteAsync(id, userId, ct);
    }

    private static ActivityDto Map(Activity e) =>
        new ActivityDto(e.Id, e.Type, e.Subject, e.Body, e.ContactId, e.DealId, e.CreatedAtUtc);
}
