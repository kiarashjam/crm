using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly AppDbContext _db;

    public AttachmentRepository(AppDbContext db)
    {
        _db = db;
    }

    private static IQueryable<Attachment> InWorkspace(IQueryable<Attachment> q, Guid userId, Guid? organizationId) =>
        organizationId is { } orgId
            ? q.Where(a => a.OrganizationId == orgId)
            : q.Where(a => a.OrganizationId == null && a.OwnerUserId == userId);

    // Everything but the bytes, so listing a record's files never reads them.
    private static IQueryable<Attachment> WithoutContent(IQueryable<Attachment> q) =>
        q.Select(a => new Attachment
        {
            Id = a.Id,
            OrganizationId = a.OrganizationId,
            OwnerUserId = a.OwnerUserId,
            EntityType = a.EntityType,
            RecordId = a.RecordId,
            FileName = a.FileName,
            ContentType = a.ContentType,
            Size = a.Size,
            UploadedByName = a.UploadedByName,
            UploadedAtUtc = a.UploadedAtUtc,
        });

    public async Task<IReadOnlyList<Attachment>> ListAsync(
        Guid userId, Guid? organizationId, string entityType, Guid recordId, CancellationToken ct = default) =>
        await WithoutContent(InWorkspace(_db.Attachments.AsNoTracking(), userId, organizationId)
                .Where(a => a.EntityType == entityType && a.RecordId == recordId)
                .OrderByDescending(a => a.UploadedAtUtc))
            .ToListAsync(ct);

    public Task<Attachment?> FindAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        WithoutContent(InWorkspace(_db.Attachments.AsNoTracking(), userId, organizationId).Where(a => a.Id == id))
            .FirstOrDefaultAsync(ct);

    public Task<Attachment?> GetWithContentAsync(Guid id, CancellationToken ct = default) =>
        _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Attachment attachment, CancellationToken ct = default)
    {
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // A stub, so deleting doesn't first read the file it is deleting.
        _db.Entry(new Attachment { Id = id }).State = EntityState.Deleted;
        await _db.SaveChangesAsync(ct);
    }
}
