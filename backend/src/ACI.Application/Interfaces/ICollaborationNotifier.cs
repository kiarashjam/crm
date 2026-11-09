using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// Tells teammates in the bell when work comes their way: a task, lead or deal handed
/// to them, a comment on a task they own or work, a mention in a comment or a logged note.
/// </summary>
/// <remarks>
/// Each call is made after the change it is about has been saved, and never fails it:
/// a notification that cannot be written is logged and dropped, as the change stands.
/// Nobody is told about their own doing, nobody who has left the workspace, nobody who
/// turned in-app notifications off, and nobody who cannot open the private deal the
/// work is on.
/// </remarks>
public interface ICollaborationNotifier
{
    /// <summary>
    /// A task was given to someone other than whoever gave it. <paramref name="madeBy"/>
    /// names the automation or sequence that made it, in place of the person who set it off.
    /// </summary>
    Task TaskAssignedAsync(TaskItem task, Guid actorId, string? madeBy = null, CancellationToken ct = default);

    /// <summary>Several tasks were given to one person at once: one line for them all.</summary>
    Task TasksAssignedAsync(Guid assigneeId, IReadOnlyList<TaskItem> tasks, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// A lead or a deal (<paramref name="recordType"/>, a <c>RecordTypes</c> value) was
    /// given to someone other than whoever gave it. <paramref name="madeBy"/> names what
    /// did it when no person did: "lead routing".
    /// </summary>
    Task RecordAssignedAsync(string recordType, Guid recordId, string recordName, Guid? organizationId, Guid? assigneeId, Guid actorId, string? madeBy = null, CancellationToken ct = default);

    /// <summary>Several leads or deals were given to one person at once: one line for them all.</summary>
    Task RecordsAssignedAsync(string recordType, Guid assigneeId, Guid? organizationId, IReadOnlyList<(Guid Id, string Name)> records, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Someone commented on a task. Whoever it mentions hears of the mention; its
    /// assignee and the person who made it hear of the comment.
    /// </summary>
    Task TaskCommentedAsync(TaskItem task, Guid commentId, string body, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// A logged activity, a note or a call, mentions people. On an edit,
    /// <paramref name="textBefore"/> is what it said before, and only those it newly
    /// mentions are told.
    /// </summary>
    Task ActivityMentionsAsync(Activity activity, string? recordName, string? textBefore, Guid actorId, CancellationToken ct = default);
}
