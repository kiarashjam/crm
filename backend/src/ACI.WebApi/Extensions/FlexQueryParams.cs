using ACI.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Extensions;

/// <summary>
/// A list's tag and custom field parameters, read from its query string (see <see cref="FlexFilter"/>):
/// <c>tag</c> and <c>notTag</c> repeated, <c>tagMatch=all</c>, and <c>field=key:op:value</c> repeated.
/// </summary>
public sealed class FlexQueryParams
{
    [FromQuery(Name = "tag")]
    public Guid[]? Tag { get; set; }

    [FromQuery(Name = "tagMatch")]
    public string? TagMatch { get; set; }

    [FromQuery(Name = "notTag")]
    public Guid[]? NotTag { get; set; }

    [FromQuery(Name = "field")]
    public string[]? Field { get; set; }

    /// <summary>The filter, with the list's own sort parameters for a sort by a field ("cf:key").</summary>
    public FlexFilter ToFilter(string? sortBy = null, string? sortDir = null) =>
        FlexFilter.Parse(Tag, TagMatch, NotTag, Field, sortBy, sortDir);
}
