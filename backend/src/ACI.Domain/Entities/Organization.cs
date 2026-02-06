namespace ACI.Domain.Entities;

/// <summary>
/// Organization (tenant) — CRM data is scoped per organization.
/// </summary>
public class Organization : Common.BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>User who owns the organization; can invite, accept/reject join requests.</summary>
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>API key for webhook authentication. Generated per organization.</summary>
    public string? WebhookApiKey { get; set; }
    /// <summary>Timestamp when the webhook API key was created.</summary>
    public DateTime? WebhookApiKeyCreatedAtUtc { get; set; }

    public User OwnerUser { get; set; } = null!;
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Invite> Invites { get; set; } = new List<Invite>();
    public ICollection<JoinRequest> JoinRequests { get; set; } = new List<JoinRequest>();
    public ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
    public ICollection<LeadStatus> LeadStatuses { get; set; } = new List<LeadStatus>();
    public ICollection<LeadSource> LeadSources { get; set; } = new List<LeadSource>();
    public OrgSettings? Settings { get; set; }
}
