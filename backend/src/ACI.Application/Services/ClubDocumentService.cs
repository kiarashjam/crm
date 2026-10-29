using System.Globalization;
using System.Security.Cryptography;
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
    Task<JsonObject?> AcknowledgeDonationAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    /// <param name="idempotencyKey">From the Idempotency-Key header: a retry carrying the same key gets the first redemption back instead of spending the card again.</param>
    Task<Result<JsonObject>> RedeemGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, string? idempotencyKey, CancellationToken ct = default);
    // Money records are voided, with a reason, and kept; only an owner or a manager may (see the controllers).
    Task<Result<JsonObject>> VoidGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> VoidChargeAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> VoidPaymentAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> AddTabItemAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, CancellationToken ct = default);
    Task<Result<JsonObject>> CloseTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<Result<JsonObject>> VoidTabAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> BillSubscriptionAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject?> SendCampaignAsync(Guid? organizationId, Guid id, CancellationToken ct = default);
    Task<JsonObject> GetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<JsonObject> SaveSettingsAsync(Guid? organizationId, JsonObject patch, CancellationToken ct = default);
    Task<JsonObject> ResetSettingsAsync(Guid? organizationId, CancellationToken ct = default);
}

public sealed class ClubDocumentService : IClubDocumentService
{
    private const string SettingsKind = "hospitality-settings";
    private const decimal MaxMoney = 1_000_000_000m;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] ChargeKinds = ["Dues", "Dining", "Bar", "Event", "Spa", "Booking", "Retail", "Other"];
    private static readonly string[] PaymentMethods = ["CardOnFile", "Cash", "Check", "Wire", "AppliedCredit"];
    private static readonly string[] TabVenues = ["Bar", "Dining", "Spa", "Rooftop", "PoolDeck"];
    private static readonly string[] Cadences = ["Monthly", "Quarterly", "SemiAnnual", "Annual"];

    /// <summary>
    /// What a PUT may change on a money record. Everything else on it is the server's:
    /// a status, a balance, a date the billing run moves.
    /// </summary>
    /// <remarks>
    /// A PUT used to merge any key it was sent, so {"amount": -100} on a subscription
    /// produced negative dues, and {"status": "Active"} cleared a past-due one. Money
    /// kinds missing from this list cannot be changed by a PUT at all.
    /// </remarks>
    private static readonly Dictionary<string, string[]> Patchable = new(StringComparer.Ordinal)
    {
        ["subscriptions"] = ["name", "amount", "cadence", "status", "notes", "nextChargeAtUtc"],
    };

    private static readonly HashSet<string> MoneyKinds = new(StringComparer.Ordinal)
    {
        "charges", "payments", "gift-cards", "gift-card-redemptions", "tabs", "subscriptions", "donations",
    };

    private readonly IClubDocumentRepository _docs;
    private readonly IClubRepository _club;
    private readonly ICurrentUserService _user;

    public ClubDocumentService(IClubDocumentRepository docs, IClubRepository club, ICurrentUserService user)
    {
        _docs = docs;
        _club = club;
        _user = user;
    }

    public async Task<JsonArray> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default)
    {
        if (kind == "resources")
            await EnsureResourcesAsync(organizationId, ct);
        var rows = await _docs.ListAsync(organizationId, kind, ct);
        var array = new JsonArray();
        var now = DateTime.UtcNow;
        foreach (var row in rows)
            if (Parse(row.Payload) is JsonObject obj)
                array.Add(AsOf(kind, obj, now));
        return array;
    }

    /// <summary>
    /// A record as it stands now. Some states come from the clock rather than from a
    /// write: a gift card past its expiry is expired, and an unpaid charge past its due
    /// date is overdue, whether or not anything saved that.
    /// </summary>
    /// <remarks>
    /// Nothing ever set a charge Overdue, so the dashboard's "Past due" and the Charges
    /// page's Overdue figure read zero however late the dues were. Worked out on every
    /// read, so the screens, the reports and the assistant all see the same thing, and no
    /// read has to write.
    /// </remarks>
    private static JsonObject AsOf(string kind, JsonObject obj, DateTime now)
    {
        var status = Text(obj, "status");
        if (kind == "gift-cards" && status == "Active" && ReadDate(obj, "expiresAtUtc") is { } expires && expires <= now)
            obj["status"] = "Expired";
        if (kind == "charges" && status is ("Pending" or "PartiallyPaid") && ReadDate(obj, "dueAtUtc") is { } due && due < now
            && Dec(obj, "amount") - Dec(obj, "paidAmount") > 0)
            obj["status"] = "Overdue";
        return obj;
    }

    public async Task<Result<JsonObject>> CreateAsync(Guid? organizationId, string kind, JsonObject body, CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(organizationId, kind, body, ct);
        if (!prepared.IsSuccess) return prepared;
        var obj = prepared.Value;
        return await WriteAsync(async () =>
        {
            await InsertAsync(organizationId, kind, obj, ct);
            return Result.Success(obj);
        }, ct, kind == "payments" ? DomainErrors.Club.ChargeChanged : null);
    }

    public async Task<Result<JsonObject>> UpdateAsync(Guid? organizationId, string kind, Guid id, JsonObject patch, CancellationToken ct = default)
    {
        var current = await Load(organizationId, kind, id, ct);
        if (current == null) return DomainErrors.Club.RecordNotFound;
        if (MoneyKinds.Contains(kind))
        {
            if (!Patchable.TryGetValue(kind, out var allowed)) return DomainErrors.Club.NotEditable;
            // PastDue is the billing's to say; a person pauses, resumes or cancels.
            if (patch.ContainsKey("status") && Text(patch, "status") is not ("Active" or "Paused" or "Cancelled"))
                return DomainErrors.Club.SubscriptionStatusInvalid;
            foreach (var key in allowed)
                if (patch.ContainsKey(key))
                    current[key] = patch[key]?.DeepClone();
            if (CheckSubscription(current) is { } invalid) return invalid;
        }
        else
        {
            foreach (var pair in patch)
                current[pair.Key] = pair.Value?.DeepClone();
        }
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
        // These are voided and kept, so the books can say what happened to them.
        if (kind is "charges" or "payments" or "gift-cards" or "gift-card-redemptions" or "tabs")
            throw new InvalidOperationException($"Club {kind} are voided, not deleted.");
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

    public async Task<JsonObject?> AcknowledgeDonationAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var row = await Load(organizationId, "donations", id, ct);
        if (row == null) return null;
        row["acknowledged"] = true;
        await Save(organizationId, "donations", id, row, ct);
        return row;
    }

    public async Task<Result<JsonObject>> RedeemGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, string? idempotencyKey, CancellationToken ct = default)
    {
        var key = idempotencyKey?.Trim() ?? "";
        if (key.Length > 100) return DomainErrors.Club.TooLong("An Idempotency-Key", 100);
        // A retry after a timeout, say, carries the key of the try that already went through.
        if (key.Length > 0 && await RedemptionAsync(organizationId, id, key, ct) is { } earlier) return earlier;

        var card = await Load(organizationId, "gift-cards", id, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        var status = Text(card, "status");
        // Only the clock says a card has expired, so it is checked here rather than trusted to a status.
        if (status == "Active" && ReadDate(card, "expiresAtUtc") is { } expires && expires <= DateTime.UtcNow)
            return DomainErrors.Club.GiftCardExpired(expires);
        if (status != "Active") return DomainErrors.Club.GiftCardNotActive(status);
        if (Amount(body, "amount") is not { } asked) return DomainErrors.Club.AmountInvalid;
        var remaining = Dec(card, "remainingValue");
        var amount = Math.Min(asked, remaining);
        if (amount <= 0) return DomainErrors.Club.GiftCardNotActive("Redeemed");
        remaining -= amount;
        card["remainingValue"] = remaining;
        card["status"] = remaining <= 0 ? "Redeemed" : "Active";

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
        if (key.Length > 0) redemption["idempotencyKey"] = key;

        // Two terminals redeeming the same card both read its balance, and both spent it.
        // Now the card is saved first, checked against the balance this request read: the
        // second terminal is refused there, before any redemption of its own is written,
        // and the card and its redemption are saved together or not at all.
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "gift-cards", id, card, ct);
            await Save(organizationId, "gift-card-redemptions", ReadId(redemption)!.Value, redemption, ct);
            return Result.Success(redemption);
        }, ct, DomainErrors.Club.GiftCardChanged);
    }

    /// <summary>The redemption of this card made with this idempotency key, if there is one.</summary>
    private async Task<JsonObject?> RedemptionAsync(Guid? organizationId, Guid cardId, string key, CancellationToken ct)
    {
        foreach (var row in await _docs.ListAsync(organizationId, "gift-card-redemptions", ct))
            if (Parse(row.Payload) is { } redemption && Text(redemption, "giftCardId") == cardId.ToString() && Text(redemption, "idempotencyKey") == key)
                return redemption;
        return null;
    }

    public async Task<Result<JsonObject>> VoidGiftCardAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        if (ReadReason(body, out var reason) is { } noReason) return noReason;
        var card = await Load(organizationId, "gift-cards", id, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        if (Text(card, "status") == "Voided") return DomainErrors.Club.GiftCardVoided;
        // What was left on the card is written off, and the card says how much.
        card["writtenOffValue"] = Dec(card, "remainingValue");
        card["remainingValue"] = 0;
        MarkVoided(card, reason);
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "gift-cards", id, card, ct);
            return Result.Success(card);
        }, ct, DomainErrors.Club.GiftCardChanged);
    }

    /// <remarks>
    /// Charges used to be deleted, by anyone with an account: a 500 bar charge could
    /// disappear, the balance and the next statement changed, and nothing said who did it
    /// or that it had ever been there. A voided charge stays, saying who voided it and why,
    /// and no longer counts towards the balance.
    /// </remarks>
    public async Task<Result<JsonObject>> VoidChargeAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        if (ReadReason(body, out var reason) is { } noReason) return noReason;
        var charge = await Load(organizationId, "charges", id, ct);
        if (charge == null) return DomainErrors.Club.ChargeNotFound;
        if (Text(charge, "status") == "Voided") return DomainErrors.Club.ChargeVoided;
        // Money paid against it would drop out of the balance with it, so the payments go first.
        if (Dec(charge, "paidAmount") > 0) return DomainErrors.Club.ChargeHasPayments;
        MarkVoided(charge, reason);
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "charges", id, charge, ct);
            await RecomputeBalanceAsync(organizationId, Text(charge, "memberId"), ct);
            return Result.Success(charge);
        }, ct, DomainErrors.Club.ChargeChanged);
    }

    /// <summary>A payment taken back: its charge owes again what it paid, and the payment stays, voided.</summary>
    public async Task<Result<JsonObject>> VoidPaymentAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        if (ReadReason(body, out var reason) is { } noReason) return noReason;
        var payment = await Load(organizationId, "payments", id, ct);
        if (payment == null) return DomainErrors.Club.PaymentNotFound;
        if (Text(payment, "status") == "Voided") return DomainErrors.Club.PaymentVoided;
        var chargeIdText = Text(payment, "chargeId");
        if (chargeIdText.Length == 0 && payment["appliedToChargeIds"] is JsonArray { Count: > 0 } applied)
            chargeIdText = applied[0]?.ToString() ?? "";
        JsonObject? charge = null;
        if (Guid.TryParse(chargeIdText, out var chargeId))
        {
            charge = await Load(organizationId, "charges", chargeId, ct);
            if (charge != null && Text(charge, "status") != "Voided")
            {
                var paid = Math.Max(0, Dec(charge, "paidAmount") - Dec(payment, "amount"));
                charge["paidAmount"] = paid;
                charge["status"] = paid > 0 ? "PartiallyPaid" : "Pending";
                charge.Remove("paidAtUtc");
            }
            else charge = null;
        }
        MarkVoided(payment, reason);
        return await WriteAsync(async () =>
        {
            if (charge != null) await Save(organizationId, "charges", chargeId, charge, ct);
            await Save(organizationId, "payments", id, payment, ct);
            await RecomputeBalanceAsync(organizationId, Text(payment, "memberId"), ct);
            return Result.Success(payment);
        }, ct, DomainErrors.Club.ChargeChanged);
    }

    public async Task<Result<JsonObject>> AddTabItemAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        var status = Text(tab, "status");
        if (status != "Open") return DomainErrors.Club.TabNotOpen(status);
        var name = Text(body, "name").Trim();
        if (name.Length == 0) return DomainErrors.Club.TabItemNameRequired;
        if (name.Length > 200) return DomainErrors.Club.TooLong("An item name", 200);
        // A quantity of -3 took 45 off the tab with no reason recorded: a comp nobody
        // could trace. A half portion is fine; less than nothing, or a thousand, is not.
        if (Number(body, "quantity") is not { } quantity || quantity <= 0 || quantity > 1000 || quantity != Math.Round(quantity, 2))
            return DomainErrors.Club.TabQuantityInvalid;
        if (Number(body, "unitPrice") is not { } unitPrice || unitPrice < 0 || unitPrice > 100_000 || unitPrice != Math.Round(unitPrice, 2))
            return DomainErrors.Club.TabPriceInvalid;
        var items = CopyArray(tab["items"] as JsonArray);
        var item = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = name,
            ["quantity"] = quantity,
            ["unitPrice"] = unitPrice,
            ["addedAtUtc"] = DateTime.UtcNow.ToString("o"),
        };
        StampWho(item, "added");
        items.Add(item);
        tab["items"] = items;
        tab["total"] = Total(items);
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "tabs", id, tab, ct);
            return Result.Success(tab);
        }, ct, DomainErrors.Club.TabChanged);
    }

    public async Task<Result<JsonObject>> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", tabId, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        // Taking an item off a tab that has already posted left the posted charge as it
        // was, and the tab's history no longer matched the member's account.
        var status = Text(tab, "status");
        if (status != "Open") return DomainErrors.Club.TabNotOpen(status);
        var items = CopyArray(tab["items"] as JsonArray);
        JsonArray next = new();
        foreach (var item in items)
        {
            if (item is JsonObject obj && Text(obj, "id") == itemId.ToString()) continue;
            next.Add(item?.DeepClone());
        }
        if (next.Count == items.Count) return DomainErrors.Club.TabItemNotFound;
        tab["items"] = next;
        tab["total"] = Total(next);
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "tabs", tabId, tab, ct);
            return Result.Success(tab);
        }, ct, DomainErrors.Club.TabChanged);
    }

    public async Task<Result<JsonObject>> CloseTabAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        var status = Text(tab, "status");
        // Closing twice posts once: a double click, or a second device, gets the closed tab back.
        if (status == "Closed") return tab;
        if (status != "Open") return DomainErrors.Club.TabNotOpen(status);
        var items = tab["items"] as JsonArray ?? new JsonArray();
        var total = Total(items);
        var venue = Text(tab, "venue");
        var label = VenueLabel(venue);
        var itemCount = items.Count;

        // Everything the close writes is worked out first, so a refusal (a member who has
        // since left, say) comes before anything is saved.
        JsonObject? charge = null;
        if (total > 0)
        {
            var prepared = await PrepareAsync(organizationId, "charges", new JsonObject
            {
                ["memberId"] = tab["memberId"]?.DeepClone(),
                ["kind"] = venue switch { "Bar" => "Bar", "Spa" => "Spa", _ => "Dining" },
                ["description"] = $"{label} tab ({itemCount} items)",
                ["amount"] = total,
            }, ct);
            if (!prepared.IsSuccess) return prepared;
            charge = prepared.Value;
            tab["chargeId"] = Text(charge, "id");
        }
        var tier = Text(tab, "memberTier");
        var multiplier = tier switch { "Silver" => 1.25m, "Gold" => 1.5m, "Platinum" => 2m, _ => 1m };
        var points = (int)Math.Floor(total * 10m * multiplier);
        LoyaltyLedgerEntry? earned = null;
        if (points > 0 && Guid.TryParse(Text(tab, "memberId"), out var memberId))
        {
            earned = new LoyaltyLedgerEntry
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = memberId,
                MemberName = Text(tab, "memberName"),
                Kind = "Earned",
                Reason = venue == "Bar" ? "BarPurchase" : "DiningPurchase",
                Points = points,
                Note = $"{label} tab — {itemCount} items",
                CreatedAtUtc = DateTime.UtcNow,
            };
            tab["loyaltyEntryId"] = earned.Id.ToString();
        }
        tab["total"] = total;
        tab["pointsAwarded"] = points;
        tab["status"] = "Closed";
        tab["closedAtUtc"] = DateTime.UtcNow.ToString("o");
        StampWho(tab, "closed");

        // Two bartenders closing the same tab both saw it open, and both posted a charge
        // and the points. The tab is saved first now, checked against the open tab this
        // request read: the second close is refused there, before it posts anything, and
        // the charge, the balance and the points are saved with the tab or not at all.
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "tabs", id, tab, ct);
            if (charge != null) await InsertAsync(organizationId, "charges", charge, ct);
            if (earned != null) await _club.AddLedgerAsync(earned, ct);
            return Result.Success(tab);
        }, ct, DomainErrors.Club.TabChanged);
    }

    public async Task<Result<JsonObject>> VoidTabAsync(Guid? organizationId, Guid id, JsonObject body, CancellationToken ct = default)
    {
        var tab = await Load(organizationId, "tabs", id, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        var status = Text(tab, "status");
        if (status == "Voided") return tab;
        // A stale screen used to void a tab someone else had closed: the tab said "no
        // charge", while the charge and the points it posted stayed on the account.
        if (status == "Closed") return DomainErrors.Club.TabClosedCannotVoid;
        if (status != "Open") return DomainErrors.Club.TabNotOpen(status);
        var reason = Text(body, "reason").Trim();
        if (reason.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        tab["status"] = "Voided";
        tab["voidedAtUtc"] = DateTime.UtcNow.ToString("o");
        StampWho(tab, "voided");
        if (reason.Length > 0) tab["voidReason"] = reason;
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "tabs", id, tab, ct);
            return Result.Success(tab);
        }, ct, DomainErrors.Club.TabChanged);
    }

    public async Task<Result<JsonObject>> BillSubscriptionAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var sub = await Load(organizationId, "subscriptions", id, ct);
        if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
        var status = Text(sub, "status");
        // This used to answer 200 with the subscription as it was, and the screen said
        // "Cycle charged" for a charge that was never posted.
        if (status is "Cancelled" or "Paused") return DomainErrors.Club.SubscriptionNotBillable(status);
        var charge = await PrepareAsync(organizationId, "charges", new JsonObject
        {
            ["memberId"] = sub["memberId"]?.DeepClone(),
            ["kind"] = "Dues",
            ["description"] = Text(sub, "name"),
            ["amount"] = Dec(sub, "amount"),
            ["dueAtUtc"] = DateTime.UtcNow.AddDays(14).ToString("o"),
        }, ct);
        if (!charge.IsSuccess) return charge;
        var days = CadenceDays(Text(sub, "cadence"));
        var next = ReadDate(sub, "nextChargeAtUtc") ?? DateTime.UtcNow;
        sub["lastChargedAtUtc"] = DateTime.UtcNow.ToString("o");
        sub["nextChargeAtUtc"] = next.AddDays(days).ToString("o");
        sub["status"] = "Active";
        // As with a tab: the subscription is saved first, against the copy this request
        // read, so two presses of Bill at once post one dues charge, not two.
        return await WriteAsync(async () =>
        {
            await Save(organizationId, "subscriptions", id, sub, ct);
            await InsertAsync(organizationId, "charges", charge.Value, ct);
            return Result.Success(sub);
        }, ct);
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
        // The charge is paid down before the payment is written: that save is checked
        // against the charge as it was when the payment was checked, so of two payments
        // made at once, the second is refused before it is recorded.
        if (kind == "payments")
            await ApplyPaymentAsync(organizationId, Guid.Parse(Text(obj, "chargeId")), Dec(obj, "amount"), ct);
        await Save(organizationId, kind, ReadId(obj)!.Value, obj, ct);
        await AfterCreateAsync(organizationId, kind, obj, ct);
    }

    /// <summary>
    /// Fills in what the server owns and checks what the client sent.
    /// </summary>
    /// <remarks>
    /// The money fields on these records are worked out here, never taken from the
    /// request. This used to fill a field only when the request left it out, so a POST
    /// of a charge with "status":"Paid","paidAmount":500 was stored as paid with no
    /// payment behind it, a gift card could be issued for 10 with 5,000 on it, and a
    /// tab opened with "memberTier":"Platinum" earned double points.
    /// </remarks>
    private async Task<Error?> StampAsync(Guid? organizationId, string kind, JsonObject obj, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("o");
        switch (kind)
        {
            case "donations":
                if (Amount(obj, "amount") is not { } gift) return DomainErrors.Club.AmountInvalid;
                obj["amount"] = gift;
                Set(obj, "receivedAtUtc", now);
                // Saying thank you is its own step (POST /acknowledge), not something a new record arrives with.
                obj["acknowledged"] = false;
                Set(obj, "isAnonymous", false);
                break;
            case "charges":
            {
                if (Amount(obj, "amount") is not { } amount) return DomainErrors.Club.AmountInvalid;
                if (await StampMemberAsync(organizationId, obj, ct) is { } noMember) return noMember;
                var chargeKind = Text(obj, "kind").Trim();
                if (chargeKind.Length == 0) chargeKind = "Other";
                if (!ChargeKinds.Contains(chargeKind)) return DomainErrors.Club.ChargeKindInvalid;
                var description = Text(obj, "description").Trim();
                if (description.Length == 0) return DomainErrors.Club.DescriptionRequired;
                if (description.Length > 500) return DomainErrors.Club.TooLong("A description", 500);
                if (!StampDate(obj, "dueAtUtc")) return DomainErrors.Club.DateInvalid("The due date");
                obj["amount"] = amount;
                obj["kind"] = chargeKind;
                obj["description"] = description;
                obj["postedAtUtc"] = now;
                obj["paidAmount"] = 0;
                obj["status"] = "Pending";
                Drop(obj, "paidAtUtc", "voidedAtUtc", "voidedByUserId", "voidedByName", "voidReason");
                break;
            }
            case "payments":
            {
                if (Amount(obj, "amount") is not { } amount) return DomainErrors.Club.AmountInvalid;
                if (!PaymentMethods.Contains(Text(obj, "method"))) return DomainErrors.Club.MethodInvalid;
                if (!StampDate(obj, "receivedAtUtc")) return DomainErrors.Club.DateInvalid("The date received");
                // A payment settles a charge, and may settle no more than it still owes:
                // the excess used to be recorded and then lost, as there is no house
                // credit to put it in. A stale page paying a charge twice is caught here too.
                if (!Guid.TryParse(Text(obj, "chargeId"), out var chargeId)) return DomainErrors.Club.PaymentChargeRequired;
                var charge = await Load(organizationId, "charges", chargeId, ct);
                if (charge == null) return DomainErrors.Club.ChargeNotFound;
                if (Text(charge, "status") == "Voided") return DomainErrors.Club.ChargeVoided;
                var owed = Dec(charge, "amount") - Dec(charge, "paidAmount");
                if (owed <= 0) return DomainErrors.Club.ChargeAlreadyPaid;
                if (amount > owed) return DomainErrors.Club.Overpaid(owed, amount);
                obj["amount"] = amount;
                obj["chargeId"] = chargeId.ToString();
                obj["memberId"] = Text(charge, "memberId");
                obj["memberName"] = Text(charge, "memberName");
                Set(obj, "receivedAtUtc", now);
                obj["appliedToChargeIds"] = new JsonArray(chargeId.ToString());
                Drop(obj, "status", "voidedAtUtc", "voidedByUserId", "voidedByName", "voidReason");
                break;
            }
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
                if (await StampMemberAsync(organizationId, obj, ct) is { } noSubscriber) return noSubscriber;
                if (string.IsNullOrWhiteSpace(Text(obj, "cadence"))) obj["cadence"] = "Monthly";
                obj["status"] = "Active";
                if (CheckSubscription(obj) is { } invalid) return invalid;
                obj["startedAtUtc"] = now;
                Set(obj, "nextChargeAtUtc", DateTime.UtcNow.AddDays(CadenceDays(Text(obj, "cadence"))).ToString("o"));
                Drop(obj, "lastChargedAtUtc");
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
            {
                if (Amount(obj, "faceValue") is not { } face) return DomainErrors.Club.AmountInvalid;
                if (!StampDate(obj, "expiresAtUtc")) return DomainErrors.Club.ExpiryInvalid;
                if (ReadDate(obj, "expiresAtUtc") is { } expires && expires <= DateTime.UtcNow) return DomainErrors.Club.ExpiryInvalid;
                obj["faceValue"] = face;
                obj["code"] = await NewGiftCodeAsync(organizationId, ct);
                obj["remainingValue"] = face;
                obj["status"] = "Active";
                obj["issuedAtUtc"] = now;
                Drop(obj, "voidedAtUtc", "voidedByUserId", "voidedByName", "voidReason", "writtenOffValue");
                break;
            }
            case "tabs":
                if (await StampMemberAsync(organizationId, obj, ct, withTier: true) is { } noGuest) return noGuest;
                if (!TabVenues.Contains(Text(obj, "venue"))) return DomainErrors.Club.VenueInvalid;
                obj["items"] = new JsonArray();
                obj["status"] = "Open";
                obj["openedAtUtc"] = now;
                obj["total"] = 0;
                Drop(obj, "closedAtUtc", "closedByUserId", "closedByName", "pointsAwarded", "chargeId", "loyaltyEntryId",
                    "voidedAtUtc", "voidedByUserId", "voidedByName", "voidReason");
                break;
            case "households":
                Set(obj, "createdAtUtc", now);
                break;
        }
        return null;
    }

    /// <summary>
    /// The record's member must be one of this club's; their name, and for a tab their
    /// tier, are copied from the member rather than taken from the request.
    /// </summary>
    private async Task<Error?> StampMemberAsync(Guid? organizationId, JsonObject obj, CancellationToken ct, bool withTier = false)
    {
        if (!Guid.TryParse(Text(obj, "memberId"), out var memberId)) return DomainErrors.Club.MemberNotFound;
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        obj["memberId"] = member.Id.ToString();
        obj["memberName"] = $"{member.FirstName} {member.LastName}".Trim();
        if (withTier) obj["memberTier"] = member.Tier;
        return null;
    }

    /// <summary>What a subscription may hold, on create and on a PUT alike.</summary>
    private static Error? CheckSubscription(JsonObject sub)
    {
        if (Amount(sub, "amount") is not { } amount) return DomainErrors.Club.AmountInvalid;
        sub["amount"] = amount;
        if (!Cadences.Contains(Text(sub, "cadence"))) return DomainErrors.Club.CadenceInvalid;
        if (!StampDate(sub, "nextChargeAtUtc")) return DomainErrors.Club.DateInvalid("The next billing date");
        return null;
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

    /// <summary>A payment pays its charge down. What it may pay was checked when the payment was.</summary>
    private async Task ApplyPaymentAsync(Guid? organizationId, Guid chargeId, decimal amount, CancellationToken ct)
    {
        // Gone since the payment was checked: someone else changed it.
        var charge = await Load(organizationId, "charges", chargeId, ct) ?? throw new ConcurrencyConflictException();
        var paid = Dec(charge, "paidAmount") + amount;
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
    /// The member's house balance: what their open charges leave owing, negative when they
    /// owe, plus the opening or hand-corrected part no charge explains. The browser used to
    /// work this out from its own copy of the charges, which is empty once they live here,
    /// and wrote back a balance of zero.
    /// </summary>
    /// <remarks>
    /// Without <see cref="ClubMember.ManualBalance"/> the first charge after an opening
    /// balance of -500 set the balance to that charge alone, and the 500 was gone.
    /// </remarks>
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
        member.HouseAccountBalance = member.ManualBalance - Math.Round(outstanding, 2);
        await _club.SaveMemberAsync(member, ct);
    }

    /// <summary>A reservation shows its resource's name, looked up rather than taken on trust.</summary>
    private async Task NameResourceAsync(Guid? organizationId, JsonObject reservation, CancellationToken ct)
    {
        if (!Guid.TryParse(Text(reservation, "resourceId"), out var resourceId)) return;
        var resource = await Load(organizationId, "resources", resourceId, ct);
        reservation["resourceName"] = resource == null ? "Unknown resource" : Text(resource, "name");
    }

    /// <summary>A code no other card in this workspace has.</summary>
    private async Task<string> NewGiftCodeAsync(Guid? organizationId, CancellationToken ct)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in await _docs.ListAsync(organizationId, "gift-cards", ct))
            if (Parse(row.Payload) is { } card)
                taken.Add(Text(card, "code"));
        string code;
        do code = GiftCode();
        while (taken.Contains(code));
        return code;
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

    /// <summary>Removes fields only the server sets, so a request cannot arrive with them.</summary>
    private static void Drop(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys) obj.Remove(key);
    }

    /// <summary>
    /// An optional date: absent or empty is fine, otherwise it must read as a date and is
    /// stored as UTC. False when it does not read as one.
    /// </summary>
    private static bool StampDate(JsonObject obj, string key)
    {
        var text = Text(obj, key).Trim();
        if (text.Length == 0)
        {
            obj.Remove(key);
            return true;
        }
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            return false;
        obj[key] = date.ToString("o");
        return true;
    }

    private static DateTime? ReadDate(JsonObject obj, string key) =>
        DateTime.TryParse(Text(obj, key), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static string Text(JsonObject obj, string key) => obj[key]?.ToString() ?? "";

    private static int Int(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<int>(out var n) ? n : int.TryParse(Text(obj, key), out var p) ? p : 0;

    private static decimal Dec(JsonObject obj, string key) => Number(obj, key) ?? 0;

    /// <summary>The value as a number; null when it is missing or is not one.</summary>
    private static decimal? Number(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<decimal>(out var n) ? n
        : decimal.TryParse(Text(obj, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;

    /// <summary>The reason a void must give: not blank, and short enough to keep.</summary>
    private static Error? ReadReason(JsonObject body, out string reason)
    {
        reason = Text(body, "reason").Trim();
        if (reason.Length == 0) return DomainErrors.Club.ReasonRequired;
        if (reason.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        return null;
    }

    private void MarkVoided(JsonObject obj, string reason)
    {
        obj["status"] = "Voided";
        obj["voidedAtUtc"] = DateTime.UtcNow.ToString("o");
        obj["voidReason"] = reason;
        StampWho(obj, "voided");
    }

    /// <summary>Who did this, on the record itself, so the tab or charge says it without the audit log.</summary>
    private void StampWho(JsonObject obj, string action)
    {
        if (_user.UserId is { } userId) obj[$"{action}ByUserId"] = userId.ToString();
        if (!string.IsNullOrWhiteSpace(_user.UserName)) obj[$"{action}ByName"] = _user.UserName;
    }

    /// <summary>A positive amount of money, to the cent; null when the value is not one.</summary>
    private static decimal? Amount(JsonObject obj, string key)
    {
        var value = Dec(obj, key);
        if (value <= 0 || value >= MaxMoney) return null;
        var cents = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return cents > 0 ? cents : null;
    }

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
        return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
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
