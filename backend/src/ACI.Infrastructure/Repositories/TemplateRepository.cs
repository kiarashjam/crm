using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly AppDbContext _db;

    public TemplateRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Templates.Where(t => t.UserId == null).OrderBy(t => t.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<Template>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Templates.Where(t => t.UserId == userId).OrderBy(t => t.Title).ToListAsync(ct);

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Templates.FindAsync([id], ct);
}
