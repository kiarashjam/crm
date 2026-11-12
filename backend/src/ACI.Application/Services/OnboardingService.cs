using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>
/// Welcoming a new customer: when a lead becomes a deal, the person is handed to the
/// workspace's own portal, which opens their account and sends the welcome email, and
/// reports each step back here.
/// </summary>
public interface IOnboardingService
{
    Task<Result<OnboardingSettingsDto>> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<OnboardingSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, SaveOnboardingSettingsRequest request, CancellationToken ct = default);
    Task<Result<OnboardingSecretDto>> NewSecretAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<OnboardingPingDto>> PingAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<OnboardingPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, OnboardingPreviewRequest request, CancellationToken ct = default);
    Task<Result<OnboardingDto>> StartAsync(Guid dealId, Guid organizationId, Guid userId, StartOnboardingRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OnboardingDto>>> ForDealAsync(Guid dealId, Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<OnboardingDto>> ResendAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>A report from the receiving system, which proves itself with the handoff's callback token.</summary>
    Task<Result<OnboardingReportReceipt>> ReportAsync(Guid id, string? token, OnboardingReportRequest request, CancellationToken ct = default);
}

public sealed class OnboardingService : IOnboardingService
{
    public const string HandoffEvent = "deal.onboarding";
    public const string PingEvent = "onboarding.ping";

    /// <summary>A designed email runs to tens of kilobytes; a pasted page with images inlined, more. This is plenty.</summary>
    internal const int MaxHtml = 200_000;
    internal const int MaxSubject = 300;
    internal const int MaxMessage = 4000;
    /// <summary>A handoff resent and reported on many times still keeps a readable history.</summary>
    internal const int MaxSteps = 60;

    /// <summary>What a receiver may report, and where each one leaves the handoff.</summary>
    private static readonly Dictionary<string, string> Reports = new(StringComparer.Ordinal)
    {
        ["account_created"] = OnboardingStatuses.AccountReady,
        ["account_existing"] = OnboardingStatuses.AccountReady,
        ["email_sent"] = OnboardingStatuses.Welcomed,
        ["password_set"] = OnboardingStatuses.Active,
        ["email_failed"] = OnboardingStatuses.Failed,
        ["failed"] = OnboardingStatuses.Failed,
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOnboardingRepository _onboarding;
    private readonly IOrganizationRepository _organizations;
    private readonly IDealService _deals;
    private readonly IContactRepository _contacts;
    private readonly IClubService _club;
    private readonly IUserRepository _users;
    private readonly IOutboxRepository _outbox;
    private readonly IOutboxSignal _signal;
    private readonly IWebhookTransport _transport;
    private readonly IActivityService _activities;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceModules _modules;
    private readonly EmailSettings _settings;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(
        IOnboardingRepository onboarding,
        IOrganizationRepository organizations,
        IDealService deals,
        IContactRepository contacts,
        IClubService club,
        IUserRepository users,
        IOutboxRepository outbox,
        IOutboxSignal signal,
        IWebhookTransport transport,
        IActivityService activities,
        ITenantContext tenant,
        IWorkspaceModules modules,
        IOptions<EmailSettings> settings,
        ILogger<OnboardingService> logger)
    {
        _onboarding = onboarding;
        _organizations = organizations;
        _deals = deals;
        _contacts = contacts;
        _club = club;
        _users = users;
        _outbox = outbox;
        _signal = signal;
        _transport = transport;
        _activities = activities;
        _tenant = tenant;
        _modules = modules;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public async Task<Result<OnboardingSettingsDto>> GetSettingsAsync(Guid organizationId, CancellationToken ct = default) =>
        ToDto(await _onboarding.GetSettingsAsync(organizationId, ct), await _organizations.GetByIdAsync(organizationId, ct));

    public async Task<Result<OnboardingSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, SaveOnboardingSettingsRequest request, CancellationToken ct = default)
    {
        if (!await _organizations.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);

        var url = request.WebhookUrl?.Trim() ?? "";
        if (url.Length > 0 && AddressProblem(url) is { } problem) return new Error("Onboarding.WebhookUrl", problem);
        if (url.Length > 500) return new Error("Onboarding.WebhookUrl", "The address can be at most 500 characters.");
        if ((request.EmailSubject?.Trim().Length ?? 0) > MaxSubject) return new Error("Onboarding.TooLong", $"The subject can be at most {MaxSubject} characters.");
        if ((request.EmailHtml?.Length ?? 0) > MaxHtml) return new Error("Onboarding.TooLong", $"The email's HTML can be at most {MaxHtml / 1000} KB.");
        if (request.DefaultLanguage != null && Language(request.DefaultLanguage) == null)
            return new Error("Onboarding.Language", "The language is \"fr\" or \"en\".");
        if (UnknownPlaceholders(request.EmailSubject, request.EmailHtml) is { Count: > 0 } unknown)
            return Unknown(unknown);
        if (Blank(request.SenderName) is { } senderName && (senderName.Length > 100 || senderName.Any(char.IsControl)))
            return new Error("Onboarding.Sender", "The sender's name can be at most 100 characters, on one line.");
        if (Blank(request.SenderEmail) is { } senderEmail && !IsEmail(senderEmail))
            return new Error("Onboarding.Sender", "The from address is not an email address.");
        if (Blank(request.ReplyTo) is { } replyTo && !IsEmail(replyTo))
            return new Error("Onboarding.Sender", "The reply-to address is not an email address.");

        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (settings == null)
        {
            settings = new OnboardingSettings { Id = Guid.NewGuid(), OrganizationId = organizationId };
            _onboarding.AddSettings(settings);
        }
        settings.WebhookUrl = url.Length == 0 ? null : url;
        if (request.Enabled is bool enabled) settings.Enabled = enabled;
        settings.EmailSubject = Blank(request.EmailSubject);
        settings.EmailHtml = string.IsNullOrWhiteSpace(request.EmailHtml) ? null : request.EmailHtml;
        if (request.DefaultLanguage != null) settings.DefaultLanguage = Language(request.DefaultLanguage)!;
        if (request.AddToClubMembers is bool add) settings.AddToClubMembers = add;
        settings.SenderName = Blank(request.SenderName);
        settings.SenderEmail = Blank(request.SenderEmail)?.ToLowerInvariant();
        settings.ReplyTo = Blank(request.ReplyTo)?.ToLowerInvariant();
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.UpdatedByUserId = userId;
        await _onboarding.SaveAsync(ct);
        return ToDto(settings, await _organizations.GetByIdAsync(organizationId, ct));
    }

    public async Task<Result<OnboardingSecretDto>> NewSecretAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (!await _organizations.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (settings == null)
        {
            settings = new OnboardingSettings { Id = Guid.NewGuid(), OrganizationId = organizationId };
            _onboarding.AddSettings(settings);
        }
        var secret = WebhookSigning.NewSecret();
        settings.WebhookSecret = secret;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.UpdatedByUserId = userId;
        await _onboarding.SaveAsync(ct);
        return new OnboardingSecretDto(secret);
    }

    public async Task<Result<OnboardingPingDto>> PingAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (!await _organizations.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (NotSetUp(settings) is { } notSetUp) return notSetUp;
        if (AddressProblem(settings!.WebhookUrl!) is { } problem) return new Error("Onboarding.WebhookUrl", problem);

        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            eventName = PingEvent,
            payload = new { version = 1, workspace = new { id = organizationId, name = workspace?.Name } },
            sentAtUtc = now,
            id,
            organizationId,
            occurredAtUtc = now,
        });
        var answer = await _transport.PostAsync(new Uri(settings.WebhookUrl!.Trim()), body, Headers(PingEvent, id, settings.WebhookSecret!, body), ct);
        if (!answer.Succeeded)
            return new OnboardingPingDto(false, answer.StatusCode, answer.Error ?? $"The portal answered {answer.StatusCode}.");
        // A portal may say who its email goes out from: { "from": { "name", "email" } }.
        string? fromName = null, fromEmail = null;
        try
        {
            using var said = JsonDocument.Parse(answer.Body ?? "{}");
            if (said.RootElement.ValueKind == JsonValueKind.Object
                && said.RootElement.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Object)
            {
                fromName = Clip(Text(from, "name"), 100);
                fromEmail = Text(from, "email") is { } e && IsEmail(e) ? e : null;
            }
        }
        catch (JsonException)
        {
            // Not JSON, or not this: the portal took the ping, which is what was asked.
        }
        return new OnboardingPingDto(true, answer.StatusCode, null, fromName, fromEmail);
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    public async Task<Result<OnboardingPreviewDto>> PreviewAsync(Guid organizationId, Guid userId, OnboardingPreviewRequest request, CancellationToken ct = default)
    {
        if (Problem(request.WelcomeMessage, request.EmailSubject, request.EmailHtml, request.StartsOnUtc) is { } problem) return problem;
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        var language = Language(request.Language) ?? settings?.DefaultLanguage ?? "fr";

        string name = request.Name?.Trim() ?? "", email = request.Email?.Trim() ?? "";
        string? company = request.Company, dealName = request.DealName;
        if (request.DealId is Guid dealId)
        {
            var person = await PersonAsync(dealId, organizationId, userId, ct);
            if (person.IsFailure) return person.Error;
            (name, email, company, dealName) = (person.Value.Name, person.Value.Email, person.Value.Company, person.Value.Deal.Name);
        }

        var values = await ValuesAsync(organizationId, userId, name, email, company, dealName, request.WelcomeMessage,
            request.StartsOnUtc, request.StartUndetermined, language, ct);
        var subjectTemplate = Blank(request.EmailSubject) ?? settings?.EmailSubject;
        var htmlTemplate = string.IsNullOrWhiteSpace(request.EmailHtml) ? settings?.EmailHtml : request.EmailHtml;

        var sender = Sender(settings, await _organizations.GetByIdAsync(organizationId, ct));
        var subject = OnboardingTemplate.Fill(subjectTemplate ?? DefaultSubject(language, values.Workspace), values, html: false, out var unknownInSubject);
        if (string.IsNullOrWhiteSpace(htmlTemplate))
        {
            return new OnboardingPreviewDto(subject, StandIn(values, language), UsesReceiverDesign: true, unknownInSubject,
                sender.Name, sender.Email, sender.ReplyTo);
        }
        var html = OnboardingTemplate.Fill(htmlTemplate, values, html: true, out var unknownInHtml);
        return new OnboardingPreviewDto(
            OnboardingTemplate.WithSampleReceiverValues(subject, values.Email, html: false),
            OnboardingTemplate.WithSampleReceiverValues(html, values.Email, html: true),
            UsesReceiverDesign: false,
            unknownInSubject.Concat(unknownInHtml).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            sender.Name, sender.Email, sender.ReplyTo);
    }

    // ── Handing over ──────────────────────────────────────────────────────────

    public async Task<Result<OnboardingDto>> StartAsync(Guid dealId, Guid organizationId, Guid userId, StartOnboardingRequest request, CancellationToken ct = default)
    {
        if (Problem(request.WelcomeMessage, request.EmailSubject, request.EmailHtml, request.StartsOnUtc) is { } problem) return problem;
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (NotSetUp(settings) is { } notSetUp) return notSetUp;

        var found = await PersonAsync(dealId, organizationId, userId, ct);
        if (found.IsFailure) return found.Error;
        var person = found.Value;
        if (string.IsNullOrWhiteSpace(person.Email))
            return new Error("Onboarding.NoEmail", "The deal's contact has no email address to welcome them at.");

        // One handoff per deal: a second would open a second account, or send a second
        // welcome. Sending again is Resend, on the handoff there is.
        var earlier = await _onboarding.ForDealAsync(dealId, organizationId, ct);
        if (earlier.FirstOrDefault() is { } last)
            return new Error("Onboarding.AlreadyExists",
                $"This deal was handed over on {last.CreatedAtUtc:d MMM yyyy}. Resend it from the deal instead.");

        var language = Language(request.Language) ?? settings!.DefaultLanguage;
        var startsOn = request.StartsOnUtc?.Date;
        // A date, or still to be decided: one or the other, never neither.
        var undetermined = startsOn == null;
        var values = await ValuesAsync(organizationId, userId, person.Name, person.Email, person.Company, person.Deal.Name,
            request.WelcomeMessage, startsOn, undetermined, language, ct);

        var subjectTemplate = Blank(request.EmailSubject) ?? settings!.EmailSubject;
        var htmlTemplate = string.IsNullOrWhiteSpace(request.EmailHtml) ? settings!.EmailHtml : request.EmailHtml;
        string? subject = null, html = null;
        var unknown = new List<string>();
        if (subjectTemplate != null)
        {
            subject = OnboardingTemplate.Fill(subjectTemplate, values, html: false, out var u);
            unknown.AddRange(u);
        }
        if (!string.IsNullOrWhiteSpace(htmlTemplate))
        {
            html = OnboardingTemplate.Fill(htmlTemplate, values, html: true, out var u);
            unknown.AddRange(u);
        }
        if (unknown.Count > 0) return Unknown(unknown.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        var now = DateTime.UtcNow;
        var onboarding = new DealOnboarding
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DealId = dealId,
            LeadId = request.LeadId,
            ContactId = person.ContactId,
            Name = Clip(person.Name, 200)!,
            Email = Clip(person.Email, 256)!,
            Phone = Clip(person.Phone, 64),
            Language = language,
            WelcomeMessage = Blank(request.WelcomeMessage),
            StartsOnUtc = startsOn,
            StartUndetermined = undetermined,
            EmailSubject = subject,
            EmailHtml = html,
            Status = OnboardingStatuses.Queued,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
            UpdatedAtUtc = now,
        };
        AddStep(onboarding, "queued", now, null);

        // A workspace that has turned its club off has no members list to show them on: they
        // would be kept where nobody sees them. The handoff, which is what was asked for, goes on.
        if ((request.AddToClubMembers ?? settings!.AddToClubMembers) && await _modules.IsOnAsync(organizationId, WorkspaceModules.Club, ct))
            onboarding.ClubMemberId = await JoinClubAsync(onboarding, person, userId, ct);

        _onboarding.Add(onboarding);
        await _onboarding.SaveAsync(ct);

        var message = await QueueAsync(onboarding, person, settings!, values, ct);
        await NoteAsync(onboarding, "Welcome & portal: handed over",
            $"{onboarding.Name} was handed to the portal to open their account and send the welcome email. Starts: {OnboardingTemplate.StartDate(startsOn, undetermined, "en")}.", ct);
        return ToDto(onboarding, message);
    }

    public async Task<Result<OnboardingDto>> ResendAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var onboarding = await _onboarding.GetAsync(id, organizationId, ct);
        if (onboarding == null) return new Error("Onboarding.NotFound", "That handoff was not found.");
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (NotSetUp(settings) is { } notSetUp) return notSetUp;
        var messages = await _onboarding.MessagesAsync(Ids(onboarding), ct);
        var message = onboarding.OutboundMessageId is Guid mid ? messages.GetValueOrDefault(mid) : null;
        if (message?.Status == OutboundStatuses.Queued)
            return new Error("Onboarding.Conflict", "It is still being handed over; the portal has not answered yet.");
        if (onboarding.Status == OnboardingStatuses.Active)
            return new Error("Onboarding.Conflict", "They have already set their password and are in.");

        var found = await PersonAsync(onboarding.DealId, organizationId, userId, ct);
        if (found.IsFailure) return found.Error;
        var values = await ValuesAsync(organizationId, userId, onboarding.Name, onboarding.Email, found.Value.Company, found.Value.Deal.Name,
            onboarding.WelcomeMessage, onboarding.StartsOnUtc, onboarding.StartUndetermined, onboarding.Language, ct);

        var now = DateTime.UtcNow;
        onboarding.Status = OnboardingStatuses.Queued;
        onboarding.UpdatedAtUtc = now;
        AddStep(onboarding, "resent", now, null);
        await _onboarding.SaveAsync(ct);
        var queued = await QueueAsync(onboarding, found.Value, settings!, values, ct);
        return ToDto(onboarding, queued);
    }

    public async Task<Result<IReadOnlyList<OnboardingDto>>> ForDealAsync(Guid dealId, Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        // Only for a deal this person may see: a private deal's handoffs are as private.
        var deal = await _deals.GetByIdAsync(dealId, userId, organizationId, ct);
        if (deal.IsFailure) return deal.Error;
        return Result.Success(await ToDtosAsync(await _onboarding.ForDealAsync(dealId, organizationId, ct), ct));
    }

    // ── Reports from the receiver ─────────────────────────────────────────────

    public async Task<Result<OnboardingReportReceipt>> ReportAsync(Guid id, string? token, OnboardingReportRequest request, CancellationToken ct = default)
    {
        // The same answer for a handoff that does not exist and a wrong token, so the
        // address tells a stranger nothing.
        var refused = new Error("General.Unauthorized", "That handoff and token do not match.");
        var found = await _onboarding.GetInAnyWorkspaceAsync(id, ct);
        if (found == null) return refused;
        using var acting = _tenant.ActFor(found.OrganizationId, found.CreatedByUserId);
        var settings = await _onboarding.GetSettingsAsync(found.OrganizationId, ct);
        if (string.IsNullOrWhiteSpace(settings?.WebhookSecret) || !WebhookSigning.IsCallbackToken(settings.WebhookSecret, id, token))
            return refused;

        var report = request.Event?.Trim().ToLowerInvariant() ?? "";
        if (!Reports.TryGetValue(report, out var reached))
            return new Error("Onboarding.Event", $"The event is one of {string.Join(", ", Reports.Keys)}.");
        var at = request.OccurredAtUtc is DateTime when && when > DateTime.UtcNow.AddDays(-30) && when < DateTime.UtcNow.AddMinutes(5)
            ? DateTime.SpecifyKind(when, DateTimeKind.Utc)
            : DateTime.UtcNow;
        var detail = Clip(Blank(request.Message), 300);

        // Two reports can land together (the account, then the email a moment later):
        // the one that loses the race reads the handoff again and applies itself on top.
        // A report the receiver sends again (its own retry) is taken, and changes nothing.
        var onboarding = found;
        var firstTime = false;
        for (var attempt = 0; ; attempt++)
        {
            var repeated = Since(onboarding, "queued", "resent").Any(s => s.Key == report);
            firstTime = !repeated;
            if (!repeated)
            {
                AddStep(onboarding, report, at, detail);
                onboarding.Status = Next(onboarding.Status, reached);
                if (Blank(request.AccountId) is { } account) onboarding.ExternalAccountId = Clip(account, 200);
                onboarding.UpdatedAtUtc = DateTime.UtcNow;
            }
            try
            {
                if (!repeated) await _onboarding.SaveAsync(ct);
                break;
            }
            catch (ConcurrencyConflictException) when (attempt < 3)
            {
                onboarding = await _onboarding.GetAsync(id, found.OrganizationId, ct);
                if (onboarding == null) return refused;
            }
        }

        if (firstTime)
        {
            var note = report switch
            {
                "email_sent" => ("Welcome & portal: welcome email sent", $"The portal sent {onboarding.Name} their welcome email, with the link to set a password."),
                "password_set" => ("Welcome & portal: they're in", $"{onboarding.Name} set their password and can sign in to the portal."),
                "email_failed" or "failed" => ("Welcome & portal: the portal reported a problem", detail ?? "The portal could not finish the welcome."),
                _ => ((string, string)?)null,
            };
            if (note is var (subject, body)) await NoteAsync(onboarding, subject, body, ct);
        }
        return new OnboardingReportReceipt(onboarding.Id, onboarding.Status);
    }

    // ── Pieces ────────────────────────────────────────────────────────────────

    private sealed record Person(DealDto Deal, Guid? ContactId, string Name, string Email, string? Phone, string? Company);

    /// <summary>The deal, if this person may see it, and the contact on it.</summary>
    private async Task<Result<Person>> PersonAsync(Guid dealId, Guid organizationId, Guid userId, CancellationToken ct)
    {
        var deal = await _deals.GetByIdAsync(dealId, userId, organizationId, ct);
        if (deal.IsFailure) return deal.Error;
        if (deal.Value.ContactId is not Guid contactId)
            return new Error("Onboarding.NoContact", "The deal has no contact to welcome. Add one to the deal first.");
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return new Error("Onboarding.NoContact", "The deal's contact was not found.");
        return new Person(deal.Value, contact.Id, contact.Name.Trim(), contact.Email.Trim(), contact.Phone, deal.Value.CompanyName);
    }

    private async Task<OnboardingTemplate.Values> ValuesAsync(
        Guid organizationId, Guid userId, string name, string email, string? company, string? dealName,
        string? welcomeMessage, DateTime? startsOn, bool undetermined, string language, CancellationToken ct)
    {
        var sender = await _users.GetByIdAsync(userId, ct);
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        return new OnboardingTemplate.Values(name, email, company, dealName, Blank(welcomeMessage),
            startsOn?.Date, startsOn == null, language, sender?.Name, workspace?.Name);
    }

    /// <summary>
    /// The person among the club's members here: the one with their email if there is one,
    /// with the start brought up to date, else a new pending member.
    /// </summary>
    private async Task<Guid?> JoinClubAsync(DealOnboarding onboarding, Person person, Guid userId, CancellationToken ct)
    {
        var member = await _onboarding.ClubMemberByEmailAsync(onboarding.OrganizationId, onboarding.Email, ct);
        if (member == null)
        {
            // Made the way the club makes every member (ClubService): at its first tier on
            // sale, with the house account the money ledger keeps for each member, and links
            // to the contact and lead checked to be this workspace's. No dues and no fee: the
            // club starts those when the membership does.
            var space = onboarding.Name.IndexOf(' ');
            var first = space < 0 ? onboarding.Name : onboarding.Name[..space];
            var last = space < 0 ? "" : onboarding.Name[(space + 1)..].Trim();
            var joined = onboarding.StartsOnUtc ?? DateTime.UtcNow.Date;
            MemberWriteRequest Request(Guid? leadId) => new()
            {
                FirstName = Clip(first, 100),
                // A member has a last name; someone known by one name has it twice rather than none.
                LastName = Clip(last.Length > 0 ? last : first, 100),
                Email = onboarding.Email,
                Phone = onboarding.Phone,
                Status = "Pending",
                JoinedAtUtc = joined,
                RenewsAtUtc = joined.AddYears(1),
                Notes = Clip($"Joined through the deal \"{person.Deal.Name}\".", 1000),
                ContactId = onboarding.ContactId,
                LeadId = leadId,
                StartsOnUtc = onboarding.StartsOnUtc,
                StartUndetermined = onboarding.StartUndetermined,
                // The language the portal welcomes them in ("fr" or "en"): the club's statements
                // and reminders to them follow it.
                PreferredLanguage = AppLanguages.Normalise(onboarding.Language),
            };
            var created = await _club.CreateMemberAsync(userId, onboarding.OrganizationId, Request(onboarding.LeadId), ct);
            // The lead came from the browser: one the club cannot link to is left off, not a reason to add nobody.
            if (created.IsFailure && onboarding.LeadId != null)
                created = await _club.CreateMemberAsync(userId, onboarding.OrganizationId, Request(null), ct);
            if (created.IsFailure)
            {
                // The handoff goes on without them: the portal welcome is what was asked for.
                _logger.LogWarning("Handoff {OnboardingId}: the club member could not be added: {Error}", onboarding.Id, created.Error.Description);
                return null;
            }
            member = await _onboarding.ClubMemberByEmailAsync(onboarding.OrganizationId, onboarding.Email, ct);
            if (member == null) return created.Value.Id;
        }
        else
        {
            member.StartsOnUtc = onboarding.StartsOnUtc;
            member.StartUndetermined = onboarding.StartUndetermined;
            member.ContactId ??= onboarding.ContactId;
            // A member the club already had keeps the language staff set; one with none takes the handoff's.
            member.PreferredLanguage ??= AppLanguages.Normalise(onboarding.Language);
        }
        member.DealId ??= onboarding.DealId;
        return member.Id;
    }

    /// <summary>Queue the handoff for the outbox, which posts it, signed, and retries until it is taken.</summary>
    private async Task<OutboundMessage?> QueueAsync(DealOnboarding onboarding, Person person, OnboardingSettings settings, OnboardingTemplate.Values values, CancellationToken ct)
    {
        var sender = Sender(settings, await _organizations.GetByIdAsync(onboarding.OrganizationId, ct));
        var space = onboarding.Name.IndexOf(' ');
        var payload = new
        {
            version = 1,
            onboardingId = onboarding.Id,
            deal = new { id = person.Deal.Id, name = person.Deal.Name, amount = person.Deal.Amount, currency = person.Deal.Currency, stage = person.Deal.DealStageName ?? person.Deal.Stage },
            contact = new
            {
                id = onboarding.ContactId,
                name = onboarding.Name,
                firstName = space < 0 ? onboarding.Name : onboarding.Name[..space],
                lastName = space < 0 ? "" : onboarding.Name[(space + 1)..].Trim(),
                email = onboarding.Email,
                phone = onboarding.Phone,
                company = person.Company,
            },
            membership = new
            {
                startsOn = onboarding.StartsOnUtc?.ToString("yyyy-MM-dd"),
                startUndetermined = onboarding.StartUndetermined,
                clubMemberId = onboarding.ClubMemberId,
            },
            language = onboarding.Language,
            welcomeMessage = onboarding.WelcomeMessage,
            email = onboarding.EmailHtml == null && onboarding.EmailSubject == null
                ? null
                : new { subject = onboarding.EmailSubject, html = onboarding.EmailHtml },
            callback = new
            {
                url = $"{_settings.ApiBaseUrl.TrimEnd('/')}/api/public/onboarding/{onboarding.Id:D}/events",
                token = WebhookSigning.CallbackToken(settings.WebhookSecret!, onboarding.Id),
            },
            sentBy = new { name = values.SenderName },
            // Who the email is from: the organization, by name, with its replies coming
            // back to it. The portal sends from this address only on a domain its mail
            // service may send for, and otherwise from its own, replying here.
            from = new { name = sender.Name, email = sender.Email, replyTo = sender.ReplyTo },
            workspace = new { id = onboarding.OrganizationId, name = values.Workspace },
        };
        var now = DateTime.UtcNow;
        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = onboarding.OrganizationId,
            Channel = OutboundChannels.Webhook,
            Source = OutboundSources.Onboarding,
            SourceId = onboarding.Id,
            Status = OutboundStatuses.Queued,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = onboarding.CreatedByUserId,
            EventName = HandoffEvent,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            DealId = onboarding.DealId,
            ContactId = onboarding.ContactId,
        };
        try
        {
            // Not the request's token: the handoff is saved, and a person who closes the
            // page now must not leave it without its message.
            await _outbox.AddAsync(message, CancellationToken.None);
            onboarding.OutboundMessageId = message.Id;
            await _onboarding.SaveAsync(CancellationToken.None);
            _signal.Notify();
            return message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The handoff stays, shown as not handed over, and Resend tries again.
            _logger.LogError(ex, "Handoff {OnboardingId} could not be queued", onboarding.Id);
            return null;
        }
    }

    private async Task NoteAsync(DealOnboarding onboarding, string subject, string body, CancellationToken ct)
    {
        try
        {
            var created = await _activities.CreateAsync(onboarding.CreatedByUserId, onboarding.OrganizationId, new CreateActivityRequest
            {
                Type = "system",
                Subject = subject,
                Body = body,
                DealId = onboarding.DealId,
                ContactId = onboarding.ContactId,
                IsAutomated = true,
            }, ct);
            if (created.IsFailure)
                _logger.LogWarning("Handoff {OnboardingId}: its timeline entry was refused: {Error}", onboarding.Id, created.Error.Description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The timeline is a record of it, not the handoff itself.
            _logger.LogWarning(ex, "Handoff {OnboardingId}: its timeline entry could not be written", onboarding.Id);
        }
    }

    internal static Dictionary<string, string> Headers(string eventName, Guid id, string secret, string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new Dictionary<string, string>
        {
            ["X-Cadence-Event"] = eventName,
            ["X-Cadence-Event-Id"] = id.ToString("D"),
            [WebhookSigning.TimestampHeader] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [WebhookSigning.SignatureHeader] = WebhookSigning.Sign(secret, timestamp, body),
        };
    }

    // ── Steps and status ──────────────────────────────────────────────────────

    private static readonly string[] Order =
    {
        OnboardingStatuses.Queued, OnboardingStatuses.Delivered, OnboardingStatuses.AccountReady, OnboardingStatuses.Welcomed, OnboardingStatuses.Active,
    };

    /// <summary>
    /// Where a report leaves the handoff: forward, never back (an account report arriving
    /// after the email's leaves it welcomed), and a failure only until they are in.
    /// Any progress after a failure clears it, as after a resend that worked.
    /// </summary>
    internal static string Next(string current, string reached)
    {
        if (reached == OnboardingStatuses.Failed)
            return current == OnboardingStatuses.Active ? current : OnboardingStatuses.Failed;
        if (current == OnboardingStatuses.Failed) return reached;
        return Array.IndexOf(Order, reached) > Array.IndexOf(Order, current) ? reached : current;
    }

    private static List<OnboardingStepDto> Steps(DealOnboarding onboarding)
    {
        try
        {
            return JsonSerializer.Deserialize<List<OnboardingStepDto>>(onboarding.StepsJson, Json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>The steps since the latest of <paramref name="marks"/>: this round of handing over.</summary>
    private static List<OnboardingStepDto> Since(DealOnboarding onboarding, params string[] marks)
    {
        var steps = Steps(onboarding);
        var from = steps.FindLastIndex(s => marks.Contains(s.Key));
        return from < 0 ? steps : steps.Skip(from + 1).ToList();
    }

    private static void AddStep(DealOnboarding onboarding, string key, DateTime at, string? detail)
    {
        var steps = Steps(onboarding);
        steps.Add(new OnboardingStepDto(key, at, detail));
        // The first step says when it began; past the cap, the oldest after it go.
        if (steps.Count > MaxSteps) steps.RemoveRange(1, steps.Count - MaxSteps);
        onboarding.StepsJson = JsonSerializer.Serialize(steps, Json);
    }

    private static IReadOnlyCollection<Guid> Ids(params DealOnboarding[] rows) =>
        rows.Where(o => o.OutboundMessageId != null).Select(o => o.OutboundMessageId!.Value).ToList();

    private async Task<IReadOnlyList<OnboardingDto>> ToDtosAsync(IReadOnlyList<DealOnboarding> rows, CancellationToken ct)
    {
        var messages = await _onboarding.MessagesAsync(Ids(rows.ToArray()), ct);
        return rows.Select(o => ToDto(o, o.OutboundMessageId is Guid id ? messages.GetValueOrDefault(id) : null)).ToList();
    }

    /// <summary>
    /// The handoff as it stands, with what the outbox knows folded in: taken by the
    /// receiver, or given up on, before the receiver has reported anything.
    /// </summary>
    internal static OnboardingDto ToDto(DealOnboarding o, OutboundMessage? message)
    {
        var steps = Steps(o);
        var status = o.Status;
        if (message?.Status == OutboundStatuses.Sent && message.SentAtUtc is DateTime sent)
        {
            // Shown in its place, after the latest send it belongs to.
            var round = steps.FindLastIndex(s => s.Key is "queued" or "resent");
            if (!steps.Skip(round + 1).Any(s => s.Key == "delivered"))
            {
                steps.Insert(round + 1, new OnboardingStepDto("delivered", sent, null));
            }
            if (status == OnboardingStatuses.Queued) status = OnboardingStatuses.Delivered;
        }
        else if (status == OnboardingStatuses.Queued && (message == null || message.Status is OutboundStatuses.Failed or OutboundStatuses.Cancelled))
        {
            status = OnboardingStatuses.Failed;
            steps.Add(new OnboardingStepDto("undeliverable", o.UpdatedAtUtc,
                message?.LastError ?? "It could not be queued for the portal."));
        }
        var delivery = message == null ? null : new OnboardingDeliveryDto(
            message.Status, message.Attempts, message.LastStatusCode, message.LastError, message.SentAtUtc,
            message.Status == OutboundStatuses.Queued ? message.NextAttemptAtUtc : null);
        var canResend = status != OnboardingStatuses.Active && message?.Status != OutboundStatuses.Queued;
        return new OnboardingDto(o.Id, o.DealId, o.LeadId, o.ContactId, o.ClubMemberId, o.Name, o.Email, o.Language, o.WelcomeMessage,
            o.StartsOnUtc, o.StartUndetermined, status, steps, delivery, o.ExternalAccountId, canResend, o.CreatedAtUtc, o.UpdatedAtUtc);
    }

    private sealed record SenderInfo(string? Name, string? Email, string? ReplyTo);

    /// <summary>Who the welcome is from: what the workspace set, or the organization's name and its own sending address.</summary>
    private static SenderInfo Sender(OnboardingSettings? s, Organization? workspace) => new(
        Blank(s?.SenderName) ?? Blank(workspace?.Name),
        Blank(s?.SenderEmail),
        Blank(s?.ReplyTo) ?? (Blank(workspace?.SendingAddress) is { } own && IsEmail(own) ? own : null));

    private static OnboardingSettingsDto ToDto(OnboardingSettings? s, Organization? workspace) => new(
        s?.WebhookUrl,
        !string.IsNullOrWhiteSpace(s?.WebhookSecret),
        string.IsNullOrWhiteSpace(s?.WebhookSecret) ? null : s!.WebhookSecret![^4..],
        s?.Enabled ?? true,
        s?.EmailSubject,
        s?.EmailHtml,
        s?.DefaultLanguage ?? "fr",
        s?.AddToClubMembers ?? true,
        NotSetUp(s) == null,
        s?.UpdatedAtUtc,
        OnboardingTemplate.CadenceFields,
        OnboardingTemplate.ReceiverFields,
        s?.SenderName,
        s?.SenderEmail,
        s?.ReplyTo,
        Blank(workspace?.Name),
        Blank(workspace?.SendingAddress) is { } own && IsEmail(own) ? own : null);

    // ── Rules ─────────────────────────────────────────────────────────────────

    private static Error? NotSetUp(OnboardingSettings? s) =>
        string.IsNullOrWhiteSpace(s?.WebhookUrl) || string.IsNullOrWhiteSpace(s.WebhookSecret)
            ? new Error("Onboarding.NotSetUp", "Set up Welcome & portal in Settings first: the portal's address and its signing secret.")
            : null;

    /// <summary>Why the address cannot be posted to, or null. Checked again on the address dialled.</summary>
    private static string? AddressProblem(string url) =>
        !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            ? "The portal's address must be an https URL."
            : !PublicAddress.LooksPublic(uri)
                ? "The portal's address must be on the public internet."
                : null;

    private static Error? Problem(string? message, string? subject, string? html, DateTime? startsOn)
    {
        if ((message?.Length ?? 0) > MaxMessage) return new Error("Onboarding.TooLong", $"The welcome message can be at most {MaxMessage} characters.");
        if ((subject?.Trim().Length ?? 0) > MaxSubject) return new Error("Onboarding.TooLong", $"The subject can be at most {MaxSubject} characters.");
        if ((html?.Length ?? 0) > MaxHtml) return new Error("Onboarding.TooLong", $"The email's HTML can be at most {MaxHtml / 1000} KB.");
        if (startsOn is DateTime d && (d.Year < 2000 || d.Year > 2100)) return new Error("Onboarding.StartDate", "That start date is not a real one.");
        return null;
    }

    private static List<string> UnknownPlaceholders(string? subject, string? html)
    {
        var sample = new OnboardingTemplate.Values("A B", "a@b.c", null, null, null, null, true, "en", null, null);
        OnboardingTemplate.Fill(subject, sample, html: false, out var a);
        OnboardingTemplate.Fill(html, sample, html: true, out var b);
        return a.Concat(b).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Error Unknown(IReadOnlyList<string> unknown) => new("Onboarding.Unfilled",
        $"Nothing fills {string.Join(", ", unknown)}. Use one of the listed placeholders, or give it a fallback, like {{{{name|there}}}}.");

    private static string? Language(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "fr" => "fr",
        "en" => "en",
        _ => null,
    };

    private static string DefaultSubject(string language, string? workspace) => language == "fr"
        ? $"Bienvenue{(string.IsNullOrWhiteSpace(workspace) ? "" : " chez " + workspace)}, {{{{firstName}}}}"
        : $"Welcome{(string.IsNullOrWhiteSpace(workspace) ? "" : " to " + workspace)}, {{{{firstName}}}}";

    /// <summary>
    /// What the receiver's own design will say, roughly: the preview when the workspace
    /// has no HTML of its own.
    /// </summary>
    private static string StandIn(OnboardingTemplate.Values v, string language)
    {
        var fr = language == "fr";
        static string E(string? s) => OnboardingTemplate.Html(s);
        var first = v.Name.Split(' ', 2)[0];
        var message = string.IsNullOrWhiteSpace(v.WelcomeMessage) ? "" :
            $"<p style=\"margin:0 0 16px;padding:14px 16px;border-left:3px solid #f97316;background:#fff7ed;border-radius:8px;color:#334155\">{E(v.WelcomeMessage).Replace("\n", "<br>")}</p>";
        return $"""
            <div style="font-family:Georgia,serif;max-width:560px;margin:0 auto;padding:32px 28px;background:#ffffff;color:#0f172a">
              <p style="margin:0 0 6px;font:600 12px/1.4 system-ui,sans-serif;letter-spacing:.12em;text-transform:uppercase;color:#94a3b8">{E(v.Workspace)}</p>
              <h1 style="margin:0 0 18px;font-size:26px;font-weight:normal">{(fr ? "Bienvenue" : "Welcome")}, {E(first)}</h1>
              {message}
              <p style="margin:0 0 16px;font:15px/1.6 system-ui,sans-serif;color:#334155">{(fr ? "Votre espace membre est prêt. Choisissez votre mot de passe pour vous connecter avec" : "Your member area is ready. Choose your password to sign in with")} <strong>{E(v.Email)}</strong>.</p>
              <p style="margin:0 0 22px;font:15px/1.6 system-ui,sans-serif;color:#334155">{(fr ? "Début de votre adhésion" : "Your membership starts")}: <strong>{E(OnboardingTemplate.StartDate(v.StartsOn, v.StartUndetermined, language))}</strong></p>
              <p style="margin:0"><a href="#set-password" style="display:inline-block;padding:12px 22px;border-radius:999px;background:#0f172a;color:#ffffff;text-decoration:none;font:600 14px system-ui,sans-serif">{(fr ? "Choisir mon mot de passe" : "Set my password")}</a></p>
            </div>
            """;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>One plain address: no name, no list, no line breaks, a dot in the domain.</summary>
    private static bool IsEmail(string value)
    {
        var v = value.Trim();
        if (v.Length > 256 || v.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is ',' or ';' or '<' or '>')) return false;
        var at = v.IndexOf('@');
        return at > 0 && at == v.LastIndexOf('@') && v.IndexOf('.', at) > at + 1 && !v.EndsWith('.');
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? Blank(v.GetString()) : null;

    private static string? Clip(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}
