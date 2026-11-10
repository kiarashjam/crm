using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ACI.Application.Common;

namespace ACI.WebApi.Extensions;

/// <summary>
/// Tells, for an update request, a field sent as null from a field not sent at all (see
/// <see cref="IMergePatchRequest"/>).
/// </summary>
/// <remarks>
/// The JSON reader sets a property only when the body names it, so wrapping each
/// property's setter is enough: when it is set to null, the property's name is noted on
/// the request. Nothing else about reading or writing JSON changes.
/// </remarks>
public static class MergePatchJson
{
    /// <summary>Has <paramref name="options"/> note the fields each update request's body sends as null.</summary>
    public static void TrackNullsSent(JsonSerializerOptions options) =>
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RecordNullsSent } };

    private static void RecordNullsSent(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(IMergePatchRequest).IsAssignableFrom(typeInfo.Type)) return;
        foreach (var property in typeInfo.Properties)
        {
            if (property.Set is not { } set) continue;
            var name = (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;
            property.Set = (target, value) =>
            {
                if (value is null) ((IMergePatchRequest)target).NullsSent.Add(name);
                set(target, value);
            };
        }
    }
}
