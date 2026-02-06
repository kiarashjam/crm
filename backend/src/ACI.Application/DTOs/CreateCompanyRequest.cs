using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a new company.
/// </summary>
public record CreateCompanyRequest
{
    /// <summary>
    /// Company name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Company domain/website (optional).
    /// </summary>
    [StringLength(253, ErrorMessage = "Domain cannot exceed 253 characters")]
    [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$", 
        ErrorMessage = "Invalid domain format (e.g., example.com)")]
    public string? Domain { get; init; }

    /// <summary>
    /// Industry sector (optional).
    /// </summary>
    [StringLength(100, ErrorMessage = "Industry cannot exceed 100 characters")]
    public string? Industry { get; init; }

    /// <summary>
    /// Company size (e.g., 1-10, 11-50).
    /// </summary>
    [StringLength(50, ErrorMessage = "Size cannot exceed 50 characters")]
    public string? Size { get; init; }
}
