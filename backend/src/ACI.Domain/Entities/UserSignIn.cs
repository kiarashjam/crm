namespace ACI.Domain.Entities;

/// <summary>
/// A Google or Microsoft account a person signs in with (single sign-on). Linked the first
/// time the provider vouches for the person's own address; from then on the provider's
/// account (its issuer and subject) finds the person, even after the address changes there.
/// </summary>
public class UserExternalLogin : Common.BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Which provider: "google" or "microsoft" (SsoProviders).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider's issuer for the account: for Microsoft it names the person's tenant.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The provider's id for the account (the id token's "sub"), unique for one issuer.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The verified address the provider gave the last time the person signed in with it.</summary>
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// A passkey (WebAuthn credential) a person registered in Settings. Only the public half is
/// kept: the private key never leaves the person's device or password manager.
/// </summary>
public class UserPasskey : Common.BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>The credential's id, as the authenticator made it (up to 1023 bytes).</summary>
    public byte[] CredentialId { get; set; } = [];

    /// <summary>The credential's public key, COSE-encoded, that sign-ins are verified against.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>
    /// The authenticator's signature counter from the last sign-in. A sign-in reporting a value
    /// that is not higher is refused (the key may have been copied); passkeys that sync between
    /// devices always report 0, and stay at 0.
    /// </summary>
    public long SignCount { get; set; }

    /// <summary>The authenticator model's AAGUID, all zeros when it did not say.</summary>
    public Guid Aaguid { get; set; }

    /// <summary>The name the person gave it ("MacBook", "YubiKey"), to tell several apart.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the browser can reach it (usb, nfc, ble, internal, hybrid), comma-separated, as hints.</summary>
    public string? Transports { get; set; }

    /// <summary>Whether it can be synced to the person's other devices (a password manager's passkey).</summary>
    public bool IsBackupEligible { get; set; }

    /// <summary>Whether it was synced when last used.</summary>
    public bool IsBackedUp { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// A sign-in step in progress: a single sign-on round trip to Google or Microsoft, its
/// hand-back to the app, or a passkey ceremony's challenge. Each is used once, within
/// minutes, and deleted when used; one that expires unused is swept away later.
/// </summary>
/// <remarks>
/// Kept in the database rather than in one server's memory, so a step started on one
/// instance can finish on another, and survives a restart in between.
/// </remarks>
public class AuthChallenge : Common.BaseEntity
{
    /// <summary>What the step is for (AuthChallengePurposes).</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) of the secret the browser holds for the step; the secret itself is never kept.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Whose step it is, once known (a passkey added in Settings, a code handed back after sign-on).</summary>
    public Guid? UserId { get; set; }

    /// <summary>What the step needs to finish, as JSON: the provider's nonce and PKCE verifier, or the passkey options.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
