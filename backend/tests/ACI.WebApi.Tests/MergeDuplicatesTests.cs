using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Folding a duplicate into the record kept: everything on the duplicate ends up on
/// the record kept, nothing is lost on the way, and only people who could change both
/// records can do it.
/// </summary>
/// <remarks>
/// The data tests run on SQLite, so the foreign keys, unique indexes and transaction
/// are real; the HTTP tests cover routing and who may merge.
/// </remarks>
public class MergeDuplicatesTests
{
    private sealed record World(AppDbContext Db, User User, Organization Org);

    private static async Task<World> OpenAsync(SqliteConnection connection)
    {
        var db = SqliteDb.Open(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Owner", Email = "owner@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Pavillon", OwnerUserId = user.Id, CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(user, org);
        await db.SaveChangesAsync();
        return new World(db, user, org);
    }

    private static Lead NewLead(World w, string name, string email, DateTime? created = null) => new()
    {
        Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = name, Email = email,
        Status = "New", CreatedAtUtc = created ?? DateTime.UtcNow,
    };

    private static Deal NewDeal(World w, string name, string value = "1000") => new()
    {
        Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = name, Value = value, CreatedAtUtc = DateTime.UtcNow,
    };

    private static Attachment FileOn(World w, string type, Guid recordId, string name) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = w.Org.Id, OwnerUserId = w.User.Id, EntityType = type, RecordId = recordId,
        FileName = name, Size = 3, Content = new byte[] { 1, 2, 3 }, UploadedByName = "Owner", UploadedAtUtc = DateTime.UtcNow,
    };

    private static CustomFieldValueSet FieldsOn(World w, string type, Guid recordId, string json) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = w.Org.Id, OwnerUserId = w.User.Id, EntityType = type, RecordId = recordId,
        ValuesJson = json, UpdatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task MergingLeads_MovesEverythingOntoTheLeadKept_AndFillsWhatItWasMissing()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var earlier = DateTime.UtcNow.AddDays(-30);
        var keep = NewLead(w, "Ada Lovelace", "ada@example.ch");
        keep.Status = "Qualified";
        keep.EmailOpenCount = 2;
        var drop = NewLead(w, "Ada Lovelace", "ada@example.ch", earlier);
        drop.Phone = "+41 79 123 45 67";
        drop.JobTitle = "Countess";
        drop.Status = "New";
        drop.EmailOpenCount = 3;
        var madeFromDrop = new Contact { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Ada", Email = "ada.c@example.ch", CreatedAtUtc = DateTime.UtcNow, ConvertedFromLeadId = drop.Id };
        db.AddRange(keep, drop, madeFromDrop);
        db.Add(new TaskItem { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Title = "Call back", LeadId = drop.Id, CreatedAtUtc = DateTime.UtcNow });
        db.Add(new Activity { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Type = "note", Subject = "Met at the fair", LeadId = drop.Id, CreatedAtUtc = DateTime.UtcNow });
        db.Add(new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = drop.Id, OrganizationId = w.Org.Id, ToStatus = "New", ChangedAtUtc = earlier });
        db.Add(new SentMessage { Id = Guid.NewGuid(), OrganizationId = w.Org.Id, SentByUserId = w.User.Id, To = "ada@example.ch", Subject = "Hello", Body = "Hi", LeadId = drop.Id, SentAtUtc = DateTime.UtcNow });
        db.Add(FileOn(w, RecordTypes.Lead, drop.Id, "brief.pdf"));
        db.Add(FieldsOn(w, RecordTypes.Lead, keep.Id, """{"budget":"","tier":"Gold"}"""));
        db.Add(FieldsOn(w, RecordTypes.Lead, drop.Id, """{"budget":"5000","tier":"Silver","seats":12}"""));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new LeadRepository(db).MergeIntoAsync(keep.Id, drop.Id, w.User.Id, w.Org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        (await db.Leads.AnyAsync(l => l.Id == drop.Id)).Should().BeFalse();
        var kept = await db.Leads.SingleAsync(l => l.Id == keep.Id);
        kept.Status.Should().Be("Qualified", "the kept lead's own values stay");
        kept.Phone.Should().Be("+41 79 123 45 67");
        kept.JobTitle.Should().Be("Countess");
        kept.EmailOpenCount.Should().Be(5);
        kept.CreatedAtUtc.Should().BeCloseTo(earlier, TimeSpan.FromSeconds(1), "the person first came in with the older record");

        (await db.TaskItems.SingleAsync()).LeadId.Should().Be(keep.Id);
        (await db.Activities.SingleAsync()).LeadId.Should().Be(keep.Id);
        (await db.LeadStatusEvents.SingleAsync()).LeadId.Should().Be(keep.Id);
        (await db.SentMessages.SingleAsync()).LeadId.Should().Be(keep.Id);
        (await db.Contacts.SingleAsync(c => c.Id == madeFromDrop.Id)).ConvertedFromLeadId.Should().Be(keep.Id);
        var file = await db.Attachments.SingleAsync();
        file.RecordId.Should().Be(keep.Id, "the duplicate's files are not deleted with it");
        file.Content.Should().Equal(1, 2, 3);
        var fields = JsonDocument.Parse((await db.CustomFieldValues.SingleAsync()).ValuesJson).RootElement;
        fields.GetProperty("tier").GetString().Should().Be("Gold");
        fields.GetProperty("budget").GetString().Should().Be("5000");
        fields.GetProperty("seats").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task MergingLeads_ThatBothBecameContacts_KeepsBothContacts()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var keep = NewLead(w, "Grace", "grace@example.ch");
        var drop = NewLead(w, "Grace", "grace@example.ch");
        var fromKeep = new Contact { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Grace", Email = "g1@example.ch", CreatedAtUtc = DateTime.UtcNow, ConvertedFromLeadId = keep.Id };
        var fromDrop = new Contact { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Grace", Email = "g2@example.ch", CreatedAtUtc = DateTime.UtcNow, ConvertedFromLeadId = drop.Id };
        db.AddRange(keep, drop, fromKeep, fromDrop);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new LeadRepository(db).MergeIntoAsync(keep.Id, drop.Id, w.User.Id, w.Org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        (await db.Contacts.SingleAsync(c => c.Id == fromKeep.Id)).ConvertedFromLeadId.Should().Be(keep.Id);
        (await db.Contacts.SingleAsync(c => c.Id == fromDrop.Id)).ConvertedFromLeadId.Should().BeNull(
            "a lead becomes at most one contact; the other contact stays, unlinked");
    }

    [Fact]
    public async Task MergingDeals_MovesHistoryLineItemsAndFiles_AndAPrivateDuplicateKeepsItPrivate()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var keep = NewDeal(w, "Pavillon renewal", "0");
        var drop = NewDeal(w, "Pavillon renewal", "12000");
        drop.Currency = "CHF";
        drop.IsPrivate = true;
        drop.Description = "Signed off by the board";
        var lead = NewLead(w, "Lin", "lin@example.ch");
        lead.ConvertedToDealId = drop.Id;
        db.AddRange(keep, drop, lead);
        db.Add(new DealStageChange { Id = Guid.NewGuid(), DealId = drop.Id, ToStageName = "Proposal", ChangedByUserId = w.User.Id, ChangedAtUtc = DateTime.UtcNow });
        db.Add(new DealLineItem { Id = Guid.NewGuid(), DealId = drop.Id, Name = "Seats", Quantity = 10, UnitPrice = 1200, CreatedAtUtc = DateTime.UtcNow });
        db.Add(new TaskItem { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Title = "Send contract", DealId = drop.Id, CreatedAtUtc = DateTime.UtcNow });
        db.Add(FileOn(w, RecordTypes.Deal, drop.Id, "quote.pdf"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new DealRepository(db).MergeIntoAsync(keep.Id, drop.Id, w.User.Id, w.Org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        (await db.Deals.AnyAsync(d => d.Id == drop.Id)).Should().BeFalse();
        var kept = await db.Deals.SingleAsync(d => d.Id == keep.Id);
        kept.Value.Should().Be("12000", "an empty value is filled from the duplicate");
        kept.Currency.Should().Be("CHF");
        kept.Description.Should().Be("Signed off by the board");
        kept.IsPrivate.Should().BeTrue();
        (await db.DealStageChanges.SingleAsync()).DealId.Should().Be(keep.Id, "stage history cascades on delete, so it has to move first");
        (await db.DealLineItems.SingleAsync()).DealId.Should().Be(keep.Id);
        (await db.TaskItems.SingleAsync()).DealId.Should().Be(keep.Id);
        (await db.Attachments.SingleAsync()).RecordId.Should().Be(keep.Id);
        (await db.Leads.SingleAsync()).ConvertedToDealId.Should().Be(keep.Id);
    }

    [Fact]
    public async Task MergingCompanies_KeepsTheDuplicatesFiles()
    {
        // Merging deleted the other company, and its files and custom fields went with it.
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var keep = new Company { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Acme", CreatedAtUtc = DateTime.UtcNow };
        var drop = new Company { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Acme", Domain = "acme.ch", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(keep, drop);
        db.Add(FileOn(w, RecordTypes.Company, drop.Id, "logo.png"));
        db.Add(FieldsOn(w, RecordTypes.Company, drop.Id, """{"region":"Romandie"}"""));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new CompanyRepository(db).MergeIntoAsync(keep.Id, drop.Id, w.User.Id, w.Org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        (await db.Companies.SingleAsync()).Domain.Should().Be("acme.ch");
        (await db.Attachments.SingleAsync()).RecordId.Should().Be(keep.Id);
        (await db.CustomFieldValues.SingleAsync()).RecordId.Should().Be(keep.Id);
    }

    [Fact]
    public async Task MergingContacts_MovesFilesOffTheArchivedDuplicate()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var keep = new Contact { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Sam Lee", Email = "sam@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var drop = new Contact { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = "Sam Lee", Email = "sam.lee@example.ch", Description = "Prefers mornings", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(keep, drop);
        db.Add(FileOn(w, RecordTypes.Contact, drop.Id, "card.jpg"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await new ContactRepository(db).MergeIntoAsync(keep.Id, drop.Id, w.User.Id, w.Org.Id)).Should().BeTrue();
        db.ChangeTracker.Clear();

        (await db.Contacts.SingleAsync(c => c.Id == drop.Id)).IsArchived.Should().BeTrue();
        (await db.Contacts.SingleAsync(c => c.Id == keep.Id)).Description.Should().Be("Prefers mornings");
        (await db.Attachments.SingleAsync()).RecordId.Should().Be(keep.Id);
    }

    [Fact]
    public async Task ARecordIsNotMergedWithItself_OrWithOneFromAnotherWorkspace()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var other = new Organization { Id = Guid.NewGuid(), Name = "Elsewhere", OwnerUserId = w.User.Id, CreatedAtUtc = DateTime.UtcNow };
        var mine = NewLead(w, "Ada", "ada@example.ch");
        var theirs = NewLead(w, "Ada", "ada@example.ch");
        theirs.OrganizationId = other.Id;
        db.AddRange(other, mine, theirs);
        db.Add(new TaskItem { Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = other.Id, Title = "Theirs", LeadId = theirs.Id, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var leads = new LeadRepository(db);
        (await leads.MergeIntoAsync(mine.Id, mine.Id, w.User.Id, w.Org.Id)).Should().BeFalse();
        (await leads.MergeIntoAsync(mine.Id, theirs.Id, w.User.Id, w.Org.Id)).Should().BeFalse();
        db.ChangeTracker.Clear();

        (await db.Leads.CountAsync()).Should().Be(2);
        (await db.TaskItems.SingleAsync()).LeadId.Should().Be(theirs.Id);
    }

    [Fact]
    public void CustomFieldValues_TheKeptOnesWin_AndEmptyOnesAreFilled()
    {
        var folded = JsonDocument.Parse(RecordMerge.FoldValues(
            """{"a":"kept","b":"","c":null,"d":[]}""",
            """{"a":"other","b":"filled","c":3,"d":["x"],"e":true,"f":""}""")).RootElement;
        folded.GetProperty("a").GetString().Should().Be("kept");
        folded.GetProperty("b").GetString().Should().Be("filled");
        folded.GetProperty("c").GetInt32().Should().Be(3);
        folded.GetProperty("d")[0].GetString().Should().Be("x");
        folded.GetProperty("e").GetBoolean().Should().BeTrue();
        folded.TryGetProperty("f", out _).Should().BeFalse("an empty value on the other record adds nothing");
        RecordMerge.FoldValues("not json", """{"a":1}""").Should().Be("""{"a":1}""");
    }

    // ── Over HTTP ────────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedAsync<T>(CustomWebApplicationFactory factory, T entity) where T : Domain.Common.BaseEntity
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    private static Lead LeadIn(Guid org, Guid owner, string name) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = name, Email = $"{Guid.NewGuid():N}@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow,
    };

    private static Deal DealIn(Guid org, Guid owner, string name, bool isPrivate = false) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = name, Value = "500", IsPrivate = isPrivate, CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task AMemberMergesTwoLeads_AndTheDuplicateIsGone()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var keep = await SeedAsync(factory, LeadIn(org, ownerId, "Ada"));
        var drop = await SeedAsync(factory, LeadIn(org, ownerId, "Ada"));
        await SeedAsync(factory, new TaskItem { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Follow up", LeadId = drop, CreatedAtUtc = DateTime.UtcNow });

        var res = await rep.In(org).PostAsync($"/api/leads/{keep}/merge", Body(new { otherLeadId = drop }));
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());

        (await owner.In(org).GetAsync($"/api/leads/{drop}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.Services.Read(db => db.TaskItems.Single().LeadId)).Should().Be(keep);
    }

    [Fact]
    public async Task AViewerCannotMerge_AndNobodyCanMergeAcrossWorkspaces()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var otherOrg = await CreateAsync(factory, ownerId, "Elsewhere");
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        var keep = await SeedAsync(factory, LeadIn(org, ownerId, "Ada"));
        var drop = await SeedAsync(factory, LeadIn(org, ownerId, "Ada"));
        var elsewhere = await SeedAsync(factory, LeadIn(otherOrg, ownerId, "Ada"));

        (await viewer.In(org).PostAsync($"/api/leads/{keep}/merge", Body(new { otherLeadId = drop })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.In(org).PostAsync($"/api/leads/{keep}/merge", Body(new { otherLeadId = elsewhere })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.In(org).PostAsync($"/api/leads/{keep}/merge", Body(new { otherLeadId = keep })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(3);
    }

    [Fact]
    public async Task APrivateDuplicate_CannotHideATeammatesDeal_ButAManagerMayMergeIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var (manager, managerId) = await SignInAsync(factory, "manager@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);
        var shared = await SeedAsync(factory, DealIn(org, ownerId, "Renewal"));
        var repsPrivate = await SeedAsync(factory, DealIn(org, repId, "Renewal", isPrivate: true));

        var refused = await rep.In(org).PostAsync($"/api/deals/{shared}/merge", Body(new { otherDealId = repsPrivate }));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("private");

        // The other way round is theirs to do: the private deal stays private.
        (await rep.In(org).PostAsync($"/api/deals/{repsPrivate}/merge", Body(new { otherDealId = shared })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var another = await SeedAsync(factory, DealIn(org, ownerId, "Upsell"));
        var hidden = await SeedAsync(factory, DealIn(org, repId, "Upsell", isPrivate: true));
        (await manager.In(org).PostAsync($"/api/deals/{another}/merge", Body(new { otherDealId = hidden })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await factory.Services.Read(db => db.Deals.Single(d => d.Id == another).IsPrivate)).Should().BeTrue();
    }

    [Fact]
    public async Task ADealSomeoneCannotSee_CannotBeMergedAway()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var repsDeal = await SeedAsync(factory, DealIn(org, repId, "Renewal"));
        var ownersPrivate = await SeedAsync(factory, DealIn(org, ownerId, "Renewal", isPrivate: true));

        (await rep.In(org).PostAsync($"/api/deals/{repsDeal}/merge", Body(new { otherDealId = ownersPrivate })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.Services.Read(db => db.Deals.Count())).Should().Be(2);
    }
}
