using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing copy templates in the CRM system.
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ITemplateRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        ITemplateRepository repository, 
        IUserRepository userRepository,
        ILogger<TemplateService> logger)
    {
        _repository = repository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TemplateDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        string? category = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting paged templates for user {UserId} in organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}', category '{Category}'", 
            userId, organizationId, page, pageSize, search, category);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, category, ct);

        _logger.LogDebug("Retrieved {Count} of {Total} templates for user {UserId}", items.Count, totalCount, userId);

        return PagedResult<TemplateDto>.Create(
            items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateDto>> GetUserTemplatesAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting templates for user {UserId} in organization {OrganizationId}", userId, organizationId);
        
        var templates = new List<Template>();
        
        // Get user's own templates
        var userTemplates = await _repository.GetByUserIdAsync(userId, ct);
        templates.AddRange(userTemplates);
        
        // Get organization shared templates (excluding user's own)
        if (organizationId.HasValue)
        {
            var sharedTemplates = await _repository.GetSharedTemplatesAsync(organizationId.Value, ct);
            templates.AddRange(sharedTemplates.Where(t => t.UserId != userId));
        }
        
        // Get system templates
        var allTemplates = await _repository.GetAllAsync(ct);
        templates.AddRange(allTemplates.Where(t => t.IsSystemTemplate && templates.All(x => x.Id != t.Id)));
        
        _logger.LogDebug("Retrieved {Count} templates for user {UserId}", templates.Count, userId);
        
        return templates.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<TemplateDto>> GetByIdAsync(
        Guid userId, 
        Guid id, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting template {TemplateId} for user {UserId}", id, userId);
        
        var template = await _repository.GetByIdAsync(id, ct);
        
        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found", id);
            return DomainErrors.Template.NotFound;
        }
        
        // Check access: user owns it, it's shared with their org, or it's a system template
        if (template.UserId != userId && !template.IsSharedWithOrganization && !template.IsSystemTemplate)
        {
            _logger.LogWarning("User {UserId} does not have access to template {TemplateId}", userId, id);
            return DomainErrors.Template.NotOwned;
        }
        
        return MapToDto(template);
    }

    /// <inheritdoc />
    public async Task<Result<TemplateDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateTemplateRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating template for user {UserId} with title '{Title}'", userId, request.Title);
        
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _logger.LogWarning("Template creation failed - title is required");
            return DomainErrors.Template.TitleRequired;
        }
        
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            _logger.LogWarning("Template creation failed - content is required");
            return DomainErrors.Template.ContentRequired;
        }

        try
        {
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                Category = request.Category ?? "Custom",
                CopyTypeId = Enum.TryParse<CopyTypeId>(request.CopyTypeId.Replace("-", ""), true, out var copyType) 
                    ? copyType : CopyTypeId.SalesEmail,
                Goal = request.Goal,
                Content = request.Content,
                BrandTone = request.BrandTone,
                Length = request.Length,
                UseCount = 0,
                UserId = userId,
                OrganizationId = organizationId,
                IsSharedWithOrganization = request.IsSharedWithOrganization,
                IsSystemTemplate = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(template, ct);
            
            _logger.LogInformation("Successfully created template {TemplateId} with title '{Title}'", created.Id, created.Title);
            return MapToDto(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template for user {UserId}", userId);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TemplateDto>> UpdateAsync(
        Guid userId, 
        Guid id, 
        UpdateTemplateRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating template {TemplateId} for user {UserId}", id, userId);
        
        var template = await _repository.GetByIdAsync(id, ct);
        
        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found for update", id);
            return DomainErrors.Template.NotFound;
        }
        
        if (template.UserId != userId)
        {
            _logger.LogWarning("User {UserId} cannot update template {TemplateId} - not owner", userId, id);
            return DomainErrors.Template.NotOwned;
        }
        
        if (template.IsSystemTemplate)
        {
            _logger.LogWarning("Cannot modify system template {TemplateId}", id);
            return DomainErrors.Template.CannotModifySystem;
        }

        try
        {
            if (request.Title != null) template.Title = request.Title;
            if (request.Description != null) template.Description = request.Description;
            if (request.Category != null) template.Category = request.Category;
            if (request.CopyTypeId != null && Enum.TryParse<CopyTypeId>(request.CopyTypeId.Replace("-", ""), true, out var copyType))
                template.CopyTypeId = copyType;
            if (request.Goal != null) template.Goal = request.Goal;
            if (request.Content != null) template.Content = request.Content;
            if (request.BrandTone != null) template.BrandTone = request.BrandTone;
            if (request.Length != null) template.Length = request.Length;
            if (request.IsSharedWithOrganization.HasValue) template.IsSharedWithOrganization = request.IsSharedWithOrganization.Value;
            
            template.UpdatedAtUtc = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(template, ct);
            
            _logger.LogInformation("Successfully updated template {TemplateId}", id);
            return MapToDto(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid userId, 
        Guid id, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting template {TemplateId} for user {UserId}", id, userId);
        
        var template = await _repository.GetByIdAsync(id, ct);
        
        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found for deletion", id);
            return DomainErrors.Template.NotFound;
        }
        
        if (template.UserId != userId)
        {
            _logger.LogWarning("User {UserId} cannot delete template {TemplateId} - not owner", userId, id);
            return DomainErrors.Template.NotOwned;
        }
        
        if (template.IsSystemTemplate)
        {
            _logger.LogWarning("Cannot delete system template {TemplateId}", id);
            return DomainErrors.Template.CannotModifySystem;
        }

        try
        {
            await _repository.DeleteAsync(id, ct);
            
            _logger.LogInformation("Successfully deleted template {TemplateId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task IncrementUseCountAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Incrementing use count for template {TemplateId}", id);
        await _repository.IncrementUseCountAsync(id, ct);
    }

    private static TemplateDto MapToDto(Template t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Category,
        t.CopyTypeId.ToString().ToLowerInvariant().Replace("email", "-email").Replace("message", "-message").Replace("note", "-note"),
        t.Goal,
        t.Content,
        t.BrandTone,
        t.Length,
        t.UseCount,
        t.IsSharedWithOrganization,
        t.IsSystemTemplate,
        t.UserId,
        t.User?.Name,
        t.CreatedAtUtc,
        t.UpdatedAtUtc
    );
}
