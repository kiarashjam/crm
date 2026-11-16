using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// A workspace's privacy settings: who answers for its data, whether its public forms ask for
/// marketing consent, and how long it keeps personal data (see <see cref="Domain.Entities.PrivacySettings"/>).
/// </summary>
public interface IPrivacySettings
{
    /// <summary>The settings as they stand; a workspace that saved none gets the defaults (nothing kept shorter than before).</summary>
    Task<PrivacySettingsDto> GetAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Saves the settings; refused with a 409 when <paramref name="expectedVersion"/> is not the version saved.</summary>
    Task<Result<PrivacySettingsDto>> SaveAsync(Guid organizationId, Guid userId, PrivacySettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>What the workspace's public forms show about marketing consent.</summary>
    Task<FormConsentDto> FormConsentAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// What the privacy page says: the operator of this Cadence, and, given the token of one of
    /// a workspace's public forms, that workspace as the controller of its data, its processors
    /// and its retention periods. Answers anyone: it holds nothing that is not on the page.
    /// </summary>
    Task<PrivacyNoticeDto> NoticeAsync(string? formToken, CancellationToken ct = default);
}

/// <summary>The words beside the marketing checkbox when a workspace has not written its own.</summary>
public static class FormConsentDefaults
{
    public const string Text = "Yes, I would like to hear about news, events and offers by email. I can change my mind at any time.";
}

/// <summary>A workspace's privacy settings, as the settings page shows them.</summary>
/// <param name="MarketingConsentText">The words the forms show: the workspace's own, or the standard ones.</param>
/// <param name="MarketingConsentTextIsDefault">Whether the workspace has written none of its own.</param>
public sealed record PrivacySettingsDto(
    string? ControllerName,
    string? ControllerAddress,
    string? ControllerEmail,
    string? DpoContact,
    string MarketingConsent,
    string MarketingConsentText,
    bool MarketingConsentTextIsDefault,
    int? LeadRetentionMonths,
    int? MemberRetentionYears,
    int? MailRetentionMonths,
    int? DetailRetentionMonths,
    Guid? Version,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>
/// A change to the privacy settings. Every field is sent, as the page shows them: an empty
/// text or a missing period clears it (a period cleared keeps the data until someone deletes it).
/// </summary>
public sealed class PrivacySettingsRequest
{
    public string? ControllerName { get; set; }
    public string? ControllerAddress { get; set; }
    public string? ControllerEmail { get; set; }
    public string? DpoContact { get; set; }
    /// <summary>off, optional or required.</summary>
    public string? MarketingConsent { get; set; }
    /// <summary>Empty for the standard wording.</summary>
    public string? MarketingConsentText { get; set; }
    public int? LeadRetentionMonths { get; set; }
    public int? MemberRetentionYears { get; set; }
    public int? MailRetentionMonths { get; set; }
    public int? DetailRetentionMonths { get; set; }
}

/// <summary>What a public form shows about marketing consent.</summary>
/// <param name="Mode">off, optional or required.</param>
public sealed record FormConsentDto(string Mode, string Text);

/// <summary>Who answers for the data, as the privacy page names them.</summary>
/// <param name="Name">Null while nobody has entered it: the page says so rather than inventing one.</param>
public sealed record PrivacyPartyDto(string? Name, string? Address, string? Email, string? DpoContact);

/// <summary>A service the data goes to, as the privacy page lists it.</summary>
/// <param name="Location">Where it processes the data.</param>
/// <param name="Transfer">How a transfer outside Switzerland and the EEA is covered, when there is one.</param>
public sealed record PrivacyProcessorDto(string Name, string Purpose, string Location, string? Transfer);

/// <summary>The retention periods in force: null keeps that data until someone deletes it.</summary>
public sealed record PrivacyRetentionDto(int? LeadMonths, int? MemberYears, int? MailMonths, int? DetailMonths);

/// <summary>Everything the public privacy page shows, as the server knows it.</summary>
/// <param name="Operator">Who runs this Cadence and answers for the staff's accounts.</param>
/// <param name="Controller">Who answers for the people in a workspace: the workspace named by the form, or with none the operator.</param>
/// <param name="WorkspaceName">The workspace a form belongs to; null without one.</param>
/// <param name="Hosting">Where the database and the app run.</param>
/// <param name="Retention">The workspace's retention periods; null without a workspace.</param>
/// <param name="MarketingConsentText">The words the workspace's forms show beside the marketing checkbox; null without a workspace.</param>
/// <param name="SessionHours">How long a sign-in lasts.</param>
public sealed record PrivacyNoticeDto(
    PrivacyPartyDto Operator,
    PrivacyPartyDto Controller,
    string? WorkspaceName,
    string Hosting,
    IReadOnlyList<PrivacyProcessorDto> Processors,
    PrivacyRetentionDto? Retention,
    string? MarketingConsentText,
    int SessionHours);

/// <summary>
/// Removes, once a day, what a workspace's retention settings say it no longer keeps: leads
/// that went nowhere, members long gone, the text of old emails, and old details around
/// requests and the audit trail. Off for every workspace until it sets a period.
/// </summary>
public interface IRetention
{
    /// <summary>What a run now would remove, counted without removing anything.</summary>
    Task<RetentionPreviewDto> PreviewAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Runs the workspace's retention now, as the daily run would.</summary>
    Task<Result<RetentionRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>The workspace's runs, newest first: what each removed.</summary>
    Task<IReadOnlyList<RetentionRunDto>> RunsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The daily run for every workspace that set a period, once per workspace and local day,
    /// after 04:00 its time. Returns how many workspaces it ran.
    /// </summary>
    Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default);
}

/// <summary>One rule of a workspace's retention, with what it would remove now.</summary>
/// <param name="Rule">leads, members, mail or details.</param>
/// <param name="Period">The period set; null when the rule is off.</param>
/// <param name="Unit">months or years.</param>
/// <param name="Count">How many rows it would remove or anonymise now.</param>
/// <param name="What">What the rule does, in words.</param>
public sealed record RetentionRuleDto(string Rule, int? Period, string Unit, int Count, string What);

/// <summary>What a retention run now would do.</summary>
/// <param name="Kept">Rows a rule leaves alone although their period is over, and why (a member who still owes money, say).</param>
public sealed record RetentionPreviewDto(bool AnythingOn, IReadOnlyList<RetentionRuleDto> Rules, IReadOnlyList<string> Kept);

/// <summary>One retention run: what it removed.</summary>
public sealed record RetentionRunDto(
    Guid Id, string Trigger, DateOnly? ScheduledFor, string Status, DateTime StartedAtUtc, DateTime? FinishedAtUtc,
    string? StartedByName, int LeadsDeleted, int MembersAnonymised, int MessagesPurged, int DetailsPurged, string? Failures);
