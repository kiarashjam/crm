using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to convert a lead to contact, deal, and/or company.
/// </summary>
public record ConvertLeadRequest
{
    /// <summary>
    /// Whether to create a new contact from the lead.
    /// </summary>
    public bool CreateContact { get; init; }

    /// <summary>
    /// Whether to create a new deal from the lead.
    /// </summary>
    public bool CreateDeal { get; init; }

    /// <summary>
    /// Name for the new deal (required if CreateDeal is true).
    /// </summary>
    [StringLength(200, ErrorMessage = "Deal name cannot exceed 200 characters")]
    public string? DealName { get; init; }

    /// <summary>
    /// Value for the new deal.
    /// </summary>
    [StringLength(50, ErrorMessage = "Deal value cannot exceed 50 characters")]
    [RegularExpression(@"^-?\d+(\.\d{1,2})?$", ErrorMessage = "Deal value must be a valid number")]
    public string? DealValue { get; init; }

    /// <summary>
    /// Stage for the new deal (legacy, prefer DealStageId).
    /// </summary>
    [StringLength(50, ErrorMessage = "Deal stage cannot exceed 50 characters")]
    public string? DealStage { get; init; }

    /// <summary>
    /// Pipeline ID for the new deal.
    /// </summary>
    public Guid? PipelineId { get; init; }

    /// <summary>
    /// Deal stage ID for the new deal.
    /// </summary>
    public Guid? DealStageId { get; init; }

    /// <summary>
    /// Whether to create a new company from the lead company info.
    /// </summary>
    public bool CreateNewCompany { get; init; }

    /// <summary>
    /// Name for the new company (if CreateNewCompany is true).
    /// </summary>
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? NewCompanyName { get; init; }

    /// <summary>
    /// Existing company ID to use (instead of creating new).
    /// </summary>
    public Guid? ExistingCompanyId { get; init; }

    /// <summary>
    /// Existing contact ID to link (instead of creating new).
    /// </summary>
    public Guid? ExistingContactId { get; init; }

    /// <summary>
    /// Existing deal ID to attach to (instead of creating new).
    /// </summary>
    public Guid? ExistingDealId { get; init; }
}
