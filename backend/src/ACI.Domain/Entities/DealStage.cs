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

    public Pipeline Pipeline { get; set; } = null!;
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
