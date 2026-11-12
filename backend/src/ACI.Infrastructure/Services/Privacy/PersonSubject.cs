using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>
/// The person an export or an erasure is about: the records staff confirmed are theirs, the
/// addresses they use, and what text about them looks like.
/// </summary>
internal sealed class PersonSubject
{
    public List<Lead> Leads { get; init; } = new();
    public List<Contact> Contacts { get; init; } = new();
    public List<ClubMember> Members { get; init; } = new();
    public List<ClubApplication> Applications { get; init; } = new();

    /// <summary>
    /// Addresses only this person uses: mail, consent and the suppression list are searched by
    /// them, and they go on the suppression list when the person is erased.
    /// </summary>
    public HashSet<string> Addresses { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Addresses a record outside the selection still uses (a family mailbox, say): searched
    /// only through the selected records, never by themselves, and not suppressed.
    /// </summary>
    public HashSet<string> SharedAddresses { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The deals made for the person: on their contact, or from their lead.</summary>
    public List<Deal> Deals { get; set; } = new();

    public HashSet<Guid> LeadIds => Leads.Select(l => l.Id).ToHashSet();
    public HashSet<Guid> ContactIds => Contacts.Select(c => c.Id).ToHashSet();
    public HashSet<Guid> MemberIds => Members.Select(m => m.Id).ToHashSet();
    public HashSet<Guid> ApplicationIds => Applications.Select(a => a.Id).ToHashSet();
    public HashSet<Guid> DealIds => Deals.Select(d => d.Id).ToHashSet();

    /// <summary>Every id of the person's own records, for links that name any of them.</summary>
    public HashSet<Guid> RecordIds
    {
        get
        {
            var ids = new HashSet<Guid>();
            ids.UnionWith(LeadIds);
            ids.UnionWith(ContactIds);
            ids.UnionWith(MemberIds);
            ids.UnionWith(ApplicationIds);
            return ids;
        }
    }

    public int RecordCount => Leads.Count + Contacts.Count + Members.Count + Applications.Count;

    /// <summary>The person's names as their records give them, longest first.</summary>
    public IReadOnlyList<string> Names =>
        Leads.Select(l => l.Name)
            .Concat(Contacts.Select(c => c.Name))
            .Concat(Members.Where(m => m.ErasedAtUtc == null).Select(m => $"{m.FirstName} {m.LastName}"))
            .Concat(Applications.Select(a => $"{a.FirstName} {a.LastName}"))
            .Select(n => Regex.Replace(n ?? "", @"\s+", " ").Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .ToList();

    /// <summary>
    /// What text about the person is searched for in records that are not theirs (a note on
    /// someone else's lead, an email to someone else) and replaced by the pseudonym: their own
    /// addresses, and their names of two words or more. A single first name would match
    /// everyone else who has it.
    /// </summary>
    public IReadOnlyList<string> Needles =>
        Addresses
            .Concat(Names.Where(n => n.Contains(' ') && n.Length >= 5))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .ToList();

    /// <summary>Text with every mention of the person replaced by <paramref name="pseudonym"/>.</summary>
    public string? Scrub(string? text, string pseudonym)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = text;
        foreach (var needle in Needles)
            result = Regex.Replace(result, Regex.Escape(needle), pseudonym, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return result;
    }

    /// <summary>Whether <paramref name="text"/> mentions the person at all.</summary>
    public bool Mentions(string? text) =>
        !string.IsNullOrEmpty(text) && Needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a comma separated recipient list names one of the person's own addresses.</summary>
    public bool Addressed(string? recipients) => Recipients(recipients).Any(Addresses.Contains);

    /// <summary>The addresses in a comma or semicolon separated list, as the outbox compares them.</summary>
    public static IEnumerable<string> Recipients(string? list) =>
        (list ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Address);

    /// <summary>"Anna Keller &lt;anna@x.ch&gt;" or "anna@x.ch", as an address to compare.</summary>
    public static string Address(string entry)
    {
        var open = entry.LastIndexOf('<');
        var close = entry.LastIndexOf('>');
        var bare = open >= 0 && close > open ? entry[(open + 1)..close] : entry;
        return ErasedAddresses.Normalize(bare);
    }

    /// <summary>A recipient list without the person's own addresses, or null when nobody else is left.</summary>
    public string? WithoutPerson(string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) return list;
        var kept = list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !Addresses.Contains(Address(e)))
            .ToList();
        return kept.Count == 0 ? null : string.Join(", ", kept);
    }
}
