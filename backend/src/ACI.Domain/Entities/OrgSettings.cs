using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Per-organization settings: company name and brand tone for Intelligent Sales Writer.
/// </summary>
public class OrgSettings
{
    public Guid OrganizationId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public BrandTone BrandTone { get; set; } = BrandTone.Professional;
    public DateTime UpdatedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;
}
