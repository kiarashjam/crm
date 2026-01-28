using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Copy template (system or user-owned) with goal and copy type.
/// </summary>
public class Template : Common.BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public CopyTypeId CopyTypeId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User? User { get; set; }
}
