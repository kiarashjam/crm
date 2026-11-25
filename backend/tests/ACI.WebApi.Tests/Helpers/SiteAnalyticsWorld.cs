using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The API with platform admins configured (Platform:AdminEmails) and a mail server that keeps
/// what it is handed, for the site analytics' tests.
/// </summary>
public sealed class AnalyticsApp : CustomWebApplicationFactory
{
    public const string AdminEmail = "owner@cadencee.example";

    /// <summary>The second admin, written in another case than the account's to show the match ignores case.</summary>
    public const string SecondAdminConfigured = "Second.Admin@CadenceE.example";

    public const string FrontendBaseUrl = "https://www.cadencee.example";

    private readonly string _admins;

    public AnalyticsApp(string? admins = null) => _admins = admins ?? $"{AdminEmail}, {SecondAdminConfigured}";

    public MailServer Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Platform:AdminEmails"] = _admins,
            ["Email:FrontendBaseUrl"] = FrontendBaseUrl,
        }));
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Mail.Mock.Object));
    }
}

/// <summary>Browsers, posts and seeded events for the site analytics' tests.</summary>
public static class SiteAnalyticsWorld
{
    public const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    public const string IPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1";
    public const string Googlebot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    public const string Endpoint = "/api/site-analytics/events";

    /// <summary>A client posting as a browser at <paramref name="address"/> (the proxy's forwarded address, as Azure's front end sends it).</summary>
    public static HttpClient Browser(this CustomWebApplicationFactory app, string userAgent = Chrome, string address = "203.0.113.7")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.UserAgent.Clear();
        if (!string.IsNullOrEmpty(userAgent)) client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        client.DefaultRequestHeaders.Add("Origin", "https://www.cadencee.ai");
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-CH"));
        return client;
    }

    /// <summary>What sendBeacon sends: JSON as text/plain.</summary>
    public static StringContent Beacon(object body, string contentType = "text/plain") =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, contentType);

    public static StringContent Events(params object[] events) => Beacon(new { events });

    public static async Task<List<SiteEvent>> SiteEventsAsync(this IServiceProvider services) =>
        await services.Read(db => db.SiteEvents.AsNoTracking().OrderBy(e => e.Id).ToList());

    public static async Task<List<SiteBotHit>> BotHitsAsync(this IServiceProvider services) =>
        await services.Read(db => db.SiteBotHits.AsNoTracking().OrderBy(h => h.Bot).ToList());

    /// <summary>Writes events as they would be stored, for the reads to count.</summary>
    public static async Task SeedAsync(this CustomWebApplicationFactory app, params SiteEvent[] events)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SiteEvents.AddRange(events);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// One stored event at <paramref name="at"/> (UTC), on its Zurich day, for visitor <paramref name="visitor"/>
    /// in session <paramref name="session"/>.
    /// </summary>
    public static SiteEvent Event(DateTime at, string visitor, string session, string type = SiteEventTypes.PageView, string path = "/",
        string? name = null, string source = "direct", string? referrer = null, string? campaign = null, string? utmSource = null,
        string? utmMedium = null, string device = "desktop", string browser = "Chrome", string os = "Windows", string? language = "de",
        bool signedIn = false) => new()
    {
        OccurredAtUtc = DateTime.SpecifyKind(at, DateTimeKind.Utc),
        Day = SiteAnalyticsRules.DayOf(at),
        Type = type,
        Name = name,
        Path = path,
        Source = source,
        ReferrerHost = referrer,
        UtmCampaign = campaign,
        UtmSource = utmSource,
        UtmMedium = utmMedium,
        VisitorHash = visitor.PadRight(32, '0'),
        SessionKey = session.PadRight(32, '0'),
        Device = device,
        Browser = browser,
        Os = os,
        Language = language,
        SignedIn = signedIn,
    };

    /// <summary>Noon in Zurich on <paramref name="day"/>, as UTC, plus <paramref name="minutes"/>.</summary>
    public static DateTime NoonOf(DateOnly day, int minutes = 0) =>
        RegionalNoon(day).AddMinutes(minutes);

    private static DateTime RegionalNoon(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified), SiteAnalyticsRules.Zone);

    /// <summary>Registers <paramref name="email"/> and marks the address confirmed, as opening the emailed link does.</summary>
    public static async Task<(HttpClient Client, Guid UserId)> ConfirmedAsync(this CustomWebApplicationFactory app, string email)
    {
        var (client, userId) = await Workspaces.SignInAsync(app, email);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.EmailVerifiedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (client, userId);
    }
}
