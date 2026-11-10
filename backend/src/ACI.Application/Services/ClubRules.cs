using System.Globalization;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

/// <summary>
/// What a club's settings say the server is to do: the points a purchase, a visit and an
/// event check-in earn, and what the billing does on its own. Read from the hospitality
/// settings document, over the defaults the settings page shows, with each tier's
/// multiplier taken from the price catalogue.
/// </summary>
/// <remarks>
/// The settings page saved all of this and nothing on the server read it: closing a tab
/// earned 10 points per franc times a multiplier written into the code, a check-in earned
/// whatever the browser sent, and dues were billed when someone pressed a button. Every
/// place that applies one of these rules now asks here.
/// </remarks>
public interface IClubRules
{
    Task<ClubRulesSnapshot> ReadAsync(Guid? organizationId, CancellationToken ct = default);
}

/// <summary>The points rules: base points per unit of currency spent, per visit by venue, per event check-in.</summary>
public sealed record LoyaltyRules(decimal BasePointsPerUnit, IReadOnlyDictionary<string, int> VisitPointsByVenue, int EventCheckInPoints)
{
    public static readonly LoyaltyRules Default = new(10m, ClubSettings.DefaultVisitPoints, 50);
}

/// <summary>What the club's billing does on its own. Everything automatic starts off until the club turns it on.</summary>
/// <param name="AutomaticBilling">The daily run bills every subscription whose period has started.</param>
/// <param name="PaymentTermsDays">Days a billed charge has before it is due.</param>
/// <param name="RemindersEnabled">The daily run reminds members of charges past their due date.</param>
/// <param name="ReminderDays">Days after the due date each reminder goes, ascending: [3, 14, 30] is three reminders.</param>
/// <param name="LapseEnabled">The daily run lapses an active member with a charge overdue longer than the grace period, and reinstates them once nothing is overdue.</param>
/// <param name="GracePeriodDays">Days a charge may be overdue before its member lapses.</param>
/// <param name="MonthlyStatements">The daily run emails every member with activity or a balance last month's statement, from <paramref name="StatementDay"/> on.</param>
/// <param name="StatementDay">The day of the month statements go out, 1 to 28.</param>
public sealed record BillingSettings(
    bool AutomaticBilling, int PaymentTermsDays, bool RemindersEnabled, IReadOnlyList<int> ReminderDays,
    bool LapseEnabled, int GracePeriodDays, bool MonthlyStatements, int StatementDay)
{
    public static readonly BillingSettings Default = new(false, 14, false, [3, 14, 30], false, 60, false, 1);

    /// <summary>Whether the daily run has anything to do for the club.</summary>
    public bool AnythingAutomatic => AutomaticBilling || RemindersEnabled || LapseEnabled || MonthlyStatements;
}

/// <summary>A club's rules at one moment: the settings and the tiers they apply to.</summary>
public sealed class ClubRulesSnapshot
{
    public ClubRulesSnapshot(LoyaltyRules loyalty, BillingSettings billing, IReadOnlyList<CatalogItemDto> tiers)
    {
        Loyalty = loyalty;
        Billing = billing;
        Tiers = tiers;
    }

    public LoyaltyRules Loyalty { get; }
    public BillingSettings Billing { get; }
    public IReadOnlyList<CatalogItemDto> Tiers { get; }

    /// <summary>The tier of this name (any case), or null.</summary>
    public CatalogItemDto? Tier(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null
        : Tiers.FirstOrDefault(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The tier's loyalty multiplier; 1 for a tier the catalogue does not have.</summary>
    public decimal MultiplierFor(string? tier) => Tier(tier)?.LoyaltyMultiplier is { } m and > 0 ? m : 1m;

    /// <summary>Points for spending <paramref name="total"/>: the base per unit times the tier's multiplier, rounded down.</summary>
    public int PointsForSpend(decimal total, string? tier) =>
        total <= 0 ? 0 : Points(total * Loyalty.BasePointsPerUnit * MultiplierFor(tier));

    /// <summary>Points for a visit to <paramref name="venue"/>: the venue's points times the tier's multiplier, rounded down.</summary>
    public int PointsForVisit(string? venue, string? tier) =>
        venue != null && Loyalty.VisitPointsByVenue.TryGetValue(venue, out var points) ? Points(points * MultiplierFor(tier)) : 0;

    /// <summary>Points for checking in at an event: the base times the tier's multiplier, rounded down.</summary>
    public int PointsForEventCheckIn(string? tier) => Points(Loyalty.EventCheckInPoints * MultiplierFor(tier));

    private static int Points(decimal value) => value <= 0 ? 0 : value >= int.MaxValue ? int.MaxValue : (int)Math.Floor(value);
}

/// <summary>
/// The hospitality settings document's keys, their defaults (the ones the settings page
/// shows) and their checks.
/// </summary>
public static class ClubSettings
{
    public const string Kind = "hospitality-settings";

    public const string BasePoints = "basePointsPerDollar";
    public const string VisitPoints = "visitPointsByVenue";
    public const string EventPoints = "eventCheckInPoints";
    public const string Billing = "billing";
    public const string Currency = "currency";

    /// <summary>
    /// Keys the settings page no longer saves. The tiers are the price catalogue's now; the
    /// redemption rate and the referral and anniversary bonuses were saved and never used by
    /// anything, so they left the page rather than look as if they did something.
    /// </summary>
    public static readonly string[] Retired = ["tiers", "pointsPerDollarRedemption", "referralBonus", "anniversaryBonus"];

    public static readonly IReadOnlyDictionary<string, int> DefaultVisitPoints = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Dining"] = 50, ["Bar"] = 40, ["Spa"] = 60, ["Gym"] = 20, ["Coworking"] = 30, ["Event"] = 50, ["Lounge"] = 25, ["Rooftop"] = 30,
    };

    /// <summary>The rules the document sets, over the defaults; a value that does not read as one leaves the default.</summary>
    public static (LoyaltyRules Loyalty, BillingSettings Billing) Read(JsonObject? doc)
    {
        doc ??= new JsonObject();
        var loyaltyDefault = LoyaltyRules.Default;
        var basePoints = Number(doc[BasePoints]) is { } b && b is >= 0 and <= 1000 ? b : loyaltyDefault.BasePointsPerUnit;
        // By venue, whatever case the page wrote it in: "spa" sets the Spa venue's points.
        var visits = new Dictionary<string, int>(DefaultVisitPoints, StringComparer.OrdinalIgnoreCase);
        if (doc[VisitPoints] is JsonObject venues)
            foreach (var (venue, value) in venues)
                if (Whole(value) is { } points && points is >= 0 and <= 100_000) visits[venue] = points;
        var eventPoints = Whole(doc[EventPoints]) is { } e && e is >= 0 and <= 100_000 ? e : loyaltyDefault.EventCheckInPoints;

        var d = BillingSettings.Default;
        var billing = doc[Billing] as JsonObject ?? new JsonObject();
        var reminderDays = Days(billing["reminderDays"]) ?? d.ReminderDays;
        var settings = new BillingSettings(
            Flag(billing["automaticBilling"]) ?? d.AutomaticBilling,
            Whole(billing["paymentTermsDays"]) is { } terms && terms is >= 0 and <= 365 ? terms : d.PaymentTermsDays,
            Flag(billing["remindersEnabled"]) ?? d.RemindersEnabled,
            reminderDays,
            Flag(billing["lapseEnabled"]) ?? d.LapseEnabled,
            Whole(billing["gracePeriodDays"]) is { } grace && grace is >= 1 and <= 365 ? grace : d.GracePeriodDays,
            Flag(billing["monthlyStatements"]) ?? d.MonthlyStatements,
            Whole(billing["statementDay"]) is { } day && day is >= 1 and <= 28 ? day : d.StatementDay);
        return (new LoyaltyRules(basePoints, visits, eventPoints), settings);
    }

    /// <summary>
    /// What a save may change, checked: null when it is fine, else the sentence that says
    /// what is wrong. The retired keys are not checked; <see cref="Clean"/> drops them.
    /// </summary>
    public static Error? Validate(JsonObject patch)
    {
        if (patch.ContainsKey(Currency) && !RegionalSettings.TryCurrency(patch[Currency]?.ToString(), out _))
            return Invalid("The club currency is a three-letter code, such as CHF.");
        if (patch.ContainsKey(BasePoints) && Number(patch[BasePoints]) is not (>= 0 and <= 1000))
            return Invalid("The base points per unit spent must be a number from 0 to 1,000.");
        if (patch.ContainsKey(EventPoints) && Whole(patch[EventPoints]) is not (>= 0 and <= 100_000))
            return Invalid("The event check-in points must be a whole number from 0 to 100,000.");
        if (patch.ContainsKey(VisitPoints))
        {
            if (patch[VisitPoints] is not JsonObject venues) return Invalid("The points for each venue must be listed by venue.");
            foreach (var (venue, value) in venues)
                if (Whole(value) is not (>= 0 and <= 100_000))
                    return Invalid($"The points for a visit to {venue} must be a whole number from 0 to 100,000.");
        }
        if (patch.ContainsKey(Billing))
        {
            if (patch[Billing] is not JsonObject billing) return Invalid("The billing settings must be a set of values.");
            foreach (var flag in new[] { "automaticBilling", "remindersEnabled", "lapseEnabled", "monthlyStatements" })
                if (billing.ContainsKey(flag) && Flag(billing[flag]) == null) return Invalid($"Billing's {flag} must be true or false.");
            if (billing.ContainsKey("paymentTermsDays") && Whole(billing["paymentTermsDays"]) is not (>= 0 and <= 365))
                return Invalid("The payment terms must be a whole number of days from 0 to 365.");
            if (billing.ContainsKey("gracePeriodDays") && Whole(billing["gracePeriodDays"]) is not (>= 1 and <= 365))
                return Invalid("The grace period must be a whole number of days from 1 to 365.");
            if (billing.ContainsKey("statementDay") && Whole(billing["statementDay"]) is not (>= 1 and <= 28))
                return Invalid("Statements go out on a day of the month from 1 to 28, which every month has.");
            if (billing.ContainsKey("reminderDays") && Days(billing["reminderDays"]) == null)
                return Invalid("Reminders are one to five numbers of days after the due date, each from 1 to 365 and each later than the one before.");
        }
        return null;
    }

    /// <summary>The patch without the keys the page no longer saves: an older page still sending them changes nothing.</summary>
    public static JsonObject Clean(JsonObject patch)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in patch)
            if (!Retired.Contains(key)) copy[key] = value?.DeepClone();
        return copy;
    }

    /// <summary>
    /// The settings as the server applies them: the document's own values, with the defaults
    /// for anything it does not set and the retired keys left out, so the page shows what is
    /// in force rather than a guess of its own.
    /// </summary>
    public static JsonObject Effective(JsonObject? doc)
    {
        var (loyalty, billing) = Read(doc);
        var result = new JsonObject();
        if (doc != null)
            foreach (var (key, value) in doc)
                if (!Retired.Contains(key)) result[key] = value?.DeepClone();
        result[BasePoints] = loyalty.BasePointsPerUnit;
        var venues = new JsonObject();
        foreach (var (venue, points) in loyalty.VisitPointsByVenue) venues[venue] = points;
        result[VisitPoints] = venues;
        result[EventPoints] = loyalty.EventCheckInPoints;
        result[Billing] = new JsonObject
        {
            ["automaticBilling"] = billing.AutomaticBilling,
            ["paymentTermsDays"] = billing.PaymentTermsDays,
            ["remindersEnabled"] = billing.RemindersEnabled,
            ["reminderDays"] = new JsonArray(billing.ReminderDays.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray()),
            ["lapseEnabled"] = billing.LapseEnabled,
            ["gracePeriodDays"] = billing.GracePeriodDays,
            ["monthlyStatements"] = billing.MonthlyStatements,
            ["statementDay"] = billing.StatementDay,
        };
        return result;
    }

    private static Error Invalid(string sentence) => new("Settings.Invalid", sentence);

    private static decimal? Number(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<decimal>(out var d)) return d;
        if (value.TryGetValue<double>(out var x) && !double.IsNaN(x) && !double.IsInfinity(x) && Math.Abs(x) < 1e15) return (decimal)x;
        return value.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    private static int? Whole(JsonNode? node) =>
        Number(node) is { } d && d == Math.Floor(d) && d is >= int.MinValue and <= int.MaxValue ? (int)d : null;

    private static bool? Flag(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>One to five days from 1 to 365, strictly rising; null when the node is not that.</summary>
    private static IReadOnlyList<int>? Days(JsonNode? node)
    {
        if (node is not JsonArray array || array.Count is 0 or > 5) return null;
        var days = new List<int>();
        foreach (var item in array)
        {
            if (Whole(item) is not { } day || day is < 1 or > 365 || (days.Count > 0 && day <= days[^1])) return null;
            days.Add(day);
        }
        return days;
    }
}

/// <inheritdoc cref="IClubRules"/>
public sealed class ClubRules : IClubRules
{
    private readonly IClubDocumentRepository _docs;
    private readonly IClubCatalog _catalog;

    public ClubRules(IClubDocumentRepository docs, IClubCatalog catalog)
    {
        _docs = docs;
        _catalog = catalog;
    }

    public async Task<ClubRulesSnapshot> ReadAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _docs.ListAsync(organizationId, ClubSettings.Kind, ct);
        // The settings page saves one document; should there ever be two, the latest stands.
        var latest = rows.OrderByDescending(r => r.UpdatedAtUtc).FirstOrDefault();
        var doc = latest == null ? null : ParseObject(latest.Payload);
        var (loyalty, billing) = ClubSettings.Read(doc);
        return new ClubRulesSnapshot(loyalty, billing, await _catalog.TiersAsync(organizationId, ct));
    }

    private static JsonObject? ParseObject(string payload)
    {
        try
        {
            return JsonNode.Parse(payload) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
