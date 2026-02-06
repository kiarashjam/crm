namespace ACI.Domain.Entities;

/// <summary>
/// Org-level lead status (e.g. New, Contacted, Qualified, Lost); configurable by admin.
/// </summary>
public class LeadStatus : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Organization Organization { get; set; } = null!;
}
