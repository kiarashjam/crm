using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a lead via webhook.
/// </summary>
public record WebhookLeadRequest
{
    /// <summary>
    /// "person" or "establishment" (optional): what the lead is. Left out, the workspace's
    /// own kind. A workspace of people only refuses establishments.
    /// </summary>
    [StringLength(20)]
    public string? Kind { get; init; }

    /// <summary>
    /// Lead's full name, or the establishment's name ("Pizzeria Roma").
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Lead's email address: required for a person, optional for an establishment (its
    /// general address).
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string? Email { get; init; }

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

    // An establishment's own fields (optional), as on the lead.
    [StringLength(40)] public string? EstablishmentType { get; init; }
    [StringLength(200)] public string? AddressLine { get; init; }
    [StringLength(20)] public string? PostalCode { get; init; }
    [StringLength(100)] public string? City { get; init; }
    /// <summary>Country (optional), for a person or an establishment.</summary>
    [StringLength(80)] public string? Country { get; init; }
    [StringLength(300)] public string? Website { get; init; }
    [Range(0, 100000)] public int? Seats { get; init; }
    [StringLength(200)] public string? ContactPersonName { get; init; }
    [StringLength(100)] public string? ContactPersonRole { get; init; }
    [EmailAddress(ErrorMessage = "Invalid contact person email format")]
    [StringLength(256)] public string? ContactPersonEmail { get; init; }
    [StringLength(64)] public string? ContactPersonPhone { get; init; }

    /// <summary>
    /// Shared webhook password when not sent as <c>X-Webhook-Password</c> (password-based endpoint only).
    /// </summary>
    [StringLength(256, ErrorMessage = "Webhook password cannot exceed 256 characters")]
    public string? WebhookPassword { get; init; }
}
