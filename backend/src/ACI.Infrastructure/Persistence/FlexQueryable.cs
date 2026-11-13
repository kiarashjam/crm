using System.Globalization;
using ACI.Application.Common;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>A list's tag and field conditions with the fields they name, read from the workspace's definitions.</summary>
public sealed record FlexPlan(FlexFilter Filter, IReadOnlyDictionary<string, CustomFieldDefinition> Fields)
{
    public static readonly FlexPlan None = new(FlexFilter.None, new Dictionary<string, CustomFieldDefinition>());

    /// <summary>The field to sort by, when the list asked for one that can be sorted (not a link).</summary>
    public CustomFieldDefinition? SortField =>
        Filter.SortField is { } key && Fields.TryGetValue(key, out var field) && field.Type != CustomFieldTypes.Link ? field : null;
}

/// <summary>
/// Filters and sorts any list of leads, contacts, companies, deals or members by its records'
/// tags and custom fields, in the database.
/// </summary>
/// <remarks>
/// Each condition is a semi-join on RecordTags or CustomFieldEntries ("a row for this field
/// whose number is at least 1000"), which the workspace's query filter scopes and the
/// (workspace, field, value) indexes answer from the index alone: filtering by a field costs
/// the same whichever field it is and however many records the workspace holds. A field sort
/// orders by the one row each record has for the field, records without a value last.
/// </remarks>
public static class FlexQueryable
{
    /// <summary>The fields the filter names, of the records' kind, in the workspace the request acts for.</summary>
    public static async Task<FlexPlan> PlanAsync(AppDbContext db, string entityType, FlexFilter? filter, CancellationToken ct)
    {
        if (filter == null || (!filter.Filters && filter.SortField == null)) return FlexPlan.None;
        var keys = filter.Fields.Select(f => f.Key).Append(filter.SortField).OfType<string>().Distinct().ToList();
        var fields = keys.Count == 0
            ? new Dictionary<string, CustomFieldDefinition>()
            : await db.CustomFieldDefinitions.AsNoTracking()
                .Where(d => d.EntityType == entityType && keys.Contains(d.Key))
                .ToDictionaryAsync(d => d.Key, StringComparer.Ordinal, ct);
        return new FlexPlan(filter, fields);
    }

    public static IQueryable<T> Filter<T>(IQueryable<T> query, AppDbContext db, FlexPlan plan) where T : BaseEntity
    {
        var filter = plan.Filter;
        if (!filter.Filters) return query;
        var tags = db.RecordTags.AsQueryable();
        if (filter.Tags.Count > 0)
        {
            if (filter.AllTags)
            {
                foreach (var tag in filter.Tags)
                {
                    var tagId = tag;
                    query = query.Where(r => tags.Any(t => t.RecordId == r.Id && t.TagId == tagId));
                }
            }
            else
            {
                var tagIds = filter.Tags.ToList();
                query = query.Where(r => tags.Any(t => t.RecordId == r.Id && tagIds.Contains(t.TagId)));
            }
        }
        foreach (var tag in filter.NotTags)
        {
            var tagId = tag;
            query = query.Where(r => !tags.Any(t => t.RecordId == r.Id && t.TagId == tagId));
        }
        foreach (var condition in filter.Fields)
            if (plan.Fields.TryGetValue(condition.Key, out var field)) query = Condition(query, db, field, condition);
        return query;
    }

    private static IQueryable<T> Condition<T>(IQueryable<T> query, AppDbContext db, CustomFieldDefinition field, FieldCondition c) where T : BaseEntity
    {
        var fieldId = field.Id;
        var rows = db.CustomFieldEntries.Where(e => e.FieldId == fieldId);
        switch (c.Op)
        {
            case "empty": return query.Where(r => !rows.Any(e => e.RecordId == r.Id));
            case "set": return query.Where(r => rows.Any(e => e.RecordId == r.Id));
        }

        switch (field.Type)
        {
            case CustomFieldTypes.Checkbox:
                return c.Op switch
                {
                    "true" or "is" when c.Op == "true" || IsYes(c.Value) => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.BoolValue == true)),
                    "false" or "is" => query.Where(r => !rows.Any(e => e.RecordId == r.Id && e.BoolValue == true)),
                    _ => query,
                };

            case CustomFieldTypes.Number or CustomFieldTypes.Money:
            {
                if (c.Op == "between")
                {
                    if (!Number(c.Value, out var low) || !Number(c.To, out var high)) return query;
                    return query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue >= low && e.NumberValue <= high));
                }
                if (!Number(c.Value, out var n)) return query;
                return c.Op switch
                {
                    "eq" or "is" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue == n)),
                    "neq" or "not" => query.Where(r => !rows.Any(e => e.RecordId == r.Id && e.NumberValue == n)),
                    "gt" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue > n)),
                    "gte" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue >= n)),
                    "lt" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue < n)),
                    "lte" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.NumberValue <= n)),
                    _ => query,
                };
            }

            case CustomFieldTypes.Date:
            {
                if (c.Op == "between")
                {
                    if (!Date(c.Value, out var from) || !Date(c.To, out var to)) return query;
                    return query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue >= from && e.DateValue <= to));
                }
                if (!Date(c.Value, out var day)) return query;
                return c.Op switch
                {
                    "is" or "on" or "eq" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue == day)),
                    "before" or "lt" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue < day)),
                    "after" or "gt" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue > day)),
                    "lte" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue <= day)),
                    "gte" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.DateValue >= day)),
                    _ => query,
                };
            }

            case CustomFieldTypes.Link:
                if (!Guid.TryParse(c.Value, out var linked)) return query;
                return c.Op switch
                {
                    "is" => query.Where(r => rows.Any(e => e.RecordId == r.Id && e.LinkId == linked)),
                    "not" => query.Where(r => !rows.Any(e => e.RecordId == r.Id && e.LinkId == linked)),
                    _ => query,
                };

            case CustomFieldTypes.MultiSelect when c.Op == "all":
                foreach (var value in c.Values.Select(Key))
                {
                    var option = value;
                    query = query.Where(r => rows.Any(e => e.RecordId == r.Id && e.TextKey == option));
                }
                return query;
        }

        // Text, a web address, a single or multi select: by the key every text row keeps.
        var values = c.Values.Select(Key).ToList();
        switch (c.Op)
        {
            case "is" when values.Count > 0:
                var wanted = values[0];
                return query.Where(r => rows.Any(e => e.RecordId == r.Id && e.TextKey == wanted));
            case "not" when values.Count > 0:
                var unwanted = values[0];
                return query.Where(r => !rows.Any(e => e.RecordId == r.Id && e.TextKey == unwanted));
            case "any" when values.Count > 0:
                return query.Where(r => rows.Any(e => e.RecordId == r.Id && values.Contains(e.TextKey!)));
            case "none" when values.Count > 0:
                return query.Where(r => !rows.Any(e => e.RecordId == r.Id && values.Contains(e.TextKey!)));
            case "contains" when !string.IsNullOrWhiteSpace(c.Value):
                var part = c.Value!.Trim().ToLower();
                return query.Where(r => rows.Any(e => e.RecordId == r.Id && e.TextValue != null && e.TextValue.ToLower().Contains(part)));
            default:
                return query;
        }
    }

    /// <summary>
    /// The query in the order of the field the list sorts by, records without a value last
    /// whichever way; null when it sorts by no field. The caller adds its own tie-breakers.
    /// </summary>
    public static IOrderedQueryable<T>? Sort<T>(IQueryable<T> query, AppDbContext db, FlexPlan plan) where T : BaseEntity
    {
        if (plan.SortField is not { } field) return null;
        var fieldId = field.Id;
        var desc = plan.Filter.SortDescending;
        var rows = db.CustomFieldEntries.Where(e => e.FieldId == fieldId);
        var ordered = query.OrderBy(r => rows.Any(e => e.RecordId == r.Id) ? 0 : 1);
        return field.Type switch
        {
            CustomFieldTypes.Number or CustomFieldTypes.Money => desc
                ? ordered.ThenByDescending(r => rows.Where(e => e.RecordId == r.Id).Max(e => e.NumberValue))
                : ordered.ThenBy(r => rows.Where(e => e.RecordId == r.Id).Min(e => e.NumberValue)),
            CustomFieldTypes.Date => desc
                ? ordered.ThenByDescending(r => rows.Where(e => e.RecordId == r.Id).Max(e => e.DateValue))
                : ordered.ThenBy(r => rows.Where(e => e.RecordId == r.Id).Min(e => e.DateValue)),
            CustomFieldTypes.Checkbox => desc
                ? ordered.ThenByDescending(r => rows.Any(e => e.RecordId == r.Id && e.BoolValue == true) ? 1 : 0)
                : ordered.ThenBy(r => rows.Any(e => e.RecordId == r.Id && e.BoolValue == true) ? 1 : 0),
            _ => desc
                ? ordered.ThenByDescending(r => rows.Where(e => e.RecordId == r.Id).Max(e => e.TextKey))
                : ordered.ThenBy(r => rows.Where(e => e.RecordId == r.Id).Min(e => e.TextKey)),
        };
    }

    /// <summary>
    /// The ids of records whose custom field text or tag names hold <paramref name="lowered"/>,
    /// for a list's search to take with an OR: a contact is found by their "Membership no." or
    /// a "VIP" tag as well as by their name.
    /// </summary>
    public static IQueryable<Guid> SearchHits(AppDbContext db, string lowered) =>
        db.CustomFieldEntries.Where(e => e.TextValue != null && e.TextValue.ToLower().Contains(lowered)).Select(e => e.RecordId)
            .Concat(db.RecordTags.Where(t => t.Tag.Name.ToLower().Contains(lowered)).Select(t => t.RecordId));

    /// <summary>The key a text value is matched by: its first characters, as the rows keep them.</summary>
    private static string Key(string value) =>
        value.Length <= CustomFieldEntry.TextKeyLength ? value : value[..CustomFieldEntry.TextKeyLength];

    private static bool Number(string? raw, out decimal number) =>
        decimal.TryParse((raw ?? "").Trim().Replace("'", "").Replace("’", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    private static bool Date(string? raw, out DateOnly day) =>
        DateOnly.TryParseExact((raw ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static bool IsYes(string? raw) => raw?.Trim().ToLowerInvariant() is "true" or "yes" or "1";
}
