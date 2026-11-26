import { useEffect, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { CTA_COPY } from '../ctaCopy';
import type { SiteLang } from '../site';
import { CtaLink } from './CtaLink';
import { useLaunchOfferActive, useSignupLabel } from './LaunchOffer';

/** How far down the page (px) before the bar appears: past the hero and its own buttons. */
const SHOW_AFTER_PX = 640;

/**
 * On a phone, once the hero's buttons have scrolled away: "Start free" stays at the bottom of
 * the screen, with the offer in one line. Hidden from md up, where the header's button is
 * always in view; not rendered at all until the page has scrolled, so the prerendered HTML
 * and the first screen are unchanged.
 */
export function StickySignupBar({ lang }: { lang: SiteLang }) {
  const [shown, setShown] = useState(false);
  const offer = useLaunchOfferActive();
  const label = useSignupLabel(lang);

  useEffect(() => {
    const update = () => setShown(window.scrollY > SHOW_AFTER_PX);
    update();
    window.addEventListener('scroll', update, { passive: true });
    return () => window.removeEventListener('scroll', update);
  }, []);

  const copy = CTA_COPY[lang];
  return (
    <>
      {/* Room at the foot of the page, so the bar never covers the footer's last lines. */}
      <div className="h-20 md:hidden" aria-hidden="true" />
      {shown && (
        <div
          data-sticky-signup=""
          className="fixed inset-x-0 bottom-0 z-40 border-t border-slate-200 bg-white/95 px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-8px_24px_-12px_rgba(15,23,42,0.25)] backdrop-blur md:hidden"
        >
          <div className="mx-auto flex max-w-xl items-center gap-3">
            <p className="flex-1 text-sm font-semibold leading-snug text-slate-800">
              {offer ? copy.barNote : copy.barNoteAfterOffer}
            </p>
            <CtaLink
              kind="signup"
              lang={lang}
              track="cta-signup-bar"
              className="inline-flex shrink-0 items-center gap-1.5 rounded-xl bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white shadow-lg"
            >
              {label}
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </CtaLink>
          </div>
        </div>
      )}
    </>
  );
}

export default StickySignupBar;
