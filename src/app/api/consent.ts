// A member's consent to be contacted, per channel and purpose.
//
// nFADP and GDPR expect a club to show, for everyone it sends news and offers to, that
// they agreed: how, when and to what. Each answer is kept; the latest for a channel and
// purpose stands. Marketing goes only to channels where the member said yes; service
// messages (statements, bookings, receipts) go whatever they said. An unsubscribe link in
// a marketing email records a withdrawal here by itself.

import { apiWithFallback, authFetchJson, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

export type ConsentChannel = 'email' | 'sms' | 'post';
export type ConsentPurpose = 'marketing' | 'service';
export type ConsentState = 'granted' | 'withdrawn' | 'unknown';
export type ConsentSource = 'form' | 'import' | 'staff' | 'member' | 'unsubscribe';
/** What staff may record by hand; an import and the unsubscribe link record themselves. */
export type RecordableConsentSource = 'form' | 'member' | 'staff';

export const CONSENT_CHANNELS: ConsentChannel[] = ['email', 'sms', 'post'];
export const CONSENT_PURPOSES: ConsentPurpose[] = ['marketing', 'service'];
export const RECORDABLE_SOURCES: RecordableConsentSource[] = ['form', 'member', 'staff'];

export const CONSENT_CHANNEL_LABEL: Record<ConsentChannel, string> = { email: 'Email', sms: 'SMS', post: 'Post' };
export const CONSENT_PURPOSE_LABEL: Record<ConsentPurpose, string> = { marketing: 'Marketing', service: 'Service messages' };
export const CONSENT_SOURCE_LABEL: Record<ConsentSource, string> = {
  form: 'On a form',
  import: 'Imported',
  staff: 'Staff decision',
  member: 'The member told us',
  unsubscribe: 'Unsubscribe link',
};

/** Where one channel and purpose stands. */
export interface ConsentStateRow {
  channel: ConsentChannel;
  purpose: ConsentPurpose;
  state: ConsentState;
  source?: ConsentSource | null;
  recordedAtUtc?: string | null;
  recordedByName?: string | null;
  detail?: string | null;
}

/** One answer as recorded. */
export interface ConsentRecordRow {
  id: string;
  channel: ConsentChannel;
  purpose: ConsentPurpose;
  granted: boolean;
  source: ConsentSource;
  address?: string | null;
  detail?: string | null;
  recordedAtUtc: string;
  recordedByName?: string | null;
  isCurrent: boolean;
}

export interface MemberConsent {
  memberId: string;
  current: ConsentStateRow[];
  history: ConsentRecordRow[];
}

export interface ConsentInput {
  channel: ConsentChannel;
  purpose: ConsentPurpose;
  granted: boolean;
  source: RecordableConsentSource;
  detail?: string;
}

// ── demo ────────────────────────────────────────────────────────────────────

interface DemoConsentRecord extends ConsentRecordRow {
  memberId: string;
}

const demoStore = createMockStore<DemoConsentRecord>({
  storageKey: 'crm.mock.memberConsent.v1',
  seed: [
    {
      id: 'consent-seed-1', memberId: 'mem-seed-1', channel: 'email', purpose: 'marketing', granted: true, source: 'form',
      detail: 'Ticked the newsletter box on the membership form', recordedAtUtc: new Date(Date.now() - 400 * 86_400_000).toISOString(),
      recordedByName: 'Demo User', isCurrent: true,
    },
  ],
  idOf: (r) => r.id,
});

function demoConsent(memberId: string): MemberConsent {
  const history = demoStore.list()
    .filter((r) => r.memberId === memberId)
    .sort((a, b) => Date.parse(b.recordedAtUtc) - Date.parse(a.recordedAtUtc));
  const current: ConsentStateRow[] = [];
  for (const channel of CONSENT_CHANNELS) {
    for (const purpose of CONSENT_PURPOSES) {
      const standing = history.find((r) => r.channel === channel && r.purpose === purpose);
      current.push(standing
        ? {
          channel, purpose, state: standing.granted ? 'granted' : 'withdrawn', source: standing.source,
          recordedAtUtc: standing.recordedAtUtc, recordedByName: standing.recordedByName, detail: standing.detail,
        }
        : { channel, purpose, state: 'unknown' });
    }
  }
  return { memberId, current, history: history.map((r, i) => ({ ...r, isCurrent: history.findIndex((h) => h.channel === r.channel && h.purpose === r.purpose) === i })) };
}

/** Which demo members agreed to marketing by email: what demo campaigns and messages read. */
export function demoMarketingConsent(memberId: string): ConsentState {
  return demoConsent(memberId).current.find((s) => s.channel === 'email' && s.purpose === 'marketing')?.state ?? 'unknown';
}

// ── API ─────────────────────────────────────────────────────────────────────

export async function getMemberConsent(memberId: string, req: RequestOptions = {}): Promise<MemberConsent> {
  return apiWithFallback(
    () => authFetchJson<MemberConsent>(`/api/members/${encodeURIComponent(memberId)}/consent`, { signal: req.signal }),
    async () => demoConsent(memberId),
  );
}

export async function recordMemberConsent(memberId: string, input: ConsentInput): Promise<MemberConsent> {
  return apiWithFallback(
    () => authFetchJson<MemberConsent>(`/api/members/${encodeURIComponent(memberId)}/consent`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
    async () => {
      demoStore.add({
        id: mockId('consent'), memberId, channel: input.channel, purpose: input.purpose, granted: input.granted,
        source: input.source, detail: input.detail?.trim() || null, recordedAtUtc: new Date().toISOString(),
        recordedByName: 'Demo User', isCurrent: true,
      });
      return demoConsent(memberId);
    },
  );
}
