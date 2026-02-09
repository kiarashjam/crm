using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services;

/// <summary>
/// AI-powered Intelligent Sales Writer using OpenAI's GPT models.
/// </summary>
public sealed class OpenAICopyGenerator : ICopyGenerator
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAICopyGenerator> _logger;
    private readonly TemplateCopyGenerator _fallback;
    
    private const string OpenAIApiUrl = "https://api.openai.com/v1/chat/completions";
    
    public OpenAICopyGenerator(
        HttpClient httpClient,
        IOptions<OpenAISettings> settings,
        ILogger<OpenAICopyGenerator> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _fallback = new TemplateCopyGenerator();
        
        // Configure HTTP client
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }
    
    public async Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildGenerationPrompt(copyTypeId, goal, context, length, brandName, brandTone);
            var result = await CallOpenAIAsync(prompt, GetMaxTokensForLength(length), ct);
            return result ?? await _fallback.GenerateAsync(copyTypeId, goal, context, length, brandName, brandTone, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI generation failed, falling back to templates");
            return await _fallback.GenerateAsync(copyTypeId, goal, context, length, brandName, brandTone, ct);
        }
    }
    
    public async Task<string> RewriteAsync(
        string originalCopy,
        string adjustment,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildRewritePrompt(originalCopy, adjustment);
            var result = await CallOpenAIAsync(prompt, _settings.MaxTokens, ct);
            return result ?? await _fallback.RewriteAsync(originalCopy, adjustment, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI rewrite failed, falling back to templates");
            return await _fallback.RewriteAsync(originalCopy, adjustment, ct);
        }
    }
    
    public async Task<GenerateCopyResult> GenerateWithRecipientAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        RecipientContext? recipient,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildRecipientPrompt(copyTypeId, goal, context, length, brandName, brandTone, recipient);
            var includeSubject = copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase);
            
            var result = await CallOpenAIAsync(prompt, GetMaxTokensForLength(length) + (includeSubject ? 50 : 0), ct);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                return await _fallback.GenerateWithRecipientAsync(copyTypeId, goal, context, length, brandName, brandTone, recipient, ct);
            }
            
            // Parse subject line if included
            string? subject = null;
            var body = result;
            
            if (includeSubject)
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && lines[0].StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                {
                    subject = lines[0]["Subject:".Length..].Trim();
                    body = string.Join("\n", lines.Skip(1)).Trim();
                }
            }
            
            return new GenerateCopyResult(body, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI recipient generation failed, falling back to templates");
            return await _fallback.GenerateWithRecipientAsync(copyTypeId, goal, context, length, brandName, brandTone, recipient, ct);
        }
    }

    public async Task<GenerateCopyResult> GenerateInLanguageAsync(
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
        try
        {
            var prompt = BuildMultiLanguagePrompt(copyTypeId, goal, context, length, brandName, brandTone, recipient, targetLanguage);
            var includeSubject = copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase);
            
            var result = await CallOpenAIAsync(prompt, GetMaxTokensForLength(length) + (includeSubject ? 50 : 0), ct);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                return await _fallback.GenerateInLanguageAsync(copyTypeId, goal, context, length, brandName, brandTone, recipient, targetLanguage, ct);
            }
            
            // Parse subject line if included
            string? subject = null;
            var body = result;
            
            if (includeSubject)
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var firstLine = lines.FirstOrDefault() ?? "";
                // Check for subject in multiple languages
                if (firstLine.Contains(':') && (
                    firstLine.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase) ||
                    firstLine.StartsWith("Asunto:", StringComparison.OrdinalIgnoreCase) || // Spanish
                    firstLine.StartsWith("Objet:", StringComparison.OrdinalIgnoreCase) ||  // French
                    firstLine.StartsWith("Betreff:", StringComparison.OrdinalIgnoreCase) || // German
                    firstLine.StartsWith("Oggetto:", StringComparison.OrdinalIgnoreCase) || // Italian
                    firstLine.StartsWith("主题:", StringComparison.OrdinalIgnoreCase) ||     // Chinese
                    firstLine.StartsWith("件名:", StringComparison.OrdinalIgnoreCase)))    // Japanese
                {
                    var colonIdx = firstLine.IndexOf(':');
                    subject = firstLine[(colonIdx + 1)..].Trim();
                    body = string.Join("\n", lines.Skip(1)).Trim();
                }
            }
            
            return new GenerateCopyResult(body, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI multi-language generation failed, falling back to templates");
            return await _fallback.GenerateInLanguageAsync(copyTypeId, goal, context, length, brandName, brandTone, recipient, targetLanguage, ct);
        }
    }
    
    private async Task<string?> CallOpenAIAsync(string prompt, int maxTokens, CancellationToken ct)
    {
        var request = new OpenAIRequest
        {
            Model = _settings.Model,
            Messages = new[]
            {
                new OpenAIMessage { Role = "system", Content = GetSystemPrompt() },
                new OpenAIMessage { Role = "user", Content = prompt }
            },
            MaxTokens = maxTokens,
            Temperature = _settings.Temperature
        };
        
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        _logger.LogInformation("Calling OpenAI API with model {Model}", _settings.Model);
        
        var response = await _httpClient.PostAsync(OpenAIApiUrl, content, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }
        
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson, JsonOptions);
        
        var result = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        
        if (!string.IsNullOrWhiteSpace(result))
        {
            _logger.LogInformation("OpenAI generation successful, tokens used: {Tokens}", 
                openAIResponse?.Usage?.TotalTokens ?? 0);
        }
        
        return result;
    }
    
    private static string GetSystemPrompt() => """
        You are an expert B2B sales copywriter. Your copy is:
        - Concise and action-oriented
        - Personalized and human-sounding
        - Clear with strong calls-to-action
        - Never generic or templated
        
        Important rules:
        - Do NOT use placeholder brackets like [First Name] - the copy should be ready to send
        - Do NOT include signatures unless specifically asked
        - Match the requested tone exactly
        - Keep within length guidelines
        """;
    
    private static string BuildGenerationPrompt(
        string copyTypeId, 
        string goal, 
        string? context, 
        string length,
        string? brandName,
        string? brandTone)
    {
        var toneDescription = GetToneDescription(brandTone);
        var lengthDescription = GetLengthDescription(length);
        var copyTypeName = GetCopyTypeName(copyTypeId);
        
        return $"""
            Write a {copyTypeName} with these specifications:
            
            GOAL: {goal}
            TONE: {toneDescription}
            LENGTH: {lengthDescription}
            {(string.IsNullOrWhiteSpace(brandName) ? "" : $"YOUR BRAND: {brandName}")}
            {(string.IsNullOrWhiteSpace(context) ? "" : $"\nADDITIONAL CONTEXT:\n{context}")}
            
            Write only the message body. Do not include subject line or signature.
            """;
    }
    
    private static string BuildRewritePrompt(string originalCopy, string adjustment)
    {
        var instructions = adjustment?.ToLowerInvariant() switch
        {
            "shorter" or "short" => """
                Make this copy SHORTER:
                - Cut to the essential message
                - Remove all filler words and phrases
                - Keep under 100 words
                - Maintain the core message and CTA
                """,
            "friendlier" or "friendly" => """
                Make this copy FRIENDLIER:
                - Use warm, conversational language
                - Add 1-2 appropriate emojis
                - Replace formal phrases with casual ones
                - Sound genuinely interested in the recipient
                """,
            "persuasive" or "more persuasive" => """
                Make this copy MORE PERSUASIVE:
                - Add urgency without being pushy
                - Emphasize specific benefits and outcomes
                - Include compelling statistics or proof points
                - Strengthen the call-to-action
                - Create appropriate FOMO
                """,
            _ => "Improve this copy while maintaining the core message."
        };
        
        return $"""
            {instructions}
            
            ORIGINAL COPY:
            {originalCopy}
            
            REWRITTEN COPY:
            """;
    }
    
    private static string BuildRecipientPrompt(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        RecipientContext? recipient)
    {
        var toneDescription = GetToneDescription(brandTone);
        var lengthDescription = GetLengthDescription(length);
        var copyTypeName = GetCopyTypeName(copyTypeId);
        var includeSubject = copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase);
        
        var recipientInfo = BuildRecipientInfo(recipient);
        
        var subjectInstruction = includeSubject 
            ? "\n\nStart with 'Subject: [compelling subject line]' on its own line, then the body."
            : "";
        
        return $"""
            Write a personalized {copyTypeName} with these specifications:
            
            GOAL: {goal}
            TONE: {toneDescription}
            LENGTH: {lengthDescription}
            {(string.IsNullOrWhiteSpace(brandName) ? "" : $"YOUR BRAND: {brandName}")}
            {recipientInfo}
            {(string.IsNullOrWhiteSpace(context) ? "" : $"\nADDITIONAL CONTEXT:\n{context}")}
            
            Personalize the message using the recipient information provided.{subjectInstruction}
            """;
    }

    private static string BuildMultiLanguagePrompt(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? brandName,
        string? brandTone,
        RecipientContext? recipient,
        string targetLanguage)
    {
        var toneDescription = GetToneDescription(brandTone);
        var lengthDescription = GetLengthDescription(length);
        var copyTypeName = GetCopyTypeName(copyTypeId);
        var includeSubject = copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase);
        var languageName = GetLanguageName(targetLanguage);
        
        var recipientInfo = BuildRecipientInfo(recipient);
        
        var subjectInstruction = includeSubject 
            ? $"\n\nStart with the subject line in {languageName} on its own line (e.g., 'Asunto:' for Spanish), then the body."
            : "";
        
        return $"""
            Write a personalized {copyTypeName} in {languageName} with these specifications:
            
            IMPORTANT: Write the ENTIRE response in {languageName}. Do not use English.
            
            GOAL: {goal}
            TONE: {toneDescription}
            LENGTH: {lengthDescription}
            LANGUAGE: {languageName}
            {(string.IsNullOrWhiteSpace(brandName) ? "" : $"YOUR BRAND: {brandName}")}
            {recipientInfo}
            {(string.IsNullOrWhiteSpace(context) ? "" : $"\nADDITIONAL CONTEXT:\n{context}")}
            
            Personalize the message using the recipient information provided.
            Use culturally appropriate greetings and closings for {languageName}.{subjectInstruction}
            """;
    }

    private static string BuildRecipientInfo(RecipientContext? recipient)
    {
        if (recipient == null) return "";
        
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(recipient.Name))
            parts.Add($"- Name: {recipient.Name}");
        if (!string.IsNullOrWhiteSpace(recipient.Email))
            parts.Add($"- Email: {recipient.Email}");
        if (!string.IsNullOrWhiteSpace(recipient.Company))
            parts.Add($"- Company: {recipient.Company}");
        if (!string.IsNullOrWhiteSpace(recipient.Title))
            parts.Add($"- Title: {recipient.Title}");
        if (!string.IsNullOrWhiteSpace(recipient.DealStage))
            parts.Add($"- Deal Stage: {recipient.DealStage}");
        if (!string.IsNullOrWhiteSpace(recipient.DealValue))
            parts.Add($"- Deal Value: {recipient.DealValue}");
        if (!string.IsNullOrWhiteSpace(recipient.Type))
            parts.Add($"- Type: {recipient.Type}");
        
        return parts.Count > 0 ? "\nRECIPIENT:\n" + string.Join("\n", parts) : "";
    }

    private static string GetLanguageName(string languageCode) => languageCode?.ToLowerInvariant() switch
    {
        "es" or "spanish" => "Spanish",
        "fr" or "french" => "French",
        "de" or "german" => "German",
        "it" or "italian" => "Italian",
        "pt" or "portuguese" => "Portuguese",
        "nl" or "dutch" => "Dutch",
        "zh" or "chinese" => "Chinese (Simplified)",
        "ja" or "japanese" => "Japanese",
        "ko" or "korean" => "Korean",
        "ar" or "arabic" => "Arabic",
        "ru" or "russian" => "Russian",
        "hi" or "hindi" => "Hindi",
        "pl" or "polish" => "Polish",
        "tr" or "turkish" => "Turkish",
        "sv" or "swedish" => "Swedish",
        "no" or "norwegian" => "Norwegian",
        "da" or "danish" => "Danish",
        "fi" or "finnish" => "Finnish",
        _ => "English"
    };
    
    private static string GetToneDescription(string? brandTone) => brandTone?.ToLowerInvariant() switch
    {
        "friendly" => "Warm, conversational, and approachable. Use casual language and appropriate emojis.",
        "persuasive" => "Compelling, urgent, and action-oriented. Emphasize value and create FOMO.",
        _ => "Professional and business-focused. Polished but not stiff."
    };
    
    private static string GetLengthDescription(string length) => length?.ToLowerInvariant() switch
    {
        "short" => "Very concise, 2-3 sentences max. Get straight to the point.",
        "long" => "Detailed, 2-3 paragraphs with supporting points. Comprehensive but not rambling.",
        _ => "Moderate length, 1 paragraph or about 4-6 sentences. Balanced."
    };
    
    private static string GetCopyTypeName(string copyTypeId) => copyTypeId?.ToLowerInvariant() switch
    {
        "sales-email" => "sales email",
        "follow-up" => "follow-up message",
        "crm-note" => "CRM note",
        "deal-message" => "deal update message",
        "workflow-message" => "workflow message",
        "linkedin-connect" => "LinkedIn connection request (max 300 characters)",
        "linkedin-inmail" => "LinkedIn InMail message",
        "sms" => "SMS text message (max 160 characters)",
        "call-script" => "phone call script",
        "meeting-agenda" => "meeting agenda",
        _ => "sales message"
    };
    
    private int GetMaxTokensForLength(string length) => length?.ToLowerInvariant() switch
    {
        "short" => 150,
        "long" => 600,
        _ => _settings.MaxTokens
    };
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    // OpenAI API DTOs
    private class OpenAIRequest
    {
        public string Model { get; set; } = "";
        public OpenAIMessage[] Messages { get; set; } = [];
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
    }
    
    private class OpenAIMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }
    
    private class OpenAIResponse
    {
        public OpenAIChoice[]? Choices { get; set; }
        public OpenAIUsage? Usage { get; set; }
    }
    
    private class OpenAIChoice
    {
        public OpenAIMessage? Message { get; set; }
    }
    
    private class OpenAIUsage
    {
        public int TotalTokens { get; set; }
    }
}
