using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What a sequence's steps do to real people, run through the real repositories.
/// </summary>
public class SequenceOutreachTests
{
    /// <summary>
    /// The app, with a mail server that takes everything and remembers it, and due
    /// steps run by the test rather than by the minute-by-minute runner.
    /// </summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly List<string> SentTo = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var runner = services.Single(d => d.ImplementationType == typeof(ACI.WebApi.Services.LeadFollowUpBackgroundService));
                services.Remove(runner);
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendHtmlMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string to, string _, string _, string _, string _, CancellationToken _) => SentTo.Add(to))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }

        public async Task RunDueStepsAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISequenceService>().RunDueAsync();
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> SequenceAsync(HttpClient client, object step) =>
        (await Json(await client.PostAsync("/api/sequences", Body(new { name = "New lead outreach", status = "active", steps = new[] { step } })), "create sequence"))
            .GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client, string email = "jean@example.ch") =>
        (await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email, status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> EnrollAsync(HttpClient client, Guid sequenceId, Guid leadId) =>
        client.PostAsync("/api/sequences/enroll", Body(new { sequenceId, targetType = "lead", targetId = leadId, targetName = "Jean Dupont" }));

    /// <summary>Ticks Do Not Contact on the contact the app made for this lead, as a rep does on its page.</summary>
    private static async Task MarkPersonDoNotContactAsync(CustomWebApplicationFactory app, HttpClient client, Guid leadId)
    {
        var contactId = await app.Services.Read(db => db.Contacts.Single(c => c.ConvertedFromLeadId == leadId).Id);
        await Json(await client.PutAsync($"/api/contacts/{contactId}", Body(new { doNotContact = true })), "mark do not contact");
    }

    private static async Task<string?> EnrollmentStatusAsync(HttpClient client, Guid enrollmentId) =>
        (await Json(await client.GetAsync("/api/sequences/enrollments"), "list enrollments")).EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == enrollmentId).GetProperty("status").GetString();

    [Fact]
    public async Task ALeadWhosePersonSaidStopIsNotEnrolled()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = await SequenceAsync(owner, new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi {{firstName}}" });
        var leadId = await LeadAsync(owner);
        await MarkPersonDoNotContactAsync(app, owner, leadId);

        var res = await EnrollAsync(owner, sequenceId, leadId);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("they are marked do not contact");
    }

    [Fact]
    public async Task AnEnrolledLeadWhosePersonSaysStopGetsNoMoreEmail()
    {
        // The rep ticks Do Not Contact on the person's contact after they reply "please
        // stop". The enrollment targets the lead, whose state never carried the flag,
        // so the next steps used to go out anyway.
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = await SequenceAsync(owner, new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi {{firstName}}" });
        var leadId = await LeadAsync(owner);
        var enrollmentId = (await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll")).GetProperty("id").GetGuid();
        await MarkPersonDoNotContactAsync(app, owner, leadId);

        await app.RunDueStepsAsync();

        app.SentTo.Should().BeEmpty();
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped");
    }

    [Fact]
    public async Task AnotherRecordWithTheSameAddressSayingStopCountsToo()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        var sequenceId = await SequenceAsync(owner, new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi" });
        var leadId = await LeadAsync(owner);
        var enrollmentId = (await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll")).GetProperty("id").GetGuid();
        // An old record for the same person, since archived, said stop. Archiving a
        // record does not take that back.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            db.Contacts.Add(new ACI.Domain.Entities.Contact
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "J. Dupont", Email = " JEAN@example.ch",
                DoNotContact = true, IsArchived = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await app.RunDueStepsAsync();

        app.SentTo.Should().BeEmpty();
        (await EnrollmentStatusAsync(owner, enrollmentId)).Should().Be("stopped");
    }

    [Fact]
    public async Task AReachableLeadStillGetsTheEmail()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = await SequenceAsync(owner, new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi" });
        var leadId = await LeadAsync(owner);
        await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll");

        await app.RunDueStepsAsync();

        app.SentTo.Should().Equal("jean@example.ch");
    }

    [Fact]
    public async Task ACallStepGoesToWhoeverWorksTheLead()
    {
        // The owner enrolls a lead that was routed to a rep. The call step used to be
        // created for the owner, so the rep never saw it.
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        var (_, repId) = await SignInAsync(app, "rep@example.ch");
        await AddMemberAsync(app, org, repId, OrgMemberRole.Member);
        var sequenceId = await SequenceAsync(owner, new { type = "call", dayOffset = 0, taskTitle = "Call Jean" });
        var leadId = await LeadAsync(owner);
        await Json(await owner.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = repId })), "assign to the rep");
        await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll");

        await app.RunDueStepsAsync();

        var task = (await Json(await owner.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).EnumerateArray().Single();
        task.GetProperty("title").GetString().Should().Be("Call Jean");
        task.GetProperty("assigneeId").GetGuid().Should().Be(repId);
    }

    [Fact]
    public async Task ACallStepForAnUnassignedLeadStaysWithWhoeverEnrolledThem()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = await SequenceAsync(owner, new { type = "call", dayOffset = 0, taskTitle = "Call Jean" });
        var leadId = await LeadAsync(owner);
        await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll");

        await app.RunDueStepsAsync();

        var task = await app.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId));
        task.AssigneeId.Should().BeNull();
        task.UserId.Should().Be(ownerId);
    }
}
