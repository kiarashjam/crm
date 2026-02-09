namespace ACI.Domain.Entities;

/// <summary>
/// A timestamped comment on a task, authored by a user.
/// </summary>
public class TaskComment : Common.BaseEntity
{
    public Guid TaskItemId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
    public User Author { get; set; } = null!;
}
