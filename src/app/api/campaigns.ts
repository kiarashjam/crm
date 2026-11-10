// Campaigns to the club's members.
//
// The server sends them by email through its outbox, one message per member, now or at
// a time set for them, and once: two presses of Send, or two servers, send it once, and
// a campaign that has started sending can no longer be edited or sent again. A
// marketing campaign reaches only members who agreed to marketing by email (and carries
// an unsubscribe link); a service one reaches everyone it is for. Each member's message
// has its own status, and the campaign totals them. "Send a test to me" emails a copy
// marked as a test to the person who pressed it, and counts for nothing.
//
// SMS and push are not available: there is no provider behind them. Campaigns logged
// before Cadence could send read "Logged, not delivered": nobody received them.
//
// In demo mode the same rules run in this browser, and nothing is emailed.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { addDemoCommunication, demoCommunications, type CommCategory, type Communication } from './communications';
import { demoMarketingConsent } from './consent';
import { getMembers, type MemberStatus, type MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CampaignStatus = 'Draft' | 'Scheduled' | 'Sending' | 'Sent' | 'Logged';
export type CampaignPurpose = 'marketing' | 'service';
export type CampaignChannel = 'Email' | 'SMS' | 'Push';

/** How a campaign status reads on screen. */
export function campaignStatusLabel(status: CampaignStatus): string {
  switch (status) {
    case 'Logged':
      return 'Logged, not delivered';
    case 'Sending':
      return 'Sending';
    default:
      return status;
  }
}

/** Whether nothing of it has gone: it can still be edited, scheduled, sent or deleted. */
export function isEditable(status: CampaignStatus): boolean {
  return status === 'Draft' || status === 'Scheduled';
}

export interface SegmentFilter {
  tiers?: MemberTier[];
  statuses?: MemberStatus[];
}

/**
 * Where a campaign's messages stand. A funnel: sent counts every message the mail server
 * took (delivered, opened and bounced ones too), delivered counts opened ones.
 */
export interface CampaignTotals {
  recipients: number;
  queued: number;
  sent: number;
  delivered: number;
  opened: number;
  bounced: number;
  suppressed: number;
  failed: number;
  skipped: number;
}

export interface Campaign {
  id: string;
  name: string;
  channel: CampaignChannel;
  category: CommCategory;
  purpose: CampaignPurpose;
  subject: string;
  body: string;
  segment: SegmentFilter;
  status: CampaignStatus;
  createdAtUtc: string;
  scheduledAtUtc?: string | null;
  sendingStartedAtUtc?: string | null;
  sentAtUtc?: string | null;
  recipientCount: number;
  totals?: CampaignTotals | null;
  version?: string;
}

export interface CampaignInput {
  name: string;
  category: CommCategory;
  purpose: CampaignPurpose;
  subject: string;
  body: string;
  segment: SegmentFilter;
  /** When to send it; null takes a schedule off, leaving a draft. */
  scheduledAtUtc?: string | null;
}

/** Who a campaign would reach if it were sent now. */
export interface CampaignAudience {
  purpose: CampaignPurpose;
  matching: number;
  withAddress: number;
  agreed: number;
  willReceive: number;
}

export interface CampaignTestResult {
  messageId: string;
  to: string;
  status: 'sent' | 'queued';
}

// ── demo ────────────────────────────────────────────────────────────────────

const day = 86_400_000;
const hour = 3_600_000;

const seed: Campaign[] = [
  {
    id: 'cmp-seed-1', name: 'Summer rooftop opening', channel: 'Email', category: 'Announcement', purpose: 'marketing',
    subject: 'Rooftop terrace reopens Saturday — your seat awaits',
    body: 'The rooftop reopens for the season this Saturday. Members can reserve early.',
    segment: { statuses: ['Active'] }, status: 'Logged', createdAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    sentAtUtc: new Date(Date.now() - 4 * day).toISOString(), recipientCount: 4,
  },
  {
    id: 'cmp-seed-2', name: 'Platinum-only — Founders dinner', channel: 'Email', category: 'Event', purpose: 'marketing',
    subject: 'Exclusive: Founders dinner — Platinum members only',
    body: 'Save the date for an intimate dinner with our founders, members of the chef council, and a few special surprises.',
    segment: { tiers: ['Platinum'], statuses: ['Active'] }, status: 'Scheduled', createdAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    scheduledAtUtc: new Date(Date.now() + 2 * day + 9 * hour).toISOString(), recipientCount: 0,
  },
  {
    id: 'cmp-seed-3', name: 'Lapsed re-engagement', channel: 'Email', category: 'Renewal', purpose: 'marketing',
    subject: 'We miss you — a complimentary night to return',
    body: 'It has been a minute. Come back this month and your first dinner is on us.',
    segment: { statuses: ['Lapsed'] }, status: 'Draft', createdAtUtc: new Date(Date.now() - 6 * hour).toISOString(), recipientCount: 0,
  },
];

const campaignStore = createMockStore<Campaign>({
  storageKey: 'crm.mock.campaigns.v2',
  seed,
  idOf: (c) => c.id,
});

async function demoMembers(segment: SegmentFilter) {
  return (await getMembers()).filter((m) =>
    (!segment.tiers?.length || segment.tiers.includes(m.tier))
    && (!segment.statuses?.length || segment.statuses.includes(m.status)));
}

function demoTotals(campaign: Campaign): CampaignTotals | null {
  if (campaign.status !== 'Sent') return null;
  const rows = demoCommunications().filter((c) => c.campaignId === campaign.id);
  const count = (...statuses: Communication['status'][]) => rows.filter((r) => statuses.includes(r.status)).length;
  return {
    recipients: rows.length,
    queued: count('queued'),
    sent: count('sent', 'delivered', 'opened', 'bounced'),
    delivered: count('delivered', 'opened'),
    opened: count('opened'),
    bounced: count('bounced'),
    suppressed: count('suppressed'),
    failed: count('failed'),
    skipped: count('skipped'),
  };
}

function withDemoTotals(campaign: Campaign): Campaign {
  const totals = demoTotals(campaign);
  return totals ? { ...campaign, totals, recipientCount: totals.recipients } : campaign;
}

async function demoAudience(segment: SegmentFilter, purpose: CampaignPurpose): Promise<CampaignAudience> {
  const members = await demoMembers(segment);
  const withAddress = members.filter((m) => /.+@.+\..+/.test(m.email ?? ''));
  const agreed = purpose === 'marketing' ? withAddress.filter((m) => demoMarketingConsent(m.id) === 'granted') : withAddress;
  return {
    purpose,
    matching: members.length,
    withAddress: withAddress.length,
    agreed: agreed.length,
    willReceive: new Set(agreed.map((m) => m.email.trim().toLowerCase())).size,
  };
}

async function demoSend(campaign: Campaign): Promise<Campaign> {
  const members = await demoMembers(campaign.segment);
  const addresses = new Set<string>();
  const now = new Date().toISOString();
  for (const m of members) {
    const address = (m.email ?? '').trim();
    const refused = campaign.purpose === 'marketing' && demoMarketingConsent(m.id) !== 'granted';
    const duplicate = !refused && addresses.has(address.toLowerCase());
    if (!refused) addresses.add(address.toLowerCase());
    addDemoCommunication({
      id: mockId('com'), memberId: m.id, memberName: `${m.firstName} ${m.lastName}`, email: address, campaignId: campaign.id,
      campaignName: campaign.name, channel: 'Email', category: campaign.category, purpose: campaign.purpose, subject: campaign.subject,
      preview: campaign.body.slice(0, 140),
      // Nothing leaves this browser in the demo; the statuses show what the server would do.
      status: refused ? 'suppressed' : duplicate ? 'skipped' : 'sent',
      reason: refused ? 'This member has not agreed to marketing by email.' : duplicate ? 'Another member at this address gets this campaign.' : null,
      createdAtUtc: now, sentAtUtc: refused || duplicate ? null : now,
    });
  }
  return withDemoTotals(campaignStore.update(campaign.id, { status: 'Sent', sentAtUtc: now, sendingStartedAtUtc: now, scheduledAtUtc: null })!);
}

// ── API ─────────────────────────────────────────────────────────────────────

export async function getCampaigns(req: RequestOptions = {}): Promise<Campaign[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Campaign[]>('/api/campaigns', { signal: req.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...campaignStore.list()]
        .sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc))
        .map(withDemoTotals);
    },
  );
}

/** Every member the campaign was for, with where their message stands. */
export async function getCampaignRecipients(id: string, req: RequestOptions = {}): Promise<Communication[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Communication[]>(`/api/campaigns/${encodeURIComponent(id)}/recipients`, { signal: req.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => demoCommunications().filter((c) => c.campaignId === id).sort((a, b) => a.memberName.localeCompare(b.memberName)),
  );
}

/** Who a campaign for this segment and purpose would reach if it were sent now. */
export async function getCampaignAudience(segment: SegmentFilter, purpose: CampaignPurpose, req: RequestOptions = {}): Promise<CampaignAudience> {
  return apiWithFallback(
    () => {
      const params = new URLSearchParams({ purpose });
      for (const tier of segment.tiers ?? []) params.append('tiers', tier);
      for (const status of segment.statuses ?? []) params.append('statuses', status);
      return authFetchJson<CampaignAudience>(`/api/campaigns/audience?${params.toString()}`, { signal: req.signal });
    },
    () => demoAudience(segment, purpose),
  );
}

export async function createCampaign(input: CampaignInput): Promise<Campaign> {
  return apiWithFallback(
    () => authFetchJson<Campaign>('/api/campaigns', { method: 'POST', body: JSON.stringify({ ...input, channel: 'Email' }) }),
    async () => {
      await delay(100);
      return campaignStore.add({
        id: mockId('cmp'),
        channel: 'Email',
        createdAtUtc: new Date().toISOString(),
        status: input.scheduledAtUtc ? 'Scheduled' : 'Draft',
        recipientCount: 0,
        ...input,
      });
    },
  );
}

/** Changes a draft or scheduled campaign; refused (409) once it has started sending or changed meanwhile. */
export async function updateCampaign(id: string, patch: Partial<CampaignInput>, version?: string): Promise<Campaign> {
  return apiWithFallback(
    () => authFetchJson<Campaign>(`/api/campaigns/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify({ ...patch, version }),
    }),
    async () => {
      await delay(80);
      const current = campaignStore.byId(id);
      if (!current || !isEditable(current.status)) throw new Error('This campaign has started sending, so it can no longer be changed.');
      const scheduled = patch.scheduledAtUtc === undefined ? current.scheduledAtUtc : patch.scheduledAtUtc;
      return campaignStore.update(id, { ...patch, scheduledAtUtc: scheduled ?? null, status: scheduled ? 'Scheduled' : 'Draft' })!;
    },
  );
}

/** Deletes a draft or scheduled campaign; one that has started sending is kept (409). */
export async function deleteCampaign(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/campaigns/${encodeURIComponent(id)}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(60);
      const current = campaignStore.byId(id);
      if (current && !isEditable(current.status)) throw new Error('A campaign that has started sending is kept, with what went to whom.');
      return campaignStore.remove(id);
    },
  );
}

/** Sends it now, once. Refused (409) when it is being sent or has been. */
export async function sendCampaign(id: string, version?: string): Promise<Campaign> {
  return apiWithFallback(
    () => authFetchJson<Campaign>(`/api/campaigns/${encodeURIComponent(id)}/send`, {
      method: 'POST',
      headers: version ? { 'If-Match': `"${version}"` } : undefined,
    }),
    async () => {
      await delay(150);
      const current = campaignStore.byId(id);
      if (!current) throw new Error('That campaign was not found.');
      if (!isEditable(current.status)) throw new Error('This campaign is being sent or has been, so it cannot be sent again.');
      return demoSend(current);
    },
  );
}

/** Emails a copy marked as a test to the signed-in person alone. */
export async function sendCampaignTest(id: string): Promise<CampaignTestResult> {
  return apiWithFallback(
    () => authFetchJson<CampaignTestResult>(`/api/campaigns/${encodeURIComponent(id)}/test`, { method: 'POST' }),
    async () => {
      await delay(120);
      return { messageId: mockId('test'), to: 'demo@cadence.example', status: 'sent' as const };
    },
  );
}

export interface CampaignStats {
  drafts: number;
  scheduled: number;
  sent: number;
  /** Emails the mail server took, across sent campaigns. */
  emailsSent: number;
}

export function computeCampaignStats(campaigns: Campaign[]): CampaignStats {
  let drafts = 0;
  let scheduled = 0;
  let sent = 0;
  let emailsSent = 0;
  for (const c of campaigns) {
    if (c.status === 'Draft') drafts++;
    else if (c.status === 'Scheduled') scheduled++;
    else if (c.status === 'Sent' || c.status === 'Sending') sent++;
    emailsSent += c.totals?.sent ?? 0;
  }
  return { drafts, scheduled, sent, emailsSent };
}
