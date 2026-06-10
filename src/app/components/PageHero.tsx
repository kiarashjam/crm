import { cn } from '@/app/components/ui/utils';

export type StatTone = 'slate' | 'indigo' | 'violet' | 'blue' | 'emerald' | 'teal' | 'amber' | 'orange' | 'rose' | 'cyan';

export interface PageStat {
  label: string;
  value: string | number;
  icon: React.ElementType;
  tone?: StatTone;
  onClick?: () => void;
}

interface PageHeroProps {
  icon: React.ElementType;
  /** Tailwind gradient for the icon badge, e.g. "from-indigo-500 to-violet-500". */
  iconGradient?: string;
  title: string;
  subtitle?: string;
  /** Right-aligned action buttons. */
  actions?: React.ReactNode;
  /** Optional premium stat cards rendered below the hero. */
  stats?: PageStat[];
}

const TONES: Record<StatTone, { border: string; blob: string; iconBg: string; icon: string; value: string; ring: string }> = {
  slate: { border: 'border-slate-200/80', blob: 'from-slate-100 to-slate-50', iconBg: 'from-slate-100 to-slate-200', icon: 'text-slate-600', value: 'text-slate-900', ring: 'hover:shadow-slate-100' },
  indigo: { border: 'border-indigo-100', blob: 'from-indigo-50 to-indigo-100', iconBg: 'from-indigo-100 to-indigo-200', icon: 'text-indigo-600', value: 'text-indigo-600', ring: 'hover:shadow-indigo-100' },
  violet: { border: 'border-violet-100', blob: 'from-violet-50 to-violet-100', iconBg: 'from-violet-100 to-violet-200', icon: 'text-violet-600', value: 'text-violet-600', ring: 'hover:shadow-violet-100' },
  blue: { border: 'border-blue-100', blob: 'from-blue-50 to-blue-100', iconBg: 'from-blue-100 to-blue-200', icon: 'text-blue-600', value: 'text-blue-600', ring: 'hover:shadow-blue-100' },
  emerald: { border: 'border-emerald-100', blob: 'from-emerald-50 to-emerald-100', iconBg: 'from-emerald-100 to-emerald-200', icon: 'text-emerald-600', value: 'text-emerald-600', ring: 'hover:shadow-emerald-100' },
  teal: { border: 'border-teal-100', blob: 'from-teal-50 to-teal-100', iconBg: 'from-teal-100 to-teal-200', icon: 'text-teal-600', value: 'text-teal-600', ring: 'hover:shadow-teal-100' },
  amber: { border: 'border-amber-100', blob: 'from-amber-50 to-amber-100', iconBg: 'from-amber-100 to-amber-200', icon: 'text-amber-600', value: 'text-amber-600', ring: 'hover:shadow-amber-100' },
  orange: { border: 'border-orange-100', blob: 'from-orange-50 to-orange-100', iconBg: 'from-orange-100 to-orange-200', icon: 'text-orange-600', value: 'text-orange-600', ring: 'hover:shadow-orange-100' },
  rose: { border: 'border-rose-100', blob: 'from-rose-50 to-rose-100', iconBg: 'from-rose-100 to-rose-200', icon: 'text-rose-600', value: 'text-rose-600', ring: 'hover:shadow-rose-100' },
  cyan: { border: 'border-cyan-100', blob: 'from-cyan-50 to-cyan-100', iconBg: 'from-cyan-100 to-cyan-200', icon: 'text-cyan-600', value: 'text-cyan-600', ring: 'hover:shadow-cyan-100' },
};

function StatCard({ stat }: { stat: PageStat }) {
  const t = TONES[stat.tone ?? 'slate'];
  const Icon = stat.icon;
  return (
    <div
      onClick={stat.onClick}
      className={cn(
        'group relative overflow-hidden rounded-2xl border bg-white p-5 shadow-sm transition-all duration-300 hover:shadow-xl',
        t.border, t.ring,
        stat.onClick && 'cursor-pointer',
      )}
    >
      <div className={cn('absolute -mr-2 -mt-2 right-0 top-0 h-20 w-20 rounded-bl-[60px] bg-gradient-to-br transition-transform group-hover:scale-110', t.blob)} />
      <div className="relative">
        <div className={cn('mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br transition-transform group-hover:scale-110', t.iconBg)}>
          <Icon className={cn('h-5 w-5', t.icon)} />
        </div>
        <p className={cn('text-3xl font-bold tracking-tight', t.value)}>{stat.value}</p>
        <p className="mt-1 text-xs font-medium text-slate-500">{stat.label}</p>
      </div>
    </div>
  );
}

/**
 * Premium page header used across the app: a dark gradient banner with
 * decorative glows, a gradient icon badge, title/subtitle and actions — plus an
 * optional row of stat cards. Matches the Leads / Deals / Companies look.
 */
export default function PageHero({ icon: Icon, iconGradient = 'from-orange-500 to-amber-500', title, subtitle, actions, stats }: PageHeroProps) {
  return (
    <>
      <div className="relative mb-8 overflow-hidden rounded-2xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
        {/* Decorative glows + subtle dot texture */}
        <div aria-hidden className="absolute inset-0 overflow-hidden">
          <div className="absolute -top-20 -right-20 h-80 w-80 rounded-full bg-orange-500/20 blur-3xl" />
          <div className="absolute -bottom-20 -left-20 h-60 w-60 rounded-full bg-violet-500/15 blur-3xl" />
          <div className="absolute left-1/2 top-1/2 h-96 w-96 -translate-x-1/2 -translate-y-1/2 rounded-full bg-blue-500/10 blur-3xl" />
          <div className="absolute inset-0 opacity-[0.07] [background-image:radial-gradient(circle,white_1px,transparent_1px)] [background-size:24px_24px]" />
        </div>

        <div className="relative px-6 py-8 lg:px-8 lg:py-10">
          <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex items-center gap-5">
              <div className={cn('flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br shadow-lg shadow-black/30', iconGradient)}>
                <Icon className="h-8 w-8 text-white" />
              </div>
              <div className="min-w-0">
                <h1 className="text-2xl font-bold tracking-tight text-white lg:text-3xl">{title}</h1>
                {subtitle && <p className="mt-1 text-slate-400">{subtitle}</p>}
              </div>
            </div>
            {actions && <div className="flex flex-wrap items-center gap-3">{actions}</div>}
          </div>
        </div>
      </div>

      {stats && stats.length > 0 && (
        <div className={cn('mb-8 grid grid-cols-2 gap-4', stats.length >= 4 ? 'md:grid-cols-3 lg:grid-cols-4' : 'md:grid-cols-3')}>
          {stats.map((s) => <StatCard key={s.label} stat={s} />)}
        </div>
      )}
    </>
  );
}
