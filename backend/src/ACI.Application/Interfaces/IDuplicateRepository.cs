using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>The records duplicates are looked for among, and the team's decisions about them.</summary>
public interface IDuplicateRepository
{
    /// <summary>
    /// The workspace's active contacts that share an email, a phone or a name with another
    /// one, with their companies. Only those: the rest cannot be in a group.
    /// </summary>
    Task<IReadOnlyList<Contact>> ContactCandidatesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The workspace's companies.</summary>
    Task<IReadOnlyList<Company>> CompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The pairs of this kind the workspace's people said are not the same.</summary>
    Task<IReadOnlyList<DuplicateDecision>> DecisionsAsync(string kind, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>How many of these ids are records of this kind in the workspace, for the kinds the server holds.</summary>
    Task<int?> CountInWorkspaceAsync(string kind, IReadOnlyCollection<Guid> ids, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Records each pair not yet recorded. Returns how many were new.</summary>
    Task<int> AddDecisionsAsync(string kind, IReadOnlyCollection<(Guid A, Guid B)> pairs, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Forgets the decisions on these pairs, either way round. Returns how many went.</summary>
    Task<int> RemoveDecisionsAsync(string kind, IReadOnlyCollection<(Guid A, Guid B)> pairs, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
