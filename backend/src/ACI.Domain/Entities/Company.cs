namespace ACI.Domain.Entities;

/// <summary>
/// Company (organization) owned by a user; contacts and deals can be linked.
/// </summary>
public class Company : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
