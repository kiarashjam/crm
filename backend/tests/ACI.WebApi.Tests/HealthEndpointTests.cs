using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Monitoring;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Tests;

/// <summary>
/// /health tells the truth: start-up that failed, a background job that stopped or keeps
/// failing, and email stuck in the outbox, as JSON an anonymous caller may read; 503 when the
/// server cannot do its work.
/// </summary>
public class HealthEndpointTests
{
    /// <summary>
    /// A host whose jobs report to a registry on a clock the test moves, and whose /health runs
    /// only the checks named, when any are: every in-memory host fails start-up (it cannot run the
    /// start-up DDL), so a test of what another check says leaves "startup" out.
    /// </summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly ManualClock Clock = new();
        public readonly JobHeartbeats Heartbeats;
        private readonly string[] _only;

        /// <summary>Checks of the test's own, added after the filter, so they always run.</summary>
        public Action<IHealthChecksBuilder>? MoreChecks { get; init; }

        public App(params string[] only)
        {
            Heartbeats = new JobHeartbeats(Clock);
            _only = only;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IJobHeartbeats>(Heartbeats);
                if (_only.Length > 0)
                {
                    services.Configure<HealthCheckServiceOptions>(options =>
                    {
                        foreach (var other in options.Registrations.Where(r => !_only.Contains(r.Name)).ToList())
                            options.Registrations.Remove(other);
                    });
                }
                MoreChecks?.Invoke(services.AddHealthChecks());
            });
        }

        public async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> HealthAsync()
        {
            var res = await CreateClient().GetAsync("/health");
            var raw = await res.Content.ReadAsStringAsync();
            res.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            return (res.StatusCode, JsonDocument.Parse(raw).RootElement.Clone(), raw);
        }

        public async Task SeedAsync(Action<AppDbContext> seed)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }
    }

    private static JsonElement Check(JsonElement body, string name) =>
        body.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    [Fact]
    public async Task AnswersWithEveryCheckInOneShape()
    {
        using var app = new App(StartupHealthCheck.Name, JobsHealthCheck.Name, OutboxHealthCheck.Name);

        var (_, body, _) = await app.HealthAsync();

        body.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        body.GetProperty("totalDurationMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().BeEquivalentTo(StartupHealthCheck.Name, JobsHealthCheck.Name, OutboxHealthCheck.Name);
        foreach (var check in body.GetProperty("checks").EnumerateArray())
        {
            check.EnumerateObject().Select(p => p.Name).Should().Equal("name", "status", "description", "durationMs", "data");
            check.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Object);
        }
    }

    [Fact]
    public async Task Answers503WithStartupUnhealthyWhenStartUpFailed()
    {
        using var app = new App(StartupHealthCheck.Name, JobsHealthCheck.Name);
        app.Services.GetRequiredService<StartupState>().MarkDatabaseFailed(DateTime.UtcNow);

        var (status, body, raw) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.GetProperty("status").GetString().Should().Be("Unhealthy");
        var startup = Check(body, StartupHealthCheck.Name);
        startup.GetProperty("status").GetString().Should().Be("Unhealthy");
        startup.GetProperty("description").GetString().Should().Contain("failed");
        startup.GetProperty("data").TryGetProperty("failedAtUtc", out _).Should().BeTrue();
        raw.Should().NotContain("Exception").And.NotContain("Microsoft.", "the error itself is for the log");
    }

    [Fact]
    public async Task TheTestHostsOwnFailedStartUpIsReportedToo()
    {
        // Every in-memory host fails the start-up DDL, exactly as a SQL outage at boot would;
        // Program.cs marks the state where it keeps the error for /db-status.
        using var app = new App(StartupHealthCheck.Name);

        var (status, body, _) = await app.HealthAsync();

        app.Services.GetRequiredService<StartupState>().DatabaseFailed.Should().BeTrue();
        status.Should().Be(HttpStatusCode.ServiceUnavailable);
        Check(body, StartupHealthCheck.Name).GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    [Fact]
    public async Task ASilentJobMakesJobsDegraded_StillAnswering200_SoAppServiceDoesNotRestartMidRun()
    {
        using var app = new App(JobsHealthCheck.Name);
        app.Heartbeats.Register(OutboxDispatcherBackgroundService.JobName, TimeSpan.FromSeconds(15));
        app.Heartbeats.Register(RetentionBackgroundService.JobName, TimeSpan.FromHours(1));
        app.Heartbeats.Beat(OutboxDispatcherBackgroundService.JobName);
        app.Heartbeats.Beat(RetentionBackgroundService.JobName);

        var (fine, _, _) = await app.HealthAsync();
        fine.Should().Be(HttpStatusCode.OK);

        app.Clock.Advance(TimeSpan.FromMinutes(11));
        app.Heartbeats.Beat(RetentionBackgroundService.JobName);
        var (status, body, _) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.OK, "an Unhealthy /health has App Service restart the instance; a stopped job is alerted on instead");
        var jobs = Check(body, JobsHealthCheck.Name);
        jobs.GetProperty("status").GetString().Should().Be("Degraded");
        jobs.GetProperty("description").GetString().Should().Contain("outbox");
        var outbox = jobs.GetProperty("data").GetProperty("outbox");
        outbox.GetProperty("state").GetString().Should().Be("silent");
        outbox.GetProperty("lastSeenUtc").GetDateTime().Should().Be(app.Clock.UtcNow - TimeSpan.FromMinutes(11));
        outbox.GetProperty("failures").GetInt32().Should().Be(0);
        jobs.GetProperty("data").GetProperty("retention").GetProperty("state").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task AFailingJobMakesJobsDegradedWhichStillAnswers200()
    {
        using var app = new App(JobsHealthCheck.Name);
        app.Heartbeats.Register(ClubPosBackgroundService.JobName, TimeSpan.FromMinutes(1));
        app.Heartbeats.Failed(ClubPosBackgroundService.JobName,
            new InvalidOperationException("Login failed for user 'cadence'. Password=hunter2"));

        var (status, body, raw) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Degraded");
        var jobs = Check(body, JobsHealthCheck.Name);
        jobs.GetProperty("status").GetString().Should().Be("Degraded");
        jobs.GetProperty("data").GetProperty("club-pos").GetProperty("failures").GetInt32().Should().Be(1);
        raw.Should().NotContain("hunter2").And.NotContain("Login failed");
    }

    [Fact]
    public async Task AnEmailQueuedForMoreThanHalfAnHourMakesTheOutboxDegraded()
    {
        using var app = new App(OutboxHealthCheck.Name);
        var now = app.Clock.UtcNow;
        var workspace = Guid.NewGuid();
        await app.SeedAsync(db =>
        {
            db.Organizations.Add(new Organization { Id = workspace, Name = "Zebra Holdings Secret", OwnerUserId = Guid.NewGuid(), CreatedAtUtc = now });
            // Waiting two hours: due then, not yet sent.
            db.OutboundMessages.Add(Email(workspace, OutboundStatuses.Queued, created: now.AddHours(-2), due: now.AddHours(-2)));
            // Fresh, and gone: neither is late.
            db.OutboundMessages.Add(Email(workspace, OutboundStatuses.Queued, created: now.AddMinutes(-5), due: now.AddMinutes(-5)));
            db.OutboundMessages.Add(Email(workspace, OutboundStatuses.Sent, created: now.AddHours(-3), due: null));
            // A newsletter's email set for later: queued long ago, not due yet, so not late.
            db.OutboundMessages.Add(Email(workspace, OutboundStatuses.Queued, created: now.AddHours(-2), due: now.AddHours(1)));
        });

        var (status, body, raw) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.OK);
        var outbox = Check(body, OutboxHealthCheck.Name);
        outbox.GetProperty("status").GetString().Should().Be("Degraded");
        outbox.GetProperty("description").GetString().Should().Contain("1 email has waited more than 30 minutes");
        var data = outbox.GetProperty("data");
        data.GetProperty("queuedEmails").GetInt32().Should().Be(2);
        data.GetProperty("lateEmails").GetInt32().Should().Be(1);
        data.GetProperty("oldestQueuedMinutes").GetInt32().Should().Be(120);
        data.GetProperty("failedLast24h").GetInt32().Should().Be(0);
        raw.Should().NotContain("Zebra").And.NotContain("@example.com", "/health is anonymous");
    }

    [Fact]
    public async Task AMessageGivenUpOnInTheLastDayMakesTheOutboxDegraded()
    {
        using var app = new App(OutboxHealthCheck.Name);
        var now = app.Clock.UtcNow;
        await app.SeedAsync(db =>
        {
            db.OutboundMessages.Add(Email(Guid.NewGuid(), OutboundStatuses.Failed, created: now.AddHours(-3), due: null));
            db.OutboundMessages.Add(Email(Guid.NewGuid(), OutboundStatuses.Failed, created: now.AddDays(-3), due: null));
        });

        var (_, body, _) = await app.HealthAsync();

        var outbox = Check(body, OutboxHealthCheck.Name);
        outbox.GetProperty("status").GetString().Should().Be("Degraded");
        outbox.GetProperty("data").GetProperty("failedLast24h").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AnOutboxThatKeepsUpIsHealthy()
    {
        using var app = new App(OutboxHealthCheck.Name);
        var now = app.Clock.UtcNow;
        await app.SeedAsync(db =>
        {
            db.OutboundMessages.Add(Email(Guid.NewGuid(), OutboundStatuses.Queued, created: now.AddMinutes(-10), due: now.AddMinutes(-10)));
            db.OutboundMessages.Add(Email(Guid.NewGuid(), OutboundStatuses.Sent, created: now.AddHours(-1), due: null));
        });

        var (status, body, _) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.OK);
        var outbox = Check(body, OutboxHealthCheck.Name);
        outbox.GetProperty("status").GetString().Should().Be("Healthy");
        outbox.GetProperty("data").GetProperty("oldestQueuedMinutes").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task TheAnswerCarriesNoSecretsEvenWhenAChecksErrorWouldHave()
    {
        // A failing check's error is where a connection string turns up. The SQL check returns
        // its exception (no description); a check that throws gets the exception's message as
        // its description from the health check service. Both shapes, with a secret in each.
        // (The real SQL check is left out only because it waits out its connect timeout here.)
        const string Secret = "Server=tcp:prod.example;User ID=cadence;Password=hunter2";
        using var app = new App(StartupHealthCheck.Name, JobsHealthCheck.Name, OutboxHealthCheck.Name)
        {
            MoreChecks = checks => checks
                .AddCheck("returns-its-error", () => HealthCheckResult.Unhealthy(Secret, new InvalidOperationException(Secret), new Dictionary<string, object> { ["connection"] = Secret }))
                .AddCheck("throws", () => throw new InvalidOperationException(Secret)),
        };
        app.Heartbeats.Register(OutboxDispatcherBackgroundService.JobName, TimeSpan.FromSeconds(15));
        app.Heartbeats.Failed(OutboxDispatcherBackgroundService.JobName, new Exception(Secret));

        var (status, body, raw) = await app.HealthAsync();

        status.Should().Be(HttpStatusCode.ServiceUnavailable);
        foreach (var failed in new[] { "returns-its-error", "throws" })
        {
            var check = Check(body, failed);
            check.GetProperty("status").GetString().Should().Be("Unhealthy");
            check.GetProperty("description").GetString().Should().Be(HealthResponse.FailedWithoutDetails);
            check.GetProperty("data").EnumerateObject().Should().BeEmpty();
        }
        foreach (var secret in new[] { "Data Source", ":memory:", "Password", "hunter2", "Server=", "User ID", "Exception", "SqlClient", "   at " })
            raw.Should().NotContain(secret);
    }

    private static OutboundMessage Email(Guid workspace, string status, DateTime created, DateTime? due) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = workspace,
        Channel = OutboundChannels.Email,
        Source = OutboundSources.Composer,
        Status = status,
        To = "lead@example.com",
        Subject = "Hello",
        CreatedAtUtc = created,
        NextAttemptAtUtc = due,
        SentAtUtc = status == OutboundStatuses.Sent ? created : null,
    };
}
