namespace ACI.Domain.Entities;

/// <summary>
/// Contact (person) owned by a user; optionally linked to a company.
/// </summary>
public class Contact : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Lead conversion traceability
    public Guid? ConvertedFromLeadId { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }

    // Soft delete / archive
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    // HP-7: Contact description / notes
    public string? Description { get; set; }

    // Contact preferences / compliance
    public bool DoNotContact { get; set; }
    public string? PreferredContactMethod { get; set; }

    /// <summary>
    /// The import that created this contact (see <see cref="ImportBatch"/>), including the
    /// contact a lead from an import is given; null for one made any other way.
    /// </summary>
    public Guid? ImportBatchId { get; set; }

    /// <summary>
    /// The phone as duplicates are matched on it: its last nine digits, so "+41 79 123 45 67"
    /// and "079 123 45 67" are one number. Null when it has fewer than seven digits. Written
    /// on every save (AppDbContext), never by hand.
    /// </summary>
    public string? PhoneKey { get; set; }

    /// <summary>
    /// The name as duplicates are matched on it: lower case, no accents, single spaces. Empty
    /// when the name is too short to match on; null only on a row from before the column,
    /// until start-up fills it. Written on every save (AppDbContext), never by hand.
    /// </summary>
    public string? NameKey { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public User? ArchivedByUser { get; set; }
    public Organization? Organization { get; set; }
    public Company? Company { get; set; }
    public Lead? ConvertedFromLead { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
