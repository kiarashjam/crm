using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class EmailImageRepository : IEmailImageRepository
{
    private readonly AppDbContext _db;
    public EmailImageRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmailImage>> ListAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.EmailImages.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(take)
            // Everything but the bytes: a library of hundreds of images is read as a list.
            .Select(i => new EmailImage
            {
                Id = i.Id, OrganizationId = i.OrganizationId, FileName = i.FileName, ContentType = i.ContentType, Size = i.Size,
                Width = i.Width, Height = i.Height, CreatedAtUtc = i.CreatedAtUtc, CreatedByUserId = i.CreatedByUserId,
            })
            .ToListAsync(ct);

    public Task<int> CountAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.EmailImages.CountAsync(i => i.OrganizationId == organizationId, ct);

    public async Task AddAsync(EmailImage image, CancellationToken ct = default)
    {
        _db.EmailImages.Add(image);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var image = await _db.EmailImages.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, ct);
        if (image == null) return false;
        _db.EmailImages.Remove(image);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Read past the workspace filter on purpose: a mail app fetching an image is in no
    // workspace. The id is random and only ever in the workspace's own emails, and the
    // bytes are an image the workspace chose to put in front of everyone it emails.
    public Task<EmailImage?> GetPublicAsync(Guid id, CancellationToken ct = default) =>
        _db.EmailImages.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
}
