import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Mitgliederverwaltung',
  h1: 'Mitgliederbindung: Mitgliedschaften verlängern und Kündigungen im Verein reduzieren',
  lead: 'Mitglieder gehen selten wegen eines schlechten Abends. Sie entfernen sich über Monate, und der Verlängerungstermin macht es nur offiziell. Dieser Ratgeber zeigt, wie Sie die Mitgliederbindung messen, Abwanderung früh erkennen, Verlängerungen ohne Hinterherlaufen organisieren, das Bezahlen vereinfachen und ehemalige Mitglieder zurückgewinnen, mit einem 90-Tage-Plan für den Start.',
  readingMinutes: 8,
  keyTakeaways: [
    'Ob ein Mitglied verlängert, entscheidet sich Monate vor dem Termin: daran, ob es den Club noch nutzt.',
    'Messen Sie die Verlängerungsquote nach Mitgliedschaftsart und Eintrittsjahr, nicht nur den Mitgliederbestand.',
    'Besuche, Konsumationen und Teilnahme sind die Frühwarnzeichen: Ein Rückgang verdient einen Anruf.',
    'Beginnen Sie jede Verlängerung 90 Tage im Voraus, mit einem persönlichen Gespräch für gefährdete Mitglieder.',
    'Machen Sie das Bezahlen einfach: ein klarer Auszug, QR-Rechnung, TWINT oder Karte, bei Bedarf in Raten.',
  ],
  sections: [
    {
      id: 'warum-mitglieder-kuendigen',
      heading: 'Warum Mitglieder kündigen',
      paragraphs: [
        'Fragt man den Vorstand, warum Mitglieder kündigen, heisst es meist: der Preis. Fragt man die Ausgetretenen, klingt es anders: Sie kamen nicht mehr, und dann lohnte sich der Beitrag nicht mehr. Der Preis ist der genannte Grund, die fehlende Nutzung die Ursache. Die häufigsten Ursachen:',
      ],
      bullets: [
        'Sie sind nie richtig angekommen: keine Kontakte, keine Gewohnheiten im ersten Jahr.',
        'Das Leben hat sich verändert: Umzug, neue Stelle, Kinder, Trennung, Pensionierung.',
        'Der Club hat sich verändert: anderes Publikum, neuer Koch, weniger von den Anlässen, für die sie kamen.',
        'Ein Problem blieb liegen: eine Reklamation, ein Fehler in der Abrechnung.',
        'Reibung bei der Verlängerung: Rechnung an die alte Adresse, abgelaufene Karte, Erinnerung zu spät.',
      ],
      tip: 'Führen Sie mit jedem austretenden Mitglied ein kurzes Austrittsgespräch und ordnen Sie den Grund einer von wenigen festen Kategorien zu. Nach einem Jahr wissen Sie, welche Ursachen Sie selbst beheben können.',
    },
    {
      id: 'mitgliederbindung-messen',
      heading: 'Mitgliederbindung und Abwanderung messen',
      paragraphs: ['Der Mitgliederbestand verdeckt das Wesentliche: Ein Club kann wachsen und trotzdem jedes Jahr ein Fünftel seiner Mitglieder verlieren. Messen Sie deshalb die Verlängerungen.'],
      subsections: [
        {
          heading: 'Die Verlängerungsquote',
          paragraphs: [
            'Teilen Sie die Zahl der Mitglieder, die verlängert haben, durch die Zahl der Mitgliedschaften, die im Zeitraum fällig waren. Waren 200 fällig und haben 176 verlängert, beträgt die Verlängerungsquote 88 % und die Abwanderung 12 %. Mitglieder, die im Zeitraum eingetreten sind, zählen nicht mit.',
          ],
        },
        {
          heading: 'Aufschlüsseln',
          paragraphs: ['Eine Gesamtzahl sagt wenig. Schlüsseln Sie sie auf, damit Sie sehen, wo Mitglieder verloren gehen:'],
          bullets: [
            'Nach Eintrittsjahr: Die erste Verlängerung ist die heikelste.',
            'Nach Mitgliedschaftsart: Einzel, Paar oder Familie, Firma, Junioren, auswärtige Mitglieder.',
            'Nach Beiträgen: welche Beiträge erhalten bleiben, nicht nur welche Personen.',
            'Nach Austrittsgrund, aus den Austrittsgesprächen.',
          ],
        },
      ],
    },
    {
      id: 'gefaehrdete-mitglieder-erkennen',
      heading: 'Gefährdete Mitglieder früh erkennen',
      paragraphs: ['Die meisten Austritte kündigen sich Monate vorher an, in Daten, die der Club bereits hat:'],
      bullets: [
        'Besuche: weniger Check-ins als im eigenen Rhythmus des Mitglieds, oder seit 60 bis 90 Tagen kein Besuch.',
        'Konsumationen: Bar- und Restaurantrechnungen, die schrumpfen, ein Hauskonto, das einschläft.',
        'Teilnahme: keine Anmeldungen zu Anlässen mehr, der Partner oder die Partnerin kommt nicht mehr.',
        'Service: eine offene Reklamation, ein unbezahlter Auszug, eine neue Adresse oder ein neuer Arbeitgeber.',
      ],
      subsections: [
        {
          heading: 'Von Signalen zur Anrufliste',
          paragraphs: [
            'Vergleichen Sie jedes Mitglied mit seiner eigenen Geschichte statt mit dem Durchschnitt: Wer wöchentlich kam und jetzt monatlich kommt, ist stärker gefährdet als jemand, der immer monatlich kam. Vergeben Sie pro Signal einen Punkt und gehen Sie jeden Monat die höchsten Werte durch, die nächsten Termine zuerst.',
            'In einer [Mitgliederverwaltung](page:membership) wie CadenceE trägt jede Erneuerung eine Risikobewertung und steht neben Besuchen, Konsumationen und Haushalt des Mitglieds: Die Liste ist bereit, wenn Sie sie brauchen.',
          ],
        },
      ],
    },
    {
      id: 'verlaengerungskalender',
      heading: 'Der Verlängerungskalender und die Erinnerungen',
      paragraphs: [
        'Eine Verlängerung sollte niemanden überraschen. Ob alle Mitgliedschaften am 1. Januar verlängert werden oder jede an ihrem Jahrestag, der Rhythmus bleibt gleich. Schreiben Sie die Nachrichten einmal und lassen Sie die [E-Mail-Automatisierung](page:email) sie rechtzeitig verschicken, in der Sprache jedes Mitglieds.',
      ],
      steps: [
        { title: '90 Tage vorher', text: 'Fällige Mitglieder nach Risiko sortieren und die gefährdeten anrufen, bevor eine Nachricht hinausgeht.' },
        { title: '60 Tage vorher', text: 'Eine persönliche Verlängerungsnachricht: was die Mitgliedschaft nächstes Jahr bietet, die kommenden Anlässe, was dem Mitglied dieses Jahr gefallen hat.' },
        { title: '30 Tage vorher', text: 'Rechnung oder Auszug mit QR-Rechnung und den weiteren Zahlungsmöglichkeiten.' },
        { title: '14 Tage vorher', text: 'Eine kurze, freundliche Erinnerung an alle, die noch nicht bezahlt haben.' },
        { title: 'Am Stichtag', text: 'Den Verlängerten danken. Geschäftsführung oder Vorstand rufen die übrigen an.' },
        { title: '30 Tage danach', text: 'Ergebnis und Grund festhalten. Ausgetretene kommen auf die Liste zur Rückgewinnung.' },
      ],
      tip: 'Im Verein können Mitglieder von Gesetzes wegen mit einer Frist von sechs Monaten auf Ende Jahr austreten (siehe Fragen unten). Führen Sie die Gespräche deshalb vor Beginn dieser Frist.',
    },
    {
      id: 'das-verlaengerungsgespraech',
      heading: 'Das Verlängerungsgespräch',
      paragraphs: [
        'Bei gefährdeten Mitgliedern wirkt ein Anruf oder ein Kaffee viel besser als die dritte E-Mail. Er sollte von jemandem kommen, der das Mitglied kennt, und mit Zuhören beginnen, nicht mit der Rechnung. Schauen Sie vorher in den Verlauf: letzter Besuch, gebuchte Anlässe, Haushalt. Fragen Sie dann:',
      ],
      bullets: [
        'Wie war Ihr Jahr im Club?',
        'Was würde Sie öfter herbringen?',
        'Hat sich bei Ihnen oder Ihrer Familie etwas verändert?',
        'Gibt es etwas, das wir falsch gemacht haben?',
      ],
      subsections: [
        {
          heading: 'Auf häufige Einwände antworten',
          paragraphs: [
            '«Wir kommen zu wenig» ist eine Einladung: Schlagen Sie passende Anlässe vor oder eine Mitgliedschaftsart, die zur heutigen Nutzung passt. «Es ist zu teuer» meint oft dasselbe. Halten Sie fest, was vereinbart wurde, damit die nächste Person im Team es weiss.',
          ],
        },
      ],
    },
    {
      id: 'zahlung-vereinfachen',
      heading: 'Reibung beim Bezahlen beseitigen',
      paragraphs: [
        'Viele nicht verlängerte Mitgliedschaften sind gar keine Entscheidung: Die Rechnung ging unter, die Karte lief ab, die Zahlung wurde verschoben und vergessen. Jeder Schritt weniger hält Mitglieder.',
      ],
      bullets: [
        'Ein klarer Auszug statt separater Rechnungen für Beitrag, Konsumationen und Anlässe.',
        'Die Zahlungsmittel, die man in der Schweiz nutzt: QR-Rechnung für die Banking-App, TWINT und Karte.',
        'Wiederkehrende Beiträge monatlich oder quartalsweise, damit die Verlängerung kein grosser Jahresschock ist.',
        'Die richtige Rechnungsadresse: die zahlende Person im Haushalt oder die Buchhaltung der Firma.',
        'Ein gut sichtbares Fälligkeitsdatum und Erinnerungen im Ton des Clubs, nicht im Ton eines Inkassobüros.',
      ],
      tip: 'Mehr dazu in unserem Ratgeber zur [QR-Rechnung im Verein](page:guide-qr-bill). In CadenceE landen Beiträge, Konsumationen und Anlässe auf einem Monatsauszug, bezahlt per Karte, TWINT, SEPA oder QR-Rechnung: siehe [Abrechnung und Zahlungen](page:billing).',
    },
    {
      id: 'haushalte-und-firmenmitgliedschaften',
      heading: 'Haushalte und Firmenmitgliedschaften',
      paragraphs: [
        'Im Haushalt ist die zahlende Person nicht immer die, die den Club nutzt. Kommt nur ein Partner, stellt der andere den Beitrag irgendwann in Frage: Laden Sie beide ein und sprechen Sie bei der Verlängerung mit beiden. Achten Sie auf den Übertritt der Kinder von den Junioren zu den Erwachsenen, einen häufigen Austrittsmoment.',
        'Eine Firmenmitgliedschaft verlängert jemand, der selten vorbeikommt, im HR oder in der Finanzabteilung. Schicken Sie dieser Person vor der Budgetplanung eine kurze Übersicht, wie ihre Leute den Club genutzt haben. Verlässt jemand die Firma, bieten Sie an, den Platz zu übertragen oder in eine persönliche Mitgliedschaft umzuwandeln. Beides ist einfacher, wenn [Haushalte und Firmenmitgliedschaften](page:private-clubs) als solche erfasst sind.',
      ],
    },
    {
      id: 'anlaesse-und-kommunikation',
      heading: 'Anlässe und Kommunikation, die Mitglieder halten',
      paragraphs: [
        'Mitgliederbindung beginnt am ersten Tag. Wer in den ersten drei Monaten Leute kennengelernt und einige Anlässe besucht hat, bleibt mit grosser Wahrscheinlichkeit. Das Ankommen beginnt mit dem [Aufnahmeprozess](page:guide-membership-applications) und geht weiter mit einem Willkommensabend, Vorstellungen und einem Anruf nach dem ersten Monat.',
        'Danach zählt ein regelmässiger Rhythmus kleiner Formate (eine Degustation, ein Vortrag, ein Mitgliedertisch) mehr als wenige grosse Anlässe. In der Kommunikation geht Relevanz vor Häufigkeit: Schreiben Sie in der Sprache des Mitglieds, laden Sie nach Interessen ein und schicken Sie Werbung nur an Personen, die eingewilligt haben. Mehr im Ratgeber [Privatclub führen](page:guide-run-a-club).',
      ],
    },
    {
      id: 'ehemalige-zurueckgewinnen',
      heading: 'Ehemalige Mitglieder zurückgewinnen',
      paragraphs: [
        'Nicht jeder Austritt ist endgültig: Man kehrt aus dem Ausland zurück, die Kinder werden grösser, die neue Stelle ist eingespielt. Schliessen Sie die Tür sanft: danken, den Grund festhalten und fragen, ob Sie in Kontakt bleiben dürfen.',
      ],
      bullets: [
        'Nach drei bis sechs Monaten: wer aus praktischen Gründen ging, zu einem offenen Anlass einladen.',
        'Nach einem Jahr: erzählen, was sich im Club verändert hat, besonders beim Austrittsgrund.',
        'Die Rückkehr einfach machen: kein komplett neues Aufnahmeverfahren für Ehemalige ohne offene Beträge.',
      ],
      tip: 'Bewahren Sie über Ehemalige nur auf, was Sie brauchen, und schicken Sie Werbe-E-Mails nur mit Einwilligung. Unsere [nDSG-Checkliste für Vereine](page:guide-fadp-checklist) erklärt die Regeln.',
    },
    {
      id: '90-tage-plan',
      heading: 'Ein 90-Tage-Plan für bessere Mitgliederbindung',
      paragraphs: ['Drei Monate genügen, um die Grundlagen vor der nächsten Verlängerungsrunde zu legen.'],
      steps: [
        { title: 'Tag 1 bis 30: die Fakten', text: 'Mitglieder, Termine, Besuche und Zahlungen an einem Ort zusammenführen (siehe [von Excel zum CRM](page:guide-excel-to-crm)). Die Verlängerungsquote des Vorjahrs berechnen und alle Fälligkeiten der nächsten sechs Monate auflisten.' },
        { title: 'Tag 31 bis 60: die Routine', text: 'Risikosignale festlegen, die Mitglieder mit den höchsten Werten anrufen, die Erinnerungen schreiben und prüfen, dass alle per QR-Rechnung, TWINT oder Karte zahlen können.' },
        { title: 'Tag 61 bis 90: umsetzen und auswerten', text: 'Die ersten Verlängerungen nach dem neuen Kalender führen, Austrittsgespräche halten, erste Einladungen an Ehemalige verschicken und mit dem Vorjahr vergleichen.' },
      ],
      tip: 'Um Erneuerungen mit Risikobewertung, Besuche und Auszüge nebeneinander zu sehen, [probieren Sie die Demo aus](/login?demo=1).',
    },
  ],
  faq: {
    heading: 'Mitgliederbindung und Verlängerung: häufige Fragen',
    items: [
      {
        q: 'Was ist eine gute Verlängerungsquote für einen Club oder Verein?',
        a: 'Einen allgemeingültigen Richtwert gibt es nicht: Er hängt von der Art des Clubs, vom Beitrag und von der Warteliste ab. Vergleichen Sie mit Ihren eigenen Vorjahren, nach Mitgliedschaftsart und Eintrittsjahr, und betrachten Sie Mitglieder im ersten Jahr separat.',
      },
      {
        q: 'Wie berechnet man die Abwanderung von Mitgliedern?',
        a: 'Teilen Sie die Zahl der Mitglieder, die nicht verlängert haben, durch die Zahl der im Zeitraum fälligen Mitgliedschaften. Waren 200 fällig und haben 24 nicht verlängert, liegt die Abwanderung bei 12 %.',
      },
      {
        q: 'Wann sollte man an die Verlängerung der Mitgliedschaft erinnern?',
        a: 'Eine persönliche Nachricht rund 60 Tage vor dem Termin, die Rechnung 30 Tage vorher, eine freundliche Erinnerung 14 Tage vorher und kurz nach dem Termin ein Anruf bei allen, die nicht bezahlt haben. Gefährdete Mitglieder verdienen 90 Tage vorher ein Gespräch.',
      },
      {
        q: 'Wie reduziert man Kündigungen im Verein?',
        a: 'Neue Mitglieder im ersten Jahr gut integrieren, Besuche, Konsumationen und Teilnahme beobachten, mit gefährdeten Mitgliedern vor dem Termin sprechen, das Bezahlen vereinfachen und jeden Austritt nach dem Grund fragen.',
      },
      {
        q: 'Mit welcher Frist können Mitglieder aus dem Verein austreten?',
        a: 'Nach Art. 70 Abs. 2 ZGB ist der Austritt von Gesetzes wegen zulässig, wenn er mit einer Frist von sechs Monaten auf das Ende des Kalenderjahres oder, wenn eine Verwaltungsperiode vorgesehen ist, auf deren Ende erklärt wird. Prüfen Sie Ihre Statuten und sprechen Sie mit gefährdeten Mitgliedern, bevor diese Frist beginnt.',
      },
    ],
  },
  cta: {
    heading: 'Gefährdete Verlängerungen sehen, bevor daraus Kündigungen werden',
    text: 'CadenceE zeigt jede Erneuerung mit Risikobewertung neben Besuchen, Konsumationen und Haushalt des Mitglieds und verschickt Erinnerungen und Auszüge von einem Ort aus, damit Ihr Team rechtzeitig die richtigen Mitglieder anruft.',
  },
};

export default guide;
