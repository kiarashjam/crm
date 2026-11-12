using System.Net;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The language a club writes to a member in: their statements, reminders, welcome and event
/// emails. Kept on the member, set by staff, and followed by every email and PDF the member gets.
/// </summary>
public class MemberLanguageTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string? Language(JsonElement member) =>
        member.TryGetProperty("preferredLanguage", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static async Task<(HttpClient Owner, Guid Org)> ClubAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, org);
    }

    [Fact]
    public async Task StaffSetAMembersLanguage_ChangeIt_AndClearItToFollowTheWorkspace_AndOnlyOursAreTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _) = await ClubAsync(factory);

        var created = await Json(await owner.PostAsync("/api/members", Body(new
        {
            firstName = "Jeanne", lastName = "Dupont", email = "jeanne@example.ch", tier = "Gold", status = "Active", preferredLanguage = "fr",
        })), "add Jeanne in French");
        Language(created).Should().Be("fr");
        var id = created.GetProperty("id").GetGuid();

        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = "de-CH" })), "switch her to German"))
            .Should().Be("de", "a locale name is read as its language");
        // A save that leaves the language out keeps it.
        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { phone = "+41 79 123 45 67" })), "change her phone"))
            .Should().Be("de");
        Language(await Json(await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = (string?)null })), "clear it"))
            .Should().BeNull("an explicit null follows the workspace again");

        var refused = await owner.PutAsync($"/api/members/{id}", Body(new { preferredLanguage = "it" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());
        var list = await Json(await owner.GetAsync("/api/members"), "list the members");
        list.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id).TryGetProperty("preferredLanguage", out var kept)
            .Should().BeTrue();
        kept.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
