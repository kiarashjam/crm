using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IContactService
{
    Task<IReadOnlyList<ContactDto>> GetContactsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ContactDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<ContactDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
}
