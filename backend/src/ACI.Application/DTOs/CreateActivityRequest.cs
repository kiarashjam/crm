using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a new activity.
/// </summary>
public record CreateActivityRequest
{
    /// <summary>
    /// Activity type. Valid values: call, meeting, email, note, task, follow_up, deadline, video, demo, system.
    /// "system" is an update the app writes on a lead's timeline — reassigned, a
    /// follow-up done, a pipeline move. It was refused, so on the real backend none of
    /// those entries were ever saved and the lead's history had gaps.
    /// </summary>
    [Required(ErrorMessage = "Type is required")]
    [StringLength(20, ErrorMessage = "Type cannot exceed 20 characters")]
    [RegularExpression(@"(?i)^(call|meeting|email|note|task|follow_up|deadline|video|demo|system)$", 
        ErrorMessage = "Type must be one of: call, meeting, email, note, task, follow_up, deadline, video, demo, system")]
    public required string Type { get; init; }

    /// <summary>
    /// Activity subject/title (optional).
    /// </summary>
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
    public string? Subject { get; init; }

    /// <summary>
    /// Activity body/description (optional).
    /// </summary>
    [StringLength(5000, ErrorMessage = "Body cannot exceed 5000 characters")]
    public string? Body { get; init; }

    /// <summary>
    /// Related contact ID (at least one of ContactId, DealId, or LeadId is recommended).
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Related deal ID.
    /// </summary>
    public Guid? DealId { get; init; }

    /// <summary>
    /// Related lead ID.
    /// </summary>
    public Guid? LeadId { get; init; }

    /// <summary>
    /// Comma-separated list of participants (optional).
    /// </summary>
    [StringLength(500, ErrorMessage = "Participants cannot exceed 500 characters")]
    public string? Participants { get; init; }

    /// <summary>
    /// Logged by the system on someone's behalf — a sequence email — rather than
    /// done by a person. Such activity is recorded, but it is not a "first
    /// response": an automated day-0 email used to clear the response deadline, so
    /// the alert that nobody had called a new lead never fired for enrolled leads.
    /// Not bindable from a request body; only server code can set it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAutomated { get; init; }
}
