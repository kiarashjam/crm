namespace ACI.Application.Common;

/// <summary>
/// An update whose JSON body can clear a field by sending it as null, as a JSON merge
/// patch does. A field left out of the body is left as it is.
/// </summary>
/// <remarks>
/// Every field of these requests is nullable, so a field sent as null and a field not
/// sent at all both arrived as null, and every save read null as "not sent": a contact's
/// company, a deal's close date or a member's phone could be set but never taken off,
/// while the form said it was saved. The API's JSON reader records here which fields the
/// body sent as null (MergePatchJson, in the web project). A request built in code, such
/// as an import row, records none, so it clears nothing.
/// </remarks>
public interface IMergePatchRequest
{
    /// <summary>The properties the request body sent as null, by their C# names.</summary>
    ISet<string> NullsSent { get; }
}

public static class MergePatch
{
    /// <summary>Whether the body sent <paramref name="property"/> as null, which takes the value off.</summary>
    public static bool Clears(this IMergePatchRequest request, string property) => request.NullsSent.Contains(property);
}
