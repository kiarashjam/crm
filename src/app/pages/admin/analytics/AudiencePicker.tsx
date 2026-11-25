// Whose visits: Website visitors (the default), Signed-in or Everyone. A row of buttons like
// the period's, so the owner sees the website's growth without their own use of the CRM.

import { Globe, UserRound, Users, type LucideIcon } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import type { SiteAnalyticsAudience } from '@/app/api/siteAnalytics';
import { AUDIENCE_OPTIONS } from './audience';

const ICONS: Record<SiteAnalyticsAudience, LucideIcon> = {
  visitors: Globe,
  'signed-in': UserRound,
  everyone: Users,
};

export function AudiencePicker({ audience, onChange }: { audience: SiteAnalyticsAudience; onChange: (next: SiteAnalyticsAudience) => void }) {
  return (
    <div role="group" aria-label="Audience" className="flex h-9 max-w-full overflow-x-auto rounded-xl border border-slate-200 bg-white p-0.5 dark:border-white/10 dark:bg-white/5">
      {AUDIENCE_OPTIONS.map((o) => {
        const Icon = ICONS[o.value];
        const on = audience === o.value;
        return (
          <button
            key={o.value}
            type="button"
            aria-pressed={on}
            title={o.description}
            onClick={() => { if (!on) onChange(o.value); }}
            className={cn(
              'inline-flex shrink-0 items-center gap-1.5 rounded-[10px] px-2.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 sm:px-3',
              on
                // gray, not slate: the app's dark theme repaints slate-backed pills as cards.
                ? 'bg-gray-900 text-white shadow-sm dark:bg-white dark:text-gray-900'
                : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-white/10 dark:hover:text-white',
            )}
          >
            <Icon className="hidden h-3.5 w-3.5 sm:block" aria-hidden />
            {o.label}
          </button>
        );
      })}
    </div>
  );
}
