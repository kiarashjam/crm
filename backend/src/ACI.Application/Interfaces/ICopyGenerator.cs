namespace ACI.Application.Interfaces;

/// <summary>
/// Generates AI copy based on type, goal, and context (implemented in Infrastructure).
/// </summary>
public interface ICopyGenerator
{
    Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        CancellationToken ct = default);
}
