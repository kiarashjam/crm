using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Multi-step email sequence (drip campaign).
/// </summary>
public class EmailSequence : Common.BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public bool IsActive { get; set; }
    public bool IsSharedWithOrganization { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    
    public User? User { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<EmailSequenceStep> Steps { get; set; } = new List<EmailSequenceStep>();
    public ICollection<EmailSequenceEnrollment> Enrollments { get; set; } = new List<EmailSequenceEnrollment>();
}

/// <summary>
/// Individual step in an email sequence.
/// </summary>
public class EmailSequenceStep : Common.BaseEntity
{
    public Guid SequenceId { get; set; }
    public int StepOrder { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public CopyTypeId CopyTypeId { get; set; }
    public int DelayDays { get; set; } // Days to wait after previous step
    public int DelayHours { get; set; } // Hours to wait
    public bool StopOnReply { get; set; } = true;
    
    public EmailSequence? Sequence { get; set; }
}

/// <summary>
/// Enrollment of a contact/lead in a sequence.
/// </summary>
public class EmailSequenceEnrollment : Common.BaseEntity
{
    public Guid SequenceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? LeadId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public int CurrentStep { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public DateTime? LastSentAtUtc { get; set; }
    public DateTime? NextSendAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    
    public EmailSequence? Sequence { get; set; }
    public User? User { get; set; }
    public Contact? Contact { get; set; }
    public Lead? Lead { get; set; }
}

public enum EnrollmentStatus
{
    Active,
    Paused,
    Completed,
    Replied,
    Unsubscribed,
    Bounced
}
