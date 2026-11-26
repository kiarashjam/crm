// The pricing page, in each language: the launch offer (free until 31 December 2026) and,
// for when it has ended, the words that replace it (afterOffer). The page never shows the
// offer after the date: languagePage() swaps in afterOffer, and the head drops the offer.
//
// Before 2027, replace afterOffer with the real pricing (docs/SEO.md, "Before 2027").
//
// "No credit card required" is true: signing up asks for a name, an email address and a
// password (POST /api/auth/register), then a workspace name; no payment details at all.

import type { PageContent } from './types';

const enIncluded: PageContent['sections'][number] = {
  heading: 'What is included',
  paragraphs: ['Everything CadenceE does today, with nothing held back for a higher tier:'],
  bullets: [
    '[Leads and sales](page:sales): five phases, scoring, assignment, imports',
    '[Contracts with e-signature](page:e-signature) and deposits at signing',
    '[Membership management](page:membership): members, households, renewals',
    '[Events and hospitality](page:hospitality): reservations, tabs, Lightspeed POS',
    '[Billing and payments](page:billing): dues, statements, card, TWINT, SEPA, QR-bill',
    '[Email automation](page:email): outbox, sequences, newsletters, campaigns',
    'The [AI assistant](page:ai), the sales writer and the morning brief',
    'Reports, audit log, privacy requests, roles and three languages',
  ],
  link: 'features',
};

const enData: PageContent['sections'][number] = {
  heading: 'Your data stays yours',
  paragraphs: [
    'Lists such as leads and activities export to CSV, the accounting export goes to Bexio or Abacus, and a privacy request exports everything held about a person, under the GDPR and the Swiss FADP. Whatever you decide later, your data is not locked in.',
  ],
};

export const enPricing: PageContent = {
  eyebrow: 'Pricing',
  h1: 'CadenceE is free',
  h1Accent: 'until 31 December 2026',
  lead: 'Launch offer: every feature of CadenceE, for your whole team, at no cost until 31 December 2026. Sign up now and use it free for the whole period. Pricing from 2027 will be announced well before then.',
  heroCardTitle: 'Included, free, until 31 December 2026',
  heroPoints: [
    'Every module: sales, club, billing, email and AI',
    'Your team, with roles and permissions',
    'English, French and German',
    'No credit card required to sign up',
    'Your data exportable at any time',
  ],
  intro: {
    heading: 'One launch offer, everything included',
    paragraphs: [
      'During the launch period there is a single plan, and it costs nothing. Every feature area of CadenceE is included until 31 December 2026: the sales side, the club side, billing and payments, email, reports and the AI assistant. It is not a trial that stops after a few days, and no feature is kept back for a higher tier.',
      'Signing up asks for your name, your email address and a password, and then a name for your workspace. No credit card and no payment details are asked for. Pricing for 2027 will be announced well before the offer ends, so you can decide with time to spare.',
    ],
  },
  sections: [
    enIncluded,
    {
      heading: 'What happens after 31 December 2026',
      paragraphs: [
        'Pricing for 2027 will be announced well before the launch offer ends. Because no payment details are taken when you sign up, nothing can be charged automatically: you decide whether to continue once you know the price.',
      ],
    },
    enData,
  ],
  features: {
    heading: 'Every feature area, included',
    items: [
      { icon: 'leads', title: 'Leads and sales', text: 'Five phases from outreach to deposit, with scoring and assignment.' },
      { icon: 'signature', title: 'E-signature contracts', text: 'Signed online without an account, deposit at signing.' },
      { icon: 'members', title: 'Membership', text: 'Members, households, applications, renewals, corporate memberships.' },
      { icon: 'reservations', title: 'Events and reservations', text: 'Waitlists, private-event hire, visits and check-ins.' },
      { icon: 'tabs', title: 'Tabs and POS', text: 'Bar and restaurant tabs, house accounts, Lightspeed POS.' },
      { icon: 'payments', title: 'Billing and payments', text: 'Dues, statements, card, TWINT, SEPA and QR-bill.' },
      { icon: 'automations', title: 'Email automation', text: 'Outbox, sequences, automations, newsletters, campaigns.' },
      { icon: 'ai', title: 'AI assistant', text: 'Built on Claude, with the sales writer and the morning brief.' },
      { icon: 'roles', title: 'Platform', text: 'Roles, workspaces, audit log, passkeys and three languages.' },
    ],
  },
  steps: {
    heading: 'Start in four steps',
    items: [
      { title: 'Create your workspace', text: 'Your name, your email address and a password; no credit card.' },
      { title: 'Invite your team', text: 'Give each colleague the role and permissions they need.' },
      { title: 'Bring your data', text: 'Import leads, contacts and members with a dry run first, and undo if needed.' },
      { title: 'Work from one place', text: 'Every feature is switched on: follow leads to signature, welcome members, bill them.' },
    ],
  },
  faq: {
    heading: 'Pricing questions',
    items: [
      { q: 'How much does CadenceE cost?', a: 'Nothing until 31 December 2026. During the launch offer every feature is free for everyone who signs up. Pricing from 2027 will be announced well before then.' },
      { q: 'Is it really free?', a: 'Yes. There is one plan during the launch period, with every feature area included, and no payment details are asked for when you sign up. More on what the [free CRM](page:free-crm) includes.' },
      { q: 'What happens after 2026?', a: 'Pricing for 2027 will be announced well before the offer ends. Because no payment details are stored, nothing is charged automatically: you decide whether to continue.' },
      { q: 'Do I need a credit card?', a: 'No. Signing up asks only for your name, your email address and a password, and then a name for your workspace.' },
      { q: 'Can I export my data?', a: 'Yes. Lists such as leads and activities export to CSV, the accounting export goes to Bexio or Abacus, and a privacy request exports everything held about a person, under the GDPR and the Swiss FADP.' },
      { q: 'Which features are included?', a: 'All of them: leads and sales, contracts with e-signature, membership, events and hospitality, billing and payments, email automation, reports and the AI assistant. See all [features](page:features).' },
    ],
  },
  related: ['free-crm', 'features', 'private-clubs'],
  cta: {
    heading: 'Start free today',
    text: 'Sign up now and use every feature of CadenceE free until 31 December 2026.',
  },
  afterOffer: {
    h1: 'CadenceE pricing',
    h1Accent: 'for clubs and sales teams',
    lead: 'The launch offer, free until 31 December 2026, has ended. Ask us for the current pricing, or sign in to your workspace.',
    heroCardTitle: 'What every workspace includes',
    heroPoints: [
      'Every module: sales, club, billing, email and AI',
      'Your team, with roles and permissions',
      'English, French and German',
      'Your data exportable at any time',
    ],
    intro: {
      heading: 'Pricing',
      paragraphs: [
        'CadenceE was free for everyone who signed up during its launch offer, until 31 December 2026. Ask us for the pricing that applies now.',
      ],
    },
    sections: [enIncluded, enData],
    // The free-CRM page is about the offer: no longer linked once it has ended.
    related: ['features', 'private-clubs', 'swiss'],
    faq: {
      heading: 'Pricing questions',
      items: [
        { q: 'How much does CadenceE cost?', a: 'Ask us for the current pricing; we answer every request.' },
        { q: 'Do I need a credit card to sign up?', a: 'No. Signing up asks only for your name, your email address and a password, and then a name for your workspace.' },
        { q: 'Can I export my data?', a: 'Yes. Lists such as leads and activities export to CSV, the accounting export goes to Bexio or Abacus, and a privacy request exports everything held about a person, under the GDPR and the Swiss FADP.' },
        { q: 'Which features are included?', a: 'All of them: leads and sales, contracts with e-signature, membership, events and hospitality, billing and payments, email automation, reports and the AI assistant. See all [features](page:features).' },
      ],
    },
    cta: {
      heading: 'Talk to us about pricing',
      text: 'Ask for the current pricing or a demo, or create your workspace.',
    },
  },
};

const frIncluded: PageContent['sections'][number] = {
  heading: 'Ce qui est inclus',
  paragraphs: ['Tout ce que CadenceE fait aujourd’hui, sans rien réserver à une formule supérieure :'],
  bullets: [
    '[Prospects et ventes](page:sales) : cinq phases, score, attribution, imports',
    '[Contrats en signature électronique](page:e-signature) et acompte à la signature',
    '[Gestion des membres](page:membership) : membres, foyers, renouvellements',
    '[Événements et hôtellerie-restauration](page:hospitality) : réservations, notes, caisse Lightspeed',
    '[Facturation et paiements](page:billing) : cotisations, relevés, carte, TWINT, SEPA, facture QR',
    '[Automatisation des e-mails](page:email) : boîte d’envoi, séquences, newsletters, campagnes',
    'L’[assistant IA](page:ai), le rédacteur commercial et le brief du matin',
    'Rapports, journal d’audit, demandes nLPD, rôles et trois langues',
  ],
  link: 'features',
};

const frData: PageContent['sections'][number] = {
  heading: 'Vos données restent les vôtres',
  paragraphs: [
    'Les listes comme les prospects et les activités s’exportent en CSV, l’export comptable part vers Bexio ou Abacus, et une demande d’accès exporte tout ce qui concerne une personne, selon le RGPD et la nLPD. Quoi que vous décidiez ensuite, vos données ne sont pas captives.',
  ],
};

export const frPricing: PageContent = {
  eyebrow: 'Tarifs',
  h1: 'CadenceE est gratuit',
  h1Accent: 'jusqu’au 31 décembre 2026',
  lead: 'Offre de lancement : toutes les fonctionnalités de CadenceE, pour toute votre équipe, sans frais jusqu’au 31 décembre 2026. Inscrivez-vous maintenant et profitez-en gratuitement pendant toute la période. Les tarifs à partir de 2027 seront annoncés bien avant.',
  heroCardTitle: 'Inclus, gratuitement, jusqu’au 31 décembre 2026',
  heroPoints: [
    'Tous les modules : vente, club, facturation, e-mails et IA',
    'Votre équipe, avec rôles et autorisations',
    'Français, allemand et anglais',
    'Inscription sans carte de crédit',
    'Vos données exportables à tout moment',
  ],
  intro: {
    heading: 'Une offre de lancement, tout compris',
    paragraphs: [
      'Pendant la période de lancement, il n’existe qu’une seule formule, et elle est gratuite. Toutes les fonctionnalités de CadenceE sont incluses jusqu’au 31 décembre 2026 : la vente, la vie du club, la facturation et les paiements, les e-mails, les rapports et l’assistant IA. Ce n’est pas un essai qui s’arrête après quelques jours, et aucune fonctionnalité n’est réservée à une formule supérieure.',
      'L’inscription demande votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail. Aucune carte de crédit ni coordonnée de paiement n’est demandée. Les tarifs 2027 seront annoncés bien avant la fin de l’offre, pour que vous puissiez décider en toute tranquillité.',
    ],
  },
  sections: [
    frIncluded,
    {
      heading: 'Et après le 31 décembre 2026 ?',
      paragraphs: [
        'Les tarifs 2027 seront annoncés bien avant la fin de l’offre de lancement. Comme aucune coordonnée de paiement n’est enregistrée à l’inscription, rien ne peut être prélevé automatiquement : vous décidez de continuer une fois le prix connu.',
      ],
    },
    frData,
  ],
  features: {
    heading: 'Tous les domaines, inclus',
    items: [
      { icon: 'leads', title: 'Prospects et ventes', text: 'Cinq phases du premier contact à l’acompte, avec score et attribution.' },
      { icon: 'signature', title: 'Signature électronique', text: 'Signés en ligne sans compte, acompte à la signature.' },
      { icon: 'members', title: 'Membres', text: 'Membres, foyers, candidatures, renouvellements, adhésions d’entreprise.' },
      { icon: 'reservations', title: 'Événements et réservations', text: 'Listes d’attente, privatisations, visites et pointages.' },
      { icon: 'tabs', title: 'Notes et caisse', text: 'Notes de bar et de restaurant, comptes membres, caisse Lightspeed.' },
      { icon: 'payments', title: 'Facturation et paiements', text: 'Cotisations, relevés, carte, TWINT, SEPA et facture QR.' },
      { icon: 'automations', title: 'Automatisation des e-mails', text: 'Boîte d’envoi, séquences, automatisations, newsletters, campagnes.' },
      { icon: 'ai', title: 'Assistant IA', text: 'Basé sur Claude, avec le rédacteur commercial et le brief du matin.' },
      { icon: 'roles', title: 'Plateforme', text: 'Rôles, espaces de travail, journal d’audit, clés d’accès et trois langues.' },
    ],
  },
  steps: {
    heading: 'Démarrer en quatre étapes',
    items: [
      { title: 'Créez votre espace', text: 'Votre nom, votre e-mail et un mot de passe ; aucune carte de crédit.' },
      { title: 'Invitez votre équipe', text: 'Donnez à chacun le rôle et les autorisations dont il a besoin.' },
      { title: 'Importez vos données', text: 'Prospects, contacts et membres avec une simulation d’abord, et une annulation possible.' },
      { title: 'Travaillez au même endroit', text: 'Tout est activé : suivez les prospects jusqu’à la signature, accueillez les membres, facturez.' },
    ],
  },
  faq: {
    heading: 'Questions sur les tarifs',
    items: [
      { q: 'Combien coûte CadenceE ?', a: 'Rien jusqu’au 31 décembre 2026. Pendant l’offre de lancement, toutes les fonctionnalités sont gratuites pour toute personne qui s’inscrit. Les tarifs à partir de 2027 seront annoncés bien avant.' },
      { q: 'Est-ce vraiment gratuit ?', a: 'Oui. Il n’y a qu’une formule pendant la période de lancement, avec toutes les fonctionnalités, et aucune coordonnée de paiement n’est demandée à l’inscription. Plus de détails sur notre [CRM gratuit](page:free-crm).' },
      { q: 'Que se passe-t-il après 2026 ?', a: 'Les tarifs 2027 seront annoncés bien avant la fin de l’offre. Comme aucune coordonnée de paiement n’est enregistrée, rien n’est prélevé automatiquement : vous décidez de continuer ou non.' },
      { q: 'Faut-il une carte de crédit ?', a: 'Non. L’inscription demande seulement votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail.' },
      { q: 'Puis-je exporter mes données ?', a: 'Oui. Les listes comme les prospects et les activités s’exportent en CSV, l’export comptable part vers Bexio ou Abacus, et une demande d’accès exporte tout ce qui concerne une personne, selon le RGPD et la nLPD.' },
      { q: 'Quelles fonctionnalités sont incluses ?', a: 'Toutes : prospects et ventes, contrats en signature électronique, membres, événements et hôtellerie-restauration, facturation et paiements, automatisation des e-mails, rapports et assistant IA. Voir toutes les [fonctionnalités](page:features).' },
    ],
  },
  related: ['free-crm', 'features', 'private-clubs'],
  cta: {
    heading: 'Commencez gratuitement dès aujourd’hui',
    text: 'Inscrivez-vous maintenant et utilisez toutes les fonctionnalités de CadenceE gratuitement jusqu’au 31 décembre 2026.',
  },
  afterOffer: {
    h1: 'Les tarifs de CadenceE',
    h1Accent: 'pour clubs et équipes de vente',
    lead: 'L’offre de lancement, gratuite jusqu’au 31 décembre 2026, est terminée. Demandez-nous les tarifs actuels, ou connectez-vous à votre espace de travail.',
    heroCardTitle: 'Ce que comprend chaque espace de travail',
    heroPoints: [
      'Tous les modules : vente, club, facturation, e-mails et IA',
      'Votre équipe, avec rôles et autorisations',
      'Français, allemand et anglais',
      'Vos données exportables à tout moment',
    ],
    intro: {
      heading: 'Tarifs',
      paragraphs: [
        'CadenceE était gratuit pour toutes les personnes inscrites pendant son offre de lancement, jusqu’au 31 décembre 2026. Demandez-nous les tarifs qui s’appliquent aujourd’hui.',
      ],
    },
    sections: [frIncluded, frData],
    // The free-CRM page is about the offer: no longer linked once it has ended.
    related: ['features', 'private-clubs', 'swiss'],
    faq: {
      heading: 'Questions sur les tarifs',
      items: [
        { q: 'Combien coûte CadenceE ?', a: 'Demandez-nous les tarifs actuels ; nous répondons à chaque demande.' },
        { q: 'Faut-il une carte de crédit pour s’inscrire ?', a: 'Non. L’inscription demande seulement votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail.' },
        { q: 'Puis-je exporter mes données ?', a: 'Oui. Les listes comme les prospects et les activités s’exportent en CSV, l’export comptable part vers Bexio ou Abacus, et une demande d’accès exporte tout ce qui concerne une personne, selon le RGPD et la nLPD.' },
        { q: 'Quelles fonctionnalités sont incluses ?', a: 'Toutes : prospects et ventes, contrats en signature électronique, membres, événements et hôtellerie-restauration, facturation et paiements, automatisation des e-mails, rapports et assistant IA. Voir toutes les [fonctionnalités](page:features).' },
      ],
    },
    cta: {
      heading: 'Parlons tarifs',
      text: 'Demandez les tarifs actuels ou une démonstration, ou créez votre espace de travail.',
    },
  },
};

const deIncluded: PageContent['sections'][number] = {
  heading: 'Was alles enthalten ist',
  paragraphs: ['Alles, was CadenceE heute kann, ohne dass etwas einem teureren Paket vorbehalten bleibt:'],
  bullets: [
    '[Leads und Vertrieb](page:sales): fünf Phasen, Scoring, Zuweisung, Importe',
    '[Verträge mit E-Signatur](page:e-signature) und Anzahlung bei Unterzeichnung',
    '[Mitgliederverwaltung](page:membership): Mitglieder, Haushalte, Erneuerungen',
    '[Anlässe und Gastronomie](page:hospitality): Reservationen, Konsumationen, Lightspeed-Kasse',
    '[Abrechnung und Zahlungen](page:billing): Beiträge, Auszüge, Karte, TWINT, SEPA, QR-Rechnung',
    '[E-Mail-Automatisierung](page:email): Postausgang, Sequenzen, Newsletter, Kampagnen',
    'Der [KI-Assistent](page:ai), der KI-Texter und das Morgenbriefing',
    'Berichte, Audit-Log, Datenschutzbegehren, Rollen und drei Sprachen',
  ],
  link: 'features',
};

const deData: PageContent['sections'][number] = {
  heading: 'Ihre Daten bleiben Ihre Daten',
  paragraphs: [
    'Listen wie Leads und Aktivitäten lassen sich als CSV exportieren, der Buchhaltungsexport geht nach Bexio oder Abacus, und ein Auskunftsbegehren exportiert alles, was zu einer Person gespeichert ist, nach DSGVO und DSG. Wie auch immer Sie sich später entscheiden: Ihre Daten sind nicht eingesperrt.',
  ],
};

export const dePricing: PageContent = {
  eyebrow: 'Preise',
  h1: 'CadenceE ist kostenlos',
  h1Accent: 'bis 31. Dezember 2026',
  lead: 'Einführungsangebot: alle Funktionen von CadenceE, für Ihr ganzes Team, ohne Kosten bis 31. Dezember 2026. Registrieren Sie sich jetzt und nutzen Sie den ganzen Zeitraum kostenlos. Die Preise ab 2027 geben wir lange vorher bekannt.',
  heroCardTitle: 'Inklusive, kostenlos, bis 31. Dezember 2026',
  heroPoints: [
    'Alle Module: Vertrieb, Club, Abrechnung, E-Mail und KI',
    'Ihr Team, mit Rollen und Berechtigungen',
    'Deutsch, Französisch und Englisch',
    'Registrierung ohne Kreditkarte',
    'Ihre Daten jederzeit exportierbar',
  ],
  intro: {
    heading: 'Ein Einführungsangebot, alles inklusive',
    paragraphs: [
      'Während der Einführungsphase gibt es ein einziges Paket, und es kostet nichts. Alle Funktionen von CadenceE sind bis 31. Dezember 2026 enthalten: Vertrieb, Clubbetrieb, Abrechnung und Zahlungen, E-Mail, Berichte und der KI-Assistent. Es ist keine Testversion, die nach ein paar Tagen endet, und keine Funktion bleibt einem teureren Paket vorbehalten.',
      'Für die Registrierung brauchen Sie nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich. Weder Kreditkarte noch Zahlungsangaben werden verlangt. Die Preise für 2027 geben wir lange vor dem Ende des Angebots bekannt, damit Sie in Ruhe entscheiden können.',
    ],
  },
  sections: [
    deIncluded,
    {
      heading: 'Was nach dem 31. Dezember 2026 passiert',
      paragraphs: [
        'Die Preise für 2027 geben wir lange vor dem Ende des Einführungsangebots bekannt. Weil bei der Registrierung keine Zahlungsangaben gespeichert werden, kann nichts automatisch belastet werden: Sie entscheiden, ob Sie weitermachen, wenn Sie den Preis kennen.',
      ],
    },
    deData,
  ],
  features: {
    heading: 'Alle Bereiche, inklusive',
    items: [
      { icon: 'leads', title: 'Leads und Vertrieb', text: 'Fünf Phasen von der Kontaktaufnahme bis zur Anzahlung, mit Scoring und Zuweisung.' },
      { icon: 'signature', title: 'Verträge mit E-Signatur', text: 'Online unterschrieben ohne Konto, Anzahlung bei Unterzeichnung.' },
      { icon: 'members', title: 'Mitglieder', text: 'Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen, Firmenmitgliedschaften.' },
      { icon: 'reservations', title: 'Anlässe und Reservationen', text: 'Wartelisten, Privatanlässe, Besuche und Check-ins.' },
      { icon: 'tabs', title: 'Konsumationen und Kasse', text: 'Bar- und Restaurantrechnungen, Mitgliederkonten, Lightspeed-Kasse.' },
      { icon: 'payments', title: 'Abrechnung und Zahlungen', text: 'Beiträge, Auszüge, Karte, TWINT, SEPA und QR-Rechnung.' },
      { icon: 'automations', title: 'E-Mail-Automatisierung', text: 'Postausgang, Sequenzen, Automationen, Newsletter, Kampagnen.' },
      { icon: 'ai', title: 'KI-Assistent', text: 'Auf Basis von Claude, mit KI-Texter und Morgenbriefing.' },
      { icon: 'roles', title: 'Plattform', text: 'Rollen, Arbeitsbereiche, Audit-Log, Passkeys und drei Sprachen.' },
    ],
  },
  steps: {
    heading: 'In vier Schritten starten',
    items: [
      { title: 'Arbeitsbereich erstellen', text: 'Name, E-Mail-Adresse und Passwort; keine Kreditkarte.' },
      { title: 'Team einladen', text: 'Jeder Person die Rolle und die Berechtigungen geben, die sie braucht.' },
      { title: 'Daten übernehmen', text: 'Leads, Kontakte und Mitglieder importieren, zuerst mit Probelauf, bei Bedarf rückgängig.' },
      { title: 'An einem Ort arbeiten', text: 'Alles ist eingeschaltet: Leads bis zur Unterschrift begleiten, Mitglieder begrüssen, abrechnen.' },
    ],
  },
  faq: {
    heading: 'Fragen zu den Preisen',
    items: [
      { q: 'Was kostet CadenceE?', a: 'Bis 31. Dezember 2026 nichts. Während des Einführungsangebots sind alle Funktionen für alle kostenlos, die sich registrieren. Die Preise ab 2027 geben wir lange vorher bekannt.' },
      { q: 'Ist es wirklich kostenlos?', a: 'Ja. Während der Einführungsphase gibt es ein Paket mit allen Funktionen, und bei der Registrierung werden keine Zahlungsangaben verlangt. Mehr zu unserem [kostenlosen CRM](page:free-crm).' },
      { q: 'Was passiert nach 2026?', a: 'Die Preise für 2027 geben wir lange vor dem Ende des Angebots bekannt. Weil keine Zahlungsangaben gespeichert sind, wird nichts automatisch belastet: Sie entscheiden, ob Sie weitermachen.' },
      { q: 'Brauche ich eine Kreditkarte?', a: 'Nein. Die Registrierung verlangt nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich.' },
      { q: 'Kann ich meine Daten exportieren?', a: 'Ja. Listen wie Leads und Aktivitäten lassen sich als CSV exportieren, der Buchhaltungsexport geht nach Bexio oder Abacus, und ein Auskunftsbegehren exportiert alles, was zu einer Person gespeichert ist, nach DSGVO und DSG.' },
      { q: 'Welche Funktionen sind enthalten?', a: 'Alle: Leads und Vertrieb, Verträge mit E-Signatur, Mitglieder, Anlässe und Gastronomie, Abrechnung und Zahlungen, E-Mail-Automatisierung, Berichte und KI-Assistent. Alle [Funktionen](page:features) im Überblick.' },
    ],
  },
  related: ['free-crm', 'features', 'private-clubs'],
  cta: {
    heading: 'Heute kostenlos starten',
    text: 'Registrieren Sie sich jetzt und nutzen Sie alle Funktionen von CadenceE bis 31. Dezember 2026 kostenlos.',
  },
  afterOffer: {
    h1: 'Die Preise von CadenceE',
    h1Accent: 'für Clubs und Verkaufsteams',
    lead: 'Das Einführungsangebot, kostenlos bis 31. Dezember 2026, ist beendet. Fragen Sie uns nach den aktuellen Preisen, oder melden Sie sich in Ihrem Arbeitsbereich an.',
    heroCardTitle: 'Was jeder Arbeitsbereich enthält',
    heroPoints: [
      'Alle Module: Vertrieb, Club, Abrechnung, E-Mail und KI',
      'Ihr Team, mit Rollen und Berechtigungen',
      'Deutsch, Französisch und Englisch',
      'Ihre Daten jederzeit exportierbar',
    ],
    intro: {
      heading: 'Preise',
      paragraphs: [
        'CadenceE war für alle kostenlos, die sich während des Einführungsangebots bis 31. Dezember 2026 registriert haben. Fragen Sie uns nach den Preisen, die heute gelten.',
      ],
    },
    sections: [deIncluded, deData],
    // The free-CRM page is about the offer: no longer linked once it has ended.
    related: ['features', 'private-clubs', 'swiss'],
    faq: {
      heading: 'Fragen zu den Preisen',
      items: [
        { q: 'Was kostet CadenceE?', a: 'Fragen Sie uns nach den aktuellen Preisen; wir beantworten jede Anfrage.' },
        { q: 'Brauche ich für die Registrierung eine Kreditkarte?', a: 'Nein. Die Registrierung verlangt nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich.' },
        { q: 'Kann ich meine Daten exportieren?', a: 'Ja. Listen wie Leads und Aktivitäten lassen sich als CSV exportieren, der Buchhaltungsexport geht nach Bexio oder Abacus, und ein Auskunftsbegehren exportiert alles, was zu einer Person gespeichert ist, nach DSGVO und DSG.' },
        { q: 'Welche Funktionen sind enthalten?', a: 'Alle: Leads und Vertrieb, Verträge mit E-Signatur, Mitglieder, Anlässe und Gastronomie, Abrechnung und Zahlungen, E-Mail-Automatisierung, Berichte und KI-Assistent. Alle [Funktionen](page:features) im Überblick.' },
      ],
    },
    cta: {
      heading: 'Sprechen wir über Preise',
      text: 'Fragen Sie nach den aktuellen Preisen oder einer Demo, oder erstellen Sie Ihren Arbeitsbereich.',
    },
  },
};
