using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Tracks Intelligent Sales Writer analytics and usage statistics.
/// </summary>
public class CopyAnalytics : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public CopyTypeId CopyTypeId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string? BrandTone { get; set; }
    public int GenerationCount { get; set; } = 1;
    public int RewriteCount { get; set; }
    public int CopyCount { get; set; } // Clipboard copies
    public int SendCount { get; set; } // Sent via integrations
    public int ResponseCount { get; set; } // Tracked responses
    public double ResponseRate => SendCount > 0 ? (double)ResponseCount / SendCount * 100 : 0;
    public DateTime DateUtc { get; set; } // Aggregated by day
    
    public User? User { get; set; }
    public Organization? Organization { get; set; }
}

/// <summary>
/// Tracks individual copy conversions (responses).
/// </summary>
public class CopyConversion : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? CopyHistoryId { get; set; }
    public CopyTypeId CopyTypeId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public ConversionType ConversionType { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    
    public User? User { get; set; }
    public Organization? Organization { get; set; }
    public CopyHistoryItem? CopyHistory { get; set; }
}

public enum ConversionType
{
    Replied,
    Meeting,
    Call,
    Demo,
    Deal,
    Other
}
