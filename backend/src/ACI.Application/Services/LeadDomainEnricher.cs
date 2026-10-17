namespace ACI.Application.Services;

/// <summary>
/// Reads a company name from an email domain. Free inboxes are skipped.
/// Nothing here is invented firmographic data.
/// </summary>
public static class LeadDomainEnricher
{
    /// <summary>
    /// Personal-mail providers: an address here says nothing about an employer.
    /// The Swiss and neighbouring ones were missing, so a lead writing from
    /// bluewin.ch — Swisscom's consumer mail, about as common as addresses get in
    /// Switzerland — was attached to a company called "Bluewin", along with every
    /// other Bluewin lead, and scored extra points for "having a company".
    /// </summary>
    private static readonly HashSet<string> FreeInboxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com",
        "yahoo.com", "yahoo.co.uk", "icloud.com", "me.com", "mac.com", "aol.com",
        "proton.me", "protonmail.com", "protonmail.ch", "pm.me", "gmx.com", "msn.com",
        // Switzerland
        "bluewin.ch", "bluemail.ch", "gmx.ch", "hispeed.ch", "sunrise.ch", "swissonline.ch",
        "netplus.ch", "vtxmail.ch", "mail.ch", "hotmail.ch", "outlook.ch",
        // Neighbours
        "gmx.net", "gmx.de", "gmx.at", "web.de", "t-online.de", "freenet.de",
        "orange.fr", "free.fr", "laposte.net", "sfr.fr", "wanadoo.fr", "hotmail.fr", "outlook.fr", "live.fr", "yahoo.fr",
        "libero.it", "virgilio.it", "tiscali.it", "alice.it", "hotmail.it", "yahoo.it", "yahoo.de", "hotmail.de", "outlook.de",
    };

    /// <summary>Second-level labels under which the registrable name sits one label further left (acme.co.uk).</summary>
    private static readonly HashSet<string> SecondLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "co", "com", "org", "net", "ac", "gov", "edu",
    };

    public static bool TryCompanyFromEmail(string email, out string domain, out string companyName)
    {
        domain = "";
        companyName = "";
        var at = email.LastIndexOf('@');
        if (at < 1 || at == email.Length - 1) return false;
        var host = email[(at + 1)..].Trim().ToLowerInvariant();
        if (host.Length < 3 || !host.Contains('.')) return false;

        // The registrable domain, not the host: jane@us.ibm.com is IBM, and the
        // first label used to name the company "Us". Mail from a subsidiary host
        // now also lands on the same company as its parent.
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2) return false;
        var take = labels.Length >= 3 && labels[^1].Length == 2 && SecondLevel.Contains(labels[^2]) ? 3 : 2;
        domain = string.Join('.', labels[^take..]);
        if (FreeInboxes.Contains(domain) || FreeInboxes.Contains(host)) return false;

        var label = labels[^take];
        if (label.Length < 2) return false;
        companyName = char.ToUpperInvariant(label[0]) + label[1..];
        return true;
    }
}
