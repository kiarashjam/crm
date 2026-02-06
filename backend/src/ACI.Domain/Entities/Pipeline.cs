namespace ACI.Domain.Entities;

/// <summary>
/// Sales pipeline (e.g. Sales, Enterprise); belongs to an organization; has ordered stages.
/// </summary>
public class Pipeline : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<DealStage> DealStages { get; set; } = new List<DealStage>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
