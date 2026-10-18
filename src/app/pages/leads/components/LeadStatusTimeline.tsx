import { useEffect, useState } from 'react';
import { getLeadHistory, type LeadStatusEvent } from '@/app/api/leadOps';

/**
 * The server stores UTC but sends it without a zone ("2026-10-17T08:00:00"), which
 * `new Date` reads as local time — an hour or two off in Switzerland.
 */
function utc(value: string): Date {
  return new Date(/Z$|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`);
}

/**
 * The lead's recent status changes. `status` is the lead's current status: the
 * list is fetched again when it changes, so a pipeline step taken on this page
 * shows up here without a reload (it used to load once and go stale).
 */
export function LeadStatusTimeline({ leadId, status }: { leadId: string; status?: string }) {
  const [events, setEvents] = useState<LeadStatusEvent[]>([]);

  useEffect(() => {
    let cancelled = false;
    getLeadHistory(leadId)
      .then((rows) => { if (!cancelled) setEvents(Array.isArray(rows) ? rows : []); })
      .catch(() => { if (!cancelled) setEvents([]); });
    return () => { cancelled = true; };
  }, [leadId, status]);

  if (events.length === 0) return null;

  return (
    <div className="mt-3 space-y-1">
      <p className="text-xs font-medium text-slate-500">Status history</p>
      {events.slice(0, 6).map((event) => (
        <p key={event.id} className="text-xs text-slate-600">
          {event.fromStatus ? `${event.fromStatus} → ` : ''}{event.toStatus}
          {' · '}
          {utc(event.changedAtUtc).toLocaleString()}
        </p>
      ))}
    </div>
  );
}
