namespace ACI.Application.Interfaces;

/// <summary>Whether a lead, contact, company, deal or club member exists in the caller's workspace.</summary>
public interface IWorkspaceRecords
{
    /// <summary>
    /// Whether the record is the caller's to see: in the workspace, a deal as DealVisibility
    /// shows it to them, and a member only in an organization and not erased.
    /// </summary>
    Task<bool> ExistsAsync(string entityType, Guid recordId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Which of <paramref name="recordIds"/> the caller can see, by the rule of <see cref="ExistsAsync"/>.</summary>
    Task<IReadOnlySet<Guid>> VisibleAsync(string entityType, IReadOnlyCollection<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The names of those of <paramref name="recordIds"/> the caller can see: a member's first and last name, any other record's name.</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Whether work can be handed to this person here: a member of the organization or,
    /// in a personal workspace, only the caller.
    /// </summary>
    Task<bool> IsAssignableAsync(Guid assigneeId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Whether the pipeline is this organization's. A personal workspace has none.</summary>
    Task<bool> PipelineExistsAsync(Guid pipelineId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The pipeline a stage belongs to, when the stage is this organization's; otherwise null.</summary>
    Task<Guid?> StagePipelineAsync(Guid stageId, Guid? organizationId, CancellationToken ct = default);
}
