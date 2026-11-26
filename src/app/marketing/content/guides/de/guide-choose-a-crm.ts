import type { GuideContent } from '../types';

// CRM auswählen für Club, Gastronomie oder KMU in der Schweiz: von den Abläufen ausgehen, eine
// Checkliste mit Punkten (Datenmodell, E-Mail, Schweizer Zahlungen, DSG, Abhängigkeit, KI, Kosten),
// Test, Fragen an Anbieter, Warnsignale und Zeitplan. Neutral; CadenceE ist eine Option unter vielen.
const guide: GuideContent = {
  eyebrow: 'Software wählen',
  h1: 'CRM auswählen: der Leitfaden für Clubs und KMU in der Schweiz',
  lead: 'Das richtige CRM passt zu Ihren Abläufen, Ihren Daten, zu Schweizer Zahlungen und Sprachen, zum Datenschutz und zu Ihrem Budget über mehrere Jahre. Hier finden Sie eine Methode, eine Checkliste mit Punkten, Fragen an Anbieter, Warnsignale und einen realistischen Zeitplan, für Clubs, Gastronomiebetriebe und kleine Verkaufsteams.',
  readingMinutes: 9,
  keyTakeaways: [
    'Gehen Sie von Ihren Abläufen aus, von der Anfrage bis zur Zahlung und Erneuerung, nicht von einer Funktionsliste.',
    'Halten Sie die Muss-Kriterien kurz und bewerten Sie jedes Produkt mit derselben gewichteten Checkliste.',
    'Prüfen Sie in der Schweiz QR-Rechnung und TWINT, Export nach Bexio oder Abacus, Deutsch und Französisch sowie den Datenschutz nach DSG.',
    'Testen Sie mit eigenen Szenarien und einer Stichprobe Ihrer Daten, inklusive Export, bevor Sie unterschreiben.',
    'Vergleichen Sie die Gesamtkosten über drei Jahre und rechnen Sie mit sechs bis zehn Wochen für den Entscheid.',
  ],
  sections: [
    {
      id: 'von-den-ablaeufen-ausgehen',
      heading: 'Von den Abläufen ausgehen, nicht von Funktionen',
      paragraphs: [
        'CRM-Projekte enttäuschen, wenn zuerst die Software gewählt wird und die Anforderungen erst danach auftauchen. Jede Demo überzeugt, weil sie zeigt, wie sich der Anbieter Ihre Arbeit vorstellt. Schreiben Sie deshalb auf, wie Sie tatsächlich arbeiten, bevor Sie ein Produkt ansehen.',
      ],
      steps: [
        { title: 'Den Kundenweg aufzeichnen', text: 'Club: Anfrage, Besuch, Aufnahmeantrag, Vertrag, Anzahlung, Mitgliedschaft, Erneuerung. Betrieb: Anfrage, Offerte, Vertrag, Anlass, Rechnung. Verkaufsteam: Lead, Termin, Offerte, Unterschrift, Zahlung.' },
        { title: 'Personen und Rollen auflisten', text: 'Wer erfasst, wer fasst nach, wer gibt frei, wer rechnet ab, wer liest nur.' },
        { title: 'Schwachstellen sammeln', text: 'Verlorene Anfragen, Dubletten, verspätete Rechnungen, Berichte, die jeden Monat von Hand entstehen.' },
        { title: 'Fünf bis zehn Szenarien schreiben', text: 'Kurze Geschichten wie «ein Mitglied empfiehlt einen Freund, der den Club besucht, sich bewirbt und eine Anzahlung leistet». Sie werden Ihr Testdrehbuch.' },
      ],
      tip: 'Beziehen Sie die Leute ein, die täglich mit dem CRM arbeiten, nicht nur die Geschäftsleitung: Wenn Empfang oder Verkauf es nicht nutzen, scheitert auch die beste Wahl auf dem Papier.',
    },
    {
      id: 'muss-und-kann-kriterien',
      heading: 'Muss-Kriterien und Kann-Kriterien',
      paragraphs: [
        'Teilen Sie jede Anforderung einer von zwei Listen zu. Ein Muss-Kriterium ist etwas, ohne das Sie nicht arbeiten können oder eine gesetzliche oder vertragliche Pflicht verletzen würden; ein Kann-Kriterium spart Zeit, ist aber verzichtbar. Beschränken Sie sich auf etwa zehn Muss-Kriterien: Mit dreissig fällt jedes Produkt durch. Typische Beispiele:',
      ],
      bullets: [
        'Privatclub: Mitglieder und Haushalte, Aufnahmeanträge, Erneuerungen, wiederkehrende Beiträge und Auszüge.',
        'Gastronomie und Eventlocation: Anlassanfragen, Verträge und Anzahlungen, Reservationen, Konsumationen und Anbindung an die Kasse.',
        'Kleines Verkaufsteam: eine klare Pipeline, Aufgaben und Erinnerungen, E-Mails aus dem CRM, einfache Berichte.',
        'Alle in der Schweiz: die Sprachen des Teams, Schweizer Zahlungsmittel, ein Buchhaltungsexport und solider Datenschutz.',
      ],
    },
    {
      id: 'crm-auswahl-checkliste',
      heading: 'Die Checkliste für die CRM-Auswahl, mit Punkten',
      paragraphs: [
        'Gewichten Sie jedes Kriterium von 1 (Kann) bis 3 (Muss), bewerten Sie jedes Produkt von 0 (fehlt) bis 3 (erfüllt den Bedarf voll), nachdem Sie es in Aktion gesehen haben, und multiplizieren Sie. Ein Muss-Kriterium mit 0 Punkten scheidet das Produkt aus, egal wie hoch die Summe ist.',
      ],
      subsections: [
        {
          heading: 'Datenmodell: Leads, Mitglieder, Anlässe',
          paragraphs: [
            'Vertriebs-CRM denken in Leads, Firmen und Deals. Ein Club braucht zusätzlich Mitglieder, Haushalte, Firmenmitgliedschaften, Aufnahmeanträge und Erneuerungen, ein Betrieb Anlässe, Reservationen und Wartelisten. Mitglieder, die in «Deals» gezwängt werden, funktionieren in der Demo und stören danach jeden Tag.',
          ],
        },
        {
          heading: 'E-Mail und Nachfassen',
          paragraphs: [
            'Persönliche E-Mails, die beim Kontakt festgehalten werden, Nachfass-Sequenzen, Newsletter, Bounce-Handling sowie erfasste Einwilligungen und ein Abmeldelink in Werbe-E-Mails, wie es das Schweizer Recht verlangt. Unser Ratgeber zu [Nachfass-E-Mails](page:guide-sales-follow-up) zeigt, was ein guter Ablauf braucht.',
          ],
        },
        {
          heading: 'Zahlungen und Schweizer Besonderheiten',
          paragraphs: [
            'Die QR-Rechnung (sie hat 2022 die orangen und roten Einzahlungsscheine abgelöst), TWINT, Karte und SEPA, Beträge in Franken, Abgleich der Bankdatei und ein Export, den Ihr Treuhänder in Bexio oder Abacus übernehmen kann. Prüfen Sie die Sprachen der Oberfläche pro Person, Italienisch inklusive, falls nötig. Mehr dazu im Ratgeber zur [QR-Rechnung für Vereine](page:guide-qr-bill).',
          ],
        },
        {
          heading: 'Datenschutz, Hosting und Zugriffe',
          paragraphs: [
            'Mit dem revidierten DSG, seit September 2023 in Kraft, bleiben Sie für die Daten in Ihrem CRM verantwortlich. Hosting in der Schweiz ist nicht vorgeschrieben, aber Daten dürfen grundsätzlich nur in Länder mit angemessenem Datenschutz wie die Staaten der EU und des EWR gehen oder mit Garantien wie Standardvertragsklauseln. Fragen Sie, wo die Daten liegen, wer die Unterauftragsbearbeiter sind und ob es einen Auftragsbearbeitungsvertrag gibt, und prüfen Sie Rollen, Audit-Log und Zwei-Faktor-Anmeldung. Siehe unsere [nDSG-Checkliste](page:guide-fadp-checklist).',
          ],
        },
        {
          heading: 'Import, Export und Abhängigkeit',
          paragraphs: [
            'Der Einstieg muss sicher sein: Vorschau oder Probelauf, Dublettenerkennung und eine Rückgängig-Funktion. Der Ausstieg muss einfach sein: alle Daten in einem offenen Format wie CSV, jederzeit und ohne Zusatzkosten. Ein CRM, das man nicht verlassen kann, kann die Preise erhöhen.',
          ],
        },
        {
          heading: 'Integrationen',
          paragraphs: [
            'Listen Sie auf, was angebunden sein muss: Website-Formulare, E-Mail-Domain und Kalender, Kasse oder Reservationssystem, Buchhaltung, elektronische Signatur. Ziehen Sie Fertiges dem Versprechen einer API vor; braucht eine Integration einen Berater, gehört er in die Kosten.',
          ],
        },
        {
          heading: 'Künstliche Intelligenz',
          paragraphs: [
            'Fragen Sie, was die KI tut (Entwürfe, Zusammenfassungen, Vorschläge für nächste Schritte), ob sie Datensätze ändern oder E-Mails senden kann, ohne dass eine Person bestätigt, welcher Anbieter Ihre Daten verarbeitet und ob sie sich abschalten lässt.',
          ],
        },
        {
          heading: 'Gesamtkosten und Preismodelle',
          paragraphs: [
            'CRM werden pro Benutzer, pro Kontakt, nach Funktionsstufen oder pro Arbeitsbereich verrechnet, oft mit Zusätzen für E-Mail-Volumen, KI oder Module. Rechnen Sie Einrichtung, Migration, Schulung, Integrationen, interne Zeit und Preiserhöhungen nach dem ersten Jahr dazu, und vergleichen Sie die Summe über drei Jahre für die Grösse, die Sie erreichen wollen.',
          ],
        },
        {
          heading: 'Support und Anbieter',
          paragraphs: [
            'Wer hilft bei einer Störung, in welcher Sprache, wie schnell, und steht das im Vertrag? Fragen Sie, wie oft sich das Produkt ändert und was mit Ihren Daten geschieht, wenn der Anbieter verkauft wird oder aufgibt.',
          ],
        },
      ],
    },
    {
      id: 'crm-testen',
      heading: 'Ein CRM mit echten Szenarien testen',
      paragraphs: [
        'Die Demo eines Anbieters zeigt das Produkt von seiner besten Seite; Ihr Test soll zeigen, wie es Ihre Arbeit erledigt. Wählen Sie zwei oder drei Produkte aus und spielen Sie mit allen dasselbe Drehbuch durch.',
      ],
      steps: [
        { title: 'Testdrehbuch vorbereiten', text: 'Machen Sie aus den Szenarien Schritte mit erwartetem Ergebnis: «50 Mitglieder importieren», «Vertrag senden und Anzahlung erfassen», «alle Erneuerungen im nächsten Monat finden».' },
        { title: 'Eigene Daten als Stichprobe', text: 'Einige Dutzend Datensätze im echten Format, mit möglichst wenig Personendaten.' },
        { title: 'Die Nutzer ans Steuer', text: 'Wer das CRM täglich nutzen wird, spielt die Szenarien selbst durch.' },
        { title: 'Den Ausstieg testen', text: 'Exportieren Sie, was Sie importiert haben, und prüfen Sie, ob nichts fehlt.' },
        { title: 'Am selben Tag bewerten', text: 'Füllen Sie die Checkliste aus, solange die Eindrücke frisch sind, und notieren Sie jeden Umweg.' },
      ],
      tip: 'Begrenzen Sie den Test auf zwei bis drei Wochen. Steht CadenceE auf Ihrer Liste, können Sie [die Demo ausprobieren](/login?demo=1), einen Arbeitsbereich mit Beispieldaten ohne Konto, bevor Sie eigene Daten importieren.',
    },
    {
      id: 'fragen-an-anbieter',
      heading: 'Fragen an CRM-Anbieter',
      paragraphs: ['Stellen Sie sie schriftlich, damit Sie die Antworten nebeneinanderlegen können:'],
      bullets: [
        'Wo werden unsere Daten gespeichert, und welche Unterauftragsbearbeiter haben Zugriff?',
        'Gibt es einen Auftragsbearbeitungsvertrag nach DSG und, falls nötig, nach DSGVO?',
        'Wie exportieren wir alle unsere Daten, in welchem Format und zu welchen Kosten?',
        'Was ist im Preis enthalten, was kostet extra, und wie lang ist die Kündigungsfrist?',
        'Welche unserer Muss-Kriterien funktionieren heute, welche sind nur geplant?',
        'In welchen Sprachen gibt es Oberfläche, E-Mails und Support?',
        'Kann die KI Daten ändern oder Nachrichten senden, ohne dass eine Person bestätigt?',
        'Wer hilft bei der Migration, und lässt sich ein missglückter Import rückgängig machen?',
      ],
    },
    {
      id: 'warnsignale',
      heading: 'Warnsignale bei der CRM-Auswahl',
      paragraphs: ['Nicht jedes ist ein Ausschlussgrund, aber jedes verdient eine schriftliche Antwort, bevor Sie unterschreiben:'],
      bullets: [
        'Muss-Kriterien, die «auf der Roadmap» stehen oder sich «mit der API bauen lassen».',
        'Keine Möglichkeit, alle Daten selbst zu exportieren.',
        'Ein Rabatt, der nur gilt, wenn Sie diese Woche unterschreiben.',
        'Vage Antworten zum Speicherort oder zu den Auftragsbearbeitern.',
        'Eine lange Mindestlaufzeit mit automatischer Verlängerung.',
        'KI, die Datensätze ändert oder E-Mails versendet, ohne dass jemand bestätigt.',
      ],
    },
    {
      id: 'zeitplan',
      heading: 'Ein realistischer Zeitplan für den Entscheid',
      paragraphs: [
        'Für einen Club, einen Betrieb oder ein kleines Verkaufsteam sind sechs bis zehn Wochen vom ersten Schritt bis zur Unterschrift realistisch. Bestimmen Sie eine Person, die für den Entscheid verantwortlich ist.',
      ],
      steps: [
        { title: 'Woche 1 bis 2: Abläufe und Anforderungen', text: 'Kundenweg, Szenarien, Muss-Kriterien und Budgetrahmen.' },
        { title: 'Woche 3: von der Longlist zur Shortlist', text: 'Fünf oder sechs Produkte auf dem Papier mit den Muss-Kriterien abgleichen; zwei oder drei behalten.' },
        { title: 'Woche 4 bis 6: Demos und Tests', text: 'Drehbuch durchspielen, schriftliche Antworten einholen, bewerten.' },
        { title: 'Woche 7: Referenzen und Vertrag', text: 'Mit Kunden sprechen, Vertrag und Auftragsbearbeitungsvertrag lesen, Ausstiegsbedingungen prüfen.' },
        { title: 'Woche 8: entscheiden und planen', text: 'Wählen, Starttermin festlegen, Migration und Schulung planen.' },
      ],
      tip: 'Rechnen Sie danach mit zwei bis sechs Wochen für die Migration, je nach Zustand Ihrer Daten. Unser Ratgeber [von Excel zum CRM](page:guide-excel-to-crm) zeigt, wie Sie sie vorbereiten.',
    },
    {
      id: 'wo-cadencee-passt',
      heading: 'Wo CadenceE passt',
      paragraphs: [
        'CadenceE ist eine Option für Privatclubs, Gastronomie und Verkaufsteams in der Schweiz. In der Schweiz zusammen mit einem Privatclub entwickelt, deckt es den Weg von der Anfrage bis zur Zahlung in einem Datensatz ab: einen [Lead-Tracker mit fünf Phasen](page:sales) bis zur Anzahlung, online unterschriebene Verträge, Mitglieder und Haushalte, Anlässe und Konsumationen sowie [Schweizer Zahlungen und Exporte](page:swiss) mit QR-Rechnung, TWINT, Bexio und Abacus.',
        'Die App gibt es auf Deutsch, Französisch und Englisch, von jeder Person selbst gewählt; Auskunfts- und Löschbegehren werden nach DSG und DSGVO bearbeitet; Listen lassen sich als CSV exportieren; und der KI-Assistent schlägt Änderungen vor, die eine Person bestätigt. Ein Hotelverwaltungssystem ist es nicht. Prüfen Sie es mit derselben Checkliste wie alle anderen, am besten anhand der [Funktionen](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'CRM auswählen: häufige Fragen',
    items: [
      { q: 'Wie wähle ich das richtige CRM für ein KMU aus?', a: 'Zeichnen Sie Ihren Kundenweg vom Erstkontakt bis zur Zahlung auf, legen Sie wenige Muss-Kriterien fest, bewerten Sie zwei oder drei Produkte mit derselben Checkliste und testen Sie sie mit eigenen Szenarien und Daten.' },
      { q: 'Welche Kriterien zählen bei der CRM-Auswahl am meisten?', a: 'Passung zu Ihren Daten, Bedienbarkeit, E-Mail und Nachfassen, Zahlungen und Buchhaltungsexport, Datenschutz, Export und Abhängigkeit, Integrationen, Support und die Gesamtkosten über mehrere Jahre.' },
      { q: 'Muss ein Schweizer Unternehmen sein CRM in der Schweiz hosten?', a: 'Nicht von Gesetzes wegen. Das DSG erlaubt die Speicherung in Ländern mit angemessenem Datenschutz wie den Staaten der EU und des EWR oder mit Garantien wie Standardvertragsklauseln. Manche Verträge oder Branchen verlangen Schweizer Hosting: Prüfen Sie Ihre.' },
      { q: 'Wie lange dauert die Auswahl eines CRM?', a: 'Sechs bis zehn Wochen für eine kleine Organisation, dazu zwei bis sechs Wochen für Migration und Schulung.' },
      { q: 'Wie vergleicht man CRM-Preise?', a: 'Vergleichen Sie die Summe über drei Jahre für die erwartete Zahl an Benutzern und Kontakten, mit Einrichtung, Migration, Schulung, Zusätzen und Preiserhöhungen, statt nur den Einstiegspreis pro Benutzer.' },
      { q: 'Braucht ein Club ein CRM oder eine Mitgliederverwaltung?', a: 'Ein Club, der Mitgliedschaften aktiv verkauft, braucht beides: eine Verkaufspipeline und die Mitgliederverwaltung. Ein einziges System macht aus einem Interessenten ein Mitglied, ohne doppelte Erfassung. Siehe [CRM für Privatclubs](page:private-clubs).' },
    ],
  },
  cta: {
    heading: 'Prüfen Sie CadenceE mit Ihrer Checkliste',
    text: 'Spielen Sie Ihre eigenen Szenarien in CadenceE durch, von der ersten Anfrage über den unterschriebenen Vertrag und die bezahlte Anzahlung bis zum Monatsauszug eines Mitglieds, und bewerten Sie es wie jedes andere CRM auf Ihrer Shortlist.',
  },
};

export default guide;
