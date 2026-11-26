import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Ordnung schaffen',
  h1: 'Von Excel zum CRM: wann und wie Sie die Kundenliste ablösen',
  lead: 'Ein ehrlicher Vergleich von Excel und CRM, die Anzeichen, dass eine Tabelle Ihrem Club oder Verkaufsteam nicht mehr genügt, und ein Zweiwochenplan, um Daten zu bereinigen, zu importieren und umzusteigen, ohne etwas zu verlieren.',
  readingMinutes: 8,
  keyTakeaways: [
    'Eine Tabelle genügt für eine Person und einige Dutzend Kontakte; sobald mehrere damit arbeiten und Nachfassen zählt, stösst sie an Grenzen.',
    'Die Warnzeichen: Dubletten, konkurrierende Versionen, keine Historie der Änderungen und Nachfassen, das nur im Kopf existiert.',
    'Erst bereinigen, dann importieren: eine Zeile pro Person, eine Spalte pro Angabe, einheitliche Formate und ausdrücklich erfasste Einwilligungen.',
    'Mit Probelauf importieren und nur, wenn sich der Import rückgängig machen lässt; zuerst offene Leads und aktuelle Mitglieder, Archive später.',
    'Planen Sie zwei Wochen, bestimmen Sie eine verantwortliche Person, und prüfen Sie vor dem Entscheid, dass Sie alles wieder exportieren können.',
  ],
  sections: [
    {
      id: 'anzeichen-dass-excel-nicht-mehr-reicht',
      heading: 'Anzeichen, dass Excel nicht mehr reicht',
      paragraphs: [
        'Fast jeder Verein, jeder Club und jedes Verkaufsteam beginnt mit einer Tabelle, und das ist vernünftig: Excel ist auf jedem Computer, und alle können damit umgehen. Schwierig wird es, wenn die Datei zum Ort wird, an dem das Geschäft lebt, statt einer Liste, die jemand führt.',
      ],
      bullets: [
        'Dubletten: dieselbe Person steht dreimal drin, dreimal anders geschrieben, mit drei verschiedenen Telefonnummern',
        'Versionen: «Mitglieder_FINAL_v3_Anna.xlsx» und «Mitglieder_FINAL_neu.xlsx» existieren beide, und niemand weiss, welche gilt',
        'Keine Historie: wer hat die Nummer geändert, wann war der letzte Anruf, was wurde versprochen?',
        'Keine Erinnerungen: Nachfassen hängt am Gedächtnis, und ein warmer Lead kühlt in zwei hektischen Wochen ab',
        'Geteilte Datei als Risiko: ein unbedachtes Sortieren einer einzigen Spalte bringt Zeilen durcheinander, ein Tabellenblatt verschwindet, die Datei wird überschrieben',
        'Datenschutz: die ganze Mitgliederliste reist als E-Mail-Anhang und liegt auf Laptops, ohne Kontrolle darüber, wer was sieht',
      ],
      tip: 'Kommen Ihnen drei dieser Punkte bekannt vor, kostet Sie die Tabelle bereits Leads, Zahlungen oder Zeit. Planen Sie den Umstieg jetzt, bevor eine verlorene Datei ihn erzwingt.',
    },
    {
      id: 'was-ein-crm-bietet',
      heading: 'Was ein CRM stattdessen bietet',
      paragraphs: [
        'Ein CRM führt einen Datensatz pro Person und pro Firma und hängt alles daran: Anrufe, E-Mails, Termine, Verträge, Zahlungen und Notizen. Statt einer Zeile, die man von links nach rechts liest, haben Sie eine verlässliche Historie, und jede Kollegin kann dort weitermachen, wo ein Kollege aufgehört hat.',
        'Jeder Lead hat eine Phase und einen nächsten Schritt mit Datum, also erinnert das CRM statt Ihres Gedächtnisses. Dubletten werden bei Erfassung und Import erkannt, jede Änderung wird mit ihrem Urheber protokolliert, und der Zugriff richtet sich nach der Rolle: Das Barpersonal sieht nicht, was die Aufnahmekommission sieht.',
        'In CadenceE zum Beispiel begleitet das [Vertriebs-CRM](page:sales) jeden Lead durch fünf Phasen, von der Kontaktaufnahme bis zur Anzahlung, und die [Mitgliederverwaltung](page:membership) führt Mitglieder, Haushalte und Erneuerungen auf denselben Datensätzen. E-Mails gehen aus dem CRM hinaus und werden beim Empfänger festgehalten.',
      ],
    },
    {
      id: 'excel-vs-crm',
      heading: 'Excel vs CRM: ein ehrlicher Vergleich',
      paragraphs: [
        'Ein CRM ist nicht von vornherein besser. Es braucht etwas Einrichtung und Disziplin, und ein Team, das es nie öffnet, gewinnt nichts. Die eigentliche Frage ist, ob Ihre Arbeit über das hinausgewachsen ist, was ein Raster fassen kann.',
      ],
      subsections: [
        {
          heading: 'Wann eine Tabelle noch genügt',
          paragraphs: [
            'Eine Person mit einigen Dutzend Kontakten, eine einmalige Gästeliste, ein Budget oder eine schnelle Auswertung von Zahlen: Das alles kann Excel sehr gut. Viele Teams nutzen es nach dem Umstieg weiter, mit Exporten aus dem CRM, denn eine Pivot-Tabelle beantwortet eine ungewöhnliche Frage immer noch am schnellsten.',
          ],
        },
        {
          heading: 'Wann sich ein CRM lohnt',
          paragraphs: [
            'Mehrere Personen arbeiten an denselben Kontakten, Nachfassen entscheidet über Abschluss oder Verlängerung, Sie müssen vor einem Anruf wissen, was bisher geschah, oder Sie verwalten Personendaten, die Sie schützen und auf Anfrage herausgeben müssen. Treffen zwei dieser Punkte zu, wird die geteilte Tabelle eher zum Risiko als zum Werkzeug.',
          ],
        },
      ],
    },
    {
      id: 'daten-vorbereiten',
      heading: 'Die Daten vorbereiten',
      paragraphs: [
        'Die meisten Migrationen scheitern hier, nicht an der Software. Eine Stunde Bereinigung erspart eine Woche, in der Datensätze einzeln korrigiert werden.',
      ],
      steps: [
        { title: 'Alle Listen sammeln', text: 'Verkaufsliste, Mitgliederliste, Newsletter-Export, die Datei des Kassiers, Kontakte aus dem Mailprogramm: alles nebeneinanderlegen, bevor Sie etwas entscheiden.' },
        { title: 'Spalten festlegen', text: 'Vorname, Nachname, E-Mail, Telefon, Firma, Sprache, Status, Quelle, zuständige Person, nächster Schritt und sein Datum. Eine Angabe pro Spalte, nichts zusammengefasst.' },
        { title: 'Formate bereinigen', text: 'Namen aufteilen, überflüssige Leerzeichen entfernen, eine E-Mail-Adresse pro Zelle, Nummern im Format +41 79 123 45 67, ein Datumsformat, und Strasse, Hausnummer, PLZ und Ort getrennt.' },
        { title: 'Dubletten entfernen', text: 'Nach E-Mail sortieren, dann nach Name und PLZ. Entscheiden, welche Zeile gilt, und die Notizen zusammenführen, statt blind zu löschen.' },
        { title: 'Einwilligung festhalten', text: 'Eine Spalte für die Newsletter-Einwilligung mit Datum und Quelle anlegen. Wissen Sie nicht, ob jemand zugestimmt hat, nehmen Sie es nicht an.' },
        { title: 'Unnötiges weglassen', text: 'Alte Leads ohne Kontakt seit Jahren und Daten, deren Aufbewahrung Sie nicht begründen können, bleiben besser zurück.' },
      ],
      tip: 'Speichern Sie vor jeder Bereinigung eine unveränderte Kopie jeder Originaldatei, schreibgeschützt. Geht ein Schritt schief, fangen Sie einfach von vorne an.',
    },
    {
      id: 'der-import',
      heading: 'Der Import: Zuordnung, Probelauf und Rückgängig',
      paragraphs: [
        'Speichern Sie jedes bereinigte Blatt als CSV in UTF-8, damit Umlaute und Akzente wie ä, é und ç erhalten bleiben. Ordnen Sie dann jede Spalte einem Feld im CRM zu: «Natel» dem Telefonfeld, «Bemerkungen» einer Notiz. Eine Spalte ohne passendes Feld wird zur Notiz oder bewusst weggelassen.',
        'Importieren Sie nie eine ganze Datei blind. Ein Probelauf zeigt, wie viele Datensätze neu entstehen, aktualisiert oder als Dubletten übersprungen werden. Prüfen Sie zehn Datensätze von Hand gegen das Original, auch den mit dem kompliziertesten Namen, und starten Sie erst dann den echten Import, im Wissen, dass Sie ihn rückgängig machen können.',
        'In CadenceE lassen sich Leads, Kontakte, Firmen und Mitglieder aus Tabellen importieren. Jeder Import beginnt mit einem Probelauf, der das Ergebnis zeigt, der Dublettenschutz greift auch bei importierten Leads, und ein Import lässt sich rückgängig machen.',
      ],
    },
    {
      id: 'was-zuerst-migrieren',
      heading: 'Was Sie zuerst übernehmen',
      paragraphs: [
        'Beginnen Sie mit dem, was das Team am Montagmorgen braucht, nicht mit zehn Jahren Geschichte:',
      ],
      bullets: [
        'Offene Leads und Deals, jeweils mit zuständiger Person und nächstem Schritt',
        'Aktuelle Mitglieder oder Kunden, mit Haushalt oder Firma',
        'Firmen und Partner, mit denen Sie zusammenarbeiten',
        'Eine Zusammenfassung früherer Kontakte: letztes Datum und eine kurze Notiz statt jedes alten Anrufs',
        'Archive nur, wenn Sie sie wirklich brauchen, und erst, wenn alles andere läuft',
      ],
    },
    {
      id: 'das-team-mitnehmen',
      heading: 'Das Team mitnehmen',
      paragraphs: [
        'Software ändert Gewohnheiten nicht von allein. Teams, die gut umsteigen, einigen sich auf wenige Regeln und halten sie im ersten Monat ein.',
      ],
      bullets: [
        'Eine verantwortliche Person bestimmen, die über Felder entscheidet, Fragen beantwortet und Fehler korrigiert',
        'Vereinbaren: Was nicht im CRM steht, ist nicht passiert',
        'Die alte Tabelle am Umstiegstag schreibschützen, damit es nur einen Arbeitsort gibt',
        'Alle an ihren eigenen Leads und Mitgliedern schulen, nicht an Beispieldaten',
        'Jeder Person eine gespeicherte Ansicht ihrer Liste und einen täglichen Blick auf das Fällige geben',
        'Alle in ihrer Sprache arbeiten lassen: CadenceE läuft auf Deutsch, Französisch und Englisch',
      ],
      tip: 'Eine kurze Morgenroutine festigt die Gewohnheit. In CadenceE bereitet der [KI-Assistent](page:ai) jeden Morgen eine Übersicht dessen vor, was heute Aufmerksamkeit braucht, und gutes Nachfassen erklärt der Ratgeber zu [Nachfass-E-Mails im Vertrieb](page:guide-sales-follow-up).',
    },
    {
      id: 'zweiwochenplan',
      heading: 'Ein Plan für den Umstieg in zwei Wochen',
      paragraphs: [
        'Zwei Wochen reichen für einen Verein oder ein kleines Verkaufsteam, wenn eine Person das Projekt führt.',
      ],
      steps: [
        { title: 'Tag 1 und 2: entscheiden', text: 'Verantwortliche Person bestimmen, alle genutzten Tabellen auflisten und Felder und Phasen festlegen.' },
        { title: 'Tag 3 bis 5: bereinigen', text: 'Formate, Dubletten, Einwilligungen, und aussortieren, was nicht mitkommt.' },
        { title: 'Tag 6: testen', text: 'Fünfzig Zeilen mit Probelauf importieren, von Hand prüfen, die Zuordnung anpassen und den Test rückgängig machen.' },
        { title: 'Tag 7: einrichten', text: 'Kolleginnen und Kollegen mit ihren Rollen einladen, E-Mail-Vorlagen und gespeicherte Ansichten vorbereiten.' },
        { title: 'Tag 8: importieren', text: 'Den vollständigen Import starten, Stichproben prüfen und die Tabellen schreibschützen.' },
        { title: 'Tag 9 und 10: schulen', text: 'Jeder Person ihre eigenen Datensätze und die tägliche Routine zeigen.' },
        { title: 'Tag 11 bis 14: arbeiten und korrigieren', text: 'Nur noch im CRM arbeiten, Fragen sammeln, Fehler beheben und am Ende der Woche gemeinsam Bilanz ziehen.' },
      ],
    },
    {
      id: 'daten-wieder-exportieren',
      heading: 'Daten wieder exportieren: kein Lock-in',
      paragraphs: [
        'Testen Sie den Ausgang, bevor Sie sich festlegen. Sie sollten Ihre Listen jederzeit als CSV exportieren, Ihrer Treuhand einen Buchhaltungsexport übergeben und einer Person auf Anfrage alles herausgeben können, was Sie über sie gespeichert haben, wie es das DSG verlangt. Ein System, das den Abschied schwer macht, verrät damit einiges über sich.',
        'In CadenceE lassen sich Listen wie Leads und Aktivitäten als CSV exportieren, der Buchhaltungsexport geht nach Bexio oder Abacus, und ein Auskunftsbegehren exportiert alles, was zu einer Person gespeichert ist. Worauf Sie bei der Wahl achten sollten, zeigt der Ratgeber [das richtige CRM wählen](page:guide-choose-a-crm), den Datenschutz die [nDSG-Checkliste für Vereine](page:guide-fadp-checklist). Vereine, die Beiträge verrechnen, lesen auch den Ratgeber zur [QR-Rechnung im Verein](page:guide-qr-bill), denn sie verlangt strukturierte Adressen. Wie Ihre Listen in einem CRM aussehen, zeigt Ihnen [die Demo](/login?demo=1); alle Möglichkeiten finden Sie unter [Funktionen](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'Excel vs CRM: häufige Fragen',
    items: [
      { q: 'Ist Excel ein CRM?', a: 'Nein. Excel kann eine Kontaktliste speichern, aber ohne Historie pro Person, ohne Erinnerungen, ohne Dublettenschutz und ohne Kontrolle, wer was sieht oder ändert: genau das, wofür ein CRM gebaut ist.' },
      { q: 'Wann sollte ein KMU die Kundenliste in Excel ablösen?', a: 'Wenn mehrere Personen an denselben Kontakten arbeiten, wenn Nachfassen über Verkäufe oder Verlängerungen entscheidet, oder wenn Sie Personendaten verwalten, die geschützt werden müssen. Eine einzelne Person mit einer kurzen Liste kann bei der Tabelle bleiben.' },
      { q: 'Wie lange dauert der Umstieg von Excel auf ein CRM?', a: 'Für einen Verein oder ein kleines Verkaufsteam etwa zwei Wochen: Bereinigung, Testimport, vollständiger Import und eine Woche Arbeit im CRM, in der eine Person laufend korrigiert.' },
      { q: 'Gehen beim Import einer Tabelle Daten verloren?', a: 'Nicht, wenn Sie eine schreibgeschützte Kopie des Originals behalten, jede Spalte bewusst zuordnen und zuerst einen Probelauf machen, in einem CRM, in dem sich ein Import rückgängig machen lässt.' },
      { q: 'Können wir Excel neben dem CRM weiter nutzen?', a: 'Ja, für Auswertungen mit Exporten aus dem CRM. Ändern Sie Kontakte aber nicht an beiden Orten, sonst haben Sie wieder zwei Wahrheiten.' },
      { q: 'Ist eine geteilte Excel-Mitgliederliste nach DSG ein Problem?', a: 'Das Gesetz verbietet keine Tabellen, verlangt aber eine angemessene Datensicherheit und dass Sie Auskunftsbegehren beantworten können. Eine Datei, die ohne Zugriffskontrolle per E-Mail herumgeht, erschwert beides. Mehr in der [nDSG-Checkliste für Vereine](page:guide-fadp-checklist).' },
    ],
  },
  cta: {
    heading: 'Die Tabelle loslassen, die Daten behalten',
    text: 'CadenceE importiert Ihre Leads, Kontakte und Mitglieder aus einer Tabelle, zuerst mit Probelauf und danach rückgängig machbar, und exportiert Ihre Listen jederzeit wieder als CSV.',
  },
};

export default guide;
