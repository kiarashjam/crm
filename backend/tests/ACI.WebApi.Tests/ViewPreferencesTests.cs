using System.Net;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// How a person likes each list page shown is saved with their settings, so it is the
/// same in every browser they sign in from, and is theirs alone.
/// </summary>
public class ViewPreferencesTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task ViewsAreSavedForThePerson_AndSurviveOtherSettingsChangesAndAReset()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ada, _) = await SignInAsync(factory, "ada@example.ch");
        var (grace, _) = await SignInAsync(factory, "grace@example.ch");
        const string views = """{"v":1,"pages":{"leads":{"layout":"table","density":"compact","at":1}}}""";

        await Json(await ada.PutAsync("/api/settings", Body(new { viewPreferencesJson = views })));
        (await Json(await ada.GetAsync("/api/settings"))).GetProperty("viewPreferencesJson").GetString().Should().Be(views);

        // Saving something else leaves them alone.
        await Json(await ada.PutAsync("/api/settings", Body(new { timezone = "Europe/Zurich" })));
        (await Json(await ada.GetAsync("/api/settings"))).GetProperty("viewPreferencesJson").GetString().Should().Be(views);

        // So does resetting Settings: views are chosen on the pages, not there.
        (await ada.PostAsync("/api/settings/reset", null)).IsSuccessStatusCode.Should().BeTrue();
        (await Json(await ada.GetAsync("/api/settings"))).GetProperty("viewPreferencesJson").GetString().Should().Be(views);

        // Someone else's are their own.
        var theirs = await Json(await grace.GetAsync("/api/settings"));
        (theirs.TryGetProperty("viewPreferencesJson", out var v) ? v.GetString() : null).Should().BeNull();
    }

    [Fact]
    public async Task AnOversizedSaveIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ada, _) = await SignInAsync(factory, "ada@example.ch");
        var res = await ada.PutAsync("/api/settings", Body(new { viewPreferencesJson = new string('x', 4001) }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
