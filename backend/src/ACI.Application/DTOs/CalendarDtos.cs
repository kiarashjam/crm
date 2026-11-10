namespace ACI.Application.DTOs;

/// <summary>
/// One thing on the sales calendar.
/// </summary>
/// <param name="Kind">"task" (due then), "meeting" (a logged meeting, video call or demo) or "lead_meeting" (the meeting date on a lead's pipeline).</param>
/// <param name="StartUtc">When it starts; for an all-day entry, midnight UTC of <paramref name="Day"/>.</param>
/// <param name="EndUtc">When a meeting with a duration ends.</param>
/// <param name="AllDay">A date with no time: shown on <paramref name="Day"/> wherever the viewer is.</param>
/// <param name="Day">The calendar day ("2026-11-12") of an all-day entry.</param>
/// <param name="Status">A task's status, a meeting's outcome, or for a lead's meeting "held" or "missed".</param>
/// <param name="Detail">The lead, deal or contact it is about.</param>
/// <param name="Who">Who it is for: a task's assignee, who logged the meeting, who works the lead.</param>
public record CalendarEntryDto(
    string Kind,
    Guid Id,
    string Title,
    DateTime StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Day,
    string Link,
    string? Status,
    string? Detail,
    string? Who);

/// <param name="Truncated">A kind held more entries than were sent.</param>
public record CalendarDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string Scope,
    IReadOnlyList<CalendarEntryDto> Entries,
    bool Truncated);
