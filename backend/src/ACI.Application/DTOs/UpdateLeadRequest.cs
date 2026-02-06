using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing lead.
/// </summary>
public record UpdateLeadRequest
{
    /// <summary>
    /// Lead's full name.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// Lead's email address.
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string? Email { get; init; }

    /// <summary>
    /// Lead's phone number.
    /// </summary>
    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters")]
    [Phone(ErrorMessage = "Invalid phone format")]
    public string? Phone { get; init; }

    /// <summary>
    /// Associated company ID.
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// Source of the lead (e.g., Website, Referral).
    /// </summary>
    [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
    public string? Source { get; init; }

    /// <summary>
    /// Current status of the lead.
    /// </summary>
    [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
    public string? Status { get; init; }

    /// <summary>
    /// Lead source reference ID.
    /// </summary>
    public Guid? LeadSourceId { get; init; }

    /// <summary>
    /// Lead status reference ID.
    /// </summary>
    public Guid? LeadStatusId { get; init; }

    /// <summary>
    /// Lead score (0-100).
    /// </summary>
    [Range(0, 100, ErrorMessage = "Lead score must be between 0 and 100")]
    public int? LeadScore { get; init; }

    /// <summary>
    /// Last time the lead was contacted.
    /// </summary>
    public DateTime? LastContactedAt { get; init; }

    /// <summary>
    /// Additional description or notes about the lead.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Current lifecycle stage (e.g., Subscriber, Lead, MQL).
    /// </summary>
    [StringLength(50, ErrorMessage = "Lifecycle stage cannot exceed 50 characters")]
    public string? LifecycleStage { get; init; }
}
