namespace ACI.Domain.Entities;

/// <summary>
/// A prepared email the workspace keeps, in one version per language, for the composer
/// to start from: pick it, pick the language, and its subject and text come up ready to
/// change and send.
/// </summary>
/// <remarks>
/// The text may hold the placeholders the outbox fills for each recipient ({{firstName}},
/// {{company}} and the rest, see MergeFields) and blanks for the sender to fill in before
/// sending ("[[date and time]]"), which the composer will not send while any is left.
/// </remarks>
public class EmailTemplate : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>What it is for, shown as a group in the composer ("First contact", "Club").</summary>
    public string? Category { get; set; }
    /// <summary>When to use it, in a line.</summary>
    public string? Description { get; set; }
    /// <summary>
    /// For one of the starter set the workspace was given (see StarterEmailTemplates), which
    /// one; null for the workspace's own. Unique per workspace, so the set is given once.
    /// </summary>
    public string? StarterKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<EmailTemplateVersion> Versions { get; set; } = new();
}

/// <summary>A prepared email in one language: its subject and text.</summary>
public class EmailTemplateVersion : Common.BaseEntity
{
    public Guid EmailTemplateId { get; set; }
    /// <summary>"fr" or "en" (see EmailTemplateLanguages).</summary>
    public string Language { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
