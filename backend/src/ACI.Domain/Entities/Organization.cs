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
    /// <summary>
    /// SHA-256 (hex) of the key website forms and Zapier post leads with. The raw key
    /// is shown once, when it is made; a copy of the table cannot post as anyone.
    /// </summary>
    public string? WebhookApiKeyHash { get; set; }
    /// <summary>Timestamp when the webhook API key was created.</summary>
    public DateTime? WebhookApiKeyCreatedAtUtc { get; set; }
    /// <summary>
    /// SHA-256 (hex) of the key AI assistants connect with over MCP, which reads the
    /// whole workspace. Kept apart from <see cref="WebhookApiKeyHash"/>, whose key only
    /// posts leads and so ends up in website forms and Zapier; the raw key is shown once.
    /// </summary>
    public string? McpApiKeyHash { get; set; }
    public DateTime? McpApiKeyCreatedAtUtc { get; set; }
    /// <summary>Optional shared password for JSON webhooks. When null or empty, the app default password applies.</summary>
    public string? WebhookPassword { get; set; }
    /// <summary>Where Cadence posts lead and deal events. Empty means it posts nowhere.</summary>
    public string? OutboundWebhookUrl { get; set; }
    public string? OutboundWebhookSecret { get; set; }
    /// <summary>
    /// ISO 4217 code new deals are priced in unless they say otherwise. A label only:
    /// changing it converts nothing. Club pages keep their own currency in the hospitality
    /// settings.
    /// </summary>
    public string Currency { get; set; } = "CHF";
    /// <summary>
    /// How amounts are written for this workspace (its number format), as a BCP 47 name
    /// ("de-CH"). Month and day names stay in each viewer's language, as the app is in English.
    /// </summary>
    public string Locale { get; set; } = "de-CH";
    /// <summary>IANA time zone the workspace's days and months begin in, for reports and forecasts.</summary>
    public string TimeZone { get; set; } = "Europe/Zurich";
    /// <summary>
    /// Days without activity after which an open deal is flagged as stale on the board,
    /// unless its pipeline sets its own (<see cref="Pipeline.StaleDealDays"/>). 0: never.
    /// </summary>
    public int StaleDealDays { get; set; } = 30;

    public User OwnerUser { get; set; } = null!;
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Invite> Invites { get; set; } = new List<Invite>();
    public ICollection<JoinRequest> JoinRequests { get; set; } = new List<JoinRequest>();
    public ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
    public ICollection<LeadStatus> LeadStatuses { get; set; } = new List<LeadStatus>();
    public ICollection<LeadSource> LeadSources { get; set; } = new List<LeadSource>();
    public OrgSettings? Settings { get; set; }
}
