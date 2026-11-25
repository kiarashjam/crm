// An event's invitations on its page: who was invited and where each stands (no answer, coming,
// waitlist, declined; after the event, came or no-show from the door), the counts, "Invite
// members", and one reminder for those who have not answered. And the expected attendance, an
// estimate from each member's own past show rate at the club's events.

import { useState } from 'react';
import { BellRing, Loader2, MailPlus, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';
import type { ClubEvent } from '@/app/api/events';
import { InvitationsNeedServerError, remindInvitations, type EventInvitation, type ExpectedAttendance } from '@/app/api/eventInvitations';
import { Button } from '@/app/components/ui/button';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { useEventInvitations, useExpectedAttendance } from '@/app/hooks/queries';
import { currentDateLocale } from '@/app/i18n/current';
import { InviteMembersDialog } from './InviteMembersDialog';

const STATE: Record<string, { label: string; className: string }> = {
  no_answer: { label: 'No answer', className: 'bg-slate-100 text-slate-600 ring-slate-200' },
  coming: { label: 'Coming', className: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  waitlist: { label: 'Waitlist', className: 'bg-amber-50 text-amber-700 ring-amber-200' },
  declined: { label: 'Declined', className: 'bg-rose-50 text-rose-700 ring-rose-200' },
  cancelled: { label: 'Cancelled', className: 'bg-slate-100 text-slate-500 ring-slate-200' },
  came: { label: 'Came', className: 'bg-emerald-100 text-emerald-800 ring-emerald-300' },
  no_show: { label: 'No-show', className: 'bg-rose-100 text-rose-800 ring-rose-300' },
};

const EMAIL: Record<string, string> = {
  queued: 'email waiting to go',
  sent: 'email sent',
  delivered: 'email delivered',
  opened: 'email opened',
  bounced: 'email bounced',
  suppressed: 'email not sent: they asked not to be emailed',
  failed: 'email failed',
  cancelled: 'email taken back',
};

function day(iso?: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short' });
  } catch {
    return iso;
  }
}

/** The estimate, labelled as one: about how many of those with a seat will come. */
export function ExpectedAttendanceLine({ eventId }: { eventId: string }) {
  const expected = useExpectedAttendance(eventId);
  if (expected.error instanceof InvitationsNeedServerError) return null;
  if (expected.isPending) return null;
  if (expected.isError || !expected.data) {
    return (
      <p className="text-xs text-slate-500">
        The expected attendance could not be worked out.{' '}
        <button type="button" className="underline" onClick={() => void expected.refetch()}>Retry</button>
      </p>
    );
  }
  const e: ExpectedAttendance = expected.data;
  if (e.seats === 0) return null;
  const rate = `${Math.round(e.clubRate * 100)}%`;
  return (
    <p className="flex flex-wrap items-center gap-1.5 text-sm text-slate-700" data-testid="expected-attendance">
      <TrendingUp className="h-4 w-4 text-violet-600" aria-hidden />
      <span>
        Expected attendance (an estimate): <strong>about {e.expected.toLocaleString(currentDateLocale(), { maximumFractionDigits: 1 })}</strong> of {e.seats} with a seat.
      </span>
      <span className="text-xs text-slate-500">
        {e.clubHasHistory
          ? `From each member’s own show rate at the club’s past events; members with no history, guests and unnamed seats count at the club’s average of ${rate}.`
          : 'The club has no attendance history yet, so everyone counts at 90%.'}
      </span>
    </p>
  );
}

export function InvitationsPanel({ event }: { event: ClubEvent }) {
  const invitations = useEventInvitations(event.id);
  const [inviting, setInviting] = useState(false);
  const [reminding, setReminding] = useState(false);
  const open = event.status === 'Published' && Date.parse(event.startAtUtc) > Date.now();

  // In demo mode there is no server to send from: the panel says so once, quietly.
  if (invitations.error instanceof InvitationsNeedServerError) {
    return (
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-500" aria-label="Invitations">
        Inviting members with a reply link needs a connected server.
      </section>
    );
  }

  const remind = async () => {
    setReminding(true);
    try {
      const done = await remindInvitations(event.id);
      toast.success(done.reminded > 0
        ? `${done.reminded} reminder${done.reminded === 1 ? '' : 's'} on the way.${done.notSent ? ` ${done.notSent} could not be sent.` : ''}`
        : 'Nobody to remind: each member who has not answered has had their one reminder.');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'The reminders could not be sent.');
    } finally {
      setReminding(false);
      void invitations.refetch();
    }
  };

  const data = invitations.data;
  const c = data?.counts;
  const after = Date.parse(event.startAtUtc) <= Date.now();

  return (
    <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm" aria-labelledby="invitations-heading">
      <div className="flex flex-wrap items-center gap-2">
        <h2 id="invitations-heading" className="inline-flex items-center gap-2 font-semibold text-slate-900">
          <MailPlus className="h-4 w-4" aria-hidden /> Invitations
        </h2>
        <div className="ml-auto flex flex-wrap gap-2">
          {c && c.noAnswer > 0 && (
            <WriteOnly permission={PERMISSION.ClubMembers}>
              <Button size="sm" variant="outline" onClick={() => void remind()} disabled={reminding || !data?.open || c.canRemind === 0}>
                {reminding ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <BellRing className="h-3.5 w-3.5" aria-hidden />}
                Remind those who haven’t answered{c.canRemind > 0 ? ` (${c.canRemind})` : ''}
              </Button>
            </WriteOnly>
          )}
          <WriteOnly permission={PERMISSION.ClubMembers}>
            <Button size="sm" onClick={() => setInviting(true)} disabled={!open}
              title={open ? undefined : 'Members can be invited to a published event that has not started.'}>
              <MailPlus className="h-3.5 w-3.5" aria-hidden /> Invite members
            </Button>
          </WriteOnly>
        </div>
      </div>

      <div className="mt-2">
        <ExpectedAttendanceLine eventId={event.id} />
      </div>

      {invitations.isPending ? (
        <p className="mt-3 flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading the invitations…</p>
      ) : invitations.isError && !data ? (
        <LoadFailed className="mt-3" what="The invitations" error={invitations.error} onRetry={() => void invitations.refetch()} retrying={invitations.isFetching} />
      ) : !data || data.invitations.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">
          Nobody has been invited yet. Choose members by their interests, tags and habits, and each gets a link to answer “I’ll come” or “I can’t come”.
        </p>
      ) : (
        <>
          <p className="mt-3 text-sm text-slate-700" data-testid="invitation-counts">
            Invited <strong>{c!.invited}</strong> · Coming <strong>{c!.coming}</strong> · Waitlist <strong>{c!.waitlist}</strong> · Declined <strong>{c!.declined}</strong> · No answer <strong>{c!.noAnswer}</strong>
            {c!.cancelled > 0 && <> · Cancelled <strong>{c!.cancelled}</strong></>}
            {after && <> · Came <strong>{c!.came}</strong> · No-show <strong>{c!.noShow}</strong></>}
            {c!.reminded > 0 && <span className="text-slate-500"> · {c!.reminded} reminded</span>}
          </p>
          <ul className="mt-2 max-h-80 divide-y divide-slate-100 overflow-y-auto rounded-xl border border-slate-100" aria-label="Who was invited">
            {data.invitations.map((i: EventInvitation) => {
              const state = STATE[i.state] ?? { label: i.state, className: 'bg-slate-100 text-slate-600 ring-slate-200' };
              return (
                <li key={i.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
                  <span className="min-w-0 flex-1 truncate font-medium text-slate-900">{i.memberName}</span>
                  <span className={`rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${state.className}`}>
                    {i.state === 'waitlist' && i.waitlistPosition ? `${state.label} #${i.waitlistPosition}` : state.label}
                  </span>
                  <span className="w-full text-xs text-slate-500 sm:w-auto">
                    Invited {day(i.invitedAtUtc)}
                    {i.answeredAtUtc ? ` · answered ${day(i.answeredAtUtc)}` : ''}
                    {i.remindedAtUtc ? ` · reminded ${day(i.remindedAtUtc)}` : ''}
                    {i.emailStatus && EMAIL[i.emailStatus] ? ` · ${EMAIL[i.emailStatus]}` : ''}
                  </span>
                </li>
              );
            })}
          </ul>
        </>
      )}

      <InviteMembersDialog event={event} open={inviting} onOpenChange={(next) => { setInviting(next); if (!next) void invitations.refetch(); }} />
    </section>
  );
}
