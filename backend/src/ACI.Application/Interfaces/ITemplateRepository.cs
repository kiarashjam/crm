using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ITemplateRepository
{
    Task<(IReadOnlyList<Template> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? category = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetSharedTemplatesAsync(Guid organizationId, CancellationToken ct = default);
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Template> CreateAsync(Template template, CancellationToken ct = default);
    Task<Template> UpdateAsync(Template template, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task IncrementUseCountAsync(Guid id, CancellationToken ct = default);
}
