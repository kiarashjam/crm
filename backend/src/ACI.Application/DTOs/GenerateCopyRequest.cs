using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to generate marketing copy.
/// </summary>
public record GenerateCopyRequest
{
    /// <summary>
    /// Copy type identifier (e.g., email, social-post).
    /// </summary>
    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    /// <summary>
    /// Goal or purpose of the copy.
    /// </summary>
    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    /// <summary>
    /// Additional context (optional).
    /// </summary>
    [StringLength(2000, ErrorMessage = "Context cannot exceed 2000 characters")]
    public string? Context { get; init; }

    /// <summary>
    /// Desired length (short, medium, long).
    /// </summary>
    [Required(ErrorMessage = "Length is required")]
    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public required string Length { get; init; }

    /// <summary>
    /// Company name for personalization (optional).
    /// </summary>
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? CompanyName { get; init; }

    /// <summary>
    /// Brand tone (e.g., professional, casual) (optional).
    /// </summary>
    [StringLength(50, ErrorMessage = "Brand tone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }
}
