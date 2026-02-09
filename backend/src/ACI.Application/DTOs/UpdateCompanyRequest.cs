using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing company.
/// </summary>
public record UpdateCompanyRequest
{
    /// <summary>
    /// Company name.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// Company domain/website.
    /// </summary>
    [StringLength(253, ErrorMessage = "Domain cannot exceed 253 characters")]
    [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$", 
        ErrorMessage = "Invalid domain format (e.g., example.com)")]
    public string? Domain { get; init; }

    /// <summary>
    /// Industry sector.
    /// </summary>
    [StringLength(100, ErrorMessage = "Industry cannot exceed 100 characters")]
    public string? Industry { get; init; }

    /// <summary>
    /// Company size (e.g., 1-10, 11-50).
    /// </summary>
    [StringLength(50, ErrorMessage = "Size cannot exceed 50 characters")]
    public string? Size { get; init; }

    /// <summary>
    /// Company description.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Company website URL.
    /// </summary>
    [StringLength(500, ErrorMessage = "Website cannot exceed 500 characters")]
    public string? Website { get; init; }

    /// <summary>
    /// Company location / address.
    /// </summary>
    [StringLength(300, ErrorMessage = "Location cannot exceed 300 characters")]
    public string? Location { get; init; }
}
