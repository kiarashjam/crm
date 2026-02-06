namespace ACI.Domain.Entities;

/// <summary>
/// Org-level lead source (e.g. Website, Referral, Ads); configurable by admin.
/// </summary>
public class LeadSource : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Organization Organization { get; set; } = null!;
}
