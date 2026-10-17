import { useEffect, useState } from 'react';
import { getLeadHistory, type LeadStatusEvent } from '@/app/api/leadOps';

export function LeadStatusTimeline({ leadId }: { leadId: string }) {
  const [events, setEvents] = useState<LeadStatusEvent[]>([]);

  useEffect(() => {
    getLeadHistory(leadId).then(setEvents).catch(() => setEvents([]));
  }, [leadId]);

  if (events.length === 0) return null;

  return (
    <div className="mt-3 space-y-1">
      <p className="text-xs font-medium text-slate-500">Status history</p>
      {events.slice(0, 6).map((event) => (
        <p key={event.id} className="text-xs text-slate-600">
          {event.fromStatus ? `${event.fromStatus} → ` : ''}{event.toStatus}
          {' · '}
          {new Date(event.changedAtUtc).toLocaleString()}
        </p>
      ))}
    </div>
  );
}
