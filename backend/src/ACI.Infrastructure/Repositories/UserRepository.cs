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
            // Profile
            existing.CompanyName = settings.CompanyName;
            existing.JobTitle = settings.JobTitle;
            existing.AvatarUrl = settings.AvatarUrl;
            existing.Phone = settings.Phone;
            existing.Timezone = settings.Timezone;
            existing.Language = settings.Language;
            existing.Bio = settings.Bio;

            // Brand
            existing.BrandTone = settings.BrandTone;
            existing.EmailSignature = settings.EmailSignature;
            existing.DefaultEmailSubjectPrefix = settings.DefaultEmailSubjectPrefix;

            // Appearance
            existing.Theme = settings.Theme;
            existing.DataDensity = settings.DataDensity;
            existing.SidebarCollapsed = settings.SidebarCollapsed;
            existing.ShowWelcomeBanner = settings.ShowWelcomeBanner;

            // Notifications
            existing.EmailNotificationsEnabled = settings.EmailNotificationsEnabled;
            existing.EmailOnNewLead = settings.EmailOnNewLead;
            existing.EmailOnDealUpdate = settings.EmailOnDealUpdate;
            existing.EmailOnTaskDue = settings.EmailOnTaskDue;
            existing.EmailOnTeamMention = settings.EmailOnTeamMention;
            existing.EmailDigestFrequency = settings.EmailDigestFrequency;
            existing.InAppNotificationsEnabled = settings.InAppNotificationsEnabled;
            existing.InAppSoundEnabled = settings.InAppSoundEnabled;
            existing.BrowserNotificationsEnabled = settings.BrowserNotificationsEnabled;

            // Defaults
            existing.DefaultPipelineId = settings.DefaultPipelineId;
            existing.DefaultLeadStatusId = settings.DefaultLeadStatusId;
            existing.DefaultLeadSourceId = settings.DefaultLeadSourceId;
            existing.DefaultFollowUpDays = settings.DefaultFollowUpDays;
            existing.DefaultCurrency = settings.DefaultCurrency;

            // Privacy
            existing.ShowActivityStatus = settings.ShowActivityStatus;
            existing.AllowAnalytics = settings.AllowAnalytics;

            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
            _db.UserSettings.Update(existing);
        }
        else
        {
            settings.CreatedAtUtc = DateTime.UtcNow;
            _db.UserSettings.Add(settings);
        }
        await _db.SaveChangesAsync(ct);
        return existing ?? settings;
    }
}
