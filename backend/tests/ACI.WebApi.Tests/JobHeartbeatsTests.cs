using ACI.Application.Interfaces;
using ACI.WebApi.Monitoring;
using ACI.WebApi.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Tests;

/// <summary>A clock a test moves on, so three intervals of a job pass in a moment.</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public DateTime UtcNow => _now.UtcDateTime;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>
/// The background jobs' heartbeats: a job that stops, or keeps failing, is seen, instead of
/// living only in a log nobody reads.
/// </summary>
public class JobHeartbeatsTests
{
    private readonly ManualClock _clock = new();

    private JobHeartbeat Job(JobHeartbeats heartbeats, string name) => heartbeats.Snapshot().Single(j => j.Job == name);

    [Fact]
    public void AJobJustRegisteredIsNeitherSilentNorFailing()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));

        var job = Job(heartbeats, "outbox");
        job.RegisteredAtUtc.Should().Be(_clock.UtcNow);
        job.LastBeatAtUtc.Should().BeNull();
        job.IsSilent(_clock.UtcNow).Should().BeFalse();
        job.IsFailing.Should().BeFalse();
    }

    [Fact]
    public void AJobIsSilentOnceThreeOfItsIntervalsPassWithoutASignOfLife()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("retention", TimeSpan.FromHours(1));
        heartbeats.Beat("retention");

        _clock.Advance(TimeSpan.FromHours(3));
        Job(heartbeats, "retention").IsSilent(_clock.UtcNow).Should().BeFalse("three intervals exactly is still in time");
        _clock.Advance(TimeSpan.FromSeconds(1));
        Job(heartbeats, "retention").IsSilent(_clock.UtcNow).Should().BeTrue();

        heartbeats.Beat("retention");
        Job(heartbeats, "retention").IsSilent(_clock.UtcNow).Should().BeFalse("a run is a sign of life");
    }

    [Fact]
    public void AShortIntervalStillGetsAMinuteBeforeItIsSilent()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));
        heartbeats.Beat("outbox");

        _clock.Advance(TimeSpan.FromSeconds(50));
        Job(heartbeats, "outbox").IsSilent(_clock.UtcNow).Should().BeFalse("three times 15 s is 45 s, but a job gets at least a minute");
        _clock.Advance(TimeSpan.FromSeconds(11));
        Job(heartbeats, "outbox").IsSilent(_clock.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void AFailedRunIsASignOfLifeButMakesTheJobFailingUntilARunGoesWell()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("club-pos", TimeSpan.FromMinutes(1));
        heartbeats.Beat("club-pos");

        _clock.Advance(TimeSpan.FromMinutes(2));
        heartbeats.Failed("club-pos", new InvalidOperationException("The POS answered 500"));
        _clock.Advance(TimeSpan.FromMinutes(2));
        heartbeats.Failed("club-pos", new InvalidOperationException("The POS answered 500"));

        var failing = Job(heartbeats, "club-pos");
        failing.IsFailing.Should().BeTrue();
        failing.ConsecutiveFailures.Should().Be(2);
        failing.LastSeenAtUtc.Should().Be(_clock.UtcNow, "a failed run still says the loop turns");
        failing.IsSilent(_clock.UtcNow).Should().BeFalse();

        heartbeats.Beat("club-pos");
        var recovered = Job(heartbeats, "club-pos");
        recovered.IsFailing.Should().BeFalse();
        recovered.ConsecutiveFailures.Should().Be(0);
        recovered.LastError.Should().NotBeNull("the last error is kept for the System health page");
    }

    [Fact]
    public void TheErrorIsOneLineOfTypeAndMessageWithoutTheStack()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));
        Exception thrown;
        try
        {
            throw new AggregateException(new TimeoutException("The mail server did not answer\nsecond line"));
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        heartbeats.Failed("outbox", thrown);

        Job(heartbeats, "outbox").LastError.Should().Be("TimeoutException: The mail server did not answer");

        heartbeats.Failed("outbox", new Exception(new string('x', 1000)));
        Job(heartbeats, "outbox").LastError!.Length.Should().BeLessThanOrEqualTo(300);
    }

    [Fact]
    public void RegisteringAgainKeepsWhatTheJobHasDone()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("weekly-digest", TimeSpan.FromHours(1));
        heartbeats.Beat("weekly-digest");
        var beat = _clock.UtcNow;

        _clock.Advance(TimeSpan.FromMinutes(5));
        heartbeats.Register("weekly-digest", TimeSpan.FromHours(2));

        var job = Job(heartbeats, "weekly-digest");
        job.Interval.Should().Be(TimeSpan.FromHours(2));
        job.LastBeatAtUtc.Should().Be(beat);
    }

    [Fact]
    public void ABeatOrAFailureFromAJobNobodyRegisteredStillCounts()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Beat("unregistered");
        heartbeats.Failed("also-unregistered", new Exception("boom"));

        heartbeats.Snapshot().Select(j => j.Job).Should().Equal("also-unregistered", "unregistered");
        Job(heartbeats, "also-unregistered").IsFailing.Should().BeTrue();
    }

    [Fact]
    public void TheSnapshotIsInNameOrderAndNamesAreNotCaseSensitive()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("retention", TimeSpan.FromHours(1));
        heartbeats.Register("automations", TimeSpan.FromSeconds(30));
        heartbeats.Beat("Automations");

        heartbeats.Snapshot().Select(j => j.Job).Should().Equal("automations", "retention");
        Job(heartbeats, "automations").LastBeatAtUtc.Should().NotBeNull();
    }
}

/// <summary>"jobs" in /health: Unhealthy when a job is silent, Degraded when one is failing.</summary>
public class JobsHealthCheckTests
{
    private readonly ManualClock _clock = new();

    private async Task<HealthCheckResult> CheckAsync(JobHeartbeats heartbeats) =>
        await new JobsHealthCheck(heartbeats, _clock).CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task HealthyWhileEveryJobRunsAndSaysWhenEachWasLastSeen()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));
        heartbeats.Register("retention", TimeSpan.FromHours(1));
        _clock.Advance(TimeSpan.FromSeconds(10));
        heartbeats.Beat("outbox");
        heartbeats.Beat("retention");

        var result = await CheckAsync(heartbeats);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Keys.Should().BeEquivalentTo("outbox", "retention");
        var outbox = Json(result.Data["outbox"]);
        outbox.GetProperty("state").GetString().Should().Be("ok");
        outbox.GetProperty("lastSeenUtc").GetDateTime().Should().Be(_clock.UtcNow);
        outbox.GetProperty("failures").GetInt32().Should().Be(0);
        outbox.GetProperty("intervalSeconds").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task UnhealthyWhenAJobIsSilent()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));
        heartbeats.Register("retention", TimeSpan.FromHours(1));
        heartbeats.Beat("outbox");
        heartbeats.Beat("retention");

        _clock.Advance(TimeSpan.FromMinutes(5));
        heartbeats.Beat("retention");

        var result = await CheckAsync(heartbeats);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("outbox").And.NotContain("retention");
        Json(result.Data["outbox"]).GetProperty("state").GetString().Should().Be("silent");
        Json(result.Data["retention"]).GetProperty("state").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task DegradedWhenAJobsLastRunFailed()
    {
        var heartbeats = new JobHeartbeats(_clock);
        heartbeats.Register("club-billing", TimeSpan.FromHours(1));
        heartbeats.Register("outbox", TimeSpan.FromSeconds(15));
        heartbeats.Beat("outbox");
        heartbeats.Failed("club-billing", new InvalidOperationException("Login failed for user 'cadence'"));

        var result = await CheckAsync(heartbeats);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("club-billing");
        var billing = Json(result.Data["club-billing"]);
        billing.GetProperty("state").GetString().Should().Be("failing");
        billing.GetProperty("failures").GetInt32().Should().Be(1);
        System.Text.Json.JsonSerializer.Serialize(result.Data).Should().NotContain("Login failed",
            "the last error is for the System health page, not for an anonymous /health");
    }

    [Fact]
    public async Task AJobThatHasNotRunYetIsLeftAloneForTheStartupGrace()
    {
        var heartbeats = new JobHeartbeats(_clock);
        // The POS import waits four minutes before its first pass, then looks every minute.
        heartbeats.Register("club-pos", TimeSpan.FromMinutes(1));

        _clock.Advance(TimeSpan.FromMinutes(4));
        var waiting = await CheckAsync(heartbeats);
        waiting.Status.Should().Be(HealthStatus.Healthy);
        Json(waiting.Data["club-pos"]).GetProperty("state").GetString().Should().Be("waiting");

        _clock.Advance(JobsHealthCheck.StartupGrace);
        (await CheckAsync(heartbeats)).Status.Should().Be(HealthStatus.Unhealthy, "a job that never runs has stopped");
    }

    [Fact]
    public async Task HealthyWithNoJobsAtAll()
    {
        var result = await CheckAsync(new JobHeartbeats(_clock));
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().BeEmpty();
    }

    private static System.Text.Json.JsonElement Json(object value) =>
        System.Text.Json.JsonSerializer.SerializeToElement(value);
}
