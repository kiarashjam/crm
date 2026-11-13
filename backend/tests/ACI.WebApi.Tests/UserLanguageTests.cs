using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The language a person chose for the app is kept on their account, so it follows them to
/// every browser they sign in from; until they choose, the app follows their workspace.
/// </summary>
public class UserLanguageTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string? Language(JsonElement e) =>
        e.TryGetProperty("language", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [Fact]
    public async Task ANewAccountHasNoLanguage_AChoiceIsKeptForThatPersonAlone_AndClearingItFollowsTheWorkspaceAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ada, _) = await SignInAsync(factory, "ada@example.ch");
        var (grace, _) = await SignInAsync(factory, "grace@example.ch");

        Language(await Json(await ada.GetAsync("/api/settings/language"))).Should().BeNull("nobody has chosen yet");

        Language(await Json(await ada.PutAsync("/api/settings/language", Body(new { language = "fr" })))).Should().Be("fr");
        Language(await Json(await ada.GetAsync("/api/settings/language"))).Should().Be("fr");
        Language(await Json(await grace.GetAsync("/api/settings/language"))).Should().BeNull("one person's choice is theirs alone");

        // A locale name is read as its language.
        Language(await Json(await ada.PutAsync("/api/settings/language", Body(new { language = "DE-ch" })))).Should().Be("de");

        // Resetting Settings leaves it: the language lives on the account.
        (await ada.PostAsync("/api/settings/reset", null)).IsSuccessStatusCode.Should().BeTrue();
        Language(await Json(await ada.GetAsync("/api/settings/language"))).Should().Be("de");

        Language(await Json(await ada.PutAsync("/api/settings/language", Body(new { language = (string?)null })))).Should().BeNull();
        Language(await Json(await ada.GetAsync("/api/settings/language"))).Should().BeNull();
    }

    [Fact]
    public async Task ALanguageTheAppIsNotWrittenInIsRefused_AndNobodySignedOutCanAskOrSet()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ada, _) = await SignInAsync(factory, "ada@example.ch");

        var refused = await ada.PutAsync("/api/settings/language", Body(new { language = "it" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());
        Language(await Json(await ada.GetAsync("/api/settings/language"))).Should().BeNull();

        var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/settings/language")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsync("/api/settings/language", Body(new { language = "fr" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task TheMigrationCopiesGermanAndFrenchChosenInTheOldProfileSelect_LeavesTheEnglishDefault_AndASecondRunChangesNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var ours = migrations.Single(m => m.EndsWith("_AddLanguagesAndCsvSeparator", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(ours) - 1]);

            async Task User(Guid id, string? language)
            {
                await SqlServerApp.ExecuteAsync(database, $@"
                    INSERT INTO [Users] ([Id], [Name], [Email], [TwoFactorEnabled], [CreatedAtUtc])
                    VALUES ('{id}', N'Someone', N'{id:N}@example.ch', 0, SYSUTCDATETIME())");
                if (language != null)
                    await SqlServerApp.ExecuteAsync(database, $@"
                        INSERT INTO [UserSettings] ([UserId], [BrandName], [Language], [BrandTone], [Theme], [DataDensity],
                            [SidebarCollapsed], [ShowWelcomeBanner], [EmailNotificationsEnabled], [EmailOnNewLead], [EmailOnDealUpdate],
                            [EmailOnTaskDue], [EmailOnTeamMention], [InAppNotificationsEnabled], [InAppSoundEnabled],
                            [BrowserNotificationsEnabled], [WeeklyDigestEnabled], [ShowActivityStatus], [AllowAnalytics],
                            [CreatedAtUtc], [UpdatedAtUtc])
                        VALUES ('{id}', N'', N'{language}', 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1,
                            SYSUTCDATETIME(), SYSUTCDATETIME())");
            }
            var german = Guid.NewGuid();
            var french = Guid.NewGuid();
            var english = Guid.NewGuid();
            var spanish = Guid.NewGuid();
            var noSettings = Guid.NewGuid();
            await User(german, "de");
            await User(french, " FR ");
            await User(english, "en");
            await User(spanish, "es");
            await User(noSettings, null);

            await SqlServerSchema.MigrateAloneAsync(database);

            async Task<string?> LanguageOf(Guid id) => await SqlServerApp.ScalarAsync(database, $"SELECT [Language] FROM [Users] WHERE [Id] = '{id}'") as string;
            (await LanguageOf(german)).Should().Be("de");
            (await LanguageOf(french)).Should().Be("fr");
            (await LanguageOf(english)).Should().BeNull("English was everyone's default, so it says nothing about a choice");
            (await LanguageOf(spanish)).Should().BeNull("the app is not written in Spanish");
            (await LanguageOf(noSettings)).Should().BeNull();
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Language] FROM [UserSettings] WHERE [UserId] = '{german}'"))
                .Should().Be("de", "the old column is copied, not moved");

            // Someone who chose since keeps their choice when the copy runs again.
            await SqlServerApp.ExecuteAsync(database, $"UPDATE [Users] SET [Language] = N'en' WHERE [Id] = '{german}'");
            await SqlServerApp.ExecuteAsync(database, AddLanguagesAndCsvSeparator.CopyChosenLanguagesSql);
            (await LanguageOf(german)).Should().Be("en");
            (await LanguageOf(french)).Should().Be("fr");
            (await LanguageOf(english)).Should().BeNull();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
