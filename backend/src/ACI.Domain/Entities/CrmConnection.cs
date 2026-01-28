namespace ACI.Domain.Entities;

/// <summary>
/// CRM connection status and credentials per user.
/// </summary>
public class CrmConnection
{
    public Guid UserId { get; set; }
    public bool Connected { get; set; }
    public string? AccountEmail { get; set; }
    public string? EncryptedToken { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
