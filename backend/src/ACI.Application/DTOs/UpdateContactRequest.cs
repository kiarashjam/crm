using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing contact.
/// </summary>
public record UpdateContactRequest
{
    /// <summary>
    /// Contact's full name.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// Contact's email address.
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string? Email { get; init; }

    /// <summary>
    /// Contact's phone number.
    /// </summary>
    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters")]
    [Phone(ErrorMessage = "Invalid phone format")]
    public string? Phone { get; init; }

    /// <summary>
    /// Contact's job title.
    /// </summary>
    [StringLength(100, ErrorMessage = "Job title cannot exceed 100 characters")]
    public string? JobTitle { get; init; }

    /// <summary>
    /// Associated company ID.
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// Whether this contact should not be contacted.
    /// </summary>
    public bool? DoNotContact { get; init; }

    /// <summary>
    /// Preferred method of contact (email, phone, etc).
    /// </summary>
    [StringLength(50, ErrorMessage = "Preferred contact method cannot exceed 50 characters")]
    public string? PreferredContactMethod { get; init; }

    /// <summary>
    /// Contact description / notes. HP-7.
    /// </summary>
    [StringLength(4000, ErrorMessage = "Description cannot exceed 4000 characters")]
    public string? Description { get; init; }
}
