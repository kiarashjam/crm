using System.Globalization;
using System.Text.RegularExpressions;

namespace ACI.Application.Common.I18n;

/// <summary>
/// What the server writes to a member, in English, German (Swiss spelling: "ss", never "ß") and
/// French (as written in Romandy): the statement and invoice PDFs with their Swiss QR-bill, the
/// statement, reminder, welcome, event and pay-link emails, the marketing footer, and the
/// defaults of the public forms.
/// </summary>
/// <remarks>
/// <para>
/// Every entry has all three languages side by side, so none can be missing one; the test
/// (MemberTextsTests) checks that each keeps the English placeholders, that German has no "ß"
/// and that French puts a no-break space before : ? ! and ;. A placeholder is a name in braces,
/// {club}, filled with text the caller has already formatted (amounts in the workspace's
/// number format, dates in the reader's language).
/// </para>
/// <para>
/// The QR-bill's headings are the SIX Swiss Implementation Guidelines' own words in each language
/// (IG QR-bill, "Titles and headings"); a bank reads the slip by them.
/// </para>
/// </remarks>
public static partial class MemberTexts
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
        // ── shared ──────────────────────────────────────────────────────────────
        ["greeting"] = new("Dear {name},", "Guten Tag {name},", "Bonjour {name},"),
        ["or"] = new(" or ", " oder ", " ou "),
        ["us"] = new("us", "uns", "nous"),
        ["theClub"] = new("the club", "dem Club", "le club"),

        // ── the public forms' defaults ─────────────────────────────────────────
        ["form.applicationConsent"] = new(
            "Yes, I would like to hear about news, events and offers from the club by email. I can change my mind at any time.",
            "Ja, ich möchte per E-Mail über Neuigkeiten, Anlässe und Angebote des Clubs informiert werden. Ich kann meine Meinung jederzeit ändern.",
            "Oui, je souhaite recevoir par e-mail les nouvelles, événements et offres du club. Je peux changer d’avis à tout moment."),
        ["form.applicationThanks"] = new(
            "Thank you for applying. The membership committee will be in touch.",
            "Vielen Dank für Ihren Antrag. Das Aufnahmekomitee meldet sich bei Ihnen.",
            "Merci pour votre demande. Le comité d’admission prendra contact avec vous."),
        ["form.captureConsent"] = new(
            "Yes, I would like to hear about news, events and offers by email. I can change my mind at any time.",
            "Ja, ich möchte per E-Mail über Neuigkeiten, Anlässe und Angebote informiert werden. Ich kann meine Meinung jederzeit ändern.",
            "Oui, je souhaite recevoir par e-mail les nouvelles, événements et offres. Je peux changer d’avis à tout moment."),
        ["form.captureThanks"] = new("Thanks. We'll be in touch.", "Danke. Wir melden uns bei Ihnen.", "Merci. Nous reviendrons vers vous."),
        ["form.consentRequired"] = new(
            "Please tick the box to say you would like to hear from the club: this form signs you up for its news.",
            "Bitte kreuzen Sie das Feld an, um Neuigkeiten des Clubs zu erhalten: Mit diesem Formular melden Sie sich dafür an.",
            $"Veuillez cocher la case pour recevoir les nouvelles du club{Nb}: ce formulaire vous y inscrit."),
        ["form.captureConsentRequired"] = new(
            "Please tick the box to say you would like to hear from us: this form signs you up for news.",
            "Bitte kreuzen Sie das Feld an, um von uns zu hören: Mit diesem Formular melden Sie sich für Neuigkeiten an.",
            $"Veuillez cocher la case pour avoir de nos nouvelles{Nb}: ce formulaire vous inscrit à nos actualités."),
        ["form.captureEstablishmentName"] = new(
            "Enter the name of the establishment.",
            "Bitte geben Sie den Namen des Betriebs ein.",
            "Veuillez indiquer le nom de l’établissement."),

        // ── welcome, on approval ───────────────────────────────────────────────
        ["welcome.subject"] = new("Welcome to {club}", "Willkommen bei {club}", "Bienvenue chez {club}"),
        ["welcome.approved"] = new(
            "Your application has been approved, and you are now a {tier} member of {club}.",
            "Ihr Antrag wurde angenommen: Sie sind jetzt Mitglied von {club} ({tier}).",
            $"Votre demande a été acceptée{Nb}: vous êtes désormais membre de {{club}} ({{tier}})."),
        ["welcome.closing"] = new(
            "We look forward to welcoming you. If you have any questions, reply to this email.",
            "Wir freuen uns, Sie bei uns begrüssen zu dürfen. Bei Fragen antworten Sie einfach auf diese E-Mail.",
            "Nous nous réjouissons de vous accueillir. Pour toute question, répondez simplement à cet e-mail."),

        // ── the retention rules' email, to a member who has just become at risk ──
        ["retention.subject"] = new(
            "We would love to see you at {club} again",
            "Wir würden uns freuen, Sie wieder bei {club} zu sehen",
            "Nous serions ravis de vous revoir chez {club}"),
        ["retention.body"] = new(
            "We have not seen you at {club} for a while, and we wanted to ask how you are. Is there anything we could do better, or something you would like us to offer? Simply reply to this email: we read every answer.",
            "Wir haben Sie schon eine Weile nicht mehr bei {club} gesehen und möchten gerne wissen, wie es Ihnen geht. Gibt es etwas, das wir besser machen könnten, oder etwas, das Sie sich von uns wünschen? Antworten Sie einfach auf diese E-Mail: Wir lesen jede Antwort.",
            $"Nous ne vous avons pas vu chez {{club}} depuis un moment et souhaitions prendre de vos nouvelles. Y a-t-il quelque chose que nous pourrions mieux faire, ou que vous aimeriez nous voir proposer{Nb}? Répondez simplement à cet e-mail{Nb}: nous lisons chaque réponse."),
        ["retention.closing"] = new(
            "We look forward to seeing you soon.",
            "Wir freuen uns darauf, Sie bald wiederzusehen.",
            "Au plaisir de vous revoir bientôt."),

        // ── statements by email ────────────────────────────────────────────────
        ["statement.subject"] = new("Your statement for {period}", "Ihr Kontoauszug für {period}", "Votre relevé de compte pour {period}"),
        ["statement.attached"] = new(
            "Your statement for {period} is attached.",
            "Im Anhang finden Sie Ihren Kontoauszug für {period}.",
            "Vous trouverez en pièce jointe votre relevé de compte pour {period}."),
        ["statement.broughtForward"] = new("Brought forward: {amount}", "Saldovortrag: {amount}", $"Solde reporté{Nb}: {{amount}}"),
        ["statement.charges"] = new("Charges: {amount}", "Belastungen: {amount}", $"Débits{Nb}: {{amount}}"),
        ["statement.payments"] = new("Payments and credits: {amount}", "Zahlungen und Gutschriften: {amount}", $"Paiements et crédits{Nb}: {{amount}}"),
        ["statement.amountDue"] = new("Amount due: {amount}", "Offener Betrag: {amount}", $"Montant dû{Nb}: {{amount}}"),
        ["statement.inCredit"] = new("In credit: {amount}", "Guthaben: {amount}", $"Solde en votre faveur{Nb}: {{amount}}"),
        ["statement.balance"] = new("Balance: {amount}", "Saldo: {amount}", $"Solde{Nb}: {{amount}}"),
        ["statement.question"] = new(
            "If you have a question about it, reply to this email.",
            "Bei Fragen dazu antworten Sie einfach auf diese E-Mail.",
            "Pour toute question à ce sujet, répondez simplement à cet e-mail."),

        // ── payment reminders ──────────────────────────────────────────────────
        ["reminder.subjectFinal"] = new("Final payment reminder", "Letzte Zahlungserinnerung", "Dernier rappel de paiement"),
        ["reminder.subjectFirst"] = new("Payment reminder", "Zahlungserinnerung", "Rappel de paiement"),
        ["reminder.subjectSecond"] = new("Second payment reminder", "Zweite Zahlungserinnerung", "Deuxième rappel de paiement"),
        ["reminder.subjectNth"] = new("Payment reminder ({step})", "Zahlungserinnerung ({step})", "Rappel de paiement ({step})"),
        ["reminder.introFinal"] = new(
            "This is a final reminder. The following on your account is still unpaid:",
            "Dies ist eine letzte Erinnerung. Folgendes auf Ihrem Konto ist noch offen:",
            $"Ceci est un dernier rappel. Les montants suivants de votre compte sont toujours impayés{Nb}:"),
        ["reminder.introFirst"] = new(
            "This is a reminder that the following on your account has not been paid by its due date:",
            "Wir erinnern Sie daran, dass Folgendes auf Ihrem Konto bis zum Fälligkeitsdatum nicht bezahlt wurde:",
            $"Nous vous rappelons que les montants suivants de votre compte n’ont pas été payés à l’échéance{Nb}:"),
        ["reminder.introLater"] = new(
            "We have still not received payment for the following on your account:",
            "Für Folgendes auf Ihrem Konto haben wir noch keine Zahlung erhalten:",
            $"Nous n’avons toujours pas reçu le paiement des montants suivants de votre compte{Nb}:"),
        ["reminder.line"] = new("- {description}: {amount}, due {date}", "- {description}: {amount}, fällig am {date}", $"- {{description}}{Nb}: {{amount}}, échu le {{date}}"),
        ["reminder.total"] = new("Total overdue: {amount}", "Total überfällig: {amount}", $"Total en retard{Nb}: {{amount}}"),
        ["reminder.lapse"] = new(
            "A membership is suspended when a charge is still unpaid {days} days after its due date, until it is paid.",
            "Eine Mitgliedschaft wird sistiert, wenn eine Belastung {days} Tage nach Fälligkeit noch offen ist, bis sie bezahlt ist.",
            "Une adhésion est suspendue lorsqu’un montant est encore impayé {days} jours après son échéance, jusqu’à son paiement."),
        ["reminder.ignore"] = new(
            "If you have paid in the last few days, thank you, and please ignore this reminder.",
            "Falls Sie in den letzten Tagen bezahlt haben, danken wir Ihnen und bitten Sie, diese Erinnerung zu ignorieren.",
            "Si vous avez payé ces derniers jours, merci, et veuillez ignorer ce rappel."),

        // ── events ─────────────────────────────────────────────────────────────
        ["event.when"] = new("dddd d MMMM yyyy 'at' HH:mm", "dddd, d. MMMM yyyy 'um' HH:mm", "dddd d MMMM yyyy 'à' HH:mm"),
        ["event.atPlace"] = new(" at {location}", " ({location})", " ({location})"),
        ["event.promotedSubject"] = new("A place for you at {event}", "Ein Platz für Sie: {event}", $"Une place pour vous{Nb}: {{event}}"),
        ["event.promotedBody"] = new(
            "Good news: a place has come free at {event} on {when}{where}, and it is yours.",
            "Gute Nachricht: Für {event} am {when}{where} ist ein Platz frei geworden, und er gehört Ihnen.",
            $"Bonne nouvelle{Nb}: une place s’est libérée pour {{event}} le {{when}}{{where}}, et elle est à vous."),
        ["event.ticketOnAccount"] = new(
            "The ticket, {ticket}, is on your house account.",
            "Das Ticket ({ticket}) wurde Ihrem Clubkonto belastet.",
            "Le billet ({ticket}) a été porté sur votre compte au club."),
        ["event.ticketOnHostAccount"] = new(
            "The ticket, {ticket}, is on your host's house account.",
            "Das Ticket ({ticket}) wurde dem Clubkonto Ihres Gastgebers belastet.",
            "Le billet ({ticket}) a été porté sur le compte au club de votre hôte."),
        ["event.cantCome"] = new(
            "If you can no longer come, please let us know, so the next person waiting can have the place.",
            "Falls Sie nicht mehr kommen können, geben Sie uns bitte Bescheid, damit die nächste Person auf der Warteliste den Platz erhält.",
            "Si vous ne pouvez plus venir, merci de nous le faire savoir afin que la personne suivante sur la liste d’attente puisse en profiter."),
        ["event.cancelledSubject"] = new("{event} is cancelled", "{event} ist abgesagt", $"Annulation{Nb}: {{event}}"),
        ["event.cancelledBody"] = new(
            "We are sorry: {event} on {when}{where} is cancelled.",
            "Es tut uns leid: {event} am {when}{where} ist abgesagt.",
            $"Nous sommes désolés{Nb}: {{event}} du {{when}}{{where}} n’aura pas lieu."),
        ["event.ticketTakenOff"] = new(
            "The ticket, {ticket}, has been taken off the house account it was on.",
            "Das Ticket ({ticket}) wurde dem Clubkonto wieder gutgeschrieben.",
            "Le billet ({ticket}) a été retiré du compte au club sur lequel il figurait."),
        ["event.seeYou"] = new(
            "We hope to see you at another event soon.",
            "Wir hoffen, Sie bald an einem anderen Anlass zu sehen.",
            "Nous espérons vous voir bientôt à un autre événement."),

        // ── pay links ──────────────────────────────────────────────────────────
        ["payLink.subjectSetup"] = new("Save a card with {club}", "Karte bei {club} speichern", "Enregistrer une carte auprès de {club}"),
        ["payLink.subjectPay"] = new("Pay your account with {club}", "Ihr Konto bei {club} bezahlen", "Régler votre compte auprès de {club}"),
        ["payLink.setupAutoPay"] = new(
            "You can save a card for your membership dues, which are then charged to it when they are billed.",
            "Sie können eine Karte für Ihre Mitgliederbeiträge speichern; diese werden ihr dann bei jeder Rechnungsstellung belastet.",
            "Vous pouvez enregistrer une carte pour vos cotisations, qui y seront alors débitées lors de leur facturation."),
        ["payLink.setupPlain"] = new(
            "You can save a card with the club for paying your account.",
            "Sie können beim Club eine Karte speichern, um Ihr Konto zu bezahlen.",
            "Vous pouvez enregistrer une carte auprès du club pour régler votre compte."),
        ["payLink.setupSecure"] = new(
            "The card is entered on Stripe's secure page; the club never sees its number.",
            "Die Karte wird auf der sicheren Seite von Stripe erfasst; der Club sieht ihre Nummer nie.",
            $"La carte est saisie sur la page sécurisée de Stripe{Nb}; le club n’en voit jamais le numéro."),
        ["payLink.payOnline"] = new(
            "You can pay {amount} online, by {methods}, on Stripe's secure page.",
            "Sie können {amount} online mit {methods} auf der sicheren Seite von Stripe bezahlen.",
            "Vous pouvez payer {amount} en ligne, par {methods}, sur la page sécurisée de Stripe."),
        ["payLink.validUntil"] = new("The link works until {date}.", "Der Link ist gültig bis {date}.", "Le lien est valable jusqu’au {date}."),
        // ── the member portal ──────────────────────────────────────────────────
        ["portal.inviteSubject"] = new("{club}: your member account", "{club}: Ihr Mitgliederkonto", $"{{club}}{Nb}: votre espace membre"),
        ["portal.inviteIntro"] = new(
            "{club} has a member portal: see your statements and pay them, sign up for events, book, and choose what you hear from the club.",
            "{club} hat ein Mitgliederportal: Dort sehen und bezahlen Sie Ihre Kontoauszüge, melden sich für Anlässe an, reservieren und bestimmen, was Sie vom Club erhalten.",
            $"{{club}} a un espace membre{Nb}: consultez et réglez vos relevés, inscrivez-vous aux événements, réservez et choisissez ce que vous recevez du club."),
        ["portal.inviteOpen"] = new("Open your account:", "Öffnen Sie Ihr Konto:", $"Ouvrir votre espace{Nb}:"),
        ["portal.inviteOpenFor"] = new("Open the account of {name}:", "Konto von {name} öffnen:", $"Ouvrir l’espace de {{name}}{Nb}:"),
        ["portal.inviteValid"] = new(
            "Each link works once, until {date}. After that, ask for a new one on the portal's page: {portal}",
            "Jeder Link funktioniert einmal, bis am {date}. Danach können Sie auf der Seite des Portals einen neuen anfordern: {portal}",
            $"Chaque lien fonctionne une fois, jusqu’au {{date}}. Ensuite, demandez-en un nouveau sur la page de l’espace{Nb}: {{portal}}"),
        ["portal.signInSubject"] = new("Your sign-in link for {club}", "Ihr Anmeldelink für {club}", "Votre lien de connexion pour {club}"),
        ["portal.signInIntro"] = new(
            "Here is your link to sign in to the member portal of {club}:",
            "Hier ist Ihr Link für die Anmeldung im Mitgliederportal von {club}:",
            $"Voici votre lien pour vous connecter à l’espace membre de {{club}}{Nb}:"),
        ["portal.signInAs"] = new("Sign in as {name}:", "Als {name} anmelden:", $"Se connecter en tant que {{name}}{Nb}:"),
        ["portal.signInValid"] = new(
            "Each link works once, for {minutes} minutes. If you did not ask for it, you can ignore this email: nobody can sign in without the link.",
            "Jeder Link funktioniert einmal, während {minutes} Minuten. Falls Sie ihn nicht angefordert haben, können Sie diese E-Mail ignorieren: Ohne den Link kann sich niemand anmelden.",
            $"Chaque lien fonctionne une fois, pendant {{minutes}} minutes. Si vous ne l’avez pas demandé, ignorez cet e-mail{Nb}: personne ne peut se connecter sans le lien."),

        ["method.card"] = new("card", "Karte", "carte"),
        ["method.twint"] = new("TWINT", "TWINT", "TWINT"),
        ["method.sepa"] = new("SEPA direct debit", "SEPA-Lastschrift", "prélèvement SEPA"),

        // ── the footer of a member's marketing email ──────────────────────────
        ["footer.marketingQuestion"] = new(
            "Don't want news and offers from {club} by email?",
            "Keine Neuigkeiten und Angebote von {club} mehr per E-Mail?",
            $"Vous ne souhaitez plus recevoir les nouvelles et offres de {{club}} par e-mail{Nb}?"),
        ["footer.unsubscribe"] = new("Unsubscribe", "Abmelden", "Se désinscrire"),

        // ── statement and invoice PDFs ─────────────────────────────────────────
        ["pdf.datePattern"] = new("d MMM yyyy", "d. MMM yyyy", "d MMM yyyy"),
        ["pdf.statementTitle"] = new("Statement", "Kontoauszug", "Relevé de compte"),
        ["pdf.householdTitle"] = new("Household statement", "Haushaltsauszug", "Relevé du ménage"),
        ["pdf.statementSubject"] = new("Account statement", "Kontoauszug", "Relevé de compte"),
        ["pdf.boxBroughtForward"] = new("Brought forward", "Saldovortrag", "Solde reporté"),
        ["pdf.boxCharges"] = new("Charges", "Belastungen", "Débits"),
        ["pdf.boxPayments"] = new("Payments and credits", "Zahlungen und Gutschriften", "Paiements et crédits"),
        ["pdf.boxClosing"] = new("Closing balance", "Schlusssaldo", "Solde final"),
        ["pdf.rowBroughtForward"] = new("Balance brought forward", "Saldovortrag", "Solde reporté"),
        ["pdf.continued"] = new("Statement, continued", "Kontoauszug, Fortsetzung", "Relevé de compte, suite"),
        ["pdf.nothingInPeriod"] = new(
            "Nothing was charged or paid in this period.",
            "In dieser Periode wurde nichts belastet oder bezahlt.",
            "Rien n’a été débité ni payé durant cette période."),
        ["pdf.totals"] = new("Totals for the period", "Total der Periode", "Totaux de la période"),
        ["pdf.amountDue"] = new("Amount due: {amount}", "Offener Betrag: {amount}", $"Montant dû{Nb}: {{amount}}"),
        ["pdf.inCredit"] = new("In credit: {amount}", "Guthaben: {amount}", $"Solde en votre faveur{Nb}: {{amount}}"),
        ["pdf.nothingToPay"] = new("Nothing to pay", "Nichts zu bezahlen", "Rien à payer"),
        ["pdf.dueNote"] = new(
            "This is what the account owed when the period ended. Payments made since are on the next statement.",
            "So viel war am Ende der Periode offen. Seither eingegangene Zahlungen erscheinen auf dem nächsten Auszug.",
            "C’est le montant dû à la fin de la période. Les paiements effectués depuis figurent sur le prochain relevé."),
        ["pdf.questionsNote"] = new(
            "Questions about this statement? Reply to the email it came with, or ask the membership office.",
            "Fragen zu diesem Auszug? Antworten Sie auf die E-Mail, mit der er kam, oder wenden Sie sich an das Mitgliedersekretariat.",
            $"Des questions sur ce relevé{Nb}? Répondez à l’e-mail qui l’accompagnait ou adressez-vous au secrétariat des membres."),
        ["pdf.qrNote"] = new(
            "Pay with the QR-bill at the foot of the statement: scan it in your banking app, or quote its reference.",
            "Bezahlen Sie mit der QR-Rechnung am Fuss des Auszugs: Scannen Sie sie in Ihrer Banking-App, oder geben Sie ihre Referenz an.",
            $"Payez avec la QR-facture au bas du relevé{Nb}: scannez-la dans votre application bancaire ou indiquez sa référence."),
        ["pdf.vatHeading"] = new("VAT included in the charges above", "In den obigen Belastungen enthaltene MWST", "TVA comprise dans les débits ci-dessus"),
        ["pdf.vatLine"] = new("{rate}% on {gross}: {vat}", "{rate} % auf {gross}: {vat}", $"{{rate}} % sur {{gross}}{Nb}: {{vat}}"),
        ["pdf.householdHeading"] = new("Who ran up what in this period", "Wer in dieser Periode was belastet hat", "Qui a dépensé quoi durant cette période"),
        ["pdf.ownAccountOwes"] = new("Own account owes {amount}", "Eigenes Konto: {amount} offen", $"Compte personnel{Nb}: {{amount}} dû"),
        ["pdf.ownAccountCredit"] = new("Own account in credit {amount}", "Eigenes Konto: {amount} Guthaben", $"Compte personnel{Nb}: {{amount}} en votre faveur"),
        ["pdf.paymentSlip"] = new("Payment slip", "QR-Rechnung", "QR-facture"),
        ["pdf.page"] = new("Page {page} of {pages}", "Seite {page} von {pages}", "Page {page} sur {pages}"),
        ["pdf.issued"] = new("Issued {date}", "Ausgestellt am {date}", "Établi le {date}"),
        ["pdf.tierMember"] = new("{tier} member", "Mitglied {tier}", "Membre {tier}"),
        ["pdf.forHousehold"] = new("For the {household} household", "Für den Haushalt {household}", "Pour le ménage {household}"),
        ["pdf.colDate"] = new("Date", "Datum", "Date"),
        ["pdf.colDescription"] = new("Description", "Beschreibung", "Description"),
        ["pdf.colCharges"] = new("Charges", "Belastungen", "Débits"),
        ["pdf.colPayments"] = new("Payments", "Zahlungen", "Paiements"),
        ["pdf.colOwed"] = new("Owed", "Offen", "Dû"),
        ["pdf.formerHouseholdMember"] = new("A former member of the household", "Ein früheres Mitglied des Haushalts", "Un ancien membre du ménage"),
        ["pdf.noLongerInHousehold"] = new("No longer in the household", "Nicht mehr im Haushalt", "Ne fait plus partie du ménage"),
        // A company's statement: its own charges and those its nominees ran up on its account.
        ["pdf.companyTitle"] = new("Company statement", "Firmenauszug", "Relevé de l’entreprise"),
        ["pdf.formerNominee"] = new("A former nominee", "Eine frühere nominierte Person", "Une ancienne personne désignée"),

        ["line.balanceBroughtOver"] = new("Balance brought over", "Übertragener Saldo", "Solde repris"),
        ["line.correction"] = new("Correction", "Korrektur", "Correction"),
        ["line.void"] = new("Void", "Storno", "Annulation"),
        ["line.refund"] = new("Refund", "Rückerstattung", "Remboursement"),
        ["line.incurredBy"] = new("Incurred by {name}", "Verursacht von {name}", "Dépensé par {name}"),
        ["line.payment"] = new("Payment", "Zahlung", "Paiement"),
        ["line.paymentBy"] = new("Payment ({method})", "Zahlung ({method})", "Paiement ({method})"),
        ["line.paymentVoided"] = new("Payment voided", "Zahlung storniert", "Paiement annulé"),
        ["line.voided"] = new("Voided: {description}", "Storniert: {description}", $"Annulé{Nb}: {{description}}"),

        ["invoice.title"] = new("Invoice", "Rechnung", "Facture"),
        ["invoice.number"] = new("No. {number}", "Nr. {number}", "N° {number}"),
        ["invoice.footer"] = new("Invoice {number}", "Rechnung {number}", "Facture {number}"),
        ["invoice.colPosted"] = new("Posted", "Gebucht", "Comptabilisé"),
        ["invoice.colAmount"] = new("Amount", "Betrag", "Montant"),
        ["invoice.vatIncluded"] = new("VAT {rate}% included", "Inkl. {rate} % MWST", "TVA {rate} % comprise"),
        ["invoice.paidSoFar"] = new("Paid so far", "Bereits bezahlt", "Déjà payé"),
        ["invoice.cancelled"] = new("Cancelled: nothing to pay", "Storniert: nichts zu bezahlen", $"Annulée{Nb}: rien à payer"),
        ["invoice.paidInFull"] = new("Paid in full", "Vollständig bezahlt", "Entièrement payée"),
        ["invoice.payBy"] = new("Please pay by {date}.", "Bitte bezahlen Sie bis {date}.", "Merci de payer d’ici au {date}."),
        ["invoice.qrNote"] = new(
            "Pay with the QR-bill below: scan it in your banking app, or quote its reference.",
            "Bezahlen Sie mit der QR-Rechnung unten: Scannen Sie sie in Ihrer Banking-App, oder geben Sie ihre Referenz an.",
            $"Payez avec la QR-facture ci-dessous{Nb}: scannez-la dans votre application bancaire ou indiquez sa référence."),
        ["invoice.questions"] = new(
            "Questions about this invoice? Reply to the email it came with, or ask the membership office.",
            "Fragen zu dieser Rechnung? Antworten Sie auf die E-Mail, mit der sie kam, oder wenden Sie sich an das Mitgliedersekretariat.",
            $"Des questions sur cette facture{Nb}? Répondez à l’e-mail qui l’accompagnait ou adressez-vous au secrétariat des membres."),
        ["invoice.balanceBroughtOver"] = new("Balance brought over", "Übertragener Saldo", "Solde repris"),
        ["invoice.qrMessage"] = new("Invoice {number}", "Rechnung {number}", "Facture {number}"),
        // A membership contract's deposit, paid on signing (its QR-bill invoice).
        ["deposit.description"] = new("Deposit: {title}", "Anzahlung: {title}", $"Acompte{Nb}: {{title}}"),
        ["deposit.qrMessage"] = new("Deposit {number}", "Anzahlung {number}", "Acompte {number}"),
        ["statement.qrMessage"] = new("Statement {period}", "Kontoauszug {period}", "Relevé {period}"),

        // ── the Swiss QR-bill, in the SIX guidelines' words ─────────────────────
        ["qr.paymentPart"] = new("Payment part", "Zahlteil", "Section paiement"),
        ["qr.accountPayableTo"] = new("Account / Payable to", "Konto / Zahlbar an", "Compte / Payable à"),
        ["qr.reference"] = new("Reference", "Referenz", "Référence"),
        ["qr.additionalInformation"] = new("Additional information", "Zusätzliche Informationen", "Informations supplémentaires"),
        ["qr.currency"] = new("Currency", "Währung", "Monnaie"),
        ["qr.amount"] = new("Amount", "Betrag", "Montant"),
        ["qr.receipt"] = new("Receipt", "Empfangsschein", "Récépissé"),
        ["qr.acceptancePoint"] = new("Acceptance point", "Annahmestelle", "Point de dépôt"),
        ["qr.separate"] = new("Separate before paying in", "Vor der Einzahlung abzutrennen", "A détacher avant le versement"),
        ["qr.payableBy"] = new("Payable by", "Zahlbar durch", "Payable par"),
        ["qr.payableByNameAddress"] = new("Payable by (name/address)", "Zahlbar durch (Name/Adresse)", "Payable par (nom/adresse)"),

        // ── the pay link's page ─────────────────────────────────────────────────
        ["pay.accountOwes"] = new("what the account owes", "offener Betrag des Kontos", "montant dû sur le compte"),
        ["pay.chargesPaid"] = new("Charges already paid", "Belastungen bereits bezahlt", "Montants déjà payés"),
        ["pay.chargeAndMore"] = new("{first} and {count} more", "{first} und {count} weitere", "{first} et {count} de plus"),
        ["pay.setupAutoPay"] = new("Save a card for your membership dues", "Eine Karte für Ihre Mitgliederbeiträge speichern", "Enregistrer une carte pour vos cotisations"),
        ["pay.setupPlain"] = new("Save a card for paying your account", "Eine Karte zum Bezahlen Ihres Kontos speichern", "Enregistrer une carte pour régler votre compte"),
        ["pay.noCardsOnline"] = new(
            "The club cannot take cards online just now. Please ask the club.",
            "Der Club kann gerade keine Karten online annehmen. Bitte wenden Sie sich an den Club.",
            "Le club ne peut pas accepter de cartes en ligne pour le moment. Veuillez vous adresser au club."),
        ["pay.noPaymentsOnline"] = new(
            "The club cannot take payments online just now. Please ask the club.",
            "Der Club kann gerade keine Zahlungen online annehmen. Bitte wenden Sie sich an den Club.",
            "Le club ne peut pas accepter de paiements en ligne pour le moment. Veuillez vous adresser au club."),
        ["pay.currencyNotPayable"] = new(
            "The account's currency cannot be paid online.",
            "Die Währung des Kontos kann nicht online bezahlt werden.",
            "La monnaie du compte ne peut pas être payée en ligne."),
        ["pay.nothingToPay"] = new(
            "There is nothing to pay: everything this link was for is paid.",
            "Es ist nichts zu bezahlen: Alles, wofür dieser Link war, ist bezahlt.",
            $"Il n’y a rien à payer{Nb}: tout ce que concernait ce lien est payé."),
    };

    // A choice of words a value stored in English stands for: charge kinds, payment methods and
    // household roles as the ledger keeps them. One the table does not know is shown as it is.
    private static readonly Dictionary<string, Entry> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kind:Dues"] = new("Dues", "Mitgliederbeitrag", "Cotisation"),
        ["kind:Dining"] = new("Dining", "Restaurant", "Restaurant"),
        ["kind:Bar"] = new("Bar", "Bar", "Bar"),
        ["kind:Event"] = new("Event", "Anlass", "Événement"),
        ["kind:Spa"] = new("Spa", "Spa", "Spa"),
        ["kind:Booking"] = new("Booking", "Reservation", "Réservation"),
        ["kind:Retail"] = new("Retail", "Shop", "Boutique"),
        ["kind:Other"] = new("Other", "Anderes", "Autre"),
        ["kind:Deposit"] = new("Deposit", "Anzahlung", "Acompte"),
        ["method:CardOnFile"] = new("CardOnFile", "Hinterlegte Karte", "Carte enregistrée"),
        ["method:Card"] = new("Card", "Karte", "Carte"),
        ["method:Twint"] = new("Twint", "TWINT", "TWINT"),
        ["method:DirectDebit"] = new("DirectDebit", "Lastschrift", "Prélèvement"),
        ["method:Cash"] = new("Cash", "Bar", "Espèces"),
        ["method:Check"] = new("Check", "Check", "Chèque"),
        ["method:Wire"] = new("Wire", "Überweisung", "Virement"),
        ["method:AppliedCredit"] = new("AppliedCredit", "Guthaben verrechnet", "Crédit imputé"),
        ["role:Primary"] = new("Primary", "Hauptmitglied", "Membre principal"),
        ["role:Spouse"] = new("Spouse", "Ehepartner/in", "Conjoint(e)"),
        ["role:Partner"] = new("Partner", "Partner/in", "Partenaire"),
        ["role:Child"] = new("Child", "Kind", "Enfant"),
        ["role:Dependent"] = new("Dependent", "Angehörige/r", "Personne à charge"),
        ["role:Parent"] = new("Parent", "Elternteil", "Parent"),
        ["role:Sibling"] = new("Sibling", "Geschwister", "Frère ou sœur"),
        ["role:Other"] = new("Other", "Andere", "Autre"),
        // On a company's statement: the company's own charges, and each nominee's.
        ["role:Company"] = new("Company", "Firma", "Entreprise"),
        ["role:Nominee"] = new("Nominee", "Nominierte Person", "Personne désignée"),
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// The words for <paramref name="key"/> in <paramref name="language"/> (English for any other),
    /// with each {name} filled from <paramref name="args"/>. A placeholder without a value stays
    /// as it is, which a reader and a test can see.
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

    /// <summary>A charge kind as the ledger stores it ("Dues"), in the reader's language.</summary>
    public static string Kind(string language, string? kind) => Word("kind", language, kind);

    /// <summary>A payment method as the ledger stores it ("Wire"), in the reader's language.</summary>
    public static string Method(string language, string? method) => Word("method", language, method);

    /// <summary>A household role as it is stored ("Spouse"), in the reader's language.</summary>
    public static string Role(string language, string? role) => Word("role", language, role);

    private static string Word(string group, string language, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Words.TryGetValue($"{group}:{value.Trim()}", out var entry) ? entry.In(language) : value.Trim();
    }

    /// <summary>"card or TWINT or SEPA direct debit": the words joined with the language's "or".</summary>
    public static string JoinOr(string language, IEnumerable<string> items) => string.Join(T(language, "or"), items);

    /// <summary>
    /// A date in the reader's language and the workspace's region: "12 Nov 2026", "12. Nov. 2026",
    /// "12 nov. 2026". English keeps the invariant month names it always printed.
    /// </summary>
    public static string Date(string language, string? workspaceLocale, DateTime local) =>
        local.ToString(T(language, "pdf.datePattern"), Culture(language, workspaceLocale));

    /// <summary>
    /// A statement's period as the reader says it: "October 2026" (Oktober, octobre) for a calendar
    /// month, else its first and last day. <paramref name="toLocalExclusive"/> is the moment after it.
    /// </summary>
    public static string Period(string language, string? workspaceLocale, DateTime fromLocal, DateTime toLocalExclusive)
    {
        if (fromLocal.Day == 1 && fromLocal.TimeOfDay == TimeSpan.Zero && toLocalExclusive == fromLocal.AddMonths(1))
            return fromLocal.ToString("MMMM yyyy", Culture(language, workspaceLocale));
        return $"{Date(language, workspaceLocale, fromLocal)} – {Date(language, workspaceLocale, toLocalExclusive.AddTicks(-1))}";
    }

    /// <summary>The culture a reader's dates and month names are written in (invariant for English, as before).</summary>
    public static CultureInfo Culture(string language, string? workspaceLocale) =>
        AppLanguages.Normalise(language) is { } known && known != AppLanguages.English
            ? AppLanguages.DateCulture(known, workspaceLocale)
            : CultureInfo.InvariantCulture;

    /// <summary>Every key, with its three languages: for the tests.</summary>
    public static IReadOnlyDictionary<string, (string En, string De, string Fr)> All =>
        Entries.Concat(Words).ToDictionary(e => e.Key, e => (e.Value.En, e.Value.De, e.Value.Fr), StringComparer.Ordinal);
}
