namespace ACI.Domain.Entities;

public class SalesTeam : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class SalesTeamMember
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
}
