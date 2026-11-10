using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Tests;

/// <summary>
/// Removing a member hands their open work to another member, and nothing of the
/// organization reminds them any more.
/// </summary>
/// <remarks>
/// Removal used to delete the membership and nothing else. The rep's leads and deals kept
/// an owner nobody could act as, and their open tasks went on emailing reminders, with
/// customers' names in them, to someone who no longer worked for the club.
/// </remarks>
public class MemberOffboardingTests
{
    /// <summary>The app with a mail server that remembers who it sent task reminders to.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly List<(string To, string Title)> Reminded = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendTaskReminderEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string to, string _, string title, DateTime? _, string _, CancellationToken _) => Reminded.Add((to, title)))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private sealed record Team(App App, HttpClient Owner, Guid OwnerId, Guid Org, Guid MarcId, HttpClient Anna, Guid AnnaId, Guid ViewerId);

    private static async Task<Team> TeamAsync()
    {
        var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        var (_, marcId) = await SignInAsync(app, "marc@pavillon46.ch");
        await AddMemberAsync(app, org, marcId, OrgMemberRole.Member);
        var (anna, annaId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AddMemberAsync(app, org, annaId, OrgMemberRole.Member);
        var (_, viewerId) = await SignInAsync(app, "board@pavillon46.ch");
        await AddMemberAsync(app, org, viewerId, OrgMemberRole.Viewer);
        return new Team(app, owner.In(org), ownerId, org, marcId, anna.In(org), annaId, viewerId);
    }

    private sealed record Work(Guid AssignedLead, Guid OwnLead, Guid ConvertedLead, Guid LostLead, Guid OpenDeal, Guid WonDeal, Guid DueTask, Guid DoneTask, Guid AnnasTask);

    /// <summary>Marc's work: what is still open, what is over, and what he gave someone else.</summary>
    private static async Task<Work> MarcsWorkAsync(Team t)
    {
        var now = DateTime.UtcNow;
        Lead L(string name, Guid userId, Guid? assignee, string status = "Contacted", bool converted = false) => new()
        {
            Id = Guid.NewGuid(), UserId = userId, AssignedToUserId = assignee, OrganizationId = t.Org, Name = name,
            Email = $"{Guid.NewGuid():N}@example.ch", Status = status, IsConverted = converted, CreatedAtUtc = now,
        };
        Deal D(string name, bool? won) => new()
        {
            Id = Guid.NewGuid(), UserId = t.OwnerId, AssigneeId = t.MarcId, OrganizationId = t.Org, Name = name, Value = "1000", IsWon = won, CreatedAtUtc = now,
        };
        TaskItem T(string title, Guid? assignee, TaskStatusEnum status = TaskStatusEnum.Todo) => new()
        {
            Id = Guid.NewGuid(), UserId = t.MarcId, AssigneeId = assignee, OrganizationId = t.Org, Title = title, Status = status,
            Completed = status == TaskStatusEnum.Completed, ReminderDateUtc = now.AddMinutes(-5), DueDateUtc = now.AddHours(3), CreatedAtUtc = now,
        };
        var assigned = L("Müller family", t.OwnerId, t.MarcId);
        var own = L("Banque Léman", t.MarcId, null, "New");
        var converted = L("Hotel Bellevue", t.MarcId, t.MarcId, "Signed", converted: true);
        var lost = L("Café Central", t.MarcId, t.MarcId, "Lost / Not Interested");
        var openDeal = D("Gold membership", null);
        var wonDeal = D("Silver membership", true);
        var due = T("Call Mrs Keller about her renewal", t.MarcId);
        var done = T("Send the welcome pack", t.MarcId, TaskStatusEnum.Completed);
        var annas = T("Book the tasting", t.AnnaId);
        using var scope = t.App.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        db.AddRange(assigned, own, converted, lost, openDeal, wonDeal, due, done, annas);
        await db.SaveChangesAsync();
        return new Work(assigned.Id, own.Id, converted.Id, lost.Id, openDeal.Id, wonDeal.Id, due.Id, done.Id, annas.Id);
    }

    private static async Task<JsonElement> RemoveAsync(HttpClient client, Guid org, Guid member, Guid? handOverTo, HttpStatusCode expected)
    {
        var url = $"/api/organizations/{org}/members/{member}" + (handOverTo is Guid to ? $"?handOverTo={to}" : "");
        var res = await client.DeleteAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task A_removed_reps_open_work_goes_to_the_member_chosen_and_what_is_over_stays_theirs()
    {
        var t = await TeamAsync();
        using var _ = t.App;
        var w = await MarcsWorkAsync(t);

        var removed = await RemoveAsync(t.Owner, t.Org, t.MarcId, t.AnnaId, HttpStatusCode.OK);

        removed.GetProperty("handedOverToUserId").GetGuid().Should().Be(t.AnnaId);
        removed.GetProperty("leads").GetInt32().Should().Be(2);
        removed.GetProperty("deals").GetInt32().Should().Be(1);
        removed.GetProperty("tasks").GetInt32().Should().Be(1);
        var state = await t.App.Services.Read(db => new
        {
            Leads = db.Leads.ToDictionary(l => l.Id, l => l.AssignedToUserId),
            Deals = db.Deals.ToDictionary(d => d.Id, d => d.AssigneeId),
            Tasks = db.TaskItems.ToDictionary(x => x.Id, x => x.AssigneeId),
            StillMember = db.OrganizationMembers.Any(m => m.OrganizationId == t.Org && m.UserId == t.MarcId),
        });
        state.StillMember.Should().BeFalse();
        state.Leads[w.AssignedLead].Should().Be(t.AnnaId);
        state.Leads[w.OwnLead].Should().Be(t.AnnaId, "a lead of his that nobody was assigned to was his to work");
        state.Leads[w.ConvertedLead].Should().Be(t.MarcId, "a converted lead keeps whoever won it");
        state.Leads[w.LostLead].Should().Be(t.MarcId, "a lost lead keeps whoever worked it");
        state.Deals[w.OpenDeal].Should().Be(t.AnnaId);
        state.Deals[w.WonDeal].Should().Be(t.MarcId, "a won deal is still credited to him");
        state.Tasks[w.DueTask].Should().Be(t.AnnaId);
        state.Tasks[w.DoneTask].Should().Be(t.MarcId);
        state.Tasks[w.AnnasTask].Should().Be(t.AnnaId);

        // Anna hears what she was given, and sees it as hers.
        var bell = await (await t.Anna.GetAsync("/api/notifications")).Content.ReadAsStringAsync();
        bell.Should().Contain("assigned you 2 leads").And.Contain("Gold membership").And.Contain("Call Mrs Keller about her renewal");
        (await (await t.Anna.GetAsync($"/api/deals/{w.OpenDeal}")).Content.ReadAsStringAsync()).Should().Contain(t.AnnaId.ToString());

        // The reminder that was due goes to her, and nothing goes to him.
        await new TaskReminderBackgroundService(t.App.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance)
            .ProcessRemindersAsync(CancellationToken.None);
        t.App.Reminded.Should().Contain(("anna@pavillon46.ch", "Call Mrs Keller about her renewal"));
        t.App.Reminded.Should().NotContain(r => r.To == "marc@pavillon46.ch", "he no longer works here");
    }

    [Fact]
    public async Task With_nobody_chosen_the_owner_takes_over()
    {
        var t = await TeamAsync();
        using var _ = t.App;
        var w = await MarcsWorkAsync(t);

        var removed = await RemoveAsync(t.Owner, t.Org, t.MarcId, null, HttpStatusCode.OK);

        removed.GetProperty("handedOverToUserId").GetGuid().Should().Be(t.OwnerId);
        (await t.App.Services.Read(db => db.Leads.Single(l => l.Id == w.AssignedLead).AssignedToUserId)).Should().Be(t.OwnerId);
        (await t.App.Services.Read(db => db.TaskItems.Single(x => x.Id == w.DueTask).AssigneeId)).Should().Be(t.OwnerId);
    }

    [Fact]
    public async Task The_work_goes_only_to_a_member_who_can_work_it_and_nobody_is_removed_otherwise()
    {
        var t = await TeamAsync();
        using var _ = t.App;
        var w = await MarcsWorkAsync(t);
        var (_, outsiderId) = await SignInAsync(t.App, "someone@rival.ch");

        (await RemoveAsync(t.Owner, t.Org, t.MarcId, t.ViewerId, HttpStatusCode.BadRequest)).GetProperty("errorCode").GetString()
            .Should().Be("Organization.HandOverReadOnly");
        (await RemoveAsync(t.Owner, t.Org, t.MarcId, outsiderId, HttpStatusCode.BadRequest)).GetProperty("errorCode").GetString()
            .Should().Be("Organization.HandOverOutsider");
        (await RemoveAsync(t.Owner, t.Org, t.MarcId, t.MarcId, HttpStatusCode.BadRequest)).GetProperty("errorCode").GetString()
            .Should().Be("Organization.HandOverToLeaver");
        await RemoveAsync(t.Anna, t.Org, t.MarcId, t.AnnaId, HttpStatusCode.Forbidden);

        (await t.App.Services.Read(db => db.OrganizationMembers.Any(m => m.OrganizationId == t.Org && m.UserId == t.MarcId))).Should().BeTrue();
        (await t.App.Services.Read(db => db.Leads.Single(l => l.Id == w.AssignedLead).AssignedToUserId)).Should().Be(t.MarcId);
    }
}
