using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Whether a record exists in the caller's workspace, by the same rule the record
/// repositories use: in an organization, its records; with none, the user's own.
/// </summary>
public sealed class WorkspaceRecords : IWorkspaceRecords
{
    private readonly AppDbContext _db;

    public WorkspaceRecords(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(string entityType, Guid recordId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        entityType switch
        {
            RecordTypes.Lead => _db.Leads.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Contact => _db.Contacts.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Company => _db.Companies.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Deal => _db.Deals.VisibleTo(_db, userId, organizationId).AnyAsync(r => r.Id == recordId, ct),
            _ => Task.FromResult(false),
        };

    public Task<bool> IsAssignableAsync(Guid assigneeId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? Task.FromResult(assigneeId == userId)
            : _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == assigneeId, ct);

    public Task<bool> PipelineExistsAsync(Guid pipelineId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? Task.FromResult(false)
            : _db.Pipelines.AnyAsync(p => p.Id == pipelineId && p.OrganizationId == organizationId, ct);

    public async Task<Guid?> StagePipelineAsync(Guid stageId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? null
            : await _db.DealStages
                .Where(s => s.Id == stageId && s.Pipeline.OrganizationId == organizationId)
                .Select(s => (Guid?)s.PipelineId)
                .FirstOrDefaultAsync(ct);
}
