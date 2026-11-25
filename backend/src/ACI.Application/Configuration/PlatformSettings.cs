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
    /// The most events one server stores in a Zurich day; past it, posts are answered and
    /// dropped until the next day, and a warning is logged once. Bounds what a flood the rate
    /// limits let through can add to the table.
    /// </summary>
    public int DailyEventCap { get; set; } = 200_000;
}
