import type { GuideContent } from '../types';

// Nachfass-E-Mails im Vertrieb: wann nachfassen, was schreiben, Vorlagen, wann aufhören, die
// Schweizer Regeln für Werbe-E-Mails (Art. 3 Abs. 1 lit. o UWG, DSG) und das Nachfassen im CRM.
const guide: GuideContent = {
  eyebrow: 'Vertrieb',
  h1: 'Nachfass-E-Mails im Vertrieb: so bekommen Sie eine Antwort',
  lead: 'Eine erste E-Mail bleibt oft unbeantwortet, aus Gründen, die nichts mit Ihnen zu tun haben. Hier lesen Sie, wann Sie nachfassen, was Sie schreiben, sechs Vorlagen zum Anpassen, wann Sie aufhören, was in der Schweiz für Einwilligung und Abmeldung gilt und wie ein CRM den Überblick behält.',
  readingMinutes: 9,
  keyTakeaways: [
    'Schweigen ist selten ein Nein: Planen Sie drei bis fünf Nachfass-E-Mails, bevor Sie einen neuen Lead aufgeben.',
    'Fassen Sie nach einem Termin am selben Tag nach, nach einer Offerte zwei bis drei Arbeitstage später.',
    'Jede Nachricht bringt etwas Neues und bittet um einen einzigen, einfachen nächsten Schritt.',
    'Ohne Antwort nach vier bis fünf Wochen folgt eine höfliche Abschluss-E-Mail, dann ist Schluss.',
    'Werbe-E-Mails an eine Liste brauchen eine vorgängige Einwilligung; persönliches Nachfassen endet, sobald die Person es wünscht.',
  ],
  sections: [
    {
      id: 'warum-nachfassen',
      heading: 'Warum die meisten Abschlüsse mehr als eine E-Mail brauchen',
      paragraphs: [
        'Ihr Kontakt hat Ihre E-Mail zwischen zwei Sitzungen gelesen und vergessen zu antworten, oder er wartet auf das Budget des nächsten Quartals. Schweigen heisst meistens: falscher Zeitpunkt, keine Absage.',
        'Über eine Mitgliedschaft, einen Privatanlass oder einen Dienstleistungsvertrag entscheiden mehrere Personen über mehrere Wochen. Wer hilfsbereit und ohne Druck präsent bleibt, ist noch da, wenn entschieden wird; viele Geschäfte gehen verloren, nur weil niemand nochmals geschrieben hat. Die Gründe für das Schweigen sind meist banal:',
      ],
      bullets: [
        'Die E-Mail kam im falschen Moment.',
        'Der Entscheid hängt von jemand anderem oder vom Budget ab.',
        'Der nächste Schritt war unklar oder schien aufwendig.',
      ],
    },
    {
      id: 'wann-nachfassen',
      heading: 'Wann Sie eine Nachfass-E-Mail senden: Timing und Rhythmus',
      paragraphs: [
        'Fassen Sie rasch nach, solange das Gespräch frisch ist, und lassen Sie die Abstände dann grösser werden. Rechnen Sie in Arbeitstagen und denken Sie an die Ferien: Die Schulferien sind von Kanton zu Kanton verschieden, und zwischen Weihnachten und Anfang Januar ist es fast überall ruhig.',
      ],
      subsections: [
        {
          heading: 'Nach dem Erstkontakt',
          paragraphs: [
            'Bleibt Ihre erste E-Mail unbeantwortet, passt für die meisten Clubs, Betriebe und B2B-Teams ein Rhythmus über fünf Wochen: eine kurze Erinnerung an Tag 3, etwas Nützliches an Tag 7 bis 10, ein anderer Ansatz an Tag 17 bis 21 und eine Abschluss-E-Mail um Tag 30. Hat der Lead Sie kontaktiert, antworten Sie noch am selben Tag.',
          ],
        },
        {
          heading: 'Nach einem Termin',
          paragraphs: [
            'Senden Sie die Follow-up-E-Mail nach einem Termin am selben Tag, spätestens am nächsten Morgen: was Sie verstanden haben, wer was macht und den nächsten Schritt mit Datum. Diese E-Mail darf nie fehlen.',
          ],
        },
        {
          heading: 'Angebot nachfassen: nach Offerte oder Vertrag',
          paragraphs: [
            'Zwei bis drei Arbeitstage nach dem Versand der Offerte fragen Sie, ob sie angekommen ist, und bieten an, sie gemeinsam durchzugehen. Wartet ein Vertrag auf die [elektronische Unterschrift](page:e-signature), ist eine Erinnerung vor einer echten Frist ein Service, kein Druck.',
          ],
        },
        {
          heading: 'Nach längerem Schweigen',
          paragraphs: [
            'Wird ein interessierter Lead still, werden Sie langsamer: eine Nachricht nach einer Woche, eine weitere nach zwei bis drei Wochen, dann die Abschluss-E-Mail. Monate später schreiben Sie nur wieder, wenn sich wirklich etwas geändert hat.',
          ],
        },
      ],
      tip: 'Prüfen Sie vor jedem Nachfassen den Verlauf: Hat der Lead inzwischen einer Kollegin geantwortet oder einen Termin gebucht, streichen Sie die geplante Nachricht.',
    },
    {
      id: 'was-schreiben',
      heading: 'Was hineingehört: Betreff, Mehrwert, ein nächster Schritt',
      paragraphs: [
        'Eine gute Nachfass-E-Mail hat drei Teile: den Zusammenhang in einem Satz, etwas Neues und Nützliches und einen einzigen nächsten Schritt.',
      ],
      subsections: [
        {
          heading: 'Betreffzeilen, die geöffnet werden',
          paragraphs: [
            'Kurz und konkret: «Ihr Besuch am Donnerstag: nächste Schritte», «Offerte für Ihren Sommeranlass». Fassen Sie im selben Verlauf nach, und täuschen Sie bei einer ersten E-Mail nie ein «AW:» vor.',
          ],
        },
        {
          heading: 'In jeder Nachricht etwas Neues',
          paragraphs: [
            'Eine Antwort auf eine Frage aus dem Termin, ein Beispiel eines ähnlichen Kunden, ein reserviertes Datum, eine einfachere Variante. «Ich wollte nur kurz nachhaken» bringt dem Leser nichts: Ohne Neuigkeit warten Sie besser.',
          ],
        },
        {
          heading: 'Ein klarer nächster Schritt',
          paragraphs: [
            '«Passt Ihnen Dienstag um 10 Uhr oder Mittwoch um 14 Uhr für ein Gespräch von 20 Minuten?» wirkt besser als «Bei Fragen stehe ich gerne zur Verfügung.» Eine Frage, höchstens ein Link.',
          ],
        },
        {
          heading: 'Der Schweizer Ton',
          paragraphs: [
            'Bleiben Sie beim Sie, bis Ihr Gegenüber das Du anbietet, nennen Sie genaue Daten und Zeiten und verzichten Sie auf Druck und erfundene Dringlichkeit. «Freundliche Grüsse» ist in der Schweiz der übliche Gruss.',
          ],
        },
      ],
    },
    {
      id: 'vorlagen',
      heading: 'Sechs Follow-up-E-Mail-Vorlagen zum Anpassen',
      paragraphs: [
        'Ersetzen Sie Namen, Daten und Details durch Ihre eigenen und behalten Sie den Aufbau: Zusammenhang, etwas Nützliches, ein nächster Schritt.',
      ],
      subsections: [
        {
          heading: '1. Nach einem Erstkontakt ohne Antwort',
          paragraphs: [
            'Betreff: AW: Sommeranlass Ihres Teams',
            'Guten Tag Frau Keller, ich habe Ihnen letzte Woche wegen des Sommeranlasses Ihres Teams geschrieben. Die Daten im Juni sind schnell vergeben: Darf ich Ihnen drei mögliche Daten und ein Beispielmenü schicken? Freundliche Grüsse, Laura Meier',
          ],
        },
        {
          heading: '2. Die Follow-up-E-Mail nach einem Termin',
          paragraphs: [
            'Betreff: Danke für das Gespräch: Zusammenfassung und nächste Schritte',
            'Guten Tag Herr Rochat, vielen Dank für Ihre Zeit heute. Wie ich es verstanden habe, planen Sie Ende September ein Abendessen für 80 Personen. Wie besprochen sende ich Ihnen bis Freitag eine Offerte mit zwei Raumvarianten, und Sie bestätigen die Teilnehmerzahl mit Ihrer Geschäftsleitung. Können wir am Dienstag um 10 Uhr nochmals sprechen? Freundliche Grüsse, Laura Meier',
          ],
        },
        {
          heading: '3. Angebot nachfassen: nach dem Versand der Offerte',
          paragraphs: [
            'Betreff: Ihre Offerte für das Abendessen im September',
            'Guten Tag Herr Rochat, ist unsere Offerte gut bei Ihnen angekommen, und entspricht sie Ihren Vorstellungen? Gerne gehe ich sie in 15 Minuten telefonisch mit Ihnen durch, am Donnerstagmorgen oder Freitagnachmittag. Freundliche Grüsse, Laura Meier',
          ],
        },
        {
          heading: '4. Wenn ein Vertrag auf die Unterschrift wartet',
          paragraphs: [
            'Betreff: Ihr Mitgliedschaftsvertrag: noch ein Schritt',
            'Guten Tag Frau Favre, Ihr Vertrag ist über den Link vom Montag bereit zur Unterschrift, und die Eintrittsanzahlung begleichen Sie im selben Schritt. Ist etwas unklar, antworten Sie einfach auf diese E-Mail: Ich melde mich noch heute. Freundliche Grüsse, Marc Bühler',
          ],
        },
        {
          heading: '5. Mehrwert nach längerem Schweigen',
          paragraphs: [
            'Betreff: Drei Ideen für Ihren Kundenanlass',
            'Guten Tag Herr Steiner, im Anhang finden Sie drei Formate von Kundenanlässen, die wir für Unternehmen Ihrer Grösse durchgeführt haben. Kommt eines davon Ihrer Idee nahe, passe ich es gerne für Sie an. Freundliche Grüsse, Laura Meier',
          ],
        },
        {
          heading: '6. Die Abschluss-E-Mail',
          paragraphs: [
            'Betreff: Soll ich Ihre Anfrage schliessen?',
            'Guten Tag Herr Steiner, da ich nichts von Ihnen gehört habe, nehme ich an, dass der Zeitpunkt nicht passt. Dies ist deshalb meine letzte Nachricht zu Ihrem Kundenanlass. Ändert sich etwas, antworten Sie einfach auf diese E-Mail: Ich nehme den Faden gerne wieder auf. Freundliche Grüsse, Laura Meier',
          ],
        },
      ],
    },
    {
      id: 'wann-aufhoeren',
      heading: 'Wann aufhören: die Abschluss-E-Mail und danach',
      paragraphs: [
        'Für einen neuen Lead sind drei bis fünf Nachfass-E-Mails über vier bis sechs Wochen eine vernünftige Grenze; darüber hinaus schaden Sie Ihrem Ruf und dem Ihrer Absenderadresse. Die Abschluss-E-Mail gibt einer beschäftigten Person eine letzte, einfache Gelegenheit zu antworten und bringt oft ein nützliches «Jetzt nicht, melden Sie sich im Herbst». Hören Sie sofort auf, wenn:',
      ],
      bullets: [
        'die Person darum bittet oder sich abmeldet;',
        'sie absagt oder jemand anderen als zuständig nennt;',
        'die E-Mail unzustellbar ist oder die Person nicht mehr dort arbeitet;',
        'sie antwortet, einen Termin bucht oder unterschreibt.',
      ],
      tip: 'Halten Sie fest, warum ein Lead geschlossen wurde («dieses Jahr kein Budget»): In sechs Monaten zeigt Ihnen diese Notiz, ob sich ein neuer Anlauf lohnt.',
    },
    {
      id: 'einwilligung-und-abmeldung',
      heading: 'Einwilligung und Abmeldung: was in der Schweiz gilt',
      paragraphs: [
        'Eine persönliche Nachricht an jemanden, den Sie getroffen haben, zu einer Offerte, die er verlangt hat, ist geschäftliche Korrespondenz. Ein Newsletter oder derselbe Werbetext an eine Liste ist Marketing und fällt in der Schweiz unter das Bundesgesetz gegen den unlauteren Wettbewerb (UWG).',
        'Nach Art. 3 Abs. 1 lit. o UWG handelt unlauter, wer Massenwerbung per E-Mail ohne vorgängige Einwilligung der Empfänger verschickt, den Absender nicht korrekt angibt oder nicht auf eine problemlose Ablehnungsmöglichkeit ohne Kosten hinweist. Ausnahme: Kunden, deren Adresse Sie beim Verkauf erhalten und die Sie auf die Ablehnungsmöglichkeit hingewiesen haben, dürfen Werbung für Ihre eigenen ähnlichen Produkte erhalten. Geschäftsadressen sind nicht ausgenommen.',
        'Wo persönliches Nachfassen endet und Massenwerbung beginnt, legt das Gesetz nicht genau fest: Eine Nachricht für eine einzelne Person gilt in der Regel als Korrespondenz, eine Vorlage an eine gekaufte Liste nicht. Zudem kann nach dem DSG jede Person der Bearbeitung ihrer Daten für Werbung widersprechen. In der Praxis:',
      ],
      bullets: [
        'Newsletter und Kampagnen nur an Personen, die eingewilligt haben, mit festgehaltener Einwilligung.',
        'Ein klarer Absender und ein funktionierender Abmeldelink in jeder Werbe-E-Mail.',
        'Persönliches Nachfassen, das endet, sobald die Person es wünscht.',
        'Strengere Regeln für viele Empfänger in der EU: Deutschland etwa verlangt für Werbe-E-Mails grundsätzlich eine vorherige Einwilligung, auch unter Unternehmen.',
      ],
      tip: 'Allgemeine Information, keine Rechtsberatung. Zum Umgang mit Personendaten insgesamt: unsere [nDSG-Checkliste](page:guide-fadp-checklist).',
    },
    {
      id: 'nachfassen-im-crm',
      heading: 'Nachfassen im CRM verfolgen: Sequenzen und Erinnerungen',
      paragraphs: [
        'Nachfassen scheitert meist daran, dass niemand daran denkt. Eine Tabelle reicht für zwanzig Leads, nicht für zweihundert, die sich drei Personen teilen. Nutzen Sie Sequenzen für die Standardschritte und Aufgaben für das persönliche Nachfassen, und achten Sie darauf, dass eine Sequenz stoppt, sobald die Person antwortet.',
        'Im [Vertriebs-CRM](page:sales) von CadenceE steht jeder Lead in einer von fünf Phasen, von der Kontaktaufnahme bis zur Anzahlung, mit seinen Aufgaben. [Sequenzen](page:email) verschicken das Nachfassen nach Zeitplan über einen Postausgang, der jede E-Mail beim Empfänger festhält und Einwilligungen respektiert, und ein Morgen-Briefing zeigt, was heute ansteht. Noch mit Excel unterwegs? Lesen Sie [von Excel zum CRM](page:guide-excel-to-crm).',
      ],
      steps: [
        { title: 'Ein nächster Schritt für jeden Lead', text: 'Eine Phase, eine verantwortliche Person, ein Datum.' },
        { title: 'Sequenzen einmal schreiben', text: 'Aus den Vorlagen oben, in jeder Verkaufssprache.' },
        { title: 'Jede E-Mail beim Lead', text: 'Damit niemand im Team doppelt schreibt.' },
        { title: 'Nachfassen zuerst', text: 'Bevor der Posteingang den Tag übernimmt.' },
      ],
    },
    {
      id: 'ki-texter',
      heading: 'Einen KI-Texter verantwortungsvoll nutzen',
      paragraphs: [
        'KI hilft gegen das leere Blatt: ein erster Entwurf der Zusammenfassung, drei Betreffzeilen, eine kürzere Fassung. Sie weiss aber nicht, was Sie versprochen haben oder was stimmt. Der [KI-Texter](page:ai) von CadenceE entwirft Nachfass-E-Mails im Ton Ihrer Marke, mit dem Ziel und der Länge, die Sie wählen, und eine Person prüft und versendet jede Nachricht. Mit jedem Werkzeug gilt:',
      ],
      bullets: [
        'Geben Sie ihm Ihre Notizen und den gewünschten nächsten Schritt.',
        'Prüfen Sie jede Tatsache, jeden Namen, jedes Datum, jeden Preis und jedes Versprechen.',
        'Streichen Sie Floskeln, damit es nach Ihnen klingt.',
        'Lassen Sie ihn nie Kunden oder Zahlen erfinden oder unbeaufsichtigt an eine Liste schreiben.',
      ],
    },
  ],
  faq: {
    heading: 'Nachfass-E-Mails: häufige Fragen',
    items: [
      { q: 'Wie lange warte ich bis zur Nachfass-E-Mail?', a: 'Nach einem Termin schreiben Sie am selben Tag. Nach einer ersten E-Mail oder einer Offerte warten Sie zwei bis drei Arbeitstage, dann etwa eine Woche, dann zwei.' },
      { q: 'Wie oft sollte man nachfassen?', a: 'Drei- bis fünfmal über vier bis sechs Wochen, zuletzt mit einer Abschluss-E-Mail. Hören Sie sofort auf, wenn die Person darum bittet.' },
      { q: 'Was gehört in eine Follow-up-E-Mail nach einem Termin?', a: 'Ein Dank, eine Zusammenfassung des Verstandenen, wer was bis wann macht, und der nächste Schritt mit Datum.' },
      { q: 'Wie fasse ich ein Angebot nach, ohne aufdringlich zu wirken?', a: 'Fragen Sie, ob die Offerte angekommen ist, bieten Sie ein kurzes Gespräch an und bringen Sie in der nächsten Nachricht etwas Nützliches. Nennen Sie Fristen nur, wenn sie echt sind.' },
      { q: 'Brauche ich in der Schweiz eine Einwilligung zum Nachfassen?', a: 'Für persönliches Nachfassen bei jemandem, mit dem Sie in Kontakt stehen, in der Regel nicht; hören Sie aber auf, wenn die Person es verlangt. Werbe-E-Mails an eine Liste brauchen nach UWG eine vorgängige Einwilligung, ausser bei bestehenden Kunden, sowie einen klaren Absender und eine Abmeldemöglichkeit.' },
      { q: 'Darf KI meine Nachfass-E-Mails schreiben?', a: 'Sie kann sie entwerfen, aber eine Person sollte jede prüfen und selbst versenden.' },
    ],
  },
  cta: {
    heading: 'Jedes Nachfassen rechtzeitig, an einem Ort',
    text: 'CadenceE gibt jedem Lead eine Phase und einen nächsten Schritt, verschickt Ihre Nachfass-Sequenzen mit eingebauter Einwilligung und entwirft jede E-Mail mit einem KI-Texter, dessen Entwürfe Sie vor dem Versand prüfen.',
  },
};

export default guide;
