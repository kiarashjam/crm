using System.ComponentModel.DataAnnotations;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to generate copy in multiple languages.
/// </summary>
public record GenerateCopyMultiLanguageRequest
{
    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    [StringLength(50, ErrorMessage = "BrandTone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public string? Length { get; init; }

    /// <summary>
    /// Target language code (e.g., "es", "fr", "de", "zh").
    /// </summary>
    [Required(ErrorMessage = "TargetLanguage is required")]
    [StringLength(10, ErrorMessage = "TargetLanguage cannot exceed 10 characters")]
    public required string TargetLanguage { get; init; }

    public RecipientDto? Recipient { get; init; }

    public CrmObjectDto? CrmObject { get; init; }
}

