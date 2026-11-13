using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Retention;

/// <summary>
/// A workspace's retention and referral rules as the services apply them: its settings row,
/// or the defaults (every save action off, no referral bonus, no sponsor rule) when it has none.
/// </summary>
internal static class RetentionRules
{
    /// <summary>The workspace's rules, read in the workspace being acted for.</summary>
    public static async Task<ClubRetentionSettings> ReadAsync(AppDbContext db, Guid organizationId, CancellationToken ct) =>
        await db.ClubRetentionSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct)
        ?? new ClubRetentionSettings { OrganizationId = organizationId };

    public static RiskWeights Weights(ClubRetentionSettings s) =>
        new(s.VisitWeight, s.SpendWeight, s.BalanceWeight, s.EventWeight, s.ConcernWeight, s.TenureWeight, s.AtRiskThreshold, s.WatchThreshold);

    /// <summary>The workspace's currency and the culture its amounts are written in.</summary>
    public static async Task<(string Currency, CultureInfo Culture, string? Locale, string Name, Guid OwnerUserId, string? TimeZone)> WorkspaceAsync(
        AppDbContext db, Guid organizationId, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Currency, o.Locale, o.Name, o.OwnerUserId, o.TimeZone }).FirstOrDefaultAsync(ct);
        var currency = RegionalSettings.TryCurrency(org?.Currency, out var code) ? code : RegionalSettings.DefaultCurrency;
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(RegionalSettings.TryLocale(org?.Locale, out var locale) ? locale : RegionalSettings.DefaultLocale);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }
        return (currency, culture, org?.Locale, org?.Name ?? "", org?.OwnerUserId ?? Guid.Empty, org?.TimeZone);
    }

    /// <summary>An amount as staff read it in the workspace's format: "CHF 1'250.00".</summary>
    public static string Money(decimal amount, string currency, CultureInfo culture) =>
        $"{(amount < 0 ? "-" : "")}{currency} {Math.Abs(amount).ToString("N2", culture)}";

    /// <summary>How many periods of a cadence fall in a year.</summary>
    public static int PeriodsPerYear(string? cadence) => cadence switch
    {
        "Quarterly" => 4,
        "SemiAnnual" => 2,
        "Annual" => 1,
        _ => 12,
    };

    /// <summary>When a membership began: its start when one is set and has come, else the day the member joined.</summary>
    public static DateTime MemberSince(DateTime joinedAtUtc, DateTime? startsOnUtc, DateTime nowUtc) =>
        startsOnUtc is DateTime starts && starts <= nowUtc && starts > DateTime.MinValue ? starts : joinedAtUtc;
}
