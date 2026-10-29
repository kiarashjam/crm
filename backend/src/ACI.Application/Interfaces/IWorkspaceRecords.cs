namespace ACI.Application.Interfaces;

/// <summary>Whether a lead, contact, company or deal exists in the caller's workspace.</summary>
public interface IWorkspaceRecords
{
    Task<bool> ExistsAsync(string entityType, Guid recordId, Guid userId, Guid? organizationId, CancellationToken ct = default);

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
