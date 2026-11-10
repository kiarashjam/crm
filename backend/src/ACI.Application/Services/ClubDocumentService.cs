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
    Task<JsonObject?> SendCampaignAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
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

    private readonly IClubDocumentRepository _docs;
    private readonly IClubRepository _club;

    public ClubDocumentService(IClubDocumentRepository docs, IClubRepository club)
    {
        _docs = docs;
        _club = club;
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

    public async Task<JsonObject?> SendCampaignAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var campaign = await Load(organizationId, "campaigns", id, ct);
        if (campaign == null) return null;
        var members = await _club.MembersAsync(organizationId, ct);
        var count = 0;
        foreach (var member in members)
        {
            if (!Matches(campaign["segment"] as JsonObject, member)) continue;
            count++;
            await CreateAsync(organizationId, "communications", new JsonObject
            {
                ["memberId"] = member.Id.ToString(),
                ["memberName"] = $"{member.FirstName} {member.LastName}".Trim(),
                ["channel"] = Text(campaign, "channel"),
                ["category"] = Text(campaign, "category"),
                ["subject"] = Text(campaign, "subject"),
                ["preview"] = Preview(Text(campaign, "body")),
                ["campaignName"] = Text(campaign, "name"),
            }, ct);
        }
        campaign["status"] = "Sent";
        campaign["sentAtUtc"] = DateTime.UtcNow.ToString("o");
        campaign["recipientCount"] = count;
        await Save(organizationId, "campaigns", id, campaign, ct);
        return campaign;
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
            case "communications":
                Set(obj, "sentAtUtc", now);
                Set(obj, "status", "Sent");
                break;
            case "campaigns":
                Set(obj, "createdAtUtc", now);
                Set(obj, "status", string.IsNullOrWhiteSpace(Text(obj, "scheduledAtUtc")) ? "Draft" : "Scheduled");
                Set(obj, "recipientCount", 0);
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

    private static string Preview(string body) => body.Length <= 140 ? body : body[..140];

    private static bool Matches(JsonObject? segment, ClubMember member)
    {
        if (segment == null) return true;
        if (segment["tiers"] is JsonArray tiers && tiers.Count > 0 && !tiers.Any(t => t?.ToString() == member.Tier))
            return false;
        if (segment["statuses"] is JsonArray statuses && statuses.Count > 0 && !statuses.Any(s => s?.ToString() == member.Status))
            return false;
        return true;
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
