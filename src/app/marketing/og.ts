// What each Open Graph image (1200×630, public/og/) says: generated once by
// scripts/seo/og-images.mjs from these specs and committed. The head picks the -offer
// image of the home and pricing pages only while the launch offer runs.

import { LAUNCH_OFFER_COPY } from '@/app/lib/launchOffer';
import de from './content/de';
import en from './content/en';
import fr from './content/fr';
import type { LanguageContent } from './content/types';
import { PAGE_META, SITE_STRINGS } from './meta';
import { MARKETING_PAGES } from './routes';
import { DEFAULT_OG_IMAGE, OFFER_OG_PAGES, ogImagePath } from './seo';
import { SITE_LANGS, type SiteLang } from './site';

export interface OgImageSpec {
  /** Where it is served: "/og/sales-en.png". */
  path: string;
  lang: SiteLang;
  kicker: string;
  title: string;
  /** The title's last words, in the brand gradient. */
  accent?: string;
  footer: string;
  /** The launch offer's badge, on the -offer images. */
  badge?: string;
}

const CONTENT: Record<SiteLang, LanguageContent> = { en, fr, de };

/** The English home page is the animated Homepage, not a content entry: its words are here. */
const EN_HOME = { kicker: 'Swiss CRM for private clubs and sales teams', title: 'Run your club. Close your sales.', accent: 'One Swiss CRM.' };

export function ogImageSpecs(): OgImageSpec[] {
  const specs: OgImageSpec[] = [];
  for (const page of MARKETING_PAGES) {
    for (const lang of SITE_LANGS) {
      const words = CONTENT[lang].pages[page.id];
      const base = lang === 'en' && page.id === 'home'
        ? EN_HOME
        : { kicker: words?.eyebrow ?? PAGE_META[lang][page.id].nav, title: words?.h1 ?? PAGE_META[lang][page.id].title, accent: words?.h1Accent };
      const footer = `www.cadencee.ai · ${SITE_STRINGS[lang].builtIn}`;
      // The pricing page's own words are the offer's; its plain image uses the after-offer ones.
      const plain = page.id === 'pricing' && words?.afterOffer
        ? { ...base, title: words.afterOffer.h1 ?? base.title, accent: words.afterOffer.h1Accent ?? base.accent }
        : base;
      specs.push({ path: ogImagePath(page.id, lang), lang, footer, ...plain });
      if ((OFFER_OG_PAGES as readonly string[]).includes(page.id)) {
        specs.push({ path: ogImagePath(page.id, lang, true), lang, footer, ...base, badge: LAUNCH_OFFER_COPY[lang].badge });
      }
    }
  }
  specs.push({
    path: DEFAULT_OG_IMAGE,
    lang: 'en',
    kicker: 'CadenceE · Cadence Engine',
    title: 'The CRM that runs your club',
    accent: 'and its sales.',
    footer: `www.cadencee.ai · ${SITE_STRINGS.en.builtIn}`,
  });
  return specs;
}
