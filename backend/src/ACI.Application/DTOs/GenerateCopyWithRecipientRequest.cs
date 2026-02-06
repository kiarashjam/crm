using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to generate copy with recipient context.
/// </summary>
public record GenerateCopyWithRecipientRequest
{
    /// <summary>
    /// Copy type identifier.
    /// </summary>
    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    /// <summary>
    /// Goal or purpose.
    /// </summary>
    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    /// <summary>
    /// Additional context.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Context cannot exceed 2000 characters")]
    public string? Context { get; init; }

    /// <summary>
    /// Desired length.
    /// </summary>
    [Required(ErrorMessage = "Length is required")]
    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public required string Length { get; init; }

    /// <summary>
    /// Company name for personalization.
    /// </summary>
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? CompanyName { get; init; }

    /// <summary>
    /// Brand tone.
    /// </summary>
    [StringLength(50, ErrorMessage = "Brand tone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    /// <summary>
    /// Recipient information.
    /// </summary>
    public RecipientDto? Recipient { get; init; }
}

/// <summary>
/// Recipient information for copy personalization.
/// </summary>
public record RecipientDto
{
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; init; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string? Email { get; init; }

    [StringLength(200, ErrorMessage = "Company cannot exceed 200 characters")]
    public string? Company { get; init; }

    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
    public string? Title { get; init; }

    [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
    public string? Type { get; init; }

    [StringLength(100, ErrorMessage = "LastActivity cannot exceed 100 characters")]
    public string? LastActivity { get; init; }

    [StringLength(50, ErrorMessage = "DealStage cannot exceed 50 characters")]
    public string? DealStage { get; init; }

    [StringLength(50, ErrorMessage = "DealValue cannot exceed 50 characters")]
    public string? DealValue { get; init; }
}

/// <summary>
/// Generic CRM object reference for context in copy generation.
/// </summary>
public record CrmObjectDto
{
    [StringLength(100, ErrorMessage = "Id cannot exceed 100 characters")]
    public string? Id { get; init; }

    /// <summary>
    /// Object type: lead, deal, contact, company.
    /// </summary>
    [StringLength(20, ErrorMessage = "Type cannot exceed 20 characters")]
    public string? Type { get; init; }

    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; init; }

    [StringLength(50, ErrorMessage = "Stage cannot exceed 50 characters")]
    public string? Stage { get; init; }

    [StringLength(50, ErrorMessage = "Value cannot exceed 50 characters")]
    public string? Value { get; init; }

    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters")]
    public string? Notes { get; init; }
}

/// <summary>
/// Response containing generated copy with optional subject.
/// </summary>
public record GenerateCopyWithSubjectResponse(string Body, string? Subject);
