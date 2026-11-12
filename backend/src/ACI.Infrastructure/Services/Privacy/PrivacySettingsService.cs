using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Configuration;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>A workspace's privacy settings and what the public privacy page says (see <see cref="IPrivacySettings"/>).</summary>
public sealed class PrivacySettingsService : IPrivacySettings
{
    /// <summary>The longest periods a workspace may set; long enough for any rule, short enough to be a rule.</summary>
    private const int MaxMonths = 120;
    private const int MaxYears = 30;

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly EmailSettings _email;
    private readonly OpenAISettings _openAi;

    public PrivacySettingsService(AppDbContext db, IConfiguration configuration, IOptions<EmailSettings> email, IOptions<OpenAISettings> openAi)
    {
        _db = db;
        _configuration = configuration;
        _email = email.Value;
        _openAi = openAi.Value;
    }

    public async Task<PrivacySettingsDto> GetAsync(Guid organizationId, CancellationToken ct = default) =>
        ToDto(await _db.PrivacySettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct));

    public async Task<Result<PrivacySettingsDto>> SaveAsync(Guid organizationId, Guid userId, PrivacySettingsRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var mode = (request.MarketingConsent ?? MarketingConsentModes.Optional).Trim().ToLowerInvariant();
        if (!MarketingConsentModes.IsMode(mode)) return DomainErrors.Privacy.ConsentModeInvalid;
        string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var name = Text(request.ControllerName);
        var address = Text(request.ControllerAddress);
        var email = Text(request.ControllerEmail);
        var dpo = Text(request.DpoContact);
        var consentText = Text(request.MarketingConsentText);
        if (name is { Length: > 200 }) return DomainErrors.Privacy.TooLong("The name", 200);
        if (address is { Length: > 500 }) return DomainErrors.Privacy.TooLong("The address", 500);
        if (dpo is { Length: > 300 }) return DomainErrors.Privacy.TooLong("The data protection contact", 300);
        if (consentText is { Length: > 500 }) return DomainErrors.Privacy.TooLong("The checkbox's words", 500);
        if (email != null && (email.Length > 256 || !ValidationHelper.IsValidEmail(email))) return DomainErrors.Privacy.ControllerEmailInvalid;
        if (!InRange(request.LeadRetentionMonths, MaxMonths) || !InRange(request.MemberRetentionYears, MaxYears)
            || !InRange(request.MailRetentionMonths, MaxMonths) || !InRange(request.DetailRetentionMonths, MaxMonths))
            return DomainErrors.Privacy.RetentionOutOfRange;

        var row = await _db.PrivacySettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (row != null && expectedVersion != null && row.Version != expectedVersion) return DomainErrors.Privacy.Changed;
        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new PrivacySettings { Id = Guid.NewGuid(), OrganizationId = organizationId, CreatedAtUtc = now };
            _db.PrivacySettings.Add(row);
        }
        row.ControllerName = name;
        row.ControllerAddress = address;
        row.ControllerEmail = email;
        row.DpoContact = dpo;
        row.MarketingConsent = mode;
        row.MarketingConsentText = consentText;
        row.LeadRetentionMonths = request.LeadRetentionMonths;
        row.MemberRetentionYears = request.MemberRetentionYears;
        row.MailRetentionMonths = request.MailRetentionMonths;
        row.DetailRetentionMonths = request.DetailRetentionMonths;
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two first saves at once (one settings row per workspace), or a save over a newer one.
            _db.ChangeTracker.Clear();
            return DomainErrors.Privacy.Changed;
        }
        return Result.Success(ToDto(row));
    }

    public async Task<FormConsentDto> FormConsentAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await _db.PrivacySettings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        return new FormConsentDto(row?.MarketingConsent ?? MarketingConsentModes.Optional, row?.MarketingConsentText ?? FormConsentDefaults.Text);
    }

    public async Task<PrivacyNoticeDto> NoticeAsync(string? formToken, CancellationToken ct = default)
    {
        var section = _configuration.GetSection("Privacy");
        string? Config(string key) => string.IsNullOrWhiteSpace(section[key]) ? null : section[key]!.Trim();
        var operatorParty = new PrivacyPartyDto(Config("OperatorName"), Config("OperatorAddress"), Config("OperatorEmail"), Config("DpoContact"));
        var hosting = Config("Hosting") ?? "Microsoft Azure (App Service and Azure SQL Database), West Europe region (the Netherlands)";
        var sessionHours = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) && minutes > 0 ? Math.Max(1, minutes / 60) : 24;

        // The workspace a public form belongs to, found by the form's token, across workspaces:
        // the page is anonymous, and the token is what the form's own page already shows.
        var organizationId = await WorkspaceOfFormAsync(formToken, ct);
        PrivacySettings? settings = null;
        string? workspaceName = null;
        if (organizationId is Guid org)
        {
            settings = await _db.PrivacySettings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == org, ct);
            workspaceName = await _db.Organizations.AsNoTracking().Where(o => o.Id == org).Select(o => o.Name).FirstOrDefaultAsync(ct);
        }
        var controller = organizationId == null
            ? operatorParty
            : new PrivacyPartyDto(settings?.ControllerName ?? workspaceName, settings?.ControllerAddress, settings?.ControllerEmail, settings?.DpoContact);

        var processors = new List<PrivacyProcessorDto>
        {
            new("Microsoft (Azure)", "Hosting the app and its database, where everything on this page is kept.", hosting, null),
        };
        var mailHost = (_email.SmtpHost ?? "").Trim();
        if (mailHost.Contains("sendgrid", StringComparison.OrdinalIgnoreCase))
            processors.Add(new("Twilio SendGrid", "Sending the email the club sends, and reporting whether it arrived, was opened or bounced.", "United States",
                "Swiss-U.S. and EU-U.S. Data Privacy Framework, and standard contractual clauses"));
        else if (mailHost.Length > 0)
            processors.Add(new($"The email relay at {mailHost}", "Sending the email the club sends.", "As its operator says", null));
        if (_openAi.IsConfigured)
            processors.Add(new("OpenAI", "Drafting the text of an email when staff ask the AI writer: the recipient's name, company, job title, deal stage and last activity go with the request; their email address does not.",
                "United States", "Standard contractual clauses in OpenAI's data processing addendum"));
        if (organizationId is Guid workspace)
        {
            if (await _db.ClubPaymentSettings.IgnoreQueryFilters().AsNoTracking().AnyAsync(s => s.OrganizationId == workspace && s.StripeSecretKeyProtected != null, ct))
                processors.Add(new("Stripe", "Card, TWINT and SEPA direct debit payments, and cards members save for their dues.", "Ireland and the United States",
                    "Swiss-U.S. and EU-U.S. Data Privacy Framework, and standard contractual clauses"));
            if (await _db.ClubPosSettings.IgnoreQueryFilters().AsNoTracking().AnyAsync(s => s.OrganizationId == workspace && s.RefreshTokenProtected != null, ct))
                processors.Add(new("Lightspeed (Restaurant K-Series)", "The till: what members buy at the bar or restaurant is posted to their account.", "As Lightspeed's privacy policy says", null));
        }

        var retention = settings == null && organizationId == null
            ? null
            : new PrivacyRetentionDto(settings?.LeadRetentionMonths, settings?.MemberRetentionYears, settings?.MailRetentionMonths, settings?.DetailRetentionMonths);
        var consentText = organizationId == null ? null : settings?.MarketingConsentText ?? FormConsentDefaults.Text;
        return new PrivacyNoticeDto(operatorParty, controller, workspaceName, hosting, processors, retention, consentText, sessionHours);
    }

    /// <summary>The workspace of an open capture or application form with this token.</summary>
    private async Task<Guid?> WorkspaceOfFormAsync(string? token, CancellationToken ct)
    {
        var t = (token ?? "").Trim();
        if (t.Length == 0 || t.Length > 64) return null;
        var capture = await _db.LeadCaptureForms.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.PublicToken == t && f.IsActive).Select(f => f.OrganizationId).FirstOrDefaultAsync(ct);
        if (capture != null) return capture;
        return await _db.ClubApplicationForms.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.PublicToken == t && f.IsActive).Select(f => f.OrganizationId).FirstOrDefaultAsync(ct);
    }

    private static bool InRange(int? value, int max) => value == null || (value >= 1 && value <= max);

    private static PrivacySettingsDto ToDto(PrivacySettings? s) => new(
        s?.ControllerName, s?.ControllerAddress, s?.ControllerEmail, s?.DpoContact,
        s?.MarketingConsent ?? MarketingConsentModes.Optional,
        s?.MarketingConsentText ?? FormConsentDefaults.Text,
        s?.MarketingConsentText == null,
        s?.LeadRetentionMonths, s?.MemberRetentionYears, s?.MailRetentionMonths, s?.DetailRetentionMonths,
        s?.Version, s?.UpdatedAtUtc, s?.UpdatedByName);
}
