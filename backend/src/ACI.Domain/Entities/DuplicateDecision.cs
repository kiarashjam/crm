namespace ACI.Domain.Entities;

/// <summary>
/// Someone in the workspace said two records that look alike are not the same: a father
/// and son with one name and the club's landline, say. Kept for the whole team, so the pair
/// is not offered for merging again, to them or to anyone else.
/// </summary>
/// <remarks>
/// One row per pair, read either way round. The records are not tied by a foreign key: a
/// pair can be of contacts, companies, leads, deals, members or households, and a decision
/// about a record since deleted simply matches nothing.
/// </remarks>
public class DuplicateDecision : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary>Who decided; with no organization, also whose personal workspace it is.</summary>
    public Guid UserId { get; set; }

    /// <summary>One of <see cref="DuplicateKinds.All"/>.</summary>
    public string Kind { get; set; } = DuplicateKinds.Contacts;

    public Guid RecordIdA { get; set; }
    public Guid RecordIdB { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Organization? Organization { get; set; }
}

/// <summary>The kinds of record whose duplicates are found and decided on.</summary>
public static class DuplicateKinds
{
    public const string Contacts = "contacts";
    public const string Companies = "companies";
    public const string Leads = "leads";
    public const string Deals = "deals";
    public const string Members = "members";
    public const string Households = "households";

    public static readonly IReadOnlyList<string> All = [Contacts, Companies, Leads, Deals, Members, Households];

    /// <summary>The known kind for <paramref name="value"/>, ignoring case, or null.</summary>
    public static string? Normalize(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return v != null && All.Contains(v) ? v : null;
    }
}
