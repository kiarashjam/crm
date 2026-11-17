using System.ComponentModel.DataAnnotations;
namespace ACI.Application.DTOs;

/// <summary>
/// Which leads a newsletter goes to. Empty lists mean every one; a lead always needs an
/// email address. Statuses and sources are matched by name, ignoring case.
/// </summary>
public sealed class NewsletterAudienceRequest
{
    /// <summary>Only leads in these statuses; none: every status.</summary>
    public List<string>? IncludeStatuses { get; set; }

    /// <summary>Never leads in these statuses ("Not interested", "Lost", "Converted").</summary>
    public List<string>? ExcludeStatuses { get; set; }

    /// <summary>Only leads from these sources; none: every source.</summary>
    public List<string>? Sources { get; set; }

    /// <summary>Only leads assigned to these people; none (and not <see cref="Unassigned"/>): anyone's.</summary>
    public List<Guid>? OwnerIds { get; set; }

    /// <summary>Leads assigned to nobody, besides any in <see cref="OwnerIds"/>.</summary>
    public bool Unassigned { get; set; }

    /// <summary>Created on or after this day (UTC).</summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>Created on or before this day (UTC).</summary>
    public DateOnly? CreatedTo { get; set; }

    /// <summary>Leads already converted into a contact or deal too. Off: they are left out.</summary>
    public bool IncludeConverted { get; set; }
}

/// <summary>A lead in the audience, or left out of it with why.</summary>
/// <param name="Reason">For one left out: "no_email", "do_not_contact", "suppressed", "already_booked" or "duplicate".</param>
public sealed record NewsletterPersonDto(Guid LeadId, string Name, string? Email, string Status, string? Reason = null);

/// <summary>
/// Who a newsletter with these filters would reach now, counted by the server: the leads the
/// filters choose, how many get it, and how many are left out and why.
/// </summary>
/// <param name="Matching">Leads the filters choose.</param>
/// <param name="WillReceive">Emails that would go: one per lead, one per address.</param>
/// <param name="NoEmail">No usable email address.</param>
/// <param name="DoNotContact">Marked do not contact (an unsubscribe marks them so).</param>
/// <param name="Suppressed">Their address unsubscribed through the mail service, bounced or complained.</param>
/// <param name="Duplicates">Another chosen lead has the same address and gets the one email.</param>
/// <param name="AlreadyBooked">A meeting already booked and still to come.</param>
/// <param name="Recipients">The first of those who get it, for the list's preview.</param>
/// <param name="LeftOut">The first of those left out, with why.</param>
/// <param name="MaxRecipients">The most one newsletter goes to.</param>
public sealed record NewsletterAudienceDto(
    int Matching, int WillReceive, int NoEmail, int DoNotContact, int Suppressed, int Duplicates,
    IReadOnlyList<NewsletterPersonDto> Recipients, IReadOnlyList<NewsletterPersonDto> LeftOut, int MaxRecipients, int AlreadyBooked = 0);

/// <summary>What a newsletter can do in this workspace: whether it can give each lead a booking link, and if not, why.</summary>
public sealed record NewsletterSetupDto(
    bool BookingLinkAvailable, string? BookingProblem, string? BookingPageUrl, IReadOnlyList<string> Fields,
    string? Workspace, string DefaultLanguage, string? FromName, string? ReplyTo, int MaxRecipients);

/// <summary>The email: its language, subject and HTML, with placeholders.</summary>
public class NewsletterEmailRequest
{
    public string? Language { get; set; }
    public string? Subject { get; set; }
    public string? Html { get; set; }

    /// <summary>The lead the preview or test is filled in for; none: a sample person.</summary>
    public Guid? SampleLeadId { get; set; }
}

/// <summary>The email, and who its test goes to.</summary>
public sealed class NewsletterTestRequest : NewsletterEmailRequest
{
    /// <summary>Where the test goes: anyone (a colleague, a second inbox); none: the sender's own address.</summary>
    [StringLength(254)] public string? To { get; set; }
}

/// <summary>The email as one lead will read it.</summary>
/// <param name="Text">The plain-text part that goes with it.</param>
/// <param name="Unknown">Placeholders nothing fills, as written.</param>
/// <param name="BookingProblem">Why {{bookingUrl}} cannot be filled, when the email uses it and it cannot.</param>
public sealed record NewsletterPreviewDto(
    string Subject, string Html, string Text, string SampleName, string? SampleEmail, string BookingCode,
    IReadOnlyList<string> Unknown, string? BookingProblem, string? FromName, string? ReplyTo);

/// <summary>
/// The newsletter to send: the email, the filters, and the id the browser made when Send was
/// pressed. The same id sent again sends nothing more.
/// </summary>
public sealed class NewsletterSendRequest : NewsletterEmailRequest
{
    public Guid SendId { get; set; }
    public NewsletterAudienceRequest? Audience { get; set; }

    /// <summary>How many the person confirmed it would go to: when the audience has changed since, nothing is sent.</summary>
    public int? ExpectedRecipients { get; set; }
}

/// <param name="Status">"sending" or "sent".</param>
/// <param name="AlreadySent">The id had been sent before: nothing more went.</param>
public sealed record NewsletterSendDto(Guid Id, string Status, int Recipients, int LeftOut, bool AlreadySent, DateTime CreatedAtUtc);

/// <param name="Status">"sent", or "queued" when the mail server could not take it yet and it is tried again.</param>
public sealed record NewsletterTestDto(string To, string Status);

/// <summary>
/// A meeting booked, or cancelled, on the workspace's website, as the website posts it to
/// /api/public/meetings, signed with the workspace's onboarding secret.
/// </summary>
public sealed class MeetingReportRequest
{
    public Guid? OrganizationId { get; set; }
    public Guid? LeadId { get; set; }
    public string? Code { get; set; }
    public string? BookingId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Message { get; set; }

    /// <summary>ISO 8601 with its offset: "2026-12-03T14:30:00+01:00".</summary>
    public DateTimeOffset? StartsAt { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Language { get; set; }

    /// <summary>"booked" or "cancelled".</summary>
    public string? Status { get; set; }
}

/// <summary>What the CRM did with a report.</summary>
/// <param name="Result">"recorded", "cancelled", "unchanged" (a repeat), or "test" (a test email's invitation, recorded nowhere).</param>
/// <param name="LeadCreated">No lead had the id or the email, so one was made.</param>
public sealed record MeetingReportReceipt(string Result, Guid? LeadId, Guid? ActivityId, bool LeadCreated = false);
