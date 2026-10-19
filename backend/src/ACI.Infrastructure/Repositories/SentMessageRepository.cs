using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;

namespace ACI.Infrastructure.Repositories;

public sealed class SentMessageRepository : ISentMessageRepository
{
    private readonly AppDbContext _db;
    public SentMessageRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SentMessage message, CancellationToken ct = default)
    {
        _db.SentMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }
}
