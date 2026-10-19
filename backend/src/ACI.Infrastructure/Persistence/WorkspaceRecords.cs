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
}
