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
/// Lead routing, scoring, forms and sequences stay inside their workspace, and their
/// settings stay with the people allowed to change them.
/// </summary>
/// <remarks>
/// Every one of these was open before: a request with no workspace shared one bucket
/// with every other such user; an id from another workspace could be overwritten,
/// paused or deleted; and any member — a salesperson — could rewrite routing to send
/// every new lead to themselves.
/// </remarks>
public class LeadOpsIsolationTests
{
    // The bodies the real client sends (src/app/api/leadOps.ts), so these tests pin
    // the actual contract and not a convenient subset of it.
    private static StringContent Rule(object? id, string name, int priority, Guid[]? members = null) =>
        Body(new { id = id ?? Guid.Empty, name, kind = "RoundRobin", priority, memberUserIds = members ?? Array.Empty<Guid>(), isActive = true });

    private static StringContent Form(string name) =>
        Body(new { id = Guid.Empty, name, publicToken = "", isActive = true, publicPath = "" });

    [Fact]
    public async Task WithoutAWorkspaceThereIsNothingToReadOrWrite()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await SignInAsync(factory, "alone@example.ch");

        foreach (var path in new[] { "/api/lead-ops/forms", "/api/lead-ops/assignment", "/api/lead-ops/conversion", "/api/sequences", "/api/sequences/enrollments" })
        {
            var res = await client.GetAsync(path);
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{path} must not answer from a shared no-workspace bucket");
        }
        var write = await client.PostAsync("/api/lead-ops/assignment", Rule(null, "Mine", -100));
        write.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OnlyOwnersAndManagersChangeRouting()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Club Nautique");
        var (member, memberId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        var (manager, managerId) = await SignInAsync(factory, "lead@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);

        // A salesperson routing every new lead to themselves.
        var grab = await member.In(org).PostAsync("/api/lead-ops/assignment", Rule(null, "All mine", -100, new[] { memberId }));
        grab.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A capture form is a public door into the lead list: not theirs to open.
        (await member.In(org).PostAsync("/api/lead-ops/forms", Form("Website")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.In(org).PutAsync("/api/lead-ops/sla", Body(new { hours = 168 })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reading is fine for anyone in the workspace.
        (await member.In(org).GetAsync("/api/lead-ops/assignment")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Managers and owners can.
        (await manager.In(org).PostAsync("/api/lead-ops/assignment", Rule(null, "Round robin", 1)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.In(org).PutAsync("/api/lead-ops/sla", Body(new { hours = 6 })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnotherWorkspaceCannotOverwriteARuleById()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ownerA, idA) = await SignInAsync(factory, "a@example.ch");
        var orgA = await CreateAsync(factory, idA, "A");
        var (ownerB, idB) = await SignInAsync(factory, "b@example.ch");
        var orgB = await CreateAsync(factory, idB, "B");

        var created = await ownerA.In(orgA).PostAsync("/api/lead-ops/assignment", Rule(null, "A's routing", 1));
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var ruleId = doc.RootElement.GetProperty("id").GetGuid();

        // B posts with A's id. It used to update A's row in place.
        var hijack = await ownerB.In(orgB).PostAsync("/api/lead-ops/assignment", Rule(ruleId, "Hijacked", -100, new[] { idB }));
        hijack.StatusCode.Should().Be(HttpStatusCode.OK);
        using var hijackDoc = JsonDocument.Parse(await hijack.Content.ReadAsStringAsync());
        hijackDoc.RootElement.GetProperty("id").GetGuid().Should().NotBe(ruleId, "B gets a rule of its own, not A's");

        var aRules = await factory.Services.Read(db => db.LeadAssignmentRules.Where(r => r.OrganizationId == orgA).ToList());
        aRules.Should().ContainSingle().Which.Name.Should().Be("A's routing");
    }

    [Theory]
    [InlineData("territories")]
    [InlineData("queues")]
    public async Task AnotherWorkspaceCannotOverwriteATerritoryOrQueueById(string kind)
    {
        // These rely on the repository's lookup alone (no service-level check), so
        // this is what pins it: by id only, B's save rewrote A's row in place.
        using var factory = new CustomWebApplicationFactory();
        var (ownerA, idA) = await SignInAsync(factory, "a@example.ch");
        var orgA = await CreateAsync(factory, idA, "A");
        var (ownerB, idB) = await SignInAsync(factory, "b@example.ch");
        var orgB = await CreateAsync(factory, idB, "B");

        object Shape(Guid id, string name) => kind == "territories"
            ? new { id, name, countries = "CH", industries = "" }
            : new { id, name, memberUserIds = Array.Empty<Guid>() };

        var created = await ownerA.In(orgA).PostAsync($"/api/lead-ops/{kind}", Body(Shape(Guid.Empty, "A's own")));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        (await ownerB.In(orgB).PostAsync($"/api/lead-ops/{kind}", Body(Shape(id, "Hijacked"))))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var aNames = await factory.Services.Read(db => kind == "territories"
            ? db.LeadTerritories.Where(t => t.OrganizationId == orgA).Select(t => t.Name).ToList()
            : db.LeadQueues.Where(q => q.OrganizationId == orgA).Select(q => q.Name).ToList());
        aNames.Should().Equal("A's own");
    }

    [Fact]
    public async Task AnotherWorkspaceCannotPauseOrDeleteAnEnrollment()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, idA) = await SignInAsync(factory, "a@example.ch");
        var orgA = await CreateAsync(factory, idA, "A");
        var (ownerB, idB) = await SignInAsync(factory, "b@example.ch");
        var orgB = await CreateAsync(factory, idB, "B");

        var enrollmentId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OutreachEnrollments.Add(new OutreachEnrollment
            {
                Id = enrollmentId, SequenceId = Guid.NewGuid(), OrganizationId = orgA, EnrolledByUserId = idA,
                TargetType = "lead", TargetId = Guid.NewGuid(), TargetName = "A's prospect", Status = "active",
                EnrolledAtUtc = DateTime.UtcNow, NextActionAtUtc = DateTime.UtcNow.AddDays(1),
            });
            await db.SaveChangesAsync();
        }

        (await ownerB.In(orgB).PatchAsync($"/api/sequences/enrollments/{enrollmentId}", Body(new { status = "paused" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ownerB.In(orgB).DeleteAsync($"/api/sequences/enrollments/{enrollmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var still = await factory.Services.Read(db => db.OutreachEnrollments.Single(e => e.Id == enrollmentId));
        still.Status.Should().Be("active");
    }

    [Fact]
    public async Task PausingASequenceWorks()
    {
        // The page sends only { status }, and Name was required on every update —
        // so Pause / Activate was rejected with 400 every single time.
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Club");

        var created = await owner.In(org).PostAsync("/api/sequences", Body(new { name = "Welcome", status = "active", steps = Array.Empty<object>() }));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        var paused = await owner.In(org).PutAsync($"/api/sequences/{id}", Body(new { status = "paused" }));
        paused.StatusCode.Should().Be(HttpStatusCode.OK, await paused.Content.ReadAsStringAsync());
        using var after = JsonDocument.Parse(await paused.Content.ReadAsStringAsync());
        after.RootElement.GetProperty("status").GetString().Should().Be("paused");
        after.RootElement.GetProperty("name").GetString().Should().Be("Welcome", "a status change leaves the name alone");
    }

    [Fact]
    public async Task BulkUpdateDoesNotSetTheStatus()
    {
        // The status follows the pipeline. Setting it in bulk made the badge
        // disagree with the pipeline until the next edit overwrote it.
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Club");

        var res = await owner.In(org).PostAsync("/api/lead-ops/bulk", Body(new { ids = new[] { Guid.NewGuid() }, status = "Qualified" }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("follows its pipeline");
    }
}
