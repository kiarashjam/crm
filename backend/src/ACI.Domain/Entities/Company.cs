namespace ACI.Domain.Entities;

/// <summary>
/// Company (organization) owned by a user; contacts and deals can be linked.
/// </summary>
public class Company : Common.BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>When set, data is scoped by organization; when null, legacy user-scoped.</summary>
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Industry { get; set; }
    public string? Size { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
