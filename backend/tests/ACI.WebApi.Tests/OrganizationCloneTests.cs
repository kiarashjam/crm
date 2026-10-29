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
            WebhookApiKey = $"key-{name}", WebhookApiKeyCreatedAtUtc = now, WebhookPassword = "secret",
            OutboundWebhookUrl = "https://hooks.example.com/cadence", OutboundWebhookSecret = "s3cret",
        };
        db.Organizations.Add(org);
        db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = org.Id, UserId = owner, Role = OrgMemberRole.Owner, JoinedAtUtc = now },
            new OrganizationMember { OrganizationId = org.Id, UserId = manager, Role = OrgMemberRole.Manager, JoinedAtUtc = now },
            new OrganizationMember { OrganizationId = org.Id, UserId = rep, Role = OrgMemberRole.Viewer, JoinedAtUtc = now });
        db.OrgSettings.Add(new OrgSettings { OrganizationId = org.Id, CompanyName = name, UpdatedAtUtc = now });
        db.Invites.AddRange(
            new Invite { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = $"new@{name}.ch", Token = Guid.NewGuid().ToString("N"), ExpiresAtUtc = now.AddDays(7), CreatedAtUtc = now },
            new Invite { Id = Guid.NewGuid(), OrganizationId = org.Id, Email = $"joined@{name}.ch", Token = Guid.NewGuid().ToString("N"), ExpiresAtUtc = now, CreatedAtUtc = now, AcceptedByUserId = rep, AcceptedAtUtc = now });
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
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Jean Dupont", Email = $"jean@{name}.ch", Status = "Qualified",
            CompanyId = company.Id, LeadSourceId = source.Id, LeadStatusId = status.Id, AssignedToUserId = rep, IsConverted = true, CreatedAtUtc = now,
        };
        var contact = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Jean Dupont", Email = $"jean@{name}.ch", CompanyId = company.Id, CreatedAtUtc = now };
        var deal = new Deal
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, Name = "Founding membership", Value = "12000", Currency = "CHF",
            PipelineId = pipeline.Id, DealStageId = stage.Id, CompanyId = company.Id, AssigneeId = rep, IsPrivate = true, CreatedAtUtc = now,
        };
        db.AddRange(lead, contact, deal);
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
        db.AddRange(task, contract, sequence, territory, queue, member, team, rule, definition, spouse);
        db.ContactRelationships.Add(new ContactRelationship { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org.Id, ContactId = contact.Id, RelatedContactId = spouse.Id, Type = ContactRelationshipTypes.Spouse, CreatedAtUtc = now });
        db.AddRange(
            new DealStageChange { Id = Guid.NewGuid(), DealId = deal.Id, FromDealStageId = stage.Id, ToDealStageId = won.Id, ChangedByUserId = owner, ChangedAtUtc = now },
            new DealLineItem { Id = Guid.NewGuid(), DealId = deal.Id, Name = "Membership", Quantity = 1, UnitPrice = 12000, CreatedAtUtc = now },
            new TaskComment { Id = Guid.NewGuid(), TaskItemId = task.Id, AuthorId = manager, Body = "Prefers mornings", CreatedAtUtc = now },
            new ContractEvent { Id = Guid.NewGuid(), ContractId = contract.Id, Type = "sent", ActorUserId = owner, AtUtc = now },
            new OutreachStep { Id = Guid.NewGuid(), SequenceId = sequence.Id, Type = "email", DayOffset = 1, Subject = "Hello" },
            new OutreachEnrollment { Id = Guid.NewGuid(), SequenceId = sequence.Id, OrganizationId = org.Id, EnrolledByUserId = owner, TargetId = lead.Id, TargetName = "Jean Dupont", Status = "active", EnrolledAtUtc = now, NextActionAtUtc = now.AddHours(1) },
            new OutreachEnrollment { Id = Guid.NewGuid(), SequenceId = sequence.Id, OrganizationId = org.Id, EnrolledByUserId = owner, TargetId = lead.Id, TargetName = "Jean Dupont", Status = "stopped", EnrolledAtUtc = now },
            new AutomationFire { Id = Guid.NewGuid(), RuleId = rule.Id, SourceKey = lead.Id.ToString(), FiredAtUtc = now },
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
            new ClubVisit { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", MemberTier = "Gold", Venue = "Lounge", VisitedAtUtc = now },
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org.Id, MemberId = member.Id, MemberName = "Ana Silva", Points = 450, CreatedAtUtc = now },
            new ClubDocument { Id = Guid.NewGuid(), OrganizationId = org.Id, Kind = "tabs", UpdatedAtUtc = now, Payload = JsonSerializer.Serialize(new { memberId = member.Id.ToString().ToUpperInvariant(), venue = "Bar" }) },
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = lead.Id, OrganizationId = org.Id, ToStatus = "Qualified", ChangedByUserId = rep, ChangedAtUtc = now },
            new LeadScoringRule { Id = Guid.NewGuid(), OrganizationId = org.Id, Field = "source", MatchValue = "Website", Points = 10, IsActive = true },
            new LeadAssignmentRule { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "Geneva desk", Kind = "territory", TerritoryId = territory.Id, QueueId = queue.Id, IsActive = true },
            new LeadCaptureForm { Id = Guid.NewGuid(), OrganizationId = org.Id, CreatedByUserId = owner, Name = "Website form", PublicToken = Guid.NewGuid().ToString("N"), IsActive = true, CreatedAtUtc = now },
            new LeadOpsSettings { Id = Guid.NewGuid(), OrganizationId = org.Id, SlaHours = 4 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Seeded(org.Id, owner, manager, rep, lead.Id, deal.Id, contact.Id, member.Id, rule.Id, sequence.Id);
    }

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
            ? db.Set<T>().CountAsync(r => EF.Property<Guid?>(r, "OrganizationId") == org)
            : db.Set<T>().CountAsync(r => EF.Property<Guid>(r, "OrganizationId") == org);

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
        (await db.JoinRequests.CountAsync(r => r.OrganizationId == report.CopyId)).Should().Be(0, "requests to join the original stay with it");
        report.RowsLeftOut["JoinRequests"].Should().Be(1);
        copy["Invites"].Should().Be(1, "only the pending invitation; the accepted one brought someone in already");
        copy.Where(c => c.Key != "Invites").Should().Equal(originalBefore.Where(c => c.Key != "Invites"));
        foreach (var (child, key, _) in OrganizationCloner.Children)
            report.RowsCopied[db.Model.FindEntityType(child)!.GetTableName()!].Should().BeGreaterThan(0, $"{child.Name} hangs off copied rows");

        (await CountsAsync(db, original.Org)).Should().Equal(originalBefore, "the original is not changed");
        (await CountsAsync(db, other.Org)).Should().Equal(otherBefore, "another organization is not touched");
        (await db.Leads.CountAsync(l => l.OrganizationId == null)).Should().Be(1, "personal records stay personal");
    }

    [Fact]
    public async Task TheSamePeopleWorkInBoth()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var org = await db.Organizations.SingleAsync(o => o.Id == report.CopyId);
        org.Name.Should().Be("Pavillon 46 v2");
        org.OwnerUserId.Should().Be(original.Owner);
        (await db.OrganizationMembers.Where(m => m.OrganizationId == report.CopyId).OrderBy(m => m.Role).Select(m => new { m.UserId, m.Role }).ToListAsync())
            .Should().BeEquivalentTo(await db.OrganizationMembers.Where(m => m.OrganizationId == original.Org).OrderBy(m => m.Role).Select(m => new { m.UserId, m.Role }).ToListAsync());
        var lead = await db.Leads.SingleAsync(l => l.OrganizationId == report.CopyId);
        lead.AssignedToUserId.Should().Be(original.Rep, "people are shared, not copied");
        (await db.Users.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task LinksBetweenRecordsLeadToTheCopies_InColumnsAndInsideText()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var lead = await db.Leads.SingleAsync(l => l.OrganizationId == report.CopyId);
        var deal = await db.Deals.SingleAsync(d => d.OrganizationId == report.CopyId);
        var contact = await db.Contacts.SingleAsync(c => c.OrganizationId == report.CopyId && c.Name == "Jean Dupont");
        new[] { lead.Id, deal.Id, contact.Id }.Should().NotIntersectWith(new[] { original.Lead, original.Deal, original.Contact });
        lead.ConvertedToDealId.Should().Be(deal.Id, "the converted lead's loop is rebuilt between the copies");
        lead.ConvertedToContactId.Should().Be(contact.Id);
        deal.ContactId.Should().Be(contact.Id);
        contact.ConvertedFromLeadId.Should().Be(lead.Id);
        var couple = await db.ContactRelationships.SingleAsync(r => r.OrganizationId == report.CopyId);
        couple.ContactId.Should().Be(contact.Id, "a relationship links the copied people, not the originals");
        (await db.Contacts.SingleAsync(c => c.Id == couple.RelatedContactId)).OrganizationId.Should().Be(report.CopyId);
        deal.IsPrivate.Should().BeTrue();
        var stage = await db.DealStages.SingleAsync(s => s.Id == deal.DealStageId);
        (await db.Pipelines.SingleAsync(p => p.Id == stage.PipelineId)).OrganizationId.Should().Be(report.CopyId);
        (await db.DealLineItems.SingleAsync(i => i.DealId == deal.Id)).UnitPrice.Should().Be(12000);
        (await db.Attachments.SingleAsync(a => a.OrganizationId == report.CopyId)).RecordId.Should().Be(deal.Id);
        (await db.Attachments.SingleAsync(a => a.OrganizationId == report.CopyId)).Content.Should().Equal("%PDF"u8.ToArray());

        var member = await db.ClubMembers.SingleAsync(m => m.OrganizationId == report.CopyId);
        (await db.ClubDocuments.SingleAsync(d => d.OrganizationId == report.CopyId)).Payload
            .Should().Contain(member.Id.ToString().ToUpperInvariant(), "an id kept in JSON is the copy's, in the same case");
        var sequence = await db.OutreachSequences.SingleAsync(s => s.OrganizationId == report.CopyId);
        var rule = await db.AutomationRules.SingleAsync(r => r.OrganizationId == report.CopyId);
        rule.ActionsJson.Should().Contain(sequence.Id.ToString()).And.Contain(original.Rep.ToString(), "the person stays the same person");
        (await db.AutomationFires.SingleAsync(f => f.RuleId == rule.Id)).SourceKey.Should().Be(lead.Id.ToString(), "what already fired for the lead counts as fired for its copy");
        var task = await db.TaskItems.SingleAsync(t => t.OrganizationId == report.CopyId);
        (await db.Notifications.Where(n => n.OrganizationId == report.CopyId).Select(n => n.Link).ToListAsync()).Should().Contain($"/tasks/{task.Id}");
    }

    [Fact]
    public async Task NothingThatExistsEmailsAnyoneTwice_AndTheWaysInStayWithTheOriginal()
    {
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var originalOrg = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == original.Org);

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        var org = await db.Organizations.SingleAsync(o => o.Id == report.CopyId);
        new[] { org.WebhookApiKey, org.WebhookPassword, org.OutboundWebhookUrl, org.OutboundWebhookSecret }.Should().OnlyContain(v => v == null);
        (await db.OutreachEnrollments.Where(e => e.OrganizationId == report.CopyId).Select(e => e.Status).ToListAsync())
            .Should().BeEquivalentTo(["paused", "stopped"]);
        var contract = await db.Set<Contract>().SingleAsync(c => c.OrganizationId == report.CopyId);
        contract.SigningTokenHash.Should().BeNull("the signer's link opens the original");
        contract.ReminderSentAtUtc.Should().NotBeNull("the signer is not reminded a second time");
        contract.Body.Should().NotContain(original.Deal.ToString());
        (await db.TaskItems.SingleAsync(t => t.OrganizationId == report.CopyId)).ReminderSentAtUtc.Should().NotBeNull();
        (await db.LeadCaptureForms.Where(f => f.OrganizationId != null).Select(f => f.PublicToken).ToListAsync()).Should().OnlyHaveUniqueItems();
        (await db.Notifications.Where(n => n.OrganizationId == report.CopyId).Select(n => n.SourceKey).ToListAsync()).Should().Contain((string?)null);

        // The original is exactly as it was.
        var after = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == original.Org);
        after.Should().BeEquivalentTo(originalOrg);
        (await db.OutreachEnrollments.CountAsync(e => e.OrganizationId == original.Org && e.Status == "active")).Should().Be(1);
        (await db.Set<Contract>().SingleAsync(c => c.OrganizationId == original.Org)).SigningTokenHash.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePlanCountsWhatTheCopyHolds_WithoutWritingAnything()
    {
        // The dry run used to make the whole copy and roll it back, in the database every
        // tenant shares. It now only counts, and has to count what the copy really makes.
        var (connection, db, original, _, _) = await WorldAsync();
        using var _c = connection;
        await using var _d = db;
        var organizations = await db.Organizations.CountAsync();
        var before = await CountsAsync(db, original.Org);
        var cloner = new OrganizationCloner(db);

        var plan = await cloner.PlanAsync(original.Org);

        (await db.Organizations.CountAsync()).Should().Be(organizations, "planning makes no copy");
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
        var before = await db.AuditEvents.CountAsync(e => e.OrganizationId == original.Org);

        var report = await new OrganizationCloner(db).CloneAsync(original.Org, "Pavillon 46 v2");

        (await db.AuditEvents.CountAsync(e => e.OrganizationId == report.CopyId)).Should().Be(before);
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

        var task = await db.TaskItems.SingleAsync(t => t.OrganizationId == report.CopyId);
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

        var deal = await db.Deals.SingleAsync(d => d.OrganizationId == report.CopyId);
        db.DealLineItems.RemoveRange(db.DealLineItems.Where(i => i.DealId == deal.Id));
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
        var (owner, ownerId) = await Workspaces.SignInAsync(factory, "kia@pavillon46.ch");
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
