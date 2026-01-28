using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _repository;

    public ContactService(IContactRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ContactDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        var list = await _repository.SearchAsync(userId, query.Trim(), ct);
        return list.Select(Map).ToList();
    }

    public async Task<ContactDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        return entity == null ? null : Map(entity);
    }

    private static ContactDto Map(Contact e) =>
        new ContactDto(e.Id, e.Name, e.Email, e.CompanyId);
}
