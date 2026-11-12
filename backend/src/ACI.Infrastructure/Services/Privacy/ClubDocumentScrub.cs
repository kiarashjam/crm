using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>
/// Takes a person out of one of the read-only club documents kept from before the club's
/// records had tables of their own: applications, charges, bookings, event lists and the rest,
/// each a JSON payload.
/// </summary>
/// <remarks>
/// <para>
/// Replacing the person's full name and address in the raw text was not enough. An application
/// keeps the first and the last name in fields of their own, with the phone, the occupation and
/// the reasons for joining beside them, and none of those reads "Anna Keller". The documents were
/// also written with JSON's default escaping, which spells Müller "Müller" and a "+" in an
/// address "+", so a Swiss name or a tagged address never matched at all.
/// </para>
/// <para>
/// So the payload is read as JSON. An object that is the person's (its member, applicant,
/// record or address is theirs) loses their names, addresses, phone numbers and free text; one
/// that only names them in a role (a gift card they bought, a guest they brought) loses that
/// role's name and address. Every other text has their names and addresses replaced, as the
/// rest of the erasure does. Amounts, dates, ids and statuses stay. A payload that is not JSON
/// gets the plain replacement.
/// </para>
/// </remarks>
internal static class ClubDocumentScrub
{
    /// <summary>As ClubDocumentService writes the documents.</summary>
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Keys holding an id that, when it is one of the person's records, makes the object theirs.</summary>
    private static readonly HashSet<string> OwnIdKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "memberId", "applicationId", "leadId", "contactId",
    };

    /// <summary>The person's name in an object that is theirs.</summary>
    private static readonly HashSet<string> NameKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "memberName", "fullName", "displayName", "guestName", "donorName", "applicantName", "customerName", "redeemedByName",
    };

    /// <summary>What an object that is the person's loses outright: free text about them, where they live, when they were born.</summary>
    private static readonly HashSet<string> PersonalTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "notes", "note", "message", "occupation", "reasonForJoining", "reviewerNotes", "preview", "body", "address", "street",
        "city", "postalCode", "zip", "birthDate", "dateOfBirth", "dob", "company", "jobTitle",
    };

    /// <summary>The payload with the person taken out, or as it was when nothing in it is theirs.</summary>
    public static string Scrub(string payload, PersonSubject subject, string pseudonym)
    {
        if (string.IsNullOrEmpty(payload)) return payload;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return subject.Scrub(payload, pseudonym) ?? payload;
        }
        if (root == null) return payload;
        var lastName = pseudonym.StartsWith("Erased ", StringComparison.Ordinal) ? pseudonym["Erased ".Length..] : pseudonym;
        return Walk(root, subject, pseudonym, lastName) is { } replaced ? replaced.ToJsonString(Web) : root.ToJsonString(Web);
    }

    /// <summary>The node with the person taken out: a new value for a text, or the same node changed in place.</summary>
    private static JsonNode? Walk(JsonNode node, PersonSubject s, string pseudonym, string lastName)
    {
        switch (node)
        {
            case JsonObject obj:
                ScrubObject(obj, s, pseudonym, lastName);
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                    if (array[i] is { } item && Walk(item, s, pseudonym, lastName) is var replaced && !ReferenceEquals(replaced, item))
                        array[i] = replaced;
                return array;
            case JsonValue value when value.TryGetValue<string>(out var text):
                var scrubbed = s.Scrub(text, pseudonym);
                return scrubbed == text ? value : JsonValue.Create(scrubbed);
            default:
                return node;
        }
    }

    private static void ScrubObject(JsonObject obj, PersonSubject s, string pseudonym, string lastName)
    {
        // Whose object it is: the person's own, or one that names them in a role ("purchaser",
        // "host", "recipient": the prefix of purchaserMemberId or recipientEmail).
        var own = false;
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            if (Text(value) is not { } text) continue;
            if (Guid.TryParse(text, out var id))
            {
                if (OwnIdKeys.Contains(key) && s.RecordIds.Contains(id)) own = true;
                else if (key.Length > "MemberId".Length && key.EndsWith("MemberId", StringComparison.OrdinalIgnoreCase) && s.MemberIds.Contains(id))
                    roles.Add(key[..^"MemberId".Length]);
            }
            else if (key.EndsWith("email", StringComparison.OrdinalIgnoreCase) && s.Addresses.Contains(ErasedAddresses.Normalize(text)))
            {
                var role = key[..^"email".Length];
                if (role.Length == 0 || role.Equals("member", StringComparison.OrdinalIgnoreCase)) own = true;
                else roles.Add(role);
            }
        }

        foreach (var key in obj.Select(p => p.Key).ToList())
        {
            if (own && Replacement(key, obj[key], s, pseudonym, lastName, out var replacement))
            {
                obj[key] = replacement;
                continue;
            }
            if (roles.Count > 0 && RoleReplacement(key, roles, pseudonym, lastName, out var forRole))
            {
                obj[key] = forRole;
                continue;
            }
            if (obj[key] is { } child && Walk(child, s, pseudonym, lastName) is var walked && !ReferenceEquals(walked, child))
                obj[key] = walked;
        }
    }

    /// <summary>What a key of an object that is the person's becomes; false to leave it to the text replacement.</summary>
    private static bool Replacement(string key, JsonNode? value, PersonSubject s, string pseudonym, string lastName, out JsonNode? replacement)
    {
        replacement = null;
        if (key.Equals("firstName", StringComparison.OrdinalIgnoreCase)) { replacement = "Erased"; return true; }
        if (key.Equals("lastName", StringComparison.OrdinalIgnoreCase)) { replacement = lastName; return true; }
        if (NameKeys.Contains(key)) { replacement = pseudonym; return true; }
        if (PersonalTextKeys.Contains(key)) return true;
        if (key.Contains("phone", StringComparison.OrdinalIgnoreCase) || key.Contains("mobile", StringComparison.OrdinalIgnoreCase)) return true;
        // Their own address goes; another address in the object (whoever referred them) is someone else's.
        return key.EndsWith("email", StringComparison.OrdinalIgnoreCase) && Text(value) is { } text && s.Addresses.Contains(ErasedAddresses.Normalize(text));
    }

    /// <summary>What a key naming the person in a role becomes: purchaserName, hostEmail, recipientPhone…; and the message to or from them.</summary>
    private static bool RoleReplacement(string key, HashSet<string> roles, string pseudonym, string lastName, out JsonNode? replacement)
    {
        replacement = null;
        if (key.Equals("message", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var role in roles)
        {
            if (!key.StartsWith(role, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = key[role.Length..];
            if (rest.Equals("Name", StringComparison.OrdinalIgnoreCase)) { replacement = pseudonym; return true; }
            if (rest.Equals("FirstName", StringComparison.OrdinalIgnoreCase)) { replacement = "Erased"; return true; }
            if (rest.Equals("LastName", StringComparison.OrdinalIgnoreCase)) { replacement = lastName; return true; }
            if (rest.Equals("Email", StringComparison.OrdinalIgnoreCase) || rest.Equals("Phone", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
