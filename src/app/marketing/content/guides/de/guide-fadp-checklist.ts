import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Datenschutz',
  h1: 'nDSG-Checkliste für Vereine, Clubs und KMU',
  lead: 'Seit dem 1. September 2023 gilt das revidierte Datenschutzgesetz (DSG, oft nDSG genannt) für jeden Verein, jeden Club und jedes KMU in der Schweiz, das Personendaten bearbeitet. Diese Checkliste führt Sie Punkt für Punkt durch die Pflichten, mit dem jeweiligen Artikel und dem, was ein CRM dabei übernimmt. Sie bietet allgemeine Informationen, keine Rechtsberatung.',
  readingMinutes: 8,
  keyTakeaways: [
    'Das nDSG gilt für jeden Verein, jeden Club und jedes Unternehmen, das Personendaten bearbeitet, unabhängig von der Grösse.',
    'Wer weniger als 250 Mitarbeitende hat, braucht meist kein Verzeichnis der Bearbeitungstätigkeiten, wohl aber eine Datenschutzerklärung und angemessene Sicherheit.',
    'Auskunftsbegehren sind in der Regel innert 30 Tagen und unentgeltlich zu beantworten.',
    'Eine Datenpanne mit voraussichtlich hohem Risiko ist dem EDÖB so rasch als möglich zu melden.',
    'Werbe-E-Mails brauchen eine vorgängige Einwilligung, den korrekten Absender und eine einfache Abmeldung, mit einer engen Ausnahme für bestehende Kunden.',
  ],
  sections: [
    {
      id: 'fuer-wen-das-ndsg-gilt',
      heading: 'Für wen das nDSG gilt',
      paragraphs: [
        'Das Bundesgesetz über den Datenschutz vom 25. September 2020 ist am 1. September 2023 zusammen mit der Datenschutzverordnung (DSV) in Kraft getreten. Es gilt für private Personen, also auch für Vereine, Stiftungen und Unternehmen jeder Grösse, sowie für Bundesorgane. Geschützt sind natürliche Personen: ein Mitglied, ein Gast, die Kontaktperson eines Lieferanten, nicht aber eine Firma als solche. Es erfasst auch Sachverhalte im Ausland, die sich in der Schweiz auswirken (Art. 3).',
        'Die europäische DSGVO gilt in der Schweiz nicht direkt. Sie kann aber auf eine Schweizer Organisation anwendbar sein, die Personen in der EU Waren oder Dienstleistungen anbietet oder deren Verhalten beobachtet, etwa ein Club, der gezielt Mitglieder im benachbarten Ausland wirbt.',
        'Bestimmte vorsätzliche Verstösse (Informationspflicht missachten, falsche oder unvollständige Auskunft erteilen, Auftragsbearbeiter ohne Sorgfalt beiziehen, Daten ohne Garantien ins Ausland bekanntgeben, Mindestanforderungen an die Datensicherheit missachten) werden auf Antrag mit Busse bis zu 250 000 Franken bestraft, und zwar die verantwortlichen Personen (Art. 60 und 61).',
      ],
    },
    {
      id: 'personendaten-erfassen',
      heading: '1. Erfassen Sie, welche Personendaten Sie haben',
      paragraphs: [
        'Beginnen Sie mit einer einfachen Tabelle: welche Daten, über wen, zu welchem Zweck, wo gespeichert, wer Zugriff hat, wem sie bekanntgegeben werden und wie lange Sie sie aufbewahren. Typische Gruppen sind Mitglieder und ihre Haushalte, Antragstellende, Gäste, Newsletter-Abonnenten, Kundinnen und Kunden, Mitarbeitende, Freiwillige, Vorstand sowie Kontaktpersonen bei Lieferanten und Firmenmitgliedern.',
      ],
      subsections: [
        {
          heading: 'Besonders schützenswerte Personendaten',
          paragraphs: [
            'Besonders geschützt sind (Art. 5): Gesundheit, religiöse, weltanschauliche oder politische Ansichten, Ethnie, genetische und biometrische Daten, Strafverfahren und Massnahmen der sozialen Hilfe. Eine Allergie, die für ein Clubessen notiert wird, oder eine Verletzung im Sportverein sind Gesundheitsdaten. Stützen Sie sich dafür auf eine Einwilligung, muss sie ausdrücklich erfolgen (Art. 6 Abs. 7), und die Bekanntgabe an Dritte braucht einen Rechtfertigungsgrund (Art. 30 und 31).',
          ],
        },
      ],
      tip: 'Denken Sie an alles ausserhalb Ihres Hauptsystems: Tabellen auf Laptops, Papierformulare, E-Mail-Postfächer, Chatgruppen und Exporte an die Treuhand.',
    },
    {
      id: 'zwecke-und-datenschutzerklaerung',
      heading: '2. Zwecke festlegen und eine Datenschutzerklärung veröffentlichen',
      paragraphs: [
        'Personendaten dürfen nur zu einem für die betroffene Person erkennbaren Zweck beschafft und nur damit vereinbar bearbeitet werden (Art. 6). Anders als die DSGVO verlangt das DSG nicht für jede Bearbeitung eine Rechtsgrundlage wie die Einwilligung. Einen Rechtfertigungsgrund (Einwilligung, überwiegendes Interesse oder Gesetz) braucht es erst bei einer Persönlichkeitsverletzung, etwa wenn die Person widersprochen hat (Art. 30 und 31); die Abwicklung eines Mitgliedschaftsvertrags ist ein typisches überwiegendes Interesse.',
        'Bei der Beschaffung müssen Sie informieren (Art. 19), präzise, verständlich und leicht zugänglich (Art. 13 DSV). Ihre Datenschutzerklärung nennt mindestens:',
      ],
      bullets: [
        'wer Sie sind und wie man Sie erreicht;',
        'den Bearbeitungszweck und die Empfänger;',
        'bei Bekanntgabe ins Ausland die Staaten und die Garantien;',
        'falls die Daten nicht von der Person selbst stammen, die Kategorien der Daten.',
      ],
    },
    {
      id: 'verzeichnis-der-bearbeitungstaetigkeiten',
      heading: '3. Prüfen Sie, ob Sie ein Verzeichnis der Bearbeitungstätigkeiten brauchen',
      paragraphs: [
        'Das Verzeichnis (Art. 12) beschreibt für jede Tätigkeit Zweck, Kategorien von Personen und Daten, Empfänger, Aufbewahrungsdauer, Sicherheitsmassnahmen und allfällige Bekanntgaben ins Ausland.',
        'Die Ausnahme ist klar umrissen (Art. 24 DSV): Unternehmen und andere privatrechtliche Organisationen mit weniger als 250 Mitarbeitenden am 1. Januar sind befreit, ausser sie bearbeiten besonders schützenswerte Personendaten in grossem Umfang oder führen ein Profiling mit hohem Risiko durch. Es zählen die Mitarbeitenden, nicht die Mitglieder oder Freiwilligen: Ein Verein mit 3000 Mitgliedern und 20 Angestellten ist deshalb in der Regel befreit.',
      ],
      tip: 'Führen Sie die Tabelle aus Schritt 1 trotzdem nach: Sie ist die Grundlage Ihrer Datenschutzerklärung und hilft bei Auskunftsbegehren und Datenpannen.',
    },
    {
      id: 'datensicherheit',
      heading: '4. Sorgen Sie für Datensicherheit',
      paragraphs: [
        'Die Sicherheit muss dem Risiko angemessen sein (Art. 8). Die Verordnung verlangt namentlich, dass Personen nur auf die Daten zugreifen, die sie für ihre Aufgabe brauchen, dass nachvollziehbar ist, wer was eingegeben oder geändert hat, dass Daten nach einem Vorfall wiederhergestellt werden können und dass Software aktuell ist (Art. 3 DSV). Konkret:',
      ],
      bullets: [
        'Persönliche Konten statt geteilter Logins, mit Zwei-Faktor-Authentifizierung.',
        'Rollen: Das Barpersonal braucht die Zahlungshistorie der Mitglieder nicht.',
        'Zugänge am Tag des Austritts von Freiwilligen oder Mitarbeitenden entfernen.',
        'Verschlüsselte Geräte, regelmässige Backups, Papierformulare unter Verschluss.',
      ],
    },
    {
      id: 'auftragsbearbeiter-und-vertraege',
      heading: '5. Regeln Sie die Auftragsbearbeitung vertraglich',
      paragraphs: [
        'CRM, E-Mail-Dienst, Zahlungsanbieter und Cloud-Speicher sind Auftragsbearbeiter. Sie dürfen ihnen Daten vertraglich übertragen, wenn sie die Daten nur so bearbeiten, wie Sie es selbst dürften, und Sie müssen sich vergewissern, dass sie die Datensicherheit gewährleisten (Art. 9). Unterauftragsbearbeiter dürfen sie nur mit Ihrer vorgängigen Genehmigung beiziehen (Art. 9 Abs. 3). Schliessen Sie mit jedem einen Auftragsbearbeitungsvertrag ab und halten Sie fest, wo die Daten liegen.',
      ],
    },
    {
      id: 'bekanntgabe-ins-ausland',
      heading: '6. Prüfen Sie Bekanntgaben ins Ausland',
      paragraphs: [
        'Daten dürfen in Staaten gehen, die der Bundesrat in Anhang 1 der DSV als angemessen anerkennt: die EU- und EWR-Staaten, das Vereinigte Königreich und seit dem 15. September 2024 US-Unternehmen, die am Swiss-US Data Privacy Framework teilnehmen. Für andere Staaten braucht es Garantien wie vom EDÖB anerkannte Standarddatenschutzklauseln (Art. 16) oder eine Ausnahme wie die ausdrückliche Einwilligung oder die Abwicklung eines Vertrags (Art. 17). Nennen Sie die Staaten in Ihrer Datenschutzerklärung.',
      ],
    },
    {
      id: 'rechte-der-betroffenen',
      heading: '7. Seien Sie bereit für Auskunft, Herausgabe und Löschung',
      paragraphs: [
        'Jede Person kann Auskunft verlangen, ob Daten über sie bearbeitet werden (Art. 25). Die Auskunft umfasst die Daten selbst, den Zweck, die Aufbewahrungsdauer, die Empfänger und die Herkunft. Sie ist in der Regel innert 30 Tagen und unentgeltlich zu erteilen; bei unverhältnismässigem Aufwand ist eine vorher mitgeteilte Beteiligung von höchstens 300 Franken möglich (Art. 18 und 19 DSV). Prüfen Sie zuerst die Identität und suchen Sie dann überall: CRM, Tabellen, Postfächer, Buchhaltung, Papier.',
        'Betroffene können zudem die Herausgabe der Daten, die sie Ihnen gegeben haben, in einem gängigen elektronischen Format verlangen (Art. 28), unrichtige Daten berichtigen lassen sowie der Bearbeitung widersprechen oder die Löschung verlangen; weiterbearbeiten dürfen Sie dann nur mit Rechtfertigungsgrund (Art. 30 bis 32).',
      ],
    },
    {
      id: 'aufbewahrung-und-loeschung',
      heading: '8. Legen Sie Aufbewahrungsfristen fest und löschen Sie rechtzeitig',
      paragraphs: [
        'Daten sind zu vernichten oder zu anonymisieren, sobald sie für den Zweck nicht mehr nötig sind (Art. 6 Abs. 4). Legen Sie für jede Kategorie eine Frist fest, zum Beispiel:',
      ],
      bullets: [
        'Geschäftsbücher und Buchungsbelege wie Rechnungen und Zahlungen: zehn Jahre (Art. 958f OR, für Vereine über Art. 69a ZGB).',
        'Nicht aufgenommene Antragstellende: nur so lange, wie der Entscheid es erfordert.',
        'Ehemalige Mitglieder: was die Buchhaltung verlangt und, mit ihrem Einverständnis, was Sie für den Kontakt brauchen.',
        'Abgemeldete Personen: nur, was sicherstellt, dass sie nichts mehr erhalten.',
      ],
    },
    {
      id: 'datenpannen',
      heading: '9. Wissen, was bei einer Datenpanne zu tun ist',
      paragraphs: [
        'Ein gestohlener Laptop mit der Mitgliederliste, ein gehacktes Postfach oder eine Tabelle an die falsche Adresse sind Verletzungen der Datensicherheit. Führt eine solche voraussichtlich zu einem hohen Risiko für die Betroffenen, melden Sie sie dem EDÖB so rasch als möglich über sein Online-Meldeformular, mit Art der Verletzung, Folgen und ergriffenen Massnahmen (Art. 24); warten Sie nicht auf lange Abklärungen, Angaben können nachgereicht werden. Auftragsbearbeiter müssen Ihnen jede Verletzung so rasch als möglich melden.',
        'Informieren Sie die Betroffenen, wenn es zu ihrem Schutz nötig ist oder der EDÖB es verlangt. Dokumentieren Sie die Verletzung, ihre Auswirkungen und Ihre Massnahmen, und bewahren Sie die Dokumentation mindestens zwei Jahre auf (Art. 15 DSV).',
      ],
    },
    {
      id: 'werbe-e-mails-und-einwilligung',
      heading: '10. Werbe-E-Mails nur mit Einwilligung',
      paragraphs: [
        'Für Werbe-E-Mails und SMS gilt das Gesetz gegen den unlauteren Wettbewerb (Art. 3 Abs. 1 Bst. o UWG): Es braucht die vorgängige Einwilligung, den korrekten Absender und einen Hinweis auf eine einfache, unentgeltliche Ablehnungsmöglichkeit. Die Ausnahme: Wer beim Verkauf die Kontaktdaten einer Kundin erhalten und auf die Ablehnungsmöglichkeit hingewiesen hat, darf ihr Werbung für eigene ähnliche Waren oder Dienstleistungen schicken.',
        'Einladungen zur Generalversammlung oder der Monatsauszug eines Mitglieds sind keine Werbung; Aktionen und Newsletter an Nichtmitglieder schon. Halten Sie fest, wann und wie jemand eingewilligt hat, und beachten Sie eine Abmeldung in allen Listen.',
      ],
    },
    {
      id: 'wie-ein-crm-hilft',
      heading: 'Wie ein CRM beim nDSG hilft',
      paragraphs: [
        'Die meisten Probleme entstehen durch verstreute Kopien: dasselbe Mitglied in vier Tabellen, ein alter Export im Postfach, niemand weiss, wer Zugriff hat. Unser Ratgeber [von Excel zum CRM](page:guide-excel-to-crm) zeigt, wie Sie alles zusammenführen.',
        'In CadenceE begrenzen Rollen und Berechtigungen, wer was sieht, ein Audit-Log hält jede Änderung fest, die Anmeldung unterstützt Passkeys und Zwei-Faktor-Authentifizierung, die Einwilligung wird bei jeder Person erfasst, mit Abmeldung per Klick, und Auskunfts- und Löschbegehren exportieren oder löschen die Daten einer Person nach DSG und DSGVO. Mehr zum [Schweizer CRM](page:swiss) und zur [Mitgliederverwaltung](page:membership), oder [probieren Sie die Demo aus](/login?demo=1). Zum Kontakt mit Ehemaligen siehe unseren Ratgeber zur [Mitgliederbindung](page:guide-member-retention).',
      ],
    },
  ],
  faq: {
    heading: 'nDSG für Vereine und Clubs: häufige Fragen',
    items: [
      {
        q: 'Gilt das nDSG auch für kleine Vereine und Clubs?',
        a: 'Ja. Es gilt für jede private Person und Organisation, die Personendaten bearbeitet, unabhängig von Grösse und Rechtsform. Die Grösse spielt nur bei wenigen Pflichten eine Rolle, etwa beim Verzeichnis der Bearbeitungstätigkeiten.',
      },
      {
        q: 'Braucht unser Verein ein Verzeichnis der Bearbeitungstätigkeiten?',
        a: 'Meist nicht, wenn er am 1. Januar weniger als 250 Mitarbeitende beschäftigt und weder besonders schützenswerte Daten in grossem Umfang bearbeitet noch ein Profiling mit hohem Risiko durchführt (Art. 24 DSV). Mitglieder und Freiwillige sind keine Mitarbeitenden.',
      },
      {
        q: 'Braucht es für Mitgliederdaten eine Einwilligung?',
        a: 'Nicht für die Verwaltung der Mitgliedschaft, sofern Ihre Datenschutzerklärung darüber informiert. Eine Einwilligung braucht es für Werbe-E-Mails, und sie muss ausdrücklich sein, wenn Sie sich bei Gesundheitsdaten oder anderen besonders schützenswerten Daten darauf stützen.',
      },
      {
        q: 'Wann muss eine Datenpanne dem EDÖB gemeldet werden?',
        a: 'So rasch als möglich, wenn sie voraussichtlich zu einem hohen Risiko für die Betroffenen führt. Darunter besteht keine Meldepflicht, eine kurze Notiz zum Vorfall und zu Ihrer Einschätzung ist aber gute Praxis.',
      },
      {
        q: 'Braucht ein Verein einen Datenschutzberater?',
        a: 'Nein. Private Verantwortliche können eine Datenschutzberaterin oder einen Datenschutzberater ernennen, müssen aber nicht (Art. 10). Eine Person, die Begehren bearbeitet und diese Checkliste aktuell hält, ist trotzdem sinnvoll.',
      },
    ],
  },
  cta: {
    heading: 'Mitgliederdaten sauber im Griff',
    text: 'CadenceE hält Mitglieder- und Kundendaten an einem Ort, mit Rollen und Berechtigungen, Audit-Log, erfasster Einwilligung samt Abmeldung per Klick und Auskunfts- und Löschbegehren, die die Daten einer Person exportieren oder löschen.',
  },
};

export default guide;
