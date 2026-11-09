namespace ACI.Application.Common;

/// <summary>
/// Finds the teammates a comment or a note mentions: "@Anna Keller", "@Anna", "@anna".
/// </summary>
/// <remarks>
/// <para>
/// A mention is plain text, so it reads the same in a timeline, an export or the MCP
/// tools, and anyone can type one without a picker. Each person answers to their full
/// name, their email address, and their first name or the part of their address before
/// the @ when nobody else in the workspace shares it. Where two handles fit, the longer
/// wins, so "@Anna Keller" is Anna Keller even with an Anna in the team.
/// </para>
/// <para>
/// A handle two people share names nobody: telling either of them would be a guess. An
/// @ straight after a letter is an email address in the text, not a mention.
/// </para>
/// </remarks>
public static class Mentions
{
    public sealed record Person(Guid UserId, string? Name, string? Email);

    /// <summary>The people mentioned in <paramref name="text"/>, in the order they first appear.</summary>
    public static IReadOnlyList<Guid> Find(string? text, IEnumerable<Person> people)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('@')) return [];
        var handles = Handles(people);
        var found = new List<Guid>();
        for (var at = text.IndexOf('@'); at >= 0; at = text.IndexOf('@', at + 1))
        {
            if (at > 0 && (char.IsLetterOrDigit(text[at - 1]) || text[at - 1] is '.' or '_' or '-' or '+')) continue;
            var rest = text.AsSpan(at + 1);
            Guid? best = null;
            var bestLength = 0;
            foreach (var (handle, userId) in handles)
            {
                if (handle.Length <= bestLength || rest.Length < handle.Length) continue;
                if (!rest.StartsWith(handle, StringComparison.OrdinalIgnoreCase)) continue;
                // "@Anna" is not "@Annabel", but "@Anna," and "@Anna." are Anna.
                if (rest.Length > handle.Length && IsNameChar(rest[handle.Length])) continue;
                best = userId;
                bestLength = handle.Length;
            }
            if (best is Guid id && !found.Contains(id)) found.Add(id);
        }
        return found;
    }

    /// <summary>A comment's or note's text, subject and body together, as mentions are read from it.</summary>
    public static string TextOf(string? subject, string? body) => $"{subject}\n{body}";

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

    private static List<(string Handle, Guid UserId)> Handles(IEnumerable<Person> people)
    {
        var candidates = new List<(string Handle, Guid UserId, bool OnlyIfUnique)>();
        foreach (var person in people)
        {
            var name = string.Join(' ', (person.Name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (name.Length > 0)
            {
                candidates.Add((name, person.UserId, false));
                var first = name.Split(' ')[0];
                if (first != name) candidates.Add((first, person.UserId, true));
            }
            var email = (person.Email ?? "").Trim();
            if (email.Length > 0)
            {
                candidates.Add((email, person.UserId, false));
                var local = email.Split('@')[0];
                if (local.Length > 0 && local != email) candidates.Add((local, person.UserId, true));
            }
        }

        // Full names and addresses are kept unless two people share one; first names and
        // address parts only when no one else answers to the same, as a full name or otherwise.
        candidates.RemoveAll(c => c.Handle.Length < 2);
        var answering = candidates
            .GroupBy(c => c.Handle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(c => c.UserId).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
        var full = candidates
            .Where(c => !c.OnlyIfUnique)
            .GroupBy(c => c.Handle, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(c => c.UserId).Distinct().Count() == 1)
            .Select(g => (Handle: g.Key, g.First().UserId))
            .ToList();
        var fullHandles = full.Select(f => f.Handle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unique = candidates
            .Where(c => c.OnlyIfUnique && answering[c.Handle] == 1 && !fullHandles.Contains(c.Handle))
            .Select(c => (c.Handle, c.UserId));
        return full.Concat(unique).ToList();
    }
}
