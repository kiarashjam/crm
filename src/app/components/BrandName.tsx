import { cn } from '@/app/components/ui/utils';

/** The product's name, as people read it: CadenceE, at cadencee.ai. */
export const BRAND = 'CadenceE';

/** What the E stands for. */
export const BRAND_LONG = 'Cadence Engine';

/**
 * The wordmark: "Cadence" with its E — the engine — in the brand's gradient, so the
 * name reads the way the address is spelled. With `tagline`, "Cadence Engine"
 * sits under it. On a coloured background (`onColor`) the E turns light, so it
 * does not vanish into an orange panel.
 */
export function BrandName({ className, tagline = false, onColor = false }: { className?: string; tagline?: boolean; onColor?: boolean }) {
  return (
    <span className={cn('inline-flex flex-col leading-none', className)}>
      <span>
        Cadence<span className={cn('bg-gradient-to-br bg-clip-text text-transparent', onColor ? 'from-yellow-100 via-white to-pink-100 drop-shadow-[0_0_6px_rgba(255,255,255,0.6)]' : 'from-orange-500 via-pink-500 to-fuchsia-500')}>E</span>
      </span>
      {tagline && (
        <span className={cn('mt-1 text-[10px] font-semibold uppercase tracking-[0.2em]', onColor ? 'text-white/75' : 'text-orange-600/80')}>{BRAND_LONG}</span>
      )}
    </span>
  );
}

export default BrandName;
