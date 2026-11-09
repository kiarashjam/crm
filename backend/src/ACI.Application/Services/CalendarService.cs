using System.Globalization;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// The sales side of the calendar: tasks at the time they are due, the meetings, video
/// calls and demos logged as activities, and the meeting date each lead's pipeline holds.
/// </summary>
/// <remarks>
/// <para>
/// The calendar showed club events and reservations only, so a rep's day of calls and
/// meetings lived in three other pages. Club events and reservations still come from
/// their own endpoints; this adds what a rep plans and holds.
/// </para>
/// <para>
/// "Mine" is the rep's tasks (see WorkScope), the meetings they logged, and the meetings
/// of their leads; "team" is everyone's, less what sits on a private deal they cannot
/// open. A lead's meeting date is a day with no time, so it comes as an all-day entry
/// on that day, wherever the viewer is.
/// </para>
/// </remarks>
public sealed class CalendarService : ICalendarService
{
    /// <summary>
    /// The longest span read at once: a month page shows six weeks (42 days). With a day
    /// either side for the all-day entries, that is never more than three months, which
    /// the lead query is written for.
    /// </summary>
    public const int MaxDays = 45;
    public const int MaxPerKind = 1000;

    private readonly ITaskRepository _tasks;
    private readonly IActivityRepository _activities;
    private readonly ILeadRepository _leads;

    public CalendarService(ITaskRepository tasks, IActivityRepository activities, ILeadRepository leads)
    {
        _tasks = tasks;
        _activities = activities;
        _leads = leads;
    }

    public async Task<Result<CalendarDto>> GetAsync(Guid userId, Guid? organizationId, DateTime fromUtc, DateTime toUtc, string? scope, CancellationToken ct = default)
    {
        var from = ActivityFacts.Utc(fromUtc);
        var to = ActivityFacts.Utc(toUtc);
        if (to <= from || (to - from).TotalDays > MaxDays) return DomainErrors.Calendar.InvalidRange;
        var mine = !string.Equals(scope?.Trim(), WorkScope.Team, StringComparison.OrdinalIgnoreCase);

        var tasks = await _tasks.GetDueBetweenAsync(userId, organizationId, from, to, mine, MaxPerKind + 1, ct);
        var meetings = await _activities.GetMeetingsBetweenAsync(userId, organizationId, from, to, mine, MaxPerKind + 1, ct);
        // A day either side: the page's first and last day are local days, which begin
        // and end on either side of UTC midnight depending on where the viewer is.
        var firstDay = DateOnly.FromDateTime(from).AddDays(-1);
        var lastDay = DateOnly.FromDateTime(to).AddDays(1);
        var leads = await _leads.GetWithMeetingInMonthsAsync(userId, organizationId, Months(firstDay, lastDay), mine, MaxPerKind * 2, ct);
        var leadMeetings = leads
            .Select(lead => (Lead: lead, Meeting: MeetingOf(lead.PipelineState)))
            .Where(x => x.Meeting is { } m && m.Day >= firstDay && m.Day <= lastDay)
            .ToList();

        var entries = tasks.Take(MaxPerKind).Select(TaskEntry)
            .Concat(meetings.Take(MaxPerKind).Select(MeetingEntry))
            .Concat(leadMeetings.Take(MaxPerKind).Select(x => LeadMeetingEntry(x.Lead, x.Meeting!.Value)))
            .OrderBy(e => e.StartUtc).ThenBy(e => e.Title, StringComparer.Ordinal)
            .ToList();
        var truncated = tasks.Count > MaxPerKind || meetings.Count > MaxPerKind || leadMeetings.Count > MaxPerKind;
        return new CalendarDto(from, to, mine ? WorkScope.Mine : WorkScope.Team, entries, truncated);
    }

    private static CalendarEntryDto TaskEntry(TaskItem t) => new(
        "task", t.Id, t.Title, t.DueDateUtc!.Value, null, false, null, $"/tasks/{t.Id}",
        t.Status switch
        {
            Domain.Enums.TaskStatus.InProgress => "in_progress",
            Domain.Enums.TaskStatus.Completed => "completed",
            _ => "todo",
        },
        t.Lead?.Name ?? t.Deal?.Name ?? t.Contact?.Name,
        t.Assignee?.Name);

    private static CalendarEntryDto MeetingEntry(Activity a) => new(
        "meeting", a.Id,
        string.IsNullOrWhiteSpace(a.Subject) ? Capitalized(a.Type) : a.Subject.Trim(),
        a.OccurredAtUtc,
        a.DurationMinutes is int minutes ? a.OccurredAtUtc.AddMinutes(minutes) : null,
        false, null,
        a.LeadId is Guid lead ? $"/leads/{lead}" : a.DealId is Guid deal ? $"/deals/{deal}" : a.ContactId is Guid contact ? $"/contacts/{contact}" : "/activities",
        a.Outcome,
        a.Lead?.Name ?? a.Deal?.Name ?? a.Contact?.Name,
        a.User?.Name);

    private static CalendarEntryDto LeadMeetingEntry(Lead lead, (DateOnly Day, bool? Attended) meeting) => new(
        "lead_meeting", lead.Id, $"Meeting with {lead.Name}",
        meeting.Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), null,
        true, meeting.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        $"/leads/{lead.Id}",
        meeting.Attended switch { true => "held", false => "missed", _ => null },
        null,
        lead.AssignedToUser?.Name);

    /// <summary>"2026-10", "2026-11", … from the month of <paramref name="first"/> to that of <paramref name="last"/>.</summary>
    internal static IReadOnlyList<string> Months(DateOnly first, DateOnly last)
    {
        var months = new List<string>();
        for (var month = new DateOnly(first.Year, first.Month, 1); month <= last; month = month.AddMonths(1))
            months.Add(month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        return months;
    }

    /// <summary>
    /// The meeting date a lead's pipeline holds ("2026-11-12", maybe with a time after it,
    /// which is not read), and whether they came. Null for anything that is not one.
    /// </summary>
    internal static (DateOnly Day, bool? Attended)? MeetingOf(string? pipelineState)
    {
        if (string.IsNullOrWhiteSpace(pipelineState)) return null;
        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetBytes(pipelineState));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("meetingDate", out var date)
                || date.ValueKind != JsonValueKind.String) return null;
            var text = date.GetString() ?? "";
            if (text.Length < 10 || !DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return null;
            bool? attended = doc.RootElement.TryGetProperty("meetingAttended", out var came) && came.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? came.GetBoolean()
                : null;
            return (day, attended);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Capitalized(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
}
