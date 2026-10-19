using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public interface IClubDocumentService
{
    Task<JsonArray> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default);
    Task<JsonObject> CreateAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct = default);
    Task<JsonObject?> UpdateAsync(Guid? organizationId, string kind, Guid id, JsonObject patch, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct = default);
    Task<JsonObject?> RsvpAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> AcknowledgeDonationAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> RedeemGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<JsonObject?> VoidGiftCardAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> AddTabItemAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<JsonObject?> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, CancellationToken ct = default);
    Task<JsonObject?> CloseTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> VoidTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> BillSubscriptionAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> SendCampaignAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<JsonObject> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default);
    Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
}

public sealed class ClubDocumentService : IClubDocumentService
{
    private const string SettingsKind = "hospitality-settings";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IClubDocumentRepository _docs;
    private readonly IClubRepository _club;

    public ClubDocumentService(IClubDocumentRepository docs, IClubRepository club)
    {
        _docs = docs;
        _club = club;
    }

    public async Task<JsonArray> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default)
    {
        if (kind == "resources")
            await EnsureResourcesAsync(organizationId, ct);
        var rows = await _docs.ListAsync(organizationId, kind, ct);
        var array = new JsonArray();
        foreach (var row in rows)
            if (Parse(row.Payload) is JsonObject obj)
                array.Add(obj);
        return array;
    }

    public async Task<JsonObject> CreateAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct = default)
    {
        var obj = Clone(body);
        // The server picks the id. Saving looks a row up by id alone, so an id taken from
        // the request was a way to overwrite any record whose id someone had seen,
        // including another organization's.
        var id = Guid.NewGuid();
        obj["id"] = id.ToString();
        await StampAsync(organizationId, kind, obj, ct);
        await Save(organizationId, kind, id, obj, ct);
        await AfterCreateAsync(organizationId, kind, obj, ct);
        return obj;
    }

    public async Task<JsonObject?> UpdateAsync(Guid? organizationId, string kind, Guid id, JsonObject patch, CancellationToken ct = default)
    {
        var current = await Load(organizationId, kind, id, ct);
        if (current == null) return null;
        foreach (var pair in patch)
            current[pair.Key] = pair.Value?.DeepClone();
        current["id"] = id.ToString();
        if (kind == "reservations" && patch.ContainsKey("resourceId"))
            await NameResourceAsync(organizationId, current, ct);
        await Save(organizationId, kind, id, current, ct);
        return current;
    }

    public async Task<bool> DeleteAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct = default)
    {
        var row = await Load(organizationId, kind, id, ct);
        if (row == null || !await _docs.DeleteAsync(id, organizationId, kind, ct)) return false;
        switch (kind)
        {
            case "charges":
                await RecomputeBalanceAsync(organizationId, Text(row, "memberId"), ct);
                break;
            case "households":
                // A household's member links go with it, as they did in the browser.
                foreach (var link in await _docs.ListAsync(organizationId, "household-members", ct))
                    if (Parse(link.Payload) is { } obj && Text(obj, "householdId") == id.ToString())
                        await _docs.DeleteAsync(link.Id, organizationId, "household-members", ct);
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

    public async Task<JsonObject?> AcknowledgeDonationAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var row = await Load(organizationId, "donations", id, ct);
        if (row == null) return null;
        row["acknowledged"] = true;
        await Save(organizationId, "donations", id, row, ct);
        return row;
    }

    public async Task<JsonObject?> RedeemGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        var card = await Load(organizationId, "gift-cards", id, ct);
        if (card == null || Text(card, "status") != "Active") return null;
        var remaining = Dec(card, "remainingValue");
        var asked = Dec(body, "amount");
        var amount = Math.Min(asked, remaining);
        if (amount <= 0) return null;
        remaining -= amount;
        card["remainingValue"] = remaining;
        card["status"] = remaining <= 0 ? "Redeemed" : "Active";
        await Save(organizationId, "gift-cards", id, card, ct);

        var redemption = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["giftCardId"] = id.ToString(),
            ["giftCardCode"] = Text(card, "code"),
            ["amount"] = amount,
            ["redeemedByName"] = Text(body, "redeemedByName"),
            ["redeemedAtUtc"] = DateTime.UtcNow.ToString("o"),
            ["appliedTo"] = body["appliedTo"]?.DeepClone(),
        };
        await Save(organizationId, "gift-card-redemptions", ReadId(redemption)!.Value, redemption, ct);
        return redemption;
    }

    public async Task<JsonObject?> VoidGiftCardAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var card = await Load(organizationId, "gift-cards", id, ct);
        if (card == null) return null;
        card["status"] = "Voided";
        await Save(organizationId, "gift-cards", id, card, ct);
        return card;
    }

    public async Task<JsonObject?> AddTabItemAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null || Text(tab, "status") != "Open") return null;
        var items = CopyArray(tab["items"] as JsonArray);
        items.Add(new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = Text(body, "name"),
            ["quantity"] = Dec(body, "quantity"),
            ["unitPrice"] = Dec(body, "unitPrice"),
            ["addedAtUtc"] = DateTime.UtcNow.ToString("o"),
        });
        tab["items"] = items;
        tab["total"] = Total(items);
        await Save(organizationId, "tabs", id, tab, ct);
        return tab;
    }

    public async Task<JsonObject?> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", tabId, ct);
        if (tab == null) return null;
        var items = CopyArray(tab["items"] as JsonArray);
        JsonArray next = new();
        foreach (var item in items)
        {
            if (item is JsonObject obj && Text(obj, "id") == itemId.ToString()) continue;
            next.Add(item?.DeepClone());
        }
        tab["items"] = next;
        tab["total"] = Total(next);
        await Save(organizationId, "tabs", tabId, tab, ct);
        return tab;
    }

    public async Task<JsonObject?> CloseTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null || Text(tab, "status") != "Open") return null;
        var items = tab["items"] as JsonArray ?? new JsonArray();
        var total = Total(items);
        tab["total"] = total;
        var venue = Text(tab, "venue");
        var label = VenueLabel(venue);
        var itemCount = (tab["items"] as JsonArray)?.Count ?? 0;
        if (total > 0)
        {
            await CreateAsync(organizationId, "charges", new JsonObject
            {
                ["memberId"] = tab["memberId"]?.DeepClone(),
                ["memberName"] = tab["memberName"]?.DeepClone(),
                ["kind"] = venue switch { "Bar" => "Bar", "Spa" => "Spa", _ => "Dining" },
                ["description"] = $"{label} tab ({itemCount} items)",
                ["amount"] = total,
            }, ct);
        }
        var tier = Text(tab, "memberTier");
        var multiplier = tier switch { "Silver" => 1.25m, "Gold" => 1.5m, "Platinum" => 2m, _ => 1m };
        var points = (int)Math.Floor(total * 10m * multiplier);
        tab["pointsAwarded"] = points;
        tab["status"] = "Closed";
        tab["closedAtUtc"] = DateTime.UtcNow.ToString("o");
        await Save(organizationId, "tabs", id, tab, ct);
        if (points > 0 && Guid.TryParse(Text(tab, "memberId"), out var memberId))
        {
            await _club.AddLedgerAsync(new LoyaltyLedgerEntry
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = memberId,
                MemberName = Text(tab, "memberName"),
                Kind = "Earned",
                Reason = Text(tab, "venue") == "Bar" ? "BarPurchase" : "DiningPurchase",
                Points = points,
                Note = $"{label} tab — {itemCount} items",
                CreatedAtUtc = DateTime.UtcNow,
            }, ct);
        }
        return tab;
    }

    public async Task<JsonObject?> VoidTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null) return null;
        tab["status"] = "Voided";
        await Save(organizationId, "tabs", id, tab, ct);
        return tab;
    }

    public async Task<JsonObject?> BillSubscriptionAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var sub = await Load(organizationId, "subscriptions", id, ct);
        if (sub == null) return null;
        var status = Text(sub, "status");
        if (status is "Cancelled" or "Paused") return sub;
        await CreateAsync(organizationId, "charges", new JsonObject
        {
            ["memberId"] = sub["memberId"]?.DeepClone(),
            ["memberName"] = sub["memberName"]?.DeepClone(),
            ["kind"] = "Dues",
            ["description"] = Text(sub, "name"),
            ["amount"] = Dec(sub, "amount"),
            ["dueAtUtc"] = DateTime.UtcNow.AddDays(14).ToString("o"),
        }, ct);
        var days = CadenceDays(Text(sub, "cadence"));
        var next = DateTime.UtcNow;
        if (DateTime.TryParse(Text(sub, "nextChargeAtUtc"), out var parsed)) next = parsed.ToUniversalTime();
        sub["lastChargedAtUtc"] = DateTime.UtcNow.ToString("o");
        sub["nextChargeAtUtc"] = next.AddDays(days).ToString("o");
        sub["status"] = "Active";
        await Save(organizationId, "subscriptions", id, sub, ct);
        return sub;
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

    private async Task StampAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("o");
        switch (kind)
        {
            case "donations":
                Set(obj, "receivedAtUtc", now);
                Set(obj, "acknowledged", false);
                Set(obj, "isAnonymous", false);
                break;
            case "charges":
                Set(obj, "postedAtUtc", now);
                Set(obj, "paidAmount", 0);
                Set(obj, "status", "Pending");
                break;
            case "payments":
                Set(obj, "receivedAtUtc", now);
                Set(obj, "appliedToChargeIds", Guid.TryParse(Text(obj, "chargeId"), out var paidCharge)
                    ? new JsonArray(paidCharge.ToString())
                    : new JsonArray());
                break;
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
            case "subscriptions":
                Set(obj, "status", "Active");
                Set(obj, "startedAtUtc", now);
                Set(obj, "nextChargeAtUtc", DateTime.UtcNow.AddDays(CadenceDays(Text(obj, "cadence"))).ToString("o"));
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

            case "gift-cards":
                Set(obj, "code", GiftCode());
                Set(obj, "remainingValue", Dec(obj, "faceValue"));
                Set(obj, "status", "Active");
                Set(obj, "issuedAtUtc", now);
                break;
            case "tabs":
                Set(obj, "items", new JsonArray());
                Set(obj, "status", "Open");
                Set(obj, "openedAtUtc", now);
                Set(obj, "total", 0);
                if (Guid.TryParse(Text(obj, "memberId"), out var memberId))
                {
                    var member = await _club.MemberAsync(memberId, organizationId, ct);
                    if (member != null)
                    {
                        Set(obj, "memberName", $"{member.FirstName} {member.LastName}".Trim());
                        Set(obj, "memberTier", member.Tier);
                    }
                }
                break;
            case "households":
                Set(obj, "createdAtUtc", now);
                break;
        }
    }

    /// <summary>What the browser did after these writes, done here so every device sees it.</summary>
    private async Task AfterCreateAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        switch (kind)
        {
            case "charges":
                await RecomputeBalanceAsync(organizationId, Text(obj, "memberId"), ct);
                break;
            case "payments":
                if (Guid.TryParse(Text(obj, "chargeId"), out var chargeId))
                    await ApplyPaymentAsync(organizationId, chargeId, Dec(obj, "amount"), ct);
                await RecomputeBalanceAsync(organizationId, Text(obj, "memberId"), ct);
                break;
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

    /// <summary>A payment against a charge pays it down, up to what is owed.</summary>
    private async Task ApplyPaymentAsync(Guid? organizationId, Guid chargeId, decimal amount, CancellationToken ct)
    {
        var charge = await Load(organizationId, "charges", chargeId, ct);
        if (charge == null || amount <= 0) return;
        var paid = Math.Min(Dec(charge, "amount"), Dec(charge, "paidAmount") + amount);
        charge["paidAmount"] = paid;
        if (paid >= Dec(charge, "amount"))
        {
            charge["status"] = "Paid";
            charge["paidAtUtc"] = DateTime.UtcNow.ToString("o");
        }
        else if (paid > 0) charge["status"] = "PartiallyPaid";
        await Save(organizationId, "charges", chargeId, charge, ct);
    }

    /// <summary>
    /// The member's house balance is what their open charges leave owing, negative when
    /// they owe. The browser used to work this out from its own copy of the charges,
    /// which is empty once they live here, and wrote back a balance of zero.
    /// </summary>
    private async Task RecomputeBalanceAsync(Guid? organizationId, string memberIdText, CancellationToken ct)
    {
        if (!Guid.TryParse(memberIdText, out var memberId)) return;
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return;
        decimal outstanding = 0;
        foreach (var row in await _docs.ListAsync(organizationId, "charges", ct))
        {
            if (Parse(row.Payload) is not { } charge || Text(charge, "memberId") != memberIdText) continue;
            if (Text(charge, "status") == "Voided") continue;
            outstanding += Dec(charge, "amount") - Dec(charge, "paidAmount");
        }
        member.HouseAccountBalance = -Math.Round(outstanding, 2);
        await _club.SaveMemberAsync(member, ct);
    }

    /// <summary>A reservation shows its resource's name, looked up rather than taken on trust.</summary>
    private async Task NameResourceAsync(Guid? organizationId, JsonObject reservation, CancellationToken ct)
    {
        if (!Guid.TryParse(Text(reservation, "resourceId"), out var resourceId)) return;
        var resource = await Load(organizationId, "resources", resourceId, ct);
        reservation["resourceName"] = resource == null ? "Unknown resource" : Text(resource, "name");
    }

    private static int CadenceDays(string cadence) => cadence switch
    {
        "Quarterly" => 91,
        "SemiAnnual" => 182,
        "Annual" => 365,
        _ => 30,
    };

    private static string VenueLabel(string venue) => venue switch
    {
        "Bar" => "Library Bar",
        "Dining" => "Main Dining",
        "PoolDeck" => "Pool Deck",
        _ => venue,
    };

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

    private static decimal Dec(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<decimal>(out var n) ? n : decimal.TryParse(Text(obj, key), out var p) ? p : 0;

    private static JsonArray CopyArray(JsonArray? source)
    {
        var copy = new JsonArray();
        if (source == null) return copy;
        foreach (var item in source)
            copy.Add(item?.DeepClone());
        return copy;
    }

    private static decimal Total(JsonArray items)
    {
        decimal sum = 0;
        foreach (var item in items)
            if (item is JsonObject obj)
                sum += Dec(obj, "quantity") * Dec(obj, "unitPrice");
        return sum;
    }

    private static string GiftCode()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> raw = stackalloc char[12];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return $"GIFT-{new string(raw[..4])}-{new string(raw[4..8])}-{new string(raw[8..])}";
    }
}
