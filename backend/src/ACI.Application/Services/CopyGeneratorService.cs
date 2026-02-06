using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for generating copy/content using AI in the CRM system.
/// </summary>
public class CopyGeneratorService : ICopyGeneratorApplicationService
{
    private readonly ICopyGenerator _generator;
    private readonly ILogger<CopyGeneratorService> _logger;

    public CopyGeneratorService(ICopyGenerator generator, ILogger<CopyGeneratorService> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        Guid userId, 
        GenerateCopyRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating copy for user {UserId}, type '{CopyTypeId}', goal '{Goal}'", 
            userId, 
            request.CopyTypeId, 
            request.Goal);
        
        var result = await _generator.GenerateAsync(
            request.CopyTypeId,
            request.Goal,
            request.Context,
            request.Length ?? "medium",
            request.CompanyName,
            request.BrandTone,
            ct);
        
        _logger.LogInformation("Successfully generated copy for user {UserId}, {CharCount} characters", 
            userId, 
            result.Length);
        
        return result;
    }

    /// <inheritdoc />
    public async Task<GenerateCopyWithSubjectResponse> GenerateWithRecipientAsync(
        Guid userId, 
        GenerateCopyWithRecipientRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating copy with recipient for user {UserId}, type '{CopyTypeId}'", 
            userId, 
            request.CopyTypeId);
        
        RecipientContext? recipientContext = null;
        
        if (request.Recipient != null)
        {
            recipientContext = new RecipientContext(
                request.Recipient.Name,
                request.Recipient.Email,
                request.Recipient.Company,
                request.Recipient.Title,
                request.Recipient.Type,
                request.Recipient.LastActivity,
                request.Recipient.DealStage,
                request.Recipient.DealValue);
            
            _logger.LogDebug("Generating for recipient '{RecipientName}'", request.Recipient.Name);
        }

        var result = await _generator.GenerateWithRecipientAsync(
            request.CopyTypeId,
            request.Goal,
            request.Context,
            request.Length ?? "medium",
            request.CompanyName,
            request.BrandTone,
            recipientContext,
            ct);

        _logger.LogInformation("Successfully generated copy with subject for user {UserId}", userId);
        
        return new GenerateCopyWithSubjectResponse(result.Body, result.Subject);
    }

    /// <inheritdoc />
    public async Task<string> RewriteAsync(
        Guid userId, 
        RewriteCopyRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Rewriting copy for user {UserId}, adjustment '{Adjustment}'", 
            userId, 
            request.Adjustment);
        
        var result = await _generator.RewriteAsync(
            request.OriginalCopy,
            request.Adjustment,
            ct);
        
        _logger.LogInformation("Successfully rewrote copy for user {UserId}", userId);
        
        return result;
    }

    /// <inheritdoc />
    public async Task<GenerateCopyWithSubjectResponse> GenerateInLanguageAsync(
        Guid userId, 
        GenerateCopyMultiLanguageRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating copy in language '{Language}' for user {UserId}, type '{CopyTypeId}'", 
            request.TargetLanguage, 
            userId, 
            request.CopyTypeId);
        
        RecipientContext? recipientContext = null;
        
        if (request.Recipient != null)
        {
            recipientContext = new RecipientContext(
                request.Recipient.Name,
                request.Recipient.Email,
                request.Recipient.Company,
                request.Recipient.Title,
                request.Recipient.Type,
                request.Recipient.LastActivity,
                request.Recipient.DealStage,
                request.Recipient.DealValue);
        }

        var result = await _generator.GenerateInLanguageAsync(
            request.CopyTypeId,
            request.Goal,
            null, // context from CrmObject could be used here
            request.Length ?? "medium",
            null, // companyName
            request.BrandTone,
            recipientContext,
            request.TargetLanguage,
            ct);

        _logger.LogInformation(
            "Successfully generated copy in language '{Language}' for user {UserId}", 
            request.TargetLanguage, 
            userId);
        
        return new GenerateCopyWithSubjectResponse(result.Body, result.Subject);
    }
}
