namespace ACI.Domain.Entities;

/// <summary>
/// Stage within a pipeline (e.g. Qualification, Proposal, Closed Won); org-configurable.
/// </summary>
public class DealStage : Common.BaseEntity
{
    public Guid PipelineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    /// <summary>True if this stage represents a won deal.</summary>
    public bool IsWon { get; set; }
    /// <summary>True if this stage represents a lost deal.</summary>
    public bool IsLost { get; set; }
    /// <summary>
    /// The chance, 0–100, that a deal in this stage is won, for a deal that has no
    /// probability of its own; the weighted pipeline reads it. Null: the stage has none,
    /// and such a deal is counted as having no probability rather than given one.
    /// </summary>
    public int? Probability { get; set; }

    public Pipeline Pipeline { get; set; } = null!;
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
