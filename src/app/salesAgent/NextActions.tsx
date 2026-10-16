import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Compass, PenLine } from 'lucide-react';
import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import type { SuggestableLead } from './nextAction';
import {
  workQueue,
  type SuggestableDeal,
  type SuggestableEnrollment,
  type SuggestableSequence,
  type SuggestableAutomation,
  type SuggestableCompany,
  type SuggestableContact,
  type SuggestableTask,
  type WorkSuggestion,
} from './workspace';
import type { WriterBrief } from './writerBrief';

/**
 * The first thing a salesperson sees. The step comes from saved records.
 * Draft message only fills the sales writer. Nothing is sent.
 */
function listOf<T>(result: PromiseSettledResult<unknown>): T[] {
  if (result.status !== 'fulfilled' || !Array.isArray(result.value)) return [];
  return result.value.filter((item): item is T => !!item && typeof item === 'object');
}

function recipientFor(item: WorkSuggestion): { type: 'lead' | 'contact' | 'deal'; id: string; name: string } | null {
  const match = /^\/(leads|contacts|deals)\/([^/]+)$/.exec(item.href);
  if (!match) return null;
  const type = match[1] === 'leads' ? 'lead' : match[1] === 'deals' ? 'deal' : 'contact';
  return { type, id: decodeURIComponent(match[2]), name: item.name };
}

export function NextActions() {
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  const [items, setItems] = useState<WorkSuggestion[] | null>(null);
  const [remaining, setRemaining] = useState(0);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setItems(null);
    setRemaining(0);
    setFailed(false);
    Promise.allSettled([
      getLeads(), getDeals(), getTasks(), getEnrollments(), getSequences(),
      getContacts(), getCompanies(), getAutomations(),
    ])
      .then((results) => {
        if (cancelled) return;
        if (results.every((result) => result.status === 'rejected')) {
          setFailed(true);
          return;
        }
        const [leads, deals, tasks, enrollments, sequences, contacts, companies, automations] = results;
        const queue = workQueue({
          leads: listOf<SuggestableLead>(leads),
          deals: listOf<SuggestableDeal>(deals),
          tasks: listOf<SuggestableTask>(tasks),
          enrollments: listOf<SuggestableEnrollment>(enrollments),
          sequences: listOf<SuggestableSequence>(sequences),
          contacts: listOf<SuggestableContact>(contacts),
          companies: listOf<SuggestableCompany>(companies),
          automations: listOf<SuggestableAutomation>(automations),
        });
        setItems(queue.items);
        setRemaining(queue.remaining);
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [orgId]);

  if (failed) {
    return (
      <p className="mb-6 rounded-2xl border border-slate-200 bg-white px-5 py-4 text-sm text-slate-600" role="status">
        Next steps could not be loaded.
      </p>
    );
  }

  if (items === null) {
    return (
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-busy="true" aria-label="Loading next steps">
        <div className="h-5 w-40 rounded bg-slate-100" />
        <div className="mt-4 space-y-3">
          <div className="h-16 rounded-xl bg-slate-50" />
          <div className="h-16 rounded-xl bg-slate-50" />
        </div>
      </section>
    );
  }

  if (items.length === 0) return null;

  return (
    <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="next-actions-heading">
      <div className="flex items-center gap-2">
        <Compass className="h-5 w-5 text-[var(--brand-primary)]" aria-hidden="true" />
        <h2 id="next-actions-heading" className="text-lg font-semibold text-slate-900">Do this next</h2>
      </div>
      <p className="mt-1 text-sm text-slate-500">One next step per record, from what is already saved. Draft message fills the sales writer. It does not send anything.</p>
      <ol className="mt-4 space-y-3">
        {items.map((item, index) => (
          <li key={item.key} className="rounded-xl border border-slate-200 bg-slate-50 p-3">
            <div className="flex gap-3">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--brand-primary)] text-xs font-bold text-white" aria-hidden="true">{index + 1}</span>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-slate-900">
                  <Link to={item.href} className="rounded-sm hover:text-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]">
                    {item.name}
                  </Link>
                  <span className="ml-2 text-xs font-normal text-slate-500">{item.list}</span>
                </p>
                <p className="mt-1 text-sm text-slate-800">{item.action}</p>
                <p className="mt-1 text-xs text-slate-500">{item.because}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Link
                    to={item.href}
                    className="inline-flex h-11 items-center rounded-xl bg-[var(--brand-primary)] px-4 text-sm font-semibold text-white hover:bg-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2"
                  >
                    Open record
                  </Link>
                  {item.brief && (
                    <Link
                      to="/dashboard"
                      state={{ writerBrief: item.brief satisfies WriterBrief, writerRecipient: recipientFor(item) }}
                      className="inline-flex h-11 items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 hover:border-[var(--brand-primary)] hover:text-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2"
                    >
                      <PenLine className="h-4 w-4" aria-hidden="true" />
                      Draft message
                    </Link>
                  )}
                </div>
              </div>
            </div>
          </li>
        ))}
      </ol>
      {remaining > 0 && (
        <p className="mt-3 text-xs text-slate-500">{remaining} more after these. Clear one and the next moves up.</p>
      )}
    </section>
  );
}
