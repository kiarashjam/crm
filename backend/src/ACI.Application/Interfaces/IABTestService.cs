using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IABTestService
{
    Task<IReadOnlyList<ABTestDto>> GetTestsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<ABTestDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<ABTestDto> CreateAsync(Guid userId, Guid? organizationId, CreateABTestRequest request, CancellationToken ct = default);
    Task<ABTestDto> UpdateAsync(Guid userId, Guid id, UpdateABTestRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<ABTestVariantDto> TrackVariantEventAsync(Guid variantId, string eventType, CancellationToken ct = default);
}
