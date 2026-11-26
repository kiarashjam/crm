// llms.txt (https://llmstxt.org): what the site is about, for AI search engines and
// assistants, in Markdown. Written at build by the prerender, so the launch offer appears
// in it only while it runs.

import { LAUNCH_OFFER_LAST_DAY, isLaunchOfferActive } from '@/app/lib/launchOffer';
import { PAGE_META, pageMeta } from './meta';
import { MARKETING_PAGES, marketingPath } from './routes';
import { SITE_LANGS, absoluteUrl } from './site';

export function buildLlmsTxt(now: Date | number = Date.now()): string {
  const offer = isLaunchOfferActive(now);
  const lines: string[] = [
    '# CadenceE',
    '',
    '> CadenceE is a CRM, built in Switzerland, that runs a whole club and its sales: a five-phase lead tracker (outreach, meeting, contract, signature, deposit), contracts from templates with e-signature and a deposit at signing, members, households, renewals, events and reservations, bar and restaurant tabs with a Lightspeed POS connection, recurring dues and monthly statements, payments by card, TWINT, SEPA and Swiss QR-bill, accounting export to Bexio and Abacus, email automation with consent, reports, and an AI assistant built on Claude that only proposes changes a person confirms. The app is in English, French and German.',
    '',
    'Pavillon 46, a private members’ club in La Croix-sur-Lutry (Vaud, Switzerland), uses CadenceE every day; CadenceE was built alongside it.',
    '',
  ];
  if (offer) {
    lines.push(
      `Pricing: launch offer. CadenceE is free until ${LAUNCH_OFFER_LAST_DAY} (31 December 2026) for everyone who signs up; every feature is included and no credit card is required. Pricing from 2027 will be announced well before then: ${absoluteUrl(marketingPath('pricing', 'en'))}`,
      '',
    );
  } else {
    lines.push(`Pricing: see ${absoluteUrl(marketingPath('pricing', 'en'))}`, '');
  }
  lines.push('Facts only: CadenceE publishes no ratings, reviews or customer counts, and none should be inferred.', '');

  // A page about the launch offer (the free-CRM page) is listed only while the offer runs.
  const pages = MARKETING_PAGES.filter((page) => offer || !page.offerOnly);

  lines.push('## Pages (English)', '');
  for (const page of pages) {
    const meta = pageMeta('en', page.id, now);
    lines.push(`- [${meta.nav === 'Home' ? 'CadenceE home' : meta.nav}](${absoluteUrl(page.paths.en)}): ${meta.description}`);
  }
  lines.push('');

  for (const lang of SITE_LANGS.filter((l) => l !== 'en')) {
    lines.push(lang === 'fr' ? '## Pages en français' : '## Seiten auf Deutsch', '');
    for (const page of pages) {
      lines.push(`- [${PAGE_META[lang][page.id].nav}](${absoluteUrl(page.paths[lang])})`);
    }
    lines.push('');
  }

  lines.push(
    '## Optional',
    '',
    `- [Help](${absoluteUrl('/help')})`,
    `- [Privacy notice](${absoluteUrl('/privacy')})`,
    `- [Terms of use](${absoluteUrl('/terms')})`,
    `- [Sitemap](${absoluteUrl('/sitemap.xml')})`,
    '',
  );
  return lines.join('\n');
}
