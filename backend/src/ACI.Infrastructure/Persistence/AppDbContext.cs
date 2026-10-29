using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ACI.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;

    /// <param name="options">EF options.</param>
    /// <param name="currentUser">Who is making the change, for the audit trail. Supplied by DI;
    /// absent at design time, when changes are attributed to the system.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrgSettings> OrgSettings => Set<OrgSettings>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<DealStage> DealStages => Set<DealStage>();
    public DbSet<LeadStatus> LeadStatuses => Set<LeadStatus>();
    public DbSet<LeadSource> LeadSources => Set<LeadSource>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactRelationship> ContactRelationships => Set<ContactRelationship>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealStageChange> DealStageChanges => Set<DealStageChange>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadStatusEvent> LeadStatusEvents => Set<LeadStatusEvent>();
    public DbSet<LeadScoringRule> LeadScoringRules => Set<LeadScoringRule>();
    public DbSet<LeadAssignmentRule> LeadAssignmentRules => Set<LeadAssignmentRule>();
    public DbSet<LeadTerritory> LeadTerritories => Set<LeadTerritory>();
    public DbSet<LeadQueue> LeadQueues => Set<LeadQueue>();
    public DbSet<LeadCaptureForm> LeadCaptureForms => Set<LeadCaptureForm>();
    public DbSet<LeadOpsSettings> LeadOpsSettings => Set<LeadOpsSettings>();
    public DbSet<OutreachSequence> OutreachSequences => Set<OutreachSequence>();
    public DbSet<OutreachStep> OutreachSteps => Set<OutreachStep>();
    public DbSet<OutreachEnrollment> OutreachEnrollments => Set<OutreachEnrollment>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractEvent> ContractEvents => Set<ContractEvent>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<CopyHistoryItem> CopyHistoryItems => Set<CopyHistoryItem>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValueSet> CustomFieldValues => Set<CustomFieldValueSet>();
    public DbSet<DealLineItem> DealLineItems => Set<DealLineItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ForecastSnapshot> ForecastSnapshots => Set<ForecastSnapshot>();
    public DbSet<SentMessage> SentMessages => Set<SentMessage>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<AutomationFire> AutomationFires => Set<AutomationFire>();
    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();
    public DbSet<ClubVisit> ClubVisits => Set<ClubVisit>();
    public DbSet<LoyaltyLedgerEntry> LoyaltyLedger => Set<LoyaltyLedgerEntry>();
    public DbSet<ClubDocument> ClubDocuments => Set<ClubDocument>();
    public DbSet<SalesTeam> SalesTeams => Set<SalesTeam>();
    public DbSet<SalesTeamMember> SalesTeamMembers => Set<SalesTeamMember>();

    /// <summary>
    /// Whether saving writes audit lines. Off only while an organization is copied: its
    /// history is copied with it, and each copied record would otherwise get a new
    /// "created" line of its own.
    /// </summary>
    public bool RecordsAuditTrail { get; set; } = true;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTime in this model is a UTC moment (the columns are named …Utc).
        // SQL Server hands them back as Kind=Unspecified, which System.Text.Json writes
        // without a "Z", and a browser reads that as local time: a task due at 14:00
        // UTC showed as 14:00 in Zurich. Marking them UTC on the way out fixes every
        // date the API returns at once.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Saves, writing the audit trail for every record it changes and removing a deleted
    /// record's attachments and custom field values, all in the same transaction.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampClubDocumentVersions();
        await RecordDependents.RemoveAsync(this, cancellationToken);
        var events = RecordsAuditTrail ? await AuditTrail.CaptureAsync(this, _currentUser, cancellationToken) : Array.Empty<AuditEvent>();
        AuditEvents.AddRange(events);
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            ForgetUnsaved(events);
            throw;
        }
    }

    /// <inheritdoc cref="SaveChangesAsync(bool, CancellationToken)"/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampClubDocumentVersions();
        RecordDependents.Remove(this);
        var events = RecordsAuditTrail ? AuditTrail.Capture(this, _currentUser) : Array.Empty<AuditEvent>();
        AuditEvents.AddRange(events);
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            ForgetUnsaved(events);
            throw;
        }
    }

    // Whoever writes a club record (a service, a member merge, the cloner), its version
    // moves, so a save made from an older read of it is refused. See ClubDocument.Version.
    private void StampClubDocumentVersions()
    {
        foreach (var entry in ChangeTracker.Entries<ClubDocument>())
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.Version = Guid.NewGuid();
    }

    // A failed save must not leave its audit lines queued, or a retry would record them twice.
    private void ForgetUnsaved(IReadOnlyList<AuditEvent> events)
    {
        foreach (var e in events) Entry(e).State = EntityState.Detached;
    }
}

/// <summary>Reads stored DateTimes as UTC; stores a local time converted to UTC.</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
