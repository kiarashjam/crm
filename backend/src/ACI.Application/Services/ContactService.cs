using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _repository;
    private readonly IActivityRepository _activityRepository;

    public ContactService(IContactRepository repository, IActivityRepository activityRepository)
    {
        _repository = repository;
        _activityRepository = activityRepository;
    }

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(userId, list.Select(c => c.Id), ct);
        return list.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : (DateTime?)null)).ToList();
    }

    public async Task<IReadOnlyList<ContactDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        var list = await _repository.SearchAsync(userId, query.Trim(), ct);
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(userId, list.Select(c => c.Id), ct);
        return list.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : (DateTime?)null)).ToList();
    }

    public async Task<ContactDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        if (entity == null) return null;
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(userId, new[] { id }, ct);
        return Map(entity, lastByContact.TryGetValue(id, out var last) ? last : (DateTime?)null);
    }

    public async Task<ContactDto?> CreateAsync(Guid userId, CreateContactRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            return null;
        var entity = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyId = request.CompanyId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return Map(entity, null);
    }

    public async Task<ContactDto?> UpdateAsync(Guid id, Guid userId, UpdateContactRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, userId, ct);
        if (existing == null) return null;
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Email != null) existing.Email = request.Email.Trim();
        if (request.Phone != null) existing.Phone = request.Phone.Trim();
        if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId;
        existing = await _repository.UpdateAsync(existing, userId, ct);
        return existing == null ? null : Map(existing, null);
    }

    private static ContactDto Map(Contact e, DateTime? lastActivityAtUtc = null) =>
        new ContactDto(e.Id, e.Name, e.Email, e.Phone, e.CompanyId, lastActivityAtUtc);
}
