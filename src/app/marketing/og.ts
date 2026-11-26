// What each Open Graph image (1200×630, public/og/) says: generated once by
// scripts/seo/og-images.mjs from these specs and committed. The head picks the -offer
// image of the pages in OFFER_OG_PAGES (home, pricing, free CRM) only while the launch
// offer runs.

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
/** Its H1 while the launch offer runs, on its -offer image. */
const EN_HOME_OFFER = { ...EN_HOME, accent: 'One free Swiss CRM.' };

export function ogImageSpecs(): OgImageSpec[] {
  const specs: OgImageSpec[] = [];
  for (const page of MARKETING_PAGES) {
    for (const lang of SITE_LANGS) {
      const words = CONTENT[lang].pages[page.id];
      const enHome = lang === 'en' && page.id === 'home';
      const base = enHome
        ? EN_HOME
        : { kicker: words?.eyebrow ?? PAGE_META[lang][page.id].nav, title: words?.h1 ?? PAGE_META[lang][page.id].title, accent: words?.h1Accent };
      const footer = `www.cadencee.ai · ${SITE_STRINGS[lang].builtIn}`;
      // A page whose words change when the offer ends (pricing, the free-CRM page, the French
      // and German home pages) says its offer words on the -offer image and its after-offer
      // words on the plain one, which is the only one shown from 1 January 2027.
      const after = words?.afterOffer;
      const plain = after
        ? {
            kicker: after.eyebrow ?? base.kicker,
            title: after.h1 ?? base.title,
            accent: after.h1 !== undefined ? after.h1Accent : base.accent,
          }
        : base;
      specs.push({ path: ogImagePath(page.id, lang), lang, footer, ...plain });
      if ((OFFER_OG_PAGES as readonly string[]).includes(page.id)) {
        const offer = enHome ? EN_HOME_OFFER : base;
        specs.push({ path: ogImagePath(page.id, lang, true), lang, footer, ...offer, badge: LAUNCH_OFFER_COPY[lang].badge });
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
