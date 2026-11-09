using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing activity.
/// </summary>
public record UpdateActivityRequest
{
    /// <summary>
    /// Activity type. Valid values: call, meeting, email, note, task, follow_up, deadline, video, demo, system.
    /// </summary>
    [StringLength(20, ErrorMessage = "Type cannot exceed 20 characters")]
    [RegularExpression(@"(?i)^(call|meeting|email|note|task|follow_up|deadline|video|demo|system)$",
        ErrorMessage = "Type must be one of: call, meeting, email, note, task, follow_up, deadline, video, demo, system")]
    public string? Type { get; init; }

    /// <summary>
    /// Activity subject/title.
    /// </summary>
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
    public string? Subject { get; init; }

    /// <summary>
    /// Activity body/description.
    /// </summary>
    [StringLength(5000, ErrorMessage = "Body cannot exceed 5000 characters")]
    public string? Body { get; init; }

    /// <summary>
    /// Comma-separated list of participants.
    /// </summary>
    [StringLength(500, ErrorMessage = "Participants cannot exceed 500 characters")]
    public string? Participants { get; init; }

    /// <summary>When it happened. Never in the future.</summary>
    public DateTime? OccurredAtUtc { get; init; }

    /// <summary>How a call or meeting went; see CreateActivityRequest.Outcome.</summary>
    [StringLength(32)]
    public string? Outcome { get; init; }

    /// <summary>How long it took, in minutes (0 to 1440).</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>inbound or outbound, for a call or an email.</summary>
    [StringLength(16)]
    public string? Direction { get; init; }

    /// <summary>Set to true to remove the outcome.</summary>
    public bool? ClearOutcome { get; init; }

    /// <summary>Set to true to remove the duration.</summary>
    public bool? ClearDuration { get; init; }

    /// <summary>Set to true to remove the direction.</summary>
    public bool? ClearDirection { get; init; }
}
