namespace ACI.Application.Interfaces;

/// <summary>Whether a lead, contact, company or deal exists in the caller's workspace.</summary>
public interface IWorkspaceRecords
{
    Task<bool> ExistsAsync(string entityType, Guid recordId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
