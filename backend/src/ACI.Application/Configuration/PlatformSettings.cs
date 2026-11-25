namespace ACI.Application.Configuration;

/// <summary>
/// The platform itself, above every workspace: who runs it.
/// </summary>
/// <remarks>
/// <para>
/// A platform admin is someone who signs in with one of <see cref="AdminEmails"/> (compared
/// without regard to case) and has confirmed that address. They may read the site analytics
/// of cadencee.ai (/api/site-analytics) and receive its daily email. Being one gives nothing
/// inside any workspace.
/// </para>
/// <para>
/// Set as Platform__AdminEmails in the host's configuration (the deploy sets it from the
/// PLATFORM_ADMIN_EMAILS secret). Never in appsettings.json: the repository is public.
/// </para>
/// </remarks>
public sealed class PlatformSettings
{
    public const string SectionName = "Platform";

    /// <summary>The platform admins' email addresses, comma-separated. Empty: there are none.</summary>
    public string AdminEmails { get; set; } = "";
}

/// <summary>The site analytics of cadencee.ai (SiteAnalyticsController).</summary>
public sealed class SiteAnalyticsSettings
{
    public const string SectionName = "SiteAnalytics";

    /// <summary>Whether visits are recorded. Off: the tracker's posts are answered and dropped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The secret each day's salt is made from (HMAC of the day with it). Empty: one is
    /// generated once and kept in the database, protected with the server's data-protection
    /// keys. Set it (SiteAnalytics__Secret, 32 or more random characters) to keep it
    /// out of the database altogether. Changing it mid-day counts that day's visitors twice.
    /// </summary>
    public string Secret { get; set; } = "";
}
