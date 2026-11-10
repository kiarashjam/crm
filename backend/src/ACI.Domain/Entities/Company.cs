namespace ACI.Domain.Entities;

/// <summary>
/// Company (organization) owned by a user; contacts and deals can be linked.
/// </summary>
public class Company : Common.BaseEntity, Common.IVersioned
{
    public Guid UserId { get; set; }
    /// <summary>When set, data is scoped by organization; when null, legacy user-scoped.</summary>
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Industry { get; set; }
    public string? Size { get; set; }
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// The import that created this company (see <see cref="ImportBatch"/>), as a row of its
    /// own or as the company named on an imported lead or contact; null for one made any other way.
    /// </summary>
    public Guid? ImportBatchId { get; set; }

    /// <summary>A new value on every save; an edit made from an older one is refused (see <see cref="Deal.Version"/>).</summary>
    public Guid Version { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
