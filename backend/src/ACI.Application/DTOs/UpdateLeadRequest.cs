using System.ComponentModel.DataAnnotations;
using ACI.Application.Common;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing lead. A field left out is kept; the company, phone,
/// description, lifecycle stage, job title, country, industry and company size are
/// cleared when sent as null.
/// </summary>
public record UpdateLeadRequest : IMergePatchRequest
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

    /// <summary>
    /// A change to the lead's structured sales-lifecycle progression (outreach → meeting
    /// → contract → signature → deposit), as a JSON merge patch: the steps sent replace
    /// the stored ones, a step sent as null is removed, and steps not sent are kept. An
    /// empty string clears the whole pipeline. The client owns the shape; the server
    /// reads the result to work out the status, so sending it can move the status even
    /// when no status is sent.
    /// </summary>
    [StringLength(PipelineStatePatch.MaxLength, ErrorMessage = "Pipeline state is too large")]
    public string? PipelineState { get; init; }

    [StringLength(200)]
    public string? JobTitle { get; init; }

    [StringLength(80)]
    public string? Country { get; init; }

    [StringLength(120)]
    public string? Industry { get; init; }

    [StringLength(40)]
    public string? CompanySize { get; init; }

    /// <summary>
    /// "import" when an import updates the lead from a row of its file. A status it changes
    /// then sets off no lead.status_changed automations, unless <see cref="RunAutomations"/>
    /// asks for them: the file records where people already are, it is not each of them
    /// moving on today. Null for every other edit.
    /// </summary>
    [RegularExpression("(?i)^import$", ErrorMessage = "Origin can only be \"import\"")]
    public string? Origin { get; init; }

    /// <summary>With <see cref="Origin"/> "import": run the status automations anyway, because the person importing chose to.</summary>
    public bool RunAutomations { get; init; }

    /// <summary>True when an import is making this change.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsImport => string.Equals(Origin?.Trim(), "import", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The lead's version as this edit read it (<c>version</c> on the lead, or its ETag).
    /// When sent, the edit is refused with 409 if anyone has saved the lead since, rather
    /// than written over their changes. Left out, the edit is applied to the lead as it
    /// is now: right for a change to one field that reads nothing else, such as a step recorded on its pipeline.
    /// </summary>
    public Guid? Version { get; init; }

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}
