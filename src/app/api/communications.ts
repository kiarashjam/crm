// The communications log: every message the club sent a member, by a campaign or by
// someone on the team, with what happened to each.
//
// An email to a member goes through the server's outbox, and its status is what the
// mail service reports back: queued, sent, delivered, opened (where open tracking is on),
// bounced. One that did not go says why (suppressed: no consent to marketing, an
// unsubscribe, do not contact; skipped: no usable address). A message the team sent some
// other way (a text from the desk phone, a letter) can still be logged, and reads as
// logged: Cadence did not send it. Cadence cannot send SMS or push.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { demoMarketingConsent } from './consent';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CommChannel = 'Email' | 'SMS' | 'Push';
export type CommPurpose = 'marketing' | 'service';
export type CommStatus =
  | 'queued'
  | 'sent'
  | 'delivered'
  | 'opened'
  | 'bounced'
  | 'suppressed'
  | 'failed'
  | 'skipped'
  | 'logged';
export type CommCategory =
  | 'Welcome'
  | 'Dues'
  | 'Renewal'
  | 'Event'
  | 'Reservation'
  | 'Loyalty'
  | 'Announcement'
  | 'Application'
  | 'Other';

export interface Communication {
  id: string;
  memberId: string;
  memberName: string;
  email?: string | null;
  campaignId?: string | null;
  campaignName?: string | null;
  channel: CommChannel;
  category: CommCategory;
  purpose?: CommPurpose | null;
  subject: string;
  preview?: string | null;
  status: CommStatus;
  /** Why it was not sent, or did not arrive. */
  reason?: string | null;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  openedAtUtc?: string | null;
}

/** How a status reads on screen. */
export function commStatusLabel(status: CommStatus): string {
  switch (status) {
    case 'queued': return 'Queued';
    case 'sent': return 'Sent';
    case 'delivered': return 'Delivered';
    case 'opened': return 'Opened';
    case 'bounced': return 'Bounced';
    case 'suppressed': return 'Not sent';
    case 'skipped': return 'Not sent';
    case 'failed': return 'Failed';
    case 'logged': return 'Logged, not sent by CadenceE';
  }
}

/** Statuses that mean nothing reached the member. */
export const NOT_SENT: CommStatus[] = ['suppressed', 'skipped', 'failed', 'bounced'];

export const CHANNELS: CommChannel[] = ['Email', 'SMS', 'Push'];
export const CATEGORIES: CommCategory[] = [
  'Welcome',
  'Dues',
  'Renewal',
  'Event',
  'Reservation',
  'Loyalty',
  'Announcement',
  'Application',
  'Other',
];

/**
 * A sensible first answer to "marketing or service?": what a member needs to run their
 * membership (dues, a booking, their application) is service; the rest is marketing,
 * which goes only to members who agreed to it. The sender can change it.
 */
export function defaultPurposeFor(category: CommCategory): CommPurpose {
  return category === 'Dues' || category === 'Reservation' || category === 'Application' ? 'service' : 'marketing';
}

/** The message to email one member. */
export interface SendMemberEmailInput {
  memberId: string;
  category: CommCategory;
  purpose: CommPurpose;
  subject: string;
  body: string;
}

/** A message the team sent some other way, recorded so the team can see it. */
export interface LogCommunicationInput {
  memberId: string;
  channel: CommChannel;
  category: CommCategory;
  subject: string;
  preview?: string;
}

// ── demo ────────────────────────────────────────────────────────────────────

const day = 86_400_000;
const hour = 3_600_000;

const seed: Communication[] = [
  {
    id: 'com-seed-1', memberId: 'mem-seed-1', memberName: 'Amelia Hartwell', email: 'amelia.hartwell@example.com', channel: 'Email',
    category: 'Event', purpose: 'service', subject: 'Your seats for Wine Down Wednesday are confirmed',
    preview: 'See you Wednesday at 7pm in the Library Bar — wines paired with...', status: 'opened',
    createdAtUtc: new Date(Date.now() - 4 * hour).toISOString(), sentAtUtc: new Date(Date.now() - 4 * hour).toISOString(),
    openedAtUtc: new Date(Date.now() - 3 * hour).toISOString(),
  },
  {
    id: 'com-seed-2', memberId: 'mem-seed-2', memberName: 'Daniel Okafor', channel: 'SMS', category: 'Reservation',
    subject: 'Conference Room A — 2pm reminder', preview: 'Texted from the front desk phone.', status: 'logged',
    createdAtUtc: new Date(Date.now() - 1 * hour).toISOString(),
  },
  {
    id: 'com-seed-3', memberId: 'mem-seed-5', memberName: 'Sofia Reyes', email: 'sofia.reyes@example.com', channel: 'Email',
    category: 'Dues', purpose: 'service', subject: 'We were unable to process your dues',
    preview: 'Your card on file declined when we attempted to process this...', status: 'bounced',
    reason: '550 5.1.1 The email account does not exist', createdAtUtc: new Date(Date.now() - 18 * day).toISOString(),
    sentAtUtc: new Date(Date.now() - 18 * day).toISOString(),
  },
];

const commStore = createMockStore<Communication>({
  storageKey: 'crm.mock.communications.v2',
  seed,
  idOf: (c) => c.id,
});

/** Demo: the messages a demo campaign writes, so its members' logs show them. */
export function addDemoCommunication(row: Communication): Communication {
  return commStore.add(row);
}

/** Demo: every message, for the campaign figures worked out in this browser. */
export function demoCommunications(): Communication[] {
  return commStore.list();
}

// ── API ─────────────────────────────────────────────────────────────────────

/** The newest messages, the club's or one member's. */
export async function getCommunications(
  params: { memberId?: string } = {},
  req: RequestOptions = {},
): Promise<Communication[]> {
  return apiWithFallback(
    async () => {
      const query = params.memberId ? `?memberId=${encodeURIComponent(params.memberId)}` : '';
      const res = await authFetchJson<Communication[]>(`/api/communications${query}`, { signal: req.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return commStore.list()
        .filter((c) => !params.memberId || c.memberId === params.memberId)
        .sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc));
    },
  );
}

/**
 * Emails one member through the outbox. A refusal (no consent to marketing, do not
 * contact, a bounced address) is not an error: the attempt is recorded and comes back
 * with status "suppressed" and the reason.
 */
export async function sendMemberEmail(input: SendMemberEmailInput, memberName = 'Member', email?: string): Promise<Communication> {
  return apiWithFallback(
    () => authFetchJson<Communication>('/api/communications/send', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(120);
      const refused = input.purpose === 'marketing' && demoMarketingConsent(input.memberId) !== 'granted';
      return commStore.add({
        id: mockId('com'),
        memberId: input.memberId,
        memberName,
        email,
        channel: 'Email',
        category: input.category,
        purpose: input.purpose,
        subject: input.subject,
        preview: input.body.slice(0, 140),
        // Nothing leaves this browser in the demo; the status shows what the server would do.
        status: refused ? 'suppressed' : 'sent',
        reason: refused ? 'This member has not agreed to marketing by email.' : null,
        createdAtUtc: new Date().toISOString(),
        sentAtUtc: refused ? null : new Date().toISOString(),
      });
    },
  );
}

/** Records a message sent some other way. Nothing is sent. */
export async function logCommunication(input: LogCommunicationInput, memberName = 'Member'): Promise<Communication> {
  return apiWithFallback(
    () => authFetchJson<Communication>('/api/communications', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(80);
      return commStore.add({
        id: mockId('com'),
        memberId: input.memberId,
        memberName,
        channel: input.channel,
        category: input.category,
        subject: input.subject,
        preview: input.preview ?? null,
        status: 'logged',
        createdAtUtc: new Date().toISOString(),
      });
    },
  );
}

/** Deletes a logged message. What Cadence sent is kept; the server refuses (409). */
export async function deleteCommunication(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/communications/${id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(60);
      return commStore.remove(id);
    },
  );
}

export interface CommStats {
  /** Messages written in the last seven days, logged ones included. */
  lastWeek: number;
  delivered: number;
  opened: number;
  notSent: number;
}

export function computeCommStats(comms: Communication[]): CommStats {
  const weekAgo = Date.now() - 7 * day;
  let lastWeek = 0;
  let delivered = 0;
  let opened = 0;
  let notSent = 0;
  for (const c of comms) {
    if (Date.parse(c.createdAtUtc) >= weekAgo) lastWeek++;
    if (c.status === 'delivered' || c.status === 'opened') delivered++;
    if (c.status === 'opened') opened++;
    if (NOT_SENT.includes(c.status)) notSent++;
  }
  return { lastWeek, delivered, opened, notSent };
}
