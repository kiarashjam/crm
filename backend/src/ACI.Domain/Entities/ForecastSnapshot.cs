namespace ACI.Domain.Entities;

/// <summary>
/// One saved line of a month's forecast. It does not change when a deal is edited later.
/// </summary>
/// <remarks>
/// A save writes one line per owner and currency, all sharing a <see cref="SnapshotId"/>.
/// Saves are kept: saving again adds a new set of lines beside the earlier ones, so what
/// was forecast on the 1st can still be set against what closed by the 30th.
/// </remarks>
public class ForecastSnapshot : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SavedByUserId { get; set; }
    /// <summary>The month forecast, as its first day.</summary>
    public DateTime MonthUtc { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? AssigneeId { get; set; }
    public string AssigneeName { get; set; } = "Unassigned";
    /// <summary>Weighted value of the owner's open deals expected to close in the month.</summary>
    public decimal WeightedValue { get; set; }
    /// <summary>Full value of those deals.</summary>
    public decimal OpenValue { get; set; }
    /// <summary>Those deals with a probability.</summary>
    public int Covered { get; set; }
    /// <summary>Those deals without one.</summary>
    public int Missing { get; set; }
    public DateTime SavedAtUtc { get; set; }
    /// <summary>The save this line belongs to.</summary>
    public Guid SnapshotId { get; set; }
    /// <summary>
    /// True for lines that count only deals expected to close in the month. Lines saved
    /// before that counted every open deal, and are false.
    /// </summary>
    public bool ByCloseDate { get; set; }
    /// <summary>Open deals whose expected close date was before the month.</summary>
    public int OverdueCount { get; set; }
    public decimal OverdueValue { get; set; }
    /// <summary>Open deals with no expected close date.</summary>
    public int NoCloseDateCount { get; set; }
    /// <summary>Deals expected in the month whose value could not be read.</summary>
    public int UnreadableCount { get; set; }
}
