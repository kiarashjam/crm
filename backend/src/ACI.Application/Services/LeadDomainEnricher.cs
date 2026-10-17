namespace ACI.Application.Services;

/// <summary>
/// Reads a company name from an email domain. Free inboxes are skipped.
/// Nothing here is invented firmographic data.
/// </summary>
public static class LeadDomainEnricher
{
    private static readonly HashSet<string> FreeInboxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com",
        "yahoo.com", "yahoo.co.uk", "icloud.com", "me.com", "aol.com",
        "proton.me", "protonmail.com", "gmx.com", "msn.com",
    };

    public static bool TryCompanyFromEmail(string email, out string domain, out string companyName)
    {
        domain = "";
        companyName = "";
        var at = email.LastIndexOf('@');
        if (at < 1 || at == email.Length - 1) return false;
        domain = email[(at + 1)..].Trim().ToLowerInvariant();
        if (domain.Length < 3 || !domain.Contains('.') || FreeInboxes.Contains(domain)) return false;

        var label = domain.Split('.')[0];
        if (label.Length < 2) return false;
        companyName = char.ToUpperInvariant(label[0]) + label[1..];
        return true;
    }
}
