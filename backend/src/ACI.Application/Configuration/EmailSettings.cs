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

    /// <summary>Absolute origin of this API, used for the email-open pixel.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5160";

    /// <summary>
    /// When SMTP is not configured, log the reset URL and treat send as successful so local dev can complete the flow.
    /// Disable in production when real SMTP is required.
    /// </summary>
    public bool LogResetLinksWhenSmtpNotConfigured { get; set; }

    /// <summary>
    /// Domains the mail service has authenticated (SendGrid → Sender Authentication →
    /// Authenticate Your Domain), each with the workspace it was verified for, comma
    /// separated: "acme.ch=&lt;workspace id&gt;, club.example=&lt;workspace id&gt;". A
    /// workspace's own sending address is used as From only when its domain is listed
    /// here for that workspace; otherwise its email goes out from <see cref="FromAddress"/>,
    /// which the relay accepts. Listing a domain the relay has not verified makes that
    /// workspace's email fail, so add one here only after the mail service shows it as verified.
    /// </summary>
    /// <remarks>
    /// The workspace id is required. Anyone can sign up and save any sending address, so a
    /// domain listed on its own would let every workspace send as it: as another customer,
    /// or as the shared sender's own domain. An entry without a workspace id counts for none.
    /// The same domain may be listed once for each workspace it belongs to.
    /// </remarks>
    public string VerifiedSendingDomains { get; set; } = "";

    /// <summary>
    /// The domain whose mail SendGrid Inbound Parse receives (its MX record points at
    /// mx.sendgrid.net), e.g. "reply.cadence.example". When set, every workspace email
    /// also names reply+token@this-domain in Reply-To, so a reply comes back into the CRM
    /// as well as reaching the person who sent it. Empty: replies go to that person only.
    /// </summary>
    public string InboundReplyDomain { get; set; } = "";

    /// <summary>
    /// The secret in the Inbound Parse address SendGrid posts to:
    /// /api/public/email/inbound/{secret}. Empty: that endpoint answers 404.
    /// </summary>
    public string InboundParseSecret { get; set; } = "";

    /// <summary>
    /// The secret in the Event Webhook address SendGrid posts delivery reports to:
    /// /api/public/email/events/{secret}.
    /// </summary>
    public string EventWebhookSecret { get; set; } = "";

    /// <summary>
    /// The verification key SendGrid shows for a signed Event Webhook (base64, an ECDSA
    /// public key). When set, a report must carry a valid signature as well. With neither
    /// this nor <see cref="EventWebhookSecret"/>, the events endpoint answers 404.
    /// </summary>
    public string EventWebhookVerificationKey { get; set; } = "";

    /// <summary>
    /// Whether the mail service has verified <paramref name="domain"/> for sending on behalf
    /// of the workspace <paramref name="organizationId"/> (see <see cref="VerifiedSendingDomains"/>).
    /// </summary>
    public bool IsVerifiedSendingDomain(string? domain, Guid? organizationId)
    {
        var wanted = (domain ?? "").Trim().TrimEnd('.');
        if (wanted.Length == 0 || organizationId is not Guid workspace) return false;
        foreach (var entry in (VerifiedSendingDomains ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = entry.IndexOf('=');
            if (equals < 0) continue;
            if (string.Equals(entry[..equals].Trim().TrimEnd('.'), wanted, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(entry[(equals + 1)..].Trim(), out var verifiedFor) && verifiedFor == workspace)
                return true;
        }
        return false;
    }

    /// <summary>The reply-capture domain, or null when none is set.</summary>
    public string? ReplyDomain()
    {
        var domain = (InboundReplyDomain ?? "").Trim().Trim('@').TrimEnd('.');
        return domain.Length == 0 ? null : domain.ToLowerInvariant();
    }

    /// <summary>
    /// Whether replies come back into the CRM: a reply domain to name in Reply-To, and the
    /// Inbound Parse endpoint switched on to take them. With the domain alone, replies to
    /// it would be posted to an endpoint that turns them away, so it is not named.
    /// </summary>
    public bool CapturesReplies => ReplyDomain() != null && !string.IsNullOrWhiteSpace(InboundParseSecret);
}
