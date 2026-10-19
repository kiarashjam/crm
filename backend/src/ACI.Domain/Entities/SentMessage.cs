namespace ACI.Domain.Entities;

/// <summary>An email the composer actually handed to the mail server.</summary>
public class SentMessage : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SentByUserId { get; set; }
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? LeadId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? DealId { get; set; }
    public DateTime SentAtUtc { get; set; }
}
