import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Aufnahme neuer Mitglieder',
  h1: 'Der Aufnahmeprozess im Verein und Club, Schritt für Schritt',
  lead: 'Wie Sie neue Mitglieder fair und zügig aufnehmen: was ein Mitgliedsantrag fragen soll und was nicht, wie Paten und Aufnahmekommission arbeiten, wie man Ja, Nein oder Noch nicht sagt, eine Warteliste führt und aus einem unterschriebenen Vertrag ein gut integriertes Mitglied macht, mit Unterlagen, die das Schweizer Datenschutzgesetz respektieren.',
  readingMinutes: 8,
  keyTakeaways: [
    'Halten Sie Ablauf und Aufnahmekriterien schriftlich fest, bevor der nächste Antrag kommt.',
    'Fragen Sie nur, was Sie für den Entscheid brauchen; besonders schützenswerte Daten gehören nicht in den Antrag.',
    'Antworten Sie allen rasch, auch denen, die Sie ablehnen oder vertrösten.',
    'Unterschrift und Eintrittsgebühr gehören in denselben Schritt.',
    'Die Integration beginnt am Tag der Aufnahme, nicht beim ersten Besuch.',
  ],
  sections: [
    {
      id: 'warum-der-prozess-zaehlt',
      heading: 'Warum der Aufnahmeprozess zählt',
      paragraphs: [
        'Für Interessierte ist der Aufnahmeprozess die erste echte Erfahrung mit Ihrem Club. Ein langsamer, undurchsichtiger Ablauf wirkt unorganisiert; ein rascher, persönlicher zeigt, dass man gut aufgehoben ist. Die besten Kandidatinnen und Kandidaten haben oft andere Möglichkeiten und warten nicht drei Monate auf eine Antwort.',
        'Ein schriftlicher Ablauf schützt auch den Club. Wenn jeder Antrag dieselben Schritte und Kriterien durchläuft, sind die Entscheide einheitlich, der Vorstand kann sie begründen, und niemand kommt hinein oder bleibt draussen, nur weil er zur richtigen Zeit die richtige Person kannte.',
      ],
    },
    {
      id: 'ablauf-im-ueberblick',
      heading: 'Der Ablauf im Überblick',
      paragraphs: ['Die meisten Clubs und Vereine folgen, unabhängig von ihrer Grösse, derselben Reihenfolge:'],
      steps: [
        { title: 'Anfrage', text: 'Jemand interessiert sich für eine Mitgliedschaft, über die Website, ein Mitglied oder einen Besuch. Antworten Sie innert eines Arbeitstags.' },
        { title: 'Kennenlernen', text: 'Ein Rundgang oder ein Gespräch mit der für Mitglieder zuständigen Person, damit beide Seiten sehen, ob es passt.' },
        { title: 'Mitgliedsantrag', text: 'Die Person füllt einen kurzen Aufnahmeantrag aus, idealerweise online.' },
        { title: 'Paten', text: 'Falls Ihre Regeln es vorsehen, unterstützen ein oder zwei Mitglieder den Antrag.' },
        { title: 'Prüfung', text: 'Vorstand oder Aufnahmekommission prüfen den Antrag nach schriftlichen Kriterien.' },
        { title: 'Entscheid', text: 'Die Person erhält ein Ja, ein Nein oder ein Noch nicht, rasch und höflich.' },
        { title: 'Vertrag und Eintrittsgebühr', text: 'Das neue Mitglied unterschreibt und bezahlt die Eintrittsgebühr oder Anzahlung.' },
        { title: 'Willkommen', text: 'Die Integration beginnt: Portalzugang, Vorstellungen und ein erster Anlass.' },
      ],
      tip: 'Setzen Sie ein Ziel für den ganzen Weg, zum Beispiel einen Entscheid innert vier Wochen nach vollständigem Antrag, und messen Sie es.',
    },
    {
      id: 'der-mitgliedsantrag',
      heading: 'Der Mitgliedsantrag: was hinein gehört und was nicht',
      paragraphs: [
        'Ein guter Aufnahmeantrag für einen Club passt auf einen Bildschirm oder eine Seite. Jedes Feld braucht einen Zweck, den Sie der antragstellenden Person erklären könnten.',
      ],
      subsections: [
        {
          heading: 'Was Sie fragen sollten',
          paragraphs: ['Diese Felder genügen den meisten Clubs:'],
          bullets: [
            'Name, E-Mail, Telefon und Postadresse.',
            'Gewünschte Korrespondenzsprache.',
            'Mitgliederkategorie und, bei einer Familienmitgliedschaft, die Haushaltsmitglieder.',
            'Beruf oder Firma, sofern sie mit dem Zweck des Clubs zu tun haben.',
            'Wer die Person empfohlen hat, und die Namen der Paten.',
            'Einige Zeilen zur Motivation und zu den Interessen.',
            'Die Bestätigung, dass die Datenschutzerklärung gelesen wurde.',
          ],
        },
        {
          heading: 'Was Sie weglassen sollten',
          paragraphs: [
            'Das Datenschutzgesetz verlangt, nur die nötigen Daten zu bearbeiten, und schützt bestimmte Daten besonders: religiöse, weltanschauliche, politische oder gewerkschaftliche Ansichten, Gesundheit, Ethnie, genetische und biometrische Daten, straf- und verwaltungsrechtliche Verfolgungen und Sanktionen sowie Massnahmen der sozialen Hilfe. Ein Aufnahmeentscheid braucht davon kaum je etwas, also gehört es nicht in den Antrag. Dasselbe gilt für Bankverbindung und Ausweiskopien, die warten können, bis sie wirklich gebraucht werden, und für Angaben zum Einkommen.',
          ],
        },
      ],
    },
    {
      id: 'paten-und-referenzen',
      heading: 'Paten und Referenzen',
      paragraphs: [
        'Viele Privatclubs verlangen zwei Paten: Mitglieder, die die Person kennen und für sie einstehen. Das funktioniert am besten leicht und strukturiert. Statt eines freien Briefs stellen Sie jedem Paten drei kurze Fragen: seit wann er die Person kennt, in welchem Zusammenhang, und warum sie sich im Club wohlfühlen und etwas beitragen wird.',
        'Halten Sie auf dem Datensatz fest, wer wen empfohlen hat. Das hilft der Aufnahmekommission und zeigt später, welche Mitglieder Personen bringen, die bleiben. Behandeln Sie die Aussagen der Paten vertraulich, und denken Sie daran, dass Antragstellende Auskunft über ihre Daten verlangen können; klären Sie mit einer Fachperson, was das für Referenzen bedeutet.',
      ],
    },
    {
      id: 'pruefung-und-entscheid',
      heading: 'Prüfung durch die Aufnahmekommission und Entscheid',
      paragraphs: [
        'Wer über die Aufnahme entscheidet, bestimmen Ihre Regeln. In einem Verein nach Zivilgesetzbuch legen die Statuten das zuständige Organ fest; in einem Club, den eine Firma betreibt, die Mitgliedschaftsbedingungen. In beiden Fällen gilt: Machen Sie das Verfahren klar.',
      ],
      bullets: [
        'Halten Sie die Aufnahmekriterien schriftlich fest und geben Sie sie allen, die Anträge prüfen.',
        'Tagen Sie in einem bekannten Rhythmus, etwa monatlich, damit Antragstellende wissen, wann sie Bescheid erhalten.',
        'Lassen Sie Mitglieder mit Interessenkonflikt, etwa Paten, beim Entscheid in den Ausstand treten.',
        'Protokollieren Sie jeden Entscheid, das Datum und wer ihn gefällt hat.',
        'Prüfen Sie nur vollständige Anträge: Fehlendes vor der Sitzung nachfordern, nicht währenddessen.',
      ],
    },
    {
      id: 'entscheid-mitteilen',
      heading: 'Ja, Nein oder Noch nicht sagen',
      paragraphs: [
        'Teilen Sie den Entscheid innert weniger Tage nach der Sitzung mit. Eine Zusage enthält alles für den nächsten Schritt: den Vertrag zum Unterschreiben, die Eintrittsgebühr und den Beginn der Mitgliedschaft. Rufen Sie wenn möglich an: Diesen Moment vergisst man nicht.',
        'Eine Absage verdient dieselbe Höflichkeit. Bleiben Sie kurz und respektvoll, danken Sie der Person und informieren Sie die Paten, bevor sie es anderswo erfahren. Ob Sie Gründe nennen, bestimmen Ihre Regeln; wie auch immer Sie entscheiden, handeln Sie einheitlich. Geht es um den Zeitpunkt oder die Kapazität und nicht um die Person, bieten Sie eine Rückstellung oder einen Platz auf der Warteliste an.',
      ],
    },
    {
      id: 'warteliste',
      heading: 'Eine faire Warteliste führen',
      paragraphs: [
        'Ist eine Kategorie voll, hält eine Warteliste gute Kandidatinnen und Kandidaten bei der Stange. Fair bleibt sie, wenn ihre Regeln schriftlich festgehalten und eingehalten werden:',
      ],
      bullets: [
        'Legen Sie die Reihenfolge fest: meist das Datum der Zusage, manchmal nach Kategorie.',
        'Sagen Sie den Wartenden ungefähr, wo sie stehen und wie oft Plätze frei werden.',
        'Melden Sie sich mindestens ein- oder zweimal pro Jahr bei allen auf der Liste.',
        'Verlangen Sie eine Anzahlung für den Platz, halten Sie schriftlich fest, wann sie zurückerstattet wird.',
      ],
    },
    {
      id: 'vertrag-und-e-signatur',
      heading: 'Vertrag, Eintrittsgebühr und E-Signatur',
      paragraphs: [
        'Im Mitgliedschaftsvertrag werden Erwartungen zu Verpflichtungen. Er nennt Kategorie, Jahresbeitrag und Abrechnung, Laufzeit, Erneuerung und Kündigungsfrist, Hausordnung oder Statuten sowie die Eintrittsgebühr und ob ein Teil davon zurückerstattet wird.',
        'Nach Schweizer Recht sind die meisten Verträge an keine besondere Form gebunden; ein online unterschriebener Vertrag ist deshalb in der Regel gültig. Wo das Gesetz die Schriftform verlangt, ersetzt nur die qualifizierte elektronische Signatur die eigenhändige Unterschrift. Ein Mitgliedschaftsvertrag fällt normalerweise nicht darunter; lassen Sie Ihren Fall dennoch von einer Fachperson prüfen.',
        'Am einfachsten wird die Eintrittsgebühr bei der Unterzeichnung bezahlt, per Karte oder QR-Rechnung, statt eine Woche später separat in Rechnung gestellt. Genau das macht CadenceE mit [online unterschriebenen Verträgen](page:e-signature): Die Person unterschreibt ohne Konto und leistet die Anzahlung im selben Schritt. Mehr zum Einzahlungsschein im Ratgeber zur [QR-Rechnung](page:guide-qr-bill).',
      ],
    },
    {
      id: 'willkommen-und-integration',
      heading: 'Willkommen und Integration neuer Mitglieder',
      paragraphs: [
        'Die Integration beginnt am Tag der Unterschrift. Schicken Sie eine Willkommensnachricht mit den praktischen Informationen, öffnen Sie den Zugang zum Mitgliederportal, stellen Sie das neue Mitglied dem Team und einigen Mitgliedern mit ähnlichen Interessen vor, und laden Sie es im ersten Monat zu einem Anlass ein. Melden Sie sich nach etwa zwei Monaten.',
        'Wer sich im ersten Jahr zu Hause fühlt, hat allen Grund zu verlängern. Unsere Ratgeber [einen Privatclub führen](page:guide-run-a-club) und [Mitgliederbindung und Verlängerungen](page:guide-member-retention) zeigen, wie es weitergeht.',
      ],
    },
    {
      id: 'unterlagen-und-datenschutz',
      heading: 'Unterlagen und Datenschutz',
      paragraphs: [
        'Ein Aufnahmeantrag enthält Personendaten, und das DSG gilt ab dem Moment, in dem Sie sie erheben. Sagen Sie in Ihrer Datenschutzerklärung, was Sie erheben, wozu und wie lange. Beschränken Sie den Zugriff auf Anträge auf die Personen, die sie prüfen, und legen Sie eine kurze Frist fest, nach der abgelehnte Anträge gelöscht werden.',
        'Die Unterlagen aufgenommener Mitglieder gehen in die Mitgliederakte über. Rechnungen und Zahlungen für Eintrittsgebühren und Beiträge sind Buchhaltungsunterlagen und werden in der Schweiz in der Regel zehn Jahre aufbewahrt. Unsere [nDSG-Checkliste für Vereine](page:guide-fadp-checklist) deckt den Rest ab; für Ihren Fall fragen Sie eine Fachperson für Datenschutz.',
      ],
    },
    {
      id: 'werkzeuge',
      heading: 'Werkzeuge, die Anträge in Bewegung halten',
      paragraphs: [
        'Per E-Mail und Tabelle bearbeitete Anträge bleiben gern liegen: Die Antwort eines Paten steckt in einem Posteingang, und niemand weiss, wer die Person zurückrufen soll. Ein Werkzeug für diese Aufgabe hält jeden Antrag in einer Liste, mit Status und Verlauf.',
        'In CadenceE speist ein Antragsformular auf Ihrer Website eine einzige Liste, mit Dublettenschutz. Jeder Datensatz zeigt, wer die Person empfohlen hat, der Vertrag geht mit der Anzahlung zur E-Signatur, und ein Schritt macht aus der antragstellenden Person ein aktives Mitglied, mit Willkommens-E-Mail und Portalzugang. Mehr zur [Mitgliederverwaltung](page:membership) und zum [CRM für Privatclubs](page:private-clubs), oder [testen Sie die Demo](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'Aufnahmeprozess: häufige Fragen',
    items: [
      {
        q: 'Wie lange sollte ein Aufnahmeprozess dauern?',
        a: 'Streben Sie einen Entscheid innert weniger Wochen nach vollständigem Antrag an. Beantworten Sie Anfragen innert eines Arbeitstags, tagen Sie in einem bekannten Rhythmus und sagen Sie den Antragstellenden, wann sie Bescheid erhalten.',
      },
      {
        q: 'Was gehört in einen Mitgliedsantrag für einen Club?',
        a: 'Kontaktdaten, Korrespondenzsprache, Mitgliederkategorie, allenfalls Haushaltsmitglieder, Paten, einige Zeilen zur Motivation und die Bestätigung der Datenschutzerklärung. Lassen Sie besonders schützenswerte Daten wie Gesundheit, Religion oder politische Ansichten weg.',
      },
      {
        q: 'Braucht es Paten für die Aufnahme?',
        a: 'Nur wenn Ihre Regeln es vorsehen. Viele Privatclubs verlangen ein oder zwei Mitglieder, die den Antrag unterstützen; einige kurze, strukturierte Fragen an die Paten sind besser als ein freier Brief.',
      },
      {
        q: 'Muss ein Verein eine Ablehnung begründen?',
        a: 'Das hängt von Ihren Statuten oder Mitgliedschaftsbedingungen ab. Viele Clubs halten Absagen kurz. Wie auch immer Ihre Regel lautet: Wenden Sie sie einheitlich an, bleiben Sie höflich und lassen Sie die Rechtslage von einer Fachperson prüfen.',
      },
      {
        q: 'Ist eine elektronische Unterschrift für einen Mitgliedschaftsvertrag in der Schweiz gültig?',
        a: 'In der Regel ja, denn die meisten Verträge sind an keine Form gebunden. Wo das Gesetz die Schriftform verlangt, ersetzt nur die qualifizierte elektronische Signatur die eigenhändige Unterschrift; lassen Sie Ihren Fall prüfen.',
      },
      {
        q: 'Wie lange dürfen wir Daten abgelehnter Antragstellender aufbewahren?',
        a: 'Das DSG nennt keine feste Frist, nur so lange wie nötig. Legen Sie eine kurze Frist fest, nennen Sie sie in der Datenschutzerklärung und löschen Sie die Daten danach.',
      },
    ],
  },
  cta: {
    heading: 'Vom Antrag zum Mitglied in einem Datensatz',
    text: 'CadenceE nimmt Aufnahmeanträge online entgegen, schickt den Vertrag mit der Anzahlung zur E-Signatur und macht aus der antragstellenden Person mit einem Schritt ein aktives Mitglied.',
  },
};

export default guide;
