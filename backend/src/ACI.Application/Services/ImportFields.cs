using System.Globalization;
using System.Text;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>How the importer reads a cell for a field.</summary>
internal enum ImportValue
{
    Text,
    /// <summary>A person's whole name.</summary>
    Name,
    /// <summary>A first or a last name, joined into the name.</summary>
    NamePart,
    Email,
    Phone,
    /// <summary>A company's name, found or added and then linked.</summary>
    Company,
    /// <summary>One of the workspace's lead statuses.</summary>
    Status,
    /// <summary>A lead source: one of the workspace's, or any text.</summary>
    Source,
    Domain,
    Website,
    YesNo,
    Custom,
}

/// <summary>A field a column of an import can go into.</summary>
/// <param name="MaxLength">The most characters the field stores: the tighter of the API's limit and the column's.</param>
/// <param name="Headers">Column headers that suggest it, in English, German, French and Italian.</param>
internal sealed record ImportField(string Key, string Label, ImportValue Value, int MaxLength, string[] Headers, bool Required = false, string? Hint = null)
{
    /// <summary>For a custom field, its definition.</summary>
    public CustomFieldDefinition? Definition { get; init; }

    public ImportFieldDto ToDto() => new(Key, Label, Required, Definition != null, Hint);
}

/// <summary>
/// The fields each kind of file can fill, and the column headers that suggest them.
/// </summary>
/// <remarks>
/// The browser's import offered six fields and sent five: the company column it
/// mapped and previewed was never sent. Every field here is written. Custom fields
/// are offered as "custom:" and their key, and their values are checked as the
/// record's own form checks them.
/// </remarks>
internal static class ImportFields
{
    public const string CustomPrefix = "custom:";

    private static readonly string[] NameHeaders =
    {
        "name", "full name", "fullname", "contact name", "contact", "lead name", "person", "kontakt", "kontaktname",
        "vollständiger name", "nom complet", "nom et prénom", "nome completo",
    };
    private static readonly string[] FirstNameHeaders = { "first name", "firstname", "first", "given name", "forename", "vorname", "prénom", "prenom", "nome" };
    private static readonly string[] LastNameHeaders = { "last name", "lastname", "last", "surname", "family name", "nachname", "familienname", "nom", "nom de famille", "cognome" };
    private static readonly string[] EmailHeaders =
    {
        "email", "e-mail", "email address", "e-mail address", "mail", "work email", "e-mail-adresse", "emailadresse", "mailadresse",
        "courriel", "adresse e-mail", "adresse email", "indirizzo email",
    };
    private static readonly string[] PhoneHeaders =
    {
        "phone", "phone number", "telephone", "tel", "mobile", "mobile phone", "cell", "cell phone", "telefon", "telefonnummer",
        "handy", "natel", "mobil", "téléphone", "portable", "numéro de téléphone", "telefono", "cellulare",
    };
    private static readonly string[] CompanyHeaders =
    {
        "company", "company name", "organization", "organisation", "account", "account name", "employer", "firma", "unternehmen",
        "firmenname", "société", "societe", "entreprise", "azienda", "ditta",
    };
    private static readonly string[] JobTitleHeaders = { "job title", "title", "position", "role", "job", "funktion", "berufsbezeichnung", "fonction", "poste", "ruolo" };
    private static readonly string[] NotesHeaders =
    {
        "notes", "note", "description", "comments", "comment", "remarks", "notizen", "notiz", "bemerkung", "bemerkungen",
        "kommentar", "beschreibung", "remarques", "commentaire", "commentaires", "descrizione",
    };
    private static readonly string[] IndustryHeaders = { "industry", "sector", "branche", "secteur", "industrie", "settore" };
    private static readonly string[] SizeHeaders =
    {
        "company size", "size", "employees", "number of employees", "headcount", "grösse", "grosse", "unternehmensgrösse",
        "mitarbeitende", "mitarbeiter", "taille", "effectif", "dimensione",
    };

    private static ImportField FullName() => new("name", "Full name", ImportValue.Name, 200, NameHeaders, Required: true, Hint: "Or a first name and a last name column");
    private static ImportField FirstName() => new("firstName", "First name", ImportValue.NamePart, 200, FirstNameHeaders);
    private static ImportField LastName() => new("lastName", "Last name", ImportValue.NamePart, 200, LastNameHeaders);
    private static ImportField Email() => new("email", "Email", ImportValue.Email, 254, EmailHeaders, Required: true);
    private static ImportField Phone() => new("phone", "Phone", ImportValue.Phone, 50, PhoneHeaders);
    private static ImportField CompanyName() => new("company", "Company", ImportValue.Company, 200, CompanyHeaders,
        Hint: "Linked to the company of that name, which is added if there is none");

    private static readonly IReadOnlyList<ImportField> LeadFields = new[]
    {
        FullName(), FirstName(), LastName(), Email(), Phone(), CompanyName(),
        new ImportField("jobTitle", "Job title", ImportValue.Text, 200, JobTitleHeaders),
        // The column holds 64, though the API's own limit says 100.
        new ImportField("source", "Source", ImportValue.Source, 64, new[] { "source", "lead source", "origin", "channel", "quelle", "herkunft", "provenance", "fonte" }),
        new ImportField("status", "Status", ImportValue.Status, 50, new[] { "status", "lead status", "stage", "lead stage", "statut", "stato" },
            Hint: "One of the workspace's statuses; any other is imported as the default status"),
        new ImportField("country", "Country", ImportValue.Text, 80, new[] { "country", "land", "pays", "paese", "nation", "country region" }),
        new ImportField("industry", "Industry", ImportValue.Text, 120, IndustryHeaders),
        new ImportField("companySize", "Company size", ImportValue.Text, 40, SizeHeaders),
        new ImportField("description", "Notes", ImportValue.Text, 2000, NotesHeaders),
    };

    private static readonly IReadOnlyList<ImportField> ContactFields = new[]
    {
        FullName(), FirstName(), LastName(), Email(), Phone(), CompanyName(),
        new ImportField("jobTitle", "Job title", ImportValue.Text, 100, JobTitleHeaders),
        new ImportField("description", "Notes", ImportValue.Text, 4000, NotesHeaders),
        new ImportField("doNotContact", "Do not contact", ImportValue.YesNo, 10,
            new[] { "do not contact", "dnc", "opt out", "opted out", "unsubscribed", "nicht kontaktieren", "ne pas contacter" },
            Hint: "Yes or no. A yes stops email to them; a no never clears a yes already set"),
    };

    private static readonly IReadOnlyList<ImportField> CompanyFields = new[]
    {
        new ImportField("name", "Company name", ImportValue.Text, 200, new[] { "name" }.Concat(CompanyHeaders).ToArray(), Required: true),
        new ImportField("domain", "Domain", ImportValue.Domain, 253, new[] { "domain", "domain name", "email domain", "domaine", "domäne" },
            Hint: "Such as example.ch"),
        new ImportField("website", "Website", ImportValue.Website, 500,
            new[] { "website", "web site", "web", "url", "homepage", "site", "site web", "webseite", "internet", "sito web" }),
        new ImportField("industry", "Industry", ImportValue.Text, 100, IndustryHeaders),
        new ImportField("size", "Size", ImportValue.Text, 50, SizeHeaders),
        new ImportField("location", "Location", ImportValue.Text, 300,
            new[] { "location", "city", "address", "town", "ort", "stadt", "adresse", "standort", "ville", "lieu", "città", "indirizzo" }),
        new ImportField("description", "Notes", ImportValue.Text, 2000, NotesHeaders),
    };

    /// <summary>Whether this is a kind of record that can be imported.</summary>
    public static bool IsKind(string? kind) => kind is RecordTypes.Lead or RecordTypes.Contact or RecordTypes.Company;

    /// <summary>The fields a file of <paramref name="kind"/> can fill, its custom fields last.</summary>
    public static IReadOnlyList<ImportField> For(string kind, IEnumerable<CustomFieldDefinition> custom)
    {
        var standard = kind switch
        {
            RecordTypes.Lead => LeadFields,
            RecordTypes.Contact => ContactFields,
            RecordTypes.Company => CompanyFields,
            _ => Array.Empty<ImportField>(),
        };
        var customFields = custom
            .OrderBy(d => d.Order)
            .Select(d => new ImportField(CustomPrefix + d.Key, d.Label, ImportValue.Custom, 2000, new[] { d.Label, d.Key })
            {
                Definition = d,
            });
        return standard.Concat(customFields).ToList();
    }

    /// <summary>
    /// The field each header suggests, left to right; a field goes to the first column
    /// that names it. Only whole names match, so "Mailing list" is not taken for an email.
    /// </summary>
    public static IReadOnlyList<string?> Suggest(IReadOnlyList<string> headers, IReadOnlyList<ImportField> fields)
    {
        var byHeader = new Dictionary<string, ImportField>(StringComparer.Ordinal);
        foreach (var field in fields)
            foreach (var header in field.Headers)
                byHeader.TryAdd(HeaderKey(header), field);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var suggested = new List<string?>();
        foreach (var header in headers)
        {
            var field = byHeader.TryGetValue(HeaderKey(header), out var f) && taken.Add(f.Key) ? f.Key : null;
            suggested.Add(field);
        }
        return suggested;
    }

    /// <summary>A header as compared: lower case, without accents, punctuation as single spaces. "E-Mail" is "e mail".</summary>
    public static string HeaderKey(string header)
    {
        var folded = header.Normalize(NormalizationForm.FormD);
        var key = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) key.Append(char.ToLowerInvariant(c));
            else if (key.Length > 0 && key[^1] != ' ') key.Append(' ');
        }
        return key.ToString().Trim();
    }
}
