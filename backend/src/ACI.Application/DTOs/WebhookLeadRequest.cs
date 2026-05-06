using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a lead via webhook.
/// </summary>
public record WebhookLeadRequest
{
    /// <summary>
    /// Lead's full name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Lead's email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }

    /// <summary>
    /// Lead's phone number (optional). Format is not strictly validated so external tools can send any string.
    /// </summary>
    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters")]
    public string? Phone { get; init; }

    /// <summary>
    /// Company name - will create company if provided (optional).
    /// </summary>
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? CompanyName { get; init; }

    /// <summary>
    /// Lead source - defaults to "webhook" if not provided (optional).
    /// </summary>
    [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
    public string? Source { get; init; }

    /// <summary>
    /// Shared webhook password when not sent as <c>X-Webhook-Password</c> (password-based endpoint only).
    /// </summary>
    [StringLength(256, ErrorMessage = "Webhook password cannot exceed 256 characters")]
    public string? WebhookPassword { get; init; }
}
