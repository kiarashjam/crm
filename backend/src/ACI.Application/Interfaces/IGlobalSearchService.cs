using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDto> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default);
}
