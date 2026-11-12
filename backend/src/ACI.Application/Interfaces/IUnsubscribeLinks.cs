namespace ACI.Application.Interfaces;

/// <summary>
/// The token in an outreach email's unsubscribe link: which workspace, which
/// address. The person clicking has no account, so the token is the whole
/// permission. It cannot be read or altered, and it does not expire: someone may
/// unsubscribe from an email that is a year old.
/// </summary>
public interface IUnsubscribeLinks
{
    string CreateToken(Guid organizationId, string email);

    bool TryRead(string token, out Guid organizationId, out string email);

    /// <summary>
    /// The token in a club member's marketing email: it withdraws that member's consent to
    /// marketing by email (and every member's at the address), and leaves their statements
    /// and booking confirmations coming. A token of one kind is never read as the other.
    /// </summary>
    string CreateMemberToken(Guid organizationId, Guid memberId, string email);

    bool TryReadMember(string token, out Guid organizationId, out Guid memberId, out string email);
}
