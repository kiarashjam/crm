using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Copy template (system or user-owned) with goal and copy type.
/// </summary>
public class Template : Common.BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public CopyTypeId CopyTypeId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string? Content { get; set; } // Optional: Pre-written template content
    public string? BrandTone { get; set; } // professional, friendly, persuasive
    public string? Length { get; set; } // short, medium, long
    public int UseCount { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public bool IsSharedWithOrganization { get; set; } // Team sharing
    public bool IsSystemTemplate { get; set; } // System default templates
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public User? User { get; set; }
    public Organization? Organization { get; set; }
}
