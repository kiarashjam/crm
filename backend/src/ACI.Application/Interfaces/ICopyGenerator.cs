namespace ACI.Application.Interfaces;

/// <summary>
/// Intelligent Sales Writer: generates content based on type, goal, and context.
/// Implementations: TemplateCopyGenerator (fallback), AICopyGenerator (OpenAI/Azure).
/// </summary>
public interface ICopyGenerator
{
    /// <summary>
    /// Generate copy based on parameters.
    /// </summary>
    Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        CancellationToken ct = default);

    /// <summary>
    /// Rewrite existing copy with a different tone/style.
    /// </summary>
    Task<string> RewriteAsync(
        string originalCopy,
        string adjustment,
        CancellationToken ct = default);

    /// <summary>
    /// Generate copy with recipient context for personalization.
    /// </summary>
    Task<GenerateCopyResult> GenerateWithRecipientAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        RecipientContext? recipient,
        CancellationToken ct = default);

    /// <summary>
    /// Generate copy in a specific language.
    /// </summary>
    Task<GenerateCopyResult> GenerateInLanguageAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        RecipientContext? recipient,
        string targetLanguage,
        CancellationToken ct = default);
}

/// <summary>
/// Result of Intelligent Sales Writer including optional subject line.
/// </summary>
public record GenerateCopyResult(
    string Body,
    string? Subject = null);

/// <summary>
/// Context about the recipient for personalization.
/// </summary>
public record RecipientContext(
    string? Name,
    string? Email,
    string? Company,
    string? Title,
    string? Type, // "lead", "contact", "deal"
    string? LastActivity,
    string? DealStage,
    string? DealValue);
