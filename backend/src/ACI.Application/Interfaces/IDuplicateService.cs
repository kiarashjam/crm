using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Finds a workspace's duplicate contacts and companies on the server, and keeps the team's
/// "not the same" decisions for every kind of record.
/// </summary>
public interface IDuplicateService
{
    Task<DuplicateScanDto<ContactDto>> ScanContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<DuplicateScanDto<CompanyDto>> ScanCompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The pairs of this kind the team said are not the same, for kinds found in the browser.</summary>
    Task<Result<IReadOnlyList<DuplicatePairDto>>> NotSameAsync(string kind, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Keeps every pair among these records apart from now on, for everyone in the workspace.</summary>
    Task<Result> MarkNotSameAsync(string kind, IReadOnlyList<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Offers every pair among these records again.</summary>
    Task<Result> ClearNotSameAsync(string kind, IReadOnlyList<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
