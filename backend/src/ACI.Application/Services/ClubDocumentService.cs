using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public interface IClubDocumentService
{
    Task<JsonArray> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default);
    Task<Result<JsonObject>> CreateAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> UpdateAsync(Guid? organizationId, string kind, Guid id, JsonObject patch, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct = default);
    Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<JsonObject> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default);
    Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
}

public sealed class ClubDocumentService : IClubDocumentService
{
    private const string SettingsKind = "hospitality-settings";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The kinds that are money. They live in the typed ledger (IClubLedger) since the
    /// AddClubMoneyLedger migration copied them there; their old documents stay, unread,
    /// until a later release removes them, and nothing here writes one.
    /// </summary>
    private static readonly HashSet<string> MoneyKinds = new(StringComparer.Ordinal)
    {
        "charges", "payments", "gift-cards", "gift-card-redemptions", "tabs", "subscriptions", "donations",
    };

    /// <summary>
    /// Events, their attendees, the bookable resources and their bookings: in typed tables
    /// (IClubEvents, IClubReservations) since the MoveEventsAndReservationsToTables migration
    /// copied them there. Their old documents stay, unread, and nothing here writes one.
    /// </summary>
    private static readonly HashSet<string> MovedKinds = new(StringComparer.Ordinal)
    {
        "events", "event-attendees", "resources", "reservations",
    };

    private readonly IClubDocumentRepository _docs;

    // The member list was only read to send a campaign and to keep a member's balance; both
    // moved out (CampaignService, IClubLedger), so the documents need nothing but their own store.
    public ClubDocumentService(IClubDocumentRepository docs)
    {
        _docs = docs;
    }

    public async Task<JsonArray> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default)
    {
        RefuseMoney(kind);
        var rows = await _docs.ListAsync(organizationId, kind, ct);
        var array = new JsonArray();
        foreach (var row in rows)
            if (Parse(row.Payload) is JsonObject obj)
                array.Add(obj);
        return array;
    }

    public async Task<Result<JsonObject>> CreateAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct = default)
    {
        RefuseMoney(kind);
        var prepared = Prepare(kind, body);
        if (!prepared.IsSuccess) return prepared;
        var obj = prepared.Value;
        return await WriteAsync(async () =>
        {
            await InsertAsync(organizationId, kind, obj, ct);
            return Result.Success(obj);
        }, ct);
    }

    public async Task<Result<JsonObject>> UpdateAsync(Guid? organizationId, string kind, Guid id, JsonObject patch, CancellationToken ct = default)
    {
        RefuseMoney(kind);
        var current = await Load(organizationId, kind, id, ct);
        if (current == null) return DomainErrors.Club.RecordNotFound;
        foreach (var pair in patch)
            current[pair.Key] = pair.Value?.DeepClone();
        current["id"] = id.ToString();
        return await WriteAsync(async () =>
        {
            await Save(organizationId, kind, id, current, ct);
            return Result.Success(current);
        }, ct);
    }

    public async Task<bool> DeleteAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct = default)
    {
        RefuseMoney(kind);
        var row = await Load(organizationId, kind, id, ct);
        if (row == null || !await _docs.DeleteAsync(id, organizationId, kind, ct)) return false;
        switch (kind)
        {
            case "households":
                // A household's member links go with it, as they did in the browser.
                foreach (var link in await _docs.ListAsync(organizationId, "household-members", ct))
                    if (Parse(link.Payload) is { } obj && Text(obj, "householdId") == id.ToString())
                        await _docs.DeleteAsync(link.Id, organizationId, "household-members", ct);
                break;
        }
        return true;
    }

    public async Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        return rows.Count == 0 ? new JsonObject() : Parse(rows[0].Payload) ?? new JsonObject();
    }

    public async Task<JsonObject> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default)
    {
        var current = await GetSettingsAsync(organizationId, ct);
        foreach (var pair in patch)
            current[pair.Key] = pair.Value?.DeepClone();
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        var id = rows.Count == 0 ? Guid.NewGuid() : rows[0].Id;
        await Save(organizationId, SettingsKind, id, current, ct);
        return current;
    }

    public async Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        foreach (var row in rows)
            await _docs.DeleteAsync(row.Id, organizationId, SettingsKind, ct);
        return new JsonObject();
    }

    /// <summary>A new record from the request body, checked and stamped but not yet saved.</summary>
    private static Result<JsonObject> Prepare(string kind, JsonObject body)
    {
        var obj = Clone(body);
        // The server picks the id. Saving looks a row up by id alone, so an id taken from
        // the request was a way to overwrite any record whose id someone had seen,
        // including another organization's.
        obj["id"] = Guid.NewGuid().ToString();
        if (Stamp(kind, obj) is { } invalid) return invalid;
        return obj;
    }

    /// <summary>Saves a prepared record and does what follows from it.</summary>
    private async Task InsertAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        await Save(organizationId, kind, ReadId(obj)!.Value, obj, ct);
        await AfterCreateAsync(organizationId, kind, obj, ct);
    }

    /// <summary>
    /// Fills in what the server owns and checks what the client sent.
    /// </summary>
    private static Error? Stamp(string kind, JsonObject obj)
    {
        var now = DateTime.UtcNow.ToString("o");
        switch (kind)
        {
            case "applications":
                Set(obj, "submittedAtUtc", now);
                Set(obj, "status", "Submitted");
                break;
            case "households":
                Set(obj, "createdAtUtc", now);
                break;
        }
        return null;
    }

    /// <summary>What the browser did after these writes, done here so every device sees it.</summary>
    private async Task AfterCreateAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        switch (kind)
        {
            case "households":
                // The primary member is the household's first link.
                if (Guid.TryParse(Text(obj, "primaryMemberId"), out var primary))
                {
                    await CreateAsync(organizationId, "household-members", new JsonObject
                    {
                        ["householdId"] = Text(obj, "id"),
                        ["memberId"] = primary.ToString(),
                        ["role"] = "Primary",
                        ["rollUpBilling"] = true,
                    }, ct);
                }
                break;
        }
    }

    /// <summary>
    /// Runs a write as one transaction. A save that lost a race to another request comes
    /// back as a 409 the screen can show, not a 500, and nothing of it is kept.
    /// </summary>
    private async Task<Result<T>> WriteAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct, Error? conflict = null)
    {
        try
        {
            return await _docs.InTransactionAsync(work, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return conflict ?? DomainErrors.Club.Changed;
        }
    }

    /// <summary>A money kind, or one moved to its own table, asked for here is a bug in the caller: those routes answer from the typed tables.</summary>
    private static void RefuseMoney(string kind)
    {
        if (MoneyKinds.Contains(kind))
            throw new InvalidOperationException($"Club {kind} are kept in the money ledger (IClubLedger), not as documents.");
        if (MovedKinds.Contains(kind))
            throw new InvalidOperationException($"Club {kind} are kept in their own tables (IClubEvents, IClubReservations), not as documents.");
    }

    private async Task<JsonObject?> Load(Guid? organizationId, string kind, Guid id, CancellationToken ct)
    {
        var row = await _docs.GetAsync(id, organizationId, kind, ct);
        return row == null ? null : Parse(row.Payload);
    }

    private Task Save(Guid? organizationId, string kind, Guid id, JsonObject obj, CancellationToken ct) =>
        _docs.SaveAsync(new ClubDocument
        {
            Id = id,
            OrganizationId = organizationId,
            Kind = kind,
            Payload = obj.ToJsonString(Json),
            UpdatedAtUtc = DateTime.UtcNow,
        }, ct);

    private static JsonObject Clone(JsonObject source)
    {
        var copy = new JsonObject();
        foreach (var pair in source)
            copy[pair.Key] = pair.Value?.DeepClone();
        return copy;
    }

    private static JsonObject? Parse(string payload) => JsonNode.Parse(payload) as JsonObject;

    private static Guid? ReadId(JsonObject obj) =>
        Guid.TryParse(Text(obj, "id"), out var id) ? id : null;

    private static void Set(JsonObject obj, string key, JsonNode? value)
    {
        if (!obj.ContainsKey(key) || obj[key] == null)
            obj[key] = value;
    }

    private static string Text(JsonObject obj, string key) => obj[key]?.ToString() ?? "";
}
