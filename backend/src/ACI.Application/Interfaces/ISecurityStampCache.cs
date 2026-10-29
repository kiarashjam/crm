namespace ACI.Application.Interfaces;

/// <summary>
/// The short-lived copy of each user's security stamp that the bearer handler checks
/// every session token against (see <c>User.SecurityStamp</c>).
/// </summary>
public interface ISecurityStampCache
{
    /// <summary>
    /// Drop the cached stamp after it changes, so a revoked token stops working on
    /// the next request instead of when the cache entry runs out.
    /// </summary>
    void Forget(Guid userId);
}
