// Structured data (schema.org JSON-LD) for the public site.
//
// Only what is true: no rating and no review. SoftwareApplication carries an `offers` only
// while the launch offer runs (free until 31 December 2026); after that it has none until
// a real price is published: add it here then.

import { LAUNCH_OFFER_LAST_DAY, isLaunchOfferActive } from '@/app/lib/launchOffer';
import { ORGANIZATION_DESCRIPTION } from './meta';
import { SITE_LANGS, SITE_LOGO_PATH, SITE_NAME, SITE_URL, absoluteUrl, type SiteLang } from './site';

export type JsonLd = Record<string, unknown>;

const ORG_ID = `${SITE_URL}/#organization`;
const WEBSITE_ID = `${SITE_URL}/#website`;
const SOFTWARE_ID = `${SITE_URL}/#software`;

export function organizationJsonLd(): JsonLd {
  return {
    '@type': 'Organization',
    '@id': ORG_ID,
    name: SITE_NAME,
    alternateName: 'Cadence Engine',
    url: `${SITE_URL}/`,
    logo: {
      '@type': 'ImageObject',
      url: absoluteUrl(SITE_LOGO_PATH),
      width: 512,
      height: 512,
    },
    description: ORGANIZATION_DESCRIPTION,
    address: { '@type': 'PostalAddress', addressCountry: 'CH' },
  };
}

export function websiteJsonLd(): JsonLd {
  return {
    '@type': 'WebSite',
    '@id': WEBSITE_ID,
    name: SITE_NAME,
    url: `${SITE_URL}/`,
    inLanguage: [...SITE_LANGS],
    publisher: { '@id': ORG_ID },
  };
}

/** What the product does, in the reader's language, for SoftwareApplication.featureList. */
const FEATURE_LIST: Record<SiteLang, string[]> = {
  en: [
    'Five-phase lead tracker: outreach, meeting, contract, signature, deposit',
    'Lead scoring, assignment and duplicate protection',
    'Contracts from templates with e-signature and deposits at signing',
    'Members, households, applications, renewals and corporate memberships',
    'Events, reservations with waitlists and private-event hire',
    'Bar and restaurant tabs, Lightspeed POS connection and house accounts',
    'Recurring dues, monthly PDF statements and bank-file matching',
    'Payments by card, TWINT, SEPA and Swiss QR-bill',
    'Accounting export to Bexio and Abacus',
    'Email outbox, sequences, automations, newsletters and campaigns',
    'Reports, forecast, audit log and GDPR / FADP privacy requests',
    'AI assistant built on Claude, AI sales writer and daily morning brief',
    'Sign-in with Google, Microsoft or a passkey, two-factor authentication',
    'French, German and English',
  ],
  fr: [
    'Suivi des prospects en cinq phases : prise de contact, rendez-vous, contrat, signature, acompte',
    'Score, attribution et protection contre les doublons',
    'Contrats depuis des modèles, signature électronique et acompte à la signature',
    "Membres, foyers, candidatures, renouvellements et adhésions d'entreprise",
    "Événements, réservations avec liste d'attente et privatisations",
    'Notes de bar et de restaurant, caisse Lightspeed et comptes maison',
    'Cotisations récurrentes, relevés PDF mensuels et rapprochement bancaire',
    'Paiement par carte, TWINT, SEPA et facture QR',
    'Export comptable vers Bexio et Abacus',
    "Boîte d'envoi, séquences, automatisations, newsletters et campagnes",
    "Rapports, prévisions, journal d'audit et demandes RGPD / nLPD",
    'Assistant IA basé sur Claude, rédacteur commercial IA et brief du matin',
    'Connexion avec Google, Microsoft ou une clé d’accès, double authentification',
    'Français, allemand et anglais',
  ],
  de: [
    'Lead-Tracker mit fünf Phasen: Kontaktaufnahme, Termin, Vertrag, Unterschrift, Anzahlung',
    'Lead-Scoring, Zuweisung und Dublettenschutz',
    'Verträge aus Vorlagen mit E-Signatur und Anzahlung bei Unterzeichnung',
    'Mitglieder, Haushalte, Aufnahmeanträge, Erneuerungen und Firmenmitgliedschaften',
    'Anlässe, Reservationen mit Warteliste und Privatanlässe',
    'Bar- und Restaurantrechnungen, Lightspeed-Kasse und Hauskonten',
    'Wiederkehrende Beiträge, monatliche PDF-Auszüge und Abgleich der Bankdatei',
    'Zahlung per Karte, TWINT, SEPA und QR-Rechnung',
    'Buchhaltungsexport nach Bexio und Abacus',
    'Postausgang, Sequenzen, Automationen, Newsletter und Kampagnen',
    'Berichte, Prognose, Audit-Log und Datenschutzbegehren nach DSGVO / DSG',
    'KI-Assistent auf Basis von Claude, KI-Texter und tägliches Morgenbriefing',
    'Anmeldung mit Google, Microsoft oder Passkey, Zwei-Faktor-Authentifizierung',
    'Deutsch, Französisch und Englisch',
  ],
};

/** The launch offer as a schema.org Offer: price 0 CHF until 31 December 2026. */
export function launchOfferJsonLd(pricingUrl: string): JsonLd {
  return {
    '@type': 'Offer',
    price: '0',
    priceCurrency: 'CHF',
    priceValidUntil: LAUNCH_OFFER_LAST_DAY,
    availability: 'https://schema.org/InStock',
    url: pricingUrl,
  };
}

export function softwareApplicationJsonLd(
  lang: SiteLang,
  description: string,
  now: Date | number = Date.now(),
  pricingPath = '/pricing',
): JsonLd {
  const offer = isLaunchOfferActive(now) ? { offers: launchOfferJsonLd(absoluteUrl(pricingPath)) } : {};
  return {
    '@type': 'SoftwareApplication',
    '@id': SOFTWARE_ID,
    name: SITE_NAME,
    applicationCategory: 'BusinessApplication',
    applicationSubCategory: 'CRM',
    operatingSystem: 'Web',
    url: `${SITE_URL}/`,
    description,
    inLanguage: lang,
    availableLanguage: ['en', 'fr', 'de'],
    featureList: FEATURE_LIST[lang],
    publisher: { '@id': ORG_ID },
    ...offer,
  };
}

export interface Crumb {
  name: string;
  path: string;
}

export function breadcrumbJsonLd(crumbs: Crumb[]): JsonLd {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: crumbs.map((crumb, i) => ({
      '@type': 'ListItem',
      position: i + 1,
      name: crumb.name,
      item: absoluteUrl(crumb.path),
    })),
  };
}

export interface FaqEntry {
  q: string;
  a: string;
}

export function faqPageJsonLd(faq: FaqEntry[], lang: SiteLang): JsonLd {
  return {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    inLanguage: lang,
    mainEntity: faq.map((entry) => ({
      '@type': 'Question',
      name: entry.q,
      acceptedAnswer: { '@type': 'Answer', text: stripLinkMarkup(entry.a) },
    })),
  };
}

/** A @graph of several nodes under one @context. */
export function graphJsonLd(nodes: JsonLd[]): JsonLd {
  return { '@context': 'https://schema.org', '@graph': nodes };
}

/** "[label](page:id)" link markup reads as its label in structured data. */
export function stripLinkMarkup(text: string): string {
  return text.replace(/\[([^\]]+)\]\((?:page:[a-z-]+|\/[^)]*)\)/g, '$1');
}

/**
 * JSON for a <script type="application/ld+json">: "<" escaped so a "</script>" inside a
 * string cannot end the element early.
 */
export function serializeJsonLd(data: JsonLd): string {
  return JSON.stringify(data)
    .replace(/</g, "\\u003c")
    .replace(new RegExp(String.fromCharCode(0x2028), "g"), "\\u2028")
    .replace(new RegExp(String.fromCharCode(0x2029), "g"), "\\u2029");
}
