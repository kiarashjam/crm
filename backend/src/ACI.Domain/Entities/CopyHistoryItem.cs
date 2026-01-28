using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Record of generated/sent copy for history and dashboard stats.
/// </summary>
public class CopyHistoryItem : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Copy { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public RecipientType RecipientType { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
