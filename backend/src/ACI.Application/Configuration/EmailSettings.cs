namespace ACI.Application.Configuration;

/// <summary>
/// SMTP and SPA URL settings for transactional email (password reset).
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "";

    public int SmtpPort { get; set; } = 587;

    public string SmtpUser { get; set; } = "";

    public string SmtpPassword { get; set; } = "";

    public bool UseSsl { get; set; } = true;

    public string FromAddress { get; set; } = "";

    public string FromName { get; set; } = "Cadence";

    /// <summary>
    /// Absolute origin of the SPA (http or https, with host), no trailing slash. Required for password reset links;
    /// invalid or empty values cause reset to be skipped (logged) while the API still returns a generic success.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// When SMTP is not configured, log the reset URL and treat send as successful so local dev can complete the flow.
    /// Disable in production when real SMTP is required.
    /// </summary>
    public bool LogResetLinksWhenSmtpNotConfigured { get; set; }
}
