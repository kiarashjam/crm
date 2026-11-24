using System.Text.Json;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// What the lead's record holds about someone, carried onto the club member made from them: the
/// lead's tags, and the values of the lead's custom fields copied to the member fields with the
/// same key (a lead's "Interests" to the member's "Interests").
/// </summary>
/// <remarks>
/// <para>
/// Every way a member is made from a lead calls it: converting the lead with the welcome
/// (OnboardingService adding the member), an application approved (ClubApplicationService), and
/// a contract countersigned or its signed copy uploaded, which approve the application the same way.
/// </para>
/// <para>
/// Once per member: the first way to get there marks the member (ClubMember.LeadDetailsCopiedAtUtc)
/// in the same transaction as the copy, so a retry, a second way or two at once copy nothing
/// twice, and a tag or value staff removed since is not put back. A value the member already has
/// is never overwritten, and a tag they already carry is not added again. A value goes only to a
/// field of a compatible type (<see cref="LeadFieldCopy"/>).
/// </para>
/// </remarks>
public interface ILeadToMemberCopy
{
    /// <summary>
    /// Copies the lead's tags and same-key custom field values onto the member, unless they were
    /// copied for this member before. <paramref name="leadId"/> names the lead when the caller
    /// knows it; otherwise the member's own lead. Nothing for a lead or member not in the workspace.
    /// </summary>
    /// <param name="actorUserId">Who made the member: the tags and values are theirs, as if they had set them.</param>
    Task<LeadToMemberCopyResult> CopyAsync(Guid organizationId, Guid memberId, Guid? leadId, Guid actorUserId, CancellationToken ct = default);
}

/// <param name="Copied">False when nothing was done: copied for this member before, or no lead to copy from.</param>
/// <param name="Tags">Tags put on the member.</param>
/// <param name="Fields">Custom fields given a value.</param>
public sealed record LeadToMemberCopyResult(bool Copied, int Tags, int Fields)
{
    public static readonly LeadToMemberCopyResult None = new(false, 0, 0);
}

/// <summary>How one lead field's value becomes a member field's.</summary>
public static class LeadFieldCopy
{
    /// <summary>
    /// Whether a lead field's value can go to a member field: the same kind of field (a link to the
    /// same kind of record), or a single choice into a field of several choices.
    /// </summary>
    public static bool Compatible(CustomFieldDefinition from, CustomFieldDefinition to) =>
        from.Type == to.Type
            ? from.Type != CustomFieldTypes.Link || string.Equals(from.LinkEntityType, to.LinkEntityType, StringComparison.Ordinal)
            : from.Type == CustomFieldTypes.Select && to.Type == CustomFieldTypes.MultiSelect;

    /// <summary>
    /// The member field's rows for the lead field's value: checked against the member field as
    /// any value is, and for a field with options only the options the member field has, in its
    /// own spelling. None when the kinds do not fit or nothing of the value fits the member field.
    /// The record, workspace and owner are the caller's to fill.
    /// </summary>
    public static List<CustomFieldEntry> Rows(CustomFieldDefinition from, IReadOnlyCollection<CustomFieldEntry> rows, CustomFieldDefinition to)
    {
        if (rows.Count == 0 || !Compatible(from, to)) return [];
        var value = CustomFieldValues.Read(from, rows);
        if (CustomFieldValues.IsEmpty(value)) return [];
        if (CustomFieldTypes.HasOptions(to.Type))
        {
            var options = CustomFieldValues.Options(to);
            var given = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
            var chosen = given
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => options.FirstOrDefault(o => string.Equals(o, v.GetString()!.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (chosen.Count == 0) return [];
            value = to.Type == CustomFieldTypes.MultiSelect
                ? JsonSerializer.SerializeToElement(chosen)
                : JsonSerializer.SerializeToElement(chosen[0]);
        }
        var (ok, normalized, _) = CustomFieldService.Normalize(to, value);
        return ok && !CustomFieldValues.IsEmpty(normalized) ? CustomFieldValues.Rows(to, normalized) : [];
    }
}
