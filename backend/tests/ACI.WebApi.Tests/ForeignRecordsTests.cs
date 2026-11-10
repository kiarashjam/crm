using System.Net;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Tests;

/// <summary>
/// Another organization's records show on no page and open from no link: every list,
/// search, board, calendar, report and export a member reads, and every record's own
/// address with the other organization's ids in it.
/// </summary>
/// <remarks>
/// Each record of the other organization carries one word, so finding that word anywhere
/// in an answer is a leak, whichever part of the page it reached. The same pages show the
/// word to the other organization's own owner, so the check is not passing on empty pages.
/// </remarks>
public class ForeignRecordsTests
{
    private const string Mark = "Zebrafish";

    private sealed record Foreign(
        Guid Lead, Guid Contact, Guid Company, Guid Deal, Guid Task, Guid Activity, Guid Member, Guid Pipeline,
        Guid Status, Guid Sequence, Guid Contract, Guid Event);

    /// <summary>One of every kind of record, in <paramref name="org"/>, each named with the mark.</summary>
    private static async Task<Foreign> SeedAsync(CustomWebApplicationFactory factory, Guid org, Guid owner)
    {
        var now = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        var company = new Company { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Holdings", CreatedAtUtc = now };
        var contact = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Contact", Email = "contact@zebrafish.example", CompanyId = company.Id, CreatedAtUtc = now };
        var spouse = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Spouse", Email = "spouse@zebrafish.example", CreatedAtUtc = now };
        var lead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Lead", Email = "lead@zebrafish.example", Status = "New", CompanyId = company.Id, ConvertedToContactId = contact.Id, CreatedAtUtc = now };
        var pipeline = new Pipeline { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Pipeline" };
        var stage = new DealStage { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = $"{Mark} Stage", DisplayOrder = 0 };
        var deal = new Deal
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Deal", Value = "5000", Currency = "CHF",
            CompanyId = company.Id, ContactId = contact.Id, PipelineId = pipeline.Id, DealStageId = stage.Id, ExpectedCloseDateUtc = now.AddDays(10), CreatedAtUtc = now,
        };
        var task = new TaskItem { Id = Guid.NewGuid(), UserId = owner, AssigneeId = owner, OrganizationId = org, Title = $"{Mark} Task", Status = TaskStatusEnum.Todo, DueDateUtc = now.AddHours(2), LeadId = lead.Id, DealId = deal.Id, CreatedAtUtc = now };
        var activity = new Activity { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "note", Subject = $"{Mark} Activity", Body = Mark, LeadId = lead.Id, DealId = deal.Id, ContactId = contact.Id, CreatedAtUtc = now };
        var member = new ClubMember { Id = Guid.NewGuid(), OwnerUserId = owner, OrganizationId = org, FirstName = Mark, LastName = "Member", Email = "member@zebrafish.example", Tier = "Gold", Status = "active", JoinedAtUtc = now, RenewsAtUtc = now.AddYears(1) };
        var status = new LeadStatus { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Status", DisplayOrder = 99 };
        var sequence = new OutreachSequence { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = $"{Mark} Sequence", Status = "active", CreatedAtUtc = now };
        var contract = new Contract { Id = Guid.NewGuid(), OrganizationId = org, LeadId = lead.Id, Title = $"{Mark} Contract", Body = Mark, CounterpartyName = $"{Mark} Signer", CounterpartyEmail = "signer@zebrafish.example", CreatedByUserId = owner, CreatedAtUtc = now, UpdatedAtUtc = now };
        var eventDoc = new ClubDocument { Id = Guid.NewGuid(), OrganizationId = org, Kind = "events", Payload = $"{{\"name\":\"{Mark} Gala\",\"startsAtUtc\":\"{now.AddDays(3):o}\"}}", UpdatedAtUtc = now };
        db.AddRange(company, contact, spouse, lead, pipeline, stage, deal, task, activity, member, status, sequence, contract, eventDoc);
        db.AddRange(
            new ContactRelationship { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, ContactId = contact.Id, RelatedContactId = spouse.Id, Type = ContactRelationshipTypes.Spouse, CreatedAtUtc = now },
            new DealLineItem { Id = Guid.NewGuid(), DealId = deal.Id, Name = $"{Mark} Line", Quantity = 1, UnitPrice = 5000, CreatedAtUtc = now },
            new DealStageChange { Id = Guid.NewGuid(), DealId = deal.Id, ToStageName = $"{Mark} Stage", ChangedByUserId = owner, ChangedAtUtc = now },
            new TaskComment { Id = Guid.NewGuid(), TaskItemId = task.Id, AuthorId = owner, Body = $"{Mark} comment", CreatedAtUtc = now },
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = lead.Id, OrganizationId = org, ToStatus = $"{Mark} Status", ChangedByUserId = owner, ChangedAtUtc = now },
            new Product { Id = Guid.NewGuid(), OwnerUserId = owner, OrganizationId = org, Name = $"{Mark} Product", UnitPrice = 10, CreatedAtUtc = now },
            new AutomationRule { Id = Guid.NewGuid(), OwnerUserId = owner, OrganizationId = org, Name = $"{Mark} Rule", Trigger = "lead.created", ActionsJson = "[]", CreatedAtUtc = now },
            new OutreachEnrollment { Id = Guid.NewGuid(), SequenceId = sequence.Id, OrganizationId = org, EnrolledByUserId = owner, TargetType = "lead", TargetId = lead.Id, TargetName = $"{Mark} Lead", Status = "paused", EnrolledAtUtc = now },
            new LeadSource { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Source", DisplayOrder = 99 },
            new SalesTeam { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Team", CreatedAtUtc = now },
            new Template { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, IsSharedWithOrganization = true, Title = $"{Mark} Template", Content = Mark, Category = "Sales" },
            new LeadCaptureForm { Id = Guid.NewGuid(), OrganizationId = org, CreatedByUserId = owner, Name = $"{Mark} Form", PublicToken = Guid.NewGuid().ToString("N"), CreatedAtUtc = now },
            new LeadTerritory { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Territory", Countries = "CH" },
            new LeadQueue { Id = Guid.NewGuid(), OrganizationId = org, Name = $"{Mark} Queue" },
            new CustomFieldDefinition { Id = Guid.NewGuid(), OwnerUserId = owner, OrganizationId = org, EntityType = "lead", Key = "zebrafish", Label = $"{Mark} Field", Type = "text", CreatedAtUtc = now },
            new ImportBatch { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, EntityType = "lead", FileName = $"{Mark}.csv", StartedAtUtc = now, FinishedAtUtc = now },
            new CopyHistoryItem { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "email", Copy = Mark, RecipientName = $"{Mark} Lead", RecipientId = lead.Id.ToString(), CreatedAtUtc = now },
            new Attachment { Id = Guid.NewGuid(), OwnerUserId = owner, OrganizationId = org, EntityType = "lead", RecordId = lead.Id, FileName = $"{Mark}.pdf", ContentType = "application/pdf", Size = 4, Content = "%PDF"u8.ToArray(), UploadedByName = Mark, UploadedAtUtc = now },
            new ClubDocument { Id = Guid.NewGuid(), OrganizationId = org, Kind = "charges", Payload = $"{{\"memberId\":\"{member.Id}\",\"description\":\"{Mark} dinner\",\"amount\":120,\"status\":\"open\"}}", UpdatedAtUtc = now },
            new ClubDocument { Id = Guid.NewGuid(), OrganizationId = org, Kind = "reservations", Payload = $"{{\"memberName\":\"{Mark} Member\",\"startsAtUtc\":\"{now.AddDays(1):o}\"}}", UpdatedAtUtc = now },
            new ClubVisit { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = $"{Mark} Member", Venue = "Lounge", VisitedAtUtc = now },
            new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = $"{Mark} Member", Points = 10, Reason = Mark, CreatedAtUtc = now });
        await db.SaveChangesAsync();
        return new Foreign(lead.Id, contact.Id, company.Id, deal.Id, task.Id, activity.Id, member.Id, pipeline.Id, status.Id, sequence.Id, contract.Id, eventDoc.Id);
    }

    /// <summary>Every page a member reads that lists, searches, totals or exports a workspace's records.</summary>
    private static IEnumerable<string> Pages()
    {
        var from = DateTime.UtcNow.AddDays(-7).ToString("o");
        var to = DateTime.UtcNow.AddDays(30).ToString("o");
        return new[]
        {
            "/api/leads", "/api/leads/all", $"/api/leads/search?q={Mark}", "/api/leads/stats",
            "/api/contacts", "/api/contacts/all", $"/api/contacts/search?q={Mark}", "/api/contacts/stats", "/api/contacts/export",
            "/api/companies", "/api/companies/all", $"/api/companies/search?q={Mark}", "/api/companies/stats", "/api/companies/export", "/api/companies/summary",
            "/api/deals", "/api/deals/all", $"/api/deals/search?q={Mark}", "/api/deals/board",
            "/api/tasks", "/api/tasks/all", "/api/tasks/stats",
            "/api/activities", "/api/activities/all", "/api/activities/org-member-counts",
            $"/api/search?q={Mark}",
            $"/api/calendar?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&scope=team",
            "/api/work-queue?scope=team",
            "/api/notifications", "/api/copyhistory", "/api/imports", "/api/audit",
            "/api/sequences", "/api/sequences/enrollments",
            "/api/lead-ops/forms", "/api/lead-ops/territories", "/api/lead-ops/queues", "/api/lead-ops/scoring",
            "/api/lead-ops/assignment", "/api/lead-ops/conversion",
            "/api/custom-fields", "/api/products", "/api/automations", "/api/teams",
            "/api/pipelines", "/api/dealstages", "/api/leadstatuses", "/api/leadsources",
            "/api/templates", "/api/templates/all",
            "/api/members", "/api/visits", "/api/loyalty/ledger", "/api/charges", "/api/payments", "/api/gift-cards",
            "/api/tabs", "/api/campaigns", "/api/events", "/api/donations", "/api/communications", "/api/applications",
            "/api/reservations", "/api/households", "/api/subscriptions",
            "/api/reporting/dashboard", "/api/reporting/pipeline-by-stage", "/api/reporting/pipeline-by-assignee",
            "/api/reporting/summary", "/api/reporting/forecast", "/api/reporting/forecast-snapshots",
            "/api/duplicates/contacts", "/api/duplicates/companies",
        };
    }

    /// <summary>Each record's own addresses, and its related lists, with the other organization's ids in them.</summary>
    private static IEnumerable<string> Links(Foreign f) => new[]
    {
        $"/api/leads/{f.Lead}", $"/api/contacts/{f.Contact}", $"/api/companies/{f.Company}", $"/api/deals/{f.Deal}", $"/api/tasks/{f.Task}",
        $"/api/activities/lead/{f.Lead}", $"/api/activities/deal/{f.Deal}", $"/api/activities/contact/{f.Contact}",
        $"/api/tasks/by-lead/{f.Lead}", $"/api/tasks/by-deal/{f.Deal}", $"/api/tasks/by-contact/{f.Contact}", $"/api/tasks/{f.Task}/comments",
        $"/api/deals/{f.Deal}/line-items", $"/api/deals/{f.Deal}/stage-history",
        $"/api/contacts/{f.Contact}/relationships", $"/api/leads/{f.Lead}/relationships",
        $"/api/contacts/{f.Contact}/lookalikes", $"/api/contacts/{f.Contact}/referred-leads",
        $"/api/contracts/{f.Contract}", $"/api/contracts/for-lead/{f.Lead}", $"/api/contracts/{f.Contract}/pdf",
        $"/api/sequences/{f.Sequence}", $"/api/pipelines/{f.Pipeline}", $"/api/leadstatuses/{f.Status}",
        $"/api/lead-ops/history/{f.Lead}", $"/api/attachments?entityType=lead&recordId={f.Lead}",
        $"/api/custom-fields/values?entityType=lead&recordId={f.Lead}",
        $"/api/events/{f.Event}/attendees",
    };

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        return (res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Another_organizations_records_show_on_no_page_and_open_from_no_link()
    {
        using var factory = new CustomWebApplicationFactory();
        var (alpine, alpineId) = await SignInAsync(factory, "owner@alpine.example");
        var orgA = await CreateAsync(factory, alpineId, "Alpine");
        var (bernina, berninaId) = await SignInAsync(factory, "owner@bernina.example");
        var orgB = await CreateAsync(factory, berninaId, "Bernina");
        var foreign = await SeedAsync(factory, orgB, berninaId);
        alpine.In(orgA);
        bernina.In(orgB);

        // Bernina's own owner sees the mark on these pages: they do read the records.
        var seenByOwner = new List<string>();
        foreach (var url in Pages().Concat(Links(foreign)))
            if ((await GetAsync(bernina, url)).Body.Contains(Mark)) seenByOwner.Add(url);
        seenByOwner.Should().Contain(new[]
        {
            "/api/leads/all", "/api/contacts/all", "/api/companies/all", "/api/deals/all", "/api/tasks/all", "/api/activities/all",
            $"/api/search?q={Mark}", "/api/members", "/api/products", "/api/sequences", "/api/automations", "/api/pipelines",
            "/api/leadstatuses", "/api/templates/all", "/api/lead-ops/forms", "/api/custom-fields", "/api/imports", "/api/teams",
            $"/api/leads/{foreign.Lead}", $"/api/deals/{foreign.Deal}", $"/api/tasks/{foreign.Task}", $"/api/contracts/{foreign.Contract}",
        }, "the pages checked below do show these records to their own workspace");

        foreach (var url in Pages().Concat(Links(foreign)))
        {
            var (status, body) = await GetAsync(alpine, url);
            body.Should().NotContain(Mark, $"{url} ({(int)status}) must show nothing of another organization");
            ((int)status).Should().BeLessThan(500, $"{url} answers another organization's ids without failing");
        }
    }

    [Fact]
    public async Task Another_organizations_records_cannot_be_changed_or_deleted_by_id()
    {
        using var factory = new CustomWebApplicationFactory();
        var (alpine, alpineId) = await SignInAsync(factory, "owner@alpine.example");
        alpine.In(await CreateAsync(factory, alpineId, "Alpine"));
        var (_, berninaId) = await SignInAsync(factory, "owner@bernina.example");
        var orgB = await CreateAsync(factory, berninaId, "Bernina");
        var f = await SeedAsync(factory, orgB, berninaId);

        var attempts = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Put, $"/api/leads/{f.Lead}", new { name = "Taken" }),
            (HttpMethod.Put, $"/api/contacts/{f.Contact}", new { name = "Taken" }),
            (HttpMethod.Put, $"/api/companies/{f.Company}", new { name = "Taken" }),
            (HttpMethod.Put, $"/api/deals/{f.Deal}", new { name = "Taken" }),
            (HttpMethod.Put, $"/api/tasks/{f.Task}", new { title = "Taken" }),
            (HttpMethod.Put, $"/api/members/{f.Member}", new { firstName = "Taken" }),
            (HttpMethod.Delete, $"/api/activities/{f.Activity}", null),
            (HttpMethod.Delete, $"/api/tasks/{f.Task}", null),
            (HttpMethod.Delete, $"/api/deals/{f.Deal}", null),
            (HttpMethod.Delete, $"/api/leads/{f.Lead}", null),
            (HttpMethod.Delete, $"/api/contacts/{f.Contact}", null),
            (HttpMethod.Delete, $"/api/companies/{f.Company}", null),
            (HttpMethod.Delete, $"/api/members/{f.Member}", null),
            (HttpMethod.Delete, $"/api/events/{f.Event}", null),
        };
        foreach (var (method, url, body) in attempts)
        {
            var request = new HttpRequestMessage(method, url) { Content = body == null ? null : Body(body) };
            var res = await alpine.SendAsync(request);
            ((int)res.StatusCode).Should().BeInRange(400, 499, $"{method} {url} touches another organization's record");
        }

        var names = await factory.Services.Read(db => new[]
        {
            db.Leads.Single(l => l.Id == f.Lead).Name, db.Contacts.Single(c => c.Id == f.Contact).Name,
            db.Companies.Single(c => c.Id == f.Company).Name, db.Deals.Single(d => d.Id == f.Deal).Name,
            db.TaskItems.Single(t => t.Id == f.Task).Title, db.ClubMembers.Single(m => m.Id == f.Member).FirstName,
            db.Activities.Single(a => a.Id == f.Activity).Subject!, db.ClubDocuments.Single(d => d.Id == f.Event).Payload,
        });
        names.Should().OnlyContain(n => n.Contains(Mark), "every one of them is as its own workspace left it");
    }

    [Fact]
    public async Task A_personal_workspace_sees_no_organizations_records_and_no_other_persons()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, berninaId) = await SignInAsync(factory, "owner@bernina.example");
        var orgB = await CreateAsync(factory, berninaId, "Bernina");
        await SeedAsync(factory, orgB, berninaId);
        // Bernina's owner also keeps a personal lead, outside any organization.
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = berninaId, Name = $"{Mark} Personal", Email = "own@zebrafish.example", Status = "New", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var (solo, _) = await SignInAsync(factory, "solo@example.ch");
        foreach (var url in Pages())
            (await GetAsync(solo, url)).Body.Should().NotContain(Mark, $"{url} in a personal workspace is its owner's alone");
    }
}
