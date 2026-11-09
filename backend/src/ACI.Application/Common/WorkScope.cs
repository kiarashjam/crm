using System.Linq.Expressions;
using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// Whose work a task, lead or deal is: the rep's own ("mine") or anyone's in the
/// workspace ("team"). One rule, so the Tasks page, the assistant's queue and the
/// calendar agree on what "my work" holds.
/// </summary>
/// <remarks>
/// A record is someone's when it is given to them, or, while it is given to nobody,
/// when they made it: an unassigned task or lead is its maker's to do or to hand on.
/// A web lead nobody has routed is so the workspace owner's, whose key brought it in,
/// and an import's rows the importer's until they are handed out.
/// </remarks>
public static class WorkScope
{
    public const string Mine = "mine";
    public const string Team = "team";

    /// <summary>"mine" means mine; anything else, or nothing, is the whole team, as every list was before.</summary>
    public static bool IsMine(string? scope) => string.Equals(scope?.Trim(), Mine, StringComparison.OrdinalIgnoreCase);

    public static Expression<Func<TaskItem, bool>> TaskOf(Guid userId) =>
        t => t.AssigneeId == userId || (t.AssigneeId == null && t.UserId == userId);

    public static Expression<Func<Lead, bool>> LeadOf(Guid userId) =>
        l => l.AssignedToUserId == userId || (l.AssignedToUserId == null && l.UserId == userId);

    public static Expression<Func<Deal, bool>> DealOf(Guid userId) =>
        d => d.AssigneeId == userId || (d.AssigneeId == null && d.UserId == userId);
}
