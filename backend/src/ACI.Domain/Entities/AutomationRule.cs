namespace ACI.Domain.Entities;

/// <summary>A workspace rule: when something happens, do the stored actions.</summary>
public class AutomationRule : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Trigger { get; set; } = string.Empty;
    public string? TriggerConfigJson { get; set; }
    public string ActionsJson { get; set; } = "[]";
    public int RunCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Stops one rule from doing the same thing twice for the same record.</summary>
public class AutomationFire : Common.BaseEntity
{
    public Guid RuleId { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public DateTime FiredAtUtc { get; set; }
}
