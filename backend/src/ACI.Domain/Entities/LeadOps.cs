namespace ACI.Domain.Entities;

/// <summary>Who changed a lead's status, and when.</summary>
public class LeadStatusEvent : Common.BaseEntity
{
    public Guid LeadId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

/// <summary>One point rule. Field names are the calculator's vocabulary.</summary>
public class LeadScoringRule : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? MatchValue { get; set; }
    public int Points { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Round-robin, source, or territory routing.</summary>
public class LeadAssignmentRule : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>RoundRobin, Source, or Territory.</summary>
    public string Kind { get; set; } = "RoundRobin";
    public int Priority { get; set; }
    public string? SourceMatch { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? QueueId { get; set; }
    /// <summary>Comma-separated user ids. Empty means every member of the org.</summary>
    public string? MemberUserIds { get; set; }
    public int LastIndex { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LeadTerritory : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Comma-separated country names.</summary>
    public string? Countries { get; set; }
    /// <summary>Comma-separated industries.</summary>
    public string? Industries { get; set; }
}

public class LeadQueue : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MemberUserIds { get; set; }
}

public class LeadCaptureForm : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = "Website";
    public string PublicToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? SuccessMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>One row per organization. Missing row means a 4-hour first response.</summary>
public class LeadOpsSettings : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public int SlaHours { get; set; } = 4;
}

public class OutreachSequence : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<OutreachStep> Steps { get; set; } = new List<OutreachStep>();
}

public class OutreachStep : Common.BaseEntity
{
    public Guid SequenceId { get; set; }
    public int SortOrder { get; set; }
    public string Type { get; set; } = "email";
    public int DayOffset { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? TaskTitle { get; set; }
    public string? Note { get; set; }
    public OutreachSequence? Sequence { get; set; }
}

public class OutreachEnrollment : Common.BaseEntity
{
    public Guid SequenceId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid EnrolledByUserId { get; set; }
    public string TargetType { get; set; } = "lead";
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string? TargetEmail { get; set; }
    public string Status { get; set; } = "active";
    public int CurrentStep { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public DateTime? NextActionAtUtc { get; set; }
}
