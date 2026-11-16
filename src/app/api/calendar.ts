// The sales side of the calendar, from GET /api/calendar: tasks at the time they are
// due, the meetings logged as activities, and the meeting date on each lead's pipeline.
// The club's events and reservations come from their own endpoints.

import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';
import { getActivities } from './activities';
import { getLeads } from './leads';
import { getTasks, isMineInDemo } from './tasks';
import { parsePipeline } from '@/app/pages/leads/leadPipeline';
import { activityTime } from '@/app/lib/activityFacts';
import type { WorkScope } from '@/app/lib/workScope';

export type CalendarEntryKind = 'task' | 'meeting' | 'lead_meeting';

export interface CalendarEntry {
  kind: CalendarEntryKind;
  id: string;
  title: string;
  startUtc: string;
  endUtc?: string;
  /** A day with no time (a lead's meeting date): shown on `day` wherever the viewer is. */
  allDay: boolean;
  day?: string;
  link: string;
  /** A task's status, a meeting's outcome, or for a lead's meeting "held" or "missed". */
  status?: string;
  /** The lead, deal or contact it is about. */
  detail?: string;
  /** Who it is for: the task's assignee, who logged the meeting, who works the lead. */
  who?: string;
}

export interface CalendarPage {
  scope: WorkScope;
  entries: CalendarEntry[];
  /** A kind held more than one read returns. */
  truncated: boolean;
}

interface CalendarEntryRaw {
  kind: string;
  id: string;
  title: string;
  startUtc: string;
  endUtc?: string | null;
  allDay: boolean;
  day?: string | null;
  link: string;
  status?: string | null;
  detail?: string | null;
  who?: string | null;
}

const KINDS: readonly string[] = ['task', 'meeting', 'lead_meeting'];
const MEETING_TYPES = new Set(['meeting', 'video', 'demo']);

function mapEntry(raw: CalendarEntryRaw): CalendarEntry {
  return {
    kind: raw.kind as CalendarEntryKind,
    id: raw.id,
    title: raw.title,
    startUtc: raw.startUtc,
    endUtc: raw.endUtc ?? undefined,
    allDay: raw.allDay,
    day: raw.day ?? undefined,
    link: raw.link,
    status: raw.status ?? undefined,
    detail: raw.detail ?? undefined,
    who: raw.who ?? undefined,
  };
}

const pad = (n: number) => String(n).padStart(2, '0');
const capitalized = (text: string) => (text ? text[0]!.toUpperCase() + text.slice(1).toLowerCase() : text);
const dayOf = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

/** What is on the calendar from `from` up to `to` (at most 45 days): the rep's own, or the team's. */
export async function getCalendar(from: Date, to: Date, scope: WorkScope, req: RequestOptions = {}): Promise<CalendarPage> {
  if (!servesDemoData()) {
    const params = new URLSearchParams({ from: from.toISOString(), to: to.toISOString(), scope });
    const raw = await authFetchJson<{ scope?: string; entries?: CalendarEntryRaw[]; truncated?: boolean }>(
      `/api/calendar?${params.toString()}`, { signal: req.signal },
    );
    return {
      scope: raw?.scope === 'team' ? 'team' : 'mine',
      entries: (raw?.entries ?? []).filter((e) => KINDS.includes(e.kind)).map(mapEntry),
      truncated: raw?.truncated === true,
    };
  }

  // Demo mode: the same three kinds, from the browser's own lists.
  const [tasks, activities, leads] = await Promise.all([getTasks(), getActivities(), getLeads()]);
  const inRange = (iso?: string) => !!iso && Date.parse(iso) >= from.getTime() && Date.parse(iso) < to.getTime();
  const mine = scope === 'mine';
  const firstDay = dayOf(from);
  const lastDay = dayOf(new Date(to.getTime() - 1));
  const entries: CalendarEntry[] = [
    ...tasks
      .filter((t) => t.status !== 'cancelled' && inRange(t.dueDateUtc) && (!mine || isMineInDemo(t)))
      .map((t): CalendarEntry => ({
        kind: 'task', id: t.id, title: t.title, startUtc: t.dueDateUtc!, allDay: false, link: `/tasks/${t.id}`,
        status: t.status, detail: t.leadName ?? t.dealName ?? t.contactName, who: t.assigneeName,
      })),
    // A meeting of mine is one I logged, as on the server. The demo user logged every
    // demo activity, so one with no author is theirs.
    ...activities
      .filter((a) => MEETING_TYPES.has(a.type.toLowerCase()) && inRange(activityTime(a))
        && (!mine || isMineInDemo({ assigneeId: a.createdByUserId })))
      .map((a): CalendarEntry => {
        const start = activityTime(a);
        return {
          // Untitled, it is named by its type, as the server names it ("Video").
          kind: 'meeting', id: a.id, title: a.subject?.trim() || capitalized(a.type), startUtc: start,
          endUtc: typeof a.durationMinutes === 'number' ? new Date(Date.parse(start) + a.durationMinutes * 60_000).toISOString() : undefined,
          allDay: false,
          link: a.leadId ? `/leads/${a.leadId}` : a.dealId ? `/deals/${a.dealId}` : a.contactId ? `/contacts/${a.contactId}` : '/activities',
          status: a.outcome, detail: a.leadName ?? a.dealName ?? a.contactName, who: a.createdByName,
        };
      }),
    ...leads.flatMap((lead): CalendarEntry[] => {
      if (mine && !isMineInDemo({ assigneeId: lead.assignedToId })) return [];
      const pipeline = parsePipeline(lead.pipelineState);
      const day = pipeline.meetingDate?.slice(0, 10);
      if (!day || day < firstDay || day > lastDay) return [];
      return [{
        kind: 'lead_meeting', id: lead.id, title: `Meeting with ${lead.name}`, startUtc: `${day}T00:00:00Z`, allDay: true, day,
        link: `/leads/${lead.id}`,
        status: pipeline.meetingAttended === true ? 'held' : pipeline.meetingAttended === false ? 'missed' : undefined,
        who: lead.assignedToName,
      }];
    }),
  ];
  return { scope, entries: entries.sort((a, b) => a.startUtc.localeCompare(b.startUtc)), truncated: false };
}
