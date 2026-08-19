import { Link } from 'react-router-dom';
import {
  TrendingUp,
  Users,
  Briefcase,
  Target,
  Calendar,
  Plus,
  ArrowUpRight,
  TriangleAlert,
} from 'lucide-react';
import { formatMoney } from '@/app/lib/money';
import type { DashboardStats } from './types';

interface DashboardHeroProps {
  displayName: string;
  /** Null while loading, or when the request failed — see `failed`. */
  stats: DashboardStats | null;
  /** True once the figures have actually been read. */
  loaded: boolean;
  /** True when the request failed, so nothing here can be trusted. */
  failed: boolean;
}

/**
 * A figure, or an em dash when we do not have it.
 *
 * Every one of these used to fall back to a literal `0`, so a failed request and
 * a genuinely empty CRM rendered identically. "0 Active Leads" is a claim; the
 * dash is an admission.
 */
function Figure({ value, loaded }: { value: number | undefined; loaded: boolean }) {
  if (!loaded || value === undefined) return <>&mdash;</>;
  return <>{value.toLocaleString()}</>;
}

export function DashboardHero({ displayName, stats, loaded, failed }: DashboardHeroProps) {
  const others = (stats?.pipelineByCurrency ?? []).slice(1);
  const unreadable = stats?.unreadableValueCount ?? 0;

  return (
    <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 overflow-hidden rounded-2xl mb-8">
      {/* Animated background elements */}
      <div className="absolute inset-0">
        <div className="absolute top-0 left-1/4 w-96 h-96 bg-orange-500/20 rounded-full blur-[120px]" />
        <div className="absolute bottom-0 right-1/4 w-96 h-96 bg-violet-500/20 rounded-full blur-[120px]" />
        <div
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
            backgroundSize: '40px 40px',
          }}
        />
      </div>

      <div className="relative px-6 lg:px-8 py-8 lg:py-12">
        {/* Welcome & Date */}
        <div className="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-6 mb-8">
          <div>
            <p className="text-orange-400 text-sm font-medium mb-1 flex items-center gap-2">
              <Calendar className="w-4 h-4" />
              {new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
            </p>
            <h1 className="text-3xl lg:text-4xl font-bold text-white">
              Welcome back, <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-400 to-amber-400">{displayName}</span>
            </h1>
            <p className="mt-2 text-slate-400">Here's what's happening with your sales pipeline today.</p>
          </div>
          <div className="flex gap-3">
            <Link
              to="/deals"
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-white/10 hover:bg-white/20 border border-white/10 rounded-xl text-white text-sm font-medium transition-all"
            >
              <Plus className="w-4 h-4" />
              New Deal
            </Link>
            <Link
              to="/leads"
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 rounded-xl text-white text-sm font-medium transition-all shadow-lg shadow-orange-500/25"
            >
              <Users className="w-4 h-4" />
              Add Lead
            </Link>
          </div>
        </div>

        {/* A failed read is stated, not rendered as zeros. */}
        {failed && (
          <div className="mb-5 flex items-start gap-2.5 rounded-xl border border-amber-400/25 bg-amber-400/10 px-4 py-3">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-300" aria-hidden />
            <p className="text-sm text-amber-100/90">
              <strong className="font-semibold">These figures could not be loaded.</strong>{' '}
              The dashes below are missing data, not zeros. Reload to try again.
            </p>
          </div>
        )}

        {/* Main Stats Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
          {/* Pipeline Value - Hero stat */}
          <div className="col-span-2 lg:col-span-1 relative group">
            <div className="absolute inset-0 bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl blur-xl opacity-50 group-hover:opacity-70 transition-opacity" />
            <div className="relative bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl p-5 lg:p-6 h-full">
              <div className="flex items-center justify-between mb-4">
                <div className="w-10 h-10 rounded-xl bg-white/20 flex items-center justify-center">
                  <TrendingUp className="w-5 h-5 text-white" />
                </div>
                {/* There used to be a "+12.5%" badge here. It was a string
                    literal — the same number for every organisation, on every
                    visit, sitting on top of whatever the real figure was. There is
                    no period-over-period pipeline history in the system to compute
                    a change from, so nothing is claimed. */}
              </div>
              <p className="text-3xl lg:text-4xl font-bold text-white mb-1">
                {loaded && stats
                  ? formatMoney(stats.pipelineValue, stats.pipelineCurrency)
                  : '—'}
              </p>
              <p className="text-sm text-white/80">
                Open pipeline{loaded && stats ? ` in ${stats.pipelineCurrency}` : ''}
              </p>
              {/* Other currencies are listed, never folded into the figure above:
                  there is no exchange rate in this system to fold them with. */}
              {others.length > 0 && (
                <p className="mt-2 text-xs text-white/70">
                  plus {others.map((c) => formatMoney(c.value, c.currency)).join(' and ')}
                </p>
              )}
              {unreadable > 0 && (
                <p className="mt-1.5 text-xs font-medium text-white/90">
                  {unreadable} deal{unreadable === 1 ? '' : 's'} with an unreadable value excluded
                </p>
              )}
            </div>
          </div>

          {/* Active Leads */}
          <Link
            to="/leads"
            className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all group"
          >
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-blue-500/20 flex items-center justify-center">
                <Users className="w-5 h-5 text-blue-400" />
              </div>
              <ArrowUpRight className="w-4 h-4 text-slate-500 group-hover:text-blue-400 transition-colors" />
            </div>
            <p className="text-2xl lg:text-3xl font-bold text-white mb-1">
              <Figure value={stats?.activeLeadsCount} loaded={loaded} />
            </p>
            <p className="text-sm text-slate-400">Active Leads</p>
          </Link>

          {/* Open Deals */}
          <Link
            to="/deals"
            className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all group"
          >
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-violet-500/20 flex items-center justify-center">
                <Briefcase className="w-5 h-5 text-violet-400" />
              </div>
              <ArrowUpRight className="w-4 h-4 text-slate-500 group-hover:text-violet-400 transition-colors" />
            </div>
            <p className="text-2xl lg:text-3xl font-bold text-white mb-1">
              <Figure value={stats?.activeDealsCount} loaded={loaded} />
            </p>
            <p className="text-sm text-slate-400">Open Deals</p>
          </Link>

          {/* Won vs Lost */}
          <div className="bg-white/5 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/10 transition-all">
            <div className="flex items-center justify-between mb-3">
              <div className="w-10 h-10 rounded-xl bg-emerald-500/20 flex items-center justify-center">
                <Target className="w-5 h-5 text-emerald-400" />
              </div>
            </div>
            <div className="flex items-baseline gap-3">
              <div>
                <p className="text-2xl font-bold text-emerald-400">
                  <Figure value={stats?.dealsWonCount} loaded={loaded} />
                </p>
                <p className="text-xs text-slate-500">Won</p>
              </div>
              <span className="text-slate-600">/</span>
              <div>
                <p className="text-2xl font-bold text-red-400">
                  <Figure value={stats?.dealsLostCount} loaded={loaded} />
                </p>
                <p className="text-xs text-slate-500">Lost</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default DashboardHero;
