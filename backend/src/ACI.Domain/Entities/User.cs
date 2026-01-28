namespace ACI.Domain.Entities;

/// <summary>
/// Application user (auth and ownership of data).
/// </summary>
public class User : Common.BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Optional TOTP-based 2FA (e.g. Google Authenticator).
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// Protected (encrypted) Base32 TOTP secret. Do NOT store plaintext secrets.
    /// </summary>
    public string? TwoFactorSecretProtected { get; set; }

    public DateTime? TwoFactorEnabledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public UserSettings? Settings { get; set; }
    public CrmConnection? CrmConnection { get; set; }
    public ICollection<Company> Companies { get; set; } = new List<Company>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<CopyHistoryItem> CopyHistory { get; set; } = new List<CopyHistoryItem>();
}
