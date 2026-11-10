using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Households: who belongs together, one primary each, and whose bills roll up to them. The
/// one model of families for the club and the CRM (see ClubHousehold in ACI.Domain).
/// </summary>
/// <remarks>
/// <para>
/// A household's people are club members, CRM contacts, or both; a contact is in a household
/// directly or through the member linked to them. One household per person and one primary
/// per household are checked here, with a sentence, and held by unique indexes on SQL Server.
/// </para>
/// <para>
/// Every change to a household's people also saves the household, checked against the version
/// it was read at, so two edits of one household at once are one success and one 409, and a
/// change sent from a page opened before someone else's (with the household's version) is
/// refused rather than applied to what it never saw.
/// </para>
/// <para>
/// Roll-up billing is read by the ledger when it posts a charge (IClubLedger): a member whose
/// link rolls up has their charges, closed tabs and dues put on the primary's account, saying
/// who incurred them. Switching it changes what is posted from then on, never what was.
/// </para>
/// </remarks>
public interface IClubHouseholds
{
    Task<IReadOnlyList<HouseholdDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<HouseholdDto>> GetAsync(Guid organizationId, Guid householdId, CancellationToken ct = default);

    /// <summary>The household the member is in, or null.</summary>
    Task<HouseholdDto?> ForMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>The household the contact is in, directly or through the member linked to them, or null.</summary>
    Task<HouseholdDto?> ForContactAsync(Guid organizationId, Guid contactId, CancellationToken ct = default);

    /// <summary>The households these contacts are in, by contact id; a contact in none is left out.</summary>
    Task<IReadOnlyDictionary<Guid, HouseholdDto>> ForContactsAsync(Guid organizationId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default);

    /// <summary>A new household with its primary (a member or a contact).</summary>
    Task<Result<HouseholdDto>> CreateAsync(Guid organizationId, Guid userId, HouseholdWriteRequest request, CancellationToken ct = default);

    /// <summary>Renames the household or changes its notes.</summary>
    Task<Result<HouseholdDto>> UpdateAsync(Guid organizationId, Guid householdId, HouseholdWriteRequest request, CancellationToken ct = default);

    /// <summary>Deletes the household and its links; the members and contacts stay.</summary>
    Task<Result> DeleteAsync(Guid organizationId, Guid householdId, CancellationToken ct = default);

    /// <summary>
    /// Folds duplicate households into <paramref name="householdId"/>, in one save: their people
    /// move to it (each once; a duplicate's primary joins as Other, keeping whether their bills
    /// roll up only when this household's primary is a member), the notes are kept together, and
    /// the duplicates go.
    /// </summary>
    Task<Result<HouseholdDto>> MergeAsync(Guid organizationId, Guid householdId, IReadOnlyCollection<Guid> duplicateIds, CancellationToken ct = default);

    Task<Result<HouseholdDto>> AddPersonAsync(Guid organizationId, Guid householdId, Guid userId, HouseholdPersonRequest request, CancellationToken ct = default);

    /// <summary>Changes a person's role or roll-up billing (the latter needs money.manage).</summary>
    Task<Result<HouseholdDto>> UpdatePersonAsync(Guid organizationId, Guid householdId, Guid linkId, HouseholdPersonRequest request, CancellationToken ct = default);

    Task<Result<HouseholdDto>> RemovePersonAsync(Guid organizationId, Guid householdId, Guid linkId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// Makes the person the household's primary. The old primary takes the new one's relation to
    /// them turned round (a spouse stays a spouse, a child's parent becomes their parent), and the
    /// new primary's own bills stop rolling up.
    /// </summary>
    Task<Result<HouseholdDto>> SetPrimaryAsync(Guid organizationId, Guid householdId, Guid linkId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// The household's statement for [fromUtc, toUtc): the primary's account, where the bills that
    /// roll up land, each line saying whose it was, and what the others owe on accounts of their own.
    /// </summary>
    Task<Result<StatementFile>> StatementPdfAsync(Guid organizationId, Guid householdId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    // ── the contact pages' relationships card ─────────────────────────────────

    /// <summary>
    /// Records, from <paramref name="contactId"/>'s page, that <paramref name="relatedContactId"/>
    /// is their <paramref name="type"/> (a ContactRelationshipTypes value) as a household tie: a
    /// spouse or partner always, another family tie when the two share a household. Makes the
    /// household when neither is in one. Answers the link that holds the tie.
    /// </summary>
    Task<Result<Guid>> RelateContactsAsync(Guid organizationId, Guid userId, Guid contactId, Guid relatedContactId, string type, CancellationToken ct = default);

    /// <summary>
    /// Ends the household tie the link <paramref name="linkId"/> holds, from <paramref name="contactId"/>'s
    /// page: whichever of the two is not the primary leaves the household.
    /// </summary>
    Task<Result> UnrelateAsync(Guid organizationId, Guid contactId, Guid linkId, CancellationToken ct = default);
}

/// <summary>A household to make, or the fields of one to change: a field left out is kept.</summary>
public sealed class HouseholdWriteRequest
{
    public string? Name { get; set; }
    public string? Notes { get; set; }

    /// <summary>On making one: its primary, a member or (for someone not yet a member) a contact.</summary>
    public Guid? PrimaryMemberId { get; set; }
    public Guid? PrimaryContactId { get; set; }

    /// <summary>The household's version the change was made from (or If-Match); an older one is refused.</summary>
    public Guid? Version { get; set; }
}

/// <summary>Someone to add to a household, or the role and roll-up of someone in it.</summary>
public sealed class HouseholdPersonRequest
{
    public Guid? MemberId { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>Spouse, Partner, Child, Dependent, Parent, Sibling or Other: their relation to the primary.</summary>
    public string? Role { get; set; }

    /// <summary>Whether their charges, tabs and dues go on the primary's account. Money, so it takes money.manage.</summary>
    public bool? RollUpBilling { get; set; }

    public Guid? Version { get; set; }
}

public sealed class HouseholdMergeRequest
{
    public List<Guid> DuplicateIds { get; set; } = new();
}

public sealed class HouseholdPrimaryRequest
{
    public Guid LinkId { get; set; }
    public Guid? Version { get; set; }
}

/// <summary>One person in a household: a member, a contact, or a member and their contact.</summary>
public sealed record HouseholdPersonDto
{
    public Guid LinkId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContactId { get; init; }

    /// <summary>The lead the contact came from, when there is one: the CRM card links to it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LeadId { get; init; }

    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public bool RollUpBilling { get; init; }

    /// <summary>For a member: their tier, status and dues.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Tier { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberStatus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? DuesAmount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DuesFrequency { get; init; }

    /// <summary>For a member: their own account's balance (negative while they owe).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? HouseBalance { get; init; }

    /// <summary>A contact who is archived: still in the household, shown as such.</summary>
    public bool IsArchived { get; init; }

    public string Source { get; init; } = string.Empty;
    public DateTime AddedAtUtc { get; init; }
}

public sealed record HouseholdDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>The primary, when the household has one: a member, or a contact not yet a member.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? PrimaryLinkId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? PrimaryMemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PrimaryName { get; init; }

    /// <summary>The primary first, then in the order they joined.</summary>
    public IReadOnlyList<HouseholdPersonDto> People { get; init; } = Array.Empty<HouseholdPersonDto>();

    /// <summary>How many of its people are club members.</summary>
    public int Members { get; init; }

    /// <summary>The workspace currency the balances are in.</summary>
    public string Currency { get; init; } = string.Empty;

    public Guid Version { get; init; }
}
