import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowUpRight, CheckCircle2, Loader2, UserPlus } from 'lucide-react';
import { getLeadsReferredBy } from '@/app/api/leads';
import type { Lead } from '@/app/api/types';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { cn } from '@/app/components/ui/utils';

const EASE = [0.16, 1, 0.3, 1] as const;

/** The leads this contact referred, newest first, each one a link to that lead. */
export default function ReferredLeadsCard({
  contactId,
  contactName,
  revision = 0,
  className,
}: {
  contactId: string;
  contactName: string;
  /** Bumped by the page when its records change, so the list reads again. */
  revision?: number;
  className?: string;
}) {
  const reduce = useMotionPreference();
  const [leads, setLeads] = useState<Lead[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setFailed(false);
    getLeadsReferredBy(contactId)
      .then((rows) => { if (!cancelled) setLeads(rows); })
      .catch(() => { if (!cancelled) { setLeads([]); setFailed(true); } });
    return () => { cancelled = true; };
  }, [contactId, revision]);

  const firstName = contactName.split(/\s+/)[0] || contactName;
  const converted = leads?.filter((lead) => lead.isConverted).length ?? 0;

  return (
    <section aria-labelledby="referred-leads-heading" className={cn('bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8', className)}>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <UserPlus className="h-5 w-5 text-violet-500" aria-hidden="true" />
        <h2 id="referred-leads-heading" className="text-lg font-semibold text-slate-800 dark:text-white">Referred leads</h2>
        {leads && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{leads.length}</span>}
        {converted > 0 && (
          <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">{converted} converted</span>
        )}
      </div>

      {leads === null ? (
        <div className="flex items-center justify-center gap-2 py-6 text-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          Loading referrals…
        </div>
      ) : failed ? (
        <p className="py-6 text-center text-sm text-slate-500">The referred leads could not be loaded.</p>
      ) : leads.length === 0 ? (
        <div className="py-6 text-center">
          <UserPlus className="mx-auto mb-2 h-10 w-10 text-slate-300" aria-hidden="true" />
          <p className="text-sm text-slate-500">{firstName} has not referred anyone yet.</p>
          <p className="mt-1 text-xs text-slate-400">Pick {firstName} under “Referred by” on a lead to record one.</p>
        </div>
      ) : (
        <ul className="space-y-2">
          {leads.map((lead, index) => (
            <motion.li
              key={lead.id}
              initial={reduce ? false : { opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.3, ease: EASE, delay: reduce ? 0 : Math.min(index, 8) * 0.04 }}
            >
              <Link
                to={`/leads/${lead.id}`}
                className="group flex items-center gap-3 rounded-xl border border-slate-100 bg-slate-50 p-3 transition-all hover:-translate-y-px hover:border-violet-200 hover:bg-violet-50/60 hover:shadow-sm dark:border-slate-600 dark:bg-slate-700/50 dark:hover:bg-slate-700"
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-violet-500 to-fuchsia-500 text-xs font-bold text-white" aria-hidden="true">
                  {lead.name.split(/\s+/).map((part) => part[0]).filter(Boolean).slice(0, 2).join('').toUpperCase()}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-slate-800 group-hover:text-violet-700 dark:text-slate-200">{lead.name}</span>
                  <span className="block truncate text-xs text-slate-500">
                    {[lead.status, lead.companyName, lead.createdAtUtc ? `added ${new Date(lead.createdAtUtc).toLocaleDateString()}` : null].filter(Boolean).join(' · ')}
                  </span>
                </span>
                {lead.isConverted && (
                  <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                    <CheckCircle2 className="h-3 w-3" aria-hidden="true" />
                    Converted
                  </span>
                )}
                <ArrowUpRight className="h-4 w-4 shrink-0 text-slate-400 transition-transform group-hover:text-violet-600 motion-safe:group-hover:-translate-y-0.5 motion-safe:group-hover:translate-x-0.5" aria-hidden="true" />
              </Link>
            </motion.li>
          ))}
        </ul>
      )}
    </section>
  );
}
