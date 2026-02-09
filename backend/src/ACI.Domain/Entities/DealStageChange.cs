namespace ACI.Domain.Entities;

/// <summary>
/// Tracks every stage transition for a deal.
/// </summary>
public class DealStageChange : Common.BaseEntity
{
    public Guid DealId { get; set; }
    public Guid? FromDealStageId { get; set; }
    public string? FromStageName { get; set; }
    public Guid? ToDealStageId { get; set; }
    public string? ToStageName { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    public Deal Deal { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
