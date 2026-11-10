using System.Text;
using System.Text.Json;

namespace ACI.Application.Common;

/// <summary>
/// Applies a change to a lead's pipeline as a JSON merge patch (RFC 7396): the keys a
/// patch sends replace the stored ones, a key sent as null is removed, and every key it
/// does not send is kept as it is.
/// </summary>
/// <remarks>
/// The pipeline used to be replaced whole by whatever a client sent. A rep with the lead
/// open from before a contract went out recorded "meeting attended" from that older copy,
/// and the contract steps the e-signature had written disappeared, so the lead read as not
/// yet at the contract and its status went backwards. Now each editor sends only the steps
/// it changed, so two people, or a person and the contract flow, working different steps
/// of one lead both keep their work.
/// </remarks>
public static class PipelineStatePatch
{
    /// <summary>As long as a pipeline can be, patch and result alike.</summary>
    public const int MaxLength = 8000;

    public static readonly Error NotAnObject = new(
        "Lead.PipelineStateInvalid", "The pipeline change must be a JSON object of the steps to change.");

    public static readonly Error TooLarge = new(
        "Lead.PipelineStateTooLarge", "The pipeline would be too large to save.");

    /// <summary>
    /// The stored pipeline with <paramref name="patch"/> applied, as compact JSON. A stored
    /// pipeline that is not a JSON object counts as empty, which is how the status rule and
    /// the browser already read it.
    /// </summary>
    public static Result<string> Apply(string? stored, string patch)
    {
        var changes = Parse(patch);
        if (changes is not { RootElement.ValueKind: JsonValueKind.Object })
        {
            changes?.Dispose();
            return NotAnObject;
        }

        using (changes)
        using (var current = Parse(stored))
        {
            var merged = current is { RootElement.ValueKind: JsonValueKind.Object }
                ? ObjectNode.From(current.RootElement)
                : new ObjectNode();
            merged.Merge(changes.RootElement);

            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
                merged.Write(writer);
            var json = Encoding.UTF8.GetString(buffer.ToArray());
            if (json.Length > MaxLength) return TooLarge;
            return json;
        }
    }

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            // Through UTF-8 with replacement, as LeadStatusDerivation reads it: a
            // JavaScript string can hold half of a surrogate pair.
            return JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A JSON object being merged into, its fields kept in the order they came.</summary>
    private sealed class ObjectNode
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, object> _fields = new(StringComparer.Ordinal);

        public static ObjectNode From(JsonElement element)
        {
            var node = new ObjectNode();
            foreach (var property in element.EnumerateObject())
            {
                if (NameOf(property) is not { } name) continue;
                // The last of a repeated key wins, as it does in JSON.parse.
                node.Set(name, property.Value.ValueKind == JsonValueKind.Object ? From(property.Value) : property.Value);
            }
            return node;
        }

        /// <summary>RFC 7396, applied in the order the patch lists its keys.</summary>
        public void Merge(JsonElement patch)
        {
            foreach (var property in patch.EnumerateObject())
            {
                if (NameOf(property) is not { } name) continue;
                var value = property.Value;
                if (value.ValueKind == JsonValueKind.Null)
                {
                    Remove(name);
                }
                else if (value.ValueKind == JsonValueKind.Object)
                {
                    // An object merges into the object already there, or into an empty one,
                    // which drops the nulls inside it.
                    var into = _fields.TryGetValue(name, out var existing) && existing is ObjectNode inner ? inner : new ObjectNode();
                    into.Merge(value);
                    Set(name, into);
                }
                else
                {
                    Set(name, value);
                }
            }
        }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var name in _order)
            {
                writer.WritePropertyName(name);
                if (_fields[name] is ObjectNode inner) inner.Write(writer);
                // Written as it came, escapes and all, so a value is never re-encoded on
                // the way through: half a surrogate pair stays exactly as stored.
                else writer.WriteRawValue(((JsonElement)_fields[name]).GetRawText(), skipInputValidation: true);
            }
            writer.WriteEndObject();
        }

        private void Set(string name, object value)
        {
            if (!_fields.ContainsKey(name)) _order.Add(name);
            _fields[name] = value;
        }

        private void Remove(string name)
        {
            if (_fields.Remove(name)) _order.Remove(name);
        }

        // A name that escapes half of a surrogate pair is valid JSON that .NET will not turn
        // into a string. No step is called that, so it is skipped, as the status rule skips it.
        private static string? NameOf(JsonProperty property)
        {
            try { return property.Name; }
            catch (InvalidOperationException) { return null; }
        }
    }
}
