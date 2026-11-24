using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Invitations to a club event: staff choose an audience of members (<see cref="IMemberAudience"/>,
/// as campaigns do), write the letter in English, German and French, preview it as one member,
/// test it on themselves and send it; each member gets a reply link of their own and answers on
/// a public page, "I'll come" or "I can't come". Modelled on "Invite to a meeting" for leads
/// (LeadNewsletterService).
/// </summary>
/// <remarks>
/// <para>
/// An answer of "I'll come" registers the member through the event's own rules (IClubEvents:
/// a seat while there is one, else the waitlist, the ticket on their account); "I can't come"
/// after that cancels the registration the same way staff do, which gives the seat to the
/// first in line. A member may change their answer until the event starts.
/// </para>
/// <para>
/// Invitations are marketing: they reach only members who agreed to it by email, and carry the
/// link that withdraws that agreement. The outbox checks consent and the suppression list again
/// as it queues each email and as it sends it.
/// </para>
/// </remarks>
public interface IEventInvitations
{
    /// <summary>The default letters, the placeholders, who it goes out as, and whether the event can still be invited to.</summary>
    Task<Result<InvitationSetupDto>> SetupAsync(Guid organizationId, Guid userId, Guid eventId, CancellationToken ct = default);

    /// <summary>Who invitations with this audience would reach now, and who is left out and why: the counts the send keeps to.</summary>
    Task<Result<MemberAudienceDto>> AudienceAsync(Guid organizationId, Guid userId, Guid eventId, MemberAudienceFilter? audience, CancellationToken ct = default);

    /// <summary>The invitation as one member (or a sample person) will read it, in their language. Its reply link is a sample.</summary>
    Task<Result<InvitationPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, Guid eventId, InvitationPreviewRequest request, CancellationToken ct = default);

    /// <summary>The invitation as the preview shows it, marked as a test, to the person asking and nobody else.</summary>
    Task<Result<InvitationTestDto>> SendTestAsync(Guid organizationId, Guid userId, Guid eventId, InvitationPreviewRequest request, CancellationToken ct = default);

    /// <summary>
    /// Queues one invitation per member the audience chooses, each with their own reply link, spread
    /// over the minutes after the send. The same send id sent again sends nothing more.
    /// </summary>
    Task<Result<InvitationSendDto>> SendAsync(Guid organizationId, Guid userId, Guid eventId, InvitationSendRequest request, CancellationToken ct = default);

    /// <summary>Everyone invited to the event, with where each stands, and the counts.</summary>
    Task<Result<EventInvitationsDto>> ListAsync(Guid organizationId, Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Reminds those invited who have not answered: at most one reminder per member and event,
    /// whoever presses it and however often, through the outbox.
    /// </summary>
    Task<Result<InvitationRemindDto>> RemindAsync(Guid organizationId, Guid userId, Guid eventId, CancellationToken ct = default);

    /// <summary>How many of those registered are likely to come, from each member's own past show rate. An estimate.</summary>
    Task<Result<ExpectedAttendanceDto>> ExpectedAttendanceAsync(Guid organizationId, Guid eventId, CancellationToken ct = default);

    /// <summary>The public reply page: the club, the event and the member's answer so far, for the token in their link.</summary>
    Task<Result<InvitationPageDto>> PageAsync(string token, CancellationToken ct = default);

    /// <summary>The member's answer from their reply page: "coming" (registers them) or "declined" (cancels a registration they had).</summary>
    Task<Result<InvitationPageDto>> AnswerAsync(string token, string? answer, CancellationToken ct = default);
}

/// <summary>One language's letter: its subject and words, with placeholders such as {{firstName}}.</summary>
public sealed record InvitationLetterDto(string? Subject, string? Body);

/// <summary>The letter in each language. A member gets the one in their own language (English when none is set).</summary>
public sealed class InvitationLetters
{
    public InvitationLetterDto? En { get; set; }
    public InvitationLetterDto? De { get; set; }
    public InvitationLetterDto? Fr { get; set; }
}

/// <param name="Defaults">The default letter in each language, by language code.</param>
/// <param name="Fields">The placeholders the letter may use.</param>
/// <param name="ReplyLinkProblem">Why reply links cannot be made here (the app's public address is not set), when they cannot.</param>
/// <param name="Closed">Why the event cannot be invited to (cancelled, over, started), when it cannot.</param>
public sealed record InvitationSetupDto(
    IReadOnlyDictionary<string, InvitationLetterDto> Defaults, IReadOnlyList<string> Fields, string? FromName, string? ReplyTo,
    string TimeZone, string? ReplyLinkProblem, string? Closed, int MaxRecipients);

/// <summary>The letters, and who the preview or test is filled in for.</summary>
public class InvitationPreviewRequest
{
    public InvitationLetters? Letters { get; set; }

    /// <summary>The member it is filled in for; none: a sample person.</summary>
    public Guid? SampleMemberId { get; set; }

    /// <summary>For a sample person: the language to show (en, de, fr). A member's own decides otherwise.</summary>
    public string? Language { get; set; }
}

/// <summary>The invitation as one member will read it.</summary>
/// <param name="Unknown">Placeholders nothing fills, as written.</param>
public sealed record InvitationPreviewDto(
    string Language, string Subject, string Body, string SampleName, string? SampleEmail, IReadOnlyList<string> Unknown,
    string? FromName, string? ReplyTo);

/// <param name="Status">"sent", or "queued" when the mail server could not take it yet and it is tried again.</param>
public sealed record InvitationTestDto(string To, string Status, string Language);

/// <summary>The invitations to send: the audience, the letters, and the id the browser made when Send was pressed.</summary>
public sealed class InvitationSendRequest
{
    public Guid SendId { get; set; }
    public MemberAudienceFilter? Audience { get; set; }
    public InvitationLetters? Letters { get; set; }

    /// <summary>How many the person confirmed it would go to: when the audience has changed since, nothing is sent.</summary>
    public int? ExpectedRecipients { get; set; }
}

/// <param name="AlreadySent">The id had been sent before: nothing more went.</param>
public sealed record InvitationSendDto(Guid Id, string Status, int Recipients, int LeftOut, bool AlreadySent, DateTime CreatedAtUtc);

/// <summary>Where an invited member stands.</summary>
public static class InvitationStates
{
    /// <summary>Invited, and no answer yet.</summary>
    public const string NoAnswer = "no_answer";
    public const string Coming = "coming";
    public const string Waitlist = "waitlist";
    public const string Declined = "declined";

    /// <summary>Answered "I'll come", and the registration was cancelled since by the club (or the event was).</summary>
    public const string Cancelled = "cancelled";

    /// <summary>After the event: checked in at the door.</summary>
    public const string Came = "came";

    /// <summary>After the event: had a seat and did not come.</summary>
    public const string NoShow = "no_show";
}

/// <summary>Someone invited to an event.</summary>
/// <param name="State">One of <see cref="InvitationStates"/>.</param>
/// <param name="EmailStatus">What the outbox says about the invitation email: queued, sent, delivered, opened, bounced, suppressed, failed, cancelled.</param>
public sealed record EventInvitationDto(
    Guid Id, Guid MemberId, string MemberName, string? Email, string Language, string State, DateTime InvitedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? AnsweredAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? RemindedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmailStatus,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? WaitlistPosition);

/// <param name="Invited">Everyone invited.</param>
/// <param name="CanRemind">Of those who have not answered, the ones a reminder would still go to (none went yet).</param>
public sealed record EventInvitationCountsDto(
    int Invited, int Coming, int Waitlist, int Declined, int NoAnswer, int Cancelled, int Came, int NoShow, int Reminded, int CanRemind);

/// <param name="Open">Whether members can still answer: the event is on and has not started.</param>
public sealed record EventInvitationsDto(IReadOnlyList<EventInvitationDto> Invitations, EventInvitationCountsDto Counts, bool Open);

/// <param name="Reminded">Reminders queued now.</param>
/// <param name="AlreadyReminded">Members who had their one reminder already.</param>
/// <param name="NotSent">Reminders the outbox refused (consent withdrawn, an address that bounced since).</param>
public sealed record InvitationRemindDto(int Reminded, int AlreadyReminded, int NotSent);

/// <summary>
/// How many of those with a seat are likely to come: each member at their own past show rate at
/// the club's events (registered and checked in, against registered and no-show), a member with
/// no history and a guest at the club's average, or 90% while the club has no history. An estimate.
/// </summary>
/// <param name="Expected">The estimate, to one decimal: those already checked in count in full.</param>
/// <param name="Seats">Seats taken now: members, guests and unnamed seats.</param>
/// <param name="MembersWithHistory">Members with a seat whose own show rate was used.</param>
/// <param name="ClubRate">The club's average show rate (0 to 1), or 0.9 while it has no history.</param>
/// <param name="ClubHasHistory">Whether the club's rate comes from its events, rather than the 90% default.</param>
public sealed record ExpectedAttendanceDto(
    decimal Expected, int Seats, int Members, int MembersWithHistory, int Guests, int CheckedIn, decimal ClubRate, bool ClubHasHistory);

/// <summary>What the public reply page shows: nothing about anyone else.</summary>
/// <param name="Language">The member's language (en, de, fr), which the page opens in.</param>
/// <param name="Locale">The club's region, for its number and date formats.</param>
/// <param name="TimeZone">The club's time zone, which the event's time is written in.</param>
/// <param name="Answer">none, coming or declined.</param>
/// <param name="Place">With "coming": seat or waitlist.</param>
public sealed record InvitationPageDto(
    string Club, string FirstName, string Language, string? Locale, string TimeZone,
    string EventName, string? Description, string? Location, DateTime StartAtUtc, DateTime EndAtUtc, string CoverEmoji,
    decimal MemberPrice, string Currency, string Answer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Place,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? WaitlistPosition);

/// <summary>The member's answer on their reply page.</summary>
public sealed class InvitationAnswerRequest
{
    /// <summary>"coming" or "declined".</summary>
    public string? Answer { get; set; }
}
