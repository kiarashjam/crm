// The free-CRM page, in each language: for people who search for a free CRM ("free CRM
// software", "CRM gratuit", "kostenloses CRM"). Its angle is not the price (that is the
// pricing page) but what the free offer is: every feature, the whole team, no credit card,
// not a cut-down free tier. It exists for the launch offer only (offerOnly in routes.ts):
// from 1 January 2027 it shows afterOffer, is marked noindex and leaves the sitemap.
//
// Every claim comes from pricing.ts and the topic pages: no limits promised beyond "no
// feature tiers", no customer counts, no comparison with a named product. "Try the demo"
// (/login?demo=1) opens a demo workspace with sample data in the browser, with no account.

import type { PageContent } from './types';

const DEMO = '/login?demo=1';

// ---------------------------------------------------------------------------
// English

const enAudience: string[] = [
  '[Private clubs](page:private-clubs) and associations: enquiries, applications, members and dues',
  '[Hospitality](page:hospitality): restaurants, hotels and event venues, with reservations, private events and tabs',
  '[Sales teams](page:sales): every lead followed from outreach to the deposit',
  'Swiss businesses that need the [QR-bill, TWINT, Bexio and Abacus](page:swiss)',
];

const enExport =
  'Lists such as leads and activities export to CSV, the accounting export goes to Bexio or Abacus, and a privacy request exports everything held about a person, under the GDPR and the Swiss FADP.';

const enFeatures: PageContent['features']['items'] = [
  { icon: 'leads', title: 'Five-phase lead tracker', text: 'Outreach, meeting, contract, signature, deposit, with scoring and assignment.' },
  { icon: 'signature', title: 'Contracts with e-signature', text: 'Signed online without an account, with the deposit collected at signing.' },
  { icon: 'members', title: 'Membership management', text: 'Members, households, applications, renewals and corporate memberships.' },
  { icon: 'reservations', title: 'Events and reservations', text: 'Waitlists, private-event hire, visits and check-ins.' },
  { icon: 'tabs', title: 'Tabs and POS', text: 'Bar and restaurant tabs, house accounts and a Lightspeed POS connection.' },
  { icon: 'payments', title: 'Swiss payments', text: 'Card, TWINT, SEPA and QR-bill, with bank-file matching.' },
  { icon: 'automations', title: 'Email automation', text: 'Outbox, sequences, automations, newsletters and campaigns with consent.' },
  { icon: 'ai', title: 'AI assistant', text: 'Built on Claude: proposes changes you confirm, drafts emails and prepares a morning brief.' },
  { icon: 'roles', title: 'For the whole team', text: 'Roles and permissions, audit log, passkeys and two-factor authentication.' },
];

const enSteps: PageContent['steps'] = {
  heading: 'Start in minutes',
  intro: 'No sales call and no credit card: four steps from this page to a working CRM.',
  items: [
    { title: 'Create your account', text: 'Your name, your email address and a password. No credit card.' },
    { title: 'Name your workspace', text: 'Give it your club’s or your company’s name; every feature is already switched on.' },
    { title: 'Invite your team', text: 'Give each colleague the role and permissions they need.' },
    { title: 'Bring your data', text: 'Import leads, contacts and members from spreadsheets, with a dry run first and undo if needed.' },
  ],
};

export const enFreeCrm: PageContent = {
  eyebrow: 'Free CRM',
  h1: 'Free CRM software with every feature,',
  h1Accent: 'not a cut-down free tier',
  lead: `CadenceE is free until 31 December 2026: every feature, for your whole team, with no credit card and no feature tiers. A CRM for clubs, hospitality and sales teams, built in Switzerland, in English, French and German. Sign up in minutes, or [try the demo](${DEMO}) first, without an account.`,
  heroCardTitle: 'Free until 31 December 2026',
  heroPoints: [
    'Every feature: sales, club, billing, email and AI',
    'Your whole team, with roles and permissions',
    'No credit card: name, email, password',
    'English, French and German',
    'Your data exportable at any time',
  ],
  intro: {
    heading: 'A free CRM that is not a cut-down free tier',
    paragraphs: [
      'A free CRM plan is often the bottom tier of a paid product: a handful of features, and the rest behind an upgrade. CadenceE’s launch offer works the other way round. Until 31 December 2026 there is a single plan, it costs nothing, and it includes every feature area: leads and sales, contracts with e-signature, membership, events and hospitality, billing and payments, email automation, reports and the AI assistant.',
      'It is not a trial that stops after a few days, and nothing is kept back for a higher tier. Signing up asks for your name, your email address and a password, then a name for your workspace: no credit card, no payment details. Pricing for 2027 will be announced well before the offer ends, and because no payment details are stored, nothing can be charged automatically.',
    ],
  },
  sections: [
    {
      heading: 'Who this free CRM is for',
      paragraphs: [
        'CadenceE brings the sales side and the club side of a business into one CRM, so it suits teams that win, serve and bill the same people. See all [features](page:features).',
      ],
      bullets: enAudience,
    },
    {
      heading: 'Free membership management software for clubs',
      paragraphs: [
        'For a club or an association, CadenceE is free membership management software as well as a CRM. Members belong to households, companies can hold corporate memberships, applications arrive through a public form and are reviewed in one list, and every renewal carries an at-risk score. Recurring dues and monthly PDF statements are paid by card, TWINT, SEPA or Swiss QR-bill, and members sign in to their own portal with an emailed link.',
      ],
      link: 'membership',
    },
    {
      heading: 'A free sales CRM, all the way to the deposit',
      paragraphs: [
        'The lead tracker follows every opportunity through five phases: outreach, meeting, contract, signature and deposit. Leads are scored, assigned and protected against duplicates; contracts are generated from your templates and [signed online](page:e-signature) without an account, and the deposit is collected at signing. The AI sales writer drafts the emails in between, for a person to review and send.',
      ],
      link: 'sales',
    },
    {
      heading: 'A free CRM made in Switzerland',
      paragraphs: [
        'CadenceE was built in Switzerland, alongside Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud, which uses it every day. Payments work with the Swiss QR-bill and TWINT, the accounting export goes to Bexio or Abacus, and privacy requests are handled under the Swiss FADP and the GDPR. The whole app is available in English, French and German, and each person picks their own language. More on the [Swiss CRM](page:swiss).',
      ],
    },
    {
      heading: 'Your data is not locked in',
      paragraphs: [
        `A free CRM is only worth it if you can leave with your data. ${enExport} Whatever you decide in 2027, your data stays yours.`,
      ],
    },
    {
      heading: 'What happens after 31 December 2026',
      paragraphs: [
        'Pricing for 2027 will be announced well before the launch offer ends, so you can decide with time to spare. No payment details are taken when you sign up, so nothing is charged automatically: you choose whether to continue once you know the price. The [pricing page](page:pricing) has the details of the offer.',
      ],
    },
  ],
  features: { heading: 'Everything included in the free CRM', items: enFeatures },
  steps: enSteps,
  faq: {
    heading: 'Free CRM: frequent questions',
    items: [
      { q: 'Is CadenceE really free?', a: 'Yes, until 31 December 2026. During the launch offer there is one plan, it costs nothing, and every feature area is included for everyone who signs up. It is not a trial that stops after a few days.' },
      { q: 'Are there limits or feature tiers?', a: 'No feature tiers. Nothing is held back for a paid plan, and your whole team gets every module, with roles and permissions. What the offer does have is an end date: 31 December 2026.' },
      { q: 'Do I need a credit card to sign up?', a: 'No. Signing up asks only for your name, your email address and a password, and then a name for your workspace. No payment details are asked for.' },
      { q: 'What happens in 2027?', a: 'Pricing for 2027 will be announced well before the offer ends. Because no payment details are stored, nothing is charged automatically: you decide whether to continue once you know the price. See [pricing](page:pricing).' },
      { q: 'Can I export my data?', a: `Yes. ${enExport}` },
      { q: 'Can I try it without signing up?', a: `Yes. [Try the demo](${DEMO}): a demo workspace with sample data runs in your browser, with no account. When you want your own data in it, create your free workspace.` },
      { q: 'Is it free for clubs and associations too?', a: 'Yes. Membership management is part of the offer: members, households, applications, renewals, recurring dues, statements and the member portal, free until 31 December 2026. See [membership management](page:membership).' },
      { q: 'Is this a free CRM made for Switzerland?', a: 'Yes. CadenceE was built in Switzerland: payments by QR-bill and TWINT, accounting export to Bexio and Abacus, privacy requests under the FADP, and the app in English, French and German.' },
    ],
  },
  related: ['pricing', 'features', 'membership'],
  cta: {
    heading: 'Start your free CRM today',
    text: 'Every feature, your whole team, no credit card, free until 31 December 2026. Or try the demo first, with no account.',
  },
  afterOffer: {
    eyebrow: 'Launch offer',
    h1: 'CadenceE’s free launch offer',
    h1Accent: 'has ended',
    lead: `CadenceE was free for everyone who signed up until 31 December 2026. That launch offer is over: ask us for the current pricing, [try the demo](${DEMO}), or sign in to your workspace.`,
    heroCardTitle: 'What CadenceE includes',
    heroPoints: [
      'Every module: sales, club, billing, email and AI',
      'Your team, with roles and permissions',
      'English, French and German',
      'Your data exportable at any time',
    ],
    intro: {
      heading: 'The launch offer is over',
      paragraphs: [
        'During its launch offer, until 31 December 2026, CadenceE was free for everyone who signed up, with every feature included. That offer has ended. Ask us for the pricing that applies now; see [pricing](page:pricing).',
      ],
    },
    sections: [
      {
        heading: 'Who CadenceE is for',
        paragraphs: ['CadenceE brings the sales side and the club side of a business into one CRM. See all [features](page:features).'],
        bullets: enAudience,
      },
      {
        heading: 'Your data is not locked in',
        paragraphs: [`${enExport} Whatever you decide, your data stays yours.`],
      },
    ],
    features: { heading: 'What CadenceE does', items: enFeatures },
    faq: {
      heading: 'Questions about the launch offer',
      items: [
        { q: 'Is CadenceE still free?', a: 'The launch offer, free until 31 December 2026, has ended. Ask us for the current pricing; we answer every request.' },
        { q: 'Do I need a credit card to sign up?', a: 'No. Signing up asks only for your name, your email address and a password, and then a name for your workspace.' },
        { q: 'Can I try it without signing up?', a: `Yes. [Try the demo](${DEMO}): a demo workspace with sample data runs in your browser, with no account.` },
        { q: 'Can I export my data?', a: `Yes. ${enExport}` },
        { q: 'Which features are included?', a: 'All of them: leads and sales, contracts with e-signature, membership, events and hospitality, billing and payments, email automation, reports and the AI assistant. See all [features](page:features).' },
      ],
    },
    cta: {
      heading: 'Ask for the current pricing',
      text: 'Ask us for the pricing or a demo, or create your workspace.',
    },
  },
};

// ---------------------------------------------------------------------------
// Français (l’espacement typographique est appliqué par fr.ts)

const frAudience: string[] = [
  '[Clubs privés](page:private-clubs) et associations : demandes, candidatures, membres et cotisations',
  '[Hôtellerie-restauration](page:hospitality) : restaurants, hôtels et lieux d’événements, avec réservations, privatisations et notes',
  '[Équipes de vente](page:sales) : chaque prospect suivi jusqu’à l’acompte',
  'Entreprises suisses qui ont besoin de la [facture QR, de TWINT, de Bexio et d’Abacus](page:swiss)',
];

const frExport =
  'Les listes comme les prospects et les activités s’exportent en CSV, l’export comptable part vers Bexio ou Abacus, et une demande d’accès exporte tout ce qui concerne une personne, selon le RGPD et la nLPD.';

const frFeatures: PageContent['features']['items'] = [
  { icon: 'leads', title: 'Prospects en cinq phases', text: 'Prise de contact, rendez-vous, contrat, signature, acompte, avec score et attribution.' },
  { icon: 'signature', title: 'Contrats et signature électronique', text: 'Signés en ligne sans compte, avec l’acompte encaissé à la signature.' },
  { icon: 'members', title: 'Gestion des membres', text: 'Membres, foyers, candidatures, renouvellements et adhésions d’entreprise.' },
  { icon: 'reservations', title: 'Événements et réservations', text: 'Listes d’attente, privatisations, visites et pointages.' },
  { icon: 'tabs', title: 'Notes et caisse', text: 'Notes de bar et de restaurant, comptes maison et caisse Lightspeed.' },
  { icon: 'payments', title: 'Paiements suisses', text: 'Carte, TWINT, SEPA et facture QR, avec rapprochement bancaire.' },
  { icon: 'automations', title: 'Automatisation des e-mails', text: 'Boîte d’envoi, séquences, automatisations, newsletters et campagnes avec consentement.' },
  { icon: 'ai', title: 'Assistant IA', text: 'Basé sur Claude : il propose des modifications que vous confirmez, rédige vos e-mails et prépare le brief du matin.' },
  { icon: 'roles', title: 'Pour toute l’équipe', text: 'Rôles et autorisations, journal d’audit, clés d’accès et double authentification.' },
];

const frSteps: PageContent['steps'] = {
  heading: 'Démarrer en quelques minutes',
  intro: 'Ni appel commercial ni carte de crédit : quatre étapes entre cette page et un CRM qui tourne.',
  items: [
    { title: 'Créez votre compte', text: 'Votre nom, votre adresse e-mail et un mot de passe. Aucune carte de crédit.' },
    { title: 'Nommez votre espace', text: 'Donnez-lui le nom de votre club ou de votre entreprise ; toutes les fonctionnalités sont déjà activées.' },
    { title: 'Invitez votre équipe', text: 'Donnez à chacun le rôle et les autorisations dont il a besoin.' },
    { title: 'Importez vos données', text: 'Prospects, contacts et membres depuis un tableur, avec une simulation d’abord et une annulation possible.' },
  ],
};

export const frFreeCrm: PageContent = {
  eyebrow: 'CRM gratuit',
  h1: 'Un CRM gratuit avec toutes ses fonctionnalités,',
  h1Accent: 'pas une version bridée',
  lead: `CadenceE est gratuit jusqu’au 31 décembre 2026 : toutes les fonctionnalités, pour toute votre équipe, sans carte de crédit et sans formule limitée. Un logiciel CRM pour les clubs, les associations, l’hôtellerie-restauration et les équipes de vente, conçu en Suisse, en français, en allemand et en anglais. Inscrivez-vous en quelques minutes, ou [essayez la démo](${DEMO}) d’abord, sans compte.`,
  heroCardTitle: 'Gratuit jusqu’au 31 décembre 2026',
  heroPoints: [
    'Tous les modules : vente, club, facturation, e-mails et IA',
    'Toute votre équipe, avec rôles et autorisations',
    'Sans carte de crédit : nom, e-mail, mot de passe',
    'Français, allemand et anglais',
    'Vos données exportables à tout moment',
  ],
  intro: {
    heading: 'Un CRM gratuit qui n’est pas une version au rabais',
    paragraphs: [
      'Bien des CRM gratuits ne sont que la formule d’entrée d’un produit payant : quelques fonctionnalités, et le reste derrière une mise à niveau. L’offre de lancement de CadenceE fonctionne à l’inverse. Jusqu’au 31 décembre 2026, il n’existe qu’une seule formule, elle est gratuite, et elle comprend tous les domaines : prospects et ventes, contrats avec signature électronique, gestion des membres, événements et hôtellerie-restauration, facturation et paiements, automatisation des e-mails, rapports et assistant IA.',
      'Ce n’est pas un essai qui s’arrête après quelques jours, et rien n’est réservé à une formule supérieure. L’inscription demande votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail : ni carte de crédit ni coordonnées de paiement. Les tarifs 2027 seront annoncés bien avant la fin de l’offre, et comme aucune coordonnée de paiement n’est enregistrée, rien ne peut être prélevé automatiquement.',
    ],
  },
  sections: [
    {
      heading: 'À qui s’adresse ce CRM gratuit',
      paragraphs: [
        'CadenceE réunit la vente et la vie du club dans un seul CRM : il convient aux équipes qui gagnent, accueillent et facturent les mêmes personnes. Voir toutes les [fonctionnalités](page:features).',
      ],
      bullets: frAudience,
    },
    {
      heading: 'Un logiciel de gestion des membres gratuit pour club et association',
      paragraphs: [
        'Pour un club ou une association, CadenceE est aussi un logiciel de gestion des membres gratuit. Les membres sont regroupés en foyers, les entreprises peuvent détenir des adhésions d’entreprise, les candidatures arrivent par un formulaire public et sont examinées dans une seule liste, et chaque renouvellement porte un score de risque. Les cotisations récurrentes et les relevés PDF mensuels se paient par carte, TWINT, SEPA ou facture QR, et les membres se connectent à leur portail avec un lien reçu par e-mail.',
      ],
      link: 'membership',
    },
    {
      heading: 'Un CRM commercial gratuit, jusqu’à l’acompte',
      paragraphs: [
        'Le suivi des prospects accompagne chaque opportunité en cinq phases : prise de contact, rendez-vous, contrat, signature et acompte. Les prospects sont notés, attribués et protégés contre les doublons ; les contrats sont générés depuis vos modèles et [signés en ligne](page:e-signature) sans compte, et l’acompte est encaissé à la signature. Le rédacteur commercial IA prépare les e-mails entre deux étapes, pour qu’une personne les relise et les envoie.',
      ],
      link: 'sales',
    },
    {
      heading: 'Un CRM gratuit conçu en Suisse',
      paragraphs: [
        'CadenceE a été conçu en Suisse, aux côtés du Pavillon 46, club privé à La Croix-sur-Lutry (VD), qui l’utilise chaque jour. Les paiements passent par la facture QR et TWINT, l’export comptable part vers Bexio ou Abacus, et les demandes d’accès ou d’effacement sont traitées selon la nLPD et le RGPD. Toute l’application existe en français, en allemand et en anglais, au choix de chacun. Découvrez notre [CRM suisse](page:swiss).',
      ],
    },
    {
      heading: 'Vos données ne sont pas captives',
      paragraphs: [
        `Un CRM gratuit ne vaut que si vous pouvez repartir avec vos données. ${frExport} Quoi que vous décidiez en 2027, vos données restent les vôtres.`,
      ],
    },
    {
      heading: 'Ce qui se passe après le 31 décembre 2026',
      paragraphs: [
        'Les tarifs 2027 seront annoncés bien avant la fin de l’offre de lancement, pour que vous puissiez décider en toute tranquillité. Aucune coordonnée de paiement n’est demandée à l’inscription : rien n’est donc prélevé automatiquement, et vous choisissez de continuer une fois le prix connu. La page [tarifs](page:pricing) détaille l’offre.',
      ],
    },
  ],
  features: { heading: 'Tout ce que comprend le CRM gratuit', items: frFeatures },
  steps: frSteps,
  faq: {
    heading: 'CRM gratuit : questions fréquentes',
    items: [
      { q: 'CadenceE est-il vraiment gratuit ?', a: 'Oui, jusqu’au 31 décembre 2026. Pendant l’offre de lancement, il n’existe qu’une formule, elle ne coûte rien, et toutes les fonctionnalités sont incluses pour toute personne qui s’inscrit. Ce n’est pas un essai qui s’arrête après quelques jours.' },
      { q: 'Y a-t-il des limites ou des formules ?', a: 'Aucune formule limitée : rien n’est réservé à une offre payante, et toute votre équipe dispose de tous les modules, avec rôles et autorisations. L’offre a en revanche une date de fin : le 31 décembre 2026.' },
      { q: 'Faut-il une carte de crédit pour s’inscrire ?', a: 'Non. L’inscription demande seulement votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail. Aucune coordonnée de paiement n’est demandée.' },
      { q: 'Que se passe-t-il en 2027 ?', a: 'Les tarifs 2027 seront annoncés bien avant la fin de l’offre. Comme aucune coordonnée de paiement n’est enregistrée, rien n’est prélevé automatiquement : vous décidez de continuer une fois le prix connu. Voir les [tarifs](page:pricing).' },
      { q: 'Puis-je exporter mes données ?', a: `Oui. ${frExport}` },
      { q: 'Puis-je essayer sans m’inscrire ?', a: `Oui. [Essayez la démo](${DEMO}) : un espace de démonstration avec des données d’exemple s’ouvre dans votre navigateur, sans compte. Quand vous voulez y mettre vos propres données, créez votre espace gratuit.` },
      { q: 'Ce CRM gratuit convient-il à un club ou une association ?', a: 'Oui. La gestion des membres fait partie de l’offre : membres, foyers, candidatures, renouvellements, cotisations récurrentes, relevés et portail membre, gratuitement jusqu’au 31 décembre 2026. Voir la [gestion des membres](page:membership).' },
      { q: 'Est-ce un CRM gratuit conçu pour la Suisse ?', a: 'Oui. CadenceE a été conçu en Suisse : paiements par facture QR et TWINT, export comptable vers Bexio et Abacus, demandes selon la nLPD, et l’application en français, en allemand et en anglais.' },
    ],
  },
  related: ['pricing', 'features', 'membership'],
  cta: {
    heading: 'Lancez votre CRM gratuit dès aujourd’hui',
    text: 'Toutes les fonctionnalités, toute votre équipe, sans carte de crédit, gratuitement jusqu’au 31 décembre 2026. Ou essayez d’abord la démo, sans compte.',
  },
  afterOffer: {
    eyebrow: 'Offre de lancement',
    h1: 'L’offre de lancement gratuite de CadenceE',
    h1Accent: 'est terminée',
    lead: `CadenceE était gratuit pour toutes les personnes inscrites jusqu’au 31 décembre 2026. Cette offre de lancement est terminée : demandez-nous les tarifs actuels, [essayez la démo](${DEMO}), ou connectez-vous à votre espace de travail.`,
    heroCardTitle: 'Ce que comprend CadenceE',
    heroPoints: [
      'Tous les modules : vente, club, facturation, e-mails et IA',
      'Votre équipe, avec rôles et autorisations',
      'Français, allemand et anglais',
      'Vos données exportables à tout moment',
    ],
    intro: {
      heading: 'L’offre de lancement est terminée',
      paragraphs: [
        'Pendant son offre de lancement, jusqu’au 31 décembre 2026, CadenceE était gratuit pour toutes les personnes inscrites, avec toutes les fonctionnalités. Cette offre a pris fin. Demandez-nous les tarifs qui s’appliquent aujourd’hui ; voir les [tarifs](page:pricing).',
      ],
    },
    sections: [
      {
        heading: 'À qui s’adresse CadenceE',
        paragraphs: ['CadenceE réunit la vente et la vie du club dans un seul CRM. Voir toutes les [fonctionnalités](page:features).'],
        bullets: frAudience,
      },
      {
        heading: 'Vos données ne sont pas captives',
        paragraphs: [`${frExport} Quoi que vous décidiez, vos données restent les vôtres.`],
      },
    ],
    features: { heading: 'Les fonctionnalités de CadenceE', items: frFeatures },
    faq: {
      heading: 'Questions sur l’offre de lancement',
      items: [
        { q: 'CadenceE est-il toujours gratuit ?', a: 'L’offre de lancement, gratuite jusqu’au 31 décembre 2026, est terminée. Demandez-nous les tarifs actuels ; nous répondons à chaque demande.' },
        { q: 'Faut-il une carte de crédit pour s’inscrire ?', a: 'Non. L’inscription demande seulement votre nom, votre adresse e-mail et un mot de passe, puis un nom pour votre espace de travail.' },
        { q: 'Puis-je essayer sans m’inscrire ?', a: `Oui. [Essayez la démo](${DEMO}) : un espace de démonstration avec des données d’exemple s’ouvre dans votre navigateur, sans compte.` },
        { q: 'Puis-je exporter mes données ?', a: `Oui. ${frExport}` },
        { q: 'Quelles fonctionnalités sont incluses ?', a: 'Toutes : prospects et ventes, contrats en signature électronique, membres, événements et hôtellerie-restauration, facturation et paiements, automatisation des e-mails, rapports et assistant IA. Voir toutes les [fonctionnalités](page:features).' },
      ],
    },
    cta: {
      heading: 'Demandez les tarifs actuels',
      text: 'Demandez-nous les tarifs ou une démonstration, ou créez votre espace de travail.',
    },
  },
};

// ---------------------------------------------------------------------------
// Deutsch (Schweizer Schreibweise: ss statt ß)

const deAudience: string[] = [
  '[Privatclubs](page:private-clubs) und Vereine: Anfragen, Aufnahmeanträge, Mitglieder und Beiträge',
  '[Gastronomie und Hotellerie](page:hospitality): Restaurants, Hotels und Eventlocations, mit Reservationen, Privatanlässen und Konsumationen',
  '[Verkaufsteams](page:sales): jeder Lead bis zur Anzahlung verfolgt',
  'Schweizer Betriebe, die [QR-Rechnung, TWINT, Bexio und Abacus](page:swiss) brauchen',
];

const deExport =
  'Listen wie Leads und Aktivitäten lassen sich als CSV exportieren, der Buchhaltungsexport geht nach Bexio oder Abacus, und ein Auskunftsbegehren exportiert alles, was zu einer Person gespeichert ist, nach DSGVO und DSG.';

const deFeatures: PageContent['features']['items'] = [
  { icon: 'leads', title: 'Leads in fünf Phasen', text: 'Kontaktaufnahme, Termin, Vertrag, Unterschrift, Anzahlung, mit Scoring und Zuweisung.' },
  { icon: 'signature', title: 'Verträge mit E-Signatur', text: 'Online ohne Konto unterschrieben, Anzahlung bei Unterzeichnung.' },
  { icon: 'members', title: 'Mitgliederverwaltung', text: 'Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen und Firmenmitgliedschaften.' },
  { icon: 'reservations', title: 'Anlässe und Reservationen', text: 'Wartelisten, Privatanlässe, Besuche und Check-ins.' },
  { icon: 'tabs', title: 'Konsumationen und Kasse', text: 'Bar- und Restaurantrechnungen, Hauskonten und Lightspeed-Kasse.' },
  { icon: 'payments', title: 'Schweizer Zahlungen', text: 'Karte, TWINT, SEPA und QR-Rechnung, mit Abgleich der Bankdatei.' },
  { icon: 'automations', title: 'E-Mail-Automatisierung', text: 'Postausgang, Sequenzen, Automationen, Newsletter und Kampagnen mit Einwilligung.' },
  { icon: 'ai', title: 'KI-Assistent', text: 'Auf Basis von Claude: schlägt Änderungen vor, die Sie bestätigen, schreibt E-Mails und bereitet das Morgenbriefing vor.' },
  { icon: 'roles', title: 'Für das ganze Team', text: 'Rollen und Berechtigungen, Audit-Log, Passkeys und Zwei-Faktor-Authentifizierung.' },
];

const deSteps: PageContent['steps'] = {
  heading: 'In wenigen Minuten starten',
  intro: 'Kein Verkaufsgespräch und keine Kreditkarte: vier Schritte von dieser Seite zum laufenden CRM.',
  items: [
    { title: 'Konto erstellen', text: 'Name, E-Mail-Adresse und Passwort. Keine Kreditkarte.' },
    { title: 'Arbeitsbereich benennen', text: 'Mit dem Namen Ihres Clubs oder Ihrer Firma; alle Funktionen sind bereits eingeschaltet.' },
    { title: 'Team einladen', text: 'Jeder Person die Rolle und die Berechtigungen geben, die sie braucht.' },
    { title: 'Daten übernehmen', text: 'Leads, Kontakte und Mitglieder aus Tabellen importieren, zuerst mit Probelauf, bei Bedarf rückgängig.' },
  ],
};

export const deFreeCrm: PageContent = {
  eyebrow: 'Kostenloses CRM',
  h1: 'Kostenloses CRM mit allen Funktionen,',
  h1Accent: 'keine abgespeckte Gratisversion',
  lead: `CadenceE ist bis 31. Dezember 2026 kostenlos: alle Funktionen, für Ihr ganzes Team, ohne Kreditkarte und ohne abgestufte Pakete. Eine CRM-Software für Clubs, Vereine, Gastronomie und Verkaufsteams, entwickelt in der Schweiz, auf Deutsch, Französisch und Englisch. In wenigen Minuten registriert, oder zuerst ohne Konto [die Demo ausprobieren](${DEMO}).`,
  heroCardTitle: 'Kostenlos bis 31. Dezember 2026',
  heroPoints: [
    'Alle Module: Vertrieb, Club, Abrechnung, E-Mail und KI',
    'Ihr ganzes Team, mit Rollen und Berechtigungen',
    'Keine Kreditkarte: Name, E-Mail, Passwort',
    'Deutsch, Französisch und Englisch',
    'Ihre Daten jederzeit exportierbar',
  ],
  intro: {
    heading: 'Ein kostenloses CRM, das keine Sparversion ist',
    paragraphs: [
      'Viele kostenlose CRM-Angebote sind die unterste Stufe eines Bezahlprodukts: ein paar Funktionen, der Rest hinter einem Upgrade. Das Einführungsangebot von CadenceE funktioniert umgekehrt. Bis 31. Dezember 2026 gibt es ein einziges Paket, es kostet nichts, und es enthält alle Bereiche: Leads und Vertrieb, Verträge mit E-Signatur, Mitgliederverwaltung, Anlässe und Gastronomie, Abrechnung und Zahlungen, E-Mail-Automatisierung, Berichte und den KI-Assistenten.',
      'Es ist keine Testversion, die nach ein paar Tagen endet, und nichts bleibt einem teureren Paket vorbehalten. Für die Registrierung brauchen Sie nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich: weder Kreditkarte noch Zahlungsangaben. Die Preise für 2027 geben wir lange vor dem Ende des Angebots bekannt, und weil keine Zahlungsangaben gespeichert sind, kann nichts automatisch belastet werden.',
    ],
  },
  sections: [
    {
      heading: 'Für wen dieses kostenlose CRM gedacht ist',
      paragraphs: [
        'CadenceE vereint Vertrieb und Clubbetrieb in einem CRM: Es passt zu Teams, die dieselben Menschen gewinnen, betreuen und abrechnen. Alle [Funktionen](page:features) im Überblick.',
      ],
      bullets: deAudience,
    },
    {
      heading: 'Mitgliederverwaltung kostenlos, für Clubs und Vereine',
      paragraphs: [
        'Für einen Club oder Verein ist CadenceE auch eine kostenlose Mitgliederverwaltung. Mitglieder gehören zu Haushalten, Firmen können Firmenmitgliedschaften halten, Aufnahmeanträge kommen über ein öffentliches Formular und werden in einer Liste geprüft, und jede Erneuerung trägt eine Risikobewertung. Wiederkehrende Beiträge und monatliche PDF-Auszüge werden per Karte, TWINT, SEPA oder QR-Rechnung bezahlt, und Mitglieder melden sich mit einem Link per E-Mail in ihrem Portal an.',
      ],
      link: 'membership',
    },
    {
      heading: 'Ein kostenloses Vertriebs-CRM, bis zur Anzahlung',
      paragraphs: [
        'Der Lead-Tracker begleitet jede Opportunity durch fünf Phasen: Kontaktaufnahme, Termin, Vertrag, Unterschrift und Anzahlung. Leads werden bewertet, zugewiesen und vor Dubletten geschützt; Verträge entstehen aus Ihren Vorlagen, werden [online ohne Konto unterschrieben](page:e-signature), und die Anzahlung wird bei der Unterzeichnung eingezogen. Der KI-Texter entwirft die E-Mails dazwischen, die eine Person prüft und versendet.',
      ],
      link: 'sales',
    },
    {
      heading: 'Ein kostenloses CRM aus der Schweiz',
      paragraphs: [
        'CadenceE wurde in der Schweiz entwickelt, zusammen mit dem Pavillon 46, einem Privatclub in La Croix-sur-Lutry (VD), der es täglich nutzt. Bezahlt wird mit QR-Rechnung und TWINT, der Buchhaltungsexport geht nach Bexio oder Abacus, und Auskunfts- und Löschbegehren werden nach DSG und DSGVO bearbeitet. Die ganze App gibt es auf Deutsch, Französisch und Englisch, nach Wahl jeder Person. Mehr zum [Schweizer CRM](page:swiss).',
      ],
    },
    {
      heading: 'Ihre Daten sind nicht eingesperrt',
      paragraphs: [
        `Ein kostenloses CRM lohnt sich nur, wenn Sie mit Ihren Daten auch wieder gehen können. ${deExport} Wie auch immer Sie sich 2027 entscheiden: Ihre Daten bleiben Ihre Daten.`,
      ],
    },
    {
      heading: 'Nach dem 31. Dezember 2026',
      paragraphs: [
        'Die Preise für 2027 geben wir lange vor dem Ende des Einführungsangebots bekannt, damit Sie in Ruhe entscheiden können. Bei der Registrierung werden keine Zahlungsangaben verlangt: Es wird also nichts automatisch belastet, und Sie entscheiden, ob Sie weitermachen, wenn Sie den Preis kennen. Alle Details zum Angebot auf der Seite [Preise](page:pricing).',
      ],
    },
  ],
  features: { heading: 'Alles, was im kostenlosen CRM enthalten ist', items: deFeatures },
  steps: deSteps,
  faq: {
    heading: 'Kostenloses CRM: häufige Fragen',
    items: [
      { q: 'Ist CadenceE wirklich kostenlos?', a: 'Ja, bis 31. Dezember 2026. Während des Einführungsangebots gibt es ein Paket, es kostet nichts, und alle Funktionen sind für alle enthalten, die sich registrieren. Es ist keine Testversion, die nach ein paar Tagen endet.' },
      { q: 'Gibt es Einschränkungen oder Pakete?', a: 'Keine abgestuften Pakete: Nichts bleibt einem Bezahlangebot vorbehalten, und Ihr ganzes Team erhält alle Module, mit Rollen und Berechtigungen. Was das Angebot hat, ist ein Enddatum: der 31. Dezember 2026.' },
      { q: 'Brauche ich eine Kreditkarte?', a: 'Nein. Die Registrierung verlangt nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich. Zahlungsangaben werden keine verlangt.' },
      { q: 'Was passiert 2027?', a: 'Die Preise für 2027 geben wir lange vor dem Ende des Angebots bekannt. Weil keine Zahlungsangaben gespeichert sind, wird nichts automatisch belastet: Sie entscheiden, ob Sie weitermachen, wenn Sie den Preis kennen. Mehr zu den [Preisen](page:pricing).' },
      { q: 'Kann ich meine Daten exportieren?', a: `Ja. ${deExport}` },
      { q: 'Kann ich CadenceE ohne Registrierung testen?', a: `Ja. [Probieren Sie die Demo aus](${DEMO}): Ein Demo-Arbeitsbereich mit Beispieldaten läuft in Ihrem Browser, ganz ohne Konto. Wenn Sie mit Ihren eigenen Daten arbeiten möchten, erstellen Sie Ihren kostenlosen Arbeitsbereich.` },
      { q: 'Ist die Mitgliederverwaltung auch kostenlos?', a: 'Ja. Die Mitgliederverwaltung gehört zum Angebot: Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen, wiederkehrende Beiträge, Auszüge und Mitgliederportal, kostenlos bis 31. Dezember 2026. Mehr zur [Mitgliederverwaltung](page:membership).' },
      { q: 'Ist das ein kostenloses CRM aus der Schweiz?', a: 'Ja. CadenceE wurde in der Schweiz entwickelt: Zahlungen per QR-Rechnung und TWINT, Buchhaltungsexport nach Bexio und Abacus, Datenschutzbegehren nach DSG und die App auf Deutsch, Französisch und Englisch.' },
    ],
  },
  related: ['pricing', 'features', 'membership'],
  cta: {
    heading: 'Starten Sie heute Ihr kostenloses CRM',
    text: 'Alle Funktionen, Ihr ganzes Team, keine Kreditkarte, kostenlos bis 31. Dezember 2026. Oder probieren Sie zuerst die Demo aus, ohne Konto.',
  },
  afterOffer: {
    eyebrow: 'Einführungsangebot',
    h1: 'Das kostenlose Einführungsangebot von CadenceE',
    h1Accent: 'ist beendet',
    lead: `CadenceE war für alle kostenlos, die sich bis 31. Dezember 2026 registriert haben. Dieses Einführungsangebot ist beendet: Fragen Sie uns nach den aktuellen Preisen, [probieren Sie die Demo aus](${DEMO}), oder melden Sie sich in Ihrem Arbeitsbereich an.`,
    heroCardTitle: 'Was CadenceE kann',
    heroPoints: [
      'Alle Module: Vertrieb, Club, Abrechnung, E-Mail und KI',
      'Ihr Team, mit Rollen und Berechtigungen',
      'Deutsch, Französisch und Englisch',
      'Ihre Daten jederzeit exportierbar',
    ],
    intro: {
      heading: 'Das Einführungsangebot ist beendet',
      paragraphs: [
        'Während des Einführungsangebots bis 31. Dezember 2026 war CadenceE für alle kostenlos, die sich registriert haben, mit allen Funktionen. Dieses Angebot ist abgelaufen. Fragen Sie uns nach den Preisen, die heute gelten; mehr auf der Seite [Preise](page:pricing).',
      ],
    },
    sections: [
      {
        heading: 'Für wen CadenceE gedacht ist',
        paragraphs: ['CadenceE vereint Vertrieb und Clubbetrieb in einem CRM. Alle [Funktionen](page:features) im Überblick.'],
        bullets: deAudience,
      },
      {
        heading: 'Ihre Daten sind nicht eingesperrt',
        paragraphs: [`${deExport} Wie auch immer Sie sich entscheiden: Ihre Daten bleiben Ihre Daten.`],
      },
    ],
    features: { heading: 'Die Funktionen von CadenceE', items: deFeatures },
    faq: {
      heading: 'Fragen zum Einführungsangebot',
      items: [
        { q: 'Ist CadenceE noch kostenlos?', a: 'Das Einführungsangebot, kostenlos bis 31. Dezember 2026, ist beendet. Fragen Sie uns nach den aktuellen Preisen; wir beantworten jede Anfrage.' },
        { q: 'Brauche ich für die Registrierung eine Kreditkarte?', a: 'Nein. Die Registrierung verlangt nur Ihren Namen, Ihre E-Mail-Adresse und ein Passwort, danach einen Namen für Ihren Arbeitsbereich.' },
        { q: 'Kann ich CadenceE ohne Registrierung testen?', a: `Ja. [Probieren Sie die Demo aus](${DEMO}): Ein Demo-Arbeitsbereich mit Beispieldaten läuft in Ihrem Browser, ganz ohne Konto.` },
        { q: 'Kann ich meine Daten exportieren?', a: `Ja. ${deExport}` },
        { q: 'Welche Funktionen sind enthalten?', a: 'Alle: Leads und Vertrieb, Verträge mit E-Signatur, Mitglieder, Anlässe und Gastronomie, Abrechnung und Zahlungen, E-Mail-Automatisierung, Berichte und KI-Assistent. Alle [Funktionen](page:features) im Überblick.' },
      ],
    },
    cta: {
      heading: 'Fragen Sie nach den aktuellen Preisen',
      text: 'Fragen Sie uns nach den Preisen oder einer Demo, oder erstellen Sie Ihren Arbeitsbereich.',
    },
  },
};
