using System.Text.RegularExpressions;
using ACI.Application.Common.I18n;

namespace ACI.Application.Common;

/// <summary>A club's email to a member, ready for the outbox: its subject and its design.</summary>
public sealed record ClubEmail(string Subject, EmailDesign Design)
{
    /// <summary>The plain-text part as the design draws it, before the outbox adds a footer.</summary>
    public string Text => EmailLayout.Text(Design);

    /// <summary>The HTML part as the design draws it, before the outbox adds a footer.</summary>
    public string Html => EmailLayout.Html(Design, Subject);
}

/// <summary>A member's statement by email. Amounts and dates come written as the member reads them.</summary>
/// <param name="FirstName">Who it greets.</param>
/// <param name="Owed">What the account owed at the period's end: positive while owing, negative in credit.</param>
/// <param name="Closing">That amount, written without a sign.</param>
/// <param name="DueDate">When what is owed falls due (the earliest charge still open), when something is owed.</param>
/// <param name="QrBill">Whether the attached statement ends with a QR-bill to pay it with.</param>
/// <param name="PayUrl">A link to pay online, when the club takes payments online.</param>
/// <param name="PayUntil">Until when <paramref name="PayUrl"/> works.</param>
/// <param name="TestNote">For a test copy staff send themselves: what it says to them, at the top.</param>
public sealed record StatementDetails(
    string Club, string Language, string FirstName, string Period,
    string BroughtForward, string Charges, string Payments, decimal Owed, string Closing,
    string? DueDate = null, bool QrBill = false, string? PayUrl = null, string? PayUntil = null, string? TestNote = null);

/// <summary>A charge a payment reminder names, written as the member reads it.</summary>
public sealed record ReminderCharge(string Description, string Amount, string DueDate);

/// <summary>A reminder of charges a member has not paid by their due date.</summary>
/// <param name="Step">Which of the club's reminders this is, from 1.</param>
/// <param name="Steps">How many reminders the club sends.</param>
/// <param name="Total">What they come to, when they are in one currency.</param>
/// <param name="DaysOverdue">How long the oldest has been overdue.</param>
/// <param name="LapseAfterDays">When the club suspends a membership for an unpaid charge: days after its due date.</param>
public sealed record ReminderDetails(
    string Club, string Language, string FirstName, int Step, int Steps, IReadOnlyList<ReminderCharge> Charges,
    string? Total, int DaysOverdue, int? LapseAfterDays = null, string? PayUrl = null, string? PayUntil = null);

/// <summary>A link staff send a member to pay their account online, or to save a card.</summary>
/// <param name="Methods">How Stripe's page lets them pay ("card or TWINT"), for a payment.</param>
public sealed record PayLinkDetails(
    string Club, string Language, string FirstName, bool Setup, bool AutoPay, string Amount, string Methods, string Url, string Until);

/// <summary>What an event's list tells someone on it: a seat came free for them, or the event is cancelled.</summary>
/// <param name="FirstName">Who it greets, when their name is known.</param>
/// <param name="Ticket">The ticket's price, when one was charged.</param>
/// <param name="TicketOnHost">The ticket is on their host's account (a guest's), not their own.</param>
/// <param name="TicketTakenOff">The cancelled event's ticket has been taken off the account it was on.</param>
/// <param name="Member">A member of the club; otherwise a guest on the list.</param>
public sealed record EventNoticeDetails(
    string Club, string Language, string? FirstName, string Event, string When, string? Where,
    string? Ticket = null, bool TicketOnHost = false, bool TicketTakenOff = false, bool Member = true);

/// <summary>The welcome to a member whose application was approved.</summary>
public sealed record WelcomeDetails(string Club, string Language, string FirstName, string Tier);

/// <summary>A link into the member portal for someone, by name when one address serves several members.</summary>
public sealed record PortalLink(string Name, string Url);

/// <summary>The member portal's sign-in links, or the invitation staff sent to open it.</summary>
/// <param name="Greeting">Who it greets: a first name, or the first names of everyone at the address.</param>
/// <param name="Invite">An invitation staff sent; otherwise a sign-in link the member asked for.</param>
/// <param name="ValidUntil">For an invitation: the day its links stop working.</param>
/// <param name="Minutes">For a sign-in link: how many minutes it works.</param>
/// <param name="PortalUrl">The portal's own page, where a new link is asked for.</param>
public sealed record PortalLinkDetails(
    string Club, string Language, string Greeting, bool Invite, IReadOnlyList<PortalLink> Links, string? ValidUntil, int Minutes, string PortalUrl);

/// <summary>An event's invitation or its one reminder, from the letter staff wrote, filled for the member.</summary>
/// <param name="Letter">The letter with its placeholders filled, the member's reply link in it.</param>
/// <param name="TestNote">For a test copy, what it says to the person who sent it to themselves.</param>
public sealed record InvitationDetails(
    string Club, string Language, bool Reminder, string Subject, string Letter, string Event, string When, string? Where,
    string Price, string ReplyUrl, string? TestNote = null);

/// <summary>
/// The emails a club sends its members, drawn in the club's look (<see cref="EmailLook.Club"/>):
/// the club's name at the head, every word in the member's language (<see cref="MemberTexts"/>),
/// the figures set out as facts, one button for what to do, and the outbox's footer added after.
/// </summary>
/// <remarks>
/// Pure: the services gather what an email says and these draw it, so each can be tested, and
/// previewed, without a database or a mail server. Everything from a record goes through
/// <see cref="EmailDesign"/>, which escapes it.
/// </remarks>
public static partial class ClubEmails
{
    private const string Nb = " ";

    private static string T(string language, string key, params (string, object?)[] args) => MemberTexts.T(language, key, args);

    /// <summary>
    /// A fact's label. The plain-text part sets a colon after it ("Montant dû : CHF 480.00"), so in
    /// French it ends with the no-break space French puts before one.
    /// </summary>
    private static string Label(string language, string key, params (string, object?)[] args) => Spaced(language, T(language, key, args));

    /// <summary>Words that a colon follows in the plain-text part, with the no-break space before it in French.</summary>
    private static string Spaced(string language, string text) =>
        AppLanguages.Normalise(language) == AppLanguages.French ? text.TrimEnd() + Nb : text;

    /// <summary>The club's look, branded with its name, in the member's language, saying why they got it.</summary>
    private static EmailDesign Design(string club, string language, string? eyebrow, string? title, string? preheader = null, string? footer = null) =>
        new(EmailLook.Club, Blank(club))
        {
            Language = AppLanguages.Normalise(language) ?? AppLanguages.English,
            Eyebrow = eyebrow,
            Title = title,
            Preheader = preheader,
            FooterNote = footer ?? (Blank(club) is { } name ? T(language, "footer.member", ("club", name)) : null),
        };

    private static EmailDesign SignedOff(this EmailDesign design, string club, string language) =>
        design.SignOff(T(language, "signOff"), Blank(club));

    // ── billing ───────────────────────────────────────────────────────────────

    public static ClubEmail Statement(StatementDetails d)
    {
        var l = d.Language;
        var owing = d.Owed > 0;
        var (preheader, closing) = owing ? ("statement.amountDue", "statement.labelAmountDue")
            : d.Owed < 0 ? ("statement.inCredit", "statement.labelInCredit")
            : ("statement.balance", "statement.labelBalance");
        // What it comes to, large, then how it got there, as the attached PDF sets it out.
        var figures = new List<(string, string)> { (Label(l, closing), d.Closing) };
        if (owing && Blank(d.DueDate) is { } due) figures.Add((Label(l, "statement.labelDueDate"), due));
        var design = Design(d.Club, l, T(l, "statement.eyebrow"), d.Period, T(l, preheader, ("amount", d.Closing)));
        if (Blank(d.TestNote) is { } test) design.Note(test, EmailTone.Warning);
        design.Paragraph(T(l, "greeting", ("name", d.FirstName)))
            .Paragraph(T(l, "statement.attached", ("period", d.Period)))
            .Stats(figures)
            .Facts([
                (Label(l, "statement.labelBroughtForward"), d.BroughtForward),
                (Label(l, "statement.labelCharges"), d.Charges),
                (Label(l, "statement.labelPayments"), d.Payments),
            ]);
        var pay = owing ? Blank(d.PayUrl) : null;
        if (pay != null)
        {
            design.Button(T(l, "button.payOnline"), pay);
            if (Blank(d.PayUntil) is { } until) design.Paragraph(T(l, "payLink.validUntil", ("date", until)), muted: true);
        }
        if (owing && d.QrBill) design.Note(T(l, pay != null ? "statement.qrBillToo" : "pdf.qrNote"));
        design.Paragraph(T(l, "statement.question")).SignedOff(d.Club, l);
        return new ClubEmail(T(l, "statement.subject", ("period", d.Period)), design);
    }

    public static ClubEmail Reminder(ReminderDetails d)
    {
        var l = d.Language;
        var final = d.Step >= d.Steps && d.Steps > 1;
        var subject = final ? T(l, "reminder.subjectFinal")
            : d.Step <= 1 ? T(l, "reminder.subjectFirst")
            : d.Step == 2 ? T(l, "reminder.subjectSecond")
            : T(l, "reminder.subjectNth", ("step", d.Step));
        var title = final ? T(l, "reminder.titleFinal") : d.Step <= 1 ? T(l, "reminder.titleFirst") : T(l, "reminder.titleLater");
        var rows = d.Charges
            .Select(c => (Label(l, "reminder.chargeLabel", ("description", c.Description), ("date", c.DueDate)), (string?)c.Amount))
            .ToList();
        rows.Add((Label(l, "reminder.labelTotal"), d.Total));
        rows.Add((Label(l, "reminder.labelDays"), d.DaysOverdue > 0 ? d.DaysOverdue.ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
        var design = Design(d.Club, l, subject, title, d.Total is { } total ? T(l, "reminder.total", ("amount", total)) : null)
            .Paragraph(T(l, "greeting", ("name", d.FirstName)))
            .Paragraph(final ? T(l, "reminder.introFinal") : d.Step <= 1 ? T(l, "reminder.introFirst") : T(l, "reminder.introLater"))
            .Facts(rows);
        if (d.LapseAfterDays is int days) design.Note(T(l, "reminder.lapse", ("days", days)), EmailTone.Warning);
        if (Blank(d.PayUrl) is { } pay)
        {
            design.Button(T(l, "button.payOnline"), pay);
            if (Blank(d.PayUntil) is { } until) design.Paragraph(T(l, "payLink.validUntil", ("date", until)), muted: true);
        }
        design.Paragraph(T(l, "reminder.ignore")).SignedOff(d.Club, l);
        return new ClubEmail(subject, design);
    }

    public static ClubEmail PayLink(PayLinkDetails d)
    {
        var l = d.Language;
        var club = Blank(d.Club) ?? T(l, "theClub");
        if (d.Setup)
        {
            var setup = Design(d.Club, l, T(l, "payLink.eyebrowSetup"), T(l, "payLink.titleSetup"), T(l, d.AutoPay ? "payLink.setupAutoPay" : "payLink.setupPlain"))
                .Paragraph(T(l, "greeting", ("name", d.FirstName)))
                .Paragraph(T(l, d.AutoPay ? "payLink.setupAutoPay" : "payLink.setupPlain"))
                .Button(T(l, "payLink.buttonSetup"), d.Url)
                .Note(T(l, "payLink.setupSecure") + " " + T(l, "payLink.validUntil", ("date", d.Until)))
                .SignedOff(d.Club, l);
            return new ClubEmail(T(l, "payLink.subjectSetup", ("club", club)), setup);
        }
        var pay = T(l, "payLink.payOnline", ("amount", d.Amount), ("methods", d.Methods));
        var design = Design(d.Club, l, T(l, "payLink.eyebrowPay"), T(l, "payLink.titlePay"), pay)
            .Paragraph(T(l, "greeting", ("name", d.FirstName)))
            .Paragraph(pay)
            .Button(T(l, "payLink.buttonPay", ("amount", d.Amount)), d.Url)
            .Note(T(l, "payLink.validUntil", ("date", d.Until)))
            .SignedOff(d.Club, l);
        return new ClubEmail(T(l, "payLink.subjectPay", ("club", club)), design);
    }

    // ── membership ────────────────────────────────────────────────────────────

    public static ClubEmail Welcome(WelcomeDetails d)
    {
        var l = d.Language;
        var club = Blank(d.Club) ?? T(l, "theClub");
        var subject = T(l, "welcome.subject", ("club", club));
        var approved = T(l, "welcome.approved", ("tier", d.Tier), ("club", club));
        var design = Design(d.Club, l, T(l, "welcome.eyebrow"), subject, approved)
            .Paragraph(T(l, "greeting", ("name", d.FirstName)))
            .Paragraph(approved)
            .Paragraph(T(l, "welcome.closing"))
            .SignedOff(d.Club, l);
        return new ClubEmail(subject, design);
    }

    /// <summary>
    /// A security email: each link is a button with its address written out, and the note says
    /// how long it works and, for one the member asked for, that it can be ignored if they did not.
    /// </summary>
    public static ClubEmail PortalLinks(PortalLinkDetails d)
    {
        var l = d.Language;
        var club = Blank(d.Club) ?? "";
        var several = d.Links.Count > 1;
        EmailDesign design;
        string subject;
        if (d.Invite)
        {
            subject = T(l, "portal.inviteSubject", ("club", club));
            var intro = T(l, "portal.inviteIntro", ("club", club));
            design = Design(d.Club, l, T(l, "portal.eyebrow"), T(l, "portal.inviteTitle"), intro)
                .Paragraph(T(l, "greeting", ("name", d.Greeting)))
                .Paragraph(intro);
            foreach (var link in d.Links)
                design.Button(several ? T(l, "portal.inviteButtonFor", ("name", link.Name)) : T(l, "portal.inviteButton"), link.Url);
            design.Note(T(l, "portal.inviteValid", ("date", d.ValidUntil), ("portal", d.PortalUrl)));
        }
        else
        {
            subject = T(l, "portal.signInSubject", ("club", club));
            var lead = T(l, "portal.signInLead", ("club", club));
            design = Design(d.Club, l, T(l, "portal.eyebrow"), T(l, "portal.signInTitle"), lead)
                .Paragraph(T(l, "greeting", ("name", d.Greeting)))
                .Paragraph(lead);
            foreach (var link in d.Links)
                design.Button(several ? T(l, "portal.signInButtonAs", ("name", link.Name)) : T(l, "portal.signInButton"), link.Url);
            design.Note(T(l, "portal.signInValid", ("minutes", d.Minutes)));
        }
        design.SignOff(Blank(d.Club));
        return new ClubEmail(subject, design);
    }

    // ── events ────────────────────────────────────────────────────────────────

    public static ClubEmail SeatFreed(EventNoticeDetails d)
    {
        var l = d.Language;
        var lead = T(l, "event.promotedLead", ("event", d.Event));
        var design = Design(d.Club, l, T(l, "event.eyebrowPromoted"), d.Event, lead, EventFooter(d));
        if (Blank(d.FirstName) is { } name) design.Paragraph(T(l, "greeting", ("name", name)));
        design.Paragraph(lead).Facts(EventFacts(d));
        if (Blank(d.Ticket) is { } ticket)
            design.Paragraph(T(l, d.TicketOnHost ? "event.ticketOnHostAccount" : "event.ticketOnAccount", ("ticket", ticket)));
        design.Paragraph(T(l, "event.cantCome")).SignedOff(d.Club, l);
        return new ClubEmail(T(l, "event.promotedSubject", ("event", d.Event)), design);
    }

    public static ClubEmail EventCancelled(EventNoticeDetails d)
    {
        var l = d.Language;
        var lead = T(l, "event.cancelledLead", ("event", d.Event));
        var design = Design(d.Club, l, T(l, "event.eyebrowCancelled"), d.Event, lead, EventFooter(d));
        if (Blank(d.FirstName) is { } name) design.Paragraph(T(l, "greeting", ("name", name)));
        design.Paragraph(lead).Facts(EventFacts(d));
        if (d.TicketTakenOff && Blank(d.Ticket) is { } ticket) design.Paragraph(T(l, "event.ticketTakenOff", ("ticket", ticket)));
        design.Paragraph(T(l, "event.seeYou")).SignedOff(d.Club, l);
        return new ClubEmail(T(l, "event.cancelledSubject", ("event", d.Event)), design);
    }

    private static List<(string, string?)> EventFacts(EventNoticeDetails d) =>
    [
        (Label(d.Language, "fact.when"), d.When),
        (Label(d.Language, "fact.where"), d.Where),
    ];

    private static string? EventFooter(EventNoticeDetails d) =>
        d.Member ? null : T(d.Language, "footer.guest", ("event", d.Event));

    /// <summary>
    /// An invitation: the letter staff wrote, as paragraphs, with its reply link as the button and
    /// its "When: … / Where: …" lines as the facts. A letter without such lines gets the event's
    /// date, place and member price as facts before the button; one whose reply link sits inside a
    /// sentence keeps it there, and the button follows the letter.
    /// </summary>
    public static ClubEmail Invitation(InvitationDetails d)
    {
        var l = d.Language;
        var design = Design(d.Club, l, T(l, d.Reminder ? "invite.eyebrowReminder" : "invite.eyebrow"), d.Event, d.When);
        if (Blank(d.TestNote) is { } test) design.Note(test, EmailTone.Warning);

        var parts = Paragraphs(d.Letter);
        var values = new[] { d.When, d.Where, d.Price }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
        var factsAt = parts.FindIndex(p => FactLines(p, values) != null);
        var reply = d.ReplyUrl.Trim();
        var button = false;
        for (var i = 0; i < parts.Count; i++)
        {
            var lines = parts[i].Split('\n').Select(x => x.Trim()).ToList();
            var holdsLink = lines.Any(x => x == reply);
            if (holdsLink && factsAt < 0 && !button)
            {
                design.Facts(StandardFacts(d));
                factsAt = int.MaxValue;
            }
            if (i == factsAt)
            {
                design.Facts(FactLines(parts[i], values)!.Select(f => (Spaced(l, f.Label), (string?)f.Value)));
                continue;
            }
            if (!holdsLink)
            {
                design.Paragraph(parts[i]);
                continue;
            }
            // The line that is the link alone becomes the button; the words around it stay.
            design.Paragraph(string.Join("\n", lines.Where(x => x != reply && x.Length > 0)));
            if (!button) design.Button(T(l, "invite.button"), reply);
            button = true;
        }
        if (!button)
        {
            if (factsAt < 0) design.Facts(StandardFacts(d));
            design.Button(T(l, "invite.button"), reply);
        }
        return new ClubEmail(d.Subject, design);
    }

    private static List<(string, string?)> StandardFacts(InvitationDetails d) =>
    [
        (Label(d.Language, "fact.when"), d.When),
        (Label(d.Language, "fact.where"), d.Where),
        (Label(d.Language, "fact.memberPrice"), d.Price),
    ];

    [GeneratedRegex(@"^(?<label>[^:\n]{1,40}?)\s*:\s*(?<value>\S.*)$")]
    private static partial Regex FactLine();

    /// <summary>
    /// A paragraph that lists the event's details, a "Label: value" line each ("When: Friday…"),
    /// with at least one of the event's own values in it; null for any other paragraph.
    /// </summary>
    private static List<(string Label, string Value)>? FactLines(string paragraph, IReadOnlyList<string> values)
    {
        var rows = new List<(string Label, string Value)>();
        foreach (var line in paragraph.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var m = FactLine().Match(line);
            if (!m.Success || m.Groups["value"].Value.StartsWith("//", StringComparison.Ordinal)) return null;
            rows.Add((m.Groups["label"].Value.Trim(), m.Groups["value"].Value.Trim()));
        }
        return rows.Count > 0 && rows.Any(r => values.Contains(r.Value, StringComparer.OrdinalIgnoreCase)) ? rows : null;
    }

    // ── what staff write ──────────────────────────────────────────────────────

    /// <summary>
    /// A message in the club's own words (a campaign, a note to one member, the retention rules'
    /// email): the text as paragraphs, under the subject as its title when the subject reads as one
    /// and the text does not open with it.
    /// </summary>
    /// <param name="title">Whether the subject may stand as the title; a message to one member has none.</param>
    /// <param name="eyebrow">A small label above, for a test copy.</param>
    /// <param name="testNote">For a test copy, what it says to the person who sent it to themselves.</param>
    public static EmailDesign Letter(string? club, string language, string subject, string body, bool title, string? eyebrow = null, string? testNote = null)
    {
        var parts = Paragraphs(body);
        var heading = title && ReadsAsTitle(subject) && !string.Equals(parts.FirstOrDefault()?.Trim(), subject.Trim(), StringComparison.OrdinalIgnoreCase)
            ? subject.Trim()
            : null;
        // The line an inbox shows after the subject: the first words past a greeting ("Dear Anna,").
        var opening = parts.FirstOrDefault(p => !(p.Length <= 40 && p.TrimEnd().EndsWith(','))) ?? parts.FirstOrDefault();
        var design = Design(club ?? "", language, eyebrow, heading, opening?.Replace('\n', ' '));
        if (Blank(testNote) is { } test) design.Note(test, EmailTone.Warning);
        return design.Paragraphs(body);
    }

    [GeneratedRegex(@"^(re|fwd?|aw|wg|tr)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefix();

    /// <summary>
    /// Whether a subject reads as an email's title: a short line, not a reply or a forward, not a
    /// tag in brackets, no address in it, and not shouting.
    /// </summary>
    public static bool ReadsAsTitle(string? subject)
    {
        var s = (subject ?? "").Trim();
        if (s.Length is < 3 or > 70) return false;
        if (ReplyPrefix().IsMatch(s) || s.StartsWith('[') || s.Contains("://", StringComparison.Ordinal) || s.Contains('@')) return false;
        var letters = s.Where(char.IsLetter).ToList();
        if (letters.Count >= 6 && letters.All(char.IsUpper)) return false;
        return s.Count(c => c == '!') <= 1;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    [GeneratedRegex(@"\n[ \t]*\n+")]
    private static partial Regex BlankLines();

    /// <summary>Text written with blank lines between paragraphs, as paragraphs (as <see cref="EmailDesign.Paragraphs"/> reads it).</summary>
    private static List<string> Paragraphs(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : BlankLines().Split(text.Replace("\r\n", "\n").Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
