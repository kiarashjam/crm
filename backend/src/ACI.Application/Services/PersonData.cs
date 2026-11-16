using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// A person's data rights in one workspace: find everything it holds about someone, hand it
/// over as one file, and erase it.
/// </summary>
/// <remarks>
/// <para>
/// A person is not one record here: a lead with a name and an email has a contact made for
/// it, a member may be linked to both, and the same address turns up on applications,
/// bookings, gift cards, mail and consent. Staff could delete a lead and leave its contact,
/// its activities, its sent mail, its sequence and its name in the audit trail behind, and
/// the next save of another lead brought the contact back. Nobody could answer an access
/// request at all.
/// </para>
/// <para>
/// So it goes in two steps. <see cref="FindAsync"/> starts from an address or a record and
/// gathers what looks like the same person: records with the same address, and records
/// linked to them. Staff then confirm which of those are really this person (two members
/// sharing a family mailbox are two people) and the export and the erasure take exactly that
/// selection, plus everything hanging off it.
/// </para>
/// </remarks>
public interface IPersonData
{
    /// <summary>Records that look like the person, each saying why it was found.</summary>
    Task<Result<PersonFoundDto>> FindAsync(Guid organizationId, PersonLookup lookup, CancellationToken ct = default);

    /// <summary>
    /// Everything held about the selection, as a zip of JSON files with a readable summary,
    /// the files on their records included. Written to the audit trail.
    /// </summary>
    Task<Result<PersonExportFile>> ExportAsync(Guid organizationId, Guid userId, PersonSelection selection, CancellationToken ct = default);

    /// <summary>What an erasure of the selection would delete, anonymise, cancel and keep. Changes nothing.</summary>
    Task<Result<ErasurePlanDto>> PlanErasureAsync(Guid organizationId, PersonSelection selection, CancellationToken ct = default);

    /// <summary>
    /// Erases the selection, once <see cref="EraseRequest.Confirm"/> says ERASE: deletes what
    /// can go, anonymises what the books must keep, cancels what is still to come, and puts
    /// the addresses on the suppression list. The audit line it leaves names nobody.
    /// </summary>
    Task<Result<ErasureResultDto>> EraseAsync(Guid organizationId, Guid userId, EraseRequest request, CancellationToken ct = default);

    /// <summary>The workspace's log of erasures, newest first: numbers, dates and counts, no names.</summary>
    Task<IReadOnlyList<PersonErasureDto>> ErasuresAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>Where to start looking: an email address, or one of the person's records.</summary>
public sealed class PersonLookup
{
    public string? Email { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? MemberId { get; set; }
}

/// <summary>The kinds of record a person can be held as.</summary>
public static class PersonRecordKinds
{
    public const string Lead = "lead";
    public const string Contact = "contact";
    public const string Member = "member";
    public const string Application = "application";
}

/// <summary>A record that looks like the person.</summary>
/// <param name="Kind"><see cref="PersonRecordKinds"/>.</param>
/// <param name="FoundBy">Why it was found, in words: "the record you started from", "same email address", "linked to the lead …".</param>
public sealed record PersonRecordDto(string Kind, Guid Id, string Name, string? Email, string? Detail, string FoundBy);

/// <summary>What a lookup found: the addresses involved and the records.</summary>
public sealed record PersonFoundDto(IReadOnlyList<string> Addresses, IReadOnlyList<PersonRecordDto> Records);

/// <summary>
/// The records and addresses staff confirmed are this one person. An export and an erasure
/// take exactly these, and everything that hangs off them.
/// </summary>
public class PersonSelection
{
    public List<Guid> LeadIds { get; set; } = new();
    public List<Guid> ContactIds { get; set; } = new();
    public List<Guid> MemberIds { get; set; } = new();
    public List<Guid> ApplicationIds { get; set; } = new();
    /// <summary>
    /// Addresses mail, consent and the suppression list are searched by. The records' own
    /// addresses are always included; an address another record outside the selection still
    /// uses is searched only through the selected records, never by itself.
    /// </summary>
    public List<string> Addresses { get; set; } = new();
}

/// <summary>An erasure, confirmed.</summary>
public sealed class EraseRequest : PersonSelection
{
    /// <summary>Must be "ERASE".</summary>
    public string? Confirm { get; set; }
    /// <summary>Staff's own reference for the request (a ticket number, the date of the letter): never a name or an address.</summary>
    public string? Reference { get; set; }
}

/// <summary>What an erasure does to one kind of record.</summary>
public static class ErasureActions
{
    public const string Delete = "delete";
    public const string Anonymise = "anonymise";
    public const string Cancel = "cancel";
    /// <summary>Kept as it is, for a reason the line gives: a signed contract, say.</summary>
    public const string Keep = "keep";
}

/// <summary>One line of an erasure plan or result: what happens to how many of one kind of record.</summary>
/// <param name="Area">What the records are, in words: "Leads", "Charges and payments"…</param>
/// <param name="Action"><see cref="ErasureActions"/>.</param>
/// <param name="Note">Why, or what stays: "amounts, dates and VAT stay, under the pseudonym".</param>
public sealed record ErasureLine(string Area, string Action, int Count, string? Note);

/// <summary>What an erasure would do, read without changing anything.</summary>
/// <param name="Warnings">What staff should know first: money still owed, a card saved with Stripe, a signed contract kept.</param>
/// <param name="AddressesSuppressed">The addresses that go on the suppression list.</param>
/// <param name="AddressesShared">Addresses another record outside the selection still uses: not suppressed, and searched only through the selected records.</param>
public sealed record ErasurePlanDto(
    int Records,
    IReadOnlyList<ErasureLine> Lines,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> AddressesSuppressed,
    IReadOnlyList<string> AddressesShared);

/// <summary>A finished erasure.</summary>
/// <param name="Pseudonym">What the books call the person now: "Erased member 12" or "Erased person 12".</param>
public sealed record ErasureResultDto(int Number, string Pseudonym, DateTime ErasedAtUtc, IReadOnlyList<ErasureLine> Lines);

/// <summary>A line of the log of erasures.</summary>
public sealed record PersonErasureDto(
    int Number, string Trigger, DateTime ErasedAtUtc, string? ErasedByName, string? Reference, IReadOnlyDictionary<string, int> Summary);

/// <summary>An export, ready to download.</summary>
public sealed record PersonExportFile(byte[] Content, string FileName);
