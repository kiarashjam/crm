// The launch offer: CadenceE is free until 31 December 2026, for everyone who signs up
// before then. Every place the site mentions it (badges, strips, "Start free", the pricing
// page, meta descriptions, the JSON-LD Offer, the OG images) asks isLaunchOfferActive(),
// so on 1 January 2027 the promotion disappears by itself:
//
//   - the prerender runs at deploy time and leaves the offer out once it has ended;
//   - the browser checks again with its own clock when the app starts, so HTML built in
//     2026 does not show a stale offer in 2027 (and index.html hides [data-launch-offer]
//     before the first paint, from LAUNCH_OFFER_ENDS_AT_MS injected by vite.config.ts);
//   - deploy.yml redeploys on 1 January, so the static HTML itself loses it too.
//
// Before 2027: replace this with the real pricing (docs/SEO.md, "Before 2027").

import { withFrenchSpacing } from '../marketing/content/typography';
import type { SiteLang } from '../marketing/site';

/** The last day of the offer, as written in copy and in schema.org priceValidUntil. */
export const LAUNCH_OFFER_LAST_DAY = '2026-12-31';

/** The first instant after the offer: 1 January 2027, 00:00 in Zurich (CET, UTC+1). */
export const LAUNCH_OFFER_ENDS_AT = '2027-01-01T00:00:00+01:00';
export const LAUNCH_OFFER_ENDS_AT_MS = Date.parse(LAUNCH_OFFER_ENDS_AT);

/** True through 31 December 2026, 23:59:59 Europe/Zurich; false from then on. */
export function isLaunchOfferActive(now: Date | number = Date.now()): boolean {
  const at = typeof now === 'number' ? now : now.getTime();
  return Number.isFinite(at) && at < LAUNCH_OFFER_ENDS_AT_MS;
}

export interface LaunchOfferCopy {
  /** The short badge: "Free until 31 December 2026". */
  badge: string;
  /** The home page hero's ribbon. */
  ribbon: string;
  /** One sentence for the strip. */
  strip: string;
  /** The sign-up button while the offer runs. */
  cta: string;
  /** The header's sign-up button while the offer runs. */
  headerCta: string;
  /** The link from the strip to the pricing page. */
  pricingLink: string;
  /** Said beside a sign-up button. */
  noCard: string;
}

export const LAUNCH_OFFER_COPY: Record<SiteLang, LaunchOfferCopy> = {
  en: {
    badge: 'Free until 31 December 2026',
    ribbon: 'Free until 31 December 2026: sign up now and use everything free',
    strip: 'Launch offer: CadenceE is free until 31 December 2026. Sign up now and use everything, free, for that whole period.',
    cta: 'Start free',
    headerCta: 'Start free',
    pricingLink: 'See pricing',
    noCard: 'No credit card required',
  },
  fr: withFrenchSpacing({
    badge: 'Gratuit jusqu’au 31 décembre 2026',
    ribbon: 'Gratuit jusqu’au 31 décembre 2026 : inscrivez-vous et profitez de tout gratuitement',
    strip: 'Offre de lancement : CadenceE est gratuit jusqu’au 31 décembre 2026. Inscrivez-vous maintenant et profitez de tout, gratuitement, pendant toute cette période.',
    cta: 'Commencer gratuitement',
    headerCta: 'Démarrer gratuitement',
    pricingLink: 'Voir les tarifs',
    noCard: 'Sans carte de crédit',
  }),
  de: {
    badge: 'Kostenlos bis 31. Dezember 2026',
    ribbon: 'Kostenlos bis 31. Dezember 2026: jetzt registrieren und alles kostenlos nutzen',
    strip: 'Einführungsangebot: CadenceE ist bis 31. Dezember 2026 kostenlos. Jetzt registrieren und den ganzen Zeitraum alles kostenlos nutzen.',
    cta: 'Kostenlos starten',
    headerCta: 'Kostenlos starten',
    pricingLink: 'Zu den Preisen',
    noCard: 'Keine Kreditkarte nötig',
  },
};
