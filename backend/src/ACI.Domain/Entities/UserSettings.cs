using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Per-user settings: comprehensive user preferences and configuration.
/// </summary>
public class UserSettings
{
    public Guid UserId { get; set; }

    // =========== PROFILE ===========
    public string CompanyName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Language { get; set; } = "en";
    public string? Bio { get; set; }

    // =========== BRAND ===========
    public BrandTone BrandTone { get; set; } = BrandTone.Professional;
    public string? EmailSignature { get; set; }
    public string? DefaultEmailSubjectPrefix { get; set; }

    // =========== APPEARANCE ===========
    public Theme Theme { get; set; } = Theme.Light;
    public DataDensity DataDensity { get; set; } = DataDensity.Comfortable;
    public bool SidebarCollapsed { get; set; } = false;
    public bool ShowWelcomeBanner { get; set; } = true;

    // =========== NOTIFICATIONS ===========
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool EmailOnNewLead { get; set; } = true;
    public bool EmailOnDealUpdate { get; set; } = true;
    public bool EmailOnTaskDue { get; set; } = true;
    public bool EmailOnTeamMention { get; set; } = true;
    public EmailDigestFrequency EmailDigestFrequency { get; set; } = EmailDigestFrequency.Daily;
    public bool InAppNotificationsEnabled { get; set; } = true;
    public bool InAppSoundEnabled { get; set; } = true;
    public bool BrowserNotificationsEnabled { get; set; } = false;

    // =========== DEFAULTS ===========
    public string? DefaultPipelineId { get; set; }
    public string? DefaultLeadStatusId { get; set; }
    public string? DefaultLeadSourceId { get; set; }
    public int DefaultFollowUpDays { get; set; } = 3;
    public string? DefaultCurrency { get; set; } = "USD";

    // =========== PRIVACY ===========
    public bool ShowActivityStatus { get; set; } = true;
    public bool AllowAnalytics { get; set; } = true;

    // =========== METADATA ===========
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
