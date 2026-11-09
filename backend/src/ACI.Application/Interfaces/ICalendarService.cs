using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>The sales calendar: tasks by due time, logged meetings, and the meeting dates on leads.</summary>
public interface ICalendarService
{
    /// <param name="scope">"mine" (the default) or "team".</param>
    Task<Result<CalendarDto>> GetAsync(Guid userId, Guid? organizationId, DateTime fromUtc, DateTime toUtc, string? scope, CancellationToken ct = default);
}
