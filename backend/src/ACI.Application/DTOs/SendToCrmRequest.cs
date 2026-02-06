using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to send/log copy to CRM.
/// </summary>
public record SendToCrmRequest
{
    /// <summary>
    /// Type of CRM object (lead, deal, contact, company).
    /// </summary>
    [Required(ErrorMessage = "ObjectType is required")]
    [StringLength(50, ErrorMessage = "ObjectType cannot exceed 50 characters")]
    public required string ObjectType { get; init; }

    /// <summary>
    /// ID of the CRM record.
    /// </summary>
    [Required(ErrorMessage = "RecordId is required")]
    [StringLength(100, ErrorMessage = "RecordId cannot exceed 100 characters")]
    public required string RecordId { get; init; }

    /// <summary>
    /// Name of the CRM record.
    /// </summary>
    [Required(ErrorMessage = "RecordName is required")]
    [StringLength(200, ErrorMessage = "RecordName cannot exceed 200 characters")]
    public required string RecordName { get; init; }

    /// <summary>
    /// The generated copy content.
    /// </summary>
    [Required(ErrorMessage = "Copy is required")]
    [StringLength(10000, ErrorMessage = "Copy cannot exceed 10000 characters")]
    public required string Copy { get; init; }

    /// <summary>
    /// Label of the copy type.
    /// </summary>
    [Required(ErrorMessage = "CopyTypeLabel is required")]
    [StringLength(100, ErrorMessage = "CopyTypeLabel cannot exceed 100 characters")]
    public required string CopyTypeLabel { get; init; }
}
