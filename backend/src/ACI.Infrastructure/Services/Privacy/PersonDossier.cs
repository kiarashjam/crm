using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>
/// Everything a workspace holds about one person, read once: what the export writes out and
/// what the erasure works through. Reading both from the same place is what keeps them
/// agreeing: whatever the export shows, the erasure takes care of.
/// </summary>
/// <remarks>
/// <para>
/// Every read is the workspace's (the query filters apply). Rows are tracked when the
/// dossier is read for an erasure, so the erasure changes them in place, and not when it
/// is read for an export.
/// </para>
/// <para>
/// A record "about" the person is one that names one of their records by id, or one of the
/// addresses only they use (<see cref="PersonSubject.Addresses"/>). Text elsewhere that
/// mentions them (a note on someone else's lead, an email to someone else) is gathered
/// separately, as mentions: the erasure replaces the mention, and the export leaves other
/// people's emails out.
/// </para>
/// </remarks>
internal sealed class PersonDossier
{
    public required PersonSubject Subject { get; init; }

    // ── Sales ────────────────────────────────────────────────────────────────
    public List<Activity> Activities { get; private set; } = new();
    public List<Activity> ActivityMentions { get; private set; } = new();
    public List<TaskItem> Tasks { get; private set; } = new();
    public List<TaskComment> TaskComments { get; private set; } = new();
    public List<LeadStatusEvent> LeadStatusEvents { get; private set; } = new();
    public List<OutreachEnrollment> Enrollments { get; private set; } = new();
    public List<AutomationFire> AutomationFires { get; private set; } = new();
    public List<ContactRelationship> Relationships { get; private set; } = new();
    public List<DuplicateDecision> DuplicateDecisions { get; private set; } = new();
    public List<Lead> LeadsReferredByThem { get; private set; } = new();
    /// <summary>Contacts made from one of their leads that staff left out of the selection: they stay, unlinked.</summary>
    public List<Contact> ContactsFromTheirLeads { get; private set; } = new();
    /// <summary>Leads whose person contact is one of theirs, left out of the selection: they stay, and get no new contact.</summary>
    public List<Lead> LeadsOfTheirContacts { get; private set; } = new();
    public List<Contract> Contracts { get; private set; } = new();
    public List<ContractEvent> ContractEvents { get; private set; } = new();
    public List<DealOnboarding> Onboardings { get; private set; } = new();
    public List<Attachment> Attachments { get; private set; } = new();
    public List<CustomFieldValueSet> CustomFieldValues { get; private set; } = new();
    public List<CopyHistoryItem> CopyHistory { get; private set; } = new();
    public List<Notification> Notifications { get; private set; } = new();

    // ── Mail and consent ─────────────────────────────────────────────────────
    public List<OutboundMessage> Outbound { get; private set; } = new();
    public List<OutboundMessage> OutboundMentions { get; private set; } = new();
    public List<Guid> OutboundAttachmentIds { get; private set; } = new();
    public List<SentMessage> SentMessages { get; private set; } = new();
    public List<InboundEmail> Inbound { get; private set; } = new();
    public List<MemberMessage> MemberMessages { get; private set; } = new();
    public List<ConsentRecord> Consent { get; private set; } = new();
    public List<EmailSuppression> Suppressions { get; private set; } = new();
    public List<AuditEvent> Audit { get; private set; } = new();

    // ── Club ─────────────────────────────────────────────────────────────────
    public List<ClubVisit> Visits { get; private set; } = new();
    public List<LoyaltyLedgerEntry> Loyalty { get; private set; } = new();
    public List<ClubHouseAccount> HouseAccounts { get; private set; } = new();
    public List<ClubCharge> Charges { get; private set; } = new();
    public List<ClubPayment> Payments { get; private set; } = new();
    public List<ClubPaymentAllocation> Allocations { get; private set; } = new();
    public List<ClubSubscription> Subscriptions { get; private set; } = new();
    public List<ClubTab> Tabs { get; private set; } = new();
    public List<ClubTabItem> TabItems { get; private set; } = new();
    public List<ClubDonation> Donations { get; private set; } = new();
    public List<ClubGiftCard> GiftCards { get; private set; } = new();
    public List<ClubGiftCardTransaction> GiftCardTransactions { get; private set; } = new();
    public List<ClubSale> Sales { get; private set; } = new();
    public List<ClubSaleLine> SaleLines { get; private set; } = new();
    public List<ClubPosTicket> PosTickets { get; private set; } = new();
    public List<ClubOnlinePayment> OnlinePayments { get; private set; } = new();
    public List<ClubBankTransaction> BankTransactions { get; private set; } = new();
    public List<ClubPaymentProfile> PaymentProfiles { get; private set; } = new();
    public List<ClubPaymentReference> PaymentReferences { get; private set; } = new();
    public List<ClubStatementDelivery> StatementDeliveries { get; private set; } = new();
    public List<ClubPaymentReminder> PaymentReminders { get; private set; } = new();
    public List<ClubMemberCode> MemberCodes { get; private set; } = new();
    public List<ClubEventRegistration> Registrations { get; private set; } = new();
    public Dictionary<Guid, ClubEvent> Events { get; private set; } = new();
    public List<ClubReservation> Reservations { get; private set; } = new();
    public List<ClubHouseholdMember> HouseholdLinks { get; private set; } = new();
    public Dictionary<Guid, ClubHousehold> Households { get; private set; } = new();
    public List<ClubDocument> Documents { get; private set; } = new();

    /// <summary>The ids of every money record of the person's, for the audit lines written about them.</summary>
    public HashSet<Guid> MoneyRecordIds =>
        Charges.Select(c => c.Id)
            .Concat(Payments.Select(p => p.Id))
            .Concat(Subscriptions.Select(s => s.Id))
            .Concat(Tabs.Select(t => t.Id))
            .Concat(Donations.Select(d => d.Id))
            .Concat(GiftCards.Select(g => g.Id))
            .ToHashSet();

    /// <summary>Reads everything about <paramref name="subject"/>; <paramref name="track"/> for an erasure.</summary>
    /// <param name="mentions">
    /// Whether to search records that are not the person's for mentions of them. An erasure on
    /// request does; the retention job, which removes stale records by the hundred, does not:
    /// each search reads whole tables of notes and mail.
    /// </param>
    public static async Task<PersonDossier> LoadAsync(AppDbContext db, Guid organizationId, PersonSubject subject, bool track, CancellationToken ct, bool mentions = true)
    {
        var d = new PersonDossier { Subject = subject };
        IQueryable<T> Q<T>(IQueryable<T> query) where T : class => track ? query : query.AsNoTracking();

        var leadIds = subject.LeadIds.ToList();
        var contactIds = subject.ContactIds.ToList();
        var memberIds = subject.MemberIds.ToList();
        var applicationIds = subject.ApplicationIds.ToList();
        var dealIds = subject.DealIds.ToList();
        var addresses = subject.Addresses.ToList();
        var needles = mentions ? subject.Needles.ToList() : new List<string>();

        // ── Sales: what hangs off their leads, contacts and deals ───────────
        d.Activities = await Q(db.Activities).Where(a =>
                (a.LeadId != null && leadIds.Contains(a.LeadId.Value))
                || (a.ContactId != null && contactIds.Contains(a.ContactId.Value))
                || (a.DealId != null && dealIds.Contains(a.DealId.Value)))
            .OrderBy(a => a.OccurredAtUtc).ToListAsync(ct);
        var activityIds = d.Activities.Select(a => a.Id).ToHashSet();
        d.ActivityMentions = (await MentioningAsync(Q(db.Activities), needles,
                n => a => (a.Subject != null && a.Subject.Contains(n)) || (a.Body != null && a.Body.Contains(n)), ct))
            .Where(a => !activityIds.Contains(a.Id)).ToList();

        d.Tasks = await Q(db.TaskItems).Where(t =>
                (t.LeadId != null && leadIds.Contains(t.LeadId.Value))
                || (t.ContactId != null && contactIds.Contains(t.ContactId.Value))
                || (t.DealId != null && dealIds.Contains(t.DealId.Value)))
            .ToListAsync(ct);
        var taskIds = d.Tasks.Select(t => t.Id).ToList();
        d.TaskComments = await Q(db.TaskComments).Where(c => taskIds.Contains(c.TaskItemId)).ToListAsync(ct);
        d.LeadStatusEvents = await Q(db.LeadStatusEvents).Where(e => leadIds.Contains(e.LeadId)).ToListAsync(ct);
        d.Enrollments = await Q(db.OutreachEnrollments).Where(e =>
                (e.TargetType == "lead" && leadIds.Contains(e.TargetId))
                || (e.TargetType == "contact" && contactIds.Contains(e.TargetId))
                || (e.TargetEmail != null && addresses.Contains(e.TargetEmail.Trim().ToLower())))
            .ToListAsync(ct);
        // An automation remembers what it ran for by a key that starts with the record's id.
        // Read through the workspace's rules: a run has no workspace of its own.
        var ruleIds = await db.AutomationRules.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
        d.AutomationFires = new List<AutomationFire>();
        if (ruleIds.Count > 0)
            foreach (var prefix in leadIds.Concat(dealIds).Select(id => id.ToString()))
                d.AutomationFires.AddRange(await Q(db.AutomationFires)
                    .Where(f => ruleIds.Contains(f.RuleId) && f.SourceKey.StartsWith(prefix)).ToListAsync(ct));
        d.Relationships = await Q(db.ContactRelationships)
            .Where(r => contactIds.Contains(r.ContactId) || contactIds.Contains(r.RelatedContactId)).ToListAsync(ct);
        var recordIds = subject.RecordIds.Concat(dealIds).ToList();
        d.DuplicateDecisions = await Q(db.DuplicateDecisions)
            .Where(x => recordIds.Contains(x.RecordIdA) || recordIds.Contains(x.RecordIdB)).ToListAsync(ct);
        d.LeadsReferredByThem = await Q(db.Leads)
            .Where(l => l.ReferredByContactId != null && contactIds.Contains(l.ReferredByContactId.Value) && !leadIds.Contains(l.Id))
            .ToListAsync(ct);
        d.ContactsFromTheirLeads = await Q(db.Contacts)
            .Where(c => c.ConvertedFromLeadId != null && leadIds.Contains(c.ConvertedFromLeadId.Value) && !contactIds.Contains(c.Id))
            .ToListAsync(ct);
        d.LeadsOfTheirContacts = await Q(db.Leads)
            .Where(l => l.ConvertedToContactId != null && contactIds.Contains(l.ConvertedToContactId.Value) && !leadIds.Contains(l.Id))
            .ToListAsync(ct);
        d.Contracts = await Q(db.Contracts).Where(c =>
                (c.LeadId != null && leadIds.Contains(c.LeadId.Value))
                || (c.DealId != null && dealIds.Contains(c.DealId.Value))
                || addresses.Contains(c.CounterpartyEmail.Trim().ToLower()))
            .ToListAsync(ct);
        var contractIds = d.Contracts.Select(c => c.Id).ToList();
        d.ContractEvents = await Q(db.ContractEvents).Where(e => contractIds.Contains(e.ContractId)).ToListAsync(ct);
        d.Onboardings = await Q(db.DealOnboardings).Where(o =>
                (o.LeadId != null && leadIds.Contains(o.LeadId.Value))
                || (o.ContactId != null && contactIds.Contains(o.ContactId.Value))
                || (o.ClubMemberId != null && memberIds.Contains(o.ClubMemberId.Value))
                || dealIds.Contains(o.DealId)
                || addresses.Contains(o.Email.Trim().ToLower()))
            .ToListAsync(ct);
        // The files' bytes are not read here: the export reads them one by one as it writes them.
        d.Attachments = await db.Attachments.AsNoTracking().Where(a =>
                (a.EntityType == RecordTypes.Lead && leadIds.Contains(a.RecordId))
                || (a.EntityType == RecordTypes.Contact && contactIds.Contains(a.RecordId))
                || (a.EntityType == RecordTypes.Deal && dealIds.Contains(a.RecordId)))
            .Select(a => new Attachment
            {
                Id = a.Id, OrganizationId = a.OrganizationId, OwnerUserId = a.OwnerUserId, EntityType = a.EntityType,
                RecordId = a.RecordId, FileName = a.FileName, ContentType = a.ContentType, UploadedByName = a.UploadedByName,
                UploadedAtUtc = a.UploadedAtUtc,
            })
            .ToListAsync(ct);
        d.CustomFieldValues = await Q(db.CustomFieldValues).Where(v =>
                (v.EntityType == RecordTypes.Lead && leadIds.Contains(v.RecordId))
                || (v.EntityType == RecordTypes.Contact && contactIds.Contains(v.RecordId))
                || (v.EntityType == RecordTypes.Deal && dealIds.Contains(v.RecordId)))
            .ToListAsync(ct);
        var recordIdTexts = subject.RecordIds.Select(id => id.ToString()).ToList();
        d.CopyHistory = await Q(db.CopyHistoryItems).Where(c =>
                recordIdTexts.Contains(c.RecipientId)
                || (c.RecipientEmail != null && addresses.Contains(c.RecipientEmail.Trim().ToLower())))
            .ToListAsync(ct);
        // A notification links to the record it is about ("/leads/{id}") or names the person.
        var links = leadIds.Select(id => $"/leads/{id}").Concat(contactIds.Select(id => $"/contacts/{id}"))
            .Concat(memberIds.Select(id => $"/members/{id}")).Concat(dealIds.Select(id => $"/deals/{id}")).ToList();
        var linked = new List<Notification>();
        foreach (var link in links)
            linked.AddRange(await Q(db.Notifications).Where(n => n.Link != null && n.Link.StartsWith(link)).ToListAsync(ct));
        linked.AddRange(await MentioningAsync(Q(db.Notifications), needles,
            n => x => x.Title.Contains(n) || (x.Message != null && x.Message.Contains(n)), ct));
        d.Notifications = linked.DistinctBy(n => n.Id).ToList();

        // ── Mail and consent ────────────────────────────────────────────────
        var outbound = await Q(db.OutboundMessages).Where(m =>
                (m.LeadId != null && leadIds.Contains(m.LeadId.Value))
                || (m.ContactId != null && contactIds.Contains(m.ContactId.Value))
                || (m.MemberId != null && memberIds.Contains(m.MemberId.Value)))
            .ToListAsync(ct);
        foreach (var address in addresses)
            outbound.AddRange(await Q(db.OutboundMessages).Where(m =>
                    (m.To != null && m.To.Contains(address)) || (m.Cc != null && m.Cc.Contains(address)) || (m.Bcc != null && m.Bcc.Contains(address)))
                .ToListAsync(ct));
        // "anna@x.ch" is in "joanna@x.ch" too: only a message whose list really names the address counts.
        d.Outbound = outbound.DistinctBy(m => m.Id)
            .Where(m => m.LeadId is Guid l && leadIds.Contains(l) || m.ContactId is Guid c && contactIds.Contains(c)
                || m.MemberId is Guid mm && memberIds.Contains(mm)
                || subject.Addressed(m.To) || subject.Addressed(m.Cc) || subject.Addressed(m.Bcc))
            .OrderBy(m => m.CreatedAtUtc).ToList();
        var outboundIds = d.Outbound.Select(m => m.Id).ToHashSet();
        d.OutboundMentions = (await MentioningAsync(Q(db.OutboundMessages), needles,
                n => m => (m.Subject != null && m.Subject.Contains(n)) || (m.Body != null && m.Body.Contains(n))
                    || (m.TextBody != null && m.TextBody.Contains(n)) || (m.HtmlBody != null && m.HtmlBody.Contains(n))
                    || (m.PayloadJson != null && m.PayloadJson.Contains(n)), ct))
            .Where(m => !outboundIds.Contains(m.Id)).ToList();
        var outboundIdList = outboundIds.ToList();
        d.OutboundAttachmentIds = await db.OutboundAttachments.AsNoTracking()
            .Where(a => outboundIdList.Contains(a.OutboundMessageId)).Select(a => a.Id).ToListAsync(ct);
        var sent = await Q(db.SentMessages).Where(m =>
                (m.LeadId != null && leadIds.Contains(m.LeadId.Value))
                || (m.ContactId != null && contactIds.Contains(m.ContactId.Value))
                || (m.DealId != null && dealIds.Contains(m.DealId.Value)))
            .ToListAsync(ct);
        foreach (var address in addresses)
            sent.AddRange(await Q(db.SentMessages).Where(m => m.To.Contains(address) || (m.Cc != null && m.Cc.Contains(address))).ToListAsync(ct));
        d.SentMessages = sent.DistinctBy(m => m.Id)
            .Where(m => m.LeadId is Guid l && leadIds.Contains(l) || m.ContactId is Guid c && contactIds.Contains(c)
                || m.DealId is Guid dd && dealIds.Contains(dd) || subject.Addressed(m.To) || subject.Addressed(m.Cc) || subject.Addressed(m.Bcc))
            .ToList();
        d.Inbound = await Q(db.InboundEmails).Where(i =>
                outboundIdList.Contains(i.OutboundMessageId) || addresses.Contains(i.FromAddress.Trim().ToLower()))
            .ToListAsync(ct);
        d.MemberMessages = await Q(db.MemberMessages).Where(m =>
                memberIds.Contains(m.MemberId) || (m.Email != null && addresses.Contains(m.Email.Trim().ToLower())))
            .ToListAsync(ct);
        var subjects = leadIds.Concat(contactIds).Concat(memberIds).Concat(applicationIds).ToList();
        d.Consent = await Q(db.ConsentRecords).Where(c =>
                subjects.Contains(c.SubjectId) || (c.Address != null && addresses.Contains(c.Address)))
            .OrderBy(c => c.RecordedAtUtc).ToListAsync(ct);
        var suppressionValues = addresses.Concat(addresses.Select(ACI.Application.Common.ErasedAddresses.Key)).ToList();
        d.Suppressions = await Q(db.EmailSuppressions).Where(s => suppressionValues.Contains(s.Email)).ToListAsync(ct);

        // ── Club ────────────────────────────────────────────────────────────
        d.Visits = await Q(db.ClubVisits).Where(v => memberIds.Contains(v.MemberId)).OrderBy(v => v.VisitedAtUtc).ToListAsync(ct);
        d.Loyalty = await Q(db.LoyaltyLedger).Where(e => memberIds.Contains(e.MemberId)).OrderBy(e => e.CreatedAtUtc).ToListAsync(ct);
        d.HouseAccounts = await Q(db.ClubHouseAccounts).Where(a => memberIds.Contains(a.MemberId)).ToListAsync(ct);
        d.Charges = await Q(db.ClubCharges).Where(c =>
                memberIds.Contains(c.MemberId) || (c.IncurredByMemberId != null && memberIds.Contains(c.IncurredByMemberId.Value)))
            .OrderBy(c => c.PostedAtUtc).ToListAsync(ct);
        d.Payments = await Q(db.ClubPayments).Where(p => memberIds.Contains(p.MemberId)).OrderBy(p => p.ReceivedAtUtc).ToListAsync(ct);
        var paymentIds = d.Payments.Select(p => p.Id).ToList();
        d.Allocations = await Q(db.ClubPaymentAllocations).Where(a => paymentIds.Contains(a.PaymentId)).ToListAsync(ct);
        d.Subscriptions = await Q(db.ClubSubscriptions).Where(s => memberIds.Contains(s.MemberId)).ToListAsync(ct);
        d.Tabs = await Q(db.ClubTabs).Where(t => memberIds.Contains(t.MemberId)).ToListAsync(ct);
        var tabIds = d.Tabs.Select(t => t.Id).ToList();
        d.TabItems = await Q(db.ClubTabItems).Where(i => tabIds.Contains(i.TabId)).ToListAsync(ct);
        d.Donations = await Q(db.ClubDonations).Where(x => x.MemberId != null && memberIds.Contains(x.MemberId.Value)).ToListAsync(ct);
        d.GiftCards = await Q(db.ClubGiftCards).Where(g =>
                (g.PurchaserMemberId != null && memberIds.Contains(g.PurchaserMemberId.Value))
                || (g.RecipientEmail != null && addresses.Contains(g.RecipientEmail.Trim().ToLower())))
            .ToListAsync(ct);
        var cardIds = d.GiftCards.Select(g => g.Id).ToList();
        d.GiftCardTransactions = await Q(db.ClubGiftCardTransactions).Where(t =>
                cardIds.Contains(t.GiftCardId) || (t.MemberId != null && memberIds.Contains(t.MemberId.Value)))
            .ToListAsync(ct);
        d.Sales = await Q(db.ClubSales).Where(s =>
                memberIds.Contains(s.MemberId) || (s.BilledMemberId != null && memberIds.Contains(s.BilledMemberId.Value)))
            .ToListAsync(ct);
        var saleIds = d.Sales.Select(s => s.Id).ToList();
        d.SaleLines = await Q(db.ClubSaleLines).Where(l => saleIds.Contains(l.SaleId)).ToListAsync(ct);
        d.PosTickets = await Q(db.ClubPosTickets).Where(t => t.MemberId != null && memberIds.Contains(t.MemberId.Value)).ToListAsync(ct);
        d.OnlinePayments = await Q(db.ClubOnlinePayments).Where(p => memberIds.Contains(p.MemberId)).ToListAsync(ct);
        d.BankTransactions = await Q(db.ClubBankTransactions).Where(t => t.MemberId != null && memberIds.Contains(t.MemberId.Value)).ToListAsync(ct);
        d.PaymentProfiles = await Q(db.ClubPaymentProfiles).Where(p => memberIds.Contains(p.MemberId)).ToListAsync(ct);
        d.PaymentReferences = await Q(db.ClubPaymentReferences).Where(r => memberIds.Contains(r.MemberId)).ToListAsync(ct);
        d.StatementDeliveries = await Q(db.ClubStatementDeliveries).Where(s => memberIds.Contains(s.MemberId)).ToListAsync(ct);
        d.PaymentReminders = await Q(db.ClubPaymentReminders).Where(r => memberIds.Contains(r.MemberId)).ToListAsync(ct);
        d.MemberCodes = await Q(db.ClubMemberCodes).Where(c => memberIds.Contains(c.MemberId)).ToListAsync(ct);
        d.Registrations = await Q(db.ClubEventRegistrations).Where(r =>
                (r.MemberId != null && memberIds.Contains(r.MemberId.Value))
                || (r.HostMemberId != null && memberIds.Contains(r.HostMemberId.Value))
                || (r.Email != null && addresses.Contains(r.Email.Trim().ToLower())))
            .ToListAsync(ct);
        var eventIds = d.Registrations.Select(r => r.EventId).Distinct().ToList();
        d.Events = await db.ClubEvents.AsNoTracking().Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        d.Reservations = await Q(db.ClubReservations).Where(r =>
                (r.MemberId != null && memberIds.Contains(r.MemberId.Value))
                || (r.MemberEmail != null && addresses.Contains(r.MemberEmail.Trim().ToLower())))
            .ToListAsync(ct);
        d.HouseholdLinks = await Q(db.ClubHouseholdMembers).Where(l =>
                (l.MemberId != null && memberIds.Contains(l.MemberId.Value))
                || (l.ContactId != null && contactIds.Contains(l.ContactId.Value)))
            .ToListAsync(ct);
        var householdIds = d.HouseholdLinks.Select(l => l.HouseholdId).Distinct().ToList();
        d.Households = await Q(db.ClubHouseholds).Where(h => householdIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id, ct);
        // The JSON documents kept read-only from before the club's records had tables of their
        // own: any that name a member's id, or mention the person.
        var documentNeedles = memberIds.Select(id => id.ToString()).Concat(needles).ToList();
        d.Documents = await MentioningAsync(Q(db.ClubDocuments), documentNeedles, n => x => x.Payload.Contains(n), ct);

        // ── The audit trail: lines about any of their records, their deals, tasks and money ──
        var audited = recordIds.Concat(d.MoneyRecordIds).Concat(d.Tasks.Select(t => t.Id)).ToList();
        d.Audit = new List<AuditEvent>();
        foreach (var chunk in audited.Chunk(500))
        {
            var ids = chunk.ToList();
            d.Audit.AddRange(await Q(db.AuditEvents).Where(e => e.EntityId != null && ids.Contains(e.EntityId.Value)).ToListAsync(ct));
        }
        d.Audit = d.Audit.DistinctBy(e => e.Id).OrderBy(e => e.CreatedAtUtc).ToList();
        return d;
    }

    /// <summary>The rows whose text mentions any of <paramref name="needles"/>, once each.</summary>
    private static async Task<List<T>> MentioningAsync<T>(
        IQueryable<T> rows, IReadOnlyList<string> needles,
        Func<string, System.Linq.Expressions.Expression<Func<T, bool>>> mentions, CancellationToken ct)
        where T : ACI.Domain.Common.BaseEntity
    {
        var found = new Dictionary<Guid, T>();
        foreach (var needle in needles)
            foreach (var row in await rows.Where(mentions(needle)).ToListAsync(ct))
                found.TryAdd(row.Id, row);
        return found.Values.ToList();
    }
}
