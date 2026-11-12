using ACI.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The app, able to make two requests read the same club record before either of them
/// writes, so a race that is a matter of luck in production happens every time here.
/// </summary>
/// <remarks>
/// Without this the two requests usually run one after the other: the in-memory database
/// answers at once, so neither waits long enough for the other to catch up, and a test of
/// "two redeems at once" would pass whether or not the code is safe. The money ledger tells
/// <see cref="ILedgerReadObserver"/> when it has read what it is about to change; here that
/// waits at the gate.
/// </remarks>
public sealed class RaceFactory : CustomWebApplicationFactory
{
    /// <summary>Holds reads of <see cref="ReadGate.Kind"/> until <see cref="ReadGate.Readers"/> requests have made one. Off until armed.</summary>
    public ReadGate Gate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ILedgerReadObserver>(Gate));
    }

    public sealed class ReadGate : ILedgerReadObserver
    {
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public string? Kind { get; private set; }
        public int Readers { get; private set; }

        /// <summary>From now on, a read of <paramref name="kind"/> waits until <paramref name="readers"/> have read it.</summary>
        public void Arm(string kind, int readers = 2)
        {
            Kind = kind;
            Readers = readers;
        }

        public async Task ReadAsync(string kind, CancellationToken ct = default)
        {
            if (Kind != kind) return;
            if (Interlocked.Increment(ref _arrived) >= Readers) _open.TrySetResult();
            await _open.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
    }
}
