using System.Globalization;
using System.Text.RegularExpressions;

namespace ACI.Application.Common.I18n;

/// <summary>
/// The words of the recap emails, in English, German (Swiss spelling: "ss", never "ß") and French
/// (as written in Romandy). Written to staff, not members, in the language each person chose for
/// the app.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="MemberTexts"/>, which is what the club writes to its members. The
/// same rules hold and the same test checks them (RecapTextsTests): all three languages for every
/// entry, the English placeholders in each, no "ß", and a no-break space before : ? ! ; in French.
/// The date patterns are .NET's, filled with the reader's language and the workspace's region.
/// </remarks>
public static partial class RecapTexts
{
    private sealed record Entry(string En, string De, string Fr)
    {
        public string In(string language) => language switch
        {
            AppLanguages.German => De,
            AppLanguages.French => Fr,
            _ => En,
        };
    }

    private const string Nb = " ";

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal)
    {
        // ── dates, as .NET patterns ────────────────────────────────────────────
        ["date.day"] = new("dddd d MMMM yyyy", "dddd, d. MMMM yyyy", "dddd d MMMM yyyy"),
        ["date.rangeDay"] = new("d MMMM yyyy", "d. MMMM yyyy", "d MMMM yyyy"),
        ["date.month"] = new("MMMM yyyy", "MMMM yyyy", "MMMM yyyy"),

        // ── the subject, the heading and the first lines ────────────────────────
        ["subject.daily"] = new("{workspace} · Daily recap · {period}", "{workspace} · Tagesrückblick · {period}", "{workspace} · Récapitulatif du jour · {period}"),
        ["subject.weekly"] = new("{workspace} · Weekly recap · {period}", "{workspace} · Wochenrückblick · {period}", "{workspace} · Récapitulatif de la semaine · {period}"),
        ["subject.monthly"] = new("{workspace} · Monthly recap · {period}", "{workspace} · Monatsrückblick · {period}", "{workspace} · Récapitulatif du mois · {period}"),
        ["title.daily"] = new("Daily recap", "Tagesrückblick", "Récapitulatif du jour"),
        ["title.weekly"] = new("Weekly recap", "Wochenrückblick", "Récapitulatif de la semaine"),
        ["title.monthly"] = new("Monthly recap", "Monatsrückblick", "Récapitulatif du mois"),
        ["range"] = new("{first} – {last}", "{first} – {last}", "{first} – {last}"),
        ["greeting"] = new("Hi {name},", "Guten Tag {name},", "Bonjour {name},"),
        ["greetingNoName"] = new("Hello,", "Guten Tag,", "Bonjour,"),
        ["intro.daily"] = new(
            "What happened at {workspace} on {period}, each figure beside the day before.",
            "Was bei {workspace} am {period} geschah, jede Zahl neben jener des Vortags.",
            "Ce qui s’est passé chez {workspace} le {period}, chaque chiffre à côté de celui de la veille."),
        ["intro.weekly"] = new(
            "What happened at {workspace} from {first} to {last}, each figure beside the week before.",
            "Was bei {workspace} vom {first} bis {last} geschah, jede Zahl neben jener der Vorwoche.",
            "Ce qui s’est passé chez {workspace} du {first} au {last}, chaque chiffre à côté de celui de la semaine précédente."),
        ["intro.monthly"] = new(
            "What happened at {workspace} in {period}, each figure beside the month before.",
            "Was bei {workspace} im {period} geschah, jede Zahl neben jener des Vormonats.",
            "Ce qui s’est passé chez {workspace} en {period}, chaque chiffre à côté de celui du mois précédent."),

        // ── the columns ─────────────────────────────────────────────────────────
        ["col.daily"] = new("Day", "Tag", "Jour"),
        ["col.dailyBefore"] = new("Day before", "Vortag", "Veille"),
        ["col.weekly"] = new("Week", "Woche", "Semaine"),
        ["col.weeklyBefore"] = new("Week before", "Vorwoche", "Semaine précédente"),
        ["col.monthly"] = new("Month", "Monat", "Mois"),
        ["col.monthlyBefore"] = new("Month before", "Vormonat", "Mois précédent"),
        ["col.now"] = new("Now", "Jetzt", "Aujourd’hui"),

        // ── the parts ───────────────────────────────────────────────────────────
        ["section.leads"] = new("Leads", "Leads", "Prospects"),
        ["section.meetings"] = new("Meetings", "Termine", "Rendez-vous"),
        ["section.contracts"] = new("Contracts and deposits", "Verträge und Anzahlungen", "Contrats et acomptes"),
        ["section.conversions"] = new("Conversions", "Abschlüsse", "Conversions"),
        ["section.pipeline"] = new("Pipeline now", "Pipeline jetzt", "Pipeline aujourd’hui"),
        ["section.club"] = new("The club", "Der Club", "Le club"),

        ["leads.new"] = new("New leads", "Neue Leads", "Nouveaux prospects"),
        ["leads.noSource"] = new("No source", "Ohne Quelle", "Sans source"),
        ["leads.otherSources"] = new("Other sources", "Andere Quellen", "Autres sources"),
        ["leads.referred"] = new("Through a referral", "Durch eine Empfehlung", "Par recommandation"),
        ["leads.lost"] = new("Marked lost or not interested", "Als verloren oder nicht interessiert markiert", "Marqués perdus ou pas intéressés"),

        ["meetings.invited"] = new("Invitations to book a meeting sent", "Einladungen zur Terminbuchung versandt", "Invitations à prendre rendez-vous envoyées"),
        ["meetings.booked"] = new("Meetings booked", "Gebuchte Termine", "Rendez-vous pris"),
        ["meetings.held"] = new("Meetings held", "Durchgeführte Termine", "Rendez-vous tenus"),
        ["meetings.cancelled"] = new("Meetings cancelled", "Abgesagte Termine", "Rendez-vous annulés"),

        ["contracts.sent"] = new("Contracts sent", "Versandte Verträge", "Contrats envoyés"),
        ["contracts.signed"] = new("Contracts signed", "Unterzeichnete Verträge", "Contrats signés"),
        ["contracts.deposits"] = new("Deposits paid", "Bezahlte Anzahlungen", "Acomptes payés"),
        ["contracts.depositAmount"] = new("Deposits received in {currency}", "Erhaltene Anzahlungen in {currency}", "Acomptes reçus en {currency}"),

        ["conversions.converted"] = new("Leads converted", "Umgewandelte Leads", "Prospects convertis"),
        ["conversions.won"] = new("Deals won", "Gewonnene Deals", "Affaires gagnées"),
        ["conversions.wonValue"] = new("Won in {currency}", "Gewonnen in {currency}", "Gagné en {currency}"),
        ["conversions.withoutValue"] = new(
            "Won deals whose value could not be read: {count}. They are counted, not added in.",
            "Gewonnene Deals ohne lesbaren Betrag: {count}. Sie sind gezählt, aber nicht addiert.",
            $"Affaires gagnées sans montant lisible{Nb}: {{count}}. Elles sont comptées, pas additionnées."),
        ["conversions.members"] = new("New club members", "Neue Clubmitglieder", "Nouveaux membres du club"),

        ["pipeline.intro"] = new("Open leads at each phase, as they stand now.", "Offene Leads je Phase, Stand jetzt.", "Prospects en cours à chaque phase, à ce jour."),
        ["pipeline.1"] = new("Outreach", "Kontaktaufnahme", "Prise de contact"),
        ["pipeline.2"] = new("Meeting", "Termin", "Rendez-vous"),
        ["pipeline.3"] = new("Contract", "Vertrag", "Contrat"),
        ["pipeline.4"] = new("Signature", "Unterschrift", "Signature"),
        ["pipeline.5"] = new("Deposit", "Anzahlung", "Acompte"),

        ["club.visits"] = new("Visits", "Besuche", "Visites"),
        ["club.events"] = new("Events held", "Durchgeführte Anlässe", "Événements tenus"),
        ["club.attendance"] = new("Attendance at them", "Teilnehmende daran", "Participants à ces événements"),
        ["club.newMembers"] = new("New members", "Neue Mitglieder", "Nouveaux membres"),
        ["club.atRisk"] = new("Members at risk now", "Gefährdete Mitglieder jetzt", "Membres à risque aujourd’hui"),
        ["club.renewals"] = new("Renewals due in the next 30 days", "Erneuerungen in den nächsten 30 Tagen", "Renouvellements dans les 30 prochains jours"),

        // ── the plain-text lines ────────────────────────────────────────────────
        ["text.line"] = new("{label}: {value}", "{label}: {value}", $"{{label}}{Nb}: {{value}}"),
        ["text.beside"] = new("{now} (previously {before})", "{now} (zuvor {before})", "{now} (auparavant {before})"),

        // ── the foot ────────────────────────────────────────────────────────────
        ["amounts"] = new("Amounts are per currency and never converted.", "Beträge sind je Währung angegeben und nie umgerechnet.", "Les montants sont indiqués par monnaie et jamais convertis."),
        ["link.dashboard"] = new("Open the dashboard", "Dashboard öffnen", "Ouvrir le tableau de bord"),
        ["link.reports"] = new("Open the reports", "Berichte öffnen", "Ouvrir les rapports"),
        ["link.app"] = new("Open CadenceE", "CadenceE öffnen", "Ouvrir CadenceE"),
        ["link.settings"] = new("Your notification settings", "Ihre Benachrichtigungseinstellungen", "Vos paramètres de notification"),
        ["why"] = new(
            "You receive this recap because it is switched on for you in {workspace}. Choose which recaps you get, or switch them off, in your notification settings.",
            "Sie erhalten diesen Rückblick, weil er für Sie in {workspace} eingeschaltet ist. In Ihren Benachrichtigungseinstellungen wählen Sie, welche Rückblicke Sie erhalten, oder schalten sie aus.",
            "Vous recevez ce récapitulatif parce qu’il est activé pour vous dans {workspace}. Choisissez lesquels vous recevez, ou désactivez-les, dans vos paramètres de notification."),
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// The words for <paramref name="key"/> in <paramref name="language"/> (English for any other),
    /// with each {name} filled from <paramref name="args"/>. A placeholder without a value stays.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such key: a typo the tests catch.</exception>
    public static string T(string language, string key, params (string Name, object? Value)[] args)
    {
        var text = Entries[key].In(language);
        if (args.Length == 0) return text;
        return Placeholder().Replace(text, m =>
        {
            foreach (var (name, value) in args)
                if (name == m.Groups[1].Value)
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            return m.Value;
        });
    }

    /// <summary>Every key, with its three languages: for the tests.</summary>
    public static IReadOnlyDictionary<string, (string En, string De, string Fr)> All =>
        Entries.ToDictionary(e => e.Key, e => (e.Value.En, e.Value.De, e.Value.Fr), StringComparer.Ordinal);
}
