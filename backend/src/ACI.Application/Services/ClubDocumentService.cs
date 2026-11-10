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
    Task<JsonObject?> RsvpAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    /// <summary>The hospitality settings as the server applies them: what was saved, over the defaults.</summary>
    Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default);

    /// <summary>Saves the settings sent, checked first (400 with the reason); answers them as now applied.</summary>
    Task<Result<JsonObject>> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default);

    Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
}

public sealed class ClubDocumentService : IClubDocumentService
{
    private const string SettingsKind = ClubSettings.Kind;
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
        if (kind == "resources")
            await EnsureResourcesAsync(organizationId, ct);
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
        var prepared = await PrepareAsync(organizationId, kind, body, ct);
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
        if (kind == "reservations" && patch.ContainsKey("resourceId"))
            await NameResourceAsync(organizationId, current, ct);
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
            case "events":
                // Its guest list goes with it. Nothing shows a deleted event's attendees,
                // so their names and emails would only have stayed in the table.
                foreach (var attendee in await _docs.ListAsync(organizationId, "event-attendees", ct))
                    if (Parse(attendee.Payload) is { } obj && Text(obj, "eventId") == id.ToString())
                        await _docs.DeleteAsync(attendee.Id, organizationId, "event-attendees", ct);
                break;
        }
        return true;
    }

    public async Task<JsonObject?> RsvpAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var ev = await Load(organizationId, "events", id, ct);
        if (ev == null) return null;
        var registered = Int(ev, "registeredCount");
        var capacity = Int(ev, "capacity");
        if (capacity > 0 && registered >= capacity)
            ev["waitlistCount"] = Int(ev, "waitlistCount") + 1;
        else
            ev["registeredCount"] = registered + 1;
        await Save(organizationId, "events", id, ev, ct);
        return ev;
    }

    // The settings the server applies (points, billing) are read through ClubSettings, so
    // the page is answered with exactly those, the defaults filled in, rather than whatever
    // was last saved with the page left to guess the rest.
    public async Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default) =>
        ClubSettings.Effective(await StoredSettingsAsync(organizationId, ct));

    public async Task<Result<JsonObject>> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default)
    {
        // Checked before anything is kept: a multiplier of nothing or a grace period of -3
        // used to be saved and then quietly read as something else.
        if (ClubSettings.Validate(patch) is { } invalid) return invalid;
        var current = await StoredSettingsAsync(organizationId, ct) ?? new JsonObject();
        foreach (var pair in ClubSettings.Clean(patch))
        {
            // The billing switches and the points per venue are sets whose values are each
            // checked on their own: a save that sends some of them keeps the rest. Replaced
            // whole, a save of one switch turned automatic billing back off.
            if (pair.Key is ClubSettings.Billing or ClubSettings.VisitPoints
                && pair.Value is JsonObject sent && current[pair.Key] is JsonObject kept)
            {
                foreach (var (key, value) in sent) kept[key] = value?.DeepClone();
                continue;
            }
            current[pair.Key] = pair.Value?.DeepClone();
        }
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        var id = rows.Count == 0 ? Guid.NewGuid() : rows[0].Id;
        await Save(organizationId, SettingsKind, id, current, ct);
        return ClubSettings.Effective(current);
    }

    public async Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        foreach (var row in rows)
            await _docs.DeleteAsync(row.Id, organizationId, SettingsKind, ct);
        return ClubSettings.Effective(null);
    }

    private async Task<JsonObject?> StoredSettingsAsync(Guid? organizationId, CancellationToken ct)
    {
        var rows = await _docs.ListAsync(organizationId, SettingsKind, ct);
        return rows.Count == 0 ? null : Parse(rows[0].Payload);
    }

    private async Task EnsureResourcesAsync(Guid? organizationId, CancellationToken ct)
    {
        var existing = await _docs.ListAsync(organizationId, "resources", ct);
        if (existing.Count > 0) return;
        (string Name, string Kind, int Capacity)[] seed =
        [
            ("Main Dining Room", "Dining", 40),
            ("Private Dining Room", "Dining", 12),
            ("Boardroom", "Meeting", 10),
            ("Spa Suite", "Spa", 1),
            ("Tennis Court", "Court", 4),
            ("Fitness Studio", "Studio", 12),
            ("Lounge", "Lounge", 20),
        ];
        foreach (var item in seed)
        {
            await CreateAsync(organizationId, "resources", new JsonObject
            {
                ["name"] = item.Name,
                ["kind"] = item.Kind,
                ["capacity"] = item.Capacity,
            }, ct);
        }
    }

    /// <summary>A new record from the request body, checked and stamped but not yet saved.</summary>
    private async Task<Result<JsonObject>> PrepareAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct)
    {
        var obj = Clone(body);
        // The server picks the id. Saving looks a row up by id alone, so an id taken from
        // the request was a way to overwrite any record whose id someone had seen,
        // including another organization's.
        obj["id"] = Guid.NewGuid().ToString();
        if (await StampAsync(organizationId, kind, obj, ct) is { } invalid) return invalid;
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
    private async Task<Error?> StampAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("o");
        switch (kind)
        {
            case "applications":
                Set(obj, "submittedAtUtc", now);
                Set(obj, "status", "Submitted");
                break;
            case "events":
                Set(obj, "registeredCount", 0);
                Set(obj, "waitlistCount", 0);
                Set(obj, "status", "Draft");
                break;
            case "reservations":
                Set(obj, "status", "Pending");
                await NameResourceAsync(organizationId, obj, ct);
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

    /// <summary>A reservation shows its resource's name, looked up rather than taken on trust.</summary>
    private async Task NameResourceAsync(Guid? organizationId, JsonObject reservation, CancellationToken ct)
    {
        if (!Guid.TryParse(Text(reservation, "resourceId"), out var resourceId)) return;
        var resource = await Load(organizationId, "resources", resourceId, ct);
        reservation["resourceName"] = resource == null ? "Unknown resource" : Text(resource, "name");
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

    /// <summary>A money kind asked for here is a bug in the caller: those routes answer from the ledger.</summary>
    private static void RefuseMoney(string kind)
    {
        if (MoneyKinds.Contains(kind))
            throw new InvalidOperationException($"Club {kind} are kept in the money ledger (IClubLedger), not as documents.");
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

    private static int Int(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<int>(out var n) ? n : int.TryParse(Text(obj, key), out var p) ? p : 0;
}
