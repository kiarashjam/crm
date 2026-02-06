using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Record of generated/sent copy for history and dashboard stats.
/// </summary>
public class CopyHistoryItem : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Copy { get; set; } = string.Empty;
    public string? Subject { get; set; } // Email subject if applicable
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public RecipientType RecipientType { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public CopyTypeId CopyTypeId { get; set; }
    public string? Goal { get; set; }
    public string? BrandTone { get; set; }
    public bool IsRewritten { get; set; }
    
    // Analytics tracking
    public int CopyCount { get; set; } // Times copied to clipboard
    public int SendCount { get; set; } // Times sent
    public int ResponseCount { get; set; } // Responses received
    
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Organization? Organization { get; set; }
}
