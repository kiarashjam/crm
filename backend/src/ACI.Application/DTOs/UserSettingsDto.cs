using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Comprehensive user settings DTO.
/// </summary>
public record UserSettingsDto
{
    // Profile
    public string CompanyName { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Phone { get; init; }
    public string Timezone { get; init; } = "UTC";
    public string Language { get; init; } = "en";
    public string? Bio { get; init; }

    // Brand
    public string BrandTone { get; init; } = "professional";
    public string? EmailSignature { get; init; }
    public string? DefaultEmailSubjectPrefix { get; init; }

    // Appearance
    public string Theme { get; init; } = "light";
    public string DataDensity { get; init; } = "comfortable";
    public bool SidebarCollapsed { get; init; } = false;
    public bool ShowWelcomeBanner { get; init; } = true;

    // Notifications
    public bool EmailNotificationsEnabled { get; init; } = true;
    public bool EmailOnNewLead { get; init; } = true;
    public bool EmailOnDealUpdate { get; init; } = true;
    public bool EmailOnTaskDue { get; init; } = true;
    public bool EmailOnTeamMention { get; init; } = true;
    public string EmailDigestFrequency { get; init; } = "daily";
    public bool InAppNotificationsEnabled { get; init; } = true;
    public bool InAppSoundEnabled { get; init; } = true;
    public bool BrowserNotificationsEnabled { get; init; } = false;

    // Defaults
    public string? DefaultPipelineId { get; init; }
    public string? DefaultLeadStatusId { get; init; }
    public string? DefaultLeadSourceId { get; init; }
    public int DefaultFollowUpDays { get; init; } = 3;
    public string? DefaultCurrency { get; init; } = "USD";

    // Privacy
    public bool ShowActivityStatus { get; init; } = true;
    public bool AllowAnalytics { get; init; } = true;

    // Metadata (read-only)
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>
/// Partial update request for user settings.
/// </summary>
public record UpdateUserSettingsRequest
{
    // Profile
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? CompanyName { get; init; }

    [StringLength(100, ErrorMessage = "Job title cannot exceed 100 characters")]
    public string? JobTitle { get; init; }

    [StringLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters")]
    [Url(ErrorMessage = "Invalid URL format")]
    public string? AvatarUrl { get; init; }

    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters")]
    [Phone(ErrorMessage = "Invalid phone format")]
    public string? Phone { get; init; }

    [StringLength(50, ErrorMessage = "Timezone cannot exceed 50 characters")]
    public string? Timezone { get; init; }

    [StringLength(10, ErrorMessage = "Language cannot exceed 10 characters")]
    public string? Language { get; init; }

    [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters")]
    public string? Bio { get; init; }

    // Brand
    [StringLength(50, ErrorMessage = "Brand tone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    [StringLength(2000, ErrorMessage = "Email signature cannot exceed 2000 characters")]
    public string? EmailSignature { get; init; }

    [StringLength(100, ErrorMessage = "Default email subject prefix cannot exceed 100 characters")]
    public string? DefaultEmailSubjectPrefix { get; init; }

    // Appearance
    [StringLength(20, ErrorMessage = "Theme cannot exceed 20 characters")]
    [RegularExpression(@"^(light|dark|system)$", ErrorMessage = "Theme must be one of: light, dark, system")]
    public string? Theme { get; init; }

    [StringLength(20, ErrorMessage = "Data density cannot exceed 20 characters")]
    [RegularExpression(@"^(compact|comfortable|spacious)$", ErrorMessage = "Data density must be one of: compact, comfortable, spacious")]
    public string? DataDensity { get; init; }

    public bool? SidebarCollapsed { get; init; }
    public bool? ShowWelcomeBanner { get; init; }

    // Notifications
    public bool? EmailNotificationsEnabled { get; init; }
    public bool? EmailOnNewLead { get; init; }
    public bool? EmailOnDealUpdate { get; init; }
    public bool? EmailOnTaskDue { get; init; }
    public bool? EmailOnTeamMention { get; init; }

    [StringLength(20, ErrorMessage = "Email digest frequency cannot exceed 20 characters")]
    [RegularExpression(@"^(never|daily|weekly)$", ErrorMessage = "Email digest frequency must be one of: never, daily, weekly")]
    public string? EmailDigestFrequency { get; init; }

    public bool? InAppNotificationsEnabled { get; init; }
    public bool? InAppSoundEnabled { get; init; }
    public bool? BrowserNotificationsEnabled { get; init; }

    // Defaults
    [StringLength(100, ErrorMessage = "Default pipeline ID cannot exceed 100 characters")]
    public string? DefaultPipelineId { get; init; }

    [StringLength(100, ErrorMessage = "Default lead status ID cannot exceed 100 characters")]
    public string? DefaultLeadStatusId { get; init; }

    [StringLength(100, ErrorMessage = "Default lead source ID cannot exceed 100 characters")]
    public string? DefaultLeadSourceId { get; init; }

    [Range(1, 365, ErrorMessage = "Default follow-up days must be between 1 and 365")]
    public int? DefaultFollowUpDays { get; init; }

    [StringLength(10, ErrorMessage = "Default currency cannot exceed 10 characters")]
    public string? DefaultCurrency { get; init; }

    // Privacy
    public bool? ShowActivityStatus { get; init; }
    public bool? AllowAnalytics { get; init; }
}
