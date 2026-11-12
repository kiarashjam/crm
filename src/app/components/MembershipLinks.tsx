import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, ClipboardList, Crown, HomeIcon, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import {
  isOpen,
  startApplicationFromContact,
  startApplicationFromLead,
  type Application,
} from '@/app/api/applications';
import type { Household } from '@/app/api/households';
import { useMemberLinks, usePersonMembership } from '@/app/hooks/queries';
import { cn } from '@/app/components/ui/utils';

const STATUS_LABEL: Record<Application['status'], string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Waitlisted: 'Waitlisted',
};

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

function Shell({ id, title, children, className }: { id: string; title: string; children: React.ReactNode; className?: string }) {
  return (
    <section
      aria-labelledby={id}
      className={cn('overflow-hidden rounded-2xl border border-indigo-200/70 bg-gradient-to-br from-indigo-50/60 via-white to-white shadow-sm dark:border-slate-700 dark:from-slate-800 dark:via-slate-800 dark:to-slate-800', className)}
    >
      <div className="flex items-center border-b border-indigo-100/80 px-4 py-2.5 dark:border-slate-700">
        <h3 id={id} className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-indigo-700">
          <Crown className="h-3.5 w-3.5" aria-hidden="true" />
          {title}
        </h3>
      </div>
      <div className="space-y-3 p-4 text-sm">{children}</div>
    </section>
  );
}

function Failed({ what, onRetry, retrying }: { what: string; onRetry: () => void; retrying: boolean }) {
  return (
    <p className="text-sm text-slate-600">
      {what} could not be loaded.{' '}
      <button type="button" onClick={onRetry} disabled={retrying} className="font-medium text-indigo-700 underline disabled:opacity-50">
        {retrying ? 'Trying again…' : 'Try again'}
      </button>
    </p>
  );
}

function ApplicationsList({ applications }: { applications: Application[] }) {
  if (applications.length === 0) return null;
  return (
    <div>
      <p className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-500">Applications</p>
      <ul className="space-y-1">
        {applications.map((a) => (
          <li key={a.id}>
            <Link to="/applications" className="inline-flex items-center gap-1.5 text-slate-700 hover:text-indigo-700">
              <ClipboardList className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              <span>{a.requestedTier} · {STATUS_LABEL[a.status]}{a.moneyPending ? ' · dues and fee to set up' : ''}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

function HouseholdLine({ household, memberId }: { household: Household; memberId?: string }) {
  const me = memberId ? household.people.find((p) => p.memberId === memberId) : undefined;
  return (
    <div>
      <p className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-500">Household</p>
      <Link to="/households" className="inline-flex items-center gap-1.5 font-medium text-slate-800 hover:text-teal-700">
        <HomeIcon className="h-3.5 w-3.5 text-teal-600" aria-hidden="true" />
        The {household.name} household
      </Link>
      <p className="mt-0.5 text-xs text-slate-500">{household.people.map((p) => (p.isPrimary ? `${p.name} (primary)` : p.name)).join(', ')}</p>
      {me && !me.isPrimary && (
        <p className="mt-1 text-xs text-slate-600">
          {me.rollUpBilling
            ? `Their charges, tabs and dues go on ${household.primaryName ?? 'the primary'}'s account.`
            : 'Billed on their own account.'}
        </p>
      )}
    </div>
  );
}

/**
 * What a lead's or a contact's page shows of the club: the member this person became, their
 * applications and their household, and a way to start an application for them.
 */
export function PersonMembershipCard({ kind, id, personName, readOnly = false }: {
  kind: 'lead' | 'contact';
  id: string;
  personName: string;
  readOnly?: boolean;
}) {
  const query = usePersonMembership(kind, id);
  const [starting, setStarting] = useState(false);
  const first = personName.split(/\s+/)[0] || personName;
  const data = query.data;
  const hasOpen = data?.applications.some((a) => isOpen(a.status)) ?? false;

  const start = async () => {
    setStarting(true);
    try {
      await (kind === 'lead' ? startApplicationFromLead(id) : startApplicationFromContact(id));
      toast.success(`An application for ${first} is open on the Applications page.`);
      await query.refetch();
    } catch (err) {
      toast.error(errorText(err, 'The application was not started.'));
    } finally {
      setStarting(false);
    }
  };

  return (
    <Shell id={`membership-${kind}-${id}`} title="Club membership">
      {query.isPending ? (
        <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
      ) : query.isError ? (
        <Failed what="The club membership" onRetry={() => void query.refetch()} retrying={query.isFetching} />
      ) : (
        <>
          {data!.members.length > 0 ? (
            <ul className="space-y-1">
              {data!.members.map((m) => (
                <li key={m.id}>
                  <Link to={`/members/${m.id}`} className="group inline-flex items-center gap-1 font-medium text-slate-900 hover:text-indigo-700">
                    {m.name}
                    <ArrowUpRight className="h-3.5 w-3.5" aria-hidden="true" />
                  </Link>
                  <span className="ml-1.5 text-xs text-slate-500">{m.tier} member · {m.status}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-slate-600">{first} is not a member.</p>
          )}
          <ApplicationsList applications={data!.applications} />
          {data!.household && <HouseholdLine household={data!.household} />}
          {data!.members.length === 0 && !hasOpen && !readOnly && (
            <WriteOnly permission={PERMISSION.ClubMembers}>
              <Button type="button" size="sm" variant="outline" onClick={() => void start()} disabled={starting}
                className="w-full gap-1.5 border-dashed border-indigo-300 text-indigo-700 hover:bg-indigo-50">
                {starting ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <ClipboardList className="h-4 w-4" aria-hidden="true" />}
                Start a membership application
              </Button>
            </WriteOnly>
          )}
        </>
      )}
    </Shell>
  );
}

/** What a member's page shows of the sales side: their lead and contact, applications and household. */
export function MemberLinksCard({ memberId, className }: { memberId: string; className?: string }) {
  const query = useMemberLinks(memberId);
  const data = query.data;
  return (
    <Shell id={`member-links-${memberId}`} title="Lead, contact and household" className={className}>
      {query.isPending ? (
        <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
      ) : query.isError ? (
        <Failed what="Their links" onRetry={() => void query.refetch()} retrying={query.isFetching} />
      ) : (
        <>
          {data!.leadId || data!.contactId ? (
            <ul className="space-y-1">
              {data!.leadId && (
                <li>
                  <span className="text-xs text-slate-500">Lead </span>
                  <Link to={`/leads/${data!.leadId}`} className="inline-flex items-center gap-1 font-medium text-slate-900 hover:text-indigo-700">
                    {data!.leadName ?? 'Open the lead'} <ArrowUpRight className="h-3.5 w-3.5" aria-hidden="true" />
                  </Link>
                  {data!.leadStatus && <span className="ml-1.5 text-xs text-slate-500">{data!.leadStatus}</span>}
                </li>
              )}
              {data!.contactId && (
                <li>
                  <span className="text-xs text-slate-500">Contact </span>
                  <Link to={`/contacts/${data!.contactId}`} className="inline-flex items-center gap-1 font-medium text-slate-900 hover:text-indigo-700">
                    {data!.contactName ?? 'Open the contact'} <ArrowUpRight className="h-3.5 w-3.5" aria-hidden="true" />
                  </Link>
                  {data!.contactEmail && <span className="ml-1.5 text-xs text-slate-500">{data!.contactEmail}</span>}
                </li>
              )}
            </ul>
          ) : (
            <p className="text-slate-600">Not linked to a lead or a contact.</p>
          )}
          <ApplicationsList applications={data!.applications} />
          {data!.household ? <HouseholdLine household={data!.household} memberId={memberId} /> : <p className="text-xs text-slate-500">In no household.</p>}
        </>
      )}
    </Shell>
  );
}
