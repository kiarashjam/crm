using ACI.Domain.Entities;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.Application.Services;

/// <summary>
/// The single place that decides whether a task warrants a reminder, and what it
/// should say. Both delivery channels — email and the in-app bell — consult this,
/// so a task can never be considered reminder-worthy by one and not the other for
/// reasons other than the user's own preferences.
///
/// The two channels deliberately fire on DIFFERENT signals, which is why this is a
/// policy rather than a single boolean:
///   · Email  — the explicit <see cref="TaskItem.ReminderDateUtc"/> the user set.
///              A point-in-time nudge they asked for.
///   · In-app — the task's own <see cref="TaskItem.DueDateUtc"/> falling due or
///              passing. The bell is a standing list, so it should reflect what is
///              actually outstanding whether or not a reminder time was set.
/// Collapsing those into one trigger would either spam email for every due task or
/// leave the bell blank for tasks with no reminder time.
/// </summary>
public static class TaskReminderPolicy
{
    /// <summary>How far ahead of its due date a task counts as "due" for the bell.</summary>
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(1);

    /// <summary>Reminders go to whoever the task is assigned to, else its owner.</summary>
    public static Guid RecipientId(TaskItem task) => task.AssigneeId ?? task.UserId;

    /// <summary>A task nobody needs chasing about any more.</summary>
    public static bool IsClosed(TaskItem task) =>
        task.Status == TaskStatusEnum.Completed
        || task.Status == TaskStatusEnum.Cancelled
        || task.Completed;

    /// <summary>The user asked to be emailed at a specific time, and we have not yet.</summary>
    public static bool NeedsEmailReminder(TaskItem task, DateTime nowUtc) =>
        !IsClosed(task)
        && task.ReminderDateUtc != null
        && task.ReminderDateUtc <= nowUtc
        && task.ReminderSentAtUtc == null;

    /// <summary>The task is overdue, or falls due inside the window.</summary>
    public static bool NeedsInAppReminder(TaskItem task, DateTime nowUtc) =>
        !IsClosed(task)
        && task.DueDateUtc != null
        && task.DueDateUtc <= nowUtc.Add(DueSoonWindow);

    public static bool IsOverdue(TaskItem task, DateTime nowUtc) =>
        task.DueDateUtc != null && task.DueDateUtc < nowUtc;

    /// <summary>
    /// Stable identity for a generated in-app reminder, so the generator can run
    /// every few minutes without piling up duplicates.
    ///
    /// The overdue/due distinction is part of the key on purpose: a task that slips
    /// from "due today" to "overdue" is a genuinely new thing to tell someone, so it
    /// should raise a second notification rather than be suppressed as a duplicate.
    /// </summary>
    public static string SourceKey(TaskItem task, bool overdue) =>
        $"task-{(overdue ? "overdue" : "due")}-{task.Id}";

    public static string Title(bool overdue) => overdue ? "Task overdue" : "Task due today";

    /// <summary>Deep-link to the most specific record the task hangs off.</summary>
    public static string Link(TaskItem task)
    {
        if (task.LeadId != null) return $"/leads/{task.LeadId}";
        if (task.DealId != null) return $"/deals/{task.DealId}";
        if (task.ContactId != null) return $"/contacts/{task.ContactId}";
        return "/tasks";
    }
}
