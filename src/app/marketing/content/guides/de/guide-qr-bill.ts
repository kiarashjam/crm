import type { GuideContent } from '../types';

// Technische Angaben geprüft bei SIX (Swiss Implementation Guidelines for the QR-bill, v2.3,
// und six-group.com/qr-bill), bei PostFinance (Factsheet 460.445) und im KMU-Portal des Bundes
// (kmu.admin.ch), im November 2026.
const guide: GuideContent = {
  eyebrow: 'Vereinsfinanzen',
  h1: 'Die QR-Rechnung im Verein: erstellen, versenden, abgleichen',
  lead: 'Wie die QR-Rechnung funktioniert, welches Konto und welche Referenz Sie wählen, was draufstehen muss, und wie ein Verein oder Club seine Mitgliederbeiträge als PDF verschickt und sieht, wer bezahlt hat, ohne Bankbuchungen von Hand abzuhaken.',
  readingMinutes: 9,
  keyTakeaways: [
    'Die QR-Rechnung ist der einzige Schweizer Einzahlungsschein: Die orangen und roten Scheine werden seit dem 30. September 2022 nicht mehr verarbeitet.',
    'Zur QR-IBAN gehört eine 27-stellige QR-Referenz; zur normalen IBAN eine Creditor Reference (RF…) oder gar keine Referenz.',
    'Erst eine strukturierte Referenz ordnet jede Zahlung automatisch dem richtigen Mitglied zu.',
    'Seit dem 22. November 2025 müssen Adressen strukturiert sein: Strasse, Hausnummer, PLZ und Ort in getrennten Feldern.',
    'Als PDF verschickt, wird die Rechnung in der Banking-App gescannt; die camt-Datei der Bank zeigt danach, wer bezahlt hat.',
  ],
  sections: [
    {
      id: 'was-ist-die-qr-rechnung',
      heading: 'Was die QR-Rechnung ist und was sie ersetzt hat',
      paragraphs: [
        'Die QR-Rechnung ist der Standard-Einzahlungsschein für Rechnungen in der Schweiz und in Liechtenstein. Sie wurde am 30. Juni 2020 eingeführt und hat den roten Einzahlungsschein (ES) und den orangen Einzahlungsschein mit Referenznummer (ESR) abgelöst. Nach dem 30. September 2022 haben Banken und PostFinance die alten Scheine nicht mehr verarbeitet, weder online noch am Schalter, und Daueraufträge, die darauf beruhten, liefen nicht mehr.',
        'Für die Mitglieder ist der Swiss QR Code der Unterschied: ein Quadrat mit kleinem Schweizerkreuz, das alle Zahlungsdaten enthält. Die Banking-App liest ihn in einer Sekunde, niemand tippt mehr lange Referenznummern ab. Die Regeln legt SIX in ihren Implementation Guidelines fest; die jüngste Revision, in Kraft seit dem 22. November 2025, hat strukturierte Adressen zur Pflicht gemacht.',
      ],
    },
    {
      id: 'zahlteil-und-empfangsschein',
      heading: 'Zahlteil und Empfangsschein',
      paragraphs: [
        'Eine QR-Rechnung hat unten zwei Teile. Der Zahlteil rechts ist im Format A6 quer (148 × 105 mm) und trägt den QR-Code. Der Empfangsschein links misst 62 × 105 mm; ihn behält, wer am Postschalter bezahlt. Auf Papier sind beide perforiert, der Zahlteil darf keine Werbung tragen, und seine Rückseite bleibt leer. Die Titel können deutsch, französisch, italienisch oder englisch sein.',
      ],
      bullets: [
        'Konto / Zahlbar an: die IBAN oder QR-IBAN mit Name und Adresse des Vereins',
        'Referenz: die QR-Referenz oder Creditor Reference, falls vorhanden',
        'Zusätzliche Informationen: eine kurze Mitteilung wie «Mitgliederbeitrag 2027»',
        'Zahlbar durch: Name und Adresse des Mitglieds oder ein leeres Feld',
        'Währung und Betrag: CHF oder EUR, und der Betrag oder ein leeres Feld',
      ],
    },
    {
      id: 'qr-iban-oder-iban',
      heading: 'QR-IBAN mit QR-Referenz oder IBAN mit Creditor Reference',
      paragraphs: [
        'Das ist der einzige echte Entscheid, den ein Verein treffen muss. Konto und Referenzart gehören zusammen, und wer sie vermischt, liefert den häufigsten Grund für eine zurückgewiesene QR-Rechnung.',
      ],
      subsections: [
        {
          heading: 'QR-IBAN und QR-Referenz (QRR)',
          paragraphs: [
            'Die QR-IBAN ist eine eigene Nummer, die Ihre Bank für ein bestehendes Konto vergibt. Ihre Bankkennung (die QR-IID) liegt zwischen 30000 und 31999, deshalb ist ihr fünftes Zeichen immer eine 3. Sie dient nur dem Zahlungseingang und nur zusammen mit einer QR-Referenz: 27 Ziffern, die letzte davon eine Prüfziffer. Meist stecken darin Mitglieder- und Rechnungsnummer.',
          ],
        },
        {
          heading: 'IBAN und Creditor Reference (SCOR)',
          paragraphs: [
            'Mit der normalen IBAN des Vereins verwenden Sie die internationale Creditor Reference (ISO 11649): RF, zwei Prüfziffern, insgesamt 5 bis 25 Zeichen, Buchstaben erlaubt. Sie erfüllt denselben Zweck wie die QR-Referenz und wird auch im Ausland verstanden.',
          ],
        },
        {
          heading: 'IBAN ohne Referenz (NON)',
          paragraphs: [
            'Eine QR-Rechnung nur mit IBAN und einer Mitteilung wie «Mitgliederbeitrag 2027, Rossi» ist gültig, aber die Mitteilung ist der einzige Hinweis darauf, wer bezahlt hat: Jede Zahlung wird von Hand zugeordnet. Bei 30 Mitgliedern geht das, bei 300 wird es mühsam.',
          ],
        },
      ],
      tip: 'Fragen Sie Ihre Bank, ob sie für das Vereinskonto eine QR-IBAN vergibt und ob dafür oder für die camt-Dateien Gebühren anfallen. Lohnt es sich nicht, funktioniert eine normale IBAN mit Creditor Reference genauso gut.',
    },
    {
      id: 'angaben-auf-der-qr-rechnung',
      heading: 'Was auf die QR-Rechnung gehört: Adressen, Beträge, Währungen',
      paragraphs: [
        'Das Konto muss eine Schweizer oder Liechtensteiner IBAN sein, beginnend mit CH oder LI, und Name und Adresse des Vereins sind Pflicht. Die Adresse des Mitglieds und sogar der Betrag sind optional: Fehlen sie, zeigt der Zahlteil leere Felder zum Ausfüllen von Hand. Das passt für Spenden und Gönnerbeiträge.',
        'Adressen müssen jetzt strukturiert sein, mit Strasse, Hausnummer, PLZ, Ort und Land in getrennten Feldern. Kombinierte Adresszeilen wurden in einer Übergangsfrist toleriert, die im November 2026 abgelaufen ist; eine Mitgliederliste, die die ganze Adresse in einer Zelle führt, muss also zuerst aufgeteilt werden. Der erweiterte Zeichensatz erlaubt nun auch Buchstaben wie ă, ł oder ș.',
        'Erlaubt sind nur Schweizer Franken und Euro, für Beträge von 0.01 bis knapp unter einer Milliarde, mit zwei Nachkommastellen gedruckt (CHF 1 250.00). Mitteilung und codierte Rechnungsinformationen teilen sich 140 Zeichen. Bezahlt ein Haushalt mehrere Mitgliedschaften auf einmal, ist eine Rechnung pro Haushalt mit einer Referenz leichter abzugleichen als mehrere.',
      ],
    },
    {
      id: 'beitraege-als-pdf-versenden',
      heading: 'Beiträge und Auszüge als PDF versenden',
      paragraphs: [
        'Die meisten Vereine verschicken QR-Rechnungen heute per E-Mail. Ein PDF ist zum Bezahlen im E-Banking oder Mobile Banking gedacht; Zahlteil und Empfangsschein sind darin mit einem Scherensymbol umrandet, damit ein Mitglied, das lieber an den Schalter geht, sie ausdrucken und abschneiden kann.',
      ],
      steps: [
        { title: 'Mitgliederliste bereinigen', text: 'Adressen in ihre Felder aufteilen und E-Mail-Adressen prüfen. Wie eine Liste aus der Tabelle kommt, zeigt der Ratgeber [von Excel zum CRM](page:guide-excel-to-crm).' },
        { title: 'Jeder Rechnung ihre Referenz geben', text: 'Eine Referenz pro Mitglied und Rechnung, mit Prüfziffer erzeugt, nie von Hand getippt.' },
        { title: 'PDF erzeugen', text: 'Rechnung oben, Zahlteil unten, in Originalgrösse gedruckt und nie auf die Seite eingepasst.' },
        { title: 'Versenden und festhalten', text: 'Jedem Mitglied das eigene PDF schicken und festhalten, was jedes schuldet, damit Sie wissen, was in der Bankdatei kommen sollte.' },
        { title: 'Erinnern', text: 'Nach Fälligkeit freundlich mit derselben Referenz mahnen, damit auch eine späte Zahlung passt.' },
      ],
    },
    {
      id: 'wie-mitglieder-bezahlen',
      heading: 'Wie Mitglieder eine QR-Rechnung bezahlen',
      paragraphs: ['Jedes Mitglied wählt seinen Weg, und eine sauber erstellte QR-Rechnung funktioniert für alle.'],
      bullets: [
        'Mobile Banking: das Mitglied scannt den QR-Code mit der Banking-App und bestätigt',
        'E-Banking: das Mitglied lädt das PDF hoch oder liest den Code mit Scanner oder Kamera ein, je nach Bank',
        'Postschalter: das Mitglied bringt den ausgedruckten Zahlteil, die Schaltergebühren gehen zulasten des Vereins',
        'Per Post: der Zahlteil geht mit einem Zahlungsauftrag an die Bank des Mitglieds',
        'TWINT: seit November 2026 bezahlt die TWINT-App QR-Rechnungen teilnehmender Rechnungssteller, erkennbar am TWINT-Logo',
      ],
    },
    {
      id: 'abgleich-mit-camt-dateien',
      heading: 'Zahlungen mit camt-Dateien abgleichen',
      paragraphs: [
        'Banken melden Zahlungseingänge in den camt-Formaten des Standards ISO 20022: camt.054, die Gutschriftsanzeige, und camt.053, der Kontoauszug. Sie laden sie als XML im E-Banking herunter, oder die Bank liefert sie. Jede Gutschrift enthält Betrag, Datum, Auftraggeber und, falls vorhanden, die Referenz.',
        'Die Software liest die Referenz und markiert die passende Rechnung als bezahlt. Übrig bleibt eine kurze Liste von Ausnahmen: eine Überweisung ohne Referenz, eine Teilzahlung, ein Mitglied, das doppelt bezahlt hat. Nur diese Buchungen sollte der Kassier noch von Hand prüfen. In CadenceE laden Sie die Bankdatei hoch, und die Zahlungseingänge werden mit den offenen Beträgen jedes Mitglieds abgeglichen: mehr zu [Club-Abrechnung und Zahlungen](page:billing).',
      ],
    },
    {
      id: 'alternativen',
      heading: 'Alternativen: TWINT, Karte, Lastschrift und eBill',
      paragraphs: [
        'Für Jahresbeiträge und Monatsauszüge bleibt die QR-Rechnung die beste Wahl, weil jedes Mitglied sie bezahlen kann. Für den Rest:',
      ],
      bullets: [
        'TWINT: sofort und vertraut, gut für Tickets, Barrechnungen und kleine Beträge',
        'Karte: praktisch für Mitglieder im Ausland und für online bezahlte Anzahlungen',
        'Lastschrift: SIX stellt LSV+ und BDD am 30. September 2028 ein und nimmt seit dem 1. Januar 2026 keine neuen Rechnungssteller mehr auf; Nachfolger ist eBill Direct Debit, das CH-DD von PostFinance ist nicht betroffen',
        'eBill: die Rechnung erscheint im E-Banking des Mitglieds zur Freigabe, ideal für wiederkehrende Beiträge; der Verein meldet sich über seine Bank oder einen Dienstleister an',
      ],
    },
    {
      id: 'haeufige-fehler',
      heading: 'Häufige Fehler bei der QR-Rechnung',
      paragraphs: [
        'Fast jede zurückgewiesene oder nicht zuordenbare QR-Rechnung geht auf einen dieser Fehler zurück. Die meisten verschwinden, wenn Rechnungen aus einem sauberen Mitgliederdatensatz erzeugt statt aus der Datei vom Vorjahr kopiert werden.',
      ],
      bullets: [
        'QR-Referenz mit normaler IBAN oder Creditor Reference mit QR-IBAN: Die Rechnung wird zurückgewiesen',
        'Kombinierte Adresszeilen aus einer alten Vorlage, nach November 2025 weiterverwendet',
        'Dieselbe Referenz für alle Mitglieder, womit sie für den Abgleich wertlos wird',
        'Ein im PDF-Text geänderter Betrag, der im QR-Code noch der alte ist',
        'Druck mit «An Seite anpassen», der den Zahlteil unter die vorgeschriebene Grösse verkleinert',
        'Das Privatkonto des Kassiers statt des Vereinskontos',
      ],
    },
    {
      id: 'mit-einem-crm-automatisieren',
      heading: 'Wie ein CRM die QR-Rechnung automatisiert',
      paragraphs: [
        'Der Ablauf von Hand ist bekannt: Mitgliederliste exportieren, Rechnungen in einem anderen Programm erzeugen, einzeln mailen und dann Bankbuchungen gegen eine Liste abhaken. Bei jedem Schritt kann eine veraltete Adresse durchrutschen oder eine Zahlung untergehen.',
        'In CadenceE enthält der Mitgliederdatensatz Adresse, Haushalt und Beitrag. Wiederkehrende Beiträge werden fristgerecht verrechnet, der PDF-Auszug jedes Mitglieds kann eine QR-Rechnung tragen, die Bankdatei wird mit den offenen Beträgen abgeglichen, und der Buchhaltungsexport geht nach Bexio oder Abacus. Mitglieder können auch per Karte, TWINT oder SEPA bezahlen; die übrigen Schweizer Besonderheiten zeigt das [Schweizer CRM](page:swiss).',
        'Die Abrechnung ist ein Teil der [Mitgliederverwaltung](page:membership). Erinnerungen und Verlängerungen behandelt der Ratgeber [Mitgliederbindung und Verlängerungen](page:guide-member-retention), eine rechtskonforme Mitgliederliste die [nDSG-Checkliste für Vereine](page:guide-fadp-checklist). Einen Auszug mit QR-Rechnung sehen Sie, wenn Sie [die Demo testen](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'QR-Rechnung: häufige Fragen',
    items: [
      { q: 'Kann man die orangen oder roten Einzahlungsscheine noch verwenden?', a: 'Nein. Banken und PostFinance verarbeiten sie seit dem 30. September 2022 nicht mehr, und Daueraufträge, die darauf beruhten, laufen auch nicht mehr. Die QR-Rechnung ist der einzige Schweizer Einzahlungsschein.' },
      { q: 'Braucht ein Verein eine QR-IBAN?', a: 'Nein. Die QR-IBAN braucht es nur für die QR-Referenz. Mit der normalen IBAN des Vereins nutzen Sie eine Creditor Reference (RF…) für den automatischen Abgleich, oder gar keine Referenz.' },
      { q: 'Was ist der Unterschied zwischen QR-Referenz und Creditor Reference?', a: 'Die QR-Referenz hat 27 Ziffern, funktioniert nur mit einer QR-IBAN und wird in der Schweiz und in Liechtenstein verwendet. Die Creditor Reference beginnt mit RF, hat 5 bis 25 Zeichen, funktioniert mit der normalen IBAN und ist ein internationaler Standard. Beide erlauben einen automatischen Abgleich.' },
      { q: 'Wie erstelle ich eine QR-Rechnung für den Verein?', a: 'Sie brauchen die IBAN oder QR-IBAN des Vereinskontos, strukturierte Adressen von Verein und Mitglied, den Betrag in CHF oder EUR und idealerweise eine Referenz pro Rechnung. Daraus erzeugt eine Buchhaltungs-, Vereins- oder CRM-Software Zahlteil und Empfangsschein, meist als PDF.' },
      { q: 'Darf man eine QR-Rechnung per E-Mail verschicken?', a: 'Ja. Als PDF wird sie im E-Banking oder Mobile Banking bezahlt. Zahlteil und Empfangsschein sind mit einem Scherensymbol umrandet, damit ein Mitglied sie ausdrucken und abschneiden kann, wenn es am Schalter bezahlt.' },
      { q: 'Geht eine QR-Rechnung auch in Euro oder ohne Betrag?', a: 'Sie lautet auf Schweizer Franken oder Euro, auf keine andere Währung. Betrag und Adresse des Zahlenden sind optional: Der Zahlteil zeigt dann leere Felder zum Ausfüllen von Hand, was Vereine für Spenden nutzen.' },
      { q: 'Was hat sich im November 2025 geändert?', a: 'Seit dem 22. November 2025 müssen Adressen auf QR-Rechnungen strukturiert sein, mit Strasse, Hausnummer, PLZ und Ort in getrennten Feldern, und mehr Zeichen mit Akzenten sind erlaubt. Rechnungen mit kombinierten Adresszeilen wurden nur in einer Übergangsfrist angenommen, die im November 2026 endete.' },
    ],
  },
  cta: {
    heading: 'Beiträge verschickt, Zahlungen abgeglichen',
    text: 'CadenceE setzt eine QR-Rechnung auf den Auszug jedes Mitglieds und gleicht Ihre Bankdatei mit den offenen Beträgen ab: Der Kassier sieht sofort, wer bezahlt hat.',
  },
};

export default guide;
