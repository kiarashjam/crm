namespace ACI.Application.Common;

/// <summary>
/// Which records look like the same person or company, and how they group together. The
/// browser's demo mode follows the same rules (src/app/api/dedupe.ts).
/// </summary>
/// <remarks>
/// <para>
/// A contact's email is unique in its workspace, so the same person entered twice has two
/// addresses: that is the duplicate to find, and a different email cannot rule a match out.
/// People are the same with the same email, the same phone, or the same name when neither
/// their phones nor their companies disagree. Companies are the same with the same web
/// domain, or the same name when their domains do not disagree. A value one record lacks
/// never rules a match out.
/// </para>
/// <para>
/// Records linked by any matching pair, directly or through one another, form a group. A
/// pair someone marked as not the same does not link them. A key shared by more than
/// <see cref="MaxShared"/> records (an office switchboard, a very common name) is no sign
/// of a duplicate and is left out.
/// </para>
/// </remarks>
public static class DuplicateRules
{
    public const string SameEmail = "Same email";
    public const string SamePhone = "Same phone";
    public const string SameName = "Same name";
    public const string SameDomain = "Same domain";

    /// <summary>More records than this sharing one key say nothing about any two of them.</summary>
    public const int MaxShared = 50;

    /// <summary>A person, with each key already written as <see cref="MatchKeys"/> writes it.</summary>
    public sealed record Person(Guid Id, string? Email, string? Phone, string? Name, Guid? CompanyId);

    /// <summary>A person from what the record holds, each key written as <see cref="MatchKeys"/> writes it.</summary>
    public static Person PersonOf(Guid id, string? email, string? phone, string? name, Guid? companyId) =>
        new(id, MatchKeys.Email(email), MatchKeys.Phone(phone), MatchKeys.Name(name), companyId);

    /// <summary>A company, with each key already written as <see cref="MatchKeys"/> writes it.</summary>
    public sealed record Firm(Guid Id, string? Domain, string? Name);

    /// <param name="Key">The records' ids, sorted and joined by "|": the same for the same records, as the browser keys a group.</param>
    public sealed record Group(string Key, string Reason, IReadOnlyList<Guid> Ids);

    /// <param name="Groups">The duplicates still to deal with, most records first.</param>
    /// <param name="NotSame">The groups the team's "not the same" decisions broke up or hid, as they would be without them.</param>
    public sealed record Scan(IReadOnlyList<Group> Groups, IReadOnlyList<Group> NotSame);

    private sealed record Rule<T>(string Reason, Func<T, string?> Key, Func<T, T, bool>? Same = null);

    private static readonly Rule<Person>[] PersonRules =
    [
        new(SameEmail, p => p.Email),
        new(SamePhone, p => p.Phone),
        new(SameName, p => p.Name, (a, b) => !Conflicts(a.Phone, b.Phone) && !Conflicts(a.CompanyId, b.CompanyId)),
    ];

    private static readonly Rule<Firm>[] FirmRules =
    [
        new(SameDomain, f => f.Domain),
        new(SameName, f => f.Name, (a, b) => !Conflicts(a.Domain, b.Domain)),
    ];

    public static Scan Contacts(IEnumerable<Person> people, IEnumerable<(Guid A, Guid B)> notSame) =>
        Run(people.ToList(), p => p.Id, PersonRules, notSame);

    public static Scan Companies(IEnumerable<Firm> firms, IEnumerable<(Guid A, Guid B)> notSame) =>
        Run(firms.ToList(), f => f.Id, FirmRules, notSame);

    /// <summary>Why two people look like one, the strongest reason first; empty when they do not.</summary>
    public static IReadOnlyList<string> Matches(Person a, Person b) =>
        PersonRules
            .Where(rule => rule.Key(a) is { Length: > 0 } key && key == rule.Key(b) && (rule.Same == null || rule.Same(a, b)))
            .Select(rule => rule.Reason)
            .ToList();

    /// <summary>The pair written one way, whichever record comes first.</summary>
    public static (Guid A, Guid B) Pair(Guid x, Guid y) => x.CompareTo(y) <= 0 ? (x, y) : (y, x);

    /// <summary>The same key the browser gives a group: the ids, sorted as text, joined by "|".</summary>
    public static string KeyOf(IEnumerable<Guid> ids) =>
        string.Join('|', ids.Select(id => id.ToString()).OrderBy(id => id, StringComparer.Ordinal));

    private static bool Conflicts(string? a, string? b) => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a != b;

    private static bool Conflicts(Guid? a, Guid? b) => a != null && b != null && a != b;

    private static Scan Run<T>(List<T> records, Func<T, Guid> id, Rule<T>[] rules, IEnumerable<(Guid A, Guid B)> notSame)
    {
        var dismissed = notSame.Select(p => Pair(p.A, p.B)).ToHashSet();
        var pairs = new List<(Guid A, Guid B, string Reason)>();
        var unique = records.GroupBy(id).Select(g => g.First()).ToList();
        foreach (var rule in rules)
        {
            var buckets = unique
                .Select(r => (Record: r, Key: rule.Key(r)))
                .Where(x => !string.IsNullOrEmpty(x.Key))
                .GroupBy(x => x.Key!, StringComparer.Ordinal);
            foreach (var bucket in buckets)
            {
                var list = bucket.Select(x => x.Record).ToList();
                if (list.Count < 2 || list.Count > MaxShared) continue;
                for (var i = 0; i < list.Count; i++)
                    for (var j = i + 1; j < list.Count; j++)
                        if (rule.Same == null || rule.Same(list[i], list[j]))
                            pairs.Add((id(list[i]), id(list[j]), rule.Reason));
            }
        }

        var order = rules.Select(r => r.Reason).ToList();
        var ids = unique.Select(id).ToList();
        var all = GroupPairs(ids, pairs, order);
        var kept = GroupPairs(ids, pairs.Where(p => !dismissed.Contains(Pair(p.A, p.B))).ToList(), order);
        var keptKeys = kept.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var hidden = all
            .Where(g => !keptKeys.Contains(g.Key) && pairs.Any(p => dismissed.Contains(Pair(p.A, p.B)) && g.Ids.Contains(p.A)))
            .ToList();
        return new Scan(kept, hidden);
    }

    private static List<Group> GroupPairs(List<Guid> ids, List<(Guid A, Guid B, string Reason)> pairs, List<string> order)
    {
        var parent = ids.ToDictionary(i => i, i => i);
        Guid Find(Guid x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }
        foreach (var (a, b, _) in pairs)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[rb] = ra;
        }

        var reasons = pairs
            .GroupBy(p => Find(p.A))
            .ToDictionary(g => g.Key, g => g.Select(p => p.Reason).Distinct().OrderBy(order.IndexOf).ToList());
        return ids
            .GroupBy(Find)
            .Where(g => g.Count() > 1)
            .Select(g => new Group(KeyOf(g), string.Join(" · ", reasons.GetValueOrDefault(g.Key) ?? []), g.ToList()))
            .OrderByDescending(g => g.Ids.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
    }
}
