using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>What every test host leaves out of the app it starts.</summary>
public static class TestHosts
{
    /// <summary>
    /// Takes out the app's own background jobs (the <see cref="BackgroundService"/>s in
    /// ACI.WebApi.Services), but for <paramref name="keep"/>. The host's own services (the
    /// server itself is one) stay.
    /// </summary>
    public static void RemoveBackgroundJobs(IServiceCollection services, IReadOnlyCollection<Type> keep)
    {
        var jobs = services
            .Where(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType is { } type
                && typeof(BackgroundService).IsAssignableFrom(type)
                && type.Namespace == "ACI.WebApi.Services"
                && !keep.Contains(type))
            .ToList();
        foreach (var job in jobs) services.Remove(job);
    }
}
