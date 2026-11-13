using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;
using App = ACI.WebApi.Tests.AutomationRunsTests.App;

namespace ACI.WebApi.Tests;

/// <summary>
/// A person's export and erasure meeting the automation runs, which were built at the same
/// time: a run keeps the name of the record it is about, the address it wrote to and a log of
/// what each step did, and a run still waiting carries on by itself.
/// </summary>
/// <remarks>
/// The erasure knew the marks that stop a rule firing twice, but not the runs: after it, the
/// rules page went on listing "Anna Keller" and "Queued the email … to anna.keller@…", the
/// export left the runs out, and a run about the member account it keeps for the books made
/// tasks about "Erased member 1".
/// </remarks>
public class ErasureAndAutomationsTests
{
    private const string AnnaEmail = "anna.keller@example.ch";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid Org)> ClubAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "olga@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, org);
    }

    private static async Task<Guid> RuleAsync(HttpClient client, object rule) =>
        (await Json(await client.PostAsync("/api/automations", Body(rule)), "add the rule")).GetProperty("id").GetGuid();

    private static object Task(string title) => new { type = "create_task", config = new { title } };
    private static object Wait(int amount, string unit) => new { type = "wait", config = new { amount = amount.ToString(), unit } };
    private static object Email(string subject, string? purpose = null) =>
        new { type = "send_email", config = purpose == null ? (object)new { subject, body = "Dear {{firstName}}" } : new { subject, body = "Dear {{firstName}}", purpose } };

    /// <summary>Everything a lookup finds by the address, as the erasure and the export take it.</summary>
    private static async Task<Dictionary<string, object>> EverythingFoundAsync(HttpClient client, object lookup)
    {
        var found = await Json(await client.PostAsync("/api/privacy/find", Body(lookup)), "find the person");
        Guid[] Ids(string kind) => found.GetProperty("records").EnumerateArray()
            .Where(r => r.GetProperty("kind").GetString() == kind).Select(r => r.GetProperty("id").GetGuid()).ToArray();
        return new Dictionary<string, object>
        {
            ["leadIds"] = Ids("lead"), ["contactIds"] = Ids("contact"), ["memberIds"] = Ids("member"), ["applicationIds"] = Ids("application"),
            ["addresses"] = found.GetProperty("addresses").EnumerateArray().Select(a => a.GetString()!).ToArray(),
        };
    }

    [Fact]
    public async Task TheExportHandsOverTheRunsAboutThePerson_AndTheErasureTakesThemWithTheirLogs_LeavingEveryoneElsesRuns()
    {
        await using var app = new App();
        var (owner, _) = await ClubAsync(app);
        await RuleAsync(owner, new
        {
            name = "Nurture", trigger = "lead.created",
            actions = new object[] { Task("Call them"), Wait(2, "days"), Email("Still interested, {{firstName}}?") },
        });
        await RuleAsync(owner, new
        {
            name = "Welcome", trigger = "member.created",
            actions = new object[] { Wait(1, "days"), Email("Welcome to the club", purpose: "service") },
        });

        var annaLead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Anna Keller", email = AnnaEmail, status = "New" })), "Anna's lead")).GetProperty("id").GetGuid();
        var bobLead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Bob Brown", email = "bob@example.ch", status = "New" })), "Bob's lead")).GetProperty("id").GetGuid();
        var annaMember = (await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = "Anna", lastName = "Keller", email = AnnaEmail, tier = "Gold", status = "Active",
        })), "Anna joins")).GetProperty("id").GetGuid();
        var runs = await app.Services.Read(db => db.AutomationRuns.ToList());
        runs.Should().HaveCount(3, "a nurture run for each lead and a welcome for the member, each waiting");
        runs.Should().OnlyContain(r => r.Status == AutomationRunStatuses.Waiting);
        (await app.Services.Read(db => db.AutomationRunSteps.Any(s => s.Summary.Contains("Call them")))).Should().BeTrue("the log says what the first step did");

        var anna = await EverythingFoundAsync(owner, new { email = AnnaEmail });
        ((Guid[])anna["memberIds"]).Should().Equal(annaMember);
        ((Guid[])anna["leadIds"]).Should().Equal(annaLead);

        // The dry run counts her runs among what goes.
        var plan = await Json(await owner.PostAsync("/api/privacy/erasure/preview", Body(anna)), "preview");
        var line = plan.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("area").GetString() == "Automation runs");
        (line.GetProperty("action").GetString(), line.GetProperty("count").GetInt32()).Should().Be(("delete", 2));

        // Her export holds them, each with its log; Bob's stay out.
        var export = await owner.PostAsync("/api/privacy/export", Body(anna));
        export.StatusCode.Should().Be(HttpStatusCode.OK, await export.Content.ReadAsStringAsync());
        using (var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(), ZipArchiveMode.Read))
        {
            var entry = zip.GetEntry("outreach/automation-runs.json");
            entry.Should().NotBeNull("the runs about her are data about her");
            using var reader = new StreamReader(entry!.Open());
            var json = await reader.ReadToEndAsync();
            json.Should().Contain("Nurture").And.Contain("Welcome").And.Contain("Created the task").And.Contain("\"log\"");
            json.Should().NotContain("Bob Brown").And.NotContain("bob@example.ch");
            json.Should().NotContain("stepsJson", "the rule's own steps are the workspace's, not hers");
        }

        anna["confirm"] = "ERASE";
        await Json(await owner.PostAsync("/api/privacy/erasure", Body(anna)), "erase Anna");

        await using (var db = app.Services.EveryWorkspace())
        {
            var left = db.AutomationRuns.ToList();
            left.Should().ContainSingle("her two runs went; Bob's is his").Which.LeadId.Should().Be(bobLead);
            left[0].Status.Should().Be(AutomationRunStatuses.Waiting);
            db.AutomationRunSteps.Where(s => s.RunId != left[0].Id).Should().BeEmpty("a run goes with its log");
            db.AutomationRunSteps.Should().NotBeEmpty("Bob's log stays");
            db.AutomationRuns.Should().NotContain(r => (r.RecordName ?? "").Contains("Anna") || (r.Email ?? "").Contains("anna"));
        }

        // Two days on, Bob's email goes; nothing is left that could write to her.
        app.Clock.Advance(TimeSpan.FromDays(3));
        await app.RunDueAsync();
        var sent = await app.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.Automation).ToList());
        sent.Should().ContainSingle().Which.Subject.Should().Be("Still interested, Bob?");
    }

    [Fact]
    public async Task ATaskARuleMadeAboutAMemberGoesWithThem_AndOneWhoseRunIsGoneLosesTheirName()
    {
        await using var app = new App();
        var (owner, _) = await ClubAsync(app);
        // A club record's task names its member ("About Anna Keller.") and is linked to no record
        // of theirs: a task has no member, and these two have no contact.
        await RuleAsync(owner, new { name = "Welcome call", trigger = "member.created", actions = new object[] { Task("Welcome call") } });
        var pack = await RuleAsync(owner, new { name = "Welcome pack", trigger = "member.created", actions = new object[] { Task("Send the welcome pack") } });
        async Task<Guid> Member(string first, string last, string email) => (await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = last, email, tier = "Gold", status = "Active",
        })), $"{first} joins")).GetProperty("id").GetGuid();
        var anna = await Member("Anna", "Keller", AnnaEmail);
        await Member("Bob", "Brown", "bob@example.ch");
        var tasks = await app.Services.Read(db => db.TaskItems.ToList());
        tasks.Should().HaveCount(4);
        tasks.Where(t => t.Description == "About Anna Keller.").Should().HaveCount(2).And.OnlyContain(t => t.ContactId == null && t.LeadId == null && t.DealId == null);
        // The second rule is deleted, and its runs and their logs with it: its task stays.
        (await owner.DeleteAsync($"/api/automations/{pack}")).IsSuccessStatusCode.Should().BeTrue();

        var records = await EverythingFoundAsync(owner, new { memberId = anna });
        var plan = await Json(await owner.PostAsync("/api/privacy/erasure/preview", Body(records)), "preview");
        var lines = plan.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("area").GetString()!, l => (l.GetProperty("action").GetString(), l.GetProperty("count").GetInt32()));
        lines["Tasks"].Should().Be(("delete", 1), "the welcome call, found through the log of the run that made it");
        lines["Tasks elsewhere that mention them"].Should().Be(("anonymise", 1), "the welcome pack, whose run is gone");
        records["confirm"] = "ERASE";
        await Json(await owner.PostAsync("/api/privacy/erasure", Body(records)), "erase Anna");

        var left = await app.Services.Read(db => db.TaskItems.ToList());
        left.Should().NotContain(t => (t.Description ?? "").Contains("Anna") || t.Title.Contains("Anna"));
        left.Should().ContainSingle(t => t.Description == "About Erased member 1.").Which.Title.Should().Be("Send the welcome pack");
        left.Where(t => t.Description == "About Bob Brown.").Select(t => t.Title).Should().BeEquivalentTo(["Welcome call", "Send the welcome pack"], "Bob's are his");
        (await app.Services.Read(db => db.AuditEvents.Any(e => (e.EntityLabel ?? "").Contains("Anna") || e.Summary.Contains("Anna Keller")))).Should().BeFalse(
            "a task's lines in the audit trail lose her name too");
    }

    [Fact]
    public async Task ARunSetOffForAMemberWhoseDataWasErasedStops_AndMakesNoTaskAboutThem()
    {
        await using var app = new App();
        var (owner, _) = await ClubAsync(app);
        await RuleAsync(owner, new { name = "New booking", trigger = "reservation.created", actions = new object[] { Task("Prepare the table") } });
        var jean = (await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
        })), "Jean joins")).GetProperty("id").GetGuid();
        var jeanRecords = await EverythingFoundAsync(owner, new { memberId = jean });
        jeanRecords["confirm"] = "ERASE";
        await Json(await owner.PostAsync("/api/privacy/erasure", Body(jeanRecords)), "erase Jean");

        // The member row stays for the books, under the pseudonym, and can still be picked: a
        // booking made for it by mistake sets the rule off.
        var court = (await Json(await owner.PostAsync("/api/resources", Body(new { name = "Court", kind = "Court", capacity = 4 })), "add a court")).GetProperty("id").GetString();
        var start = DateTime.UtcNow.AddDays(2);
        await Json(await owner.PostAsync("/api/reservations", Body(new { resourceId = court, memberId = jean, startAtUtc = start, endAtUtc = start.AddHours(1) })), "a booking for the erased member");

        (await app.Services.Read(db => db.TaskItems.Count(t => t.Title == "Prepare the table"))).Should().Be(0,
            "a task about someone the club no longer holds would be work about nobody");
        var run = await app.Services.Read(db => db.AutomationRuns.Single());
        run.Status.Should().Be(AutomationRunStatuses.Cancelled);
        run.LastError.Should().Contain("erased");
    }
}
