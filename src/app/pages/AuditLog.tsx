import { useEffect, useMemo, useState } from 'react';
import {
  History, Loader2, Plus, Pencil, Trash2, ArrowRightCircle, Mail, GitMerge, UserPlus, RefreshCw,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { cn } from '@/app/components/ui/utils';
import { getAuditLog, type AuditEvent, type AuditEntity, type AuditAction } from '@/app/api';

const ACTION_META: Record<AuditAction, { icon: React.ElementType; tone: string }> = {
  created: { icon: Plus, tone: 'bg-emerald-100 text-emerald-600' },
  updated: { icon: Pencil, tone: 'bg-blue-100 text-blue-600' },
  deleted: { icon: Trash2, tone: 'bg-red-100 text-red-600' },
  status_changed: { icon: RefreshCw, tone: 'bg-amber-100 text-amber-600' },
  assigned: { icon: UserPlus, tone: 'bg-violet-100 text-violet-600' },
  email_sent: { icon: Mail, tone: 'bg-indigo-100 text-indigo-600' },
  merged: { icon: GitMerge, tone: 'bg-teal-100 text-teal-600' },
  converted: { icon: ArrowRightCircle, tone: 'bg-cyan-100 text-cyan-600' },
};

const ENTITY_ROUTE: Partial<Record<AuditEntity, string>> = {
  lead: '/leads', contact: '/contacts', company: '/companies', deal: '/deals',
};

const FILTERS: { id: AuditEntity | 'all'; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'lead', label: 'Leads' },
  { id: 'contact', label: 'Contacts' },
  { id: 'company', label: 'Companies' },
  { id: 'deal', label: 'Deals' },
];

function relTime(iso: string): string {
  const t = Date.parse(iso);
  const m = Math.floor((Date.now() - t) / 60_000);
  if (m < 60) return `${Math.max(1, m)}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  return d < 7 ? `${d}d ago` : new Date(t).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export default function AuditLog() {
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<AuditEntity | 'all'>('all');

  useEffect(() => {
    setLoading(true);
    getAuditLog({ limit: 200 }).then(setEvents).catch(() => setEvents([])).finally(() => setLoading(false));
  }, []);

  const shown = useMemo(
    () => (filter === 'all' ? events : events.filter((e) => e.entityType === filter)),
    [events, filter],
  );

  const detailLink = (e: AuditEvent): string | null => {
    const base = ENTITY_ROUTE[e.entityType];
    if (!base) return null;
    return e.entityId ? `${base}/${e.entityId}` : base;
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <div className="mb-6">
            <h1 className="flex items-center gap-2 text-2xl font-bold text-slate-900">
              <History className="h-6 w-6 text-indigo-600" /> Audit log
            </h1>
            <p className="mt-1 text-sm text-slate-500">A chronological record of changes across your CRM.</p>
          </div>

          <div className="mb-4 flex flex-wrap gap-2">
            {FILTERS.map((f) => (
              <button
                key={f.id}
                type="button"
                onClick={() => setFilter(f.id)}
                className={cn(
                  'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
                  filter === f.id ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
                )}
              >
                {f.label}
              </button>
            ))}
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : shown.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-200 bg-white py-16 text-center text-sm text-slate-400">
              No audit events yet.
            </div>
          ) : (
            <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
              <ul className="divide-y divide-slate-50">
                {shown.map((e) => {
                  const M = ACTION_META[e.action] ?? ACTION_META.updated;
                  const link = detailLink(e);
                  const label = (
                    <span className="font-medium text-slate-800">{e.entityLabel ?? e.entityType}</span>
                  );
                  return (
                    <li key={e.id} className="flex items-start gap-3 px-4 py-3">
                      <span className={cn('mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full', M.tone)}>
                        <M.icon className="h-4 w-4" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm text-slate-700">
                          {e.summary} · {link ? <Link to={link} className="hover:text-indigo-700 hover:underline">{label}</Link> : label}
                        </p>
                        <p className="mt-0.5 text-xs text-slate-400">
                          <span className="capitalize">{e.entityType}</span>
                          {e.actorName ? <> · by {e.actorName}</> : null} · {relTime(e.createdAtUtc)}
                        </p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
