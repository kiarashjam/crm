using System.Text.Json.Nodes;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What every merge of two records shares: it runs as one unit, and the files and
/// custom field values of the record folded in move to the record kept.
/// </summary>
/// <remarks>
/// <para>
/// Attachments and custom field values are keyed by record type and id rather than
/// by a foreign key, so nothing re-points them on its own, and deleting the record
/// folded in would take them with it (<see cref="RecordDependents"/>). They have to
/// be moved — and saved — before that record goes.
/// </para>
/// <para>
/// A merge therefore saves twice: once with everything moved to the record kept,
/// then once with the other record removed. On a relational database both saves are
/// in one transaction, so a merge that fails half way leaves both records as they
/// were.
/// </para>
/// </remarks>
public static class RecordMerge
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committed only when it returns true.</summary>
    internal static async Task<bool> InTransactionAsync(AppDbContext db, Func<Task<bool>> work, CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction != null)
            return await work();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var done = await work();
        if (done) await transaction.CommitAsync(ct);
        else await transaction.RollbackAsync(ct);
        return done;
    }

    /// <summary>
    /// Moves the files of <paramref name="dropId"/> to <paramref name="keepId"/> and
    /// folds its custom field values into the kept record's: a value the kept record
    /// already has stays, and an empty one is filled in.
    /// </summary>
    internal static async Task MoveDependentsAsync(AppDbContext db, string entityType, Guid keepId, Guid dropId, CancellationToken ct)
    {
        // Re-pointed without reading the bytes. The in-memory provider used by the
        // tests cannot run a bulk update, and there the files are small.
        if (db.Database.IsRelational())
        {
            await db.Attachments
                .Where(a => a.EntityType == entityType && a.RecordId == dropId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.RecordId, keepId), ct);
        }
        else
        {
            foreach (var attachment in await db.Attachments.Where(a => a.EntityType == entityType && a.RecordId == dropId).ToListAsync(ct))
                attachment.RecordId = keepId;
        }

        var sets = await db.CustomFieldValues
            .Where(v => v.EntityType == entityType && (v.RecordId == keepId || v.RecordId == dropId))
            .ToListAsync(ct);
        var keep = sets.FirstOrDefault(v => v.RecordId == keepId);
        var drop = sets.FirstOrDefault(v => v.RecordId == dropId);
        if (drop == null) return;
        if (keep == null)
        {
            drop.RecordId = keepId;
            drop.UpdatedAtUtc = DateTime.UtcNow;
            return;
        }
        keep.ValuesJson = FoldValues(keep.ValuesJson, drop.ValuesJson);
        keep.UpdatedAtUtc = DateTime.UtcNow;
        db.CustomFieldValues.Remove(drop);
    }

    /// <summary>
    /// Moves outreach enrolments to the record kept. One enrolment per sequence: where
    /// both were enrolled in the same one, the kept record's own stays and the copy goes.
    /// </summary>
    internal static async Task MoveEnrollmentsAsync(AppDbContext db, string targetType, Guid keepId, Guid dropId, CancellationToken ct)
    {
        var enrolments = await db.OutreachEnrollments
            .Where(e => e.TargetType == targetType && (e.TargetId == keepId || e.TargetId == dropId))
            .ToListAsync(ct);
        var keptSequences = enrolments.Where(e => e.TargetId == keepId).Select(e => e.SequenceId).ToHashSet();
        foreach (var enrolment in enrolments.Where(e => e.TargetId == dropId))
        {
            if (keptSequences.Add(enrolment.SequenceId)) enrolment.TargetId = keepId;
            else db.OutreachEnrollments.Remove(enrolment);
        }
    }

    /// <summary>The kept values, with any the kept record left empty taken from the other.</summary>
    public static string FoldValues(string keepJson, string dropJson)
    {
        var keep = ParseObject(keepJson);
        var drop = ParseObject(dropJson);
        foreach (var (key, value) in drop.ToList())
        {
            if (IsEmpty(value) || !IsEmpty(keep[key])) continue;
            keep[key] = value?.DeepClone();
        }
        return keep.ToJsonString();
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            return new JsonObject();
        }
    }

    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s),
        JsonArray a => a.Count == 0,
        _ => false,
    };

    /// <summary>The kept text, or the other when the kept one is blank.</summary>
    public static string? Fill(string? keep, string? drop) =>
        string.IsNullOrWhiteSpace(keep) && !string.IsNullOrWhiteSpace(drop) ? drop : keep;

    /// <summary>The earlier of two times, ignoring a missing one.</summary>
    public static DateTime? Earliest(DateTime? a, DateTime? b) =>
        a == null ? b : b == null ? a : (a < b ? a : b);

    /// <summary>The later of two times, ignoring a missing one.</summary>
    public static DateTime? Latest(DateTime? a, DateTime? b) =>
        a == null ? b : b == null ? a : (a > b ? a : b);
}
