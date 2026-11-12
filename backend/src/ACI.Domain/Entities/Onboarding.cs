namespace ACI.Domain.Entities;

/// <summary>
/// Where an organization hands a new customer over when a lead becomes a deal, and the
/// welcome email that goes with them. One row per organization.
/// </summary>
/// <remarks>
/// The receiving system — a member portal, an onboarding tool — is the organization's
/// own: it gets a signed <c>deal.onboarding</c> event, does its part (opens an account,
/// sends the welcome email with its own links), and reports back on the handoff's
/// callback address. Cadence never sends the welcome email itself.
/// </remarks>
public class OnboardingSettings : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The https address the handoff is posted to. Empty turns handoffs off.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Signs every handoff (X-Cadence-Signature-256), and gives each handoff the token its
    /// progress reports are posted with. Made by Cadence and shown once.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Offered on by default when a lead is converted into a deal.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The welcome email's subject, with placeholders. Empty leaves it to the receiver.</summary>
    public string? EmailSubject { get; set; }

    /// <summary>
    /// The welcome email as HTML, pasted in by the organization, with placeholders.
    /// Empty leaves the email to the receiver's own design, with the welcome message in it.
    /// </summary>
    public string? EmailHtml { get; set; }

    /// <summary>"fr" or "en": the language a handoff starts in.</summary>
    public string DefaultLanguage { get; set; } = "fr";

    /// <summary>Whether a handoff also adds the person to the club's members here.</summary>
    public bool AddToClubMembers { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// One customer handed over when their lead became a deal: what was sent, and each step
/// since, as the receiving system reports it.
/// </summary>
public class DealOnboarding : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }
    public Guid DealId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>The club member added here with the handoff, if one was.</summary>
    public Guid? ClubMemberId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>"fr" or "en".</summary>
    public string Language { get; set; } = "fr";

    /// <summary>The welcome message, as typed, plain text.</summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>When the membership starts; null with <see cref="StartUndetermined"/> when it is not yet known.</summary>
    public DateTime? StartsOnUtc { get; set; }
    public bool StartUndetermined { get; set; }

    /// <summary>The subject as sent, Cadence's placeholders filled.</summary>
    public string? EmailSubject { get; set; }

    /// <summary>The email as sent, Cadence's placeholders filled and the receiver's left for it.</summary>
    public string? EmailHtml { get; set; }

    /// <summary><see cref="OnboardingStatuses"/>.</summary>
    public string Status { get; set; } = OnboardingStatuses.Queued;

    /// <summary>The outbox message carrying the latest attempt to hand it over.</summary>
    public Guid? OutboundMessageId { get; set; }

    /// <summary>The receiver's own id for the account it opened, once it says.</summary>
    public string? ExternalAccountId { get; set; }

    /// <summary>The steps so far, oldest first, as JSON (see OnboardingStep in the application layer).</summary>
    public string StepsJson { get; set; } = "[]";

    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Changes on every save: two reports from the receiver landing together are both
    /// kept, the second read again and applied on top, not one written over the other.
    /// </summary>
    public Guid Version { get; set; }
}

/// <summary>Where a handoff has got to.</summary>
public static class OnboardingStatuses
{
    /// <summary>Waiting to be posted, or being retried.</summary>
    public const string Queued = "queued";
    /// <summary>The receiver took it (answered 2xx) and has not reported yet.</summary>
    public const string Delivered = "delivered";
    /// <summary>The receiver opened or found the person's account.</summary>
    public const string AccountReady = "account_ready";
    /// <summary>The receiver sent the welcome email.</summary>
    public const string Welcomed = "welcomed";
    /// <summary>The person set their password: they are in.</summary>
    public const string Active = "active";
    /// <summary>It could not be handed over, or the receiver reported a failure.</summary>
    public const string Failed = "failed";
}
