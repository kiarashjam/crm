import { Check } from 'lucide-react';
import { CTA_COPY } from '../ctaCopy';
import type { SiteLang } from '../site';
import { useLaunchOfferActive } from './LaunchOffer';

/**
 * Under a hero's buttons, while the launch offer runs: no card, every feature, ready in
 * minutes, three languages. The doubts that stop someone signing up, answered where they
 * decide. Nothing once the offer has ended.
 */
export function Reassurance({ lang, className = '' }: { lang: SiteLang; className?: string }) {
  if (!useLaunchOfferActive()) return null;
  return (
    <ul
      data-launch-offer=""
      className={`flex flex-wrap justify-center lg:justify-start gap-x-5 gap-y-2 text-sm font-medium text-slate-700 ${className}`}
    >
      {CTA_COPY[lang].reassurance.map((item) => (
        <li key={item} className="inline-flex items-center gap-1.5">
          <Check className="h-4 w-4 shrink-0 text-emerald-600" aria-hidden="true" />
          {item}
        </li>
      ))}
    </ul>
  );
}

export default Reassurance;
