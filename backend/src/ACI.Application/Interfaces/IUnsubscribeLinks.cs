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
}
