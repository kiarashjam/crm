// What search results and link previews show for each public page, in each language, and
// the few words the site's header, footer and 404 page share.
//
// Titles stay within 60 characters and descriptions within 155 (meta.test.ts checks both,
// and that no two pages share either). They are written for what people in Switzerland
// search: "CRM pour club privé", "Mitgliederverwaltung Software", "Swiss CRM", and the like.

import { isLaunchOfferActive } from '@/app/lib/launchOffer';
import type { MarketingPageId } from './routes';
import type { SiteLang } from './site';

export interface PageMeta {
  /** The <title>, also og:title. */
  title: string;
  /** The meta description, also og:description. */
  description: string;
  /** The short name navigation, breadcrumbs and cards use. */
  nav: string;
  /** While the launch offer runs (until 31 December 2026), these take the place of title and description. */
  offerTitle?: string;
  offerDescription?: string;
}

export const PAGE_META: Record<SiteLang, Record<MarketingPageId, PageMeta>> = {
  en: {
    home: {
      title: 'CadenceE – Swiss CRM for Private Clubs and Sales Teams',
      description: 'The Swiss CRM that runs a whole club and its sales: leads, e-signed contracts, members, QR-bill and TWINT billing, email and AI. English, French, German.',
      nav: 'Home',
      offerDescription: 'The Swiss CRM for private clubs and sales teams: leads, e-signed contracts, members, QR-bill and TWINT billing, email and AI. Free until end of 2026.',
    },
    features: {
      title: 'CadenceE Features: Leads, Members, Billing, Email and AI',
      description: 'Every CadenceE feature: a five-phase lead tracker, e-signature contracts, memberships, tabs and billing, email automation, reports and an AI assistant.',
      nav: 'Features',
      offerDescription: 'Every CadenceE feature: five-phase lead tracker, e-signature contracts, memberships, billing, email automation and an AI assistant. Free until end of 2026.',
    },
    'private-clubs': {
      title: "CRM for Private Members' Clubs | CadenceE",
      description: "Run a private members' club in one CRM: enquiries and applications, contracts and deposits, members and households, events, tabs and renewals.",
      nav: 'Private clubs',
    },
    membership: {
      title: 'Membership Management Software for Clubs | CadenceE',
      description: 'Members, households, applications, renewals with at-risk scores and corporate memberships, with a member portal and recurring dues. Made for clubs.',
      nav: 'Membership management',
    },
    hospitality: {
      title: 'Hospitality CRM for Restaurants, Hotels & Events | CadenceE',
      description: 'A CRM for restaurants, hotels and event venues: reservations with waitlists, private-event hire, bar and restaurant tabs, Lightspeed POS and guest email.',
      nav: 'Hospitality',
    },
    sales: {
      title: 'Sales CRM with Lead Tracking and AI Sales Writer | CadenceE',
      description: 'Track every lead through five phases, from outreach to deposit, with lead scoring, assignment, duplicate protection, a deals board and an AI sales writer.',
      nav: 'Sales CRM',
    },
    'e-signature': {
      title: 'Contracts with E-Signature and Deposits | CadenceE',
      description: 'Send contracts from templates, let clients sign online without an account and collect the deposit at signing by card or Swiss QR-bill. Paper contracts too.',
      nav: 'E-signature contracts',
    },
    swiss: {
      title: 'Swiss CRM with QR-Bill, TWINT, Bexio and Abacus | CadenceE',
      description: 'A CRM built in Switzerland: Swiss QR-bill and TWINT payments, Bexio and Abacus exports, French, German and English, and privacy requests under the FADP.',
      nav: 'Swiss CRM',
    },
    billing: {
      title: 'Club Billing, Dues and Payments Software | CadenceE',
      description: 'Recurring dues, house accounts, bar tabs and monthly PDF statements. Card, TWINT, SEPA and QR-bill payments, bank-file matching and an append-only ledger.',
      nav: 'Billing and payments',
    },
    email: {
      title: 'Email Automation CRM: Sequences and Newsletters | CadenceE',
      description: 'One outbox with retries and bounce handling, sequences, automations, newsletters with a booking page, and campaigns with consent and one-click unsubscribe.',
      nav: 'Email automation',
    },
    ai: {
      title: 'AI CRM Assistant Built on Claude | CadenceE',
      description: 'An AI assistant inside your CRM that proposes changes for you to confirm, writes sales emails and prepares a morning brief every day. Built on Claude.',
      nav: 'AI assistant',
    },
    pricing: {
      title: 'CadenceE Pricing for Clubs and Sales Teams',
      description: 'What CadenceE costs for private clubs, hospitality and sales teams, what every plan includes, and how to get started.',
      nav: 'Pricing',
      offerTitle: 'Pricing: CadenceE Is Free Until 31 December 2026',
      offerDescription: 'CadenceE is free until 31 December 2026: every feature, no credit card required. Sign up now; pricing for 2027 will be announced well before then.',
    },
  },
  fr: {
    home: {
      title: 'CadenceE – CRM suisse pour clubs privés et équipes de vente',
      description: 'Le CRM suisse qui gère tout un club et ses ventes : prospects, contrats signés en ligne, membres, facture QR et TWINT, e-mails et IA. En français.',
      nav: 'Accueil',
      offerDescription: 'Le CRM suisse des clubs privés et équipes de vente : prospects, contrats signés en ligne, membres, facture QR, TWINT et IA. Gratuit jusqu’à fin 2026.',
    },
    features: {
      title: 'Fonctionnalités CadenceE : prospects, membres, facturation',
      description: 'Toutes les fonctionnalités de CadenceE : prospects en cinq phases, contrats signés en ligne, membres, facturation, e-mails, rapports et assistant IA.',
      nav: 'Fonctionnalités',
      offerDescription: 'Toutes les fonctionnalités de CadenceE : prospects en cinq phases, contrats signés en ligne, membres, facturation, e-mails et IA. Gratuit jusqu’à fin 2026.',
    },
    'private-clubs': {
      title: 'CRM pour club privé et club de membres | CadenceE',
      description: 'Gérez votre club privé dans un seul CRM : candidatures, contrats et acomptes, membres et foyers, événements, notes de bar et renouvellements.',
      nav: 'Clubs privés',
    },
    membership: {
      title: 'Logiciel de gestion des membres pour clubs | CadenceE',
      description: "Membres, foyers, candidatures, renouvellements avec score de risque, adhésions d'entreprise, portail membre et cotisations récurrentes.",
      nav: 'Gestion des membres',
    },
    hospitality: {
      title: "CRM pour l'hôtellerie et la restauration | CadenceE",
      description: "Le CRM des restaurants, hôtels et lieux d'événements : réservations avec liste d'attente, privatisations, notes de bar et caisse Lightspeed.",
      nav: 'Hôtellerie-restauration',
    },
    sales: {
      title: 'CRM commercial avec suivi des prospects | CadenceE',
      description: "Suivez chaque prospect en cinq phases, du premier contact à l'acompte : score, attribution, protection contre les doublons, affaires et rédacteur IA.",
      nav: 'CRM commercial',
    },
    'e-signature': {
      title: 'Contrats avec signature électronique et acompte | CadenceE',
      description: "Envoyez vos contrats depuis des modèles, faites-les signer en ligne sans compte et encaissez l'acompte à la signature, par carte ou facture QR.",
      nav: 'Signature électronique',
    },
    swiss: {
      title: 'CRM suisse : facture QR, TWINT, Bexio et Abacus | CadenceE',
      description: "Un CRM conçu en Suisse : facture QR et TWINT, export vers Bexio et Abacus, français, allemand et anglais, demandes d'accès et d'effacement selon la nLPD.",
      nav: 'CRM suisse',
    },
    billing: {
      title: 'Facturation et paiements pour clubs | CadenceE',
      description: 'Cotisations récurrentes, comptes maison, notes de bar et relevés PDF mensuels. Carte, TWINT, SEPA ou facture QR, et rapprochement bancaire.',
      nav: 'Facturation et paiements',
    },
    email: {
      title: 'CRM avec automatisation des e-mails | CadenceE',
      description: "Une boîte d'envoi unique avec relances et rebonds gérés, séquences, automatisations, newsletters avec page de réservation et campagnes avec consentement.",
      nav: 'Automatisation des e-mails',
    },
    ai: {
      title: 'Assistant IA pour votre CRM, basé sur Claude | CadenceE',
      description: 'Un assistant IA dans votre CRM : il propose des modifications que vous confirmez, rédige vos e-mails de vente et prépare un brief chaque matin.',
      nav: 'Assistant IA',
    },
    pricing: {
      title: 'Tarifs de CadenceE pour clubs et équipes de vente',
      description: 'Ce que coûte CadenceE pour les clubs privés, l’hôtellerie-restauration et les équipes de vente, ce qui est inclus, et comment démarrer.',
      nav: 'Tarifs',
      offerTitle: 'Tarifs : CadenceE est gratuit jusqu’au 31 décembre 2026',
      offerDescription: 'CadenceE est gratuit jusqu’au 31 décembre 2026 : toutes les fonctionnalités, sans carte de crédit. Les tarifs 2027 seront annoncés bien avant.',
    },
  },
  de: {
    home: {
      title: 'CadenceE – Schweizer CRM für Privatclubs und Vertrieb',
      description: 'Das Schweizer CRM für Club und Vertrieb: Leads, elektronisch signierte Verträge, Mitglieder, QR-Rechnung und TWINT, E-Mail und KI. Auf Deutsch.',
      nav: 'Startseite',
      offerDescription: 'Das Schweizer CRM für Privatclubs und Vertrieb: Leads, E-Signatur, Mitglieder, QR-Rechnung, TWINT, E-Mail und KI. Kostenlos bis Ende 2026.',
    },
    features: {
      title: 'Funktionen von CadenceE: Leads, Mitglieder, Abrechnung, KI',
      description: 'Alle Funktionen von CadenceE: Lead-Tracker mit fünf Phasen, Verträge mit E-Signatur, Mitglieder, Abrechnung, E-Mail-Automatisierung, Berichte und KI.',
      nav: 'Funktionen',
      offerDescription: 'Alle Funktionen von CadenceE: Lead-Tracker, Verträge mit E-Signatur, Mitglieder, Abrechnung, E-Mail-Automatisierung und KI. Kostenlos bis Ende 2026.',
    },
    'private-clubs': {
      title: "CRM für Privatclubs und Members' Clubs | CadenceE",
      description: 'Führen Sie Ihren Privatclub in einem CRM: Anfragen und Aufnahmeanträge, Verträge und Anzahlungen, Mitglieder, Anlässe, Konsumationen und Erneuerungen.',
      nav: 'Privatclubs',
    },
    membership: {
      title: 'Software für die Mitgliederverwaltung in Clubs | CadenceE',
      description: 'Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen mit Risikobewertung, Firmenmitgliedschaften, Mitgliederportal und wiederkehrende Beiträge.',
      nav: 'Mitgliederverwaltung',
    },
    hospitality: {
      title: 'CRM für Gastronomie, Hotellerie und Events | CadenceE',
      description: 'Das CRM für Restaurants, Hotels und Eventlocations: Reservationen mit Warteliste, Privatanlässe, Bar- und Restaurantrechnungen und Lightspeed-Kasse.',
      nav: 'Gastronomie und Hotellerie',
    },
    sales: {
      title: 'Vertriebs-CRM mit Lead-Management und KI | CadenceE',
      description: 'Jeder Lead in fünf Phasen, vom Erstkontakt bis zur Anzahlung: Lead-Scoring, Zuweisung, Dublettenschutz, Deal-Board und ein KI-Texter für Ihre E-Mails.',
      nav: 'Vertriebs-CRM',
    },
    'e-signature': {
      title: 'Verträge mit elektronischer Signatur | CadenceE',
      description: 'Verträge aus Vorlagen versenden, online ohne Konto unterschreiben lassen und die Anzahlung bei der Unterzeichnung per Karte oder QR-Rechnung einziehen.',
      nav: 'Verträge und E-Signatur',
    },
    swiss: {
      title: 'Schweizer CRM: QR-Rechnung, TWINT, Bexio, Abacus | CadenceE',
      description: 'Ein CRM aus der Schweiz: QR-Rechnung und TWINT, Export nach Bexio und Abacus, Deutsch, Französisch und Englisch, Auskunfts- und Löschbegehren nach DSG.',
      nav: 'Schweizer CRM',
    },
    billing: {
      title: 'Club-Abrechnung, Beiträge und Zahlungen | CadenceE',
      description: 'Wiederkehrende Beiträge, Hauskonten, Barrechnungen und monatliche PDF-Auszüge. Karte, TWINT, SEPA oder QR-Rechnung, mit Abgleich der Bankdatei.',
      nav: 'Abrechnung und Zahlungen',
    },
    email: {
      title: 'CRM mit E-Mail-Automatisierung | CadenceE',
      description: 'Ein Postausgang mit Wiederholungen und Bounce-Handling, Sequenzen und Automationen, Newsletter mit Buchungsseite und Kampagnen mit Einwilligung.',
      nav: 'E-Mail-Automatisierung',
    },
    ai: {
      title: 'KI-Assistent für Ihr CRM, auf Basis von Claude | CadenceE',
      description: 'Ein KI-Assistent im CRM, der Änderungen nur vorschlägt und Sie bestätigen lässt, Verkaufs-E-Mails schreibt und jeden Morgen ein Briefing vorbereitet.',
      nav: 'KI-Assistent',
    },
    pricing: {
      title: 'Preise von CadenceE für Clubs und Verkaufsteams',
      description: 'Was CadenceE für Privatclubs, Gastronomie und Verkaufsteams kostet, was alles enthalten ist und wie Sie starten.',
      nav: 'Preise',
      offerTitle: 'Preise: CadenceE ist bis 31. Dezember 2026 kostenlos',
      offerDescription: 'CadenceE ist bis 31. Dezember 2026 kostenlos: alle Funktionen, keine Kreditkarte nötig. Die Preise für 2027 geben wir lange vorher bekannt.',
    },
  },
};

/**
 * A page's title, description and name as shown at a moment: with the launch offer's
 * words while it runs (until 31 December 2026), without them afterwards.
 */
export function pageMeta(lang: SiteLang, id: MarketingPageId, now: Date | number = Date.now()): PageMeta {
  const meta = PAGE_META[lang][id];
  if (!isLaunchOfferActive(now)) return meta;
  return {
    ...meta,
    title: meta.offerTitle ?? meta.title,
    description: meta.offerDescription ?? meta.description,
  };
}

/** The app's public pages: English, as the app shows them before anyone picks a language. */
export const PUBLIC_PAGE_META: Record<string, Omit<PageMeta, 'nav'>> = {
  '/login': {
    title: 'Sign In or Create Your Workspace | CadenceE',
    description: 'Sign in to CadenceE with your email, Google, Microsoft or a passkey, or create a workspace for your club or sales team.',
  },
  '/help': {
    title: 'Help and Support | CadenceE',
    description: 'Answers to common questions about CadenceE, the Swiss CRM for private clubs and sales teams, and how to reach support.',
  },
  '/privacy': {
    title: 'Privacy Notice | CadenceE',
    description: 'How CadenceE handles personal data under the GDPR and the Swiss Federal Act on Data Protection (FADP), and how to exercise your rights.',
  },
  '/terms': {
    title: 'Terms of Use | CadenceE',
    description: 'The terms that apply when you use CadenceE, the CRM for private clubs, hospitality and sales teams.',
  },
  '/forgot-password': {
    title: 'Forgot Your Password? | CadenceE',
    description: 'Ask for a link to reset your CadenceE password.',
  },
  '/reset-password': {
    title: 'Choose a New Password | CadenceE',
    description: 'Set a new password for your CadenceE account.',
  },
  '/verify-email': {
    title: 'Verify Your Email Address | CadenceE',
    description: 'Confirm the email address of your CadenceE account.',
  },
};

/** The words the site's header, footer, breadcrumbs and 404 page share. */
export interface SiteStrings {
  skipToContent: string;
  mainNav: string;
  menu: string;
  language: string;
  signIn: string;
  getStarted: string;
  /** The header's shorter call to action. */
  headerCta: string;
  /** The header's short labels for the pages it links to. */
  headerNav: Partial<Record<MarketingPageId, string>>;
  requestDemo: string;
  contactUs: string;
  allFeatures: string;
  breadcrumb: string;
  relatedHeading: string;
  learnMore: string;
  builtIn: string;
  usedBy: string;
  footerTagline: string;
  footerProduct: string;
  footerSolutions: string;
  footerResources: string;
  footerLanguages: string;
  help: string;
  privacy: string;
  terms: string;
  rights: string;
  notFoundTitle: string;
  notFoundHeading: string;
  notFoundText: string;
  notFoundHome: string;
  logoAlt: string;
}

export const SITE_STRINGS: Record<SiteLang, SiteStrings> = {
  en: {
    skipToContent: 'Skip to main content',
    mainNav: 'Main',
    menu: 'Menu',
    language: 'Language',
    signIn: 'Sign in',
    getStarted: 'Create your workspace',
    headerCta: 'Get started',
    headerNav: { features: 'Features', 'private-clubs': 'Clubs', hospitality: 'Hospitality', sales: 'Sales', swiss: 'Swiss CRM' },
    requestDemo: 'Request a demo',
    contactUs: 'Contact us',
    allFeatures: 'See all features',
    breadcrumb: 'Breadcrumb',
    relatedHeading: 'Related pages',
    learnMore: 'Learn more',
    builtIn: 'Built in Switzerland',
    usedBy: 'In daily use at Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud.',
    footerTagline: 'The CRM that runs a whole club and its sales, built in Switzerland.',
    footerProduct: 'Product',
    footerSolutions: 'Solutions',
    footerResources: 'Resources',
    footerLanguages: 'Languages',
    help: 'Help',
    privacy: 'Privacy',
    terms: 'Terms',
    rights: 'All rights reserved.',
    notFoundTitle: 'Page Not Found | CadenceE',
    notFoundHeading: 'This page does not exist',
    notFoundText: 'The address may be mistyped, or the page has moved. These links lead somewhere real:',
    notFoundHome: 'Back to the home page',
    logoAlt: 'CadenceE logo',
  },
  fr: {
    skipToContent: 'Aller au contenu principal',
    mainNav: 'Principale',
    menu: 'Menu',
    language: 'Langue',
    signIn: 'Se connecter',
    getStarted: 'Créer votre espace',
    headerCta: 'Commencer',
    headerNav: { features: 'Fonctionnalités', 'private-clubs': 'Clubs', hospitality: 'Hôtellerie', sales: 'Ventes', swiss: 'CRM suisse' },
    requestDemo: 'Demander une démo',
    contactUs: 'Nous contacter',
    allFeatures: 'Voir toutes les fonctionnalités',
    breadcrumb: "Fil d'Ariane",
    relatedHeading: 'Pages associées',
    learnMore: 'En savoir plus',
    builtIn: 'Conçu en Suisse',
    usedBy: 'Utilisé chaque jour au Pavillon 46, club privé à La Croix-sur-Lutry (VD).',
    footerTagline: 'Le CRM qui gère tout un club et ses ventes, conçu en Suisse.',
    footerProduct: 'Produit',
    footerSolutions: 'Solutions',
    footerResources: 'Ressources',
    footerLanguages: 'Langues',
    help: 'Aide',
    privacy: 'Confidentialité',
    terms: 'Conditions',
    rights: 'Tous droits réservés.',
    notFoundTitle: 'Page introuvable | CadenceE',
    notFoundHeading: "Cette page n'existe pas",
    notFoundText: "L'adresse contient peut-être une faute de frappe, ou la page a été déplacée. Ces liens mènent quelque part :",
    notFoundHome: "Retour à l'accueil",
    logoAlt: 'Logo CadenceE',
  },
  de: {
    skipToContent: 'Zum Hauptinhalt springen',
    mainNav: 'Hauptnavigation',
    menu: 'Menü',
    language: 'Sprache',
    signIn: 'Anmelden',
    getStarted: 'Arbeitsbereich erstellen',
    headerCta: 'Loslegen',
    headerNav: { features: 'Funktionen', 'private-clubs': 'Clubs', hospitality: 'Gastronomie', sales: 'Vertrieb', swiss: 'Schweizer CRM' },
    requestDemo: 'Demo anfragen',
    contactUs: 'Kontakt',
    allFeatures: 'Alle Funktionen ansehen',
    breadcrumb: 'Brotkrümelnavigation',
    relatedHeading: 'Verwandte Seiten',
    learnMore: 'Mehr erfahren',
    builtIn: 'Entwickelt in der Schweiz',
    usedBy: 'Täglich im Einsatz im Pavillon 46, einem Privatclub in La Croix-sur-Lutry (VD).',
    footerTagline: 'Das CRM, das einen ganzen Club und seinen Vertrieb führt, entwickelt in der Schweiz.',
    footerProduct: 'Produkt',
    footerSolutions: 'Lösungen',
    footerResources: 'Ressourcen',
    footerLanguages: 'Sprachen',
    help: 'Hilfe',
    privacy: 'Datenschutz',
    terms: 'Nutzungsbedingungen',
    rights: 'Alle Rechte vorbehalten.',
    notFoundTitle: 'Seite nicht gefunden | CadenceE',
    notFoundHeading: 'Diese Seite gibt es nicht',
    notFoundText: 'Vielleicht enthält die Adresse einen Tippfehler, oder die Seite ist umgezogen. Diese Links führen weiter:',
    notFoundHome: 'Zurück zur Startseite',
    logoAlt: 'CadenceE-Logo',
  },
};

/** One line on what CadenceE is, for structured data and llms.txt. */
export const ORGANIZATION_DESCRIPTION =
  'CadenceE is a CRM that runs a whole club and its sales: leads, contracts with e-signature, memberships, billing and payments, email, reports and an AI assistant. Built in Switzerland.';
