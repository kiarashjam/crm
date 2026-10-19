using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IClubDocumentRepository
{
    Task<IReadOnlyList<ClubDocument>> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default);
    Task<ClubDocument?> GetAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default);
    Task SaveAsync(ClubDocument document, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default);
}
