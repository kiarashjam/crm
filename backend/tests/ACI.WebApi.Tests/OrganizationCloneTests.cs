using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Tests;

/// <summary>
/// Copying an organization into a new one: every record, every link between records,
/// and its people, with nothing in the copy pointing back into the original.
/// </summary>
/// <remarks>
/// Runs on SQLite, a real relational database with the foreign keys and unique indexes
/// the copy has to satisfy. The original is filled with at least one row in every table
/// the copy covers, a converted lead whose links go round in a circle, and ids kept
/// inside JSON. A second organization and someone's personal records sit alongside, and
/// must come through untouched.
/// </remarks>
public class OrganizationCloneTests
{
    private sealed record Seeded(Guid Org, Guid Owner, Guid Manager, Guid Rep, Guid Lead, Guid Deal, Guid Contact, Guid Member, Guid Rule, Guid Sequence);

    private static async Task<Seeded> SeedAsync(AppDbContext db, string name, Guid owner, Guid manager, Guid rep)
    {
        var now = DateTime.UtcNow;
        var org = new Organization
        {
            Id = Guid.NewGuid(), Name = name, OwnerUserId = owner, CreatedAtUtc = now.AddYears(-1),
            WebhookApiKeyHash = ACI.Application.Services.OrganizationService.HashApiKey($"key-{name}"), WebhookApiKeyCreatedAtUtc = now, WebhookPassword = "secret",
            OutboundWebhookUrl = "https://hooks.example.com/cadence", OutboundWebhookSecret = "s3cret",
            McpApiKeyHash = ACI.Application.Services.OrganizationService.HashApiKey($"mcp-{name}"), McpApiKeyCreatedAtUtc = now,
        };
        db.Organizations.Add(org);
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = org.Id, UserId = owner, Role = OrgMemberRole.Owner, JoinedAtUtc = now },
            new OrganizationMember { OrganizationId = org.Id, UserId = manager, Role = OrgMemberRole.Manager, JoinedAtUtc = now },
            new OrganizationMember { OrganizationId = org.Id, UserId = rep, Role = OrgMemberRole.Viewer, JoinedAtUtc = now });
        db.OrgSettings.Add(new OrgSettings { OrganizationId = org.Id, CompanyName = name, UpdatedAtUtc = now });
        db.Invites.AddRange(
            new Invite { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = $"new@{name}.ch", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAtUtc = now.AddDays(7), CreatedAtUtc = now },
            new Invite { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = $"joined@{name}.ch", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAtUtc = now, CreatedAtUtc = now, AcceptedByUserId = rep, AcceptedAtUtc = now });
        db.JoinRequests.Add(new JoinRequest { Id = Guid.NewGuid(), OrganizationId = org.Id, UserId = rep, CreatedAtUtc = now });

        var pipeline = new Pipeline { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Memberships" };
        var stage = new DealStage { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = "Tour booked" };
        var won = new DealStage { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = "Joined", IsWon = true };
        var source = new LeadSource { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Website" };
        var status = new LeadStatus { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Qualified" };
        var company = new Company { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Acme", CreatedAtUtc = now };
        db.AddRange(pipeline, stage, won, source, status, company);
        await db.SaveChangesAsync();

        // A converted lead: the lead points at its deal, the deal at its contact, the
        // contact back at the lead. Saved in two steps, as no insert order satisfies a loop.
        // It came in by an import, so it also points at that.
        var import = new ImportBatch
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, UserId = owner, EntityType = "lead", FileName = "waitlist.csv",
            TotalRows = 1, CreatedCount = 1, StartedAtUtc = now, FinishedAtUtc = now,
        };
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Jean Dupont", Email = $"jean@{name}.ch", Status = "Qualified",
            CompanyId = company.Id, LeadSourceId = source.Id, LeadStatusId = status.Id, AssignedToUserId = rep, IsConverted = true, CreatedAtUtc = now,
            ImportBatchId = import.Id,
        };
        var contact = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Jean Dupont", Email = $"jean@{name}.ch", CompanyId = company.Id, CreatedAtUtc = now };
        var deal = new Deal
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Founding membership", Value = "12000", Currency = "CHF",
            PipelineId = pipeline.Id, DealStageId = stage.Id, CompanyId = company.Id, AssigneeId = rep, IsPrivate = true, CreatedAtUtc = now,
        };
        db.AddRange(import, lead, contact, deal);
        await db.SaveChangesAsync();
        lead.ConvertedToDealId = deal.Id;
        lead.ConvertedToContactId = contact.Id;
        lead.ConvertedToCompanyId = company.Id;
        deal.ContactId = contact.Id;
        contact.ConvertedFromLeadId = lead.Id;
        await db.SaveChangesAsync();

        var task = new TaskItem
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Title = "Call Jean", LeadId = lead.Id, DealId = deal.Id, ContactId = contact.Id,
            AssigneeId = rep, DueDateUtc = now.AddDays(2), ReminderDateUtc = now.AddDays(1), Status = TaskStatus.Todo, Recurrence = "weekly", CreatedAtUtc = now,
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, LeadId = lead.Id, DealId = deal.Id, Status = ContractStatuses.Sent, Title = "Membership agreement",
            Body = $"Agreement for deal {deal.Id}", CounterpartyName = "Jean Dupont", CounterpartyEmail = $"jean@{name}.ch", CreatedByUserId = owner,
            CreatedAtUtc = now, UpdatedAtUtc = now, SentAtUtc = now.AddDays(-4), SentByUserId = owner, SigningTokenHash = Guid.NewGuid().ToString("N"),
            SigningTokenExpiresAtUtc = now.AddDays(10),
        };
        var sequence = new OutreachSequence { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Welcome", Status = "active", CreatedAtUtc = now };
        var territory = new LeadTerritory { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Geneva" };
        var queue = new LeadQueue { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Front desk", MemberUserIds = rep.ToString() };
        var member = new ClubMember { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, FirstName = "Ana", LastName = "Silva", Email = $"ana@{name}.ch", Tier = "Gold", Status = "Active" };
        var team = new SalesTeam { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Desk", CreatedAtUtc = now };
        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, Name = "Welcome", Trigger = "lead.created", CreatedAtUtc = now,
            ActionsJson = JsonSerializer.Serialize(new object[]
            {
                new { type = "assign", config = new { userId = rep.ToString() } },
                new { type = "add_to_sequence", config = new { sequenceId = sequence.Id.ToString() } },
            }),
        };
        var definition = new CustomFieldDefinition { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, EntityType = "deal", Key = "board_seat", Label = "Board seat", Type = "checkbox", CreatedAtUtc = now };
        var spouse = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Marie Dupont", Email = $"marie@{name}.ch", CreatedAtUtc = now };
        var sentEmailId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var sentCampaignId = Guid.NewGuid();
        var sentEmail = new OutboundMessage
        {
            Id = sentEmailId, OrganizationId = org.Id, Channel = OutboundChannels.Email, Source = OutboundSources.Composer, Status = OutboundStatuses.Sent,
            Attempts = 1, CreatedAtUtc = now, SentAtUtc = now, CreatedByUserId = rep, To = $"jean@{name}.ch", Subject = "Welcome", Body = "Hello",
            LeadId = lead.Id, MessageIdHeader = $"<{sentEmailId:N}@pavillon46.ch>", ReplyToken = $"tok{sentEmailId:N}"[..21],
        };
        db.AddRange(task, contract, sequence, territory, queue, member, team, rule, definition, spouse);
        db.ContactRelationships.Add(new ContactRelationship { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, ContactId = contact.Id, RelatedContactId = spouse.Id, Type = ContactRelationshipTypes.Spouse, CreatedAtUtc = now });
        // The couple look alike to the duplicate rules; someone said they are two people.
        db.DuplicateDecisions.Add(new DuplicateDecision { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Kind = DuplicateKinds.Contacts, RecordIdA = contact.Id, RecordIdB = spouse.Id, CreatedAtUtc = now });
        db.AddRange(
            new DealStageChange { Id = Guid.NewGuid(), DealId = deal.Id, FromDealStageId = stage.Id, ToDealStageId = won.Id, ChangedByUserId = owner, ChangedAtUtc = now },
            new DealLineItem { Id = Guid.NewGuid(), DealId = deal.Id, Name = "Membership", Quantity = 1, UnitPrice = 12000, CreatedAtUtc = now },
            new TaskComment { Id = Guid.NewGuid(), TaskItemId = task.Id, AuthorId = manager, Body = "Prefers mornings", CreatedAtUtc = now },
            new ContractEvent { Id = Guid.NewGuid(), ContractId = contract.Id, Type = "sent", ActorUserId = owner, AtUtc = now },
            new OutreachStep { Id = Guid.NewGuid(), SequenceId = sequence.Id, Type = "email", DayOffset = 1, Subject = "Hello" },
            new OutreachEnrollment { Id = Guid.NewGuid(), SequenceId = sequence.Id, OrganizationId = org.Id, EnrolledByUserId = owner, TargetId = lead.Id, TargetName = "Jean Dupont", Status = "active", EnrolledAtUtc = now, NextActionAtUtc = now.AddHours(1) },
            new OutreachEnrollment { Id = Guid.NewGuid(), SequenceId = sequence.Id, OrganizationId = org.Id, EnrolledByUserId = owner, TargetId = lead.Id, TargetName = "Jean Dupont", Status = "stopped", EnrolledAtUtc = now },
            new AutomationFire { Id = Guid.NewGuid(), RuleId = rule.Id, SourceKey = lead.Id.ToString(), FiredAtUtc = now },
            // A run of the rule, waiting two days before its email, and the line its log has so far.
            new AutomationRun
            {
                Id = runId, OrganizationId = org.Id, RuleId = rule.Id, RuleName = rule.Name, Trigger = rule.Trigger, SourceKey = lead.Id.ToString(),
                Status = AutomationRunStatuses.Waiting, ActorUserId = owner, RecordType = "lead", RecordId = lead.Id, LeadId = lead.Id,
                StepsJson = "[]", CurrentPath = "1", NextAttemptAtUtc = now.AddDays(2), WaitingFor = "2 days", StartedAtUtc = now, UpdatedAtUtc = now,
            },
            new AutomationRunStep { Id = Guid.NewGuid(), OrganizationId = org.Id, RunId = runId, Path = "0", StepType = "wait", Outcome = AutomationStepOutcomes.Waiting, Summary = "Waiting 2 days", Attempt = 1, AtUtc = now },
            new SalesTeamMember { TeamId = team.Id, UserId = rep },
            new Activity { Id = Guid.NewGuid(), UserId = rep, OrganizationId = org.Id, Type = "call", Subject = "Called", LeadId = lead.Id, DealId = deal.Id, ContactId = contact.Id, CreatedAtUtc = now },
            new Notification { Id = Guid.NewGuid(), UserId = rep, OrganizationId = org.Id, Type = "task", Title = "Due tomorrow", Link = $"/tasks/{task.Id}", SourceKey = $"task-reminder:{task.Id}", CreatedAtUtc = now },
            new Notification { Id = Guid.NewGuid(), UserId = rep, OrganizationId = org.Id, Type = "digest", Title = "Weekly digest", SourceKey = $"digest:{name}", Read = true, CreatedAtUtc = now },
            new Template { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Title = "Welcome", Description = "First email", Category = "email", Goal = "intro", CreatedAtUtc = now },
            new CopyHistoryItem { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Type = "email", Copy = "Hi Jean", RecipientName = "Jean", RecipientId = lead.Id.ToString(), CreatedAtUtc = now },
            new AuditEvent { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, EntityType = "deal", EntityId = deal.Id, Action = "updated", Summary = "Stage changed", ActorName = "Kia", CreatedAtUtc = now },
            new Attachment { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, EntityType = "deal", RecordId = deal.Id, FileName = "terms.pdf", ContentType = "application/pdf", Size = 4, Content = "%PDF"u8.ToArray(), UploadedByName = "Kia", UploadedAtUtc = now },
            new CustomFieldValueSet { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, EntityType = "deal", RecordId = deal.Id, ValuesJson = """{"board_seat":true}""", UpdatedAtUtc = now },
            new Product { Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, Name = "Gold membership", UnitPrice = 2400, CreatedAtUtc = now },
            new ForecastSnapshot { Id = Guid.NewGuid(), OrganizationId = org.Id, SavedByUserId = owner, AssigneeId = rep, AssigneeName = "Rep", MonthUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), SavedAtUtc = now },
            new SentMessage { Id = Guid.NewGuid(), OrganizationId = org.Id, SentByUserId = owner, To = $"jean@{name}.ch", Subject = "Welcome", Body = "Hello", LeadId = lead.Id, SentAtUtc = now },
            sentEmail,
            new OutboundMessage
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Channel = OutboundChannels.Webhook, Source = OutboundSources.Webhook, Status = OutboundStatuses.Queued,
                NextAttemptAtUtc = now, CreatedAtUtc = now, EventName = "lead.created", PayloadJson = $$"""{"Id":"{{lead.Id}}"}""",
            },
            new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = $"bounced@{name}.ch", Reason = EmailSuppressionReasons.Bounced, OutboundMessageId = sentEmail.Id, CreatedAtUtc = now },
            new InboundEmail { Id = Guid.NewGuid(), OrganizationId = org.Id, OutboundMessageId = sentEmail.Id, MessageIdHeader = $"<reply-{name}@example.ch>", FromAddress = $"jean@{name}.ch", MatchedBy = "token", ReceivedAtUtc = now },
            new ClubVisit { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", MemberTier = "Gold", Venue = "Lounge", VisitedAtUtc = now },
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Points = 450, CreatedAtUtc = now },
            new ConsentRecord
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, SubjectType = ConsentSubjects.Member, SubjectId = member.Id, Channel = ConsentChannels.Email,
                Purpose = ConsentPurposes.Marketing, Granted = true, Source = ConsentSources.Form, Address = $"ana@{name}.ch", RecordedAtUtc = now,
                RecordedByUserId = owner, IsCurrent = true,
            },
            new Campaign
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Autumn gala", Subject = "Join us", Body = "Hello", Status = CampaignStatuses.Scheduled,
                ScheduledAtUtc = now.AddDays(2), SentByUserId = owner, CreatedByUserId = owner, CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new Campaign
            {
                Id = sentCampaignId, OrganizationId = org.Id, Name = "Spring news", Subject = "Spring", Body = "Hello", Status = CampaignStatuses.Sent,
                SendingStartedAtUtc = now, SentAtUtc = now, RecipientCount = 1, SentByUserId = owner, CreatedByUserId = owner, CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new MemberMessage
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Email = $"ana@{name}.ch", CampaignId = sentCampaignId,
                CampaignName = "Spring news", Subject = "Spring", Purpose = ConsentPurposes.Marketing, Status = MemberMessageStatuses.Queued,
                OutboundMessageId = sentEmailId, CreatedByUserId = owner, CreatedAtUtc = now,
            },
            new ClubDocument { Id = Guid.NewGuid(), OrganizationId = org.Id, Kind = "tabs", UpdatedAtUtc = now, Payload = JsonSerializer.Serialize(new { memberId = member.Id.ToString().ToUpperInvariant(), venue = "Bar" }) });
        // The club's money ledger: a tab that posted a charge, part paid; a subscription that
        // billed one; a gift card part spent; a donation. Each row points at others the copy
        // must point at its own copies of.
        var tab = new ClubTab { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", MemberTier = "Gold", Venue = "Bar", Status = "Closed", OpenedAtUtc = now, Total = 30, CreatedAtUtc = now };
        var subscription = new ClubSubscription { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Name = "Gold dues", Amount = 299, StartedAtUtc = now, CreatedAtUtc = now };
        var tabCharge = new ClubCharge { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Kind = "Bar", Description = "Bar tab", Amount = 30, PostedAtUtc = now, TabId = tab.Id, CreatedAtUtc = now };
        var dues = new ClubCharge { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Kind = "Dues", Description = "Gold dues", Amount = 299, PostedAtUtc = now, SubscriptionId = subscription.Id, BillingPeriodStartUtc = now, CreatedAtUtc = now };
        var payment = new ClubPayment { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Method = "Cash", Amount = 10, ReceivedAtUtc = now, CreatedAtUtc = now };
        var card = new ClubGiftCard { Id = Guid.NewGuid(), OrganizationId = org.Id, Code = $"GIFT-{name.ToUpperInvariant()}", FaceValue = 100, PurchaserMemberId = member.Id, IssuedAtUtc = now, CreatedAtUtc = now };
        tab.ChargeId = tabCharge.Id;
        db.AddRange(tab, subscription, tabCharge, dues, payment, card,
            new ClubHouseAccount { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, OpenedAtUtc = now },
            new ClubPaymentAllocation { Id = Guid.NewGuid(), OrganizationId = org.Id, PaymentId = payment.Id, ChargeId = tabCharge.Id, Amount = 10, CreatedAtUtc = now },
            new ClubTabItem { Id = Guid.NewGuid(), OrganizationId = org.Id, TabId = tab.Id, Name = "Negroni", Quantity = 2, UnitPrice = 15, AddedAtUtc = now },
            new ClubGiftCardTransaction { Id = Guid.NewGuid(), OrganizationId = org.Id, GiftCardId = card.Id, Type = "Issue", Amount = 100, AtUtc = now, CreatedAtUtc = now },
            new ClubDonation { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Cause = "Arts", Amount = 50, ReceivedAtUtc = now, CreatedAtUtc = now });
        // The price catalogue and what billing records: the tier the dues came from, a run,
        // a reminder of the dues, a statement sent, and the file that went with its email.
        var goldTier = new ClubCatalogItem
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Kind = CatalogKinds.Membership, Name = "Gold", Price = 299, Currency = "CHF",
            Cadence = "Monthly", ChargeKind = "Dues", CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        subscription.CatalogItemId = goldTier.Id;
        dues.CatalogItemId = goldTier.Id;
        var billingRun = new ClubBillingRun
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Trigger = BillingRunTriggers.Schedule, ScheduledFor = DateOnly.FromDateTime(now),
            Status = BillingRunStatuses.Succeeded, StartedAtUtc = now, Currency = "CHF",
        };
        db.AddRange(goldTier, billingRun,
            new ClubPaymentReminder
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ChargeId = dues.Id, MemberId = member.Id, Step = 1, DaysAfterDue = 3,
                OutboundMessageId = sentEmail.Id, RunId = billingRun.Id, CreatedAtUtc = now,
            },
            new ClubStatementDelivery
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, PeriodStartUtc = now.AddMonths(-1), PeriodEndUtc = now,
                Source = StatementSources.Monthly, OutboundMessageId = sentEmail.Id, RunId = billingRun.Id, Currency = "CHF", CreatedAtUtc = now,
            },
            new OutboundAttachment
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, OutboundMessageId = sentEmail.Id, FileName = "statement.pdf",
                ContentType = "application/pdf", Content = [1, 2, 3], CreatedAtUtc = now,
            });
        // An event with someone on its list whose ticket is on the ledger, and a booking of a
        // resource with its deposit: rows that point at the event, the member, the resource and
        // their charges, which the copy must point at its own copies of.
        var gala = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Gala", StartAtUtc = now.AddDays(7), EndAtUtc = now.AddDays(7).AddHours(3), Capacity = 40, MemberPrice = 30, Status = "Published", CreatedAtUtc = now, UpdatedAtUtc = now };
        var court = new ClubResource { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Court", Kind = "Court", Capacity = 4, DepositAmount = 299, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.AddRange(gala, court,
            new ClubEventRegistration { Id = Guid.NewGuid(), OrganizationId = org.Id, EventId = gala.Id, MemberId = member.Id, Name = "Ana Silva", Status = "Reserved", TicketPrice = 30, ChargeId = tabCharge.Id, RegisteredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now },
            new ClubReservation { Id = Guid.NewGuid(), OrganizationId = org.Id, ResourceId = court.Id, ResourceName = "Court", MemberId = member.Id, MemberName = "Ana Silva", StartAtUtc = now.AddDays(1), EndAtUtc = now.AddDays(1).AddHours(1), Status = "Confirmed", DepositAmount = 299, DepositChargeId = dues.Id, CreatedAtUtc = now, UpdatedAtUtc = now });
        // Money from outside: the Stripe and bank settings, a saved card the dues are charged
        // to, a charge of it still with Stripe and the event Stripe posted about it, a statement's
        // QR reference, and a bank file whose transfer paid in. The copy takes no money.
        var bankImport = new ClubBankImport { Id = Guid.NewGuid(), OrganizationId = org.Id, FileName = "camt054.xml", Format = "camt.054", Transactions = 1, Matched = 1, ImportedAtUtc = now };
        db.AddRange(bankImport,
            new ClubPaymentSettings
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, StripeSecretKeyProtected = "protected-key", StripeKeyHint = "sk_live_…abcd",
                StripeWebhookSecretProtected = "protected-secret", WebhookKey = Guid.NewGuid(), Iban = "CH5800791123000889012", CreatedAtUtc = now,
            },
            new ClubPaymentProfile
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, CustomerId = $"cus_{name}", PaymentMethodId = $"pm_{name}",
                PaymentMethodType = "card", Brand = "visa", Last4 = "4242", AutoPayDues = true, AutoPayAgreedAtUtc = now, CreatedAtUtc = now,
            },
            new ClubOnlinePayment
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Purpose = OnlinePaymentPurposes.AutoPay,
                Status = OnlinePaymentStatuses.Processing, Amount = 299, Description = "Gold dues", ChargeIds = dues.Id.ToString(),
                AttemptKey = $"autopay:{dues.Id}:1", PaymentIntentId = $"pi_{name}", CreatedAtUtc = now,
            },
            new ClubProviderEvent { Id = Guid.NewGuid(), OrganizationId = org.Id, EventId = $"evt_{name}", Type = "payment_intent.processing", ObjectId = $"pi_{name}", Status = "processed", ReceivedAtUtc = now },
            new ClubPaymentReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Number = 1, ScorReference = ACI.Application.Common.Payments.PaymentReferences.CreditorReference(1),
                MemberId = member.Id, Purpose = "statement", PeriodStartUtc = now.AddMonths(-1), PeriodEndUtc = now, AmountDue = 299, CreatedAtUtc = now,
            },
            new ClubBankTransaction
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ImportId = bankImport.Id, BankReference = $"CH5800791123000889012:{name}-1", Amount = 10,
                BookedAtUtc = now, Status = BankTransactionStatuses.Matched, MemberId = member.Id, LedgerPaymentId = payment.Id, CreatedAtUtc = now,
            });
        // The deposit the contract asks for, with the QR reference printed for it, still to be
        // paid: the copy points both at its own contract and keeps the payment with the original.
        var depositReference = new ClubPaymentReference
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Number = 2, ScorReference = ACI.Application.Common.Payments.PaymentReferences.CreditorReference(2),
            ContractId = contract.Id, Purpose = "deposit", AmountDue = 500, CreatedAtUtc = now,
        };
        db.AddRange(depositReference,
            new ContractDeposit
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ContractId = contract.Id, Amount = 500, Currency = "CHF", Status = ContractDepositStatuses.Awaiting,
                CheckoutSessionId = $"cs_{name}", CheckoutUrl = $"https://checkout.stripe.com/c/pay/cs_{name}", CheckoutExpiresAtUtc = now.AddHours(20),
                CheckoutAttempts = 1, PaymentReferenceId = depositReference.Id, CreatedAtUtc = now, Version = Guid.NewGuid(),
            });
        db.AddRange(
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = lead.Id, OrganizationId = org.Id, ToStatus = "Qualified", ChangedByUserId = rep, ChangedAtUtc = now },
            new LeadScoringRule { Id = Guid.NewGuid(), OrganizationId = org.Id, Field = "source", MatchValue = "Website", Points = 10, IsActive = true },
            new LeadAssignmentRule { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Geneva desk", Kind = "territory", TerritoryId = territory.Id, QueueId = queue.Id, IsActive = true },
            new LeadCaptureForm { Id = Guid.NewGuid(), OrganizationId = org.Id, CreatedByUserId = owner, Name = "Website form", PublicToken = Guid.NewGuid().ToString("N"), IsActive = true, CreatedAtUtc = now },
            new LeadOpsSettings { Id = Guid.NewGuid(), OrganizationId = org.Id, SlaHours = 4 },
            new OnboardingSettings { Id = Guid.NewGuid(), OrganizationId = org.Id, WebhookUrl = $"https://portal.{name}.ch/onboarding", WebhookSecret = "s3cret", UpdatedAtUtc = now },
            // The member portal: on, at an address of its own, with the member signed in once.
            new MemberPortalSettings { Id = Guid.NewGuid(), OrganizationId = org.Id, Enabled = true, PortalKey = $"key-{name}", MemberCancelHours = 12, UpdatedAtUtc = now },
            new MemberPortalAccount { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, InvitedAtUtc = now, InvitationsSent = 1, FirstSignInAtUtc = now, CreatedAtUtc = now },
            new MemberPortalSession { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(30), LastSeenAtUtc = now },
            new MemberPortalLink { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, TokenHash = $"hash-{name}", CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(3) },
            new EmailImage { Id = Guid.NewGuid(), OrganizationId = org.Id, FileName = "logo.png", ContentType = "image/png", Size = 4, Content = [0x89, 0x50, 0x4E, 0x47], CreatedAtUtc = now, CreatedByUserId = owner },
            new DealOnboarding
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, DealId = deal.Id, ContactId = contact.Id, ClubMemberId = member.Id, Name = "Jean Dupont",
                Email = $"jean@{name}.ch", Status = OnboardingStatuses.Welcomed, CreatedAtUtc = now, CreatedByUserId = owner, UpdatedAtUtc = now,
            });
        // The public application form, an application approved as the member from the lead and
        // contact, and a household with the member as primary and the contact (not a member) as
        // spouse: rows that point at the member, the lead, the contact and the household.
        var household = new ClubHousehold { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Silva", CreatedAtUtc = now };
        db.AddRange(household,
            new ClubApplicationForm { Id = Guid.NewGuid(), OrganizationId = org.Id, PublicToken = Guid.NewGuid().ToString("N"), IsActive = true, CreatedByUserId = owner, CreatedAtUtc = now },
            new ClubApplication
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, FirstName = "Ana", LastName = "Silva", Email = "ana@example.ch", RequestedTier = "Gold",
                Status = ApplicationStatuses.Approved, Source = ApplicationSources.PublicForm, SubmittedAtUtc = now, LeadId = lead.Id, ContactId = contact.Id,
                MemberId = member.Id, HouseholdId = household.Id, DuesSubscriptionId = subscription.Id, CreatedAtUtc = now,
            },
            new ClubHouseholdMember { Id = Guid.NewGuid(), OrganizationId = org.Id, HouseholdId = household.Id, MemberId = member.Id, Role = HouseholdRoles.Primary, IsPrimary = true, CreatedAtUtc = now },
            new ClubHouseholdMember { Id = Guid.NewGuid(), OrganizationId = org.Id, HouseholdId = household.Id, ContactId = contact.Id, Role = HouseholdRoles.Spouse, CreatedAtUtc = now });
        // Sales and the POS: a ticket imported from Lightspeed and the sale it was posted as, with
        // its line, the member's code at the POS, and the connection, whose tokens stay behind.
        var posTicket = new ClubPosTicket
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, Provider = PosProviders.LightspeedK, ExternalId = $"46100001:{name}-R1", ReceiptNumber = "R1",
            ClosedAtUtc = now, Currency = "CHF", Total = 18.50m, OnAccountAmount = 18.50m, Status = PosTicketStatuses.Posted, Attempts = 1,
            MemberId = member.Id, MemberName = "Ana Silva", MatchedBy = "code", ImportedAtUtc = now,
        };
        var sale = new ClubSale
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Source = PosProviders.LightspeedK,
            PosTicketId = posTicket.Id, Venue = "Bar", Tender = SaleTenders.HouseAccount, Subtotal = 18.50m, Total = 18.50m, OwedAmount = 18.50m,
            Currency = "CHF", ChargeIds = dues.Id.ToString(), IdempotencyKey = $"sale:{name}", SoldAtUtc = now, CreatedAtUtc = now,
        };
        posTicket.SaleId = sale.Id;
        db.AddRange(posTicket, sale,
            new ClubSaleLine { Id = Guid.NewGuid(), OrganizationId = org.Id, SaleId = sale.Id, Name = "Negroni", Quantity = 1, UnitPrice = 18.50m, Total = 18.50m, VatRate = 8.1m, Kind = "Bar" },
            new ClubMemberCode { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, Kind = MemberCodeKinds.Card, Code = "1042", CreatedAtUtc = now },
            new ClubPosSettings
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Enabled = true, RefreshTokenProtected = "protected-refresh", AccessTokenProtected = "protected-access",
                AccessTokenExpiresAtUtc = now.AddHours(1), ConnectedAtUtc = now, LocationId = "46100001", DefaultVenue = "Bar", CreatedAtUtc = now,
            });
        // Privacy: the workspace's settings, an erasure in its log and a retention run. The copy
        // holds the original's records, erasures and all, so it keeps their log and its periods.
        db.AddRange(
            new PrivacySettings
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ControllerName = name, MarketingConsent = MarketingConsentModes.Required,
                LeadRetentionMonths = 24, CreatedAtUtc = now, Version = Guid.NewGuid(),
            },
            new PersonErasure
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Number = 1, Trigger = PersonErasureTriggers.Request, ErasedAtUtc = now,
                ErasedByUserId = owner, ErasedByName = "Owner", Reference = "Letter of 3 May", SummaryJson = "{\"Leads\":1}",
            },
            new RetentionRun
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Trigger = RetentionRunTriggers.Schedule, ScheduledFor = DateOnly.FromDateTime(now),
                Status = RetentionRunStatuses.Succeeded, StartedAtUtc = now, FinishedAtUtc = now, LeadsDeleted = 1,
            });
        // The assistant: its settings and a morning brief that went out. The month's calls to the
        // language model stay with the original (left out), so the copy starts its month afresh.
        db.AddRange(
            new AssistantSettings { Id = Guid.NewGuid(), OrganizationId = org.Id, MonthlyLimit = 120, MorningBriefs = true, CreatedAtUtc = now, Version = Guid.NewGuid() },
            new AssistantBrief
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, UserId = manager, Day = DateOnly.FromDateTime(now), Status = AssistantBriefStatuses.Sent,
                Items = 3, CreatedAtUtc = now, SentAtUtc = now,
            },
            new AiUsage { Id = Guid.NewGuid(), OrganizationId = org.Id, Month = new DateOnly(now.Year, now.Month, 1), Calls = 42, UpdatedAtUtc = now });
        // Keeping members and referrals: the rules (with the save email on, which the copy turns
        // off), the member's at-risk score with the task and email its save actions made, a
        // renewal staff marked contacted, a complaint, a scoring run, and a member she brought
        // in with the points it earned her: rows that point at members, a subscription, a task,
        // an email and a loyalty entry, which the copy must point at its own copies of.
        var referred = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, OwnerUserId = owner, FirstName = "Luca", LastName = "Rossi", Email = $"luca@{name}.ch",
            Tier = "Gold", Status = "Active", ReferredByMemberId = member.Id,
        };
        var bonusPoints = new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Reason = "Referral", Points = 500, CreatedAtUtc = now };
        db.AddRange(referred, bonusPoints,
            new ClubRetentionSettings
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, SaveTask = true, SaveEmail = true, ReferralBonus = ReferralBonusKinds.Points,
                ReferralBonusPoints = 500, SponsorRule = SponsorRules.Required, CreatedAtUtc = now, Version = Guid.NewGuid(),
            },
            new ClubMemberRisk
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, Score = 72, Level = RiskLevels.AtRisk, FactorsJson = "[]",
                ComputedAtUtc = now, AtRiskSinceUtc = now, SaveActionsAtUtc = now, SaveTaskId = task.Id, SaveMessageId = sentEmail.Id,
            },
            new ClubRenewal
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, Source = RenewalSources.Subscription, SubscriptionId = subscription.Id,
                EndsAtUtc = now.AddDays(30), Stage = RenewalStages.Contacted, ContactedAtUtc = now, ContactedByUserId = owner, CreatedAtUtc = now, Version = Guid.NewGuid(),
            },
            new ClubMemberConcern { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, Kind = ConcernKinds.Complaint, Note = "Slow service at the bar", RecordedAtUtc = now, RecordedByUserId = owner },
            new ClubScoringRun
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, Trigger = ScoringRunTriggers.Schedule, ScheduledFor = DateOnly.FromDateTime(now),
                Status = "succeeded", StartedAtUtc = now, FinishedAtUtc = now, MembersScored = 2, BecameAtRisk = 1,
            },
            new ClubReferralBonus
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferrerMemberId = member.Id, ReferredMemberId = referred.Id, Kind = ReferralBonusKinds.Points,
                Status = ReferralBonusStatuses.Awarded, Points = 500, Currency = "CHF", LoyaltyEntryId = bonusPoints.Id, CreatedAtUtc = now,
                AwardedAtUtc = now, AwardedByUserId = owner,
            });
        // A company's corporate membership with a nominee, and its house account (keyed by the
        // company's id) with a charge a nominee ran up on it: the copy's must name the copy's company.
        var corporate = new ClubCorporateMembership
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id, CompanyId = company.Id, CompanyName = "Acme", Name = "Acme corporate", Seats = 2,
            StartsAtUtc = now, RenewsAtUtc = now.AddYears(1), BillingContactId = contact.Id, BillingEmail = $"accounts@{name}.ch",
            CreatedAtUtc = now, UpdatedAtUtc = now, EditVersion = Guid.NewGuid(),
        };
        db.AddRange(corporate,
            new ClubCorporateNominee { Id = Guid.NewGuid(), OrganizationId = org.Id, CorporateMembershipId = corporate.Id, MemberId = member.Id, MemberName = "Ana Silva", AddedAtUtc = now },
            new ClubHouseAccount { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = company.Id, CompanyId = company.Id, Currency = "CHF", OpenedAtUtc = now },
            new ClubCharge
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = company.Id, MemberName = "Acme", Kind = "Bar", Description = "Negroni", Amount = 18m,
                Currency = "CHF", PostedAtUtc = now, IncurredByMemberId = member.Id, IncurredByName = "Ana Silva", CreatedAtUtc = now,
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Seeded(org.Id, owner, manager, rep, lead.Id, deal.Id, contact.Id, member.Id, rule.Id, sequence.Id);
    }

    /// <remarks>
    /// The context is the app's, filtered to a workspace, and acts for none, as the clone
    /// tool's does; so the cloner must read past the filters itself. What the tests check
    /// afterwards they read across workspaces (IgnoreQueryFilters).
    /// </remarks>
    private static async Task<(SqliteConnection Connection, AppDbContext Db, Seeded Original, Seeded Other, Guid Personal)> WorldAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        var db = SqliteDb.Open(connection);
        var people = Enumerable.Range(0, 4).Select(i => new User { Id = Guid.NewGuid(), Name = $"Person {i}", Email = $"p{i}@example.ch", CreatedAtUtc = DateTime.UtcNow }).ToList();
        db.Users.AddRange(people);
        await db.SaveChangesAsync();
        var original = await SeedAsync(db, "Pavillon46", people[0].Id, people[1].Id, people[2].Id);
        var other = await SeedAsync(db, "Another club", people[3].Id, people[1].Id, people[2].Id);
        db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = people[0].Id, Name = "Personal lead", Email = "x@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (connection, db, original, other, people[0].Id);
    }

    /// <summary>Every row of every copied table, by table, for one organization.</summary>
    private static async Task<Dictionary<string, int>> CountsAsync(AppDbContext db, Guid org)
    {
        var counts = new Dictionary<string, int>();
        var count = typeof(OrganizationCloneTests).GetMethod(nameof(CountOf), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var type in OrganizationCloner.OrganizationTables(db.Model))
            counts[type.GetTableName()!] = await (Task<int>)count.MakeGenericMethod(type.ClrType).Invoke(null, [db, org])!;
        return counts;
    }

    private static Task<int> CountOf<T>(AppDbContext db, Guid org) where T : class =>
        db.Model.FindEntityType(typeof(T))!.FindProperty("OrganizationId")!.IsNullable
            ? db.Set<T>().IgnoreQueryFilters().CountAsync(r => EF.Property<Guid?>(r, "OrganizationId") == org)
            : db.Set<T>().IgnoreQueryFilters().CountAsync(r => EF.Property<Guid>(r, "OrganizationId") == org);

    [Fact]
    public void EveryTableIsCopiedOrLeftOutOnPurpose()
    {
        // A table added later is caught here: it is either copied or listed, with a reason, as left out.
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var db = SqliteDb.Open(connection);
        var covered = OrganizationCloner.OrganizationTables(db.Model).Select(t => t.ClrType)
            .Concat(OrganizationCloner.Children.Select(c => c.Child))
            .Concat(OrganizationCloner.LeftOut.Keys)
            .ToHashSet();
        db.Model.GetEntityTypes().Select(t => t.ClrType).Where(t => !covered.Contains(t))
            .Should().BeEmpty("each table must be copied with the organization or left out with a reason");
        db.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.IsShadowProperty())
            .Should().BeEmpty("the copy reads values through the entity classes, so a column with no property would be lost");
    }

    [Fact]
    public async Task TheCopyHasEverythingAndPointsOnlyAtItself()
    {
        var (connection, db, original, other, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var originalBefore = await CountsAsync(db, original.Org);
        originalBefore.Where(c => c.Value == 0).Select(c => c.Key)
            .Should().BeEmpty("the original fills every table, so the copy of each one is exercised");
        var otherBefore = await CountsAsync(db, other.Org);
        var cloner = new OrganizationCloner(db);

        var report = await cloner.CloneAsync(original.Org, "Pavillon 46 v2");

        (await cloner.VerifyAsync(report)).Should().BeEmpty();
        var copy = await CountsAsync(db, report.CopyId);
        (await db.JoinRequests.IgnoreQueryFilters().CountAsync(r => r.OrganizationId == report.CopyId)).Should().Be(0, "requests to join the original stay with it");
        report.RowsLeftOut["JoinRequests"].Should().Be(1);
        copy["Invites"].Should().Be(1, "only the pending invitation; the accepted one brought someone in already");
        copy.Where(c => c.Key != "Invites").Should().Equal(originalBefore.Where(c => c.Key != "Invites"));
        foreach (var (child, key, _) in OrganizationCloner.Children)
            report.RowsCopied[db.Model.FindEntityType(child)!.GetTableName()!].Should().BeGreaterThan(0, $"{child.Name} hangs off copied rows");

        (await CountsAsync(db, original.Org)).Should().Equal(originalBefore, "the original is not changed");
        (await CountsAsync(db, other.Org)).Should().Equal(otherBefore, "another organization is not touched");
        (await db.Leads.IgnoreQueryFilters().CountAsync(l => l.OrganizationId == null)).Should().Be(1, "personal records stay personal");
    }

    [Fact]
    public async Task TheCopyTakesNoMoneyOnline_AndChargesNoSavedCard()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var source = await db.ClubPaymentSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == original.Org);
        var copied = await db.ClubPaymentSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == report.CopyId);
        copied.StripeSecretKeyProtected.Should().BeNull("the Stripe key stays with the original");
        copied.StripeWebhookSecretProtected.Should().BeNull();
        copied.WebhookKey.Should().NotBe(source.WebhookKey, "Stripe's posts go to one workspace");
        copied.Iban.Should().Be(source.Iban, "the bank account is the same club's");
        source.StripeSecretKeyProtected.Should().Be("protected-key", "the original keeps taking money");

        (await db.ClubPaymentProfiles.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == report.CopyId)).AutoPayDues.Should().BeFalse();
        (await db.ClubPaymentProfiles.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == original.Org)).AutoPayDues.Should().BeTrue();
        (await db.ClubOnlinePayments.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == report.CopyId)).Status.Should().Be(OnlinePaymentStatuses.Canceled);
        (await db.ClubOnlinePayments.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == original.Org)).Status.Should().Be(OnlinePaymentStatuses.Processing);
        report.Adjustments.Keys.Should().Contain("Payment settings keep the Stripe key and webhook with the original")
            .And.Contain("Saved cards are not charged for dues from the copy")
            .And.Contain("Deposits not paid yet are paid to the original only");

        // The signer of a contract out for signature pays its deposit to the original.
        var deposit = await db.ContractDeposits.IgnoreQueryFilters().SingleAsync(d => d.OrganizationId == report.CopyId);
        (deposit.Status, deposit.CheckoutUrl, deposit.CheckoutSessionId).Should().Be((ContractDepositStatuses.Cancelled, (string?)null, (string?)null));
        (await db.Contracts.IgnoreQueryFilters().SingleAsync(c => c.Id == deposit.ContractId)).OrganizationId.Should().Be(report.CopyId);
        (await db.ClubPaymentReferences.IgnoreQueryFilters().SingleAsync(r => r.Id == deposit.PaymentReferenceId)).ContractId.Should().Be(deposit.ContractId);
        (await db.ContractDeposits.IgnoreQueryFilters().SingleAsync(d => d.OrganizationId == original.Org)).Status.Should().Be(ContractDepositStatuses.Awaiting);
    }

    [Fact]
    public async Task TheCopyImportsNothingFromTheOriginalsPos()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        // Both importing the same location's tickets would post every bill to the same people twice.
        var copied = await db.ClubPosSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == report.CopyId);
        copied.RefreshTokenProtected.Should().BeNull("the Lightspeed account stays with the original");
        copied.AccessTokenProtected.Should().BeNull();
        copied.Enabled.Should().BeFalse();
        copied.LocationId.Should().Be("46100001", "the location is the same club's, ready if the copy is connected");
        var source = await db.ClubPosSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == original.Org);
        source.RefreshTokenProtected.Should().Be("protected-refresh", "the original keeps importing");
        source.Enabled.Should().BeTrue();
        report.Adjustments.Keys.Should().Contain("The POS connection stays with the original, so no ticket is imported twice");
    }

    [Fact]
    public async Task TheSamePeopleWorkInBoth()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var org = await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Id == report.CopyId);
        org.Name.Should().Be("Pavillon 46 v2");
        org.OwnerUserId.Should().Be(original.Owner);
        (await db.OrganizationMembers.IgnoreQueryFilters().Where(m => m.OrganizationId == report.CopyId).OrderBy(m => m.Role).Select(m => new { m.UserId, m.Role }).ToListAsync())
            .Should().BeEquivalentTo(await db.OrganizationMembers.IgnoreQueryFilters().Where(m => m.OrganizationId == original.Org).OrderBy(m => m.Role).Select(m => new { m.UserId, m.Role }).ToListAsync());
        var lead = await db.Leads.IgnoreQueryFilters().SingleAsync(l => l.OrganizationId == report.CopyId);
        lead.AssignedToUserId.Should().Be(original.Rep, "people are shared, not copied");
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task LinksBetweenRecordsLeadToTheCopies_InColumnsAndInsideText()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var lead = await db.Leads.IgnoreQueryFilters().SingleAsync(l => l.OrganizationId == report.CopyId);
        var deal = await db.Deals.IgnoreQueryFilters().SingleAsync(d => d.OrganizationId == report.CopyId);
        var contact = await db.Contacts.IgnoreQueryFilters().SingleAsync(c => c.OrganizationId == report.CopyId && c.Name == "Jean Dupont");
        new[] { lead.Id, deal.Id, contact.Id }.Should().NotIntersectWith(new[] { original.Lead, original.Deal, original.Contact });
        lead.ConvertedToDealId.Should().Be(deal.Id, "the converted lead's loop is rebuilt between the copies");
        lead.ConvertedToContactId.Should().Be(contact.Id);
        deal.ContactId.Should().Be(contact.Id);
        contact.ConvertedFromLeadId.Should().Be(lead.Id);
        var couple = await db.ContactRelationships.IgnoreQueryFilters().SingleAsync(r => r.OrganizationId == report.CopyId);
        couple.ContactId.Should().Be(contact.Id, "a relationship links the copied people, not the originals");
        (await db.Contacts.IgnoreQueryFilters().SingleAsync(c => c.Id == couple.RelatedContactId)).OrganizationId.Should().Be(report.CopyId);
        deal.IsPrivate.Should().BeTrue();
        var stage = await db.DealStages.IgnoreQueryFilters().SingleAsync(s => s.Id == deal.DealStageId);
        (await db.Pipelines.IgnoreQueryFilters().SingleAsync(p => p.Id == stage.PipelineId)).OrganizationId.Should().Be(report.CopyId);
        (await db.DealLineItems.IgnoreQueryFilters().SingleAsync(i => i.DealId == deal.Id)).UnitPrice.Should().Be(12000);
        (await db.Attachments.IgnoreQueryFilters().SingleAsync(a => a.OrganizationId == report.CopyId)).RecordId.Should().Be(deal.Id);
        (await db.Attachments.IgnoreQueryFilters().SingleAsync(a => a.OrganizationId == report.CopyId)).Content.Should().Equal("%PDF"u8.ToArray());

        var member = await db.ClubMembers.IgnoreQueryFilters().SingleAsync(m => m.OrganizationId == report.CopyId && m.FirstName == "Ana");
        // The member she brought in names the copied her, and so does the bonus it earned her.
        var referred = await db.ClubMembers.IgnoreQueryFilters().SingleAsync(m => m.OrganizationId == report.CopyId && m.FirstName == "Luca");
        referred.ReferredByMemberId.Should().Be(member.Id);
        var bonus = await db.ClubReferralBonuses.IgnoreQueryFilters().SingleAsync(b => b.OrganizationId == report.CopyId);
        (bonus.ReferrerMemberId, bonus.ReferredMemberId).Should().Be((member.Id, referred.Id));
        (await db.LoyaltyLedger.IgnoreQueryFilters().SingleAsync(e => e.Id == bonus.LoyaltyEntryId)).OrganizationId.Should().Be(report.CopyId);
        (await db.ConsentRecords.IgnoreQueryFilters().SingleAsync(c => c.OrganizationId == report.CopyId)).SubjectId
            .Should().Be(member.Id, "a consent answer belongs to the copied member, not the original");
        var copiedCampaigns = await db.Campaigns.IgnoreQueryFilters().Where(c => c.OrganizationId == report.CopyId).ToListAsync();
        copiedCampaigns.Single(c => c.Name == "Autumn gala").Should().Match<Campaign>(c => c.Status == CampaignStatuses.Draft && c.ScheduledAtUtc == null,
            "the members are the same people: a scheduled campaign goes out once, from the original");
        var copiedMessage = await db.MemberMessages.IgnoreQueryFilters().SingleAsync(m => m.OrganizationId == report.CopyId);
        copiedMessage.MemberId.Should().Be(member.Id);
        copiedMessage.CampaignId.Should().Be(copiedCampaigns.Single(c => c.Name == "Spring news").Id);
        (await db.OutboundMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == copiedMessage.OutboundMessageId)).OrganizationId.Should().Be(report.CopyId);
        (await db.ClubDocuments.IgnoreQueryFilters().SingleAsync(d => d.OrganizationId == report.CopyId)).Payload
            .Should().Contain(member.Id.ToString().ToUpperInvariant(), "an id kept in JSON is the copy's, in the same case");
        var sequence = await db.OutreachSequences.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == report.CopyId);
        var rule = await db.AutomationRules.IgnoreQueryFilters().SingleAsync(r => r.OrganizationId == report.CopyId);
        rule.ActionsJson.Should().Contain(sequence.Id.ToString()).And.Contain(original.Rep.ToString(), "the person stays the same person");
        (await db.AutomationFires.IgnoreQueryFilters().SingleAsync(f => f.RuleId == rule.Id)).SourceKey.Should().Be(lead.Id.ToString(), "what already fired for the lead counts as fired for its copy");
        var task = await db.TaskItems.IgnoreQueryFilters().SingleAsync(t => t.OrganizationId == report.CopyId);
        (await db.Notifications.IgnoreQueryFilters().Where(n => n.OrganizationId == report.CopyId).Select(n => n.Link).ToListAsync()).Should().Contain($"/tasks/{task.Id}");
    }

    [Fact]
    public async Task AnOlderDealKeepsTheWordsItsAmountWasTypedWith()
    {
        // A deal from before amounts were numbers: the text as typed, and the number read
        // from it once. The copy rewrote the text from the number ("12000"), so the copy
        // lost the "+ VAT" that the original still has.
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        await db.Deals.IgnoreQueryFilters().Where(d => d.Id == original.Deal)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.Value, "CHF 12,000 + VAT").SetProperty(d => d.Amount, 12000m));

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var copy = await db.Deals.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.OrganizationId == report.CopyId);
        copy.Value.Should().Be("CHF 12,000 + VAT");
        copy.Amount.Should().Be(12000m);
    }

    [Fact]
    public async Task NothingThatExistsEmailsAnyoneTwice_AndTheWaysInStayWithTheOriginal()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var originalOrg = await db.Organizations.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == original.Org);

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var org = await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Id == report.CopyId);
        new[] { org.WebhookApiKeyHash, org.WebhookPassword, org.OutboundWebhookUrl, org.OutboundWebhookSecret, org.McpApiKeyHash }
            .Should().OnlyContain(v => v == null, "a copied MCP key would open both workspaces, whichever the lookup found first");
        org.McpApiKeyCreatedAtUtc.Should().BeNull();
        var portal = await db.OnboardingSettings.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == report.CopyId);
        new[] { portal.WebhookUrl, portal.WebhookSecret }.Should().OnlyContain(v => v == null,
            "a customer won in the copy is never handed to the original's portal, signed as the original");
        (await db.OnboardingSettings.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == original.Org)).WebhookSecret.Should().Be("s3cret");
        // The same members, so the copy opens no second member portal to them until it is turned on
        // there, at an address of its own; nobody is signed in to it, and no link signs in to it.
        var memberPortal = await db.MemberPortalSettings.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == report.CopyId);
        memberPortal.Enabled.Should().BeFalse();
        memberPortal.MemberCancelHours.Should().Be(12, "the club's rules come along");
        memberPortal.PortalKey.Should().NotBe("key-Pavillon46").And.NotBeNullOrWhiteSpace();
        (await db.MemberPortalSettings.IgnoreQueryFilters().SingleAsync(p => p.OrganizationId == original.Org)).Enabled.Should().BeTrue();
        (await db.MemberPortalSessions.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == report.CopyId)).Should().Be(0);
        (await db.MemberPortalLinks.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == report.CopyId)).Should().Be(0);
        (await db.MemberPortalAccounts.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == report.CopyId)).Should().Be(0);
        report.Adjustments.Keys.Should().Contain("The member portal starts off in the copy, at an address of its own");
        // The same members again: a member who becomes at risk in the copy is not emailed from it
        // as well as from the original. The club's other rules come along.
        var retention = await db.ClubRetentionSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == report.CopyId);
        (retention.SaveEmail, retention.SaveTask, retention.ReferralBonusPoints, retention.SponsorRule).Should().Be((false, true, 500, SponsorRules.Required));
        (await db.ClubRetentionSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == original.Org)).SaveEmail.Should().BeTrue();
        report.Adjustments.Keys.Should().Contain("Members found at risk in the copy are not emailed from it");
        (await db.OutreachEnrollments.IgnoreQueryFilters().Where(e => e.OrganizationId == report.CopyId).Select(e => e.Status).ToListAsync())
            .Should().BeEquivalentTo(["paused", "stopped"]);
        var contract = await db.Set<Contract>().IgnoreQueryFilters().SingleAsync(c => c.OrganizationId == report.CopyId);
        contract.SigningTokenHash.Should().BeNull("the signer's link opens the original");
        contract.ReminderSentAtUtc.Should().NotBeNull("the signer is not reminded a second time");
        contract.Body.Should().NotContain(original.Deal.ToString());
        (await db.TaskItems.IgnoreQueryFilters().SingleAsync(t => t.OrganizationId == report.CopyId)).ReminderSentAtUtc.Should().NotBeNull();
        (await db.LeadCaptureForms.IgnoreQueryFilters().Where(f => f.OrganizationId != null).Select(f => f.PublicToken).ToListAsync()).Should().OnlyHaveUniqueItems();
        (await db.Notifications.IgnoreQueryFilters().Where(n => n.OrganizationId == report.CopyId).Select(n => n.SourceKey).ToListAsync()).Should().Contain((string?)null);
        // An event still queued in the original is posted once, from the original; replies
        // and delivery reports name the original's Message-ID and reply address.
        var copiedMessages = await db.OutboundMessages.IgnoreQueryFilters().Where(m => m.OrganizationId == report.CopyId).ToListAsync();
        copiedMessages.Single(m => m.Channel == OutboundChannels.Webhook).Status.Should().Be(OutboundStatuses.Cancelled);
        var copiedEmail = copiedMessages.Single(m => m.Channel == OutboundChannels.Email);
        copiedEmail.MessageIdHeader.Should().Be($"<{copiedEmail.Id:N}@pavillon46.ch>");
        copiedEmail.ReplyToken.Should().BeNull();
        (await db.OutboundMessages.IgnoreQueryFilters().CountAsync(m => m.OrganizationId == original.Org && m.Status == OutboundStatuses.Queued)).Should().Be(1);
        // A rule's run still waiting is carried on by the original alone, or its email would go twice.
        var copiedRun = await db.AutomationRuns.IgnoreQueryFilters().SingleAsync(r => r.OrganizationId == report.CopyId);
        copiedRun.Status.Should().Be(AutomationRunStatuses.Cancelled);
        copiedRun.NextAttemptAtUtc.Should().BeNull();
        (await db.AutomationRuns.IgnoreQueryFilters().SingleAsync(r => r.OrganizationId == original.Org)).Status.Should().Be(AutomationRunStatuses.Waiting);

        // The original is exactly as it was.
        var after = await db.Organizations.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == original.Org);
        after.Should().BeEquivalentTo(originalOrg);
        (await db.OutreachEnrollments.IgnoreQueryFilters().CountAsync(e => e.OrganizationId == original.Org && e.Status == "active")).Should().Be(1);
        (await db.Set<Contract>().IgnoreQueryFilters().SingleAsync(c => c.OrganizationId == original.Org)).SigningTokenHash.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePlanCountsWhatTheCopyHolds_WithoutWritingAnything()
    {
        // The dry run used to make the whole copy and roll it back, in the database every
        // tenant shares. It now only counts, and has to count what the copy really makes.
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var organizations = await db.Organizations.IgnoreQueryFilters().CountAsync();
        var before = await CountsAsync(db, original.Org);
        var cloner = new OrganizationCloner(db);

        var plan = await cloner.PlanAsync(original.Org);

        (await db.Organizations.IgnoreQueryFilters().CountAsync()).Should().Be(organizations, "planning makes no copy");
        (await CountsAsync(db, original.Org)).Should().Equal(before);
        db.ChangeTracker.Entries().Should().BeEmpty();
        plan.AttachmentBytes.Should().Be("%PDF"u8.Length);

        var report = await cloner.CloneAsync(original.Org, "Pavillon 46 v2");
        plan.RowsToCopy.Should().Equal(report.RowsCopied);
        plan.RowsLeftOut.Should().Equal(report.RowsLeftOut);
    }

    [Fact]
    public async Task CopyingWritesNoHistoryOfItsOwn()
    {
        // The original's history is copied as it is. Each copied record would otherwise get
        // a fresh "created" line, dated today, on top of it.
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var before = await db.AuditEvents.IgnoreQueryFilters().CountAsync(e => e.OrganizationId == original.Org);

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        (await db.AuditEvents.IgnoreQueryFilters().CountAsync(e => e.OrganizationId == report.CopyId)).Should().Be(before);
        db.RecordsAuditTrail.Should().BeTrue("the switch is back on for every save after the copy");
    }

    [Fact]
    public async Task TheCheckCatchesACopyThatPointsBack()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var cloner = new OrganizationCloner(db);
        var report = await cloner.CloneAsync(original.Org, "Pavillon 46 v2");

        var task = await db.TaskItems.IgnoreQueryFilters().SingleAsync(t => t.OrganizationId == report.CopyId);
        task.LeadId = original.Lead;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await cloner.VerifyAsync(report)).Should().ContainSingle(p => p.Contains("TaskItems.LeadId"));
    }

    [Fact]
    public async Task TheCheckCatchesARowTheCopyIsMissing()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var cloner = new OrganizationCloner(db);
        var report = await cloner.CloneAsync(original.Org, "Pavillon 46 v2");

        var deal = await db.Deals.IgnoreQueryFilters().SingleAsync(d => d.OrganizationId == report.CopyId);
        db.DealLineItems.RemoveRange(db.DealLineItems.IgnoreQueryFilters().Where(i => i.DealId == deal.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await cloner.VerifyAsync(report)).Should().Contain(p => p.StartsWith("DealLineItems: the original has 1 rows"));
    }
}

/// <summary>The copy through the app itself: the same people sign in and find both organizations.</summary>
public class OrganizationCloneThroughTheAppTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task EveryMemberHasBothAndTheyStaySeparate()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await Workspaces.SignInAsync(factory, "manager@example.ch");
        var org = await Workspaces.CreateAsync(factory, ownerId, "Pavillon46");
        var (rep, repId) = await Workspaces.SignInAsync(factory, "ana@pavillon46.ch");
        await Workspaces.AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        owner.In(org);
        await Json(await owner.PostAsync("/api/leads", Workspaces.Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "add lead");
        await Json(await owner.PostAsync("/api/members", Workspaces.Body(new { firstName = "Ana", lastName = "Silva", email = "ana@example.ch", tier = "Gold" })), "add club member");

        OrganizationCloneReport report;
        using (var scope = factory.Services.CreateScope())
        {
            var cloner = new OrganizationCloner(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            report = await cloner.CloneAsync(org, "Pavillon 46 v2");
            (await cloner.VerifyAsync(report)).Should().BeEmpty();
        }

        foreach (var person in new[] { owner, rep })
        {
            var mine = await Json(await person.GetAsync("/api/organizations"), "list organizations");
            mine.EnumerateArray().Select(o => o.GetProperty("name").GetString()).Should().BeEquivalentTo(["Pavillon46", "Pavillon 46 v2"]);
        }
        rep.In(report.CopyId);
        (await Json(await rep.GetAsync("/api/leads"), "the copy's leads")).ToString().Should().Contain("Jean Dupont");
        (await Json(await rep.GetAsync("/api/members"), "the copy's club members"))[0].GetProperty("firstName").GetString().Should().Be("Ana");

        await Json(await rep.PostAsync("/api/leads", Workspaces.Body(new { name = "Only in v2", email = "v2@example.ch", status = "New" })), "add a lead in the copy");
        (await Json(await owner.GetAsync("/api/leads"), "the original's leads")).ToString().Should().NotContain("Only in v2", "the two are separate from here on");
    }
}
