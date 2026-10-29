using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
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
/// "two redeems at once" would pass whether or not the code is safe.
/// </remarks>
public sealed class RaceFactory : CustomWebApplicationFactory
{
    /// <summary>Holds reads of <see cref="Kind"/> until <see cref="Readers"/> requests have made one. Off until armed.</summary>
    public ReadGate Gate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddScoped<IClubDocumentRepository>(sp =>
                new HeldReads(new ClubDocumentRepository(sp.GetRequiredService<AppDbContext>()), Gate)));
    }

    public sealed class ReadGate
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

        public async Task PassAsync(string kind)
        {
            if (Kind != kind) return;
            if (Interlocked.Increment(ref _arrived) >= Readers) _open.TrySetResult();
            await _open.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class HeldReads : IClubDocumentRepository
    {
        private readonly IClubDocumentRepository _inner;
        private readonly ReadGate _gate;

        public HeldReads(IClubDocumentRepository inner, ReadGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<ClubDocument?> GetAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default)
        {
            var row = await _inner.GetAsync(id, organizationId, kind, ct);
            await _gate.PassAsync(kind);
            return row;
        }

        public Task<IReadOnlyList<ClubDocument>> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default) => _inner.ListAsync(organizationId, kind, ct);
        public Task SaveAsync(ClubDocument document, CancellationToken ct = default) => _inner.SaveAsync(document, ct);
        public Task<bool> DeleteAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default) => _inner.DeleteAsync(id, organizationId, kind, ct);
        public Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct = default) => _inner.InTransactionAsync(work, ct);
    }
}
