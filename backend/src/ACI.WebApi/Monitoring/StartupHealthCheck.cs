using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// "startup": Unhealthy when this instance's start-up database initialisation failed
/// (<see cref="StartupState"/>). It says only that, and when: /health is anonymous, and the
/// error names the server and the login. It is in the log, and in /db-status in development.
/// </summary>
public sealed class StartupHealthCheck : IHealthCheck
{
    public const string Name = "startup";

    private readonly StartupState _state;

    public StartupHealthCheck(StartupState state) => _state = state;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_state.DatabaseFailedAtUtc is not { } failedAt)
            return Task.FromResult(HealthCheckResult.Healthy("Start-up database initialisation finished."));

        return Task.FromResult(HealthCheckResult.Unhealthy(
            "Start-up database initialisation failed; the app is running without it. The details are in the server log.",
            data: new Dictionary<string, object> { ["failedAtUtc"] = failedAt }));
    }
}
