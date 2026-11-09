using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The bell tells teammates when work comes their way: a task handed to them, a comment
/// on a task they own or work, a mention in a comment or a note.
/// </summary>
/// <remarks>
/// None of it existed. A task could be given to a rep, discussed under them, or a rep
/// asked by name for help in a note, and they found out only by opening the record.
/// </remarks>
public class CollaborationNotificationTests
{
    private sealed record Team(
        CustomWebApplicationFactory Factory, Guid Org,
        HttpClient Marc, Guid MarcId, HttpClient Anna, Guid AnnaId, HttpClient Luca, Guid LucaId) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    /// <summary>Marc owns the workspace; Anna Keller and Luca Rossi are members.</summary>
    private static async Task<Team> TeamAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (marc, marcId) = await SignInAsync(factory, "marc@pavillon46.ch");
        var org = await CreateAsync(factory, marcId, "Pavillon46");
        var (anna, annaId) = await SignInAsync(factory, "anna@pavillon46.ch");
        var (luca, lucaId) = await SignInAsync(factory, "luca@pavillon46.ch");
        await AddMemberAsync(factory, org, annaId, OrgMemberRole.Member);
        await AddMemberAsync(factory, org, lucaId, OrgMemberRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single(u => u.Id == marcId).Name = "Marc Weber";
            db.Users.Single(u => u.Id == annaId).Name = "Anna Keller";
            db.Users.Single(u => u.Id == lucaId).Name = "Luca Rossi";
            await db.SaveChangesAsync();
        }
        return new Team(factory, org, marc.In(org), marcId, anna.In(org), annaId, luca.In(org), lucaId);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<List<JsonElement>> BellAsync(HttpClient client) =>
        (await Ok(await client.GetAsync("/api/notifications"), "read the bell")).EnumerateArray().ToList();

    private static async Task<Guid> TaskAsync(HttpClient client, string title, Guid? assigneeId = null, Guid? dealId = null) =>
        (await Ok(await client.PostAsync("/api/tasks", Body(new { title, assigneeId, dealId })), $"create {title}")).GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client) =>
        (await Ok(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();

    [Fact]
    public async Task ATaskMadeForSomeoneElseTellsThemWhoGaveItAndWhatItIs()
    {
        using var team = await TeamAsync();

        var taskId = await TaskAsync(team.Marc, "Call the Müllers about renewal", team.AnnaId);

        var bell = await BellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("type").GetString().Should().Be("task");
        bell[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you a task");
        bell[0].GetProperty("message").GetString().Should().Be("Call the Müllers about renewal");
        bell[0].GetProperty("link").GetString().Should().Be($"/tasks/{taskId}");
        (await BellAsync(team.Marc)).Should().BeEmpty("nobody is told about what they did themselves");
    }

    [Fact]
    public async Task ATaskYouGiveYourselfTellsNobody()
    {
        using var team = await TeamAsync();

        await TaskAsync(team.Anna, "My own follow-up", team.AnnaId);

        (await BellAsync(team.Anna)).Should().BeEmpty();
    }

    [Fact]
    public async Task HandingATaskOnTellsTheNewAssigneeOnce()
    {
        using var team = await TeamAsync();
        var taskId = await TaskAsync(team.Marc, "Prepare the tasting", team.AnnaId);

        await Ok(await team.Marc.PatchAsync($"/api/tasks/{taskId}/assign", Body(new { assigneeId = team.LucaId })), "hand it to Luca");
        await Ok(await team.Marc.PutAsync($"/api/tasks/{taskId}", Body(new { assigneeId = team.LucaId, priority = "high" })), "edit it, Luca still on it");

        var bell = await BellAsync(team.Luca);
        bell.Should().ContainSingle("an edit that leaves the task with Luca hands him nothing new");
        bell[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you a task");

        await Ok(await team.Marc.PutAsync($"/api/tasks/{taskId}", Body(new { assigneeId = team.AnnaId })), "give it back to Anna");
        (await BellAsync(team.Anna)).Should().HaveCount(2, "handed to her again, she is told again");
    }

    [Fact]
    public async Task TasksHandedOverTogetherAreOneLine()
    {
        using var team = await TeamAsync();
        var ids = new List<Guid>();
        foreach (var title in new[] { "Call A", "Call B", "Call C", "Call D" })
            ids.Add(await TaskAsync(team.Marc, title));

        await Ok(await team.Marc.PostAsync("/api/tasks/bulk", Body(new { taskIds = ids, action = "assignee", assigneeId = team.AnnaId })), "bulk assign");

        var bell = await BellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you 4 tasks");
        bell[0].GetProperty("message").GetString().Should().Be("Call A; Call B; Call C; and 1 more");
        bell[0].GetProperty("link").GetString().Should().Be("/tasks");
    }

    [Fact]
    public async Task ACommentTellsWhoeverWorksTheTaskAndWhoeverMadeIt()
    {
        using var team = await TeamAsync();
        var taskId = await TaskAsync(team.Marc, "Send the offer", team.AnnaId);
        var before = (await BellAsync(team.Anna)).Count;

        await Ok(await team.Luca.PostAsync($"/api/tasks/{taskId}/comments", Body(new { body = "They asked for the corporate rate." })), "Luca comments");

        var anna = await BellAsync(team.Anna);
        anna.Should().HaveCount(before + 1);
        anna[0].GetProperty("title").GetString().Should().Be("Luca Rossi commented on a task");
        anna[0].GetProperty("message").GetString().Should().Be("Send the offer: They asked for the corporate rate.");
        anna[0].GetProperty("link").GetString().Should().Be($"/tasks/{taskId}");
        (await BellAsync(team.Marc)).Should().ContainSingle(n => n.GetProperty("title").GetString() == "Luca Rossi commented on a task");
        (await BellAsync(team.Luca)).Should().BeEmpty();
    }

    [Fact]
    public async Task AMentionInACommentTellsThatPersonOnceAsAMention()
    {
        using var team = await TeamAsync();
        var taskId = await TaskAsync(team.Marc, "Send the offer");

        await Ok(await team.Anna.PostAsync($"/api/tasks/{taskId}/comments", Body(new { body = "@Luca can you check the price list?" })), "Anna mentions Luca");

        var luca = await BellAsync(team.Luca);
        luca.Should().ContainSingle();
        luca[0].GetProperty("type").GetString().Should().Be("mention");
        luca[0].GetProperty("title").GetString().Should().Be("Anna Keller mentioned you");
        luca[0].GetProperty("message").GetString().Should().Be("On the task \"Send the offer\": @Luca can you check the price list?");
        // Marc made the task, so he hears of the comment, as a comment.
        (await BellAsync(team.Marc)).Single().GetProperty("type").GetString().Should().Be("task");
    }

    [Fact]
    public async Task AMentionInANoteTellsThemAndAnEditTellsOnlyTheNewlyMentioned()
    {
        using var team = await TeamAsync();
        var leadId = await LeadAsync(team.Marc);

        var note = await Ok(await team.Marc.PostAsync("/api/activities", Body(new
        {
            type = "note", subject = "Hot lead", body = "@Anna Keller please call them before Friday.", leadId,
        })), "log the note");

        var anna = await BellAsync(team.Anna);
        anna.Should().ContainSingle();
        anna[0].GetProperty("type").GetString().Should().Be("mention");
        anna[0].GetProperty("message").GetString().Should().Be("In a note on Jean Dupont: @Anna Keller please call them before Friday.");
        anna[0].GetProperty("link").GetString().Should().Be($"/leads/{leadId}");

        await Ok(await team.Marc.PutAsync($"/api/activities/{note.GetProperty("id").GetGuid()}", Body(new
        {
            body = "@Anna Keller please call them before Friday. @luca can join.",
        })), "edit the note");

        (await BellAsync(team.Anna)).Should().ContainSingle("she was told already");
        (await BellAsync(team.Luca)).Should().ContainSingle().Which.GetProperty("type").GetString().Should().Be("mention");

        // Taken out and put back, she is still told only the once.
        var noteId = note.GetProperty("id").GetGuid();
        await Ok(await team.Marc.PutAsync($"/api/activities/{noteId}", Body(new { body = "@luca can join." })), "take Anna out");
        await Ok(await team.Marc.PutAsync($"/api/activities/{noteId}", Body(new { body = "@luca can join, and @Anna Keller." })), "put her back");
        (await BellAsync(team.Anna)).Should().ContainSingle();
    }

    [Fact]
    public async Task AnEmailAddressInANoteIsNotAMention()
    {
        using var team = await TeamAsync();
        var leadId = await LeadAsync(team.Marc);

        await Ok(await team.Marc.PostAsync("/api/activities", Body(new
        {
            type = "note", body = "Their assistant copies bob@anna.ch on everything.", leadId,
        })), "log the note");

        (await BellAsync(team.Anna)).Should().BeEmpty();
    }

    [Fact]
    public async Task NobodyIsToldWhoTurnedTheBellOff()
    {
        using var team = await TeamAsync();
        using (var scope = team.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserSettings.Add(new UserSettings { UserId = team.AnnaId, InAppNotificationsEnabled = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await TaskAsync(team.Marc, "Call the Müllers", team.AnnaId);

        (await BellAsync(team.Anna)).Should().BeEmpty();
    }

    [Fact]
    public async Task NobodyHearsOfWorkOnAPrivateDealTheyCannotOpen()
    {
        using var team = await TeamAsync();
        var deal = await Ok(await team.Marc.PostAsync("/api/deals", Body(new { name = "Board member's renewal", amount = 9000, currency = "CHF" })), "create deal");
        var dealId = deal.GetProperty("id").GetGuid();
        await Ok(await team.Marc.PutAsync($"/api/deals/{dealId}", Body(new { isPrivate = true })), "make it private");
        var taskId = await TaskAsync(team.Marc, "Draft the confidential terms", dealId: dealId);

        await Ok(await team.Marc.PostAsync($"/api/tasks/{taskId}/comments", Body(new { body = "@Anna Keller have a look?" })), "mention Anna");
        await Ok(await team.Marc.PostAsync("/api/activities", Body(new { type = "note", body = "@Luca Rossi FYI", dealId })), "mention Luca in a note");

        (await BellAsync(team.Anna)).Should().BeEmpty("the task, its title and the comment are as hidden as the deal");
        (await BellAsync(team.Luca)).Should().BeEmpty();
    }

    [Fact]
    public async Task ATaskARuleMakesForTheRepSaysTheRuleGaveItToThem()
    {
        using var team = await TeamAsync();
        await Ok(await team.Marc.PostAsync("/api/automations", Body(new
        {
            name = "New web lead", trigger = "lead.created", enabled = true,
            actions = new object[]
            {
                new { type = "create_task", config = new { title = "Call within the day", dueInDays = "0" } },
                new { type = "assign", config = new { userId = team.AnnaId.ToString() } },
            },
        })), "add the rule");

        await LeadAsync(team.Marc);

        var bell = await BellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("The automation \"New web lead\" gave you a task");
        bell[0].GetProperty("message").GetString().Should().Be("Call within the day");
    }

    [Fact]
    public async Task ALeadHandedToARepTellsThemOnce()
    {
        using var team = await TeamAsync();
        var leadId = await LeadAsync(team.Marc);

        await Ok(await team.Marc.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = team.AnnaId })), "give it to Anna");
        await Ok(await team.Marc.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = team.AnnaId })), "again, no change");

        var bell = await BellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("type").GetString().Should().Be("lead");
        bell[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you a lead");
        bell[0].GetProperty("message").GetString().Should().Be("Jean Dupont");
        bell[0].GetProperty("link").GetString().Should().Be($"/leads/{leadId}");
    }

    [Fact]
    public async Task LeadsHandedOverTogetherAreOneLine()
    {
        using var team = await TeamAsync();
        var ids = new List<Guid>();
        foreach (var (name, email) in new[] { ("Ada Lovelace", "ada@example.ch"), ("Grace Hopper", "grace@example.ch") })
            ids.Add((await Ok(await team.Marc.PostAsync("/api/leads", Body(new { name, email, status = "New" })), $"create {name}")).GetProperty("id").GetGuid());

        await Ok(await team.Marc.PostAsync("/api/lead-ops/bulk", Body(new { ids, assignedToUserId = team.LucaId })), "bulk owner");

        var bell = await BellAsync(team.Luca);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you 2 leads");
        bell[0].GetProperty("message").GetString().Should().Be("Ada Lovelace; Grace Hopper");
        bell[0].GetProperty("link").GetString().Should().Be("/leads");
    }

    [Fact]
    public async Task ANewLeadRoutedToARepTellsThemRoutingGaveItToThem()
    {
        using var team = await TeamAsync();
        await Ok(await team.Marc.PostAsync("/api/lead-ops/assignment", Body(new
        {
            id = Guid.Empty, name = "Everything to Anna", kind = "roundrobin", priority = 1,
            sourceMatch = (string?)null, territoryId = (Guid?)null, queueId = (Guid?)null,
            memberUserIds = new[] { team.AnnaId }, isActive = true,
        })), "add the routing rule");

        var leadId = await LeadAsync(team.Marc);

        var bell = await BellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("Lead routing gave you a lead");
        bell[0].GetProperty("link").GetString().Should().Be($"/leads/{leadId}");
    }

    [Fact]
    public async Task ADealMadeForOrHandedToSomeoneTellsThem()
    {
        using var team = await TeamAsync();

        var deal = await Ok(await team.Marc.PostAsync("/api/deals", Body(new { name = "Corporate membership", amount = 4800, currency = "CHF", assigneeId = team.AnnaId })), "create deal");
        var dealId = deal.GetProperty("id").GetGuid();
        await Ok(await team.Marc.PutAsync($"/api/deals/{dealId}", Body(new { assigneeId = team.LucaId })), "hand it to Luca");

        var anna = await BellAsync(team.Anna);
        anna.Should().ContainSingle();
        anna[0].GetProperty("type").GetString().Should().Be("deal");
        anna[0].GetProperty("title").GetString().Should().Be("Marc Weber assigned you a deal");
        anna[0].GetProperty("link").GetString().Should().Be($"/deals/{dealId}");
        (await BellAsync(team.Luca)).Should().ContainSingle().Which.GetProperty("message").GetString().Should().Be("Corporate membership");
    }

    [Fact]
    public async Task ACommentIsStillSavedWhenNobodyCanBeTold()
    {
        using var team = await TeamAsync();
        var taskId = await TaskAsync(team.Anna, "Solo task");

        var res = await team.Anna.PostAsync($"/api/tasks/{taskId}/comments", Body(new { body = "@nobody-by-that-name note to self" }));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BellAsync(team.Anna)).Should().BeEmpty();
    }
}
