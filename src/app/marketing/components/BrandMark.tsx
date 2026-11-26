import { Sparkles } from 'lucide-react';
import { BrandName } from '@/app/components/BrandName';
import { WaveText } from '@/app/pages/homepage/animations';

/** The site's logo: the orange tile with its pulse rings, and the wordmark with "Cadence Engine" under it. */
export function BrandMark({ dark = false, rings = true }: { dark?: boolean; rings?: boolean }) {
  return (
    <span className="flex items-center gap-3">
      <span className="relative" aria-hidden="true">
        {rings && (
          <>
            <span className="absolute inset-[-8px] rounded-full border-2 border-orange-500/60" style={{ animation: 'pulse-ring 2s ease-out infinite' }} />
            <span className="absolute inset-[-8px] rounded-full border-2 border-orange-500/40" style={{ animation: 'pulse-ring 2s ease-out 0.6s infinite' }} />
            <span className="absolute inset-[-8px] rounded-full border-2 border-orange-500/20" style={{ animation: 'pulse-ring 2s ease-out 1.2s infinite' }} />
          </>
        )}
        <span className="relative w-12 h-12 bg-gradient-to-br from-orange-600 via-amber-500 to-orange-600 rounded-2xl flex items-center justify-center shadow-xl shadow-orange-500/30 transition-all duration-500 group-hover:scale-110 group-hover:rotate-6">
          <Sparkles className="w-6 h-6 text-white" />
        </span>
        {rings && (
          <span className="absolute -bottom-1 -right-1 w-4 h-4 bg-emerald-500 rounded-full border-2 border-white shadow-lg" />
        )}
      </span>
      <span>
        <BrandName className={`text-xl font-bold block leading-none ${dark ? 'text-white' : 'text-slate-900'}`} />
        <span className={`text-[10px] font-semibold uppercase tracking-wider ${dark ? 'text-orange-400' : 'text-orange-700'}`}>
          <WaveText text="Cadence Engine" />
        </span>
      </span>
    </span>
  );
}

export default BrandMark;
