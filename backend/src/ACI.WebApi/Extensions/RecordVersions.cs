using ACI.Application.Common;

namespace ACI.WebApi.Extensions;

/// <summary>
/// A lead's, deal's, contact's or company's version as an HTTP entity tag: sent as ETag
/// when the record is read or saved, and taken back from If-Match on an edit, for a client
/// that would rather use the headers than the <c>version</c> field.
/// </summary>
public static class RecordVersions
{
    private static readonly Error IfMatchUnreadable = new(
        "Version.IfMatchUnreadable",
        "If-Match must be the ETag this record was read with, such as \"0f8fad5b-d9cb-469f-a165-70867728950e\".");

    private static readonly Error VersionsDisagree = new(
        "Version.Disagree",
        "The version in the body and the one in If-Match are different. Send one of them.");

    /// <summary>
    /// The version an edit was made from: the body's, or else the one If-Match names.
    /// Null when neither is there, or If-Match is "*", which any version satisfies.
    /// </summary>
    public static (Guid? Version, Error? Error) ExpectedVersion(this HttpRequest request, Guid? bodyVersion)
    {
        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0 || header == "*") return (bodyVersion, null);
        var tag = header.StartsWith("W/", StringComparison.Ordinal) ? header[2..] : header;
        // Only a tag this API handed out: a list of them, or anything else, cannot be one.
        if (!Guid.TryParse(tag.Trim('"'), out var fromHeader)) return (null, IfMatchUnreadable);
        if (bodyVersion is Guid fromBody && fromBody != fromHeader) return (null, VersionsDisagree);
        return (fromHeader, null);
    }

    /// <summary>Says which version of the record this answer is, so a client can send it back in If-Match.</summary>
    public static void SetETag(this HttpResponse response, Guid? version)
    {
        if (version is Guid v) response.Headers.ETag = $"\"{v}\"";
    }
}
