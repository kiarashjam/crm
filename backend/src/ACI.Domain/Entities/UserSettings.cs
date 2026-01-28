using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Per-user settings: company name and brand tone for copy generation.
/// </summary>
public class UserSettings
{
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public BrandTone BrandTone { get; set; } = BrandTone.Professional;
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
