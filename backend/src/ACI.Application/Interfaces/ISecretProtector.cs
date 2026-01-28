namespace ACI.Application.Interfaces;

/// <summary>
/// Protect/unprotect secrets for storage (e.g. 2FA secrets).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

