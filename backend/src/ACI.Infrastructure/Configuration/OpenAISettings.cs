namespace ACI.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for OpenAI integration.
/// </summary>
public class OpenAISettings
{
    public const string SectionName = "OpenAI";
    
    /// <summary>
    /// OpenAI API key. When empty, falls back to template-based generation.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Model to use for generation. Defaults to gpt-4o-mini (cost-effective).
    /// Options: gpt-4o-mini, gpt-4o, gpt-4-turbo, gpt-3.5-turbo
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// Maximum tokens for generation response.
    /// </summary>
    public int MaxTokens { get; set; } = 500;
    
    /// <summary>
    /// Temperature for generation (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public float Temperature { get; set; } = 0.7f;
    
    /// <summary>
    /// Check if OpenAI is configured with a valid API key.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && ApiKey.StartsWith("sk-");
}
