using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IDealLineItemService
{
    Task<Result<IReadOnlyList<DealLineItemDto>>> ListAsync(Guid userId, Guid? organizationId, Guid dealId, CancellationToken ct = default);

    Task<Result<DealLineItemDto>> AddAsync(Guid userId, Guid? organizationId, Guid dealId, CreateDealLineItemRequest request, CancellationToken ct = default);

    Task<Result<DealLineItemDto>> UpdateAsync(Guid userId, Guid? organizationId, Guid id, UpdateDealLineItemRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);
}
