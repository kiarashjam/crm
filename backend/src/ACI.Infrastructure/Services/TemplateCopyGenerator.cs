using ACI.Application.Interfaces;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Simple template-based fallback for Intelligent Sales Writer when AI is not configured.
/// Provides basic templates with placeholder replacement. For AI-powered personalization,
/// configure OpenAI API key in settings.
/// </summary>
public sealed class TemplateCopyGenerator : ICopyGenerator
{
    private static readonly Dictionary<string, string> BasicTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sales-email"] = """
            Hi [First Name],

            I hope this email finds you well. I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

            We've helped companies similar to yours:
            • Increase productivity by 40%
            • Reduce operational costs by 25%
            • Streamline workflows and save time

            Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

            Looking forward to connecting.

            Best regards,
            [Your Name]
            """,
        ["follow-up"] = """
            Hi [First Name],

            I wanted to follow up on our previous conversation. Please let me know if you have any questions or if you'd like to schedule a call to discuss next steps.

            Best regards,
            [Your Name]
            """,
        ["crm-note"] = """
            Note: [First Name] - [Company Name]

            Summary of our conversation:
            • [Key point 1]
            • [Key point 2]

            Next steps:
            • [Action item]
            """,
        ["deal-message"] = """
            Hi [First Name],

            I wanted to provide an update on the [Company Name] deal. Current status: [stage]. Next steps: [action items].

            Best regards,
            [Your Name]
            """,
        ["workflow-message"] = """
            Hi [First Name],

            Following up on our conversation. Please let me know if you need any additional information.

            Best regards,
            [Your Name]
            """,
    };

    private const string DefaultTemplate = """
        Hi [First Name],

        I hope this email finds you well. I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

        Would you be available for a 15-minute call next week?

        Best regards,
        [Your Name]
        """;

    private const string AINote = "\n\n[Note: Configure OpenAI API key for AI-powered personalization and advanced copy generation.]";

    public Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var template = BasicTemplates.TryGetValue(copyTypeId, out var t) ? t : DefaultTemplate;
        var result = ReplacePlaceholders(template, brandName, null);
        
        if (!string.IsNullOrWhiteSpace(context))
        {
            var ctx = context.Trim();
            if (ctx.Length > 300) ctx = ctx[..300] + "...";
            result = result.TrimEnd() + $"\n\n---\nContext: {ctx}\n---";
        }

        return Task.FromResult(result.Trim() + AINote);
    }

    public Task<string> RewriteAsync(
        string originalCopy,
        string adjustment,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Simple fallback - return original with note
        return Task.FromResult(originalCopy + AINote);
    }

    public Task<GenerateCopyResult> GenerateWithRecipientAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        RecipientContext? recipient,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var template = BasicTemplates.TryGetValue(copyTypeId, out var t) ? t : DefaultTemplate;
        var body = ReplacePlaceholders(template, brandName, recipient);
        
        if (!string.IsNullOrWhiteSpace(context))
        {
            var ctx = context.Trim();
            if (ctx.Length > 300) ctx = ctx[..300] + "...";
            body = body.TrimEnd() + $"\n\n---\nContext: {ctx}\n---";
        }

        body = body.Trim() + AINote;

        string? subject = null;
        if (copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            var recipientName = recipient?.Name?.Split(' ')[0] ?? "";
            var recipientCompany = recipient?.Company ?? brandName ?? "[Company]";
            subject = $"Meeting request: {recipientCompany} partnership";
        }

        return Task.FromResult(new GenerateCopyResult(body, subject));
    }

    public Task<GenerateCopyResult> GenerateInLanguageAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        RecipientContext? recipient,
        string targetLanguage,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        var result = GenerateWithRecipientAsync(copyTypeId, goal, context, length, brandName, brandTone, recipient, ct).Result;
        var languageNote = $"\n\n[Note: Multi-language generation requires AI to be configured. Please translate this copy to {targetLanguage}.]";
        
        return Task.FromResult(new GenerateCopyResult(result.Body + languageNote, result.Subject));
    }

    private static string ReplacePlaceholders(string template, string? brandName, RecipientContext? recipient)
    {
        var result = template;

        if (!string.IsNullOrWhiteSpace(brandName))
        {
            result = result.Replace("[Company Name]", brandName, StringComparison.OrdinalIgnoreCase);
        }

        if (recipient != null)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Name))
            {
                var firstName = recipient.Name.Split(' ')[0];
                result = result.Replace("[First Name]", firstName, StringComparison.OrdinalIgnoreCase);
                result = result.Replace("[Contact Name]", recipient.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(recipient.Company))
            {
                result = result.Replace("[Company Name]", recipient.Company, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(recipient.Title))
            {
                result = result.Replace("[Title]", recipient.Title, StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
}
