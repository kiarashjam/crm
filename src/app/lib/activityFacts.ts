// What a logged activity says beyond its text: when it happened, how a call or a
// meeting went, how long it took, and who reached out to whom.
//
// The same lists as the server's ActivityFacts (backend/src/ACI.Application/Common/
// ActivityFacts.cs), which refuses anything else: a form only offers what the kind of
// activity can carry, so what it sends is never turned away for that.

import { fromLocalInputValue, toLocalInputValue } from './dateInputs';

export type ActivityDirection = 'inbound' | 'outbound';

export const CALL_OUTCOMES = ['connected', 'no_answer', 'left_voicemail', 'left_message', 'busy', 'wrong_number'] as const;
export const MEETING_OUTCOMES = ['completed', 'no_show', 'rescheduled', 'cancelled'] as const;

export const OUTCOME_LABELS: Record<string, string> = {
  connected: 'Connected',
  no_answer: 'No answer',
  left_voicemail: 'Left voicemail',
  left_message: 'Left a message',
  busy: 'Busy',
  wrong_number: 'Wrong number',
  completed: 'Held',
  no_show: 'No-show',
  rescheduled: 'Rescheduled',
  cancelled: 'Cancelled',
};

export const DIRECTION_LABELS: Record<ActivityDirection, string> = { outbound: 'Outbound', inbound: 'Inbound' };

/** The longest call or meeting there is: a day. */
export const MAX_DURATION_MINUTES = 24 * 60;

function kind(type: string | undefined): string {
  return (type ?? '').trim().toLowerCase();
}

export function outcomesFor(type: string | undefined): readonly string[] {
  const k = kind(type);
  if (k === 'call') return CALL_OUTCOMES;
  if (k === 'meeting' || k === 'video' || k === 'demo') return MEETING_OUTCOMES;
  return [];
}

export function hasDirection(type: string | undefined): boolean {
  const k = kind(type);
  return k === 'call' || k === 'email';
}

export function hasDuration(type: string | undefined): boolean {
  const k = kind(type);
  return k === 'call' || k === 'meeting' || k === 'video' || k === 'demo';
}

/** When it happened: the time given for it, or else when it was logged. */
export function activityTime(activity: { occurredAt?: string; createdAt: string }): string {
  return activity.occurredAt || activity.createdAt;
}

/** "Outbound · No answer · 2 min", or "" when the activity says none of it. */
export function activityFactsLine(activity: { direction?: string; outcome?: string; durationMinutes?: number }): string {
  const parts: string[] = [];
  if (activity.direction === 'inbound' || activity.direction === 'outbound') parts.push(DIRECTION_LABELS[activity.direction]);
  if (activity.outcome) parts.push(OUTCOME_LABELS[activity.outcome] ?? activity.outcome.replace(/_/g, ' '));
  if (typeof activity.durationMinutes === 'number') parts.push(`${activity.durationMinutes} min`);
  return parts.join(' · ');
}

/**
 * The facts as a form holds them. `when` is a datetime-local value, empty for "now":
 * then nothing is sent and the server stamps the moment it is saved, whatever the
 * browser's clock says.
 */
export interface ActivityFactsDraft {
  when: string;
  outcome: string;
  direction: '' | ActivityDirection;
  duration: string;
}

export const EMPTY_FACTS: ActivityFactsDraft = { when: '', outcome: '', direction: '', duration: '' };

/** A saved activity's facts, ready to edit. */
export function factsDraftOf(activity: { occurredAt?: string; createdAt: string; outcome?: string; direction?: string; durationMinutes?: number }): ActivityFactsDraft {
  return {
    when: toLocalInputValue(activityTime(activity)),
    outcome: activity.outcome ?? '',
    direction: activity.direction === 'inbound' || activity.direction === 'outbound' ? activity.direction : '',
    duration: typeof activity.durationMinutes === 'number' ? String(activity.durationMinutes) : '',
  };
}

export interface ActivityFactsPayload {
  occurredAtUtc?: string;
  outcome?: string;
  durationMinutes?: number;
  direction?: ActivityDirection;
}

/** What to send for a new activity of this kind: only what the kind can carry. */
export function factsPayload(type: string | undefined, draft: ActivityFactsDraft | undefined): ActivityFactsPayload {
  if (!draft) return {};
  const payload: ActivityFactsPayload = {};
  const when = fromLocalInputValue(draft.when);
  if (when) payload.occurredAtUtc = when;
  if (draft.outcome && outcomesFor(type).includes(draft.outcome)) payload.outcome = draft.outcome;
  if (draft.direction && hasDirection(type)) payload.direction = draft.direction;
  const minutes = draft.duration.trim() === '' ? NaN : Number(draft.duration);
  if (hasDuration(type) && Number.isInteger(minutes)) payload.durationMinutes = minutes;
  return payload;
}

/** What to send when editing: the same, plus clearing whatever the form emptied. */
export function factsUpdatePayload(type: string | undefined, draft: ActivityFactsDraft): ActivityFactsPayload & {
  clearOutcome?: boolean;
  clearDuration?: boolean;
  clearDirection?: boolean;
} {
  const payload = factsPayload(type, draft);
  return {
    ...payload,
    ...(payload.outcome === undefined && { clearOutcome: true }),
    ...(payload.durationMinutes === undefined && { clearDuration: true }),
    ...(payload.direction === undefined && { clearDirection: true }),
  };
}

/** Why the server would refuse these facts, in words for the form, or null when it would not. */
export function factsProblem(type: string | undefined, draft: ActivityFactsDraft | undefined, now: Date = new Date()): string | null {
  if (!draft) return null;
  const when = fromLocalInputValue(draft.when);
  if (draft.when && !when) return 'That is not a date and time.';
  // The server allows five minutes for a clock that runs fast; the form needs none.
  if (when && Date.parse(when) > now.getTime() + 60_000) return 'An activity is something that has happened. Plan what is ahead as a task.';
  if (hasDuration(type) && draft.duration.trim() !== '') {
    const minutes = Number(draft.duration);
    if (!Number.isInteger(minutes) || minutes < 0 || minutes > MAX_DURATION_MINUTES) {
      return `The duration is in whole minutes, from 0 to ${MAX_DURATION_MINUTES}.`;
    }
  }
  return null;
}

/**
 * Whether logging this was the team answering the person, as the server judges it for
 * "last contacted" and the first response: an email they sent, or a call of theirs that
 * nobody picked up, is not.
 */
export function isOurResponse(type: string | undefined, direction?: string, outcome?: string): boolean {
  return direction !== 'inbound' || (kind(type) === 'call' && outcome === 'connected');
}
