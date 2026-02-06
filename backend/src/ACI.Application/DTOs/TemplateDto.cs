using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

public record TemplateDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string CopyTypeId,
    string Goal,
    string? Content,
    string? BrandTone,
    string? Length,
    int UseCount,
    bool IsSharedWithOrganization,
    bool IsSystemTemplate,
    Guid? UserId,
    string? UserName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

/// <summary>
/// Request to create a new template.
/// </summary>
public record CreateTemplateRequest
{
    /// <summary>
    /// Template title.
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public required string Title { get; init; }

    /// <summary>
    /// Template description.
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Template category.
    /// </summary>
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string? Category { get; init; }

    /// <summary>
    /// Copy type identifier.
    /// </summary>
    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    /// <summary>
    /// Goal or purpose.
    /// </summary>
    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    /// <summary>
    /// Template content.
    /// </summary>
    [StringLength(10000, ErrorMessage = "Content cannot exceed 10000 characters")]
    public string? Content { get; init; }

    /// <summary>
    /// Brand tone.
    /// </summary>
    [StringLength(50, ErrorMessage = "BrandTone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    /// <summary>
    /// Desired length.
    /// </summary>
    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public string? Length { get; init; }

    /// <summary>
    /// Whether to share with organization.
    /// </summary>
    public bool IsSharedWithOrganization { get; init; } = false;
}

/// <summary>
/// Request to update an existing template.
/// </summary>
public record UpdateTemplateRequest
{
    /// <summary>
    /// Template title.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public string? Title { get; init; }

    /// <summary>
    /// Template description.
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Template category.
    /// </summary>
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string? Category { get; init; }

    /// <summary>
    /// Copy type identifier.
    /// </summary>
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public string? CopyTypeId { get; init; }

    /// <summary>
    /// Goal or purpose.
    /// </summary>
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public string? Goal { get; init; }

    /// <summary>
    /// Template content.
    /// </summary>
    [StringLength(10000, ErrorMessage = "Content cannot exceed 10000 characters")]
    public string? Content { get; init; }

    /// <summary>
    /// Brand tone.
    /// </summary>
    [StringLength(50, ErrorMessage = "BrandTone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    /// <summary>
    /// Desired length.
    /// </summary>
    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public string? Length { get; init; }

    /// <summary>
    /// Whether to share with organization.
    /// </summary>
    public bool? IsSharedWithOrganization { get; init; }
}
