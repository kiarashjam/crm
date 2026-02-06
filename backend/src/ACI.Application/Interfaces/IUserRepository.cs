using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User> AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<UserSettings?> GetSettingsAsync(Guid userId, CancellationToken ct = default);
    Task<UserSettings> UpsertSettingsAsync(UserSettings settings, CancellationToken ct = default);
}
