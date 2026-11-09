namespace ACI.Domain.Entities;

/// <summary>
/// Deal (opportunity) owned by a user; optionally linked to a company.
/// </summary>
public class Deal : Common.BaseEntity, Common.IVersioned
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// <see cref="Amount"/> as text ("85500", "99.40"), written from it on every save, for
    /// older clients, the MCP tools and outbound webhooks. Deals from before amounts were
    /// numbers kept whatever was typed here.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>
    /// The deal's money, to the cent, in <see cref="Currency"/>. Reports add it up in the
    /// database, per currency. Null only for a deal saved before amounts were numbers
    /// whose text held no readable number.
    /// </summary>
    public decimal? Amount { get; set; }
    /// <summary>ISO 4217 code of <see cref="Amount"/>; a new deal takes its workspace's.</summary>
    public string? Currency { get; set; }
    public string? Stage { get; set; }
    public Guid? PipelineId { get; set; }
    public Guid? DealStageId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? ExpectedCloseDateUtc { get; set; }
    public string? Description { get; set; }
    /// <summary>Win probability 0–100.</summary>
    public int? Probability { get; set; }
    /// <summary>Null = open, true = won, false = lost.</summary>
    public bool? IsWon { get; set; }
    /// <summary>Reason the deal was closed (won/lost).</summary>
    public string? ClosedReason { get; set; }
    /// <summary>Category: Price, Timing, Competitor, NoDecision, FeatureGap, ChampionLeft, Other.</summary>
    public string? ClosedReasonCategory { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    /// <summary>Hidden from other salespeople. Managers and the owner still see it.</summary>
    public bool IsPrivate { get; set; }
    public Guid? TeamId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// A new value on every save (see <see cref="Common.IVersioned"/>). The API returns it,
    /// and an edit that sends back the one it was made from is refused once someone else
    /// has saved the deal since, instead of writing that person's changes over.
    /// </summary>
    public Guid Version { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public User? Assignee { get; set; }
    public Organization? Organization { get; set; }
    public Pipeline? Pipeline { get; set; }
    public DealStage? DealStage { get; set; }
    public Company? Company { get; set; }
    public Contact? Contact { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
    public ICollection<DealStageChange> StageChanges { get; set; } = new List<DealStageChange>();
}
