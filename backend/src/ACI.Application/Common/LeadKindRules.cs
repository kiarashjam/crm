using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// What a lead must have, by what it is (<see cref="LeadKinds"/>), and the workspace rule
/// on which kinds it takes. A person needs a name and an email, as every lead always has;
/// an establishment needs only its name, as a restaurant found on the street has no email
/// yet. A workspace of people only (a club) takes no establishments from anywhere: the
/// app, the webhook, a capture form or an import.
/// </summary>
public static class LeadKindRules
{
    /// <summary>
    /// The kind a lead is: what was asked for, else the workspace's own. Refused when it is
    /// not a kind, or one the workspace does not take.
    /// </summary>
    public static Result<string> Resolve(string? requested, string? workspaceMode)
    {
        var kind = string.IsNullOrWhiteSpace(requested) ? LeadKinds.DefaultFor(workspaceMode) : requested.Trim().ToLowerInvariant();
        if (kind is not (LeadKinds.Person or LeadKinds.Establishment))
            return new Error("Lead.KindInvalid", "A lead is a \"person\" or an \"establishment\".");
        if (!LeadKinds.Allows(workspaceMode, kind))
            return new Error("Lead.KindNotUsed", workspaceMode == LeadKinds.Person
                ? "This workspace's leads are people. To add establishments too, change what its leads are in Settings › Team."
                : "This workspace's leads are establishments. To add people too, change what its leads are in Settings › Team.");
        return kind;
    }

    /// <summary>The establishment's own fields, checked: a known type, a plausible website and contact email.</summary>
    public static Error? CheckEstablishment(string? type, string? website, string? contactEmail, int? seats)
    {
        if (!string.IsNullOrWhiteSpace(type) && !LeadKinds.EstablishmentTypes.Contains(type.Trim().ToLowerInvariant()))
            return new Error("Lead.EstablishmentType", $"The type is one of {string.Join(", ", LeadKinds.EstablishmentTypes)}.");
        if (!string.IsNullOrWhiteSpace(website) && (website.Trim().Any(char.IsWhiteSpace) || !website.Contains('.')))
            return new Error("Lead.Website", "The website is not a web address, such as pizzeria-roma.ch.");
        if (!string.IsNullOrWhiteSpace(contactEmail) && !ValidationHelper.IsValidEmail(contactEmail.Trim()))
            return new Error("Lead.ContactPersonEmail", "The contact person's email is not an email address.");
        if (seats is < 0 or > 100000)
            return new Error("Lead.Seats", "Seats is a number from 0 to 100,000.");
        return null;
    }

    /// <summary>A type as stored: lower case, or null.</summary>
    public static string? Type(string? type) => string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToLowerInvariant();

    /// <summary>
    /// Where the establishment is, as one line for its company (Company.Location):
    /// "Bahnhofstrasse 1, 8001 Zürich, Switzerland".
    /// </summary>
    public static string? Location(Lead lead)
    {
        var town = string.Join(" ", new[] { lead.PostalCode, lead.City }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        var parts = new[] { lead.AddressLine?.Trim(), town, lead.Country?.Trim() }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (parts.Count == 0) return null;
        var line = string.Join(", ", parts);
        return line.Length > 300 ? line[..300] : line;
    }

    /// <summary>
    /// Who to speak to about a lead: the person, or an establishment's contact person. The
    /// name, email and phone a person contact, an email's greeting or a contract use.
    /// </summary>
    public static (string? Name, string? Email, string? Phone) PersonOf(Lead lead) =>
        lead.Kind == LeadKinds.Establishment
            ? (Blank(lead.ContactPersonName), Blank(lead.ContactPersonEmail), Blank(lead.ContactPersonPhone))
            : (Blank(lead.Name), Blank(lead.Email), Blank(lead.Phone));

    /// <summary>
    /// The address an email about the lead goes to: the person's own, or an establishment's
    /// contact person's, else the establishment's general address.
    /// </summary>
    public static string? EmailOf(Lead lead) => EmailOf(lead.Kind, lead.Email, lead.ContactPersonEmail);

    public static string? EmailOf(string? kind, string? email, string? contactPersonEmail) =>
        kind == LeadKinds.Establishment ? Blank(contactPersonEmail) ?? Blank(email) : Blank(email);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
