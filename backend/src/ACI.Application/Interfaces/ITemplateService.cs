using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateDto>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<TemplateDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
