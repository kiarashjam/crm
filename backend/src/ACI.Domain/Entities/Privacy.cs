namespace ACI.Domain.Entities;

/// <summary>
/// A workspace's privacy settings: who answers for the personal data it holds (the
/// controller, as its privacy notice names them), whether its public forms ask for
/// marketing consent, and how long it keeps what it no longer needs.
/// </summary>
/// <remarks>
/// <para>
/// The privacy notice named a placeholder company in San Francisco under California law,
/// and nothing was ever deleted on a schedule: leads that went nowhere, members who left
/// years ago and the text of every email sent stayed for ever. nFADP and GDPR expect a
/// club to say who it is, to keep personal data no longer than it needs, and to say for
/// how long. Everything here starts empty or off; a workspace that saves nothing keeps
/// behaving as it did.
/// </para>
/// <para>
/// One row per workspace. A retention period that is null keeps that data until someone
/// deletes it, as before.
/// </para>
/// </remarks>
public class PrivacySettings : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }

    // ── Who answers for the data ─────────────────────────────────────────────
    /// <summary>The club or company that decides what is done with the data: its legal name.</summary>
    public string? ControllerName { get; set; }
    /// <summary>Its postal address, as the privacy notice prints it.</summary>
    public string? ControllerAddress { get; set; }
    /// <summary>Where people send a request to see, correct or erase their data.</summary>
    public string? ControllerEmail { get; set; }
    /// <summary>A data protection officer or advisor, if the club has one: a name and how to reach them.</summary>
    public string? DpoContact { get; set; }

    // ── Public forms ─────────────────────────────────────────────────────────
    /// <summary>
    /// <see cref="MarketingConsentModes"/>: whether the public capture and application forms
    /// show the marketing checkbox, and whether it must be ticked to send the form.
    /// </summary>
    public string MarketingConsent { get; set; } = MarketingConsentModes.Optional;
    /// <summary>The words beside the checkbox on the capture form; null for the standard wording.</summary>
    public string? MarketingConsentText { get; set; }

    // ── Retention: null keeps the data until someone deletes it ──────────────
    /// <summary>Months a lead that never became a customer is kept after anything last happened on it.</summary>
    public int? LeadRetentionMonths { get; set; }
    /// <summary>
    /// Years a member who resigned, lapsed or was cancelled is kept after anything last
    /// happened on their account; then they are anonymised, keeping what the books need.
    /// </summary>
    public int? MemberRetentionYears { get; set; }
    /// <summary>Months the text of sent emails, their attachments and generated copy are kept.</summary>
    public int? MailRetentionMonths { get; set; }
    /// <summary>
    /// Months the details around requests and the audit trail are kept: the names on audit
    /// lines, the data posted to the workspace's webhook, the raw tickets from the POS, and
    /// notifications.
    /// </summary>
    public int? DetailRetentionMonths { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>Whether a public form asks for marketing consent.</summary>
public static class MarketingConsentModes
{
    /// <summary>No checkbox: the form asks nothing, and nobody it brings in is sent marketing until they agree some other way.</summary>
    public const string Off = "off";
    /// <summary>A checkbox the visitor may tick or leave; never ticked in advance. Their answer is recorded either way.</summary>
    public const string Optional = "optional";
    /// <summary>
    /// The form is not sent until the box is ticked: for a form whose whole point is to sign
    /// up for news. An enquiry or an application must not depend on marketing consent
    /// (GDPR art. 7(4)), so the settings page says so.
    /// </summary>
    public const string Required = "required";

    public static readonly IReadOnlyList<string> All = [Off, Optional, Required];

    public static bool IsMode(string? value) => value != null && All.Contains(value);
}

/// <summary>
/// One erasure: a person erased on request, or a member anonymised by the retention job.
/// It says when, who did it and how much went, and names nobody.
/// </summary>
/// <remarks>
/// Its number is the pseudonym that replaces the person where the books keep them: a
/// charge stays a charge of "Erased member 12", with its amount, date and VAT, so the
/// accounts still add up for the ten years Swiss law keeps them (OR art. 958f).
/// </remarks>
public class PersonErasure : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    /// <summary>Counts up per workspace, from 1: the "12" in "Erased member 12".</summary>
    public int Number { get; set; }
    /// <summary><see cref="PersonErasureTriggers"/>.</summary>
    public string Trigger { get; set; } = PersonErasureTriggers.Request;
    public DateTime ErasedAtUtc { get; set; }
    /// <summary>The staff member who confirmed it; null for the retention job.</summary>
    public Guid? ErasedByUserId { get; set; }
    public string? ErasedByName { get; set; }
    /// <summary>
    /// Staff's own reference for the request (a ticket number, the date of the letter),
    /// never the person's name: the page says so, and an address typed here is refused.
    /// </summary>
    public string? Reference { get; set; }
    /// <summary>What went, as counts per kind of record (JSON); no names, addresses or ids of the person.</summary>
    public string SummaryJson { get; set; } = "{}";
}

/// <summary>Why a person was erased.</summary>
public static class PersonErasureTriggers
{
    /// <summary>The person asked, and staff confirmed the erasure.</summary>
    public const string Request = "request";
    /// <summary>The workspace's retention settings: a member gone longer than it keeps them.</summary>
    public const string Retention = "retention";
}

/// <summary>One run of a workspace's retention settings: what it removed, or would have.</summary>
/// <remarks>
/// The daily run is claimed with a row the database takes once per workspace and day, as
/// the billing run is, so two servers, or a run repeated after a crash, never remove
/// anything twice; what one run removes is gone for the next, so repeating one does no harm.
/// </remarks>
public class RetentionRun : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    /// <summary><see cref="RetentionRunTriggers"/>.</summary>
    public string Trigger { get; set; } = RetentionRunTriggers.Schedule;
    /// <summary>The workspace's day a scheduled run is for; unique per workspace. Null for a run started by hand.</summary>
    public DateOnly? ScheduledFor { get; set; }
    /// <summary><see cref="RetentionRunStatuses"/>.</summary>
    public string Status { get; set; } = RetentionRunStatuses.Running;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedByName { get; set; }
    public int LeadsDeleted { get; set; }
    public int MembersAnonymised { get; set; }
    /// <summary>Sent emails whose text and attachments were removed, and generated copy deleted.</summary>
    public int MessagesPurged { get; set; }
    /// <summary>Audit lines, webhook payloads, POS tickets and notifications whose details were removed.</summary>
    public int DetailsPurged { get; set; }
    /// <summary>What went wrong, one line per rule that failed; the other rules still ran.</summary>
    public string? Failures { get; set; }
}

public static class RetentionRunTriggers
{
    public const string Schedule = "schedule";
    public const string Manual = "manual";
}

public static class RetentionRunStatuses
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    /// <summary>Some rules ran and at least one failed; what ran stays done.</summary>
    public const string PartlyFailed = "partly_failed";
    public const string Failed = "failed";
}
