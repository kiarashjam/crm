namespace ACI.Domain.Entities;

/// <summary>
/// Contact (person) owned by a user; optionally linked to a company.
/// </summary>
public class Contact : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Company? Company { get; set; }
}
