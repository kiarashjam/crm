import { Link } from 'react-router-dom';
import { ArrowRight, Gift } from 'lucide-react';
import { LAUNCH_OFFER_COPY, isLaunchOfferActive } from '@/app/lib/launchOffer';
import { SITE_STRINGS } from '../meta';
import { marketingPath } from '../routes';
import type { SiteLang } from '../site';
import { CtaLink } from './CtaLink';

/**
 * Whether the launch offer is shown: asked at render, so the prerender (at deploy time)
 * and the browser (with its own clock, when the app starts) each decide for themselves.
 */
export function useLaunchOfferActive(): boolean {
  return isLaunchOfferActive(Date.now());
}

/** What a sign-up button says: "Start free" while the offer runs, "Create your workspace" after. */
export function useSignupLabel(lang: SiteLang): string {
  return useLaunchOfferActive() ? LAUNCH_OFFER_COPY[lang].cta : SITE_STRINGS[lang].getStarted;
}

/** "Free until 31 December 2026", as a pill. Nothing once the offer has ended. */
export function LaunchOfferBadge({ lang, className = '' }: { lang: SiteLang; className?: string }) {
  if (!useLaunchOfferActive()) return null;
  return (
    <Link
      to={marketingPath('pricing', lang)}
      data-launch-offer=""
      data-track="nav-pricing"
      className={`inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-emerald-600 to-teal-600 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-emerald-600/20 hover:shadow-xl transition-shadow ${className}`}
    >
      <Gift className="h-4 w-4" aria-hidden="true" />
      {LAUNCH_OFFER_COPY[lang].badge}
    </Link>
  );
}

/**
 * The launch offer in one band: what it is, "Start free", and the pricing page. On every
 * public page (MarketingLayout puts it above the footer). Nothing once the offer has ended.
 */
export function LaunchOfferStrip({ lang, tone = 'light' }: { lang: SiteLang; tone?: 'light' | 'onColor' }) {
  if (!useLaunchOfferActive()) return null;
  const copy = LAUNCH_OFFER_COPY[lang];
  const onColor = tone === 'onColor';
  return (
    <aside
      data-launch-offer=""
      aria-label={copy.badge}
      className={
        onColor
          ? 'mx-auto mt-10 max-w-3xl rounded-2xl border border-white/40 bg-slate-900/20 px-5 py-4 text-white backdrop-blur-sm'
          : 'border-y border-emerald-200 bg-gradient-to-r from-emerald-50 via-white to-amber-50'
      }
    >
      <div className={`flex flex-col items-center gap-4 text-center lg:flex-row lg:text-left ${onColor ? '' : 'mx-auto w-full max-w-[1280px] px-6 py-8 lg:px-12'}`}>
        <span
          className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl ${onColor ? 'bg-white/20' : 'bg-gradient-to-br from-emerald-600 to-teal-600 shadow-lg shadow-emerald-600/20'}`}
          aria-hidden="true"
        >
          <Gift className="h-6 w-6 text-white" />
        </span>
        <p className={`flex-1 text-base sm:text-lg font-medium ${onColor ? 'text-white' : 'text-slate-800'}`}>{copy.strip}</p>
        <div className="flex shrink-0 flex-col items-center gap-2 sm:flex-row sm:gap-4">
          <CtaLink
            kind="signup"
            lang={lang}
            className={`group inline-flex items-center gap-2 rounded-xl px-6 py-3 font-semibold transition-shadow ${
              onColor ? 'bg-white text-orange-800 shadow-lg' : 'bg-slate-900 text-white shadow-lg hover:shadow-xl'
            }`}
          >
            {copy.cta}
            <ArrowRight className="h-4 w-4 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
          </CtaLink>
          <Link
            to={marketingPath('pricing', lang)}
            data-track="nav-pricing"
            className={`text-sm font-semibold underline underline-offset-4 ${onColor ? 'text-white' : 'text-emerald-800 hover:text-emerald-900'}`}
          >
            {copy.pricingLink}
          </Link>
        </div>
      </div>
    </aside>
  );
}
