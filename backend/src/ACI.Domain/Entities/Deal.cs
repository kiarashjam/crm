namespace ACI.Domain.Entities;

/// <summary>
/// Deal (opportunity) owned by a user; optionally linked to a company.
/// </summary>
public class Deal : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Company? Company { get; set; }
}
