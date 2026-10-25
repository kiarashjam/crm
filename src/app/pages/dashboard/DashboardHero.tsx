import { Link, useNavigate } from 'react-router-dom';
import {
  TrendingUp,
  Users,
  Briefcase,
  Trophy,
  XCircle,
  Calendar,
  Plus,
  TriangleAlert,
  LayoutDashboard,
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import PageHero, { heroPrimary, heroSecondary, type PageStat } from '@/app/components/PageHero';
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
function figure(value: number | undefined, loaded: boolean): number | string {
  if (!loaded || value === undefined) return '—';
  return value;
}

export function DashboardHero({ displayName, stats, loaded, failed }: DashboardHeroProps) {
  const navigate = useNavigate();
  const others = (stats?.pipelineByCurrency ?? []).slice(1);
  const unreadable = stats?.unreadableValueCount ?? 0;
  const today = new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' });
  const activeLeads = figure(stats?.activeLeadsCount, loaded);
  const openDeals = figure(stats?.activeDealsCount, loaded);

  const heroStats: PageStat[] = [
    {
      label: 'Open pipeline',
      value: loaded && stats ? stats.pipelineValue : '—',
      icon: TrendingUp,
      tone: 'orange',
      hint: loaded && stats ? `in ${stats.pipelineCurrency}` : undefined,
      format: stats ? (n) => formatMoney(n, stats.pipelineCurrency) : undefined,
    },
    {
      label: 'Active Leads',
      value: activeLeads,
      icon: Users,
      tone: 'blue',
      hint: 'go to leads',
      onClick: () => navigate('/leads'),
      actionLabel: `Active leads, ${activeLeads}. Go to leads.`,
    },
    {
      label: 'Open Deals',
      value: openDeals,
      icon: Briefcase,
      tone: 'violet',
      hint: 'go to deals',
      onClick: () => navigate('/deals'),
      actionLabel: `Open deals, ${openDeals}. Go to deals.`,
    },
    { label: 'Won', value: figure(stats?.dealsWonCount, loaded), icon: Trophy, tone: 'emerald', hint: 'deals won' },
    { label: 'Lost', value: figure(stats?.dealsLostCount, loaded), icon: XCircle, tone: 'rose', hint: 'deals lost' },
  ];

  const notes = failed || others.length > 0 || unreadable > 0;

  return (
    <PageHero
      icon={LayoutDashboard}
      tone="orange"
      title={
        <>
          Welcome back,{' '}
          <span className="bg-gradient-to-r from-orange-500 to-pink-500 bg-clip-text text-transparent">{displayName}</span>
        </>
      }
      subtitle="Here's what's happening with your sales pipeline today."
      badges={
        <span className="inline-flex items-center gap-1 rounded-full border border-orange-200 bg-orange-50 px-2 py-0.5 text-[11px] font-semibold text-orange-700">
          <Calendar className="h-3 w-3" aria-hidden />
          {today}
        </span>
      }
      actions={
        <>
          <Button asChild variant="outline" className={heroSecondary}>
            <Link to="/deals">
              <Plus className="h-4 w-4" />
              New Deal
            </Link>
          </Button>
          <Button asChild className={heroPrimary('orange')}>
            <Link to="/leads">
              <Users className="h-4 w-4" />
              Add Lead
            </Link>
          </Button>
        </>
      }
      stats={heroStats}
    >
      {notes && (
        <div className="space-y-2">
          {/* A failed read is stated, not rendered as zeros. */}
          {failed && (
            <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50/90 px-3 py-2">
              <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
              <p className="text-sm text-amber-800">
                <strong className="font-semibold">These figures could not be loaded.</strong>{' '}
                The dashes above are missing data, not zeros. Reload to try again.
              </p>
            </div>
          )}
          {/* Other currencies are listed, never folded into the pipeline figure:
              there is no exchange rate in this system to fold them with. */}
          {(others.length > 0 || unreadable > 0) && (
            <p className="text-xs text-slate-500">
              {others.length > 0 && <>Open pipeline also has {others.map((c) => formatMoney(c.value, c.currency)).join(' and ')}.</>}
              {others.length > 0 && unreadable > 0 && ' '}
              {unreadable > 0 && (
                <span className="font-medium text-slate-600">
                  {unreadable} deal{unreadable === 1 ? '' : 's'} with an unreadable value excluded.
                </span>
              )}
            </p>
          )}
        </div>
      )}
    </PageHero>
  );
}

export default DashboardHero;
