namespace ACI.Domain.Enums;

/// <summary>
/// Role of a user in an organization.
/// </summary>
public enum OrgMemberRole
{
    Owner = 0,
    Member = 1,   // Salesperson (default): CRUD records; cannot change pipeline/stages/org settings
    Manager = 2, // Optional: see/assign all deals/leads; edit pipelines
}
