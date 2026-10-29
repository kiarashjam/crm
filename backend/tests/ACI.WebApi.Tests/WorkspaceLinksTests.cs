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
}
