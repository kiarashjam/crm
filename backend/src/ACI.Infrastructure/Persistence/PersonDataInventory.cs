using ACI.Domain.Entities;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Where a workspace keeps data about the people in it, table by table, and what a person's
/// export and erasure do with each (see PersonDataService).
/// </summary>
/// <remarks>
/// <para>
/// Erasure used to remove one row and leave the person everywhere else: the contact made for
/// a lead, the activities, the sent mail, the sequence, the name in the audit trail, the club
/// documents. The only code that knew every place was the merge path. This list is the one
/// place that says where personal data lives; PersonDataInventoryTests fails when a table is
/// added to the model that is in neither list, so a new table cannot quietly keep someone
/// after they were erased.
/// </para>
/// <para>
/// A table in <see cref="AboutPeople"/> is read by the dossier (PersonDossier) and handled by
/// the erasure. One in <see cref="NotAboutPeople"/> holds no data about the people a workspace
/// keeps records of: settings, catalogues, the staff's own accounts, or rows that only name
/// another row.
/// </para>
/// </remarks>
public static class PersonDataInventory
{
    /// <summary>Tables holding data about a person, and what an erasure does with each.</summary>
    public static readonly IReadOnlyDictionary<Type, string> AboutPeople = new Dictionary<Type, string>
    {
        [typeof(Lead)] = "Deleted, with its status history, files and custom fields.",
        [typeof(Contact)] = "Deleted, with its files, custom fields and relationships.",
        [typeof(ClubMember)] = "Anonymised: the row stays for the books under the pseudonym; email, phone, notes and links go; the membership is cancelled.",
        [typeof(ClubApplication)] = "Deleted.",
        [typeof(Deal)] = "Anonymised: renamed to the pseudonym, description, contact and files removed; amounts and stages stay for the pipeline's figures.",
        [typeof(Activity)] = "On their records: deleted. On others that mention them: the mention replaced by the pseudonym.",
        [typeof(TaskItem)] = "On their records: deleted.",
        [typeof(TaskComment)] = "On their tasks: deleted.",
        [typeof(LeadStatusEvent)] = "Deleted with the lead.",
        [typeof(OutreachEnrollment)] = "Deleted.",
        [typeof(AutomationFire)] = "Deleted (the run's key names the record).",
        [typeof(ContactRelationship)] = "Deleted.",
        [typeof(DuplicateDecision)] = "Deleted.",
        [typeof(Contract)] = "Drafts deleted; out for signature voided and anonymised; declined or voided anonymised; signed kept whole as a legal record.",
        [typeof(ContractEvent)] = "With a contract anonymised: its addresses and the person's name removed.",
        [typeof(DealOnboarding)] = "Anonymised.",
        [typeof(Attachment)] = "Deleted with their lead, contact or deal.",
        [typeof(CustomFieldValueSet)] = "Deleted.",
        [typeof(CopyHistoryItem)] = "Deleted.",
        [typeof(Notification)] = "Deleted when it links to or names them.",
        [typeof(OutboundMessage)] = "To them: text and attachments removed, recipient replaced, a queued one cancelled. To others: the mention replaced.",
        [typeof(OutboundAttachment)] = "Deleted with the email to them.",
        [typeof(SentMessage)] = "Text removed and recipient replaced (sent before the outbox).",
        [typeof(InboundEmail)] = "Sender and subject removed.",
        [typeof(MemberMessage)] = "Anonymised; the campaign's totals stay.",
        [typeof(ConsentRecord)] = "Deleted.",
        [typeof(EmailSuppression)] = "What the mail service reported goes; a fingerprint of each address takes its place.",
        [typeof(AuditEvent)] = "Their name replaced by the pseudonym; who did what and when stays.",
        [typeof(ClubVisit)] = "Anonymised; kept for the club's figures.",
        [typeof(LoyaltyLedgerEntry)] = "Anonymised; kept for the club's figures.",
        [typeof(ClubHouseAccount)] = "Kept: the account's balance and currency, under the member's id (no name).",
        [typeof(ClubCharge)] = "Anonymised: amounts, dates and VAT stay (the books, OR art. 958f).",
        [typeof(ClubPayment)] = "Anonymised: amounts and dates stay.",
        [typeof(ClubPaymentAllocation)] = "Kept: amounts between a payment and a charge, no name.",
        [typeof(ClubSubscription)] = "Cancelled and anonymised.",
        [typeof(ClubGiftCard)] = "Their names and address removed; code and balance stay.",
        [typeof(ClubGiftCardTransaction)] = "Their name removed; amounts stay.",
        [typeof(ClubTab)] = "Anonymised.",
        [typeof(ClubTabItem)] = "Kept: what was ordered and its price, on an anonymised tab.",
        [typeof(ClubDonation)] = "Anonymised.",
        [typeof(ClubPaymentReminder)] = "Kept: a charge, a step and a date, under the member's id.",
        [typeof(ClubStatementDelivery)] = "Kept: a period and an amount, under the member's id.",
        [typeof(ClubEventRegistration)] = "Still to come: cancelled. All: anonymised.",
        [typeof(ClubReservation)] = "Still to come: cancelled. All: anonymised.",
        [typeof(ClubHouseholdMember)] = "Deleted; the household passes to the next person, or goes when empty.",
        [typeof(ClubHousehold)] = "Deleted when nobody is left in it.",
        [typeof(ClubPaymentProfile)] = "Deleted (the saved card).",
        [typeof(ClubOnlinePayment)] = "Anonymised: amounts and the payment provider's ids stay.",
        [typeof(ClubPaymentReference)] = "Kept: a QR reference and an amount, under the member's id.",
        [typeof(ClubBankTransaction)] = "Anonymised: the debtor's name and IBAN removed; amounts and dates stay.",
        [typeof(ClubSale)] = "Anonymised.",
        [typeof(ClubSaleLine)] = "Kept: what was sold and its price.",
        [typeof(ClubMemberCode)] = "Deleted.",
        [typeof(ClubPosTicket)] = "Anonymised; the till's raw data removed.",
        [typeof(ClubDocument)] = "Read-only older records, read as JSON: in one of theirs, names, addresses, phones and notes go; elsewhere a mention is replaced by the pseudonym.",
        [typeof(PersonErasure)] = "The log of erasures itself: numbers, dates and counts, naming nobody.",
    };

    /// <summary>Tables that hold nothing about the people a workspace keeps records of, and why.</summary>
    public static readonly IReadOnlyDictionary<Type, string> NotAboutPeople = new Dictionary<Type, string>
    {
        [typeof(User)] = "Staff accounts: a person's own account, not a record the workspace keeps about someone.",
        [typeof(UserSettings)] = "A staff member's own preferences.",
        [typeof(Organization)] = "The workspace itself.",
        [typeof(OrganizationMember)] = "Staff membership of the workspace.",
        [typeof(OrgSettings)] = "The workspace's settings.",
        [typeof(Invite)] = "Invitations of staff to the workspace.",
        [typeof(JoinRequest)] = "Staff asking to join the workspace.",
        [typeof(Pipeline)] = "Settings: a sales pipeline.",
        [typeof(DealStage)] = "Settings: a pipeline's stages.",
        [typeof(LeadStatus)] = "Settings: lead statuses.",
        [typeof(LeadSource)] = "Settings: lead sources.",
        [typeof(Company)] = "Organisations, not people.",
        [typeof(DealStageChange)] = "A deal's stage history: stages and dates, no person.",
        [typeof(DealLineItem)] = "A deal's products and prices.",
        [typeof(LeadScoringRule)] = "Settings: scoring rules.",
        [typeof(LeadAssignmentRule)] = "Settings: routing rules.",
        [typeof(LeadTerritory)] = "Settings: territories.",
        [typeof(LeadQueue)] = "Settings: queues.",
        [typeof(LeadCaptureForm)] = "Settings: a public capture form.",
        [typeof(LeadOpsSettings)] = "Settings: the response-time target.",
        [typeof(OutreachSequence)] = "Settings: an email sequence.",
        [typeof(OutreachStep)] = "Settings: a sequence's steps.",
        [typeof(Template)] = "Email templates.",
        [typeof(CustomFieldDefinition)] = "Settings: the workspace's own fields.",
        [typeof(Product)] = "The product catalogue.",
        [typeof(ForecastSnapshot)] = "Forecast totals per assignee (staff) and month.",
        [typeof(AutomationRule)] = "Settings: automation rules.",
        [typeof(OnboardingSettings)] = "Settings: the hand-over to the member portal.",
        [typeof(EmailImage)] = "Images used in the workspace's emails.",
        [typeof(ClubCatalogItem)] = "The club's price catalogue.",
        [typeof(ClubBillingRun)] = "What a billing run did, in counts.",
        [typeof(ClubEvent)] = "The club's events.",
        [typeof(ClubResource)] = "The club's bookable rooms and tables.",
        [typeof(ClubApplicationForm)] = "Settings: the public application form.",
        [typeof(ClubPaymentSettings)] = "Settings: payments and the club's bank.",
        [typeof(ClubProviderEvent)] = "Stripe's event ids and types, as received.",
        [typeof(ClubBankImport)] = "A bank file's name and counts.",
        [typeof(ClubPosSettings)] = "Settings: the till connection.",
        [typeof(SalesTeam)] = "Staff teams.",
        [typeof(SalesTeamMember)] = "Staff team members.",
        [typeof(ImportBatch)] = "An import's file name and counts.",
        [typeof(Campaign)] = "A campaign's text and totals, written to everyone in it.",
        [typeof(PrivacySettings)] = "Settings: the controller's identity, the forms' consent and the retention periods.",
        [typeof(RetentionRun)] = "What a retention run removed, in counts.",
    };
}
