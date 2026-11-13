using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>Saved views of the lists: a person's own, the ones shared with the workspace, and each person's default.</summary>
public interface ISavedViewService
{
    /// <summary>The views of a list the person can open: their own and the shared ones.</summary>
    Task<Result<IReadOnlyList<SavedViewDto>>> ListAsync(Guid userId, Guid? organizationId, string? list, CancellationToken ct = default);

    Task<Result<SavedViewDto>> CreateAsync(Guid userId, Guid? organizationId, CreateSavedViewRequest request, CancellationToken ct = default);

    Task<Result<SavedViewDto>> UpdateAsync(Guid userId, Guid? organizationId, Guid id, UpdateSavedViewRequest request, Guid? expectedVersion, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result> SetDefaultAsync(Guid userId, Guid? organizationId, SetDefaultViewRequest request, CancellationToken ct = default);
}
