// Marketing campaigns.
//
// A campaign sends a templated message to a segment of members on a schedule
// (immediate or future). The "send" flow expands the segment to the member
// list and writes one Communication per recipient.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { sendCommunication, type CommChannel, type CommCategory } from './communications';
import { getMembers, type Member, type MemberStatus, type MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CampaignStatus = 'Draft' | 'Scheduled' | 'Sending' | 'Sent' | 'Failed';

export interface SegmentFilter {
  tiers?: MemberTier[];
  statuses?: MemberStatus[];
  joinedAfterUtc?: string;
  joinedBeforeUtc?: string;
  minHouseBalance?: number;
}

export interface Campaign {
  id: string;
  name: string;
  channel: CommChannel;
  category: CommCategory;
  subject: string;
  body: string;
  segment: SegmentFilter;
  status: CampaignStatus;
  createdAtUtc: string;
  scheduledAtUtc?: string;
  sentAtUtc?: string;
  recipientCount?: number;
}

const day = 86_400_000;
const hour = 3_600_000;

const seed: Campaign[] = [
  {
    id: 'cmp-seed-1',
    name: 'Summer rooftop opening',
    channel: 'Email',
    category: 'Announcement',
    subject: 'Rooftop terrace reopens Saturday — your seat awaits',
    body: 'The rooftop reopens for the season this Saturday. Members can reserve early.',
    segment: { statuses: ['Active'] },
    status: 'Sent',
    createdAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    sentAtUtc: new Date(Date.now() - 4 * day).toISOString(),
    recipientCount: 4,
  },
  {
    id: 'cmp-seed-2',
    name: 'Platinum-only — Founders dinner',
    channel: 'Email',
    category: 'Event',
    subject: 'Exclusive: Founders dinner — Platinum members only',
    body: 'Save the date for an intimate dinner with our founders, members of the chef council, and a few special surprises.',
    segment: { tiers: ['Platinum'], statuses: ['Active'] },
    status: 'Scheduled',
    createdAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    scheduledAtUtc: new Date(Date.now() + 2 * day + 9 * hour).toISOString(),
  },
  {
    id: 'cmp-seed-3',
    name: 'Lapsed re-engagement',
    channel: 'Email',
    category: 'Renewal',
    subject: 'We miss you — a complimentary night to return',
    body: 'It has been a minute. Come back this month and your first dinner is on us.',
    segment: { statuses: ['Lapsed'] },
    status: 'Draft',
    createdAtUtc: new Date(Date.now() - 6 * hour).toISOString(),
  },
];

const campaignStore = createMockStore<Campaign>({
  storageKey: 'crm.mock.campaigns.v1',
  seed,
  idOf: (c) => c.id,
});

export async function getCampaigns(): Promise<Campaign[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Campaign[]>('/api/campaigns');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...campaignStore.list()].sort(
        (a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc),
      );
    },
  );
}

export interface CampaignInput {
  name: string;
  channel: CommChannel;
  category: CommCategory;
  subject: string;
  body: string;
  segment: SegmentFilter;
  scheduledAtUtc?: string;
}

export async function createCampaign(input: CampaignInput): Promise<Campaign | null> {
  return apiWithFallback(
    () => authFetchJson<Campaign>('/api/campaigns', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return campaignStore.add({
        id: mockId('cmp'),
        createdAtUtc: new Date().toISOString(),
        status: input.scheduledAtUtc ? 'Scheduled' : 'Draft',
        ...input,
      });
    },
  );
}

export async function updateCampaign(
  id: string,
  patch: Partial<Campaign>,
): Promise<Campaign | null> {
  return apiWithFallback(
    () => authFetchJson<Campaign>(`/api/campaigns/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      return campaignStore.update(id, patch);
    },
  );
}

export async function deleteCampaign(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/campaigns/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return campaignStore.remove(id);
    },
  );
}

function matchesSegment(member: Member, segment: SegmentFilter): boolean {
  if (segment.tiers?.length && !segment.tiers.includes(member.tier)) return false;
  if (segment.statuses?.length && !segment.statuses.includes(member.status)) return false;
  if (
    segment.joinedAfterUtc &&
    Date.parse(member.joinedAtUtc) < Date.parse(segment.joinedAfterUtc)
  )
    return false;
  if (
    segment.joinedBeforeUtc &&
    Date.parse(member.joinedAtUtc) > Date.parse(segment.joinedBeforeUtc)
  )
    return false;
  if (
    segment.minHouseBalance != null &&
    member.houseAccountBalance < segment.minHouseBalance
  )
    return false;
  return true;
}

export async function previewSegment(segment: SegmentFilter): Promise<Member[]> {
  const members = await getMembers();
  return members.filter((m) => matchesSegment(m, segment));
}

export async function sendCampaign(id: string): Promise<Campaign | null> {
  const campaign = campaignStore.byId(id);
  if (!campaign) return null;
  campaignStore.update(id, { status: 'Sending' });
  const recipients = await previewSegment(campaign.segment);
  for (const m of recipients) {
    await sendCommunication({
      memberId: m.id,
      memberName: `${m.firstName} ${m.lastName}`,
      channel: campaign.channel,
      category: campaign.category,
      subject: campaign.subject,
      preview: campaign.body.slice(0, 140),
      campaignName: campaign.name,
    });
  }
  return campaignStore.update(id, {
    status: 'Sent',
    sentAtUtc: new Date().toISOString(),
    recipientCount: recipients.length,
  });
}

export interface CampaignStats {
  drafts: number;
  scheduled: number;
  sent: number;
  totalRecipients: number;
}

export function computeCampaignStats(campaigns: Campaign[]): CampaignStats {
  let drafts = 0;
  let scheduled = 0;
  let sent = 0;
  let totalRecipients = 0;
  for (const c of campaigns) {
    if (c.status === 'Draft') drafts++;
    else if (c.status === 'Scheduled') scheduled++;
    else if (c.status === 'Sent') sent++;
    if (c.recipientCount) totalRecipients += c.recipientCount;
  }
  return { drafts, scheduled, sent, totalRecipients };
}
