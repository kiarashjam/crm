using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ACI.Infrastructure.Persistence;

public partial class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;
    private readonly ITenantContext _tenant;

    /// <param name="options">EF options.</param>
    /// <param name="currentUser">Who is making the change, for the audit trail. Supplied by DI;
    /// absent at design time, when changes are attributed to the system.</param>
    /// <param name="tenant">The workspace reads are filtered to (see <see cref="TenantOrganizationId"/>).
    /// Supplied by DI. A context made without one, as a tool or a test makes it, starts in no
    /// workspace and reads no workspace's records until it acts for one (<see cref="ActFor"/>),
    /// except where a query ignores the filters.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null, ITenantContext? tenant = null) : base(options)
    {
        _currentUser = currentUser;
        _tenant = tenant ?? new ActingWorkspace();
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();
    public DbSet<AuthChallenge> AuthChallenges => Set<AuthChallenge>();
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
    public DbSet<ContractDeposit> ContractDeposits => Set<ContractDeposit>();
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
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<OutboundAttachment> OutboundAttachments => Set<OutboundAttachment>();
    public DbSet<EmailSuppression> EmailSuppressions => Set<EmailSuppression>();
    public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<AutomationFire> AutomationFires => Set<AutomationFire>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<AutomationRunStep> AutomationRunSteps => Set<AutomationRunStep>();
    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();
    public DbSet<OnboardingSettings> OnboardingSettings => Set<OnboardingSettings>();
    public DbSet<DealOnboarding> DealOnboardings => Set<DealOnboarding>();
    public DbSet<EmailImage> EmailImages => Set<EmailImage>();
    // The member portal: its settings, each member's use of it, their sessions and sign-in links.
    public DbSet<MemberPortalSettings> MemberPortalSettings => Set<MemberPortalSettings>();
    public DbSet<MemberPortalAccount> MemberPortalAccounts => Set<MemberPortalAccount>();
    public DbSet<MemberPortalSession> MemberPortalSessions => Set<MemberPortalSession>();
    public DbSet<MemberPortalLink> MemberPortalLinks => Set<MemberPortalLink>();
    public DbSet<ClubVisit> ClubVisits => Set<ClubVisit>();
    public DbSet<LoyaltyLedgerEntry> LoyaltyLedger => Set<LoyaltyLedgerEntry>();
    public DbSet<ClubDocument> ClubDocuments => Set<ClubDocument>();
    public DbSet<ClubHouseAccount> ClubHouseAccounts => Set<ClubHouseAccount>();
    public DbSet<ClubCharge> ClubCharges => Set<ClubCharge>();
    public DbSet<ClubPayment> ClubPayments => Set<ClubPayment>();
    public DbSet<ClubPaymentAllocation> ClubPaymentAllocations => Set<ClubPaymentAllocation>();
    public DbSet<ClubSubscription> ClubSubscriptions => Set<ClubSubscription>();
    public DbSet<ClubGiftCard> ClubGiftCards => Set<ClubGiftCard>();
    public DbSet<ClubGiftCardTransaction> ClubGiftCardTransactions => Set<ClubGiftCardTransaction>();
    public DbSet<ClubTab> ClubTabs => Set<ClubTab>();
    public DbSet<ClubTabItem> ClubTabItems => Set<ClubTabItem>();
    public DbSet<ClubDonation> ClubDonations => Set<ClubDonation>();
    // The price catalogue and what the billing run records.
    public DbSet<ClubCatalogItem> ClubCatalogItems => Set<ClubCatalogItem>();
    public DbSet<ClubBillingRun> ClubBillingRuns => Set<ClubBillingRun>();
    public DbSet<ClubPaymentReminder> ClubPaymentReminders => Set<ClubPaymentReminder>();
    public DbSet<ClubStatementDelivery> ClubStatementDeliveries => Set<ClubStatementDelivery>();
    // Club events with their guest lists, and the bookable resources with their bookings.
    public DbSet<ClubEvent> ClubEvents => Set<ClubEvent>();
    public DbSet<ClubEventRegistration> ClubEventRegistrations => Set<ClubEventRegistration>();
    public DbSet<ClubResource> ClubResources => Set<ClubResource>();
    public DbSet<ClubReservation> ClubReservations => Set<ClubReservation>();
    // How people join (applications and the public form) and who belongs together (households).
    public DbSet<ClubApplication> ClubApplications => Set<ClubApplication>();
    public DbSet<ClubApplicationForm> ClubApplicationForms => Set<ClubApplicationForm>();
    public DbSet<ClubHousehold> ClubHouseholds => Set<ClubHousehold>();
    public DbSet<ClubHouseholdMember> ClubHouseholdMembers => Set<ClubHouseholdMember>();
    // Money from outside the club (Stripe, bank files with QR references) and the accounting
    // settings: what a payment provider or the bank said, kept beside the ledger it posts to.
    public DbSet<ClubPaymentSettings> ClubPaymentSettings => Set<ClubPaymentSettings>();
    public DbSet<ClubPaymentProfile> ClubPaymentProfiles => Set<ClubPaymentProfile>();
    public DbSet<ClubOnlinePayment> ClubOnlinePayments => Set<ClubOnlinePayment>();
    public DbSet<ClubProviderEvent> ClubProviderEvents => Set<ClubProviderEvent>();
    public DbSet<ClubPaymentReference> ClubPaymentReferences => Set<ClubPaymentReference>();
    public DbSet<ClubBankImport> ClubBankImports => Set<ClubBankImport>();
    public DbSet<ClubBankTransaction> ClubBankTransactions => Set<ClubBankTransaction>();
    // What members buy at the bars, restaurants and shop: sales posted through the ledger, the
    // POS's closed tickets with where each stands, the POS connection, and the codes that name
    // a member at the POS.
    public DbSet<ClubSale> ClubSales => Set<ClubSale>();
    public DbSet<ClubSaleLine> ClubSaleLines => Set<ClubSaleLine>();
    public DbSet<ClubMemberCode> ClubMemberCodes => Set<ClubMemberCode>();
    public DbSet<ClubPosSettings> ClubPosSettings => Set<ClubPosSettings>();
    public DbSet<ClubPosTicket> ClubPosTickets => Set<ClubPosTicket>();
    public DbSet<SalesTeam> SalesTeams => Set<SalesTeam>();
    public DbSet<SalesTeamMember> SalesTeamMembers => Set<SalesTeamMember>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<DuplicateDecision> DuplicateDecisions => Set<DuplicateDecision>();

    // Member communications and consent.
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<MemberMessage> MemberMessages => Set<MemberMessage>();
    // A workspace's privacy settings, its log of erasures, and what its retention runs removed.
    public DbSet<PrivacySettings> PrivacySettings => Set<PrivacySettings>();
    public DbSet<PersonErasure> PersonErasures => Set<PersonErasure>();
    public DbSet<RetentionRun> RetentionRuns => Set<RetentionRun>();
    // The assistant: what each workspace's calls to the language model came to, its settings,
    // and when each person's morning brief went out.
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    public DbSet<AssistantSettings> AssistantSettings => Set<AssistantSettings>();
    public DbSet<AssistantBrief> AssistantBriefs => Set<AssistantBrief>();
    // Keeping members and growing through them: the rules, each member's at-risk score, the
    // renewals board's records, referral bonuses, concerns recorded about members, and the
    // daily scoring runs.
    public DbSet<ClubRetentionSettings> ClubRetentionSettings => Set<ClubRetentionSettings>();
    public DbSet<ClubMemberRisk> ClubMemberRisks => Set<ClubMemberRisk>();
    public DbSet<ClubRenewal> ClubRenewals => Set<ClubRenewal>();
    public DbSet<ClubReferralBonus> ClubReferralBonuses => Set<ClubReferralBonus>();
    public DbSet<ClubMemberConcern> ClubMemberConcerns => Set<ClubMemberConcern>();
    public DbSet<ClubScoringRun> ClubScoringRuns => Set<ClubScoringRun>();

    // The club's clients who are not members: memberships held by companies, with their nominees.
    public DbSet<ClubCorporateMembership> ClubCorporateMemberships => Set<ClubCorporateMembership>();
    public DbSet<ClubCorporateNominee> ClubCorporateNominees => Set<ClubCorporateNominee>();

    // Private events: the club's spaces hired by a client, their menu and services, and their guests.
    public DbSet<ClubPrivateEvent> ClubPrivateEvents => Set<ClubPrivateEvent>();
    public DbSet<ClubPrivateEventLine> ClubPrivateEventLines => Set<ClubPrivateEventLine>();
    public DbSet<ClubPrivateEventGuest> ClubPrivateEventGuests => Set<ClubPrivateEventGuest>();

    /// <summary>
    /// Whether saving writes audit lines. Off only while an organization is copied: its
    /// history is copied with it, and each copied record would otherwise get a new
    /// "created" line of its own.
    /// </summary>
    public bool RecordsAuditTrail { get; set; } = true;

    /// <summary>
    /// The import running in this request, if one is. Every lead, contact and company
    /// added while it is set carries its id, whoever adds it: the importer, the contact
    /// a new lead is given, or the company its email domain names. That is what lets
    /// the import be undone without leaving those behind.
    /// </summary>
    public Guid? ImportBatchId { get; set; }

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
        ScopeToWorkspace(modelBuilder);
    }

    /// <summary>
    /// Saves, writing the audit trail for every record it changes and removing a deleted
    /// record's attachments and custom field values, all in the same transaction.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampVersions();
        StampDealAmounts();
        StampImportBatch();
        StampContactKeys();
        StampActivityTimes();
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
        StampVersions();
        StampDealAmounts();
        StampImportBatch();
        StampContactKeys();
        StampActivityTimes();
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

    // Whoever writes a club record, a lead, a deal, a contact or a company (a service, a
    // job, a merge, the cloner), its version moves, so a save made from an older read of
    // it is refused. See IVersioned.
    private void StampVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IVersioned>())
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.Version = Guid.NewGuid();
    }

    // Amount is a deal's money and Value the same number as text, kept for older clients
    // and integrations. Every writer saves through here, so the two cannot disagree: a
    // writer that set the number (the API, lead conversion, a merge, the cloner) gets the
    // text written from it, and one that set only the text (a tool, a test) gets the
    // number read from it. A save that touches neither re-reads the text, so a row from
    // before Amount existed is put right when anyone edits it.
    //
    // Text that already reads as the number is kept as it is. The cloner and a merge
    // carry an older deal's number together with its text ("CHF 12,000 + VAT"), and
    // rewriting that to "12000" lost the words in the copy that the original still has.
    private void StampDealAmounts()
    {
        foreach (var entry in ChangeTracker.Entries<Deal>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var deal = entry.Entity;
            var numberSet = entry.State == EntityState.Added
                ? deal.Amount != null
                : entry.Property(d => d.Amount).IsModified;
            if (numberSet && deal.Amount is decimal amount)
            {
                if (MoneyText.StoredAmount(deal.Value) != amount) deal.Value = MoneyText.ValueText(amount);
            }
            else
                deal.Amount = MoneyText.StoredAmount(deal.Value);
        }
    }

    // The keys duplicates are matched on (Contact.PhoneKey, NameKey), written from the phone
    // and the name on every save, whoever saves: the API, an import, a merge, the cloner.
    private void StampContactKeys()
    {
        foreach (var entry in ChangeTracker.Entries<Contact>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var contact = entry.Entity;
            contact.PhoneKey = MatchKeys.Phone(contact.Phone);
            contact.NameKey = MatchKeys.Name(contact.Name) ?? "";
        }
    }

    // An activity saved without the time it happened (the lead status sync's own lines, a
    // merge, a test, anything written before the field existed) happened when it was
    // logged: that is all anyone recorded about it.
    private void StampActivityTimes()
    {
        foreach (var entry in ChangeTracker.Entries<Activity>())
        {
            if (entry.State != EntityState.Added || entry.Entity.OccurredAtUtc != default) continue;
            entry.Entity.OccurredAtUtc = entry.Entity.CreatedAtUtc != default ? entry.Entity.CreatedAtUtc : DateTime.UtcNow;
        }
    }

    // See ImportBatchId. Only records being added: one the import updates was already there,
    // and undoing the import must not remove it.
    private void StampImportBatch()
    {
        if (ImportBatchId is not { } batchId) return;
        foreach (var entry in ChangeTracker.Entries<Lead>())
            if (entry.State == EntityState.Added) entry.Entity.ImportBatchId ??= batchId;
        foreach (var entry in ChangeTracker.Entries<Contact>())
            if (entry.State == EntityState.Added) entry.Entity.ImportBatchId ??= batchId;
        foreach (var entry in ChangeTracker.Entries<Company>())
            if (entry.State == EntityState.Added) entry.Entity.ImportBatchId ??= batchId;
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
