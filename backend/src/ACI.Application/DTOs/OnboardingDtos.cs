namespace ACI.Application.DTOs;

/// <summary>
/// Where the workspace hands new customers over. The secret is never read back: only
/// whether there is one, and its last four characters to tell secrets apart.
/// </summary>
public sealed record OnboardingSettingsDto(
    string? WebhookUrl,
    bool HasSecret,
    string? SecretHint,
    bool Enabled,
    string? EmailSubject,
    string? EmailHtml,
    string DefaultLanguage,
    bool AddToClubMembers,
    bool Configured,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<string> CadenceFields,
    IReadOnlyList<string> ReceiverFields);

public sealed class SaveOnboardingSettingsRequest
{
    public string? WebhookUrl { get; set; }
    public bool? Enabled { get; set; }
    public string? EmailSubject { get; set; }
    public string? EmailHtml { get; set; }
    public string? DefaultLanguage { get; set; }
    public bool? AddToClubMembers { get; set; }
}

/// <summary>A new signing secret, shown this once.</summary>
public sealed record OnboardingSecretDto(string Secret);

/// <summary>What the receiver answered to a signed test post.</summary>
public sealed record OnboardingPingDto(bool Ok, int? StatusCode, string? Error);

/// <summary>
/// The welcome email for someone, before it goes. With a deal, the person is the deal's
/// contact; without one (a lead about to be converted), the name, email and company sent.
/// A subject or HTML left out uses the workspace's template.
/// </summary>
public sealed class OnboardingPreviewRequest
{
    public Guid? DealId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string? DealName { get; set; }
    public string? WelcomeMessage { get; set; }
    public DateTime? StartsOnUtc { get; set; }
    public bool StartUndetermined { get; set; }
    public string? Language { get; set; }
    public string? EmailSubject { get; set; }
    public string? EmailHtml { get; set; }
}

/// <param name="UsesReceiverDesign">No HTML of the workspace's own: the receiver sends its own design, with the welcome message in it, and Html is a stand-in for it.</param>
/// <param name="Unknown">Placeholders nobody fills, as written.</param>
public sealed record OnboardingPreviewDto(string Subject, string Html, bool UsesReceiverDesign, IReadOnlyList<string> Unknown);

/// <summary>Hand a deal's contact over to the workspace's portal.</summary>
public sealed class StartOnboardingRequest
{
    /// <summary>The lead the deal came from, for the record.</summary>
    public Guid? LeadId { get; set; }
    public string? WelcomeMessage { get; set; }
    /// <summary>When the membership starts. Left out, the start is still to be decided.</summary>
    public DateTime? StartsOnUtc { get; set; }
    public bool StartUndetermined { get; set; }
    public string? Language { get; set; }
    /// <summary>This email's own subject and HTML; left out, the workspace's template.</summary>
    public string? EmailSubject { get; set; }
    public string? EmailHtml { get; set; }
    /// <summary>Also add them to the club's members here; left out, the workspace's setting.</summary>
    public bool? AddToClubMembers { get; set; }
}

public sealed record OnboardingStepDto(string Key, DateTime AtUtc, string? Detail);

/// <summary>The outbox's attempts at handing it over.</summary>
public sealed record OnboardingDeliveryDto(
    string Status, int Attempts, int? LastStatusCode, string? LastError, DateTime? SentAtUtc, DateTime? NextAttemptAtUtc);

public sealed record OnboardingDto(
    Guid Id,
    Guid DealId,
    Guid? LeadId,
    Guid? ContactId,
    Guid? ClubMemberId,
    string Name,
    string Email,
    string Language,
    string? WelcomeMessage,
    DateTime? StartsOnUtc,
    bool StartUndetermined,
    string Status,
    IReadOnlyList<OnboardingStepDto> Steps,
    OnboardingDeliveryDto? Delivery,
    string? ExternalAccountId,
    bool CanResend,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// The receiving system's report on one handoff, posted to the callback address it was
/// given with <c>Authorization: Bearer &lt;callback.token&gt;</c>.
/// </summary>
public sealed class OnboardingReportRequest
{
    /// <summary>account_created, account_existing, email_sent, email_failed, password_set or failed.</summary>
    public string? Event { get; set; }
    /// <summary>The receiver's own id for the person's account.</summary>
    public string? AccountId { get; set; }
    /// <summary>A short note for the team: why it failed, say. Never a secret.</summary>
    public string? Message { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
}

public sealed record OnboardingReportReceipt(Guid Id, string Status);
