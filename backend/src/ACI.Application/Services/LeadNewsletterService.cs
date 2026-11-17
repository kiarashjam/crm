using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// A newsletter to chosen leads from the Leads page, inviting each to book a meeting on the
/// workspace's website with a link of their own.
/// </summary>
public interface ILeadNewsletterService
{
    /// <summary>Whether a newsletter can give each lead a booking link here, who it goes out as, and its placeholders.</summary>
    Task<Result<NewsletterSetupDto>> SetupAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>Who a newsletter with these filters would reach now, and who is left out and why.</summary>
    Task<Result<NewsletterAudienceDto>> AudienceAsync(Guid organizationId, NewsletterAudienceRequest filters, CancellationToken ct = default);

    /// <summary>The email as one lead (or a sample person) will read it. Its booking link is a test one.</summary>
    Task<Result<NewsletterPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, NewsletterEmailRequest request, CancellationToken ct = default);

    /// <summary>The email as the preview shows it, marked as a test, to the person asking and nobody else.</summary>
    Task<Result<NewsletterTestDto>> SendTestAsync(Guid organizationId, Guid userId, NewsletterEmailRequest request, CancellationToken ct = default);

    /// <summary>
    /// Queues one email per lead the filters choose, each with the lead's own booking link and
    /// code. The same send id sent again sends nothing more.
    /// </summary>
    Task<Result<NewsletterSendDto>> SendAsync(Guid organizationId, Guid userId, NewsletterSendRequest request, CancellationToken ct = default);
}

/// <remarks>
/// <para>
/// The server decides who it goes to, from the filters, the same way for the counts the
/// dialog shows and for the send: leads with an address, not marked do not contact, whose
/// address the mail service has not reported (unsubscribed, bounced, complained), one email
/// per address. The outbox checks the suppression list again as it queues and as it sends,
/// so a "stop" that arrives meanwhile still wins.
/// </para>
/// <para>
/// Each lead's email is filled in here, HTML-encoded, and queued with its own HTML part; the
/// outbox adds the unsubscribe footer to it and the List-Unsubscribe headers, as to any email,
/// and writes it on the lead's timeline once it has gone. The emails are spread over the
/// minutes after the send, <see cref="PerMinute"/> a minute, so the mail server takes them at
/// a steady rate.
/// </para>
/// </remarks>
public sealed class LeadNewsletterService : ILeadNewsletterService
{
    /// <summary>The most leads one newsletter goes to: past it, the filters are to be narrowed.</summary>
    public const int MaxRecipients = 2000;

    /// <summary>Emails a minute the mail server is given.</summary>
    public const int PerMinute = 60;

    /// <summary>Leads read for the counts: a workspace's whole book, many times over.</summary>
    private const int MaxRead = 50_000;

    private const int SampleSize = 8;
    internal const int MaxSubject = 300;
    internal const int MaxHtml = 200_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Someone to show the email for when no lead is chosen.</summary>
    private static readonly (string Name, string Email) Sample = ("Camille Laurent", "camille@example.com");

    private readonly ILeadNewsletterRepository _newsletters;
    private readonly IOnboardingRepository _onboarding;
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;
    private readonly IOutreachMail _outreach;
    private readonly IPublicAddress _address;
    private readonly IOutbox _outbox;
    private readonly ILogger<LeadNewsletterService> _logger;

    public LeadNewsletterService(
        ILeadNewsletterRepository newsletters,
        IOnboardingRepository onboarding,
        IOrganizationRepository organizations,
        IUserRepository users,
        IOutreachMail outreach,
        IPublicAddress address,
        IOutbox outbox,
        ILogger<LeadNewsletterService> logger)
    {
        _newsletters = newsletters;
        _onboarding = onboarding;
        _organizations = organizations;
        _users = users;
        _outreach = outreach;
        _address = address;
        _outbox = outbox;
        _logger = logger;
    }

    // ── What it can do ────────────────────────────────────────────────────────

    public async Task<Result<NewsletterSetupDto>> SetupAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var booking = await BookingAsync(organizationId, ct);
        var sender = await _outreach.SenderAsync(organizationId, userId, ct);
        return new NewsletterSetupDto(
            booking.Problem == null, booking.Problem, booking.Page, NewsletterTemplate.Fields, sender.WorkspaceName,
            booking.Language, sender.WorkspaceName, sender.ReplyTo, MaxRecipients);
    }

    // ── Who it goes to ────────────────────────────────────────────────────────

    public async Task<Result<NewsletterAudienceDto>> AudienceAsync(Guid organizationId, NewsletterAudienceRequest filters, CancellationToken ct = default)
    {
        if (FilterProblem(filters) is { } problem) return problem;
        var plan = await PlanAsync(organizationId, filters, ct);
        return new NewsletterAudienceDto(
            plan.Matching, plan.Recipients.Count,
            plan.LeftOut.Count(p => p.Reason == Reasons.NoEmail),
            plan.LeftOut.Count(p => p.Reason == Reasons.DoNotContact),
            plan.LeftOut.Count(p => p.Reason == Reasons.Suppressed),
            plan.LeftOut.Count(p => p.Reason == Reasons.Duplicate),
            plan.Recipients.Take(SampleSize).Select(r => Person(r.Lead, null)).ToList(),
            plan.LeftOut.Take(SampleSize).ToList(),
            MaxRecipients);
    }

    private static class Reasons
    {
        public const string NoEmail = "no_email";
        public const string DoNotContact = "do_not_contact";
        public const string Suppressed = "suppressed";
        public const string Duplicate = "duplicate";
    }

    private sealed record Recipient(NewsletterLead Lead, string Email);

    private sealed record Plan(int Matching, IReadOnlyList<Recipient> Recipients, IReadOnlyList<NewsletterPersonDto> LeftOut);

    /// <summary>
    /// The leads the filters choose, split into those who get it and those left out, in the
    /// order they came in: of two leads at one address, the older gets it.
    /// </summary>
    private async Task<Plan> PlanAsync(Guid organizationId, NewsletterAudienceRequest filters, CancellationToken ct)
    {
        var leads = await _newsletters.LeadsAsync(organizationId, filters, MaxRead, ct);
        var unreachable = await _newsletters.UnreachableAsync(organizationId, leads, ct);
        var recipients = new List<Recipient>();
        var leftOut = new List<NewsletterPersonDto>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lead in leads)
        {
            var email = lead.Email?.Trim() ?? "";
            var key = email.ToLowerInvariant();
            var reason = email.Length == 0 || !ValidationHelper.IsValidEmail(email) ? Reasons.NoEmail
                : unreachable.DoNotContact.Contains(lead.Id) ? Reasons.DoNotContact
                : unreachable.Suppressed.Contains(key) ? Reasons.Suppressed
                : !addresses.Add(key) ? Reasons.Duplicate
                : null;
            if (reason == null) recipients.Add(new Recipient(lead, email));
            else leftOut.Add(Person(lead, reason));
        }
        return new Plan(leads.Count, recipients, leftOut);
    }

    private static NewsletterPersonDto Person(NewsletterLead lead, string? reason) =>
        new(lead.Id, lead.Name, string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim(), lead.Status, reason);

    // ── The email ─────────────────────────────────────────────────────────────

    public async Task<Result<NewsletterPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, NewsletterEmailRequest request, CancellationToken ct = default)
    {
        if (EmailProblem(request, sending: false) is { } problem) return problem;
        var booking = await BookingAsync(organizationId, ct);
        var sender = await _outreach.SenderAsync(organizationId, userId, ct);
        var lead = request.SampleLeadId is Guid id ? await _newsletters.LeadAsync(organizationId, id, ct) : null;
        var name = lead?.Name ?? Sample.Name;
        var email = lead == null ? Sample.Email : lead.Email?.Trim();
        var code = MeetingInvitation.Code(lead?.Id ?? Guid.Empty);
        // A test invitation: the website shows it and takes a booking, which the CRM records nowhere.
        var url = booking.Problem == null ? BookingUrl(booking, organizationId, Guid.Empty, name, email ?? Sample.Email, code) : null;
        var values = new NewsletterTemplate.Values(name, url, code, sender.WorkspaceName);
        var html = NewsletterTemplate.Fill(request.Html, values, html: true);
        return new NewsletterPreviewDto(
            OutboxService.OneLine(NewsletterTemplate.Fill(request.Subject, values, html: false)),
            html,
            OnboardingTemplate.PlainText(html),
            name, email, code,
            NewsletterTemplate.Unknown(request.Subject, request.Html),
            NewsletterTemplate.NeedsBookingUrl(request.Subject, request.Html) ? booking.Problem : null,
            sender.WorkspaceName, sender.ReplyTo);
    }

    public async Task<Result<NewsletterTestDto>> SendTestAsync(Guid organizationId, Guid userId, NewsletterEmailRequest request, CancellationToken ct = default)
    {
        if (EmailProblem(request, sending: true) is { } problem) return problem;
        var to = (await _users.GetByIdAsync(userId, ct))?.Email?.Trim() ?? "";
        if (!ValidationHelper.IsValidEmail(to)) return new Error("Newsletter.NoTestAddress", "Your account has no email address to send the test to.");
        var preview = await PreviewAsync(organizationId, userId, request, ct);
        if (preview.IsFailure) return preview.Error;
        var email = preview.Value;
        if (Refusal(email.Unknown, email.BookingProblem) is { } refused) return refused;

        var french = Language(request.Language) == "fr";
        var note = french
            ? $"Ceci est un test de votre newsletter, telle que {email.SampleName} la recevra. Son lien de réservation est un lien de test : un rendez-vous pris avec lui n'est enregistré nulle part."
            : $"This is a test of your newsletter, as {email.SampleName} will receive it. Its booking link is a test one: a meeting booked with it is recorded nowhere.";
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.NewsletterTest,
            To = [to],
            Language = french ? "fr" : "en",
            Subject = $"[Test] {email.Subject}",
            Body = $"{note}\n\n{email.Text}",
            Html = OnboardingTemplate.WithBanner(email.Html, note),
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            ForOwnTeam = true,
            PersonWaiting = true,
        }, ct);
        if (queued.IsFailure) return queued.Error;

        // Not the request's token: a browser that hangs up must not cut the attempt off halfway.
        var attempt = await _outbox.SendNowAsync(queued.Value, CancellationToken.None);
        return attempt.Result switch
        {
            OutboxAttemptResult.NotConfigured => new Error("Email.NotConfigured", "Sending email is not set up on the server yet."),
            OutboxAttemptResult.Suppressed => new Error(attempt.Code ?? "Email.Suppressed", attempt.Error ?? $"{to} is on the suppression list, so the test was not sent."),
            OutboxAttemptResult.Failed => new Error("Email.SendFailed", $"The test was not sent. {attempt.Error ?? "The mail server refused it."}"),
            _ => new NewsletterTestDto(to, attempt.Result == OutboxAttemptResult.Sent ? "sent" : "queued"),
        };
    }

    // ── Sending ───────────────────────────────────────────────────────────────

    public async Task<Result<NewsletterSendDto>> SendAsync(Guid organizationId, Guid userId, NewsletterSendRequest request, CancellationToken ct = default)
    {
        if (request.SendId == Guid.Empty) return new Error("Newsletter.SendId", "The send has no id: reload the page and send again.");
        // Sent already: the same press again (a double click, a retry), which sends nothing more.
        if (await _newsletters.GetAsync(organizationId, request.SendId, ct) is { } earlier) return Dto(earlier, alreadySent: true);

        if (EmailProblem(request, sending: true) is { } problem) return problem;
        var filters = request.Audience ?? new NewsletterAudienceRequest();
        if (FilterProblem(filters) is { } wrong) return wrong;
        var booking = await BookingAsync(organizationId, ct);
        if (Refusal(NewsletterTemplate.Unknown(request.Subject, request.Html),
                NewsletterTemplate.NeedsBookingUrl(request.Subject, request.Html) ? booking.Problem : null) is { } refused)
            return refused;

        var plan = await PlanAsync(organizationId, filters, ct);
        if (plan.Recipients.Count == 0) return new Error("Newsletter.Nobody", "Nobody the filters choose can be emailed, so nothing was sent.");
        if (plan.Recipients.Count > MaxRecipients)
            return new Error("Newsletter.TooMany", $"This would go to {plan.Recipients.Count:N0} leads; one newsletter goes to at most {MaxRecipients:N0}. Narrow the filters.");
        if (request.ExpectedRecipients is int expected && expected != plan.Recipients.Count)
            return new Error("Newsletter.Changed.Conflict",
                $"Who it goes to has changed since you confirmed: it would now go to {plan.Recipients.Count:N0} lead{(plan.Recipients.Count == 1 ? "" : "s")}, not {expected:N0}. Check the list and confirm again.");

        var language = Language(request.Language);
        var subjectTemplate = OutboxService.OneLine(request.Subject);
        var now = DateTime.UtcNow;
        var newsletter = new LeadNewsletter
        {
            Id = request.SendId,
            OrganizationId = organizationId,
            Subject = Clip(subjectTemplate, MaxSubject)!,
            Language = language,
            AudienceJson = Clip(JsonSerializer.Serialize(filters, Json), 4000)!,
            Status = LeadNewsletterStatuses.Sending,
            LeftOut = plan.LeftOut.Count,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        };
        // The claim: of two requests with the same id, the database lets one in.
        if (!await _newsletters.TryAddAsync(newsletter, ct))
            return await _newsletters.GetAsync(organizationId, request.SendId, CancellationToken.None) is { } first
                ? Dto(first, alreadySent: true)
                : new Error("Newsletter.Changed.Conflict", "This newsletter is being sent from another window.");

        var workspace = (await _organizations.GetByIdAsync(organizationId, CancellationToken.None))?.Name;
        int queued = 0, refusedNow = 0;
        for (var i = 0; i < plan.Recipients.Count; i++)
        {
            var (lead, email) = plan.Recipients[i];
            try
            {
                var code = MeetingInvitation.Code(lead.Id);
                var url = booking.Problem == null ? BookingUrl(booking, organizationId, lead.Id, lead.Name, email, code) : null;
                var values = new NewsletterTemplate.Values(lead.Name, url, code, workspace);
                var html = NewsletterTemplate.Fill(request.Html, values, html: true);
                // Not the request's token: the send is claimed, and a browser that hangs up must
                // not leave it half sent.
                var result = await _outbox.QueueEmailAsync(new OutboundEmailRequest
                {
                    OrganizationId = organizationId,
                    Source = OutboundSources.Newsletter,
                    SourceId = newsletter.Id,
                    To = [email],
                    RecipientName = lead.Name,
                    Language = language,
                    Subject = NewsletterTemplate.Fill(subjectTemplate, values, html: false),
                    Body = OnboardingTemplate.PlainText(html),
                    Html = html,
                    LeadId = lead.Id,
                    CreatedByUserId = userId,
                    ReplyToUserId = userId,
                    TrackOpens = true,
                    NotBeforeUtc = now.AddMinutes(i / PerMinute),
                }, CancellationToken.None);
                if (result.IsSuccess) queued++;
                else
                {
                    // Someone who said stop, or bounced, between the count and now: the outbox refused them.
                    refusedNow++;
                    _logger.LogInformation("Newsletter {NewsletterId}: lead {LeadId} was not emailed: {Reason}", newsletter.Id, lead.Id, result.Error.Description);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                refusedNow++;
                _logger.LogError(ex, "Newsletter {NewsletterId}: the email to lead {LeadId} could not be queued", newsletter.Id, lead.Id);
            }
        }

        newsletter.Recipients = queued;
        newsletter.LeftOut = plan.LeftOut.Count + refusedNow;
        newsletter.Status = LeadNewsletterStatuses.Sent;
        newsletter.SentAtUtc = DateTime.UtcNow;
        await _newsletters.SaveAsync(CancellationToken.None);
        _logger.LogInformation("Newsletter {NewsletterId} queued for {Recipients} lead(s), {LeftOut} left out", newsletter.Id, queued, newsletter.LeftOut);
        return Dto(newsletter, alreadySent: false);
    }

    // ── Pieces ────────────────────────────────────────────────────────────────

    private sealed record Booking(string? Page, string? Secret, string Language, string? Problem);

    /// <summary>The booking page and secret, or why a lead's booking link cannot be made here.</summary>
    private async Task<Booking> BookingAsync(Guid organizationId, CancellationToken ct)
    {
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        var page = string.IsNullOrWhiteSpace(settings?.BookingPageUrl) ? null : settings.BookingPageUrl.Trim();
        var secret = string.IsNullOrWhiteSpace(settings?.WebhookSecret) ? null : settings.WebhookSecret;
        var problem = page == null
            ? "There is no meeting booking page yet. Add it in Settings › Welcome & portal to give each lead a link to book."
            : secret == null
                ? "The booking link is signed with the Welcome & portal signing secret, and there is none yet. Make it in Settings › Welcome & portal."
                : null;
        return new Booking(page, secret, settings?.DefaultLanguage == "en" ? "en" : "fr", problem);
    }

    /// <summary>The lead's booking page, with their signed invitation, good for <see cref="MeetingInvitation.Lifetime"/>.</summary>
    private string BookingUrl(Booking booking, Guid organizationId, Guid leadId, string name, string email, string code)
    {
        var expires = DateTimeOffset.UtcNow.Add(MeetingInvitation.Lifetime).ToUnixTimeSeconds();
        var token = MeetingInvitation.Token(booking.Secret!, new MeetingInvitationPayload(
            1, organizationId, leadId, name, email, code, _address.ApiBaseUrl.TrimEnd('/'), expires));
        return MeetingInvitation.BookingUrl(booking.Page!, token);
    }

    private static Error? EmailProblem(NewsletterEmailRequest request, bool sending)
    {
        if (request.Language != null && request.Language.Trim().Length > 0 && Language(request.Language, null) == null)
            return new Error("Newsletter.Language", "The language is \"fr\" or \"en\".");
        var subject = OutboxService.OneLine(request.Subject);
        if (subject.Length > MaxSubject) return new Error("Newsletter.TooLong", $"The subject can be at most {MaxSubject} characters.");
        if ((request.Html?.Length ?? 0) > MaxHtml) return new Error("Newsletter.TooLong", $"The email's HTML can be at most {MaxHtml / 1000} KB.");
        if (!sending) return null;
        if (subject.Length == 0) return new Error("Newsletter.NoSubject", "Add a subject.");
        if (string.IsNullOrWhiteSpace(request.Html)) return new Error("Newsletter.NoBody", "Write the email first.");
        return null;
    }

    private static Error? Refusal(IReadOnlyList<string> unknown, string? bookingProblem) =>
        unknown.Count > 0
            ? new Error("Newsletter.Unfilled", $"Nothing fills {string.Join(", ", unknown)}. Use one of the listed fields, or give it a fallback, like {{{{firstName|there}}}}.")
            : bookingProblem != null
                ? new Error("Newsletter.NoBookingLink", $"The email has a booking link ({{{{bookingUrl}}}}) that cannot be filled in. {bookingProblem}")
                : null;

    private static Error? FilterProblem(NewsletterAudienceRequest filters)
    {
        var lists = new[] { filters.IncludeStatuses, filters.ExcludeStatuses, filters.Sources };
        if (lists.Any(l => l is { Count: > 50 }) || filters.OwnerIds is { Count: > 100 })
            return new Error("Newsletter.Filters", "Too many choices in one filter.");
        if (filters.CreatedFrom is DateOnly from && filters.CreatedTo is DateOnly to && to < from)
            return new Error("Newsletter.Filters", "The \"created to\" day is before the \"created from\" day.");
        return null;
    }

    private static string Language(string? language) => Language(language, "fr")!;

    private static string? Language(string? language, string? otherwise) => language?.Trim().ToLowerInvariant() switch
    {
        "fr" => "fr",
        "en" => "en",
        _ => otherwise,
    };

    private static NewsletterSendDto Dto(LeadNewsletter n, bool alreadySent) =>
        new(n.Id, n.Status, n.Recipients, n.LeftOut, alreadySent, n.CreatedAtUtc);

    private static string? Clip(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}
