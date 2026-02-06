using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing user settings in the CRM system.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IUserRepository userRepository, ILogger<SettingsService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting settings for user {UserId}", userId);
        
        var settings = await _userRepository.GetSettingsAsync(userId, ct);
        
        if (settings != null)
        {
            _logger.LogDebug("Retrieved settings for user {UserId}", userId);
            return MapToDto(settings);
        }

        _logger.LogDebug("No settings found for user {UserId}, returning defaults", userId);
        return GetDefaultSettings();
    }

    /// <inheritdoc />
    public async Task<UserSettingsDto> SaveAsync(Guid userId, UpdateUserSettingsRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Saving settings for user {UserId}", userId);
        
        var existing = await _userRepository.GetSettingsAsync(userId, ct);
        var settings = existing ?? new UserSettings
        {
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        // Apply partial updates - only update fields that were provided
        // Profile
        if (request.CompanyName != null) settings.CompanyName = request.CompanyName;
        if (request.JobTitle != null) settings.JobTitle = request.JobTitle;
        if (request.AvatarUrl != null) settings.AvatarUrl = request.AvatarUrl;
        if (request.Phone != null) settings.Phone = request.Phone;
        if (request.Timezone != null) settings.Timezone = request.Timezone;
        if (request.Language != null) settings.Language = request.Language;
        if (request.Bio != null) settings.Bio = request.Bio;

        // Brand
        if (request.BrandTone != null && Enum.TryParse<BrandTone>(request.BrandTone, true, out var bt))
            settings.BrandTone = bt;
        if (request.EmailSignature != null) settings.EmailSignature = request.EmailSignature;
        if (request.DefaultEmailSubjectPrefix != null) settings.DefaultEmailSubjectPrefix = request.DefaultEmailSubjectPrefix;

        // Appearance
        if (request.Theme != null && Enum.TryParse<Theme>(request.Theme, true, out var theme))
            settings.Theme = theme;
        if (request.DataDensity != null && Enum.TryParse<DataDensity>(request.DataDensity, true, out var density))
            settings.DataDensity = density;
        if (request.SidebarCollapsed.HasValue) settings.SidebarCollapsed = request.SidebarCollapsed.Value;
        if (request.ShowWelcomeBanner.HasValue) settings.ShowWelcomeBanner = request.ShowWelcomeBanner.Value;

        // Notifications
        if (request.EmailNotificationsEnabled.HasValue) settings.EmailNotificationsEnabled = request.EmailNotificationsEnabled.Value;
        if (request.EmailOnNewLead.HasValue) settings.EmailOnNewLead = request.EmailOnNewLead.Value;
        if (request.EmailOnDealUpdate.HasValue) settings.EmailOnDealUpdate = request.EmailOnDealUpdate.Value;
        if (request.EmailOnTaskDue.HasValue) settings.EmailOnTaskDue = request.EmailOnTaskDue.Value;
        if (request.EmailOnTeamMention.HasValue) settings.EmailOnTeamMention = request.EmailOnTeamMention.Value;
        if (request.EmailDigestFrequency != null && Enum.TryParse<EmailDigestFrequency>(request.EmailDigestFrequency, true, out var freq))
            settings.EmailDigestFrequency = freq;
        if (request.InAppNotificationsEnabled.HasValue) settings.InAppNotificationsEnabled = request.InAppNotificationsEnabled.Value;
        if (request.InAppSoundEnabled.HasValue) settings.InAppSoundEnabled = request.InAppSoundEnabled.Value;
        if (request.BrowserNotificationsEnabled.HasValue) settings.BrowserNotificationsEnabled = request.BrowserNotificationsEnabled.Value;

        // Defaults
        if (request.DefaultPipelineId != null) settings.DefaultPipelineId = request.DefaultPipelineId;
        if (request.DefaultLeadStatusId != null) settings.DefaultLeadStatusId = request.DefaultLeadStatusId;
        if (request.DefaultLeadSourceId != null) settings.DefaultLeadSourceId = request.DefaultLeadSourceId;
        if (request.DefaultFollowUpDays.HasValue) settings.DefaultFollowUpDays = request.DefaultFollowUpDays.Value;
        if (request.DefaultCurrency != null) settings.DefaultCurrency = request.DefaultCurrency;

        // Privacy
        if (request.ShowActivityStatus.HasValue) settings.ShowActivityStatus = request.ShowActivityStatus.Value;
        if (request.AllowAnalytics.HasValue) settings.AllowAnalytics = request.AllowAnalytics.Value;

        settings.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpsertSettingsAsync(settings, ct);
        
        _logger.LogInformation("Successfully saved settings for user {UserId}", userId);
        return MapToDto(settings);
    }

    /// <inheritdoc />
    public async Task<UserSettingsDto> ResetToDefaultsAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Resetting settings to defaults for user {UserId}", userId);
        
        var settings = new UserSettings
        {
            UserId = userId,
            CompanyName = "My Company",
            BrandTone = BrandTone.Professional,
            Theme = Theme.Light,
            DataDensity = DataDensity.Comfortable,
            Timezone = "UTC",
            Language = "en",
            EmailNotificationsEnabled = true,
            EmailOnNewLead = true,
            EmailOnDealUpdate = true,
            EmailOnTaskDue = true,
            EmailOnTeamMention = true,
            EmailDigestFrequency = EmailDigestFrequency.Daily,
            InAppNotificationsEnabled = true,
            InAppSoundEnabled = true,
            DefaultFollowUpDays = 3,
            DefaultCurrency = "USD",
            ShowActivityStatus = true,
            AllowAnalytics = true,
            ShowWelcomeBanner = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await _userRepository.UpsertSettingsAsync(settings, ct);
        
        _logger.LogInformation("Successfully reset settings to defaults for user {UserId}", userId);
        return MapToDto(settings);
    }

    private static UserSettingsDto MapToDto(UserSettings s) => new()
    {
        // Profile
        CompanyName = s.CompanyName,
        JobTitle = s.JobTitle,
        AvatarUrl = s.AvatarUrl,
        Phone = s.Phone,
        Timezone = s.Timezone,
        Language = s.Language,
        Bio = s.Bio,

        // Brand
        BrandTone = s.BrandTone.ToString().ToLowerInvariant(),
        EmailSignature = s.EmailSignature,
        DefaultEmailSubjectPrefix = s.DefaultEmailSubjectPrefix,

        // Appearance
        Theme = s.Theme.ToString().ToLowerInvariant(),
        DataDensity = s.DataDensity.ToString().ToLowerInvariant(),
        SidebarCollapsed = s.SidebarCollapsed,
        ShowWelcomeBanner = s.ShowWelcomeBanner,

        // Notifications
        EmailNotificationsEnabled = s.EmailNotificationsEnabled,
        EmailOnNewLead = s.EmailOnNewLead,
        EmailOnDealUpdate = s.EmailOnDealUpdate,
        EmailOnTaskDue = s.EmailOnTaskDue,
        EmailOnTeamMention = s.EmailOnTeamMention,
        EmailDigestFrequency = s.EmailDigestFrequency.ToString().ToLowerInvariant(),
        InAppNotificationsEnabled = s.InAppNotificationsEnabled,
        InAppSoundEnabled = s.InAppSoundEnabled,
        BrowserNotificationsEnabled = s.BrowserNotificationsEnabled,

        // Defaults
        DefaultPipelineId = s.DefaultPipelineId,
        DefaultLeadStatusId = s.DefaultLeadStatusId,
        DefaultLeadSourceId = s.DefaultLeadSourceId,
        DefaultFollowUpDays = s.DefaultFollowUpDays,
        DefaultCurrency = s.DefaultCurrency,

        // Privacy
        ShowActivityStatus = s.ShowActivityStatus,
        AllowAnalytics = s.AllowAnalytics,

        // Metadata
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };

    private static UserSettingsDto GetDefaultSettings() => new()
    {
        CompanyName = "My Company",
        BrandTone = "professional",
        Theme = "light",
        DataDensity = "comfortable",
        Timezone = "UTC",
        Language = "en",
        EmailNotificationsEnabled = true,
        EmailOnNewLead = true,
        EmailOnDealUpdate = true,
        EmailOnTaskDue = true,
        EmailOnTeamMention = true,
        EmailDigestFrequency = "daily",
        InAppNotificationsEnabled = true,
        InAppSoundEnabled = true,
        DefaultFollowUpDays = 3,
        DefaultCurrency = "USD",
        ShowActivityStatus = true,
        AllowAnalytics = true,
        ShowWelcomeBanner = true,
    };
}
