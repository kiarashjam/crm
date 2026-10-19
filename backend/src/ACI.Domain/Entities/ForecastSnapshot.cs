namespace ACI.Domain.Entities;

/// <summary>
/// One saved line of a month's forecast. It does not change when a deal is edited later.
/// </summary>
public class ForecastSnapshot : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SavedByUserId { get; set; }
    public DateTime MonthUtc { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? AssigneeId { get; set; }
    public string AssigneeName { get; set; } = "Unassigned";
    public decimal WeightedValue { get; set; }
    public decimal OpenValue { get; set; }
    public int Covered { get; set; }
    public int Missing { get; set; }
    public DateTime SavedAtUtc { get; set; }
}
