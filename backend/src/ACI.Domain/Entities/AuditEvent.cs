namespace ACI.Domain.Entities;

/// <summary>
/// One line of the audit trail: who did what to which record, and when.
/// </summary>
/// <remarks>
/// Append-only. Most rows are written by the server as part of the same save
/// that changed the record (see <c>AuditTrail</c> in Infrastructure), so they
/// cannot drift from what actually happened. The actor and time are always set
/// by the server, never taken from a client.
/// </remarks>
public class AuditEvent : Common.BaseEntity
{
    /// <summary>The workspace the record belongs to; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The record's owner. Scopes a personal workspace's history to that user.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>One of <see cref="AuditVocabulary.EntityTypes"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>The record's name at the time, so the line still reads after a delete.</summary>
    public string? EntityLabel { get; set; }

    /// <summary>One of <see cref="AuditVocabulary.Actions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    /// <summary>Null when the change had no signed-in user behind it (a worker, a public form).</summary>
    public Guid? ActorUserId { get; set; }

    public string ActorName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// The words the audit trail uses. They match the client's <c>AuditEntity</c> and
/// <c>AuditAction</c> types (src/app/api/audit.ts), which render them.
/// </summary>
public static class AuditVocabulary
{
    public static readonly IReadOnlySet<string> EntityTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "lead", "contact", "company", "deal", "task", "sequence", "automation",
    };

    public static readonly IReadOnlySet<string> Actions = new HashSet<string>(StringComparer.Ordinal)
    {
        "created", "updated", "deleted", "status_changed", "assigned", "email_sent", "merged", "converted",
    };

    /// <summary>The actor name for a change with no signed-in user behind it.</summary>
    public const string SystemActor = "System";
}
