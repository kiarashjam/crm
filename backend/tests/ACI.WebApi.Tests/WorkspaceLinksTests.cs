using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The ids a request links a record to, and the person it assigns, have to be in the
/// caller's workspace.
/// </summary>
/// <remarks>
/// They were copied from the request as they came, so a member could link a task to
/// another workspace's contact, or a deal to another workspace's company, and read the
/// name back on every response; or assign a task to anyone with an account, who was
/// then emailed its reminders.
/// </remarks>
public class WorkspaceLinksTests
{
    /// <summary>Workspace A, with an owner and a rep; and workspace B next door, with its own records.</summary>
    private sealed record Setup(
        CustomWebApplicationFactory Factory,
        HttpClient Owner, Guid OwnerId, HttpClient Rep, Guid RepId, Guid Org,
        Guid OtherUserId, Guid OtherOrg,
        Guid TheirLead, Guid TheirContact, Guid TheirCompany, Guid TheirDeal, Guid TheirPipeline, Guid TheirStage,
        Guid OurContact, Guid OurCompany, Guid PrivateDeal, Guid OurPipeline, Guid OurStage, Guid OurOtherPipelineStage);

    private static async Task<Setup> SetUpAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        var (_, otherUserId) = await SignInAsync(factory, "owner@rival.ch");
        var otherOrg = await CreateAsync(factory, otherUserId, "Rival Club");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        Lead Lead(Guid user, Guid workspace, string name) => new() { Id = Guid.NewGuid(), UserId = user, OrganizationId = workspace, Name = name, Email = $"{Guid.NewGuid():N}@example.ch", Status = "New", CreatedAtUtc = now };
        Contact Contact(Guid user, Guid workspace, string name) => new() { Id = Guid.NewGuid(), UserId = user, OrganizationId = workspace, Name = name, Email = $"{Guid.NewGuid():N}@example.ch", CreatedAtUtc = now };
        Company Company(Guid user, Guid workspace, string name) => new() { Id = Guid.NewGuid(), UserId = user, OrganizationId = workspace, Name = name, CreatedAtUtc = now };
        Pipeline Pipeline(Guid workspace) => new() { Id = Guid.NewGuid(), OrganizationId = workspace, Name = "Sales" };
        DealStage Stage(Pipeline pipeline) => new() { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = "Proposal" };

        var theirLead = Lead(otherUserId, otherOrg, "Their Lead Hélène");
        var theirContact = Contact(otherUserId, otherOrg, "Their Contact Bastien");
        var theirCompany = Company(otherUserId, otherOrg, "Their Company SA");
        var theirDeal = new Deal { Id = Guid.NewGuid(), UserId = otherUserId, OrganizationId = otherOrg, Name = "Their Deal", Value = "1", CreatedAtUtc = now };
        var theirPipeline = Pipeline(otherOrg);
        var theirStage = Stage(theirPipeline);
        var ourContact = Contact(ownerId, org, "Our Contact");
        var ourCompany = Company(ownerId, org, "Our Company");
        var privateDeal = new Deal { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Secret acquisition", Value = "50000", IsPrivate = true, CreatedAtUtc = now };
        var ourPipeline = Pipeline(org);
        var ourStage = Stage(ourPipeline);
        var ourOtherPipeline = Pipeline(org);
        var ourOtherStage = Stage(ourOtherPipeline);
        db.AddRange(theirLead, theirContact, theirCompany, theirDeal, theirPipeline, theirStage,
            ourContact, ourCompany, privateDeal, ourPipeline, ourStage, ourOtherPipeline, ourOtherStage);
        await db.SaveChangesAsync();

        return new Setup(factory, owner, ownerId, rep, repId, org, otherUserId, otherOrg,
            theirLead.Id, theirContact.Id, theirCompany.Id, theirDeal.Id, theirPipeline.Id, theirStage.Id,
            ourContact.Id, ourCompany.Id, privateDeal.Id, ourPipeline.Id, ourStage.Id, ourOtherStage.Id);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task Refused(HttpResponseMessage res, HttpStatusCode status, string code, string step, params string[] mustNotShow)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        body.Should().Contain(code);
        foreach (var secret in mustNotShow) body.Should().NotContain(secret);
    }

    private static Task<HttpResponseMessage> NewTask(HttpClient client, object body) => client.PostAsync("/api/tasks", Body(body));

    [Fact]
    public async Task ATaskCannotLinkToAnotherWorkspacesRecords()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);

        await Refused(await NewTask(s.Owner, new { title = "Call", contactId = s.TheirContact }),
            HttpStatusCode.BadRequest, "Link.ContactNotFound", "a foreign contact", "Bastien");
        await Refused(await NewTask(s.Owner, new { title = "Call", leadId = s.TheirLead }),
            HttpStatusCode.BadRequest, "Link.LeadNotFound", "a foreign lead", "Hélène");
        await Refused(await NewTask(s.Owner, new { title = "Call", dealId = s.TheirDeal }),
            HttpStatusCode.BadRequest, "Link.DealNotFound", "a foreign deal", "Their Deal");

        var task = await Ok(await NewTask(s.Owner, new { title = "Call", contactId = s.OurContact }), "our own contact");
        var id = task.GetProperty("id").GetGuid();
        await Refused(await s.Owner.PutAsync($"/api/tasks/{id}", Body(new { dealId = s.TheirDeal })),
            HttpStatusCode.BadRequest, "Link.DealNotFound", "an edit to a foreign deal", "Their Deal");
        await Refused(await s.Owner.PatchAsync($"/api/tasks/{id}/link-lead", Body(new { leadId = s.TheirLead })),
            HttpStatusCode.BadRequest, "Link.LeadNotFound", "linking a foreign lead", "Hélène");
        await Refused(await s.Owner.PatchAsync($"/api/tasks/{id}/link-deal", Body(new { dealId = s.TheirDeal })),
            HttpStatusCode.BadRequest, "Link.DealNotFound", "linking a foreign deal", "Their Deal");

        var saved = await factory.Services.Read(db => db.TaskItems.Single(t => t.Id == id));
        saved.DealId.Should().BeNull("a refused edit changes nothing");
        saved.LeadId.Should().BeNull();
    }

    [Fact]
    public async Task ARepCannotLinkATaskToAPrivateDealTheyCannotSee()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);

        await Refused(await NewTask(s.Rep, new { title = "Peek", dealId = s.PrivateDeal }),
            HttpStatusCode.BadRequest, "Link.DealNotFound", "a private deal", "Secret acquisition");
        await Ok(await NewTask(s.Owner, new { title = "Prepare offer", dealId = s.PrivateDeal }), "the deal's owner can");
    }

    [Fact]
    public async Task ATaskIsOnlyAssignedToAMemberOfTheWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);

        await Refused(await NewTask(s.Owner, new { title = "Call", assigneeId = s.OtherUserId, reminderDateUtc = DateTime.UtcNow }),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "an outsider", "owner@rival.ch");
        var task = await Ok(await NewTask(s.Owner, new { title = "Call", assigneeId = s.RepId }), "a teammate");
        var id = task.GetProperty("id").GetGuid();

        await Refused(await s.Owner.PatchAsync($"/api/tasks/{id}/assign", Body(new { assigneeId = s.OtherUserId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "assigning an outsider", "owner@rival.ch");
        await Refused(await s.Owner.PutAsync($"/api/tasks/{id}", Body(new { assigneeId = s.OtherUserId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "an edit to an outsider", "owner@rival.ch");
        await Refused(await s.Owner.PostAsync("/api/tasks/bulk", Body(new { taskIds = new[] { id }, action = "assignee", assigneeId = s.OtherUserId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "a bulk assign to an outsider");

        (await factory.Services.Read(db => db.TaskItems.Single(t => t.Id == id).AssigneeId)).Should().Be(s.RepId);
    }

    [Fact]
    public async Task ATaskWhoseAssigneeHasLeftCanStillBeEdited()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);
        var deal = (await Ok(await s.Owner.PostAsync("/api/deals", Body(new { name = "Renewal", value = "900" })), "a shared deal")).GetProperty("id").GetGuid();
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = new TaskItem { Id = Guid.NewGuid(), UserId = s.OwnerId, OrganizationId = s.Org, Title = "Call", AssigneeId = s.OtherUserId, DealId = deal, CreatedAtUtc = DateTime.UtcNow };
            db.TaskItems.Add(task);
            await db.SaveChangesAsync();
            id = task.Id;
        }

        await Ok(await s.Owner.PutAsync($"/api/tasks/{id}", Body(new { title = "Call back", assigneeId = s.OtherUserId })), "an edit that keeps the assignee");
        await Ok(await s.Owner.PutAsync($"/api/tasks/{id}", Body(new { clearDeal = true })), "unlinking the deal");
        await Ok(await s.Owner.PatchAsync($"/api/tasks/{id}/assign", Body(new { assigneeId = s.RepId })), "handing it to someone still here");
    }

    [Fact]
    public async Task APersonalTaskIsOnlyForItsOwner()
    {
        using var factory = new CustomWebApplicationFactory();
        var (me, _) = await SignInAsync(factory, "solo@example.ch");
        var (_, someoneId) = await SignInAsync(factory, "someone@example.ch");

        await Refused(await NewTask(me, new { title = "Call", assigneeId = someoneId }),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "someone else in a personal workspace", "someone@example.ch");
    }

    [Fact]
    public async Task WorkOnAPrivateDealGoesToSomeoneWhoCanSeeIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);

        await Refused(await NewTask(s.Owner, new { title = "Prepare offer", dealId = s.PrivateDeal, assigneeId = s.RepId }),
            HttpStatusCode.BadRequest, "Link.AssigneeCannotSeeDeal", "a rep who cannot see the deal");
        var task = await Ok(await NewTask(s.Owner, new { title = "Prepare offer", dealId = s.PrivateDeal }), "unassigned");
        var id = task.GetProperty("id").GetGuid();
        await Refused(await s.Owner.PatchAsync($"/api/tasks/{id}/assign", Body(new { assigneeId = s.RepId })),
            HttpStatusCode.BadRequest, "Link.AssigneeCannotSeeDeal", "handing it to the rep afterwards");
    }

    [Fact]
    public async Task ADealCannotLinkToAnotherWorkspacesRecords()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);
        Task<HttpResponseMessage> NewDeal(object extra) => s.Rep.PostAsync("/api/deals", Body(Merge(new { name = "Corporate membership", value = "12000" }, extra)));

        await Refused(await NewDeal(new { companyId = s.TheirCompany }), HttpStatusCode.BadRequest, "Link.CompanyNotFound", "a foreign company", "Their Company SA");
        await Refused(await NewDeal(new { contactId = s.TheirContact }), HttpStatusCode.BadRequest, "Link.ContactNotFound", "a foreign contact", "Bastien");
        await Refused(await NewDeal(new { assigneeId = s.OtherUserId }), HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "an outsider as owner");
        await Refused(await NewDeal(new { pipelineId = s.TheirPipeline }), HttpStatusCode.BadRequest, "Deal.PipelineNotFound", "a foreign pipeline");
        await Refused(await NewDeal(new { dealStageId = s.TheirStage }), HttpStatusCode.BadRequest, "Deal.InvalidStage", "a foreign stage");
        await Refused(await NewDeal(new { pipelineId = s.OurPipeline, dealStageId = s.OurOtherPipelineStage }),
            HttpStatusCode.BadRequest, "Deal.InvalidStage", "a stage from another of our pipelines");

        var deal = await Ok(await NewDeal(new
        {
            companyId = s.OurCompany, contactId = s.OurContact, assigneeId = s.OwnerId, pipelineId = s.OurPipeline, dealStageId = s.OurStage,
        }), "our own records");
        var id = deal.GetProperty("id").GetGuid();

        await Refused(await s.Rep.PutAsync($"/api/deals/{id}", Body(new { companyId = s.TheirCompany })),
            HttpStatusCode.BadRequest, "Link.CompanyNotFound", "an edit to a foreign company", "Their Company SA");
        await Refused(await s.Rep.PutAsync($"/api/deals/{id}", Body(new { assigneeId = s.OtherUserId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "an edit to an outsider");
        await Refused(await s.Rep.PutAsync($"/api/deals/{id}", Body(new { pipelineId = s.OurPipeline, dealStageId = s.OurOtherPipelineStage })),
            HttpStatusCode.BadRequest, "Deal.InvalidStage", "a stage that is not in the pipeline sent with it");
        await Ok(await s.Rep.PutAsync($"/api/deals/{id}", Body(new { description = "Met the board" })), "an edit that changes no link");

        var saved = await factory.Services.Read(db => db.Deals.Single(d => d.Id == id));
        saved.CompanyId.Should().Be(s.OurCompany);
        saved.AssigneeId.Should().Be(s.OwnerId);
        saved.DealStageId.Should().Be(s.OurStage);
    }

    [Fact]
    public async Task APersonalDealHasNoPipelineFromAnOrganization()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);
        var (solo, _) = await SignInAsync(factory, "solo@example.ch");

        await Refused(await solo.PostAsync("/api/deals", Body(new { name = "Mine", value = "1", dealStageId = s.OurStage })),
            HttpStatusCode.BadRequest, "Deal.InvalidStage", "an organization's stage in a personal workspace");
    }

    [Fact]
    public async Task AContactOrLeadCannotTakeAnotherWorkspacesCompany()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);

        await Refused(await s.Rep.PostAsync("/api/contacts", Body(new { name = "Anna", email = "anna@example.ch", companyId = s.TheirCompany })),
            HttpStatusCode.BadRequest, "Link.CompanyNotFound", "a contact at a foreign company", "Their Company SA");
        var contact = await Ok(await s.Rep.PostAsync("/api/contacts", Body(new { name = "Anna", email = "anna@example.ch", companyId = s.OurCompany })),
            "a contact at a teammate's company");
        var contactId = contact.GetProperty("id").GetGuid();
        await Refused(await s.Rep.PutAsync($"/api/contacts/{contactId}", Body(new { companyId = s.TheirCompany })),
            HttpStatusCode.BadRequest, "Link.CompanyNotFound", "moving the contact to a foreign company", "Their Company SA");

        await Refused(await s.Rep.PostAsync("/api/leads", Body(new { name = "Marc", email = "marc@example.ch", status = "New", companyId = s.TheirCompany })),
            HttpStatusCode.BadRequest, "Link.CompanyNotFound", "a lead at a foreign company", "Their Company SA");
        var lead = await Ok(await s.Rep.PostAsync("/api/leads", Body(new { name = "Marc", email = "marc@example.ch", status = "New", companyId = s.OurCompany })),
            "a lead at our company");
        var leadId = lead.GetProperty("id").GetGuid();
        await Refused(await s.Rep.PutAsync($"/api/leads/{leadId}", Body(new { companyId = s.TheirCompany })),
            HttpStatusCode.BadRequest, "Link.CompanyNotFound", "moving the lead to a foreign company", "Their Company SA");

        (await factory.Services.Read(db => db.Contacts.Single(c => c.Id == contactId).CompanyId)).Should().Be(s.OurCompany);
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).CompanyId)).Should().Be(s.OurCompany);
    }

    [Fact]
    public async Task ALeadIsOnlyAssignedToAMemberOfTheWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);
        var lead = await Ok(await s.Owner.PostAsync("/api/leads", Body(new { name = "Marc", email = "marc@example.ch", status = "New" })), "a lead");
        var id = lead.GetProperty("id").GetGuid();

        await Refused(await s.Owner.PutAsync($"/api/leads/{id}/assign", Body(new { assignedToUserId = s.OtherUserId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "an outsider as owner", "owner@rival.ch");
        (await Ok(await s.Owner.PutAsync($"/api/leads/{id}/assign", Body(new { assignedToUserId = s.RepId })), "a teammate"))
            .GetProperty("assignedToUserId").GetGuid().Should().Be(s.RepId);

        var (solo, soloId) = await SignInAsync(factory, "solo@example.ch");
        var mine = (await Ok(await solo.PostAsync("/api/leads", Body(new { name = "Lea", email = "lea@example.ch", status = "New" })), "a personal lead"))
            .GetProperty("id").GetGuid();
        await Refused(await solo.PutAsync($"/api/leads/{mine}/assign", Body(new { assignedToUserId = s.OwnerId })),
            HttpStatusCode.Forbidden, "Link.AssigneeNotMember", "someone else in a personal workspace");
        (await Ok(await solo.PutAsync($"/api/leads/{mine}/assign", Body(new { assignedToUserId = soloId })), "yourself"))
            .GetProperty("assignedToUserId").GetGuid().Should().Be(soloId);
    }

    [Fact]
    public async Task ConvertingALeadUsesOnlyThisWorkspacesPipeline()
    {
        using var factory = new CustomWebApplicationFactory();
        var s = await SetUpAsync(factory);
        var lead = await Ok(await s.Rep.PostAsync("/api/leads", Body(new { name = "Marc", email = "marc@example.ch", status = "New" })), "a lead");
        var id = lead.GetProperty("id").GetGuid();

        await Refused(await s.Rep.PostAsync($"/api/leads/{id}/convert", Body(new { createDeal = true, pipelineId = s.TheirPipeline, dealStageId = s.TheirStage })),
            HttpStatusCode.BadRequest, "Deal.PipelineNotFound", "a foreign pipeline");
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == id).IsConverted)).Should().BeFalse("nothing was converted");
        (await factory.Services.Read(db => db.Deals.Count(d => d.OrganizationId == s.Org))).Should().Be(1, "only the private deal from the setup");

        await Ok(await s.Rep.PostAsync($"/api/leads/{id}/convert", Body(new { createDeal = true, pipelineId = s.OurPipeline, dealStageId = s.OurStage })),
            "our own pipeline");
    }

    /// <summary>Two anonymous objects as one JSON body.</summary>
    private static Dictionary<string, object?> Merge(object first, object second)
    {
        var merged = new Dictionary<string, object?>();
        foreach (var part in new[] { first, second })
            foreach (var property in part.GetType().GetProperties())
                merged[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property.GetValue(part);
        return merged;
    }
}
