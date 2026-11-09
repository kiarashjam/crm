using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// The placeholders an email may use — {{firstName}}, {{company}} and the rest — and
/// how they are filled, the same way for every sender.
/// </summary>
/// <remarks>
/// <para>
/// Each sender used to fill its own: sequences knew three placeholders, automations
/// knew {{firstName}} in the body only, and the composer knew none, so a placeholder
/// the editor suggested could reach a prospect literally. The outbox now fills every
/// message through here.
/// </para>
/// <para>
/// A placeholder may carry its own fallback, used when the record has no value:
/// "Hi {{firstName|there}}". Without one, an empty value leaves nothing behind in
/// unattended mail, and the composer, where a person is waiting, says which
/// placeholder it cannot fill instead of sending "Hi ,".
/// </para>
/// </remarks>
public static partial class MergeFields
{
    public const string FirstName = "firstName";
    public const string LastName = "lastName";
    public const string Name = "name";
    public const string Email = "email";
    public const string Company = "company";
    public const string SenderName = "senderName";
    public const string Workspace = "workspace";

    /// <summary>Every placeholder the outbox fills, as the editor may offer them.</summary>
    public static readonly IReadOnlyList<string> Known = new[] { FirstName, LastName, Name, Email, Company, SenderName, Workspace };

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*(?:\|(?<fallback>[^{}]*))?\}\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// The values for one recipient. <paramref name="name"/> is split for the first and
    /// last name: "Jean-Michel Dupont" gives "Jean-Michel" and "Dupont".
    /// </summary>
    public static IReadOnlyDictionary<string, string?> For(string? name, string? email, string? company, string? senderName, string? workspace)
    {
        var full = (name ?? "").Trim();
        var space = full.IndexOf(' ');
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [FirstName] = space < 0 ? full : full[..space],
            [LastName] = space < 0 ? "" : full[(space + 1)..].Trim(),
            [Name] = full,
            [Email] = email?.Trim(),
            [Company] = company?.Trim(),
            [SenderName] = senderName?.Trim(),
            [Workspace] = workspace?.Trim(),
        };
    }

    /// <summary>
    /// The text with each placeholder replaced by its value, else its fallback, else
    /// nothing. <paramref name="unfilled"/> names, once each and as written, every
    /// placeholder that had neither: an unknown one, or one this recipient has no value for.
    /// </summary>
    public static string Fill(string? template, IReadOnlyDictionary<string, string?> values, out IReadOnlyList<string> unfilled)
    {
        var missing = new List<string>();
        var filled = Placeholder().Replace(template ?? "", match =>
        {
            var key = match.Groups["key"].Value;
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            if (match.Groups["fallback"].Success) return match.Groups["fallback"].Value.Trim();
            var written = "{{" + key + "}}";
            if (!missing.Contains(written, StringComparer.OrdinalIgnoreCase)) missing.Add(written);
            return "";
        });
        unfilled = missing;
        return filled;
    }

    /// <summary>The text with every placeholder filled as well as it can be.</summary>
    public static string Fill(string? template, IReadOnlyDictionary<string, string?> values) => Fill(template, values, out _);
}
