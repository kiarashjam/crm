using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An import saves how far it has got after every run of rows, not only at the end.
/// </summary>
/// <remarks>
/// The batch used to be written twice: when the import started and when it finished. An
/// import whose leads each send an email can run far longer than the ten minutes after
/// which one with no end is taken to have stopped, so it was called stopped, and offered
/// for undoing, while it was still adding rows; and one the server did stop showed none of
/// the rows it had saved.
/// </remarks>
public class ImportProgressTests
{
    /// <summary>A batch as it was each time the import saved it.</summary>
    private sealed record Saved(DateTime? LastSavedAtUtc, DateTime? FinishedAtUtc, int Created);

    /// <summary>The app with an import repository that notes every save of a batch.</summary>
    private sealed class Recording : CustomWebApplicationFactory
    {
        public readonly List<Saved> Saves = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.AddScoped<IImportRepository>(sp => new RecordingImports(ActivatorUtilities.CreateInstance<ImportRepository>(sp), Saves)));
        }
    }

    private sealed class RecordingImports(IImportRepository inner, List<Saved> saves) : IImportRepository
    {
        public Task SaveBatchAsync(ImportBatch batch, CancellationToken ct = default)
        {
            lock (saves) saves.Add(new Saved(batch.LastSavedAtUtc, batch.FinishedAtUtc, batch.CreatedCount));
            return inner.SaveBatchAsync(batch, ct);
        }

        public Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default) => inner.AddBatchAsync(batch, ct);
        public Task<ImportBatch?> FindBatchAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
            inner.FindBatchAsync(id, userId, organizationId, ct);
        public Task<IReadOnlyList<(ImportBatch Batch, string ImportedBy, int Remaining)>> ListBatchesAsync(
            Guid userId, Guid? organizationId, string? entityType, int take, CancellationToken ct = default) =>
            inner.ListBatchesAsync(userId, organizationId, entityType, take, ct);
        public Task<IReadOnlyList<Lead>> LeadsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
            inner.LeadsAsync(userId, organizationId, ct);
        public Task<IReadOnlyList<Contact>> ContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
            inner.ContactsAsync(userId, organizationId, ct);
        public Task<IReadOnlyList<Company>> CompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
            inner.CompaniesAsync(userId, organizationId, ct);
        public Task<IReadOnlyDictionary<Guid, string>> CustomValuesAsync(string entityType, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
            inner.CustomValuesAsync(entityType, userId, organizationId, ct);
        public IDisposable StampNewRecords(Guid batchId) => inner.StampNewRecords(batchId);
        public void Forget() => inner.Forget();
        public Task InTransactionAsync(Func<Task> work, CancellationToken ct = default) => inner.InTransactionAsync(work, ct);
        public Task<ImportUndoPlan> PlanUndoAsync(ImportBatch batch, CancellationToken ct = default) => inner.PlanUndoAsync(batch, ct);
        public Task RemoveAsync(ImportBatch batch, ImportUndoPlan plan, CancellationToken ct = default) => inner.RemoveAsync(batch, plan, ct);
    }

    [Fact]
    public async Task ALongImportSavesHowFarItHasGotAfterEachRunOfRows()
    {
        await using var factory = new Recording();
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        // 120 rows: runs of 50, 50 and 20.
        var csv = "name,email\n" + string.Concat(Enumerable.Range(0, 120).Select(i => $"Person {i},p{i}@bluewin.ch\n"));

        var result = await OkJson(await Import(client, "lead", csv, new[] { "name", "email" }), "import");
        result.GetProperty("created").GetInt32().Should().Be(120);

        List<Saved> saves;
        lock (factory.Saves) saves = factory.Saves.ToList();
        var progress = saves.Where(s => s.FinishedAtUtc == null).ToList();
        progress.Select(s => s.Created).Should().Equal(new[] { 50, 100, 120 }, "each run of rows is counted once it is saved");
        progress.Should().OnlyContain(s => s.LastSavedAtUtc != null);
        saves.Last().Should().Match<Saved>(s => s.FinishedAtUtc != null && s.Created == 120);
    }
}
