using System.Globalization;
using System.Text.Json.Nodes;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Club money in the audit trail: charges, payments, gift cards, tabs, subscriptions
/// and donations.
/// </summary>
/// <remarks>
/// <para>
/// These are JSON documents (<see cref="ClubDocument"/>), so what changed is inside the
/// payload rather than in a column, and the lines here come from comparing the JSON
/// before and after the save. Until now none of them wrote a line: a 500 bar charge
/// could go, and nobody could say who removed it, or that it had been there.
/// </para>
/// <para>
/// As with the other records, only what a person would call a money event writes a
/// line. Bookkeeping that billing moves by itself (the next billing date, the moment a
/// charge was last paid) does not.
/// </para>
/// </remarks>
internal static partial class AuditTrail
{
    /// <summary>The money kinds, each with the JSON fields a person edits.</summary>
    private static readonly Dictionary<string, Spec> ClubMoney = new(StringComparer.Ordinal)
    {
        ["charges"] = new("charge", "Charge", nameof(ClubDocument.Kind),
            ["amount", "description", "kind", "dueAtUtc", "memberId", "reference"], e => ClubLabel(e, "description", "memberName")),
        ["payments"] = new("payment", "Payment", nameof(ClubDocument.Kind),
            ["amount", "method", "reference", "memberId"], e => ClubLabel(e, "memberName", "method")),
        ["gift-cards"] = new("gift_card", "Gift card", nameof(ClubDocument.Kind),
            ["faceValue", "recipientName", "recipientEmail", "purchaserName", "expiresAtUtc"], e => ClubLabel(e, "code", "recipientName")),
        ["tabs"] = new("tab", "Tab", nameof(ClubDocument.Kind),
            ["memberId", "venue"], e => ClubLabel(e, "venue", "memberName")),
        ["subscriptions"] = new("subscription", "Subscription", nameof(ClubDocument.Kind),
            ["name", "amount", "cadence", "memberId", "notes"], e => ClubLabel(e, "name", "memberName")),
        ["donations"] = new("donation", "Donation", nameof(ClubDocument.Kind),
            ["amount", "cause", "memberId"], e => ClubLabel(e, "cause", "memberName")),
    };

    private static readonly Dictionary<string, string> JsonFieldNames = new(StringComparer.Ordinal)
    {
        ["dueAtUtc"] = "due date",
        ["expiresAtUtc"] = "expiry date",
        ["memberId"] = "member",
        ["recipientName"] = "recipient",
        ["purchaserName"] = "purchaser",
    };

    private static void CollectClubMoney(EntityEntry entry, List<Pending> pending)
    {
        var kind = entry.Property(nameof(ClubDocument.Kind)).CurrentValue as string ?? "";
        if (!ClubMoney.TryGetValue(kind, out var spec)) return;
        void Add(string action, string summary) => pending.Add(new(entry, spec, action, _ => summary));

        switch (entry.State)
        {
            case EntityState.Added:
                Add("created", Created(kind, Payload(entry, current: true)));
                break;
            case EntityState.Deleted:
                // Said in full, so the line still means something once the record is gone.
                var gone = Payload(entry, current: false);
                Add("deleted", $"{spec.Noun} deleted ({Money(gone, kind == "gift-cards" ? "faceValue" : "amount")})");
                break;
            case EntityState.Modified:
                CollectClubChanges(entry, kind, spec, Add);
                break;
        }
    }

    private static void CollectClubChanges(EntityEntry entry, string kind, Spec spec, Action<string, string> add)
    {
        var was = Payload(entry, current: false);
        var now = Payload(entry, current: true);
        if (was == null || now == null) return;
        var (from, to) = (Str(was, "status"), Str(now, "status"));

        if (from != to && to == "Voided")
        {
            var reason = Str(now, "voidReason");
            var writtenOff = kind == "gift-cards" && Num(now, "writtenOffValue") > 0 ? $" ({Money(now, "writtenOffValue")} written off)" : "";
            add("voided", (reason.Length > 0 ? $"Voided: {reason}" : "Voided") + writtenOff);
            return;
        }

        switch (kind)
        {
            case "charges":
                if (from != to || Num(was, "paidAmount") != Num(now, "paidAmount"))
                {
                    var paid = $"paid {Money(now, "paidAmount")} of {Money(now, "amount")}";
                    add("status_changed", from != to ? $"{Moved("Status", Pretty(from), Pretty(to))}; {paid}" : Sentence(paid));
                }
                break;
            case "gift-cards":
                var spent = Num(was, "remainingValue") - Num(now, "remainingValue");
                if (spent > 0)
                    add("updated", string.Create(CultureInfo.InvariantCulture, $"Redeemed {spent:0.00}, {Money(now, "remainingValue")} left"));
                else if (from != to)
                    add("status_changed", Moved("Status", Pretty(from), Pretty(to)));
                break;
            case "tabs":
                if (TabItemChanges(was, now) is { Length: > 0 } items) add("updated", items);
                if (from != to)
                    add("status_changed", to == "Closed" ? $"Closed: {Money(now, "total")} posted to the house account" : Moved("Status", Pretty(from), Pretty(to)));
                break;
            case "donations":
                if (now["acknowledged"]?.ToString() == "true" && was["acknowledged"]?.ToString() != "true")
                    add("status_changed", "Acknowledged");
                break;
            default:
                if (from != to) add("status_changed", Moved("Status", Pretty(from), Pretty(to)));
                break;
        }

        var edited = spec.Fields.Where(f => !SameJson(was[f], now[f]))
            .Select(f => JsonFieldNames.TryGetValue(f, out var name) ? name : Words(f)).ToList();
        if (edited.Count > 0) add("updated", $"Updated {JoinList(edited)}");
    }

    private static string Created(string kind, JsonObject? obj) => kind switch
    {
        "charges" => $"Charge of {Money(obj, "amount")} posted",
        "payments" => $"Payment of {Money(obj, "amount")} recorded",
        "gift-cards" => $"Gift card issued for {Money(obj, "faceValue")}",
        "tabs" => "Tab opened",
        "subscriptions" => $"Subscription of {Money(obj, "amount")} {Words(Str(obj, "cadence"))} created",
        "donations" => $"Donation of {Money(obj, "amount")} received",
        _ => "Created",
    };

    /// <summary>"Added Negroni × 2 at 15.00; removed Olives × 1 at 6.00", or empty.</summary>
    private static string TabItemChanges(JsonObject was, JsonObject now)
    {
        static Dictionary<string, JsonObject> Items(JsonObject tab) =>
            (tab["items"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                .GroupBy(i => Str(i, "id")).ToDictionary(g => g.Key, g => g.First());
        static string Line(JsonObject i) => $"{Str(i, "name")} × {Num(i, "quantity").ToString("0.##", CultureInfo.InvariantCulture)} at {Money(i, "unitPrice")}";

        var (before, after) = (Items(was), Items(now));
        var phrases = after.Where(i => !before.ContainsKey(i.Key)).Select(i => $"added {Line(i.Value)}")
            .Concat(before.Where(i => !after.ContainsKey(i.Key)).Select(i => $"removed {Line(i.Value)}"))
            .ToList();
        return phrases.Count == 0 ? "" : Sentence(string.Join("; ", phrases));
    }

    /// <summary>"Dinner — Jean Dupont": the record's own words, so the line reads after a void.</summary>
    private static string? ClubLabel(EntityEntry entry, string first, string second)
    {
        var obj = Payload(entry, current: true);
        var parts = new[] { Str(obj, first), Str(obj, second) }.Where(p => p.Length > 0).ToArray();
        return parts.Length == 0 ? null : string.Join(" — ", parts);
    }

    private static JsonObject? Payload(EntityEntry entry, bool current)
    {
        var property = entry.Property(nameof(ClubDocument.Payload));
        var json = (current ? property.CurrentValue : property.OriginalValue) as string;
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Str(JsonObject? obj, string key) => obj?[key]?.ToString() ?? "";

    private static decimal Num(JsonObject? obj, string key) =>
        obj?[key] is JsonValue value && value.TryGetValue<decimal>(out var n) ? n
        : decimal.TryParse(Str(obj, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : 0;

    private static string Money(JsonObject? obj, string key) => Num(obj, key).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Pretty(string status) => status.Length == 0 ? status : Sentence(Words(status));

    /// <summary>The same value to a person reading the log: 100 and 100.00 alike, and null and "" alike.</summary>
    private static bool SameJson(JsonNode? a, JsonNode? b)
    {
        if (a is JsonValue x && b is JsonValue y && x.TryGetValue<decimal>(out var m) && y.TryGetValue<decimal>(out var n))
            return m == n;
        static string Flat(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node?.ToJsonString() ?? "";
        return Flat(a) == Flat(b);
    }
}
