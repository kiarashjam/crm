namespace ACI.Domain.Entities;

/// <summary>
/// A dismissible alert surfaced in the header bell: a task reminder, an @mention,
/// a deal change, or a system message.
///
/// Scoped to a single recipient (<see cref="UserId"/>) rather than to a whole
/// organization: a notification is something one person has or has not read, so
/// "unread" is meaningless org-wide. <see cref="OrganizationId"/> is recorded so
/// switching workspaces does not show another workspace's alerts, and follows the
/// same nullable convention as <see cref="Activity"/> (null = personal scope).
/// </summary>
public class Notification : Common.BaseEntity
{
    /// <summary>The recipient. Notifications are never shared between users.</summary>
    public Guid UserId { get; set; }

    /// <summary>Workspace the notification belongs to; null for personal scope.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>One of: task, deal, lead, mention, system.</summary>
    public string Type { get; set; } = "system";

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    /// <summary>In-app route to open when clicked, e.g. "/leads/{id}".</summary>
    public string? Link { get; set; }

    public bool Read { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Stable key identifying what produced this notification, e.g.
    /// "task-overdue-{taskId}". Unique per user so a reminder generator can run
    /// repeatedly without piling up duplicates for the same underlying event.
    /// </summary>
    public string? SourceKey { get; set; }

    public User User { get; set; } = null!;
    public Organization? Organization { get; set; }
}
