// Outbound communications log.
//
// Records every email / SMS / push sent to a member: who got what, when, and
// whether they opened / clicked / replied. This is the audit trail the
// hospitality team relies on when a member says "did you ever send me that?".

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CommChannel = 'Email' | 'SMS' | 'Push';
export type CommStatus = 'Queued' | 'Sent' | 'Delivered' | 'Opened' | 'Clicked' | 'Failed' | 'Bounced';
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
  channel: CommChannel;
  category: CommCategory;
  subject: string;
  preview: string;
  status: CommStatus;
  sentAtUtc: string;
  openedAtUtc?: string;
  clickedAtUtc?: string;
  campaignName?: string;
}

export const CHANNELS: CommChannel[] = ['Email', 'SMS', 'Push'];
export const STATUSES: CommStatus[] = [
  'Queued',
  'Sent',
  'Delivered',
  'Opened',
  'Clicked',
  'Failed',
  'Bounced',
];
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

const day = 86_400_000;
const hour = 3_600_000;

const seed: Communication[] = [
  {
    id: 'com-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    channel: 'Email',
    category: 'Event',
    subject: 'Your seats for Wine Down Wednesday are confirmed',
    preview: 'See you Wednesday at 7pm in the Library Bar — wines paired with...',
    status: 'Opened',
    sentAtUtc: new Date(Date.now() - 4 * hour).toISOString(),
    openedAtUtc: new Date(Date.now() - 3 * hour).toISOString(),
    campaignName: 'event-confirm-wdw-2026-06',
  },
  {
    id: 'com-seed-2',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    channel: 'SMS',
    category: 'Reservation',
    subject: 'Conference Room A — 2pm reminder',
    preview: 'Hi Daniel, your booking for Conference Room A is in 1 hour.',
    status: 'Delivered',
    sentAtUtc: new Date(Date.now() - 1 * hour).toISOString(),
  },
  {
    id: 'com-seed-3',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    channel: 'Email',
    category: 'Loyalty',
    subject: 'You just unlocked 500 anniversary points',
    preview: 'Thank you for a wonderful year with us — your account has been...',
    status: 'Clicked',
    sentAtUtc: new Date(Date.now() - 2 * day).toISOString(),
    openedAtUtc: new Date(Date.now() - 2 * day + 1 * hour).toISOString(),
    clickedAtUtc: new Date(Date.now() - 2 * day + 1.5 * hour).toISOString(),
    campaignName: 'anniversary-points-q2',
  },
  {
    id: 'com-seed-4',
    memberId: 'mem-seed-5',
    memberName: 'Sofia Reyes',
    channel: 'Email',
    category: 'Dues',
    subject: 'We were unable to process your dues',
    preview: 'Your card on file declined when we attempted to process this...',
    status: 'Bounced',
    sentAtUtc: new Date(Date.now() - 18 * day).toISOString(),
    campaignName: 'dues-failure-notice',
  },
  {
    id: 'com-seed-5',
    memberId: 'mem-seed-4',
    memberName: 'Marcus Lindgren',
    channel: 'Email',
    category: 'Application',
    subject: 'Welcome to the club — your application is approved',
    preview: "We're delighted to welcome you. Below you'll find your member...",
    status: 'Opened',
    sentAtUtc: new Date(Date.now() - 6 * day).toISOString(),
    openedAtUtc: new Date(Date.now() - 6 * day + 30 * 60 * 1000).toISOString(),
    campaignName: 'welcome-new-member',
  },
  {
    id: 'com-seed-6',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    channel: 'Push',
    category: 'Announcement',
    subject: 'Founders Anniversary Gala — RSVPs open Friday',
    preview: 'Our 5-year celebration on the 28th. Black tie. RSVPs go live...',
    status: 'Sent',
    sentAtUtc: new Date(Date.now() - 9 * hour).toISOString(),
    campaignName: 'gala-announce',
  },
  {
    id: 'com-seed-7',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    channel: 'Email',
    category: 'Renewal',
    subject: 'Your Platinum membership renews on July 25',
    preview: 'A heads up that your annual cycle restarts in 45 days. Your...',
    status: 'Delivered',
    sentAtUtc: new Date(Date.now() - 11 * day).toISOString(),
    campaignName: 'renewal-45day',
  },
];

const commStore = createMockStore<Communication>({
  storageKey: 'crm.mock.communications.v1',
  seed,
  idOf: (c) => c.id,
});

export async function getCommunications(): Promise<Communication[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Communication[]>('/api/communications');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...commStore.list()].sort(
        (a, b) => Date.parse(b.sentAtUtc) - Date.parse(a.sentAtUtc),
      );
    },
  );
}

export interface CommunicationInput {
  memberId: string;
  memberName: string;
  channel: CommChannel;
  category: CommCategory;
  subject: string;
  preview: string;
  campaignName?: string;
}

export async function sendCommunication(input: CommunicationInput): Promise<Communication | null> {
  return apiWithFallback(
    () =>
      authFetchJson<Communication>('/api/communications', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(180);
      return commStore.add({
        id: mockId('com'),
        sentAtUtc: new Date().toISOString(),
        status: 'Sent',
        ...input,
      });
    },
  );
}

export async function deleteCommunication(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/communications/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return commStore.remove(id);
    },
  );
}

export interface CommStats {
  sentThisWeek: number;
  openRate: number; // 0–1
  clickRate: number;
  bounceRate: number;
  bySent7d: number;
  byOpened7d: number;
}

export function computeCommStats(comms: Communication[]): CommStats {
  const weekAgo = Date.now() - 7 * day;
  let total = 0;
  let totalThisWeek = 0;
  let opened = 0;
  let clicked = 0;
  let bounced = 0;
  let openedThisWeek = 0;
  for (const c of comms) {
    const sent = Date.parse(c.sentAtUtc);
    const isThisWeek = sent >= weekAgo;
    total++;
    if (isThisWeek) totalThisWeek++;
    if (c.status === 'Opened' || c.status === 'Clicked') {
      opened++;
      if (isThisWeek) openedThisWeek++;
    }
    if (c.status === 'Clicked') clicked++;
    if (c.status === 'Bounced' || c.status === 'Failed') bounced++;
  }
  const total1 = total || 1;
  return {
    sentThisWeek: totalThisWeek,
    openRate: opened / total1,
    clickRate: clicked / total1,
    bounceRate: bounced / total1,
    bySent7d: totalThisWeek,
    byOpened7d: openedThisWeek,
  };
}
