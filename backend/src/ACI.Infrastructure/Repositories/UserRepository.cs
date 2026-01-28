using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Users.FindAsync([id], ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User> AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserSettings?> GetSettingsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserSettings.FindAsync([userId], ct);

    public async Task<UserSettings> UpsertSettingsAsync(UserSettings settings, CancellationToken ct = default)
    {
        var existing = await _db.UserSettings.FindAsync([settings.UserId], ct);
        if (existing != null)
        {
            existing.CompanyName = settings.CompanyName;
            existing.BrandTone = settings.BrandTone;
            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
            _db.UserSettings.Update(existing);
        }
        else
        {
            _db.UserSettings.Add(settings);
        }
        await _db.SaveChangesAsync(ct);
        return existing ?? settings;
    }

    public async Task<CrmConnection?> GetConnectionAsync(Guid userId, CancellationToken ct = default) =>
        await _db.CrmConnections.FindAsync([userId], ct);

    public async Task<CrmConnection> UpsertConnectionAsync(CrmConnection connection, CancellationToken ct = default)
    {
        var existing = await _db.CrmConnections.FindAsync([connection.UserId], ct);
        if (existing != null)
        {
            existing.Connected = connection.Connected;
            existing.AccountEmail = connection.AccountEmail;
            existing.EncryptedToken = connection.EncryptedToken;
            existing.ConnectedAtUtc = connection.ConnectedAtUtc;
            existing.UpdatedAtUtc = connection.UpdatedAtUtc;
            _db.CrmConnections.Update(existing);
            await _db.SaveChangesAsync(ct);
            return existing;
        }
        _db.CrmConnections.Add(connection);
        await _db.SaveChangesAsync(ct);
        return connection;
    }
}
