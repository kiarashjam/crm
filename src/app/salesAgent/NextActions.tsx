import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Compass, PenLine } from 'lucide-react';
import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import type { SuggestableLead } from './nextAction';
import { pageScope } from './pageScope';
import { companyPlan, contactPlan, dealPlan, leadPlan, taskPlan, type PlanLine, type RecordPlan } from './recordPlan';
import {
  collectWork,
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

const STATE_LABEL: Record<PlanLine['state'], string> = {
  done: 'Done',
  now: 'Now',
  later: 'Later',
  stopped: 'Stopped',
};

interface LoadedWork {
  items: WorkSuggestion[];
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  tasks: SuggestableTask[];
  contacts: SuggestableContact[];
  companies: SuggestableCompany[];
}

function inList(item: WorkSuggestion, list: string): boolean {
  if (list === 'Leads') return item.href.startsWith('/leads/');
  if (list === 'Deals') return item.href.startsWith('/deals/');
  return item.list === list;
}

function planFor(href: string, loaded: LoadedWork): RecordPlan | null {
  const id = decodeURIComponent(href.split('/').pop() ?? '');
  if (href.startsWith('/leads/')) {
    const lead = loaded.leads.find((item) => item.id === id);
    return lead ? leadPlan(lead) : null;
  }
  if (href.startsWith('/deals/')) {
    const deal = loaded.deals.find((item) => item.id === id);
    return deal ? dealPlan(deal) : null;
  }
  if (href.startsWith('/tasks/')) {
    const task = loaded.tasks.find((item) => item.id === id);
    return task ? taskPlan(task) : null;
  }
  if (href.startsWith('/contacts/')) {
    const contact = loaded.contacts.find((item) => item.id === id);
    return contact ? contactPlan(contact) : null;
  }
  if (href.startsWith('/companies/')) {
    const company = loaded.companies.find((item) => item.id === id);
    if (!company) return null;
    const people = loaded.contacts.filter((contact) => !contact.isArchived && contact.companyId === company.id).length;
    return companyPlan(company, people);
  }
  return null;
}

export function NextActions() {
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  const { pathname } = useLocation();
  const scope = pageScope(pathname);
  const [loaded, setLoaded] = useState<LoadedWork | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoaded(null);
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
        const input = {
          leads: listOf<SuggestableLead>(leads),
          deals: listOf<SuggestableDeal>(deals),
          tasks: listOf<SuggestableTask>(tasks),
          enrollments: listOf<SuggestableEnrollment>(enrollments),
          sequences: listOf<SuggestableSequence>(sequences),
          contacts: listOf<SuggestableContact>(contacts),
          companies: listOf<SuggestableCompany>(companies),
          automations: listOf<SuggestableAutomation>(automations),
        };
        setLoaded({
          items: collectWork(input, new Date(), Number.MAX_SAFE_INTEGER),
          leads: input.leads,
          deals: input.deals,
          tasks: input.tasks,
          contacts: input.contacts,
          companies: input.companies,
        });
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
      <p className="mb-6 rounded-2xl border border-slate-200 bg-white px-5 py-4 text-sm text-slate-600 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300" role="status">
        Next steps could not be loaded.
      </p>
    );
  }

  if (loaded === null) {
    return (
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900" aria-busy="true" aria-label="Loading next steps">
        <div className="h-5 w-40 rounded bg-slate-100 dark:bg-slate-800" />
        <div className="mt-4 space-y-3">
          <div className="h-16 rounded-xl bg-slate-50 dark:bg-slate-800" />
          <div className="h-16 rounded-xl bg-slate-50 dark:bg-slate-800" />
        </div>
      </section>
    );
  }

  const scoped = scope.mode === 'list' ? loaded.items.filter((item) => inList(item, scope.list)) : loaded.items;
  const items = scoped.slice(0, 5);
  const remaining = scoped.length - items.length;
  const plan = scope.mode === 'record' ? planFor(scope.href, loaded) : null;
  const draft = scope.mode === 'record' ? loaded.items.find((item) => item.href === scope.href && item.brief) : undefined;

  if (scope.mode === 'record') {
    return (
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900" aria-labelledby="next-actions-heading">
        <div className="flex items-center gap-2">
          <Compass className="h-5 w-5 text-[var(--brand-primary)]" aria-hidden="true" />
          <h2 id="next-actions-heading" className="text-lg font-semibold text-slate-900 dark:text-white">
            {plan ? `Plan for ${plan.name}` : 'Plan'}
          </h2>
        </div>
        {plan ? (
          <PlanLines lines={plan.lines} />
        ) : (
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">This record has no saved plan.</p>
        )}
        {draft?.brief && (
          <Link
            to="/dashboard"
            state={{ writerBrief: draft.brief satisfies WriterBrief, writerRecipient: recipientFor(draft) }}
            className="mt-4 inline-flex h-11 items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 hover:border-[var(--brand-primary)] hover:text-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
          >
            <PenLine className="h-4 w-4" aria-hidden="true" />
            Draft message
          </Link>
        )}
      </section>
    );
  }

  return (
    <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900" aria-labelledby="next-actions-heading">
      <div className="flex items-center gap-2">
        <Compass className="h-5 w-5 text-[var(--brand-primary)]" aria-hidden="true" />
        <h2 id="next-actions-heading" className="text-lg font-semibold text-slate-900 dark:text-white">Do this next</h2>
      </div>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
        {scope.mode === 'list'
          ? 'The next steps on this page, from what is already saved. Draft message fills the sales writer. It does not send anything.'
          : 'One next step per record, from what is already saved. Draft message fills the sales writer. It does not send anything.'}
      </p>
      {items.length === 0 ? (
        <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
          {scope.mode === 'list' ? 'Nothing on this page is waiting.' : 'Nothing is waiting. Saved records are up to date.'}
        </p>
      ) : (
        <ol className="mt-4 space-y-3">
          {items.map((item, index) => (
            <li key={item.key} className="rounded-xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-700 dark:bg-slate-950">
              <div className="flex gap-3">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--brand-primary)] text-xs font-bold text-white" aria-hidden="true">{index + 1}</span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-slate-900 dark:text-white">
                    <Link to={item.href} className="rounded-sm hover:text-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]">
                      {item.name}
                    </Link>
                    <span className="ml-2 text-xs font-normal text-slate-500">{item.list}</span>
                  </p>
                  <p className="mt-1 text-sm text-slate-800 dark:text-slate-200">{item.action}</p>
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
                        className="inline-flex h-11 items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 hover:border-[var(--brand-primary)] hover:text-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
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
      )}
      {remaining > 0 && (
        <p className="mt-3 text-xs text-slate-500">{remaining} more after these. Clear one and the next moves up.</p>
      )}
    </section>
  );
}

function PlanLines({ lines }: { lines: PlanLine[] }) {
  return (
    <ol className="mt-4 space-y-2">
      {lines.map((line) => (
        <li key={`${line.state}-${line.title}`} className="flex gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 dark:border-slate-700 dark:bg-slate-950">
          <span className="w-16 shrink-0 pt-0.5 text-xs font-semibold uppercase tracking-wide text-slate-500">{STATE_LABEL[line.state]}</span>
          <div className="min-w-0">
            <p className="text-sm font-medium text-slate-900 dark:text-white">{line.title}</p>
            {line.detail && <p className="mt-0.5 text-sm text-slate-600 dark:text-slate-300">{line.detail}</p>}
          </div>
        </li>
      ))}
    </ol>
  );
}
