using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// The placeholders an event invitation's letter may use, and how each member's are filled in
/// before the email is queued, as a newsletter's are (<see cref="NewsletterTemplate"/>).
/// </summary>
/// <remarks>
/// The letter is plain text, drawn in the club's look by <see cref="ClubEmails.Invitation"/> (its
/// paragraphs, its details as facts, its reply link as the button), every character escaped. A
/// placeholder may give a fallback for a member without the value: <c>{{eventPlace|the club}}</c>.
/// </remarks>
public static partial class InvitationTemplate
{
    public const string FirstName = "firstName";
    public const string Name = "name";
    public const string EventName = "eventName";
    public const string EventDate = "eventDate";
    public const string EventPlace = "eventPlace";
    public const string MemberPrice = "memberPrice";
    public const string ReplyLink = "replyLink";
    public const string Club = "club";

    /// <summary>Every placeholder a letter may use, as the editor offers them.</summary>
    public static readonly IReadOnlyList<string> Fields = [FirstName, Name, EventName, EventDate, EventPlace, MemberPrice, ReplyLink, Club];

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*(?:\|(?<fallback>[^{}]*))?\}\}")]
    private static partial Regex Placeholder();

    /// <summary>One member's values, each already written in their language.</summary>
    public sealed record Values(string FirstName, string Name, string EventName, string EventDate, string? EventPlace, string MemberPrice, string ReplyLink, string? Club);

    /// <summary>Placeholders in the texts that are none of <see cref="Fields"/>, as written ("{{phone}}").</summary>
    public static IReadOnlyList<string> Unknown(params string?[] texts) =>
        texts.SelectMany(t => Placeholder().Matches(t ?? "").Select(m => m.Groups["key"].Value))
            .Where(k => !Fields.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Select(k => "{{" + k + "}}").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Whether the text carries the member's reply link.</summary>
    public static bool HasReplyLink(string? text) =>
        Placeholder().Matches(text ?? "").Any(m => m.Groups["key"].Value.Equals(ReplyLink, StringComparison.OrdinalIgnoreCase));

    /// <summary>The text with each placeholder replaced by the member's value, else its fallback, else nothing.</summary>
    public static string Fill(string? template, Values values)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [FirstName] = values.FirstName,
            [Name] = values.Name,
            [EventName] = values.EventName,
            [EventDate] = values.EventDate,
            [EventPlace] = values.EventPlace,
            [MemberPrice] = values.MemberPrice,
            [ReplyLink] = values.ReplyLink,
            [Club] = values.Club,
        };
        return Placeholder().Replace(template ?? "", match =>
        {
            var key = match.Groups["key"].Value;
            return map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.Trim()
                : match.Groups["fallback"].Success ? match.Groups["fallback"].Value.Trim() : "";
        });
    }
}
