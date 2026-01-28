using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ITemplateRepository
{
    Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Template>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
