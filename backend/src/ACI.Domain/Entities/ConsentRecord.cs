namespace ACI.Domain.Entities;

/// <summary>
/// One thing a person said, or was recorded as saying, about being contacted: yes or no to
/// one channel (email, SMS, post) for one purpose (marketing, or the service messages their
/// membership brings), with where it came from and when.
/// </summary>
/// <remarks>
/// <para>
/// nFADP and GDPR expect a club to be able to show, for every person it sends news and
/// offers to, that they agreed: how, when and to what. A member had no such record at all,
/// and a campaign went to everyone in its segment. Rows are only ever added, so the history
/// of a person's answers is kept as they gave them; <see cref="IsCurrent"/> marks the one
/// that stands for each channel and purpose, and a unique index lets only one stand.
/// </para>
/// <para>
/// Written for any person the workspace holds, not only members (<see cref="SubjectType"/>
/// and <see cref="SubjectId"/>): the public capture form can record a lead's consent the
/// same way, and a person's data export or erasure finds every row by subject or by
/// <see cref="Address"/>.
/// </para>
/// </remarks>
public class ConsentRecord : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary><see cref="ConsentSubjects"/>: the kind of record the person is held as.</summary>
    public string SubjectType { get; set; } = ConsentSubjects.Member;

    /// <summary>The member, contact or lead it is about.</summary>
    public Guid SubjectId { get; set; }

    /// <summary><see cref="ConsentChannels"/>.</summary>
    public string Channel { get; set; } = ConsentChannels.Email;

    /// <summary><see cref="ConsentPurposes"/>.</summary>
    public string Purpose { get; set; } = ConsentPurposes.Marketing;

    /// <summary>Yes (given) or no (refused or withdrawn).</summary>
    public bool Granted { get; set; }

    /// <summary><see cref="ConsentSources"/>: how it was given or withdrawn.</summary>
    public string Source { get; set; } = ConsentSources.Staff;

    /// <summary>
    /// The address it was given for, as it was then: the email address for email, the
    /// phone number for SMS; none for post. Lower-cased for email, so a withdrawal and an
    /// erasure find it whatever case the address was typed in.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// What was agreed and how, in words: the wording a form showed, where the signed paper
    /// form is kept, what the member said at the desk.
    /// </summary>
    public string? Detail { get; set; }

    public DateTime RecordedAtUtc { get; set; }

    /// <summary>The staff member who recorded it; null when the person did it themselves (a link, a form).</summary>
    public Guid? RecordedByUserId { get; set; }

    /// <summary>
    /// The answer that stands for this subject, channel and purpose: the latest. Set false on
    /// the one before when a new answer is recorded; nothing else about a row ever changes.
    /// </summary>
    public bool IsCurrent { get; set; }
}

/// <summary>The kinds of record a <see cref="ConsentRecord"/> can be about.</summary>
public static class ConsentSubjects
{
    public const string Member = "member";
    public const string Contact = "contact";
    public const string Lead = "lead";

    /// <summary>
    /// A membership application: what the applicant said on the public form, before they are
    /// anyone else. Approval copies it onto the member they become.
    /// </summary>
    public const string Application = "application";
}

/// <summary>The ways a workspace can reach a person.</summary>
public static class ConsentChannels
{
    public const string Email = "email";
    public const string Sms = "sms";
    public const string Post = "post";

    public static readonly IReadOnlyList<string> All = [Email, Sms, Post];
}

/// <summary>What a message is for, which decides whether it needs consent.</summary>
public static class ConsentPurposes
{
    /// <summary>News, offers and invitations: sent only to people who agreed to them on that channel.</summary>
    public const string Marketing = "marketing";

    /// <summary>
    /// What a membership brings with it: statements, booking confirmations, receipts. Sent
    /// whatever the person said about marketing; their answer here is kept as said.
    /// </summary>
    public const string Service = "service";

    public static readonly IReadOnlyList<string> All = [Marketing, Service];
}

/// <summary>Where a consent answer came from.</summary>
public static class ConsentSources
{
    /// <summary>A form the person filled in: on paper, or the public capture form.</summary>
    public const string Form = "form";

    /// <summary>Brought in with an import of records the club already held.</summary>
    public const string Import = "import";

    /// <summary>Recorded by staff on their own judgement, a correction say.</summary>
    public const string Staff = "staff";

    /// <summary>The person told the club (at the desk, by phone, by email) and staff recorded it.</summary>
    public const string Member = "member";

    /// <summary>The unsubscribe link in an email, followed by the person it was sent to.</summary>
    public const string Unsubscribe = "unsubscribe";

    public static readonly IReadOnlyList<string> All = [Form, Import, Staff, Member, Unsubscribe];

    /// <summary>What staff may record by hand: the link and the import record themselves.</summary>
    public static readonly IReadOnlyList<string> Recordable = [Form, Staff, Member];
}
