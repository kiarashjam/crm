using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IContactRepository
{
    Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<Contact?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Contact> AddAsync(Contact contact, CancellationToken ct = default);
}
