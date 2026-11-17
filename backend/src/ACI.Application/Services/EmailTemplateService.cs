using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>A prepared email in one language.</summary>
public sealed record EmailTemplateVersionDto(string Language, string Subject, string Body);

/// <summary>A prepared email, with every language it is written in.</summary>
/// <param name="Starter">One of the starter set the workspace was given (and may have changed).</param>
public sealed record EmailTemplateDto(
    Guid Id, string Name, string? Category, string? Description, bool Starter,
    IReadOnlyList<EmailTemplateVersionDto> Versions, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>The workspace's prepared emails and what the composer needs with them.</summary>
/// <param name="Languages">The languages a prepared email can be written in, in the order offered.</param>
/// <param name="DefaultLanguage">The language the composer starts in when the person has not chosen one.</param>
/// <param name="Placeholders">The placeholders filled for each recipient.</param>
public sealed record EmailTemplateListDto(
    IReadOnlyList<EmailTemplateDto> Templates, IReadOnlyList<string> Languages, string DefaultLanguage, IReadOnlyList<string> Placeholders);

public sealed record SaveEmailTemplateRequest(string? Name, string? Category, string? Description, IReadOnlyList<EmailTemplateVersionDto>? Versions);

public interface IEmailTemplateService
{
    /// <summary>The workspace's prepared emails, giving it the starter set the first time.</summary>
    Task<EmailTemplateListDto> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<EmailTemplateDto>> CreateAsync(Guid organizationId, Guid userId, SaveEmailTemplateRequest request, CancellationToken ct = default);
    Task<Result<EmailTemplateDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, SaveEmailTemplateRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// The workspace's prepared emails: the composer offers them, in French or English, and
/// fills the person's details in when it sends.
/// </summary>
/// <remarks>
/// A placeholder must be one the outbox fills ({{firstName}} and the rest): one it does not
/// know is refused here, where the wording is written, rather than when someone sends it.
/// </remarks>
public sealed partial class EmailTemplateService : IEmailTemplateService
{
    public const int MaxName = 120;
    public const int MaxCategory = 60;
    public const int MaxDescription = 300;
    public const int MaxSubject = 300;
    public const int MaxBody = 20_000;

    private readonly IEmailTemplateRepository _templates;
    private readonly IOrganizationRepository _organizations;
    private readonly IWorkspaceModules _modules;
    private readonly IOnboardingRepository? _onboarding;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IEmailTemplateRepository templates,
        IOrganizationRepository organizations,
        IWorkspaceModules modules,
        ILogger<EmailTemplateService> logger,
        IOnboardingRepository? onboarding = null)
    {
        _templates = templates;
        _organizations = organizations;
        _modules = modules;
        _logger = logger;
        _onboarding = onboarding;
    }

    [GeneratedRegex(@"\{\{\s*(?<key>[^{}|]*?)\s*(?:\|[^{}]*)?\}\}")]
    private static partial Regex Placeholder();

    public async Task<EmailTemplateListDto> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _organizations.GetByIdAsync(organizationId, ct);
        if (org is { EmailTemplatesSeededAtUtc: null })
        {
            var club = await _modules.IsOnAsync(organizationId, WorkspaceModules.Club, ct);
            var now = DateTime.UtcNow;
            var starters = StarterEmailTemplates.All
                .Where(s => club || !s.ClubOnly)
                .Select(s => new EmailTemplate
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Name = s.Name,
                    Category = s.Category,
                    Description = s.Description,
                    StarterKey = s.Key,
                    CreatedAtUtc = now,
                    Versions = s.Versions.Select(v => new EmailTemplateVersion
                    {
                        Id = Guid.NewGuid(), Language = v.Language, Subject = v.Subject, Body = v.Body,
                    }).ToList(),
                })
                .ToList();
            if (await _templates.SeedAsync(organizationId, starters, now, ct))
                _logger.LogInformation("Organization {OrganizationId} was given {Count} starter prepared emails", organizationId, starters.Count);
        }

        var rows = await _templates.ListAsync(organizationId, ct);
        return new EmailTemplateListDto(
            rows.Select(Dto).ToList(),
            EmailTemplateLanguages.Supported,
            await DefaultLanguageAsync(organizationId, org?.Locale, ct),
            MergeFields.Known);
    }

    public async Task<Result<EmailTemplateDto>> CreateAsync(Guid organizationId, Guid userId, SaveEmailTemplateRequest request, CancellationToken ct = default)
    {
        var checkedRequest = await CheckAsync(organizationId, null, request, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (name, category, description, versions) = checkedRequest.Value;
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Category = category,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            Versions = versions.ToList(),
        };
        foreach (var v in template.Versions) v.EmailTemplateId = template.Id;
        await _templates.AddAsync(template, ct);
        return Dto(template);
    }

    public async Task<Result<EmailTemplateDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, SaveEmailTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(id, organizationId, ct);
        if (template == null) return NotFound;
        var checkedRequest = await CheckAsync(organizationId, id, request, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (name, category, description, versions) = checkedRequest.Value;
        template.Name = name;
        template.Category = category;
        template.Description = description;
        template.UpdatedAtUtc = DateTime.UtcNow;
        template.UpdatedByUserId = userId;
        await _templates.SaveAsync(template, versions, ct);
        return Dto(template);
    }

    public async Task<Result> DeleteAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        await _templates.DeleteAsync(id, organizationId, ct) ? Result.Success() : NotFound;

    private static readonly Error NotFound = new("EmailTemplate.NotFound", "That prepared email is not there any more.");

    private async Task<Result<(string Name, string? Category, string? Description, IReadOnlyList<EmailTemplateVersion> Versions)>> CheckAsync(
        Guid organizationId, Guid? id, SaveEmailTemplateRequest request, CancellationToken ct)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return new Error("EmailTemplate.Name", "Give the prepared email a name.");
        if (name.Length > MaxName) return new Error("EmailTemplate.Name", $"A name can be at most {MaxName} characters.");
        var category = Blank(request.Category);
        if (category?.Length > MaxCategory) return new Error("EmailTemplate.Category", $"A category can be at most {MaxCategory} characters.");
        var description = Blank(request.Description);
        if (description?.Length > MaxDescription) return new Error("EmailTemplate.Description", $"A description can be at most {MaxDescription} characters.");

        var versions = new List<EmailTemplateVersion>();
        foreach (var v in request.Versions ?? [])
        {
            var language = (v.Language ?? "").Trim().ToLowerInvariant();
            if (!EmailTemplateLanguages.IsSupported(language))
                return new Error("EmailTemplate.Language", $"\"{v.Language}\" is not a language a prepared email can be in. It can be {string.Join(" or ", EmailTemplateLanguages.Supported.Select(Label))}.");
            if (versions.Any(x => x.Language == language))
                return new Error("EmailTemplate.Language", $"The {Label(language)} version is there twice.");
            var subject = (v.Subject ?? "").Trim();
            var body = (v.Body ?? "").Replace("\r\n", "\n").TrimEnd();
            // A language left blank is no version at all.
            if (subject.Length == 0 && body.Trim().Length == 0) continue;
            if (subject.Length == 0) return new Error("EmailTemplate.Subject", $"The {Label(language)} version needs a subject.");
            if (body.Trim().Length == 0) return new Error("EmailTemplate.Body", $"The {Label(language)} version needs some text.");
            if (subject.Length > MaxSubject) return new Error("EmailTemplate.Subject", $"A subject can be at most {MaxSubject} characters.");
            if (body.Length > MaxBody) return new Error("EmailTemplate.Body", $"The text can be at most {MaxBody:N0} characters.");
            var unknown = UnknownPlaceholders(subject + "\n" + body);
            if (unknown.Count > 0)
                return new Error("EmailTemplate.Placeholder", $"The {Label(language)} version has {string.Join(", ", unknown)}, which is not a placeholder Cadence fills. "
                    + $"It can use {string.Join(", ", MergeFields.Known.Select(k => "{{" + k + "}}"))}, or a blank to fill in before sending, such as [[date]].");
            versions.Add(new EmailTemplateVersion { Id = Guid.NewGuid(), Language = language, Subject = subject, Body = body });
        }
        if (versions.Count == 0) return new Error("EmailTemplate.Versions", "Write the prepared email in at least one language.");
        if (await _templates.NameTakenAsync(organizationId, name, id, ct))
            return new Error("EmailTemplate.NameTaken", $"There is already a prepared email named \"{name}\".");
        return (name, category, description, (IReadOnlyList<EmailTemplateVersion>)versions);
    }

    /// <summary>The placeholders in <paramref name="text"/> the outbox does not fill, as written, once each.</summary>
    public static IReadOnlyList<string> UnknownPlaceholders(string text) =>
        Placeholder().Matches(text)
            .Where(m => !MergeFields.Known.Contains(m.Groups["key"].Value.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The workspace's welcome language when it has one, else its own locale's when that is a
    /// language prepared emails can be in, else English.
    /// </summary>
    private async Task<string> DefaultLanguageAsync(Guid organizationId, string? locale, CancellationToken ct)
    {
        if (_onboarding != null && (await _onboarding.GetSettingsAsync(organizationId, ct))?.DefaultLanguage is { } welcome
            && EmailTemplateLanguages.IsSupported(welcome))
            return welcome.Trim().ToLowerInvariant();
        var fromLocale = AppLanguages.Normalise(locale);
        return EmailTemplateLanguages.IsSupported(fromLocale) ? fromLocale! : EmailTemplateLanguages.English;
    }

    private static string Label(string language) => language switch
    {
        EmailTemplateLanguages.French => "French",
        EmailTemplateLanguages.English => "English",
        _ => language,
    };

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static EmailTemplateDto Dto(EmailTemplate t) => new(
        t.Id, t.Name, t.Category, t.Description, t.StarterKey != null,
        t.Versions
            .OrderBy(v => EmailTemplateLanguages.Supported.ToList().IndexOf(v.Language) is var i && i < 0 ? int.MaxValue : i)
            .Select(v => new EmailTemplateVersionDto(v.Language, v.Subject, v.Body))
            .ToList(),
        t.CreatedAtUtc, t.UpdatedAtUtc);
}
