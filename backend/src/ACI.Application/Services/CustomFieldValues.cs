using System.Globalization;
using System.Text.Json;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// How a custom field's value is written as JSON (what the API reads and sends) and as
/// typed rows (what the database keeps, <see cref="CustomFieldEntry"/>).
/// </summary>
/// <remarks>
/// <para>The JSON of each kind of field:</para>
/// <list type="bullet">
/// <item>text, web address, single select: a string;</item>
/// <item>number: a number;</item>
/// <item>money: <c>{"amount": 1200.5, "currency": "CHF"}</c>;</item>
/// <item>date: <c>"2026-11-13"</c>;</item>
/// <item>checkbox: true or false;</item>
/// <item>multi select: an array of its options, <c>["Golf", "Tennis"]</c>;</item>
/// <item>link: <c>{"id": "…"}</c>, with <c>"name"</c> added where the linked record is read back.</item>
/// </list>
/// Values only ever reach here after <see cref="CustomFieldService.Normalize"/> has checked them.
/// </remarks>
public static class CustomFieldValues
{
    private static readonly JsonElement None = JsonSerializer.SerializeToElement<object?>(null);

    /// <summary>Whether a checked value is empty: null, an empty array.</summary>
    public static bool IsEmpty(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    /// <summary>The typed rows for a checked value; none for an empty one. Record, workspace and owner are the caller's to fill.</summary>
    public static List<CustomFieldEntry> Rows(CustomFieldDefinition field, JsonElement value)
    {
        var rows = new List<CustomFieldEntry>();
        if (IsEmpty(value)) return rows;
        CustomFieldEntry Row() => new() { Id = Guid.NewGuid(), FieldId = field.Id, EntityType = field.EntityType, UpdatedAtUtc = DateTime.UtcNow };

        switch (field.Type)
        {
            case CustomFieldTypes.Number:
                rows.Add(With(Row(), r => r.NumberValue = value.GetDecimal()));
                break;
            case CustomFieldTypes.Money:
                rows.Add(With(Row(), r =>
                {
                    r.NumberValue = value.GetProperty("amount").GetDecimal();
                    r.Currency = value.GetProperty("currency").GetString();
                }));
                break;
            case CustomFieldTypes.Date:
                rows.Add(With(Row(), r => r.DateValue = DateOnly.ParseExact(value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
                break;
            case CustomFieldTypes.Checkbox:
                rows.Add(With(Row(), r => r.BoolValue = value.GetBoolean()));
                break;
            case CustomFieldTypes.Link:
                rows.Add(With(Row(), r => r.LinkId = value.GetProperty("id").GetGuid()));
                break;
            case CustomFieldTypes.MultiSelect:
                foreach (var option in value.EnumerateArray())
                    rows.Add(WithText(Row(), option.GetString()!));
                break;
            default:
                rows.Add(WithText(Row(), value.GetString()!));
                break;
        }
        return rows;
    }

    /// <summary>The value its rows make up, as JSON; null when there are none. Link names are not filled in here.</summary>
    public static JsonElement Read(CustomFieldDefinition field, IReadOnlyCollection<CustomFieldEntry> rows)
    {
        if (rows.Count == 0) return None;
        var row = rows.First();
        return field.Type switch
        {
            CustomFieldTypes.Number when row.NumberValue is decimal n => JsonSerializer.SerializeToElement(Trim(n)),
            CustomFieldTypes.Money when row.NumberValue is decimal amount =>
                JsonSerializer.SerializeToElement(new { amount = Trim(amount), currency = row.Currency ?? field.Currency }),
            CustomFieldTypes.Date when row.DateValue is DateOnly d => JsonSerializer.SerializeToElement(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            CustomFieldTypes.Checkbox when row.BoolValue is bool b => JsonSerializer.SerializeToElement(b),
            CustomFieldTypes.Link when row.LinkId is Guid id => JsonSerializer.SerializeToElement(new { id }),
            CustomFieldTypes.MultiSelect => JsonSerializer.SerializeToElement(
                rows.Where(r => r.TextValue != null).Select(r => r.TextValue!).OrderBy(Position(field)).ToList()),
            CustomFieldTypes.Number or CustomFieldTypes.Money or CustomFieldTypes.Date or CustomFieldTypes.Checkbox or CustomFieldTypes.Link => None,
            _ => row.TextValue == null ? None : JsonSerializer.SerializeToElement(row.TextValue),
        };
    }

    /// <summary>
    /// The value as one line of text, for a CSV cell or a search result: "1'200.50 CHF" is written
    /// "1200.50 CHF", a multi select "Golf, Tennis", a link the record's name, a checkbox Yes or No.
    /// </summary>
    public static string Text(CustomFieldDefinition field, IReadOnlyCollection<CustomFieldEntry> rows, IReadOnlyDictionary<Guid, string>? linkNames = null)
    {
        if (rows.Count == 0) return string.Empty;
        var row = rows.First();
        return field.Type switch
        {
            CustomFieldTypes.Number => row.NumberValue is decimal n ? Trim(n).ToString(CultureInfo.InvariantCulture) : "",
            CustomFieldTypes.Money => row.NumberValue is decimal amount
                ? $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {row.Currency ?? field.Currency}".Trim()
                : "",
            CustomFieldTypes.Date => row.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            CustomFieldTypes.Checkbox => row.BoolValue switch { true => "Yes", false => "No", _ => "" },
            CustomFieldTypes.Link => row.LinkId is Guid id ? (linkNames != null && linkNames.TryGetValue(id, out var name) ? name : id.ToString()) : "",
            CustomFieldTypes.MultiSelect => string.Join(", ", rows.Where(r => r.TextValue != null).Select(r => r.TextValue!).OrderBy(Position(field))),
            _ => row.TextValue ?? "",
        };
    }

    /// <summary>The options a select field offers, in their order.</summary>
    public static List<string> Options(CustomFieldDefinition field) =>
        field.OptionsJson == null ? new List<string>() : JsonSerializer.Deserialize<List<string>>(field.OptionsJson) ?? new List<string>();

    private static Func<string, int> Position(CustomFieldDefinition field)
    {
        var options = Options(field);
        return value =>
        {
            var index = options.FindIndex(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        };
    }

    private static CustomFieldEntry WithText(CustomFieldEntry row, string text)
    {
        row.TextValue = text;
        row.TextKey = text.Length <= CustomFieldEntry.TextKeyLength ? text : text[..CustomFieldEntry.TextKeyLength];
        return row;
    }

    private static CustomFieldEntry With(CustomFieldEntry row, Action<CustomFieldEntry> set)
    {
        set(row);
        return row;
    }

    /// <summary>A stored number without the column's trailing zeros: 12.5, not 12.50000000.</summary>
    private static decimal Trim(decimal value) => value / 1.000000000000000000000000000000000m;
}
