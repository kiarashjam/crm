// The shape of a marketing page's words. Each language's file (en.ts, fr.ts, de.ts) fills
// one of these per page; MarketingPage lays it out.
//
// In any paragraph, bullet or answer, "[label](page:sales)" links to that page in the same
// language and "[label](/login)" to an address on the site: the pages link to each other in
// their own sentences, not only in lists.

import type { MarketingPageId } from '../routes';

export const FEATURE_ICONS = [
  'leads', 'score', 'duplicates', 'partner', 'import', 'forms', 'webhook',
  'deals', 'contract', 'signature', 'deposit', 'paper',
  'members', 'households', 'applications', 'renewals', 'corporate', 'portal',
  'events', 'reservations', 'private-event', 'visits',
  'tabs', 'pos', 'house-account', 'dues', 'statements', 'payments', 'bank', 'accounting', 'ledger',
  'loyalty', 'gift-card', 'donation',
  'outbox', 'sequences', 'automations', 'newsletter', 'booking', 'campaigns', 'consent',
  'reports', 'forecast', 'audit', 'privacy',
  'ai', 'writer', 'brief',
  'sign-in', 'two-factor', 'roles', 'workspaces', 'languages', 'health', 'swiss',
] as const;

export type FeatureIcon = (typeof FEATURE_ICONS)[number];

export interface ContentSection {
  heading: string;
  paragraphs: string[];
  bullets?: string[];
  /** A page this section is a summary of: shown as a "learn more" link under it. */
  link?: MarketingPageId;
}

export interface FeatureItem {
  icon: FeatureIcon;
  title: string;
  text: string;
}

export interface StepItem {
  title: string;
  text: string;
}

export interface FaqItem {
  q: string;
  a: string;
}

export interface PageContent {
  /** The badge above the H1. */
  eyebrow: string;
  /** The H1; with h1Accent, its last words in the brand gradient. One per page, unique. */
  h1: string;
  h1Accent?: string;
  /** The paragraph under the H1. */
  lead: string;
  /** Three to five short points in the card beside the hero. */
  heroPoints: string[];
  heroCardTitle: string;
  /** The opening section under the hero. */
  intro: { heading: string; paragraphs: string[] };
  sections: ContentSection[];
  features: { heading: string; intro?: string; items: FeatureItem[] };
  steps: { heading: string; intro?: string; items: StepItem[] };
  faq: { heading: string; items: FaqItem[] };
  /** Pages linked at the foot of this one. */
  related: MarketingPageId[];
  /** On a home page: a grid of every topic page. */
  showSolutions?: { heading: string; intro: string };
  cta: { heading: string; text: string };
  /**
   * Where a page is about the launch offer (the pricing page), its words once the offer
   * has ended (from 1 January 2027): they replace the ones above.
   */
  afterOffer?: Partial<Omit<PageContent, 'afterOffer'>>;
}

/** The labels the page template's own sections use, per language. */
export interface TemplateStrings {
  featuresEyebrow: string;
  stepsEyebrow: string;
  faqEyebrow: string;
  solutionsEyebrow: string;
  stepLabel: string;
}

export interface LanguageContent {
  template: TemplateStrings;
  pages: Partial<Record<MarketingPageId, PageContent>>;
}
