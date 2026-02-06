namespace ACI.Domain.Enums;

/// <summary>
/// Status of a request to join an organization.
/// </summary>
public enum JoinRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
}
