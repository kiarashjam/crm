// Die deutschen Seiten, geschrieben für die Deutschschweiz: Sie-Form, «ss» statt «ß»,
// QR-Rechnung, DSG, Anlass, Reservation, Konsumation.
// Nur, was CadenceE heute kann: kein Preis, kein Kunde ausser Pavillon 46, keine
// Bewertung und keine Auszeichnung.

import { deFreeCrm } from './freeCrm';
import { dePricing } from './pricing';
import { deGuides } from './guides/de';
import type { LanguageContent } from './types';

const de: LanguageContent = {
  template: {
    featuresEyebrow: 'Funktionen',
    stepsEyebrow: 'So funktioniert es',
    faqEyebrow: 'FAQ',
    solutionsEyebrow: 'Lösungen',
    stepLabel: 'Schritt',
    minRead: 'Min. Lesezeit',
    updated: 'Aktualisiert am',
    onThisPage: 'Auf dieser Seite',
    keyTakeaways: 'Das Wichtigste in Kürze',
    furtherReading: 'Weiterlesen',
    moreGuides: 'Weitere Ratgeber',
    guidesEyebrow: 'Ratgeber',
    guidesH1: 'Ratgeber für Clubs, Vereine und Verkaufsteams',
    guidesLead: 'Praxisnahe Ratgeber zu den Fragen, die sich Clubs, Gastronomie und Verkaufsteams in der Schweiz am häufigsten stellen: Mitglieder und Verlängerungen, QR-Rechnung und Datenschutz, Nachfassen und die Wahl eines CRM.',
  },
  pages: {
    home: {
      eyebrow: 'Schweizer CRM',
      // Während des Einführungsangebots sagt der H1, dass das CRM kostenlos ist; ab 1. Januar 2027 gilt afterOffer.
      h1: 'Das kostenlose Schweizer CRM für Ihren Club',
      h1Accent: 'und Ihren Vertrieb',
      afterOffer: {
        h1: 'Das Schweizer CRM für Ihren Club',
        h1Accent: 'und Ihren Vertrieb',
      },
      lead: 'CadenceE vereint in einem Werkzeug, was einen Privatclub, einen Gastronomiebetrieb oder ein Verkaufsteam am Laufen hält: Leads und online unterschriebene Verträge, Mitglieder und Anlässe, Konsumationen und Abrechnung mit QR-Rechnung und TWINT, E-Mail und KI-Assistent. Entwickelt in der Schweiz, auf Deutsch, Französisch und Englisch.',
      heroCardTitle: 'In einem Arbeitsbereich',
      heroPoints: [
        'Leads in fünf Phasen, bis zur Anzahlung',
        'Verträge online unterschrieben, Anzahlung bei Unterzeichnung',
        'Mitglieder, Haushalte, Erneuerungen und Anlässe',
        'Konsumationen, Beiträge, Auszüge und Schweizer Zahlungen',
        'E-Mail, Berichte und ein KI-Assistent auf Basis von Claude',
      ],
      intro: {
        heading: 'Ein CRM, vom ersten Kontakt bis zum Monatsauszug',
        paragraphs: [
          'Ein Privatclub, ein Restaurant oder ein Verkaufsteam jongliert oft mit fünf Werkzeugen: einer Tabelle für Anfragen, einer Software für Verträge, einer weiteren für Mitglieder, einer Kasse für die Bar und einer E-Mail-Plattform, die keines der anderen kennt. Bei jeder Übergabe geht ein Lead verloren oder eine Zahlung vergessen.',
          'CadenceE ersetzt dieses Flickwerk durch einen Datensatz pro Person und pro Firma. Die Anfrage von Ihrer Website wird zum Lead, der Lead unterschreibt den Vertrag und leistet die Anzahlung, und derselbe Datensatz wird zum aktiven Mitglied mit Portal, Konto und Monatsauszug. Nichts wird doppelt erfasst.',
          'CadenceE ist täglich im Pavillon 46 im Einsatz, einem Privatclub in La Croix-sur-Lutry (VD), zusammen mit dem es entwickelt wurde.',
        ],
      },
      showSolutions: {
        heading: 'Was CadenceE für Sie tut',
        intro: 'Jede Seite erklärt einen Bereich im Detail. Wählen Sie den, der Sie betrifft.',
      },
      sections: [
        {
          heading: 'Gemacht für die Schweiz',
          paragraphs: [
            'QR-Rechnung und TWINT für Zahlungen, Buchhaltungsexport nach Bexio und Abacus, die App auf Deutsch, Französisch und Englisch nach Wahl jeder Person, und Auskunfts- und Löschbegehren nach DSG und DSGVO. Mehr zum [Schweizer CRM](page:swiss).',
          ],
          link: 'swiss',
        },
        {
          heading: 'Eine KI, die vorschlägt: Sie entscheiden',
          paragraphs: [
            'Der [KI-Assistent](page:ai) auf Basis von Claude schlägt Änderungen vor, die Sie bestätigen, schreibt Ihre Verkaufs-E-Mails und bereitet jeden Morgen ein Briefing für Ihren Tag vor. Nichts wird ohne die Zustimmung einer Person gespeichert.',
          ],
          link: 'ai',
        },
      ],
      features: {
        heading: 'CadenceE auf einen Blick',
        intro: 'Vom Empfang bis zur Buchhaltung arbeitet das ganze Team mit denselben Datensätzen.',
        items: [
          { icon: 'leads', title: 'Leads in fünf Phasen', text: 'Kontaktaufnahme, Termin, Vertrag, Unterschrift, Anzahlung.' },
          { icon: 'signature', title: 'Elektronische Signatur', text: 'Verträge online unterschrieben, ohne Konto.' },
          { icon: 'members', title: 'Mitglieder und Haushalte', text: 'Aufnahmeanträge, Erneuerungen mit Risikobewertung, Firmenmitgliedschaften.' },
          { icon: 'reservations', title: 'Anlässe und Reservationen', text: 'Wartelisten, Privatanlässe, Besuche und Check-ins.' },
          { icon: 'tabs', title: 'Konsumationen und Kasse', text: 'Bar- und Restaurantrechnungen, Mitgliederkonten, Lightspeed-Kasse.' },
          { icon: 'payments', title: 'Schweizer Zahlungen', text: 'Karte, TWINT, SEPA und QR-Rechnung, mit Abgleich der Bankdatei.' },
          { icon: 'accounting', title: 'Buchhaltungsexport', text: 'Nach Bexio oder Abacus, aus einem Hauptbuch, das nie überschrieben wird.' },
          { icon: 'automations', title: 'E-Mail-Automatisierung', text: 'Sequenzen, Automationen, Newsletter und Kampagnen.' },
          { icon: 'ai', title: 'KI-Assistent', text: 'Auf Basis von Claude: schlägt vor, schreibt, bereitet das Briefing vor.' },
        ],
      },
      steps: {
        heading: 'So starten Sie mit CadenceE',
        items: [
          { title: 'Arbeitsbereich erstellen', text: 'Registrieren Sie sich, benennen Sie den Arbeitsbereich und laden Sie Ihr Team mit passenden Rollen ein.' },
          { title: 'Daten übernehmen', text: 'Leads, Kontakte und Mitglieder aus Tabellen importieren, zuerst mit einem Probelauf und mit Rückgängig-Funktion.' },
          { title: 'Kanäle verbinden', text: 'Anfrageformular oder Webhook auf Ihrer Website, Vertragsvorlagen und Zahlungsmittel.' },
          { title: 'An einem Ort arbeiten', text: 'Vom Lead zum Mitglied und vom Mitglied zum Auszug, mit Berichten und einem Morgenbriefing.' },
        ],
      },
      faq: {
        heading: 'Häufige Fragen zu CadenceE',
        items: [
          { q: 'Was ist CadenceE?', a: 'Ein in der Schweiz entwickeltes CRM, das einen ganzen Club und seinen Vertrieb führt: Leads, Verträge mit elektronischer Signatur, Mitglieder, Anlässe, Abrechnung, E-Mail, Berichte und KI-Assistent in einem Werkzeug.' },
          { q: 'Für wen ist CadenceE gedacht?', a: 'Für Privatclubs und Members’ Clubs, für Restaurants, Hotels und Eventlocations und für Verkaufsteams, die jeden Lead bis zur Anzahlung verfolgen. Der Pavillon 46, ein Privatclub in La Croix-sur-Lutry, nutzt es täglich.' },
          { q: 'Gibt es CadenceE auf Deutsch?', a: 'Ja. Die App gibt es auf Deutsch, Französisch und Englisch, und jede Person wählt ihre Sprache. Vorbereitete E-Mails gibt es auf Französisch und Englisch.' },
          { q: 'Welche Zahlungsmittel werden unterstützt?', a: 'Karte, TWINT, SEPA und QR-Rechnung. Die Anzahlung kann bei der Vertragsunterzeichnung per Karte oder QR-Rechnung eingezogen werden. Mehr zu [Abrechnung und Zahlungen](page:billing).' },
          { q: 'Können wir bestehende Daten übernehmen?', a: 'Ja. Leads, Kontakte, Firmen und Mitglieder lassen sich aus Tabellen importieren; jeder Import beginnt mit einem Probelauf und lässt sich rückgängig machen.' },
          { q: 'Ändert die KI Daten selbstständig?', a: 'Nein. Der Assistent schlägt Änderungen vor, und eine Person bestätigt jede einzelne, bevor etwas gespeichert wird.' },
        ],
      },
      related: ['private-clubs', 'sales', 'swiss'],
      cta: {
        heading: 'Führen Sie Club und Vertrieb mit einem CRM',
        text: 'Erstellen Sie Ihren Arbeitsbereich, oder fragen Sie eine Demo an.',
      },
    },

    features: {
      eyebrow: 'Alle Funktionen',
      h1: 'CRM-Funktionen für Clubs, Gastronomie',
      h1Accent: 'und Verkaufsteams',
      lead: 'CadenceE bringt Vertrieb und Clubbetrieb in ein CRM: Leads und Verträge, Mitglieder und Anlässe, Konsumationen und Abrechnung, E-Mail und Berichte, dazu ein KI-Assistent, der nur mit Ihrer Zustimmung handelt. Hier finden Sie alle Funktionen, Bereich für Bereich.',
      heroCardTitle: 'In einem Arbeitsbereich',
      heroPoints: [
        'Leads in fünf Phasen, bis zur Anzahlung',
        'Verträge online unterschrieben, Anzahlung bei Unterzeichnung',
        'Mitglieder, Haushalte, Erneuerungen und Anlässe',
        'Konsumationen, Beiträge, Auszüge und Schweizer Zahlungen',
        'E-Mail, Berichte und KI-Assistent auf Basis von Claude',
      ],
      intro: {
        heading: 'Ein CRM statt fünf Werkzeuge',
        paragraphs: [
          'Die meisten Clubs und Gastronomiebetriebe arbeiten mit einem Flickwerk: einer Tabelle für Anfragen, einem Werkzeug für Verträge, einem weiteren für Mitglieder, einer Kasse für die Bar und einer E-Mail-Plattform, die nichts von alledem weiss. Jede Übergabe dazwischen ist eine Gelegenheit, einen Lead oder eine Zahlung zu vergessen.',
          'CadenceE ersetzt das Flickwerk durch einen Datensatz pro Person und pro Firma. Die Anfrage über Ihre Website wird zum Lead, der Lead unterschreibt und leistet die Anzahlung, und derselbe Datensatz wird zum aktiven Mitglied mit Portal, Konto und Monatsauszug.',
          'Ein Arbeitsbereich kann den Vertrieb, den Clubbetrieb oder beides nutzen, und die ganze App gibt es auf Deutsch, Französisch und Englisch. Jeder Bereich unten verweist auf eine Seite, die ihn im Detail erklärt.',
        ],
      },
      sections: [
        {
          heading: 'Leads und Vertrieb',
          paragraphs: [
            'Der [Lead-Tracker mit fünf Phasen](page:sales) begleitet jede Opportunität von der Kontaktaufnahme über Termin, Vertrag und Unterschrift bis zur Anzahlung: Das Team kennt immer den nächsten Schritt. Leads werden bewertet und zugewiesen, gegen Dubletten geschützt und halten fest, wer sie empfohlen hat und über welchen Partner sie kamen.',
          ],
          bullets: [
            'Lead-Scoring und Zuweisungsregeln',
            'Dublettenschutz bei Erfassung und Import',
            'Importe mit Probelauf und Rückgängig-Funktion',
            'Öffentliche Anfrageformulare und Website-Webhook',
          ],
          link: 'sales',
        },
        {
          heading: 'Verträge, E-Signatur und Anzahlungen',
          paragraphs: [
            'Verträge entstehen aus Ihren Vorlagen und gehen zur [elektronischen Unterschrift](page:e-signature). Wer unterschreibt, braucht kein Konto. Die Anzahlung wird bei der Unterzeichnung per Karte oder QR-Rechnung eingezogen, und ein auf Papier unterschriebener Vertrag lässt sich einfach hochladen.',
          ],
          bullets: [
            'Vertragsvorlagen, ausgefüllt aus dem Lead',
            'Unterschrift ohne Konto',
            'Anzahlung bei Unterzeichnung, Karte oder QR-Rechnung',
            'Upload von Papierverträgen',
          ],
          link: 'e-signature',
        },
        {
          heading: 'Mitglieder und Mitgliedschaften',
          paragraphs: [
            'Ist der Vertrag unterschrieben, wird der Lead in einem Schritt zum aktiven Mitglied, mit Willkommens-E-Mail und Zugang zum Mitgliederportal. Die [Mitgliederverwaltung](page:membership) umfasst Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen mit Risikobewertung und Firmenmitgliedschaften.',
          ],
          link: 'membership',
        },
        {
          heading: 'Anlässe, Reservationen und Gastronomie',
          paragraphs: [
            'Anlässe und Reservationen mit Warteliste, Privatanlässe, Besuche und Check-ins, Bar- und Restaurantrechnungen und die Verbindung zur Lightspeed-Kasse: Der [Gastronomieteil](page:hospitality) eines Clubs oder Betriebs lebt im selben CRM wie seine Mitglieder und Gäste.',
          ],
          link: 'hospitality',
        },
        {
          heading: 'Abrechnung und Zahlungen',
          paragraphs: [
            'Mitgliederkonten, wiederkehrende Beiträge und monatliche PDF-Auszüge, bezahlt per Karte, TWINT, SEPA oder QR-Rechnung. Bankdateien werden mit den offenen Beträgen abgeglichen, und der Buchhaltungsexport geht nach Bexio oder Abacus, aus einem Hauptbuch, das nur storniert und nie überschrieben wird. Mehr zu [Abrechnung und Zahlungen](page:billing).',
          ],
          bullets: [
            'Wiederkehrende Beiträge und Mitgliederkonten',
            'Monatliche PDF-Auszüge',
            'Karte, TWINT, SEPA und QR-Rechnung',
            'Export nach Bexio und Abacus',
          ],
          link: 'billing',
        },
        {
          heading: 'E-Mail und Akquise',
          paragraphs: [
            'Ein Postausgang verschickt alle E-Mails, versucht es erneut, wenn ein Server ablehnt, und verarbeitet Bounces. Sequenzen und Automationen fassen für Sie nach, Newsletter können Leads zur Terminbuchung einladen, und Kampagnen respektieren die Einwilligung mit Abmeldung per Klick. Mehr zur [E-Mail-Automatisierung](page:email).',
          ],
          link: 'email',
        },
        {
          heading: 'KI-Assistent',
          paragraphs: [
            'Der [KI-Assistent](page:ai) auf Basis von Claude liest Ihren Arbeitsbereich und schlägt Änderungen vor, die eine Person bestätigt. Der KI-Texter schreibt Verkaufs-E-Mails in Ihrem Ton, und das Morgenbriefing zeigt, was heute Aufmerksamkeit braucht.',
          ],
          link: 'ai',
        },
        {
          heading: 'Berichte, Datenschutz und Plattform',
          paragraphs: [
            'Berichte und Prognose, ein Audit-Log jeder Änderung, das Zusammenführen von Dubletten und Auskunfts- und Löschbegehren nach DSGVO und [Schweizer DSG](page:swiss). Anmeldung mit Google, Microsoft oder Passkey, Zwei-Faktor-Authentifizierung, Rollen und Berechtigungen für alle, mehrere Arbeitsbereiche und eine Systemüberwachung, die zeigt, dass alles läuft.',
          ],
          bullets: [
            'Berichte, Prognose und Audit-Log',
            'Datenschutzbegehren nach DSGVO und DSG',
            'Anmeldung mit Google, Microsoft oder Passkey',
            'Rollen, Berechtigungen und mehrere Arbeitsbereiche',
          ],
          link: 'swiss',
        },
      ],
      features: {
        heading: 'Gebaut für das ganze Team',
        intro: 'Vom Empfang bis zur Buchhaltung arbeiten alle mit denselben Datensätzen.',
        items: [
          { icon: 'leads', title: 'Lead-Tracker mit fünf Phasen', text: 'Jeder Lead hat eine Phase und einen nächsten Schritt.' },
          { icon: 'signature', title: 'Verträge mit E-Signatur', text: 'Aus Vorlagen, ohne Konto für die unterschreibende Person.' },
          { icon: 'members', title: 'Mitglieder und Haushalte', text: 'Aufnahmeanträge, Erneuerungen, Firmenmitgliedschaften.' },
          { icon: 'reservations', title: 'Anlässe und Reservationen', text: 'Wartelisten, Privatanlässe, Besuche und Check-ins.' },
          { icon: 'tabs', title: 'Konsumationen und Kasse', text: 'Barrechnungen, Mitgliederkonten, Lightspeed-Kasse.' },
          { icon: 'payments', title: 'Schweizer Zahlungen', text: 'Karte, TWINT, SEPA und QR-Rechnung, mit Bankabgleich.' },
          { icon: 'accounting', title: 'Buchhaltungsexport', text: 'Nach Bexio oder Abacus, aus einem nie überschriebenen Hauptbuch.' },
          { icon: 'automations', title: 'E-Mail-Automatisierung', text: 'Sequenzen, Automationen, Newsletter und Kampagnen.' },
          { icon: 'ai', title: 'KI-Assistent', text: 'Auf Basis von Claude: schlägt vor, schreibt, bereitet das Briefing vor.' },
          { icon: 'loyalty', title: 'Treue, Geschenkkarten, Spenden', text: 'Stammgäste belohnen, Geschenkkarten verkaufen, Spenden erfassen.' },
          { icon: 'roles', title: 'Rollen und Berechtigungen', text: 'Jede Person sieht und ändert, was ihre Rolle erlaubt.' },
          { icon: 'languages', title: 'Deutsch, Französisch, Englisch', text: 'Die ganze App in drei Sprachen, nach Wahl jeder Person.' },
        ],
      },
      steps: {
        heading: 'So startet ein Team mit CadenceE',
        items: [
          { title: 'Arbeitsbereich erstellen', text: 'Registrieren, Arbeitsbereich benennen und Kolleginnen und Kollegen mit ihrer Rolle einladen.' },
          { title: 'Daten übernehmen', text: 'Leads, Kontakte und Mitglieder aus Tabellen: zuerst ein Probelauf, danach jederzeit rückgängig.' },
          { title: 'Kanäle verbinden', text: 'Formular oder Webhook auf der Website, Vertragsvorlagen und Zahlungsmittel.' },
          { title: 'An einem Ort arbeiten', text: 'Vom Lead zur Unterschrift, vom Mitglied zum Auszug, mit Berichten und Morgenbriefing.' },
        ],
      },
      faq: {
        heading: 'Fragen zu den Funktionen',
        items: [
          { q: 'Ist CadenceE ein Vertriebs-CRM oder eine Clubsoftware?', a: 'Beides. Ein Arbeitsbereich kann den Vertrieb (Leads, Deals, Verträge), den Clubbetrieb (Mitglieder, Anlässe, Konsumationen, Abrechnung) oder beides zusammen nutzen. So wird ein Lead zum Mitglied, ohne das System zu wechseln.' },
          { q: 'In welchen Sprachen gibt es CadenceE?', a: 'Auf Deutsch, Französisch und Englisch, nach Wahl jeder Person. Vorbereitete E-Mails gibt es auf Französisch und Englisch.' },
          { q: 'Können wir bestehende Daten übernehmen?', a: 'Ja. Leads, Kontakte, Firmen und Mitglieder lassen sich aus Tabellen importieren. Jeder Import beginnt mit einem Probelauf, der das Ergebnis zeigt, und lässt sich rückgängig machen.' },
          { q: 'Wickelt CadenceE Zahlungen ab?', a: 'Ja: Karte, TWINT, SEPA und QR-Rechnung, Anzahlung bei der Vertragsunterzeichnung und Abgleich der Bankdateien mit den offenen Beträgen. Mehr zu [Abrechnung und Zahlungen](page:billing).' },
          { q: 'Ändert die KI Daten selbstständig?', a: 'Nein. Der [KI-Assistent](page:ai) schlägt Änderungen vor, und eine Person bestätigt jede einzelne, bevor etwas gespeichert wird.' },
          { q: 'Wie wird der Zugriff geregelt?', a: 'Anmeldung per E-Mail und Passwort, Google, Microsoft oder Passkey, optional mit Zwei-Faktor-Authentifizierung, Rollen und Berechtigungen für alle und ein Audit-Log jeder Änderung.' },
        ],
      },
      related: ['private-clubs', 'sales', 'swiss'],
      cta: {
        heading: 'Erleben Sie CadenceE mit Ihren eigenen Daten',
        text: 'Erstellen Sie einen Arbeitsbereich und importieren Sie eine erste Liste, oder fragen Sie eine Demo an.',
      },
    },

    'private-clubs': {
      eyebrow: 'CRM für Privatclubs',
      h1: 'Das CRM für',
      h1Accent: 'Privatclubs und Members’ Clubs',
      lead: 'Von der ersten Anfrage bis zum Monatsauszug führt CadenceE einen Privatclub an einem Ort: Aufnahmeanträge, Verträge und Anzahlungen, Mitglieder und Haushalte, Anlässe, Konsumationen und Erneuerungen. Entwickelt in der Schweiz, zusammen mit einem Club.',
      heroCardTitle: 'Was ein Club in CadenceE führt',
      heroPoints: [
        'Anfragen und Aufnahmeanträge über Ihre Website',
        'Mitgliedschaftsverträge online unterschrieben, mit Anzahlung',
        'Mitglieder, Haushalte und Firmenmitgliedschaften',
        'Anlässe, Reservationen und Privatanlässe',
        'Konsumationen, Beiträge und Monatsauszüge',
      ],
      intro: {
        heading: 'Warum ein Privatclub ein eigenes CRM braucht',
        paragraphs: [
          'Ein Privatclub verkauft kein Produkt, sondern einen Platz in einer Gemeinschaft. Der Weg zur Mitgliedschaft ist lang und persönlich: Man hört vom Club, bittet um einen Besuch, trifft das Team, stellt einen Aufnahmeantrag, unterschreibt, leistet die Eintrittsanzahlung und kommt danach für Dinners, Anlässe und an die Bar.',
          'Allgemeine CRMs hören beim unterschriebenen Deal auf, und Clubsoftware beginnt meist erst beim Mitglied. CadenceE deckt den ganzen Weg in einem Datensatz ab. Es wurde zusammen mit dem Pavillon 46 entwickelt, einem Privatclub in La Croix-sur-Lutry (VD), der damit täglich Mitgliedschaften verkauft und den Club führt.',
        ],
      },
      sections: [
        {
          heading: 'Von der Anfrage zum Mitglied in einem Datensatz',
          paragraphs: [
            'Eine Anfrage kommt über das Formular oder den Webhook Ihrer Website und wird zum Lead. Der [Tracker mit fünf Phasen](page:sales) führt ihn über Kontaktaufnahme, Termin, Vertrag und Unterschrift bis zur Anzahlung und hält fest, wer die Person empfohlen hat: Empfehlungen Ihrer Mitglieder gehen nicht mehr verloren.',
            'Sind Vertrag unterschrieben und Anzahlung geleistet, wird der Lead mit einem Schritt zum aktiven Mitglied. Die Willkommens-E-Mail geht hinaus und das Mitgliederportal öffnet sich, ohne doppelte Erfassung.',
          ],
          bullets: [
            'Öffentliche Anfrage- und Antragsformulare',
            'Empfehlung und Partner auf jedem Lead',
            'Mitgliedschaftsverträge aus Vorlagen, [online unterschrieben](page:e-signature)',
            'Umwandlung zum aktiven Mitglied in einem Schritt',
          ],
        },
        {
          heading: 'Mitgliedschaften, Haushalte und Erneuerungen',
          paragraphs: [
            'Mitglieder gehören zu Haushalten, und Firmen können Firmenmitgliedschaften für mehrere Mitarbeitende halten. Aufnahmeanträge werden in einer Liste bearbeitet. Erneuerungen tragen eine Risikobewertung, damit das Team die Mitglieder anruft, die sich entfernen, bevor sie gehen. Mehr zur [Mitgliederverwaltung](page:membership).',
          ],
          bullets: [
            'Haushalte und Firmenmitgliedschaften',
            'Aufnahmeanträge an einem Ort',
            'Erneuerungen mit Risikobewertung',
            'Ein Portal für jedes Mitglied',
          ],
        },
        {
          heading: 'Das Clubleben: Anlässe, Besuche und Konsumationen',
          paragraphs: [
            'Anlässe und Reservationen haben Wartelisten, und die Räume lassen sich für private Anlässe der Mitglieder buchen. Besuche und Check-ins zeigen, wer wie oft kommt. Konsumationen an Bar und Restaurant gehen auf die Rechnung, mit Verbindung zur Lightspeed-Kasse, und landen auf dem Mitgliederkonto.',
            'Am Monatsende erhält jedes Mitglied einen PDF-Auszug und bezahlt per Karte, TWINT, SEPA oder QR-Rechnung. Mehr zu [Abrechnung und Zahlungen](page:billing).',
          ],
        },
        {
          heading: 'Diskretion als Grundsatz',
          paragraphs: [
            'Die Mitgliederliste ist das sensibelste Gut eines Clubs. CadenceE gibt jeder Person eine Rolle mit genau den nötigen Berechtigungen, schreibt jede Änderung ins Audit-Log und unterstützt die Anmeldung mit Passkey und Zwei-Faktor-Authentifizierung. Auskunfts- und Löschbegehren werden nach DSGVO und Schweizer DSG bearbeitet.',
          ],
        },
      ],
      features: {
        heading: 'Alles, was ein Privatclub braucht',
        items: [
          { icon: 'applications', title: 'Aufnahmeanträge', text: 'Anträge über ein öffentliches Formular, Entscheid in einer Liste.' },
          { icon: 'contract', title: 'Mitgliedschaftsverträge', text: 'Aus Vorlagen erstellt und zur E-Signatur versandt.' },
          { icon: 'deposit', title: 'Eintrittsanzahlung', text: 'Bei Unterzeichnung eingezogen, per Karte oder QR-Rechnung.' },
          { icon: 'households', title: 'Haushalte', text: 'Partner und Familienmitglieder in einem Haushalt.' },
          { icon: 'corporate', title: 'Firmenmitgliedschaften', text: 'Eine Firma hält Mitgliedschaften für mehrere Mitarbeitende.' },
          { icon: 'renewals', title: 'Erneuerungen mit Risikobewertung', text: 'Sehen Sie, wer vielleicht nicht erneuert, solange Zeit bleibt.' },
          { icon: 'events', title: 'Anlässe und Wartelisten', text: 'Anlässe publizieren, reservieren, frei gewordene Plätze füllen.' },
          { icon: 'private-event', title: 'Privatanlässe', text: 'Private Anlässe der Mitglieder, von der Anfrage bis zur Rechnung.' },
          { icon: 'tabs', title: 'Konsumationen und Mitgliederkonten', text: 'Bar- und Restaurantrechnungen fliessen aufs Mitgliederkonto.' },
          { icon: 'statements', title: 'Monatsauszüge', text: 'Jeden Monat ein PDF-Auszug für jedes Mitglied.' },
          { icon: 'loyalty', title: 'Treue, Geschenkkarten, Spenden', text: 'Treue belohnen, Geschenkkarten verkaufen, Spenden erfassen.' },
          { icon: 'portal', title: 'Mitgliederportal', text: 'Anmeldung mit einem Link per E-Mail.' },
        ],
      },
      steps: {
        heading: 'So wechselt ein Club zu CadenceE',
        items: [
          { title: 'Club einrichten', text: 'Arbeitsbereich erstellen, Vertragsvorlagen und Zahlungsmittel hinzufügen, Team einladen.' },
          { title: 'Mitglieder und Interessenten importieren', text: 'Bestehende Mitglieder und offene Anfragen aus Tabellen übernehmen, zuerst mit Probelauf.' },
          { title: 'Online die Türen öffnen', text: 'Anfrage- oder Antragsformular auf die Website stellen: Neues Interesse landet direkt im CRM.' },
          { title: 'Die Saison führen', text: 'Interessenten bis zur Unterschrift begleiten, neue Mitglieder begrüssen, Anlässe durchführen, Auszüge versenden.' },
        ],
      },
      faq: {
        heading: 'CRM für Privatclubs: häufige Fragen',
        items: [
          { q: 'Wer nutzt CadenceE heute?', a: 'Der Pavillon 46, ein Privatclub in La Croix-sur-Lutry (VD), nutzt CadenceE täglich, um Mitgliedschaften zu verkaufen und den Club zu führen. CadenceE wurde zusammen mit ihm entwickelt.' },
          { q: 'Können sich künftige Mitglieder online bewerben?', a: 'Ja. Der Club veröffentlicht ein Antrags- und ein Anfrageformular; die Eingänge kommen als Aufnahmeanträge oder Leads an, mit Dublettenschutz, und das Team entscheidet in einer Liste.' },
          { q: 'Wie wird aus einem Interessenten ein Mitglied?', a: 'Der Lead durchläuft Kontaktaufnahme, Termin, Vertrag, Unterschrift und Anzahlung. Sind Vertrag unterschrieben und Anzahlung bezahlt, macht ein Schritt daraus ein aktives Mitglied, verschickt die Willkommens-E-Mail und öffnet das Mitgliederportal.' },
          { q: 'Können wir Bar und Restaurant den Mitgliedern verrechnen?', a: 'Ja. Konsumationen gehen auf die Rechnung, mit Verbindung zur Lightspeed-Kasse, und aufs Mitgliederkonto. Jeden Monat erhält das Mitglied einen PDF-Auszug und bezahlt per Karte, TWINT, SEPA oder QR-Rechnung.' },
          { q: 'Bleiben die Mitgliederdaten vertraulich?', a: 'Jede Person hat eine Rolle mit genauen Berechtigungen, jede Änderung steht im Audit-Log, und die Anmeldung unterstützt Passkeys und Zwei-Faktor-Authentifizierung. Auskunfts- und Löschbegehren folgen DSGVO und DSG.' },
          { q: 'Funktioniert CadenceE auf Deutsch und Französisch?', a: 'Ja. Die ganze App gibt es auf Deutsch, Französisch und Englisch, und jede Person wählt ihre Sprache. Mehr zum [Schweizer CRM](page:swiss).' },
        ],
      },
      related: ['membership', 'billing', 'hospitality'],
      cta: {
        heading: 'Führen Sie Ihren Club mit einem CRM',
        text: 'Erstellen Sie den Arbeitsbereich Ihres Clubs, oder fragen Sie eine Demo beim Team an, das ihn entwickelt hat.',
      },
    },

    membership: {
      eyebrow: 'Software für die Mitgliederverwaltung',
      h1: 'Software für die Mitgliederverwaltung,',
      h1Accent: 'gemacht für Clubs',
      lead: 'Alle Mitglieder, Haushalte und Firmenmitgliedschaften an einem Ort, Erneuerungen mit Risikobewertung im Blick und ein eigenes Portal für jedes Mitglied. CadenceE verwaltet die Mitgliedschaft und das Geld, das dazugehört.',
      heroCardTitle: 'Mitglieder in CadenceE',
      heroPoints: [
        'Mitglieder, Haushalte und Firmenmitgliedschaften',
        'Aufnahmeanträge in einer Liste',
        'Erneuerungen mit Risikobewertung',
        'Mitgliederportal mit Anmeldung per E-Mail-Link',
        'Wiederkehrende Beiträge und Monatsauszüge',
      ],
      intro: {
        heading: 'Eine Mitgliedschaft ist eine Beziehung, keine Tabellenzeile',
        paragraphs: [
          'Eine Tabelle kann Mitglieder auflisten. Sie sagt Ihnen aber nicht, dass sich die Mitgliedschaft einer Familie nächsten Monat erneuert, dass jemand seit dem Frühling nicht mehr da war oder dass der letzte Auszug noch offen ist. Eine Software für die Mitgliederverwaltung sollte das.',
          'CadenceE verbindet den Mitglieder-Datensatz mit allem rundherum: dem Haushalt, der Firma, die eine Firmenmitgliedschaft bezahlt, den gebuchten Anlässen, den Besuchen, den Konsumationen und den Beiträgen. Das Team sieht das ganze Bild, und jedes Mitglied sieht seines im Portal.',
        ],
      },
      sections: [
        {
          heading: 'Mitglieder, Haushalte und Firmenmitgliedschaften',
          paragraphs: [
            'Jedes Mitglied hat einen Datensatz mit Kontaktdaten, Mitgliedschaft, Sprache und Verlauf. Partner und Kinder lassen sich zu einem Haushalt zusammenfassen, damit Kommunikation und Abrechnung der Familie folgen statt der einzelnen Person. Firmen können Firmenmitgliedschaften für mehrere Mitarbeitende halten.',
          ],
          bullets: [
            'Ein Datensatz pro Mitglied, mit ganzem Verlauf',
            'Haushalte für Familien und Paare',
            'Firmenmitgliedschaften',
            'Dublettenschutz und Zusammenführen',
          ],
        },
        {
          heading: 'Aufnahmeanträge und neue Mitglieder',
          paragraphs: [
            'Künftige Mitglieder bewerben sich über ein öffentliches Antragsformular auf Ihrer Website. Die Anträge landen in einer Liste, in der Vorstand oder Team entscheiden. Wird die Mitgliedschaft über den [Vertriebsprozess](page:sales) verkauft, machen ein unterschriebener Vertrag und eine bezahlte Anzahlung den Lead in einem Schritt zum aktiven Mitglied, mit Willkommens-E-Mail.',
          ],
        },
        {
          heading: 'Erneuerungen, bevor es Kündigungen werden',
          paragraphs: [
            'Jede Erneuerung hat eine Risikobewertung: Sie wissen, welche Mitglieder wohl gehen werden, solange noch Zeit für einen Anruf bleibt. Erneuerungen, Besuche und Konsumationen stehen nebeneinander, das erleichtert das Gespräch: Sie wissen, wann jemand zuletzt da war und was ihm gefallen hat.',
          ],
        },
        {
          heading: 'Beiträge, Auszüge und Mitgliederportal',
          paragraphs: [
            'Wiederkehrende Beiträge werden fristgerecht verrechnet, zusammen mit Konsumationen und weiteren Positionen, und jedes Mitglied erhält einen monatlichen PDF-Auszug. Bezahlt wird per Karte, TWINT, SEPA oder QR-Rechnung (siehe [Abrechnung und Zahlungen](page:billing)). Im Mitgliederportal melden sich Mitglieder mit einem Link per E-Mail an, ganz ohne Passwort.',
          ],
        },
      ],
      features: {
        heading: 'Werkzeuge für die Mitgliederverwaltung',
        items: [
          { icon: 'members', title: 'Mitglieder-Datensätze', text: 'Kontaktdaten, Mitgliedschaft, Sprache und Verlauf an einem Ort.' },
          { icon: 'households', title: 'Haushalte', text: 'Die Familie zusammen für Kommunikation und Abrechnung.' },
          { icon: 'corporate', title: 'Firmenmitgliedschaften', text: 'Mehrere Mitarbeitende unter einer Firma.' },
          { icon: 'applications', title: 'Online-Aufnahmeanträge', text: 'Ein öffentliches Formular füllt eine Entscheidungsliste.' },
          { icon: 'renewals', title: 'Erneuerungen mit Risiko', text: 'Eine Bewertung zeigt, wer vielleicht geht.' },
          { icon: 'portal', title: 'Mitgliederportal', text: 'Anmeldung per E-Mail-Link, ohne Passwort.' },
          { icon: 'dues', title: 'Wiederkehrende Beiträge', text: 'Fristgerecht verrechnet, mit Konsumationen und Positionen.' },
          { icon: 'statements', title: 'PDF-Auszüge', text: 'Ein Monatsauszug für jedes Mitglied.' },
          { icon: 'visits', title: 'Besuche und Check-ins', text: 'Wer kommt, und wie oft.' },
          { icon: 'events', title: 'Anlässe und Reservationen', text: 'Mitglieder reservieren, mit Warteliste, wenn es voll ist.' },
          { icon: 'newsletter', title: 'Mitgliederkommunikation', text: 'Newsletter und Kampagnen mit Einwilligung und Abmeldung per Klick.' },
          { icon: 'privacy', title: 'Datenschutzbegehren', text: 'Auskunft und Löschung nach DSGVO und DSG.' },
        ],
      },
      steps: {
        heading: 'Von der Tabelle zur Mitgliederverwaltung',
        items: [
          { title: 'Mitglieder importieren', text: 'Laden Sie Ihre Liste hoch: Ein Probelauf zeigt, was entsteht, bevor etwas gespeichert wird, und der Import lässt sich rückgängig machen.' },
          { title: 'Gruppieren und einordnen', text: 'Haushalte bilden, Firmenmitgliedschaften verknüpfen und wiederkehrende Beiträge festlegen.' },
          { title: 'Portal öffnen', text: 'Laden Sie die Mitglieder ins Portal ein, wo sie sich per E-Mail-Link anmelden.' },
          { title: 'Erneuerungen verfolgen', text: 'Nach Risikobewertung sortieren und mit Mitgliedern sprechen, bevor sie sich zum Gehen entscheiden.' },
        ],
      },
      faq: {
        heading: 'Mitgliederverwaltung: häufige Fragen',
        items: [
          { q: 'Was ist eine Software für die Mitgliederverwaltung?', a: 'Eine Software, die die Mitglieder eines Clubs, ihre Mitgliedschaften, Erneuerungen und Zahlungen an einem Ort führt. CadenceE ergänzt sie um einen Vertriebsprozess, der künftige Mitglieder ab der ersten Anfrage begleitet. Siehe [CRM für Privatclubs](page:private-clubs).' },
          { q: 'Kann eine Familie eine Mitgliedschaft teilen?', a: 'Mitglieder lassen sich zu einem Haushalt zusammenfassen: Partner und Kinder werden für Kommunikation und Abrechnung gemeinsam behandelt.' },
          { q: 'Unterstützen Sie Firmenmitgliedschaften?', a: 'Ja. Eine Firma kann eine Mitgliedschaft für mehrere Mitarbeitende halten, jede Person mit eigenem Mitglieder-Datensatz.' },
          { q: 'Wozu dient die Risikobewertung bei Erneuerungen?', a: 'Sie zeigt, wie wahrscheinlich ein Mitglied nicht erneuert, damit sich das Team vor dem Stichtag auf diese Mitglieder konzentrieren kann.' },
          { q: 'Brauchen Mitglieder ein Passwort für das Portal?', a: 'Nein. Sie melden sich mit einem Link an, den sie per E-Mail erhalten.' },
          { q: 'Können wir unsere bestehende Mitgliederliste importieren?', a: 'Ja. Der Import beginnt mit einem Probelauf, der das Ergebnis zeigt, und lässt sich bei einem Fehler rückgängig machen.' },
        ],
      },
      related: ['private-clubs', 'billing', 'email'],
      cta: {
        heading: 'Geben Sie Ihren Mitgliedern die Aufmerksamkeit, die sie erwarten',
        text: 'Erstellen Sie einen Arbeitsbereich und importieren Sie Ihre Mitgliederliste, oder fragen Sie eine Demo an.',
      },
    },

    hospitality: {
      eyebrow: 'CRM für Gastronomie und Hotellerie',
      h1: 'Das CRM für Gastronomie und Hotellerie:',
      h1Accent: 'Restaurants, Hotels und Eventlocations',
      lead: 'CadenceE verbindet den Gast, die Reservation und die Rechnung: Reservationen mit Warteliste, Privatanlässe, Bar- und Restaurantrechnungen mit Lightspeed-Kasse und Gäste-E-Mails, in einem CRM aus der Schweiz.',
      heroCardTitle: 'Für Gastgeber-Teams',
      heroPoints: [
        'Reservationen und Anlässe mit Warteliste',
        'Privatanlässe, von der Anfrage bis zur Anzahlung',
        'Bar- und Restaurantrechnungen, Lightspeed-Kasse',
        'Kundenkonten und Monatsauszüge',
        'Gäste-Newsletter mit Einwilligung',
      ],
      intro: {
        heading: 'Kennen Sie Ihre Gäste, nicht nur Ihre Gedecke',
        paragraphs: [
          'In der Gastronomie kommen die besten Gäste wieder. Doch was Sie über sie wissen, ist verstreut: die Reservation in einem System, die Rechnung in der Kasse, die Anfrage für den Anlass in einem Postfach und die Newsletter-Liste wieder woanders.',
          'CadenceE bringt alles beim Gast zusammen. Reservationen, Privatanlässe, Konsumationen und E-Mails eines Stammgasts stehen in einem Datensatz: Das Team erkennt ihn, lädt ihn wieder ein und verrechnet richtig. Es ist das CRM des Pavillon 46, eines Clubs mit Restaurant und Bar in La Croix-sur-Lutry (VD).',
        ],
      },
      sections: [
        {
          heading: 'Reservationen, Anlässe und Wartelisten',
          paragraphs: [
            'Publizieren Sie Anlässe, nehmen Sie Reservationen an und führen Sie eine Warteliste, wenn alles belegt ist: Eine Absage wird zur Reservation statt zu einem leeren Platz. Besuche und Check-ins zeigen, wer wirklich da war, und die Gastronomieberichte stehen neben den Zahlen aus Vertrieb und Mitgliedschaft.',
          ],
        },
        {
          heading: 'Privatanlässe, von der Anfrage bis zur Anzahlung',
          paragraphs: [
            'Private Dinners, Apéros und Firmenanlässe beginnen als Anfrage. CadenceE verfolgt sie wie jede Verkaufschance: Der [Prozess mit fünf Phasen](page:sales) führt die Anfrage über Termin, Vertrag und Unterschrift bis zur Anzahlung, und der Vertrag wird mit [elektronischer Signatur](page:e-signature) online unterschrieben.',
          ],
          bullets: [
            'Anlass-Anfragen über Ihre Website',
            'Verträge aus Ihren Vorlagen',
            'Anzahlung bei Unterzeichnung, Karte oder QR-Rechnung',
            'Der Anlass im Datensatz des Gasts',
          ],
        },
        {
          heading: 'Konsumationen, Kasse und Kundenkonten',
          paragraphs: [
            'Konsumationen an Bar und Restaurant gehen auf die Rechnung. Die Verbindung zur Lightspeed-Kasse bringt die Verkäufe ins CRM, und Stammgäste oder Mitglieder können ein Konto führen, das monatlich mit einem PDF-Auszug beglichen wird. Bezahlt wird per Karte, TWINT, SEPA oder QR-Rechnung; mehr zu [Abrechnung und Zahlungen](page:billing).',
          ],
        },
        {
          heading: 'Was CadenceE nicht ist',
          paragraphs: [
            'CadenceE ist ein CRM, kein Hotel-PMS: Es verwaltet weder Zimmerbestand noch Buchungen von Plattformen. Mit Ihrer Kasse arbeitet es über die Lightspeed-Verbindung zusammen, statt sie zu ersetzen. Seine Aufgabe ist die Beziehung rund um Aufenthalt oder Essen: die Anfrage, den Anlass, das Konto und das Nachfassen.',
          ],
        },
      ],
      features: {
        heading: 'Funktionen für Gastronomie und Hotellerie',
        items: [
          { icon: 'reservations', title: 'Reservationen', text: 'Für Anlässe und Dinners, mit Warteliste.' },
          { icon: 'private-event', title: 'Privatanlässe', text: 'Anfrage, Vertrag, Anzahlung und Rechnung für private Anlässe.' },
          { icon: 'tabs', title: 'Bar- und Restaurantrechnungen', text: 'Konsumationen pro Gast oder Mitglied.' },
          { icon: 'pos', title: 'Lightspeed-Kasse', text: 'Verkäufe aus der Kasse gelangen ins CRM.' },
          { icon: 'house-account', title: 'Kundenkonten', text: 'Stammgäste begleichen einmal im Monat.' },
          { icon: 'statements', title: 'Monatliche PDF-Auszüge', text: 'Ein klarer Auszug für jedes Konto.' },
          { icon: 'visits', title: 'Besuche und Check-ins', text: 'Wer da war, wann und wie oft.' },
          { icon: 'loyalty', title: 'Treue', text: 'Belohnen Sie Gäste, die wiederkommen.' },
          { icon: 'gift-card', title: 'Geschenkkarten', text: 'Geschenkkarten verkaufen und Guthaben verfolgen.' },
          { icon: 'newsletter', title: 'Gäste-Newsletter', text: 'Neuigkeiten und Einladungen mit Einwilligung und Abmeldung per Klick.' },
          { icon: 'payments', title: 'Schweizer Zahlungen', text: 'Karte, TWINT, SEPA und QR-Rechnung.' },
          { icon: 'reports', title: 'Berichte', text: 'Gastronomieberichte neben Vertrieb und Mitgliedschaft.' },
        ],
      },
      steps: {
        heading: 'So startet ein Betrieb mit CadenceE',
        items: [
          { title: 'Betrieb einrichten', text: 'Arbeitsbereich erstellen, Anlasstypen und Zahlungsmittel hinzufügen, Lightspeed verbinden.' },
          { title: 'Gästeliste übernehmen', text: 'Gäste und Stammgäste aus Tabellen importieren, mit Probelauf und Rückgängig-Funktion.' },
          { title: 'Anfragen online empfangen', text: 'Formular auf die Website stellen: Anfragen für Privatanlässe kommen als Leads an.' },
          { title: 'Bewirten, verrechnen, nachfassen', text: 'Reservationen und Konsumationen, Monatsauszüge und Newsletter, die Gäste zurückbringen.' },
        ],
      },
      faq: {
        heading: 'CRM für Gastronomie: häufige Fragen',
        items: [
          { q: 'Ist CadenceE ein Reservationssystem für Restaurants?', a: 'CadenceE nimmt Reservationen für Anlässe und Dinners an, mit Warteliste, neben Verlauf und Rechnung des Gasts. Es ist in erster Linie ein CRM, gebaut um die Beziehung zum Gast.' },
          { q: 'Ersetzt CadenceE unsere Kasse?', a: 'Nein. CadenceE verbindet sich mit der Lightspeed-Kasse, damit die Verkäufe ins CRM gelangen, und führt Konsumationen und Kundenkonten; die Kasse bleibt Ihre Kasse.' },
          { q: 'Kann es Hotelzimmer verwalten?', a: 'Nein. CadenceE ist kein PMS und verwaltet keinen Zimmerbestand. Hotels nutzen es für Privatanlässe, Firmenkunden, Gästebeziehungen und die Abrechnung von Konten.' },
          { q: 'Wie werden Privatanlässe abgewickelt?', a: 'Eine Anlass-Anfrage wird zum Lead und durchläuft die fünf Phasen: Kontaktaufnahme, Termin, Vertrag, Unterschrift und Anzahlung. Der Vertrag wird online unterschrieben und die Anzahlung bei der Unterzeichnung eingezogen.' },
          { q: 'Mit welchen Zahlungsmitteln können Gäste bezahlen?', a: 'Karte, TWINT, SEPA und QR-Rechnung.' },
          { q: 'Können wir unseren Gästen E-Mails senden?', a: 'Ja. Newsletter und Kampagnen gehen über einen Postausgang, mit Einwilligung und Abmeldung per Klick. Mehr zur [E-Mail-Automatisierung](page:email).' },
        ],
      },
      related: ['private-clubs', 'e-signature', 'billing'],
      cta: {
        heading: 'Gäste, Anlässe und Rechnungen an einem Ort',
        text: 'Erstellen Sie den Arbeitsbereich Ihres Betriebs, oder fragen Sie eine Demo an.',
      },
    },

    sales: {
      eyebrow: 'Vertriebs-CRM',
      h1: 'Das Vertriebs-CRM, das jeden Lead begleitet,',
      h1Accent: 'bis zur Anzahlung',
      lead: 'CadenceE führt jeden Lead durch fünf Phasen (Kontaktaufnahme, Termin, Vertrag, Unterschrift und Anzahlung), mit Lead-Scoring, Zuweisung und Dublettenschutz, einem Deal-Board und einem KI-Texter für die E-Mails dazwischen.',
      heroCardTitle: 'Die fünf Phasen',
      heroPoints: [
        'Kontaktaufnahme: erster Kontakt und Nachfassen',
        'Termin: Besuch, Anruf oder Demo',
        'Vertrag: aus Ihrer Vorlage erstellt',
        'Unterschrift: online, ohne Konto',
        'Anzahlung: bei Unterzeichnung, Karte oder QR-Rechnung',
      ],
      intro: {
        heading: 'Eine Pipeline, die mit Geld auf dem Konto endet',
        paragraphs: [
          'Viele Pipelines enden bei «gewonnen». In Wirklichkeit ist ein Geschäft erst abgeschlossen, wenn der Vertrag unterschrieben und die Anzahlung eingegangen ist. Der Lead-Tracker von CadenceE ist genau darauf gebaut: fünf Phasen, jede mit einem klaren nächsten Schritt, bis zur Anzahlung.',
          'Weil Verträge, elektronische Signatur und Zahlungen im selben CRM liegen, bewegt sich die Pipeline, sobald der Kunde unterschreibt und bezahlt. Das Team verbringt seine Zeit mit Gesprächen statt mit dem Nachführen von Tabellen.',
        ],
      },
      sections: [
        {
          heading: 'Fünf Phasen, immer ein nächster Schritt',
          paragraphs: [
            'Kontaktaufnahme, Termin, Vertrag, Unterschrift, Anzahlung. Jeder Lead steht in einer Phase, mit seinen Aufgaben und Aktivitäten, sodass ihn jede Person im Team übernehmen kann. Das Deal-Board zeigt die Chancen als Spalten zum Verschieben, und gespeicherte Ansichten halten jedem seine eigene Liste bereit.',
          ],
          bullets: [
            'Leads und Deals auf einem Board',
            'Aufgaben und Aktivitäten auf jedem Lead',
            'Gespeicherte Ansichten für alle',
            'Kalender für Termine und Nachfassen',
          ],
        },
        {
          heading: 'Lead-Scoring, Zuweisung und saubere Daten',
          paragraphs: [
            'Leads werden bewertet, damit die vielversprechendsten zuerst Aufmerksamkeit bekommen, und Zuweisungsregeln leiten jeden neuen Lead an die richtige Person. Der Dublettenschutz erkennt dieselbe Person oder Firma, die zweimal ankommt, und Dubletten lassen sich zusammenführen. Jeder Lead hält fest, wer ihn empfohlen hat und über welchen Partner er kam.',
          ],
        },
        {
          heading: 'Leads, die von selbst ankommen',
          paragraphs: [
            'Öffentliche Anfrageformulare und ein Website-Webhook erstellen Leads direkt von Ihrer Website. Bestehende Listen werden aus Tabellen importiert: Ein Probelauf zeigt zuerst das Ergebnis, und der Import lässt sich rückgängig machen.',
          ],
        },
        {
          heading: 'Die nächste E-Mail, in Sekunden entworfen',
          paragraphs: [
            'Der KI-Texter schreibt E-Mails, Nachfassmeldungen und Notizen in Ihrem Ton. Sequenzen verschicken das Nachfassen nach Zeitplan, und Newsletter können Leads einladen, über eine Buchungsseite einen Termin zu buchen. Mehr zur [E-Mail-Automatisierung](page:email) und zum [KI-Assistenten](page:ai).',
          ],
        },
      ],
      features: {
        heading: 'Vertriebsfunktionen',
        items: [
          { icon: 'leads', title: 'Lead-Tracker mit fünf Phasen', text: 'Kontaktaufnahme, Termin, Vertrag, Unterschrift, Anzahlung.' },
          { icon: 'deals', title: 'Deal-Board', text: 'Chancen per Drag-and-drop von Phase zu Phase.' },
          { icon: 'score', title: 'Lead-Scoring', text: 'Konzentrieren Sie sich auf die Leads mit den besten Chancen.' },
          { icon: 'partner', title: 'Empfehlungen und Partner', text: 'Wer jeden Lead empfohlen hat, und über welchen Partner.' },
          { icon: 'duplicates', title: 'Dublettenschutz', text: 'Kein Lead doppelt erfasst, Dubletten zusammenführbar.' },
          { icon: 'import', title: 'Importe mit Probelauf', text: 'Listen ohne Risiko übernehmen, mit Rückgängig-Funktion.' },
          { icon: 'forms', title: 'Anfrageformulare', text: 'Öffentliche Formulare, die Leads erstellen.' },
          { icon: 'webhook', title: 'Website-Webhook', text: 'Leads von Ihrer Website oder einem anderen Werkzeug.' },
          { icon: 'signature', title: 'Elektronische Signatur', text: 'Verträge online unterschrieben, ohne Konto.' },
          { icon: 'deposit', title: 'Anzahlung bei Unterzeichnung', text: 'Per Karte oder QR-Rechnung.' },
          { icon: 'writer', title: 'KI-Texter', text: 'E-Mails und Nachfassen in Ihrem Ton.' },
          { icon: 'forecast', title: 'Berichte und Prognose', text: 'Was voraussichtlich abschliesst, und wann.' },
        ],
      },
      steps: {
        heading: 'So arbeitet ein Verkaufsteam in CadenceE',
        items: [
          { title: 'Erfassen', text: 'Leads kommen über Formulare, den Webhook, Importe oder das Team selbst.' },
          { title: 'Qualifizieren und zuweisen', text: 'Scoring und Zuweisungsregeln bringen jeden Lead zur richtigen Person.' },
          { title: 'Treffen und anbieten', text: 'Termin vereinbaren, dann einen Vertrag aus Ihrer Vorlage senden.' },
          { title: 'Unterschreiben und kassieren', text: 'Der Kunde unterschreibt online und leistet die Anzahlung: Der Lead erreicht seine letzte Phase.' },
        ],
      },
      faq: {
        heading: 'Vertriebs-CRM: häufige Fragen',
        items: [
          { q: 'Was sind die fünf Phasen?', a: 'Kontaktaufnahme, Termin, Vertrag, Unterschrift und Anzahlung. Der Lead rückt mit jedem erledigten Schritt vor, und die letzte Phase endet mit der bezahlten Anzahlung.' },
          { q: 'Können Leads über unsere Website hereinkommen?', a: 'Ja, über ein öffentliches Anfrageformular oder einen Website-Webhook. Beide erstellen Leads direkt, mit Dublettenschutz.' },
          { q: 'Wie funktioniert die Lead-Zuweisung?', a: 'Zuweisungsregeln leiten jeden neuen Lead an die richtige Person, und ein Lead lässt sich jederzeit neu zuweisen.' },
          { q: 'Was macht der KI-Texter?', a: 'Er entwirft Verkaufs-E-Mails, Nachfassmeldungen und Notizen im Ton Ihrer Marke, die eine Person prüft und versendet. Mehr zum [KI-Assistenten](page:ai).' },
          { q: 'Gibt es eine Prognose?', a: 'Ja. Die Berichte enthalten eine Prognose der Pipeline neben der Aktivität des Teams.' },
          { q: 'Was passiert nach der Unterschrift?', a: 'In einem Club wird ein Lead, der unterschrieben und bezahlt hat, in einem Schritt zum aktiven Mitglied, mit Willkommens-E-Mail und Mitgliederportal. Siehe [CRM für Privatclubs](page:private-clubs).' },
        ],
      },
      related: ['e-signature', 'email', 'ai'],
      cta: {
        heading: 'Begleiten Sie jeden Lead bis zur Anzahlung',
        text: 'Erstellen Sie einen Arbeitsbereich und importieren Sie Ihre Leads, oder fragen Sie eine Demo an.',
      },
    },

    'e-signature': {
      eyebrow: 'Verträge und E-Signatur',
      h1: 'Verträge mit elektronischer Signatur',
      h1Accent: 'und Anzahlung bei Unterzeichnung',
      lead: 'Erstellen Sie Verträge aus Ihren Vorlagen, lassen Sie sie online unterschreiben und ziehen Sie die Anzahlung im selben Schritt ein, per Karte oder QR-Rechnung. Wer unterschreibt, braucht kein Konto, und Papierverträge lassen sich ebenfalls hochladen.',
      heroCardTitle: 'Von der Vorlage zur Anzahlung',
      heroPoints: [
        'Verträge aus Ihren Vorlagen',
        'Online unterschrieben, ohne Konto',
        'Anzahlung per Karte oder QR-Rechnung',
        'Papierverträge hochgeladen',
        'Der Lead rückt von selbst vor',
      ],
      intro: {
        heading: 'Der langsamste Schritt in den meisten Verkäufen',
        paragraphs: [
          'Der Kunde hat zugesagt. Dann wird der Vertrag im Textprogramm geschrieben, als PDF exportiert, gemailt, ausgedruckt, unterschrieben, eingescannt und zurückgeschickt, und eine Woche später denkt jemand an die Anzahlung. Jeder Schritt gibt dem Geschäft Zeit, abzukühlen.',
          'CadenceE macht daraus einen einzigen Link. Der Vertrag entsteht aus Ihrer Vorlage mit den Daten des Leads, der Kunde liest und unterschreibt ihn online, und die Anzahlung wird gleich mit der Unterschrift angefordert. Der Lead rückt in die nächste Phase, sobald das erledigt ist.',
        ],
      },
      sections: [
        {
          heading: 'Verträge aus Ihren Vorlagen',
          paragraphs: [
            'Schreiben Sie jeden Vertrag einmal als Vorlage, mit Feldern für Name, Mitgliedschaft oder Anlass, Daten und Beträge. CadenceE füllt sie aus dem Lead aus: Jeder Vertrag stimmt beim ersten Mal.',
          ],
        },
        {
          heading: 'Unterschreiben ohne Konto',
          paragraphs: [
            'Wer unterschreibt, öffnet einen sicheren Link, liest den Vertrag und unterschreibt online. Ohne Konto und ohne Installation, so wie es Kunden erwarten, die eine Mitgliedschaft oder einen Anlass buchen.',
          ],
        },
        {
          heading: 'Die Anzahlung, bei der Unterzeichnung eingezogen',
          paragraphs: [
            'Ein Vertrag kann bei der Unterzeichnung eine Anzahlung verlangen. Der Kunde bezahlt per Karte oder mit QR-Rechnung, und die Zahlung wird auf dem Vertrag verbucht. Der [Vertriebsprozess](page:sales) erreicht seine letzte Phase, die Anzahlung, ohne dass jemand nachfassen muss.',
          ],
          bullets: [
            'Kartenzahlung bei Unterzeichnung',
            'QR-Rechnung für die Überweisung',
            'Zahlung auf dem Vertrag verbucht',
            'Später mit Ihrer Bankdatei abgeglichen',
          ],
        },
        {
          heading: 'Papier funktioniert weiterhin',
          paragraphs: [
            'Manche Kunden bevorzugen Papier, und manche Verträge werden persönlich unterschrieben. Laden Sie das unterschriebene Exemplar hoch: CadenceE verbucht es wie jeden unterschriebenen Vertrag, und Pipeline und Kundendatensatz bleiben vollständig. In einem Club wird der unterschriebene Lead danach zum Mitglied: siehe [CRM für Privatclubs](page:private-clubs).',
          ],
        },
      ],
      features: {
        heading: 'Vertragsfunktionen',
        items: [
          { icon: 'contract', title: 'Vertragsvorlagen', text: 'Wiederverwendbar, aus dem Lead ausgefüllt.' },
          { icon: 'signature', title: 'Elektronische Signatur', text: 'Ein sicherer Link zum Lesen und Unterschreiben.' },
          { icon: 'portal', title: 'Kein Konto nötig', text: 'Wer unterschreibt, erstellt kein Konto.' },
          { icon: 'deposit', title: 'Anzahlung bei Unterzeichnung', text: 'Per Karte oder QR-Rechnung, auf dem Vertrag verbucht.' },
          { icon: 'paper', title: 'Papierverträge', text: 'Von Hand unterschriebene Verträge hochladen.' },
          { icon: 'leads', title: 'Aktuelle Pipeline', text: 'Der Lead rückt selbst zu Unterschrift und Anzahlung vor.' },
          { icon: 'members', title: 'Zur Mitgliedschaft', text: 'Im Club wird ein unterschriebener, bezahlter Lead in einem Schritt zum aktiven Mitglied.' },
          { icon: 'audit', title: 'Audit-Log', text: 'Jede Änderung wird mit ihrer Urheberin oder ihrem Urheber festgehalten.' },
          { icon: 'outbox', title: 'Über Ihren Postausgang', text: 'Vertrags-E-Mails gehen über das CRM hinaus, mit erneuten Versuchen.' },
        ],
      },
      steps: {
        heading: 'So wird ein Vertrag unterschrieben',
        items: [
          { title: 'Vorbereiten', text: 'Vorlage wählen; CadenceE füllt die Daten des Leads aus.' },
          { title: 'Senden', text: 'Der Kunde erhält einen Link per E-Mail.' },
          { title: 'Unterschreiben und bezahlen', text: 'Er liest, unterschreibt online und leistet die Anzahlung per Karte oder QR-Rechnung.' },
          { title: 'Erledigt', text: 'Der Vertrag liegt im Datensatz, und der Lead rückt vor.' },
        ],
      },
      faq: {
        heading: 'Verträge mit E-Signatur: häufige Fragen',
        items: [
          { q: 'Braucht die unterschreibende Person ein CadenceE-Konto?', a: 'Nein. Sie öffnet den erhaltenen Link, liest den Vertrag und unterschreibt online, ohne ein Konto zu erstellen.' },
          { q: 'Können wir bei der Unterzeichnung eine Anzahlung einziehen?', a: 'Ja. Die Anzahlung wird bei der Unterzeichnung per Karte oder QR-Rechnung bezahlt und auf dem Vertrag verbucht.' },
          { q: 'Und wenn ein Kunde auf Papier unterschreibt?', a: 'Laden Sie das unterschriebene Exemplar hoch. CadenceE verbucht es als unterschrieben, und Lead und Kundendatensatz bleiben vollständig.' },
          { q: 'Entstehen Verträge aus Vorlagen?', a: 'Ja. Sie schreiben jeden Vertragstyp einmal als Vorlage, und CadenceE setzt die Daten des Kunden ein.' },
          { q: 'Was passiert nach der Unterschrift?', a: 'Der Lead rückt in die Phasen Unterschrift und Anzahlung. In einem Club wird ein unterschriebener und bezahlter Lead in einem Schritt zum aktiven Mitglied, mit Willkommens-E-Mail.' },
          { q: 'Werden Änderungen nachvollziehbar festgehalten?', a: 'Ja. Das Audit-Log hält Änderungen fest: Sie sehen, wer was wann getan hat.' },
        ],
      },
      related: ['sales', 'billing', 'private-clubs'],
      cta: {
        heading: 'Vertrag unterschrieben, Anzahlung bezahlt: mit einem Link',
        text: 'Erstellen Sie einen Arbeitsbereich und senden Sie Ihren ersten Vertrag, oder fragen Sie eine Demo an.',
      },
    },

    swiss: {
      eyebrow: 'Schweizer CRM',
      h1: 'Ein Schweizer CRM, das',
      h1Accent: 'Deutsch, Französisch und Englisch spricht',
      lead: 'CadenceE wurde in der Schweiz für Schweizer Unternehmen entwickelt: Zahlungen per QR-Rechnung und TWINT, Buchhaltungsexport nach Bexio und Abacus, die App auf Deutsch, Französisch und Englisch und Datenschutzbegehren nach DSG.',
      heroCardTitle: 'Gemacht für die Schweiz',
      heroPoints: [
        'QR-Rechnung und TWINT',
        'Auch Karte und SEPA',
        'Buchhaltungsexport nach Bexio und Abacus',
        'Deutsch, Französisch und Englisch',
        'Begehren nach DSG und DSGVO',
      ],
      intro: {
        heading: 'Warum ein Schweizer Unternehmen ein Schweizer CRM braucht',
        paragraphs: [
          'Internationale CRMs sind für andere Märkte gebaut. Sie stellen Rechnungen, die Schweizer Kunden so nicht bezahlen, gehen von einer Sprache pro Team aus und überlassen den Buchhaltungsexport einem Berater. Für einen Club oder ein Unternehmen in der Schweiz wird daraus jeden Monat Handarbeit.',
          'CadenceE geht von Schweizer Gewohnheiten aus. Mitglieder und Kunden bezahlen mit QR-Rechnung oder TWINT, die Treuhand erhält einen Export für Bexio oder Abacus, der Empfang arbeitet auf Französisch, während die Buchhaltung Deutsch spricht, und Begehren nach dem Datenschutzgesetz gehören zum Produkt.',
        ],
      },
      sections: [
        {
          heading: 'Schweizer Zahlungen: QR-Rechnung und TWINT',
          paragraphs: [
            'Auszüge können eine QR-Rechnung tragen, den Einzahlungsschein, den jede Schweizer Banking-App liest. Mitglieder und Kunden bezahlen auch mit TWINT, per Karte oder per SEPA, und die Anzahlung bei Vertragsunterzeichnung funktioniert mit Karte und QR-Rechnung. Bankdateien werden mit den offenen Beträgen abgeglichen: Sie sehen, wer bezahlt hat, ohne Zeilen von Hand abzuhaken. Mehr zu [Abrechnung und Zahlungen](page:billing).',
          ],
        },
        {
          heading: 'Buchhaltungsexport nach Bexio und Abacus',
          paragraphs: [
            'Der Buchhaltungsexport geht nach Bexio oder Abacus, zwei in der Schweiz weit verbreiteten Buchhaltungslösungen. Das Hauptbuch des CRM wird nur storniert, nie überschrieben: Was exportiert wurde, bleibt wahr, und jede Korrektur bleibt sichtbar.',
          ],
        },
        {
          heading: 'Deutsch, Französisch und Englisch, für jede Person',
          paragraphs: [
            'Die ganze App gibt es auf Deutsch, Französisch und Englisch, und jede Person wählt ihre Sprache. Eine Kollegin in Lausanne und ein Kollege in Zürich arbeiten am selben Lead, jeder in seiner Sprache. Beträge folgen den regionalen Einstellungen des Arbeitsbereichs, etwa CHF 1’234.50, und vorbereitete E-Mails gibt es auf Französisch und Englisch.',
          ],
        },
        {
          heading: 'Datenschutz nach DSG und DSGVO',
          paragraphs: [
            'Auskunftsbegehren (Export der Daten einer Person) und Löschbegehren werden im CRM bearbeitet, nach DSGVO und Schweizer DSG. Jede Änderung steht im Audit-Log, Rollen und Berechtigungen begrenzen, wer was sieht, und die Anmeldung unterstützt Passkeys und Zwei-Faktor-Authentifizierung.',
          ],
          bullets: [
            'Auskunfts- und Löschbegehren',
            'Audit-Log jeder Änderung',
            'Rollen und Berechtigungen',
            'Passkeys und Zwei-Faktor-Authentifizierung',
          ],
        },
      ],
      features: {
        heading: 'Schweizer Funktionen',
        items: [
          { icon: 'payments', title: 'QR-Rechnung', text: 'Auszüge mit QR-Rechnung, lesbar für jede Schweizer Bank.' },
          { icon: 'swiss', title: 'TWINT', text: 'Mitglieder und Kunden bezahlen mit TWINT.' },
          { icon: 'bank', title: 'Bankabgleich', text: 'Bankdateien mit den offenen Beträgen abgeglichen.' },
          { icon: 'accounting', title: 'Export nach Bexio', text: 'Buchhaltungsexport für Bexio.' },
          { icon: 'ledger', title: 'Export nach Abacus', text: 'Export für Abacus, aus einem nie überschriebenen Hauptbuch.' },
          { icon: 'languages', title: 'Drei Sprachen', text: 'Deutsch, Französisch und Englisch, nach Wahl jeder Person.' },
          { icon: 'privacy', title: 'Begehren nach DSG und DSGVO', text: 'Auskunft und Löschung im CRM bearbeitet.' },
          { icon: 'audit', title: 'Audit-Log', text: 'Jede Änderung festgehalten.' },
          { icon: 'two-factor', title: 'Passkeys und Zwei-Faktor', text: 'Sichere Anmeldung für das ganze Team.' },
        ],
      },
      steps: {
        heading: 'CadenceE für die Schweiz einrichten',
        items: [
          { title: 'Arbeitsbereich erstellen', text: 'Regionale Einstellungen wählen; danach wählt jede Person ihre Sprache.' },
          { title: 'Zahlungsmittel hinzufügen', text: 'QR-Rechnung, TWINT, Karte und SEPA für Mitglieder und Kunden.' },
          { title: 'Buchhaltung anbinden', text: 'Export für Bexio oder für Abacus wählen.' },
          { title: 'In Ihren Sprachen arbeiten', text: 'Team einladen: Alle arbeiten auf Deutsch, Französisch oder Englisch.' },
        ],
      },
      faq: {
        heading: 'Schweizer CRM: häufige Fragen',
        items: [
          { q: 'Unterstützt CadenceE die QR-Rechnung?', a: 'Ja. Auszüge können eine QR-Rechnung tragen, und die Anzahlung bei Vertragsunterzeichnung lässt sich per QR-Rechnung bezahlen.' },
          { q: 'Können Mitglieder mit TWINT bezahlen?', a: 'Ja. TWINT gehört zu den Zahlungsmitteln, neben Karte, SEPA und QR-Rechnung.' },
          { q: 'In welche Buchhaltungssoftware wird exportiert?', a: 'Bexio und Abacus.' },
          { q: 'Gibt es die App auf Deutsch und Französisch?', a: 'Ja, auf Deutsch, Französisch und Englisch, nach Wahl jeder Person. Vorbereitete E-Mails gibt es auf Französisch und Englisch.' },
          { q: 'Wie schützt CadenceE die Daten?', a: 'Auskunfts- und Löschbegehren werden nach DSGVO und DSG bearbeitet, jede Änderung steht im Audit-Log, und der Zugriff wird über Rollen und Berechtigungen gesteuert.' },
          { q: 'Werden Beträge in Schweizer Franken angezeigt?', a: 'Ja. Beträge folgen den regionalen Einstellungen des Arbeitsbereichs, zum Beispiel CHF 1’234.50 in einem Schweizer Arbeitsbereich.' },
          { q: 'Wer hat CadenceE entwickelt?', a: 'CadenceE wurde in der Schweiz entwickelt, zusammen mit dem Pavillon 46, einem Privatclub in La Croix-sur-Lutry (VD), der es täglich nutzt.' },
        ],
      },
      related: ['billing', 'private-clubs', 'features'],
      cta: {
        heading: 'Ein CRM, das so funktioniert wie die Schweiz',
        text: 'Erstellen Sie einen Arbeitsbereich auf Deutsch, Französisch oder Englisch, oder fragen Sie eine Demo an.',
      },
    },

    billing: {
      eyebrow: 'Abrechnung und Zahlungen',
      h1: 'Club-Abrechnung und Zahlungen,',
      h1Accent: 'ohne Hektik am Monatsende',
      lead: 'Wiederkehrende Beiträge, Mitgliederkonten, Barrechnungen und monatliche PDF-Auszüge in einem CRM, bezahlt per Karte, TWINT, SEPA oder QR-Rechnung, mit Ihrer Bankdatei abgeglichen und nach Bexio oder Abacus exportiert.',
      heroCardTitle: 'Abrechnung in CadenceE',
      heroPoints: [
        'Wiederkehrende Beiträge, fristgerecht',
        'Konsumationen und Mitgliederkonten',
        'Monatliche PDF-Auszüge',
        'Karte, TWINT, SEPA und QR-Rechnung',
        'Bankabgleich, Export nach Bexio und Abacus',
      ],
      intro: {
        heading: 'Einen Club abzurechnen heisst mehr als Rechnungen zu schicken',
        paragraphs: [
          'Ein Mitglied schuldet den Jahresbeitrag, ein Dinner vom letzten Donnerstag, zwei Drinks an der Bar und ein Ticket für den Anlass im nächsten Monat. Eine gute Abrechnung zählt alles richtig zusammen, verschickt einen klaren Auszug und weiss, wann bezahlt wurde.',
          'CadenceE tut das mit den Datensätzen, die der Club ohnehin nutzt. Beiträge, Konsumationen, Anlässe und Privatanlässe werden aufs Mitgliederkonto gebucht. Zahlungen kommen per Karte, TWINT, SEPA oder QR-Rechnung zurück, und die Bankdatei wird abgeglichen: Jedes Konto gleicht sich von selbst aus.',
        ],
      },
      sections: [
        {
          heading: 'Wiederkehrende Beiträge und Positionen',
          paragraphs: [
            'Mitgliederbeiträge werden automatisch zum Fälligkeitstermin verrechnet. Weitere Positionen wie Anlässe, Privatanlässe und Extras kommen aufs selbe Konto, damit das Mitglied einen Auszug statt eines Stapels Rechnungen erhält. Die Beiträge hängen an der [Mitgliedschaft](page:membership) und folgen so Erneuerungen und Haushalten.',
          ],
        },
        {
          heading: 'Konsumationen, Kasse und Mitgliederkonten',
          paragraphs: [
            'Konsumationen an Bar und Restaurant gehen auf die Rechnung, mit einer Verbindung zur Lightspeed-Kasse, die die Verkäufe bringt. Mitglieder und Stammgäste können ein Konto führen, das monatlich beglichen wird. Die Gästeseite erklärt das [CRM für Gastronomie](page:hospitality).',
          ],
        },
        {
          heading: 'Auszüge und Zahlungen',
          paragraphs: [
            'Am Monatsende erhält jedes Konto einen PDF-Auszug. Mitglieder bezahlen per Karte, TWINT, SEPA oder mit der QR-Rechnung auf dem Auszug. Die Anzahlung bei Vertragsunterzeichnung funktioniert genauso, per Karte oder QR-Rechnung.',
          ],
        },
        {
          heading: 'Bankabgleich, ein verlässliches Hauptbuch und Buchhaltung',
          paragraphs: [
            'Laden Sie die Bankdatei hoch, und CadenceE gleicht die Zahlungseingänge mit den offenen Beträgen ab. Das Hauptbuch wird nie überschrieben, nur storniert: Ein Fehler wird mit einer sichtbaren Gegenbuchung korrigiert, nicht durch Umschreiben der Geschichte. Der Buchhaltungsexport geht nach Bexio oder Abacus, und die Treuhand startet mit sauberen Zahlen.',
          ],
          bullets: [
            'Abgleich der Bankdatei',
            'Hauptbuch mit Korrektur nur durch Storno',
            'Export nach Bexio',
            'Export nach Abacus',
          ],
        },
      ],
      features: {
        heading: 'Funktionen für Abrechnung und Zahlung',
        items: [
          { icon: 'dues', title: 'Wiederkehrende Beiträge', text: 'Fristgerecht verrechnet.' },
          { icon: 'tabs', title: 'Bar- und Restaurantrechnungen', text: 'Konsumationen pro Mitglied oder Gast.' },
          { icon: 'pos', title: 'Lightspeed-Kasse', text: 'Verkäufe aus der Kasse gelangen ins CRM.' },
          { icon: 'house-account', title: 'Mitgliederkonten', text: 'Einmal im Monat beglichen.' },
          { icon: 'statements', title: 'Monatliche PDF-Auszüge', text: 'Ein klarer Auszug pro Konto.' },
          { icon: 'payments', title: 'Karte und SEPA', text: 'Die Zahlungsmittel, die Ihre Mitglieder schon nutzen.' },
          { icon: 'swiss', title: 'TWINT und QR-Rechnung', text: 'Schweizer Zahlungen eingebaut.' },
          { icon: 'bank', title: 'Bankabgleich', text: 'Zahlungen den offenen Beträgen zugeordnet.' },
          { icon: 'ledger', title: 'Hauptbuch mit Storno', text: 'Korrekturen sind Gegenbuchungen, nie Änderungen.' },
          { icon: 'accounting', title: 'Bexio und Abacus', text: 'Der Buchhaltungsexport für Ihre Treuhand.' },
          { icon: 'gift-card', title: 'Geschenkkarten', text: 'Geschenkkarten verkaufen und einlösen.' },
          { icon: 'donation', title: 'Spenden', text: 'Spenden neben den übrigen Einnahmen erfassen.' },
        ],
      },
      steps: {
        heading: 'So läuft ein Abrechnungsmonat',
        items: [
          { title: 'Positionen sammeln sich', text: 'Beiträge, Konsumationen, Anlässe und Extras werden auf jedes Konto gebucht.' },
          { title: 'Auszüge gehen hinaus', text: 'Jedes Konto erhält einen PDF-Auszug mit QR-Rechnung.' },
          { title: 'Zahlungen gehen ein', text: 'Per Karte, TWINT, SEPA oder QR-Rechnung.' },
          { title: 'Die Bücher schliessen', text: 'Die Bankdatei wird abgeglichen, und der Export geht nach Bexio oder Abacus.' },
        ],
      },
      faq: {
        heading: 'Club-Abrechnung: häufige Fragen',
        items: [
          { q: 'Welche Zahlungsmittel werden unterstützt?', a: 'Karte, TWINT, SEPA und QR-Rechnung.' },
          { q: 'Lassen sich Barrechnungen den Mitgliedern verrechnen?', a: 'Ja. Konsumationen gehen aufs Mitgliederkonto und erscheinen auf dem Monatsauszug. Die Verbindung zur Lightspeed-Kasse bringt die Verkäufe.' },
          { q: 'Lassen sich Fehler korrigieren?', a: 'Ja, mit einer Stornobuchung. Das Hauptbuch wird nie überschrieben: Die Geschichte bleibt vollständig, und jedes Konto erklärt sich selbst.' },
          { q: 'Gibt es eine Anbindung an Buchhaltungssoftware?', a: 'CadenceE exportiert nach Bexio und Abacus. Mehr zum [Schweizer CRM](page:swiss).' },
          { q: 'Wie werden Bankzahlungen abgeglichen?', a: 'Laden Sie die Bankdatei hoch: CadenceE ordnet die Zahlungseingänge den offenen Beträgen zu.' },
          { q: 'Kann bei Vertragsunterzeichnung eine Anzahlung eingezogen werden?', a: 'Ja, per Karte oder QR-Rechnung. Mehr zu [Verträgen mit E-Signatur](page:e-signature).' },
        ],
      },
      related: ['swiss', 'membership', 'hospitality'],
      cta: {
        heading: 'Machen Sie das Monatsende einfacher',
        text: 'Erstellen Sie einen Arbeitsbereich und richten Sie Beiträge und Zahlungsmittel ein, oder fragen Sie eine Demo an.',
      },
    },

    email: {
      eyebrow: 'E-Mail-Automatisierung',
      h1: 'E-Mail-Automatisierung in Ihrem CRM,',
      h1Accent: 'mit Einwilligung von Anfang an',
      lead: 'Alle E-Mails über einen Postausgang, mit erneuten Versuchen und Bounce-Handling. Sequenzen und Automationen, die für Sie nachfassen, Newsletter, die zur Terminbuchung einladen, und Kampagnen mit Einwilligung und Abmeldung per Klick.',
      heroCardTitle: 'E-Mail in CadenceE',
      heroPoints: [
        'Ein Postausgang, mit erneuten Versuchen und Bounce-Handling',
        'Sequenzen und Automationen',
        'Newsletter mit Buchungsseite',
        'Kampagnen mit Einwilligung',
        'Abmeldung per Klick in jeder E-Mail',
      ],
      intro: {
        heading: 'E-Mails, die Ihre Kunden kennen',
        paragraphs: [
          'Wenn E-Mails in einem separaten Marketingwerkzeug leben, weiss dieses nie ganz, was das CRM weiss: dass der Lead gestern unterschrieben hat, dass das Mitglied keine Newsletter mehr will oder dass die letzte Nachricht unzustellbar war. Nachrichten erreichen die falschen Leute zur falschen Zeit.',
          'In CadenceE gehört E-Mail zum CRM. Jede Nachricht, ob persönliche Antwort, Schritt einer Sequenz oder Newsletter, geht über denselben Postausgang und wird beim Empfänger festgehalten. Einwilligungen und Abmeldungen gelten überall, automatisch.',
        ],
      },
      sections: [
        {
          heading: 'Ein Postausgang, sauber zugestellt',
          paragraphs: [
            'Alle E-Mails gehen über einen Postausgang. Lehnt ein Server vorübergehend ab, wird die Nachricht erneut versucht; ist eine Adresse unzustellbar, wird das festgehalten, damit Sie nicht mehr dorthin schreiben. Sie sehen, was verschickt wurde, an wen, und was daraus wurde.',
          ],
        },
        {
          heading: 'Sequenzen und Automationen',
          paragraphs: [
            'Sequenzen verschicken eine Reihe von Nachfassmeldungen nach Zeitplan. Automationen reagieren auf das, was im CRM geschieht, etwa einen neuen Lead von der Website oder einen unterschriebenen Vertrag: Die richtige Nachricht geht hinaus, ohne dass jemand daran denken muss.',
          ],
        },
        {
          heading: 'Newsletter, die den Kalender füllen',
          paragraphs: [
            'Newsletter gehen an Mitglieder, Gäste oder Leads. Ein Newsletter kann Leads einladen, über eine Buchungsseite einen Termin zu buchen: Aus Interesse wird ein Termin im Kalender, und der Lead rückt im [Vertriebsprozess](page:sales) vor.',
          ],
        },
        {
          heading: 'Kampagnen mit Einwilligung',
          paragraphs: [
            'Kampagnen erreichen nur Personen, die eingewilligt haben, und jede Nachricht trägt einen Abmeldelink, der mit einem Klick überall gilt. Vorbereitete E-Mails gibt es auf Französisch und Englisch, und der [KI-Texter](page:ai) hilft bei neuen.',
          ],
          bullets: [
            'Einwilligung pro Person festgehalten',
            'Abmeldung per Klick',
            'Vorbereitete E-Mails auf Französisch und Englisch',
            'KI-Entwürfe für neue Nachrichten',
          ],
        },
      ],
      features: {
        heading: 'E-Mail-Funktionen',
        items: [
          { icon: 'outbox', title: 'Ein Postausgang', text: 'Jede E-Mail geht von einem Ort aus und steht im Datensatz.' },
          { icon: 'audit', title: 'Erneute Versuche und Bounces', text: 'Vorübergehende Ablehnungen wiederholt, Bounces festgehalten.' },
          { icon: 'sequences', title: 'Sequenzen', text: 'Nachfassen nach Zeitplan.' },
          { icon: 'automations', title: 'Automationen', text: 'Nachrichten, ausgelöst durch Ereignisse im CRM.' },
          { icon: 'newsletter', title: 'Newsletter', text: 'An Mitglieder, Gäste und Leads.' },
          { icon: 'booking', title: 'Buchungsseite', text: 'Leads buchen aus einem Newsletter einen Termin.' },
          { icon: 'campaigns', title: 'Kampagnen', text: 'Versände an die Gruppen, die Sie wählen.' },
          { icon: 'consent', title: 'Einwilligung und Abmeldung', text: 'Einwilligung respektiert, Abmeldung per Klick.' },
          { icon: 'languages', title: 'E-Mails auf Französisch und Englisch', text: 'Vorbereitete E-Mails in beiden Sprachen.' },
          { icon: 'writer', title: 'KI-Texter', text: 'Entwürfe in Ihrem Ton, zum Prüfen.' },
        ],
      },
      steps: {
        heading: 'So funktioniert E-Mail in CadenceE',
        items: [
          { title: 'Versand einrichten', text: 'Absenderadresse des Arbeitsbereichs hinzufügen und Vorlagen wählen.' },
          { title: 'Sequenzen schreiben', text: 'Das Nachfassen einmal schreiben und festlegen, wann es hinausgeht.' },
          { title: 'Automatisieren', text: 'Neue Leads, Unterschriften oder Erneuerungen lösen die richtige Nachricht aus.' },
          { title: 'Mit Einwilligung senden', text: 'Newsletter und Kampagnen gehen an Personen, die eingewilligt haben, und jeder Versand steht in ihrem Datensatz.' },
        ],
      },
      faq: {
        heading: 'E-Mail-Automatisierung: häufige Fragen',
        items: [
          { q: 'Was passiert, wenn eine E-Mail unzustellbar ist?', a: 'Der Bounce wird im Datensatz festgehalten, damit Sie nicht mehr an eine Adresse schreiben, die nicht funktioniert. Vorübergehende Ablehnungen werden automatisch wiederholt.' },
          { q: 'Was ist der Unterschied zwischen Sequenz und Automation?', a: 'Eine Sequenz ist eine Reihe von Nachfassmeldungen nach Zeitplan. Eine Automation reagiert auf ein Ereignis im CRM, etwa einen neuen Lead oder einen unterschriebenen Vertrag, und verschickt in diesem Moment die richtige Nachricht.' },
          { q: 'Wie wird die Einwilligung gehandhabt?', a: 'Kampagnen und Newsletter gehen nur an Personen, die eingewilligt haben, und jede E-Mail trägt einen Abmeldelink, der sofort gilt.' },
          { q: 'Können Leads aus einem Newsletter einen Termin buchen?', a: 'Ja. Ein Newsletter kann sie einladen, über eine Buchungsseite einen Termin zu buchen.' },
          { q: 'In welchen Sprachen gibt es vorbereitete E-Mails?', a: 'Auf Französisch und Englisch. Die App selbst gibt es auf Deutsch, Französisch und Englisch.' },
          { q: 'Hilft KI beim Schreiben?', a: 'Ja. Der KI-Texter entwirft E-Mails in Ihrem Ton, die eine Person vor dem Versand prüft. Mehr zum [KI-Assistenten](page:ai).' },
        ],
      },
      related: ['sales', 'ai', 'membership'],
      cta: {
        heading: 'Die richtige E-Mail an die richtige Person',
        text: 'Erstellen Sie einen Arbeitsbereich und fügen Sie Ihre Absenderadresse hinzu, oder fragen Sie eine Demo an.',
      },
    },

    ai: {
      eyebrow: 'KI-Assistent für CRM',
      h1: 'Ein KI-Assistent für Ihr CRM,',
      h1Accent: 'der fragt, bevor er handelt',
      lead: 'Der Assistent von CadenceE basiert auf Claude. Er liest Ihren Arbeitsbereich, schlägt Änderungen vor, die eine Person bestätigt, entwirft Verkaufs-E-Mails in Ihrem Ton und bereitet jeden Morgen ein Briefing vor, was heute Aufmerksamkeit braucht.',
      heroCardTitle: 'KI in CadenceE',
      heroPoints: [
        'Basiert auf Claude von Anthropic',
        'Schlägt vor; eine Person bestätigt',
        'KI-Texter für E-Mails und Notizen',
        'Jeden Morgen ein Briefing',
        'Teil des CRM, kein separates Werkzeug',
      ],
      intro: {
        heading: 'KI, die hilft, ohne die Kontrolle zu übernehmen',
        paragraphs: [
          'KI im CRM kann Stunden sparen oder Chaos anrichten. Ein Assistent, der Datensätze eigenmächtig ändert, aktualisiert vielleicht den falschen Lead, überschreibt eine Notiz oder verschickt etwas, das niemand freigegeben hat. Für einen Club, der Personendaten seiner Mitglieder hält, ist das nicht akzeptabel.',
          'Der Assistent von CadenceE geht einen anderen Weg: Er liest und entwirft, die Entscheidungen bleiben bei den Menschen. Jede Änderung, die er vorschlägt, erscheint als Vorschlag, den eine Person bestätigt, bevor etwas gespeichert wird.',
        ],
      },
      sections: [
        {
          heading: 'Ein Assistent, der vorschlägt statt vorschreibt',
          paragraphs: [
            'Bitten Sie den Assistenten, einen Lead aufzuräumen, die nächsten Schritte nach einem Termin vorzubereiten oder mehrere Datensätze auf einmal nachzuführen. Er ermittelt, was sich ändern sollte, und legt die Änderungen als Vorschläge vor. Nichts wird geschrieben, bevor eine Person bestätigt, und bestätigte Änderungen erscheinen im Audit-Log wie alle anderen.',
          ],
        },
        {
          heading: 'Der KI-Texter',
          paragraphs: [
            'Der Texter entwirft Verkaufs-E-Mails, Nachfassmeldungen, Deal-Nachrichten und Notizen im Ton Ihrer Marke: professionell, freundlich oder überzeugend. Sie wählen Ziel und Länge, passen den Entwurf mit einem Klick an und senden ihn über den Postausgang des CRM. Mehr zur [E-Mail-Automatisierung](page:email).',
          ],
        },
        {
          heading: 'Jeden Morgen ein Briefing',
          paragraphs: [
            'Jeden Morgen erhält jede Person ein Briefing über ihren Tag im Arbeitsbereich: die Leads, Aufgaben und Termine, die Aufmerksamkeit brauchen. Es entsteht aus den Datensätzen, die diese Person sehen darf, mit einem Link zu jedem.',
          ],
        },
        {
          heading: 'Auf Basis von Claude',
          paragraphs: [
            'Der Assistent läuft mit Claude, dem KI-Modell von Anthropic, und arbeitet innerhalb von CadenceE mit den Datensätzen Ihres Arbeitsbereichs. Er steht neben den übrigen [Funktionen des CRM](page:features), statt in einem separaten Werkzeug mit einer Kopie Ihrer Daten.',
          ],
        },
      ],
      features: {
        heading: 'KI-Funktionen',
        items: [
          { icon: 'ai', title: 'KI-Assistent', text: 'In eigenen Worten fragen, Vorschläge zum Bestätigen erhalten.' },
          { icon: 'audit', title: 'Änderungen, die Sie bestätigen', text: 'Nichts wird ohne Zustimmung gespeichert, alles wird protokolliert.' },
          { icon: 'writer', title: 'KI-Texter', text: 'E-Mails, Nachfassen und Notizen in Ihrem Ton.' },
          { icon: 'brief', title: 'Morgenbriefing', text: 'Die Prioritäten des Tages, jeden Morgen vorbereitet.' },
          { icon: 'leads', title: 'Vorgeschlagene nächste Schritte', text: 'Was als Nächstes bei einem Lead oder Deal zu tun ist.' },
          { icon: 'roles', title: 'Ihre Regeln', text: 'Das Briefing respektiert, was jede Person sehen darf.' },
        ],
      },
      steps: {
        heading: 'So arbeiten Sie mit dem Assistenten',
        items: [
          { title: 'Fragen', text: 'Beschreiben Sie in eigenen Worten, was Sie brauchen.' },
          { title: 'Prüfen', text: 'Der Assistent zeigt die Änderungen, die er vorschlägt.' },
          { title: 'Bestätigen', text: 'Übernehmen Sie, was stimmt; sonst wird nichts gespeichert.' },
          { title: 'Weitermachen', text: 'Der Texter entwirft die E-Mail, und morgen früh wartet das Briefing.' },
        ],
      },
      faq: {
        heading: 'KI-Assistent: häufige Fragen',
        items: [
          { q: 'Welches KI-Modell nutzt CadenceE?', a: 'Der Assistent basiert auf Claude von Anthropic.' },
          { q: 'Kann die KI meine Daten ohne Rückfrage ändern?', a: 'Nein. Der Assistent macht nur Vorschläge. Eine Person bestätigt jede Änderung vor dem Speichern, und bestätigte Änderungen stehen im Audit-Log.' },
          { q: 'Was macht der KI-Texter?', a: 'Er entwirft Verkaufs-E-Mails, Nachfassmeldungen, Deal-Nachrichten und Notizen in Ton und Länge Ihrer Wahl, die eine Person prüft und versendet.' },
          { q: 'Was ist das Morgenbriefing?', a: 'Eine tägliche Übersicht über den Tag jeder Person im Arbeitsbereich, erstellt aus den Datensätzen, die sie sehen darf.' },
          { q: 'Ist die KI ein separates Werkzeug?', a: 'Nein. Assistent, Texter und Briefing gehören zu CadenceE und arbeiten mit den Datensätzen Ihres Arbeitsbereichs.' },
          { q: 'Müssen wir die KI nutzen?', a: 'Nein. Das CRM funktioniert auch ohne sie; der Assistent ist da, wenn er Zeit spart. Alle [Funktionen](page:features) im Überblick.' },
        ],
      },
      related: ['sales', 'email', 'features'],
      cta: {
        heading: 'Lassen Sie die KI lesen und entwerfen',
        text: 'Erstellen Sie einen Arbeitsbereich und testen Sie den Assistenten mit Ihren eigenen Leads, oder fragen Sie eine Demo an.',
      },
    },

    pricing: dePricing,
    'free-crm': deFreeCrm,
  },
  guides: deGuides,
};

export default de;
