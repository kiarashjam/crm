namespace ACI.Domain.Entities;

/// <summary>
/// Sales pipeline (e.g. Sales, Enterprise); belongs to an organization; has ordered stages.
/// </summary>
public class Pipeline : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    /// <summary>
    /// Days without activity after which an open deal in this pipeline is flagged as
    /// stale. Null: the workspace's <see cref="Organization.StaleDealDays"/>; 0: never.
    /// </summary>
    public int? StaleDealDays { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<DealStage> DealStages { get; set; } = new List<DealStage>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
