using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Turns a lead and the org's rules into a 0–100 score.
/// Built-in rules apply only when the org has saved none.
/// </summary>
public static class LeadScoreCalculator
{
    public static IReadOnlyList<LeadScoringRule> BuiltinRules() =>
    [
        new() { Field = "source", MatchValue = "referral", Points = 20 },
        new() { Field = "source", MatchValue = "website", Points = 10 },
        new() { Field = "source", MatchValue = "ads", Points = 5 },
        new() { Field = "hasPhone", Points = 10 },
        new() { Field = "hasJobTitle", Points = 10 },
        new() { Field = "hasCompany", Points = 15 },
        new() { Field = "contactedWithinDays", MatchValue = "7", Points = 15 },
        new() { Field = "emailOpens", MatchValue = "20", Points = 5 },
        new() { Field = "status", MatchValue = "qualified", Points = 20 },
        new() { Field = "staleDays", MatchValue = "3", Points = -10 },
    ];

    public static int Score(Lead lead, IReadOnlyList<LeadScoringRule> rules, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var active = rules.Where(r => r.IsActive).ToList();
        if (active.Count == 0) active = BuiltinRules().ToList();

        var total = 0;
        foreach (var rule in active)
            total += PointsFor(lead, rule, now);

        return Math.Clamp(total, 0, 100);
    }

    private static int PointsFor(Lead lead, LeadScoringRule rule, DateTime now)
    {
        var field = rule.Field.Trim().ToLowerInvariant();
        var match = rule.MatchValue?.Trim() ?? "";
        return field switch
        {
            "source" => Contains(lead.Source, match) ? rule.Points : 0,
            "status" => Contains(lead.Status, match) ? rule.Points : 0,
            "country" => EqualsFold(lead.Country, match) ? rule.Points : 0,
            "industry" => Contains(lead.Industry, match) ? rule.Points : 0,
            "hasphone" => string.IsNullOrWhiteSpace(lead.Phone) ? 0 : rule.Points,
            "hasjobtitle" => string.IsNullOrWhiteSpace(lead.JobTitle) ? 0 : rule.Points,
            "hascompany" => lead.CompanyId == null ? 0 : rule.Points,
            "contactedwithindays" => ContactedWithin(lead, match, now) ? rule.Points : 0,
            "emailopens" => Opens(lead, match, rule.Points),
            "staledays" => IsStale(lead, match, now) ? rule.Points : 0,
            _ => 0,
        };
    }

    private static int Opens(Lead lead, string capText, int perOpen)
    {
        var cap = int.TryParse(capText, out var n) ? n : 20;
        return Math.Min(cap, Math.Max(0, lead.EmailOpenCount) * perOpen);
    }

    private static bool ContactedWithin(Lead lead, string daysText, DateTime now)
    {
        if (lead.LastContactedAt == null) return false;
        var days = int.TryParse(daysText, out var n) ? n : 7;
        return lead.LastContactedAt.Value >= now.AddDays(-days);
    }

    private static bool IsStale(Lead lead, string daysText, DateTime now)
    {
        if (lead.LastContactedAt != null || lead.FirstResponseAtUtc != null) return false;
        var days = int.TryParse(daysText, out var n) ? n : 3;
        return lead.CreatedAtUtc <= now.AddDays(-days);
    }

    private static bool Contains(string? value, string match)
    {
        if (string.IsNullOrWhiteSpace(match) || string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains(match, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsFold(string? value, string match) =>
        !string.IsNullOrWhiteSpace(match)
        && string.Equals(value?.Trim(), match, StringComparison.OrdinalIgnoreCase);
}
