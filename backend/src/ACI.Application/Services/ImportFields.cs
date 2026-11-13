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
    /// <summary>Person or establishment (<see cref="LeadKinds"/>).</summary>
    LeadKind,
    /// <summary>An establishment's type: one of <see cref="LeadKinds.EstablishmentTypes"/>, from a word for it.</summary>
    EstablishmentType,
    /// <summary>A whole number from 0 to 100,000.</summary>
    Count,
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

    // An establishment's own columns (see LeadKinds), offered where the workspace's leads
    // can be establishments. A workspace of people imports exactly as it always has.
    private static readonly string[] EstablishmentNameHeaders =
    {
        "name", "establishment", "establishment name", "restaurant", "restaurant name", "business", "business name", "venue",
        "company", "company name", "betrieb", "lokal", "restaurantname", "établissement", "etablissement", "nom", "ristorante", "locale",
    };
    private static readonly string[] ContactPersonHeaders =
    {
        "contact person", "contact name", "contact", "kontaktperson", "ansprechpartner", "ansprechperson", "kontakt",
        "personne de contact", "persona di contatto", "referente",
    };

    private static IEnumerable<ImportField> EstablishmentOwn() => new[]
    {
        new ImportField("establishmentType", "Type of establishment", ImportValue.EstablishmentType, 40,
            new[] { "establishment type", "type", "category", "kind of business", "typ", "kategorie", "catégorie", "categorie", "tipo", "categoria" },
            Hint: "Restaurant, café, bar, hotel, bakery, catering, food truck or shop; anything else is Other"),
        new ImportField("addressLine", "Street address", ImportValue.Text, 200,
            new[] { "address", "street", "street address", "address line", "adresse", "strasse", "straße", "rue", "indirizzo", "via" }),
        new ImportField("postalCode", "Postal code", ImportValue.Text, 20,
            new[] { "postal code", "postcode", "zip", "zip code", "plz", "npa", "code postal", "cap" }),
        new ImportField("city", "City", ImportValue.Text, 100,
            new[] { "city", "town", "ort", "stadt", "ortschaft", "ville", "localité", "localite", "città", "citta", "località" }),
        new ImportField("website", "Website", ImportValue.Website, 300,
            new[] { "website", "web site", "web", "url", "homepage", "site", "site web", "webseite", "internet", "sito web" }),
        new ImportField("seats", "Seats", ImportValue.Count, 6,
            new[] { "seats", "covers", "capacity", "sitzplätze", "plätze", "couverts", "places", "coperti", "posti" }),
        new ImportField("contactPersonName", "Contact person", ImportValue.Name, 200, ContactPersonHeaders),
        new ImportField("contactPersonEmail", "Contact person email", ImportValue.Email, 256,
            new[] { "contact email", "contact person email", "contact e-mail", "e-mail kontaktperson", "email contact" }),
        new ImportField("contactPersonPhone", "Contact person phone", ImportValue.Phone, 64,
            new[] { "contact phone", "contact person phone", "telefon kontaktperson", "téléphone contact" }),
    };

    /// <summary>A workspace of establishments: the place is the lead, and the person there its contact.</summary>
    private static readonly IReadOnlyList<ImportField> EstablishmentLeadFields = new ImportField[]
    {
        new("name", "Establishment name", ImportValue.Text, 200, EstablishmentNameHeaders, Required: true),
        new("email", "Email", ImportValue.Email, 254, EmailHeaders, Hint: "The establishment's own address, if it has one"),
        Phone(),
    }.Concat(EstablishmentOwn()).Concat(new ImportField[]
    {
        new("contactPersonRole", "Contact person's role", ImportValue.Text, 100, new[] { "contact role", "role" }.Concat(JobTitleHeaders).ToArray()),
        new("country", "Country", ImportValue.Text, 80, new[] { "country", "land", "pays", "paese", "nation", "country region" }),
        new("source", "Source", ImportValue.Source, 64, new[] { "source", "lead source", "origin", "channel", "quelle", "herkunft", "provenance", "fonte" }),
        new("status", "Status", ImportValue.Status, 50, new[] { "status", "lead status", "stage", "lead stage", "statut", "stato" },
            Hint: "One of the workspace's statuses; any other is imported as the default status"),
        new("description", "Notes", ImportValue.Text, 2000, NotesHeaders),
    }).ToList();

    /// <summary>A workspace of both: a person's columns, a column saying which each row is, and an establishment's.</summary>
    private static readonly IReadOnlyList<ImportField> MixedLeadFields = LeadFields
        .Select(f => f.Key == "email" ? f with { Required = false, Hint = "Needed for a person; optional for an establishment" } : f)
        .Concat(new[]
        {
            new ImportField("kind", "Person or establishment", ImportValue.LeadKind, 20,
                new[] { "kind", "lead type", "record type", "person or establishment", "art", "genre" },
                Hint: "Person or establishment; empty is a person"),
        })
        .Concat(EstablishmentOwn())
        .Concat(new[] { new ImportField("contactPersonRole", "Contact person's role", ImportValue.Text, 100, new[] { "contact role" }) })
        .ToList();

    /// <summary>The lead fields for a workspace whose leads are <paramref name="leadKinds"/> (null: a personal workspace, people).</summary>
    private static IReadOnlyList<ImportField> LeadFieldsFor(string? leadKinds) => leadKinds switch
    {
        LeadKinds.Establishment => EstablishmentLeadFields,
        LeadKinds.Both => MixedLeadFields,
        _ => LeadFields,
    };

    /// <summary>An establishment type from a word for one ("Pizzeria", "Bäckerei"); Other when none fits.</summary>
    public static string EstablishmentType(string text)
    {
        var key = HeaderKey(text).Replace(" ", "");
        if (LeadKinds.EstablishmentTypes.Contains(key)) return key;
        return key switch
        {
            "foodtruck" => "food_truck",
            "ristorante" or "gasthaus" or "gasthof" or "pizzeria" or "brasserie" or "bistro" or "trattoria" or "osteria" or "beizli" or "beiz" => "restaurant",
            "coffee" or "coffeeshop" or "kaffee" or "kaffeehaus" or "cafeteria" or "tearoom" => "cafe",
            "pub" or "lounge" or "weinbar" or "winebar" or "club" => "bar",
            "hotel" or "hostel" or "guesthouse" or "auberge" or "pension" or "albergo" => "hotel",
            "bakery" or "backerei" or "baeckerei" or "boulangerie" or "patisserie" or "konditorei" or "panetteria" or "panificio" => "bakery",
            "traiteur" or "caterer" => "catering",
            "store" or "laden" or "geschaft" or "magasin" or "boutique" or "negozio" or "deli" or "epicerie" => "shop",
            _ => "other",
        };
    }

    /// <summary>"person" or "establishment" from a cell, or null when it is neither.</summary>
    public static string? LeadKind(string text) => HeaderKey(text) switch
    {
        "person" or "people" or "individual" or "personne" or "persona" or "privat" or "private" => LeadKinds.Person,
        "establishment" or "business" or "venue" or "place" or "restaurant" or "company" or "etablissement" or "betrieb" or "lokal"
            or "unternehmen" or "entreprise" or "azienda" or "locale" => LeadKinds.Establishment,
        _ => null,
    };

    /// <summary>Whether this is a kind of record that can be imported.</summary>
    public static bool IsKind(string? kind) => kind is RecordTypes.Lead or RecordTypes.Contact or RecordTypes.Company;

    /// <summary>The fields a file of <paramref name="kind"/> can fill, its custom fields last.</summary>
    /// <param name="leadKinds">What the workspace's leads are (Organization.LeadKinds); null for a personal workspace.</param>
    public static IReadOnlyList<ImportField> For(string kind, IEnumerable<CustomFieldDefinition> custom, string? leadKinds = null)
    {
        var standard = kind switch
        {
            RecordTypes.Lead => LeadFieldsFor(leadKinds),
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
