import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Clubverwaltung',
  h1: 'Einen Privatclub führen: der Ratgeber für die Praxis',
  lead: 'Wie man einen Privatclub gut führt, von der ersten Anfrage bis zur zehnten Erneuerung: Mitgliederkategorien, Aufnahme, Anlässe, Konsumationen und Beiträge, Rollen im Team, Mitgliederdaten unter dem DSG und die passende Software für Mitgliederclubs.',
  readingMinutes: 8,
  keyTakeaways: [
    'Wenige Mitgliederkategorien, jede mit klaren Rechten, Beiträgen und einer Obergrenze.',
    'Ein schriftlich festgelegter Aufnahmeprozess, von der Anfrage bis zum Willkommen.',
    'Die ersten drei Monate entscheiden, ob ein neues Mitglied bleibt.',
    'Alle Konsumationen auf ein Mitgliederkonto und ein Auszug pro Monat.',
    'Die Mitgliederliste ist vertraulich: Rollen, Zugriff nach Bedarf und das DSG.',
    'Ein System von der ersten Anfrage bis zur Erneuerung statt fünf Tabellen.',
  ],
  sections: [
    {
      id: 'worum-es-geht',
      heading: 'Was es heisst, einen Privatclub zu führen',
      paragraphs: [
        'Ein Privatclub verkauft weder ein Essen noch einen Raum, sondern einen Platz in einer Gemeinschaft. Ihn gut zu führen heisst, drei Dinge im Gleichgewicht zu halten: die richtigen Mitglieder, und genug davon; Gründe, wiederzukommen, vom Programm bis zum Service; und Zahlen, die aufgehen, mit pünktlich bezahlten Beiträgen und beglichenen Konsumationen. Dieser Ratgeber geht die einzelnen Bereiche der Reihe nach durch.',
      ],
    },
    {
      id: 'mitgliederkategorien',
      heading: 'Eine verständliche Mitgliederstruktur',
      paragraphs: [
        'Ihre Mitgliederkategorien sind Ihr Sortiment. Jede sollte sagen, für wen sie gedacht ist, was sie umfasst, wie hoch die Eintrittsgebühr und der Jahresbeitrag sind. Typisch für Schweizer Clubs sind:',
      ],
      bullets: [
        'Einzelmitgliedschaft: die Standardmitgliedschaft mit vollem Zugang zum Club.',
        'Paar oder Familie: Partner und Kinder in einem Haushalt, gemeinsam abgerechnet.',
        'Junge Mitglieder: reduzierter Beitrag bis zu einer Altersgrenze, um den Mitgliederbestand zu verjüngen.',
        'Auswärtige Mitglieder: für alle, die weit weg wohnen und seltener kommen.',
        'Firmenmitgliedschaft: eine Firma hält Mitgliedschaften für mehrere Mitarbeitende.',
        'Ehrenmitglied: vom Vorstand ernannt, meist ohne Beitrag.',
      ],
      tip: 'Vier bis sechs Kategorien reichen fast immer. Jede zusätzliche Kategorie bringt eine weitere Regel, die man erklären muss, und einen weiteren Fall in der Abrechnung.',
      subsections: [
        {
          heading: 'Eine Obergrenze festlegen, bevor man sie braucht',
          paragraphs: [
            'Ein Club, der jeden Donnerstag überfüllt ist, verliert seinen Reiz. Leiten Sie eine Obergrenze aus Ihrer Kapazität und der Besuchshäufigkeit eines durchschnittlichen Mitglieds ab, legen Sie sie pro Kategorie fest, bevor die Nachfrage Sie dazu zwingt, und führen Sie danach eine Warteliste.',
          ],
        },
      ],
    },
    {
      id: 'anfragen-und-aufnahme',
      heading: 'Von der Anfrage zur Aufnahme',
      paragraphs: [
        'Gute Kandidatinnen und Kandidaten haben meist auch andere Möglichkeiten. Die Aufnahme muss deshalb fair, persönlich und schnell sein. Halten Sie den Ablauf einmal schriftlich fest, damit jede Anfrage gleich behandelt wird, egal wer das Telefon abnimmt.',
      ],
      steps: [
        { title: 'Jede Anfrage erfassen', text: 'Website-Formular, E-Mail, Telefon oder Empfehlung eines Mitglieds: Jede Anfrage landet noch am selben Tag in einer Liste, mit dem Namen der empfehlenden Person.' },
        { title: 'Kennenlernen und Club zeigen', text: 'Ein Besuch oder ein Mittagessen mit der für Mitglieder zuständigen Person: Hier entscheiden sich die meisten.' },
        { title: 'Antrag und Paten', text: 'Ein kurzer Mitgliedsantrag und, falls Ihre Regeln es vorsehen, ein oder zwei Mitglieder, die den Antrag unterstützen.' },
        { title: 'Entscheid', text: 'Vorstand oder Aufnahmekommission entscheiden nach schriftlichen Kriterien und in einem bekannten Rhythmus.' },
        { title: 'Vertrag und Eintrittsgebühr', text: 'Das neue Mitglied unterschreibt und bezahlt die Eintrittsgebühr oder Anzahlung, idealerweise im selben Schritt.' },
      ],
      tip: 'Unser Ratgeber zum [Aufnahmeprozess für neue Mitglieder](page:guide-membership-applications) beschreibt jeden Schritt im Detail, auch was ein Aufnahmeantrag fragen soll und was nicht.',
    },
    {
      id: 'neue-mitglieder-integrieren',
      heading: 'Neue Mitglieder in den ersten drei Monaten integrieren',
      paragraphs: [
        'Wer nach einigen Monaten noch keinen Platz im Club gefunden hat, kündigt eher bei der ersten Erneuerung. Behandeln Sie die Integration als kleines Programm, nicht als einzelne E-Mail.',
      ],
      bullets: [
        'Eine Willkommensnachricht mit Hausordnung, Öffnungszeiten und Kontakten.',
        'Zugang zum Mitgliederportal, um zu reservieren und den Auszug zu sehen.',
        'Eine persönliche Vorstellung beim Team und bei einigen Mitgliedern mit ähnlichen Interessen.',
        'Eine Einladung zu einem ersten Anlass innerhalb eines Monats.',
        'Ein Anruf nach etwa zwei Monaten: Entspricht der Club den Erwartungen?',
      ],
    },
    {
      id: 'anlaesse-und-reservationen',
      heading: 'Anlässe, Reservationen und Privatanlässe',
      paragraphs: [
        'Das Programm macht aus einer Mitgliedschaft eine Gewohnheit. Planen Sie es nach Saison, mischen Sie feste Formate wie ein monatliches Dinner mit einigen besonderen Abenden, und publizieren Sie die Daten früh. Führen Sie für ausgebuchte Anlässe eine Warteliste, damit aus einer Absage eine Buchung wird, und legen Sie eine klare Regel für kurzfristige Absagen fest.',
        'Die Vermietung von Räumen für Geburtstage oder Firmenessen kann eine willkommene Einnahmequelle sein. Behandeln Sie jede Anfrage wie einen Verkauf: Offerte, Vertrag, Anzahlung und bestätigtes Menü. Erfasste Besuche und Check-ins zeigen ausserdem, wer den Club wirklich nutzt. Mehr dazu auf unserer Seite zum [CRM für Gastronomie und Hotellerie](page:hospitality).',
      ],
    },
    {
      id: 'konsumationen-und-beitraege',
      heading: 'Konsumationen, Mitgliederkonten und Beiträge',
      paragraphs: [
        'Im Club unterschreibt man die Rechnung, statt an der Bar zu bezahlen. Jede Konsumation muss deshalb auf dem Konto des Mitglieds landen. Der einfachste Rhythmus: ein Monatsauszug pro Mitglied oder Haushalt, der Beitrag, Konsumationen, Anlässe und Privatanlässe zusammenfasst.',
      ],
      bullets: [
        'Verbinden Sie die Kasse mit dem Mitglieder-Datensatz, damit nichts abgetippt wird.',
        'Legen Sie pro Konto eine Kreditlimite und eine Regel für Zahlungsrückstände fest.',
        'Legen Sie jedem Auszug eine QR-Rechnung bei, und bieten Sie auch TWINT oder Karte an.',
        'Gleichen Sie die Bankdatei mit den offenen Beträgen ab, statt Zahlungen von Hand abzuhaken.',
        'Exportieren Sie saubere Zahlen in die Buchhaltung und korrigieren Sie Fehler mit Stornobuchungen.',
      ],
      tip: 'Klären Sie mit Ihrem Treuhänder, wie Beiträge, Eintrittsgebühren und Gastronomie mehrwertsteuerlich behandelt werden, bevor Sie Ihre Tarife festlegen. Unser Ratgeber zur [QR-Rechnung im Verein](page:guide-qr-bill) behandelt die Zahlungsseite.',
    },
    {
      id: 'kommunikation',
      heading: 'Kommunizieren wie ein Club, nicht wie eine Marke',
      paragraphs: [
        'Mitglieder wollen informiert, nicht umworben werden. Ein monatlicher Newsletter mit dem Programm, ab und zu eine persönliche Notiz der Geschäftsführung und eine Erinnerung vor jedem Anlass genügen meist. Lassen Sie jedes Mitglied seine Sprache wählen: Deutsch, Französisch oder Englisch.',
        'Schicken Sie Newsletter nur an Personen, die eingewilligt haben, und mit Abmeldelink in jedem Versand. Auszüge und Reservationsbestätigungen gehören dagegen zur Mitgliedschaft selbst.',
      ],
    },
    {
      id: 'erneuerungen',
      heading: 'Erneuerungen und Mitgliederbindung',
      paragraphs: [
        'Ob ein Mitglied verlängert, entscheidet sich lange vor dem Stichtag. Achten Sie auf Signale: seit drei Monaten kein Besuch, keine Reservationen mehr, ein offener Auszug. Rufen Sie diese Mitglieder an, bevor die Erneuerung verschickt wird, nicht erst, wenn die Kündigung eintrifft.',
        'Halten Sie Laufzeit, Erneuerung und Kündigungsfrist klar in Statuten oder Mitgliedschaftsvertrag fest. Ist Ihr Club ein Verein nach Zivilgesetzbuch, gilt bereits eine gesetzliche Regel: Mitglieder können mit einer Frist von sechs Monaten auf das Ende des Kalenderjahres oder der Verwaltungsperiode austreten, und die Statuten können diese Frist verkürzen, aber nicht verlängern. Lassen Sie Ihre Regeln von einer Fachperson prüfen. Unser Ratgeber zu [Mitgliederbindung und Verlängerungen](page:guide-member-retention) geht weiter.',
      ],
    },
    {
      id: 'rollen-im-team',
      heading: 'Rollen im Team: wer macht was',
      paragraphs: [
        'Auch ein kleiner Club läuft besser, wenn jede Aufgabe eine verantwortliche Person hat:',
      ],
      bullets: [
        'Geschäftsführung: Programm, Budget, Team und Verbindung zum Vorstand.',
        'Mitgliederbetreuung: Anfragen, Besuche, Anträge, Integration und Erneuerungen.',
        'Anlässe und Gastronomie: Kalender, Reservationen, Privatanlässe und Service.',
        'Empfang: Mitglieder und Gäste begrüssen, Check-ins und tägliche Anliegen.',
        'Finanzen: Beiträge, Auszüge, Zahlungen, Mahnungen und Export in die Buchhaltung.',
        'Vorstand oder Aufnahmekommission: Aufnahmen, Regeln und langfristige Ausrichtung.',
      ],
      tip: 'Geben Sie jeder Person den Zugriff, den ihre Rolle braucht, und nicht mehr. Die Bar muss eine Rechnung eröffnen können, nicht die Notizen der Aufnahmekommission lesen.',
    },
    {
      id: 'mitgliederdaten-und-dsg',
      heading: 'Mitgliederdaten und Datenschutz nach DSG',
      paragraphs: [
        'Die Mitgliederliste ist das sensibelste Gut eines Clubs. Das revidierte Datenschutzgesetz (DSG, oft nDSG genannt) gilt seit dem 1. September 2023, für Vereine ebenso wie für Firmen. Konkret heisst das: nur bearbeiten, was nötig ist, in einer Datenschutzerklärung offenlegen, was mit den Daten geschieht, Auskunftsbegehren beantworten, die Daten schützen und löschen, wenn sie nicht mehr gebraucht werden.',
        'Seien Sie besonders vorsichtig mit besonders schützenswerten Daten wie Gesundheit, religiösen oder politischen Ansichten und Strafverfahren: Ein Club braucht sie kaum je. Fragen Sie, bevor Sie Fotos von Mitgliedern an Anlässen veröffentlichen. Buchhaltungsunterlagen folgen eigenen Regeln und werden in der Schweiz in der Regel zehn Jahre aufbewahrt. Unsere [nDSG-Checkliste für Vereine](page:guide-fadp-checklist) zeigt die Schritte; für Ihren Fall wenden Sie sich an eine Fachperson für Datenschutz.',
      ],
    },
    {
      id: 'software-fuer-mitgliederclubs',
      heading: 'Software für Mitgliederclubs wählen',
      paragraphs: [
        'Viele Clubs arbeiten mit einer Mitgliedertabelle, einem E-Mail-Tool, einer Kasse und einer Buchhaltung, die nichts voneinander wissen. Bei jeder Übergabe geht eine Anfrage oder eine Konsumation verloren. Wenn Sie Software für die Clubverwaltung vergleichen, prüfen Sie, ob sie Folgendes bietet:',
      ],
      bullets: [
        'Einen Datensatz pro Person, von der ersten Anfrage bis zur Erneuerung und zum Austritt.',
        'Online-Mitgliedsanträge, entschieden in einer Liste.',
        'Online unterschriebene Verträge, mit der Eintrittsgebühr bei Unterzeichnung.',
        'Anlässe mit Warteliste und Konsumationen, die mit der Kasse verbunden sind.',
        'Beiträge, Auszüge und Schweizer Zahlungsmittel, mit Export in die Buchhaltung.',
        'E-Mails mit Einwilligung, mehrere Sprachen, Rollen und ein Audit-Log.',
      ],
      subsections: [
        {
          heading: 'Wo CadenceE passt',
          paragraphs: [
            'CadenceE ist ein Schweizer [CRM für Privatclubs](page:private-clubs), das diese Liste an einem Ort abdeckt: Aufnahmeanträge, online unterschriebene Verträge mit Anzahlung, [Mitgliederverwaltung](page:membership) mit Haushalten und Erneuerungen, Anlässe, Konsumationen mit Lightspeed-Kasse und [Abrechnung mit QR-Rechnung und TWINT](page:billing). Entwickelt wurde es zusammen mit dem Pavillon 46, einem Privatclub in der Waadt. Was immer Sie wählen: Unser Ratgeber [das richtige CRM wählen](page:guide-choose-a-crm) hilft beim Vergleich, und [von Excel zum CRM](page:guide-excel-to-crm) beschreibt den Umstieg. Sie können auch [die Demo testen](/login?demo=1).',
          ],
        },
      ],
    },
  ],
  faq: {
    heading: 'Einen Privatclub führen: häufige Fragen',
    items: [
      {
        q: 'Was ist ein Privatclub?',
        a: 'Ein Club, dessen Räume und Leistungen Mitgliedern und ihren Gästen vorbehalten sind. Man wird auf Antrag aufgenommen, bezahlt eine Eintrittsgebühr und einen Jahresbeitrag und begleicht Essen und Getränke meist über das Mitgliederkonto.',
      },
      {
        q: 'Wie viele Mitglieder sollte ein Privatclub haben?',
        a: 'Rechnen Sie von der Kapazität aus: wie viele Personen der Club zu Spitzenzeiten angenehm aufnimmt und wie oft ein durchschnittliches Mitglied kommt. Legen Sie pro Kategorie eine Obergrenze fest, die diese Abende angenehm hält, und führen Sie danach eine Warteliste.',
      },
      {
        q: 'Ist ein Privatclub ein Verein nach Schweizer Recht?',
        a: 'Er kann es sein. Viele Clubs sind Vereine nach Zivilgesetzbuch, deren Statuten Aufnahme, Beiträge, Ausschluss und Austritt regeln. Andere werden von einer Firma betrieben, die Mitgliedschaften per Vertrag verkauft. Die Rechtsform bestimmt, wer entscheidet und wie man austritt; lassen Sie sich beraten.',
      },
      {
        q: 'Wie verrechnet man den Mitgliedern Konsumationen?',
        a: 'Jede Konsumation geht aufs Mitgliederkonto, und ein Monatsauszug fasst Beitrag, Konsumationen und Anlässe zusammen, mit QR-Rechnung und TWINT oder Karte als Alternative. Mehr zu [Abrechnung und Zahlungen](page:billing).',
      },
      {
        q: 'Gilt das DSG auch für Mitgliederclubs?',
        a: 'Ja. Es gilt für private Personen und Organisationen, die Personendaten bearbeiten, also auch für Vereine und Firmen, die einen Club betreiben.',
      },
      {
        q: 'Welche Software nutzen Mitgliederclubs?',
        a: 'Oft laufen eine Mitgliederdatenbank, ein Reservationstool, eine Kasse und eine Buchhaltung nebeneinander. Ein Club-CRM wie CadenceE bringt Anfragen, Mitglieder, Anlässe, Konsumationen und Abrechnung in ein System.',
      },
    ],
  },
  cta: {
    heading: 'Der ganze Club, ein Datensatz pro Mitglied',
    text: 'CadenceE begleitet jede Person von der ersten Anfrage über den unterschriebenen Vertrag bis zu Monatsauszug und Erneuerung, auf Deutsch, Französisch oder Englisch.',
  },
};

export default guide;
