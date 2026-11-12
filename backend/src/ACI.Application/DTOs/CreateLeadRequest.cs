using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a new lead.
/// </summary>
public record CreateLeadRequest
{
    /// <summary>
    /// Lead's full name, or an establishment's name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Lead's email address: required for a person, and an establishment's own email when it
    /// has one. Checked by the service, which knows which the lead is.
    /// </summary>
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string? Email { get; init; }

    /// <summary>
    /// Lead's phone number (optional).
    /// </summary>
    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters")]
    [Phone(ErrorMessage = "Invalid phone format")]
    public string? Phone { get; init; }

    /// <summary>
    /// Associated company ID (optional).
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// The contact who referred this lead (optional). Must be a contact in the same workspace.
    /// </summary>
    public Guid? ReferredByContactId { get; init; }

    /// <summary>
    /// Source of the lead (e.g., Website, Referral).
    /// </summary>
    [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
    public string? Source { get; init; }

    /// <summary>
    /// Current status of the lead.
    /// </summary>
    [Required(ErrorMessage = "Status is required")]
    [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
    public required string Status { get; init; }

    /// <summary>
    /// Lead source reference ID (optional).
    /// </summary>
    public Guid? LeadSourceId { get; init; }

    /// <summary>
    /// Lead status reference ID (optional).
    /// </summary>
    public Guid? LeadStatusId { get; init; }

    /// <summary>
    /// Lead score (0-100).
    /// </summary>
    [Range(0, 100, ErrorMessage = "Lead score must be between 0 and 100")]
    public int? LeadScore { get; init; }

    /// <summary>
    /// Last time the lead was contacted (optional).
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

    [StringLength(200)]
    public string? JobTitle { get; init; }

    [StringLength(80)]
    public string? Country { get; init; }

    [StringLength(120)]
    public string? Industry { get; init; }

    [StringLength(40)]
    public string? CompanySize { get; init; }

    /// <summary>"person" or "establishment". Left out: the workspace's kind (see Organization.LeadKinds).</summary>
    [StringLength(20)]
    public string? Kind { get; init; }

    /// <summary>For an establishment: restaurant, cafe, bar, hotel, bakery, catering, food_truck, shop or other.</summary>
    [StringLength(40)]
    public string? EstablishmentType { get; init; }

    [StringLength(200)]
    public string? AddressLine { get; init; }

    [StringLength(20)]
    public string? PostalCode { get; init; }

    [StringLength(100)]
    public string? City { get; init; }

    [StringLength(300)]
    public string? Website { get; init; }

    [Range(0, 100000)]
    public int? Seats { get; init; }

    /// <summary>The person to speak to at an establishment.</summary>
    [StringLength(200)]
    public string? ContactPersonName { get; init; }

    [StringLength(100)]
    public string? ContactPersonRole { get; init; }

    [StringLength(254)]
    public string? ContactPersonEmail { get; init; }

    [StringLength(50)]
    public string? ContactPersonPhone { get; init; }

    /// <summary>
    /// The lead's five-phase pipeline as JSON, when it arrives with work already
    /// recorded. Its status then follows from it, as it does on every later save.
    /// </summary>
    [StringLength(8000, ErrorMessage = "Pipeline state is too large")]
    public string? PipelineState { get; init; }

    /// <summary>When true, a second open lead with the same email is allowed.</summary>
    public bool AllowDuplicate { get; init; }

    /// <summary>
    /// "import" for a row from a spreadsheet: someone the workspace already knows,
    /// not a new inquiry. Such a lead runs no lead.created automations (unless
    /// <see cref="RunAutomations"/> asks for them), sends no outbound webhook and
    /// starts no first-response clock. Every imported row used to be treated as a
    /// fresh inbound lead: a legacy list of 800 people got 800 welcome emails, the
    /// integration got 800 webhooks, and four hours later the owners got 800
    /// "first response is late" alerts. Null for every other way a lead arrives.
    /// </summary>
    [RegularExpression("(?i)^import$", ErrorMessage = "Origin can only be \"import\"")]
    public string? Origin { get; init; }

    /// <summary>
    /// With <see cref="Origin"/> "import": run the workspace's lead.created
    /// automations for this lead anyway, because the person importing chose to.
    /// </summary>
    public bool RunAutomations { get; init; }

    /// <summary>True when this lead comes from an import.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsImport => string.Equals(Origin?.Trim(), "import", StringComparison.OrdinalIgnoreCase);
}
