using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Logging a call, meeting or email with someone stops the outreach still scheduled to
/// them; the sequence's own sends, which are logged as automated, do not.
/// </summary>
public class SequenceReplyStopTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid LeadId, Guid EnrollmentId)> EnrolledLeadAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "kia@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var sequence = await Json(await client.PostAsync("/api/sequences", Body(new
        {
            name = "Welcome", status = "active",
            steps = new[] { new { type = "email", dayOffset = 2, subject = "Hello", body = "Hi there" } },
        })), "create sequence");
        var lead = await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead");
        var leadId = lead.GetProperty("id").GetGuid();
        var enrollment = await Json(await client.PostAsync("/api/sequences/enroll", Body(new
        {
            sequenceId = sequence.GetProperty("id").GetGuid(), targetType = "lead", targetId = leadId, targetName = "Jean Dupont",
        })), "enroll");
        return (client, leadId, enrollment.GetProperty("id").GetGuid());
    }

    private static async Task<string?> StatusAsync(HttpClient client, Guid enrollmentId) =>
        (await Json(await client.GetAsync("/api/sequences/enrollments"), "list enrollments")).EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == enrollmentId).GetProperty("status").GetString();

    [Fact]
    public async Task ALoggedCallStopsTheSequence()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, leadId, enrollmentId) = await EnrolledLeadAsync(factory);

        await Json(await client.PostAsync("/api/activities", Body(new { type = "call", subject = "Talked about the tour", leadId })), "log call");

        (await StatusAsync(client, enrollmentId)).Should().Be("stopped", "the next automated email must not go out after they replied");
    }

    [Fact]
    public async Task ANoteDoesNot()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, leadId, enrollmentId) = await EnrolledLeadAsync(factory);

        await Json(await client.PostAsync("/api/activities", Body(new { type = "note", subject = "Prefers mornings", leadId })), "log note");

        (await StatusAsync(client, enrollmentId)).Should().Be("active");
    }

    [Fact]
    public async Task AClientCannotPassItsEmailOffAsTheSequencesOwnSend()
    {
        // The sequence marks its own sends automated so they don't stop it. That flag is
        // not read from the request, so a logged email from a person always counts.
        using var factory = new CustomWebApplicationFactory();
        var (client, leadId, enrollmentId) = await EnrolledLeadAsync(factory);

        await Json(await client.PostAsync("/api/activities", Body(new { type = "email", subject = "Re: Hello", leadId, isAutomated = true })), "log email");

        (await StatusAsync(client, enrollmentId)).Should().Be("stopped");
    }
}
