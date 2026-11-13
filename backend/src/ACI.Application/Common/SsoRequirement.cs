using System.Globalization;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// Who a workspace's single sign-on requirement holds (Organization.SsoRequired and SsoDomains),
/// and its list of email domains.
/// </summary>
public static partial class SsoRequirement
{
    public const int MaxDomains = 20;

    /// <summary>
    /// Whether a workspace requiring single sign-on holds this member to it. Never the owner,
    /// so that a provider that stops working (its app deleted, the server's settings changed)
    /// cannot lock the workspace out: the owner can still sign in, with a password and 2FA, and
    /// turn the requirement off. With domains listed, only members whose address is at one of
    /// them; without, every other member.
    /// </summary>
    public static bool Holds(bool required, string? domains, bool isOwner, string memberEmail)
    {
        if (!required || isOwner) return false;
        var listed = Parse(domains);
        return listed.Count == 0 || listed.Contains(DomainOf(memberEmail));
    }

    /// <summary>The domains as stored: lower case, comma-separated.</summary>
    public static IReadOnlyList<string> Parse(string? stored) =>
        (stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.ToLowerInvariant())
            .Distinct()
            .ToList();

    /// <summary>
    /// The domains as typed ("@Pavillon46.ch", "café.ch"), cleaned and checked: the stored form,
    /// null for none, or the first one that is not a domain.
    /// </summary>
    public static Result<string?> Normalize(IEnumerable<string>? typed)
    {
        var domains = new List<string>();
        var idn = new IdnMapping();
        foreach (var raw in typed ?? [])
        {
            var text = (raw ?? "").Trim().TrimStart('@').TrimEnd('.').ToLowerInvariant();
            if (text.Length == 0) continue;
            string ascii;
            try
            {
                ascii = idn.GetAscii(text);
            }
            catch (ArgumentException)
            {
                return Result.Failure<string?>(DomainErrors.Sso.InvalidDomain(raw!.Trim()));
            }
            if (!DomainPattern().IsMatch(ascii)) return Result.Failure<string?>(DomainErrors.Sso.InvalidDomain(raw!.Trim()));
            if (!domains.Contains(ascii)) domains.Add(ascii);
        }
        if (domains.Count > MaxDomains) return Result.Failure<string?>(DomainErrors.Sso.TooManyDomains);
        // Explicitly: a bare null would convert to a failed result.
        return Result.Success<string?>(domains.Count == 0 ? null : string.Join(',', domains));
    }

    /// <summary>The part of an address after the @, lower case and in its ASCII form, as domains are stored.</summary>
    public static string DomainOf(string email)
    {
        var at = (email ?? "").LastIndexOf('@');
        var domain = at < 0 ? "" : email![(at + 1)..].Trim().ToLowerInvariant();
        try
        {
            return domain.Length == 0 ? "" : new IdnMapping().GetAscii(domain);
        }
        catch (ArgumentException)
        {
            return domain;
        }
    }

    // Labels of letters, digits and inner hyphens, at least two of them, the last not all digits.
    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?=[a-z0-9-]*[a-z])[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DomainPattern();
}
