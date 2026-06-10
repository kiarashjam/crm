// Membership applications.
//
// Prospective members fill out an application; the club reviews it and either
// approves (promoting them into a real Member record) or rejects. Approval is
// the canonical "join" path — it's where committee workflow lives.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { createMember, type MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type ApplicationStatus = 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Waitlisted';

export interface Application {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  requestedTier: MemberTier;
  referredByName?: string;
  occupation?: string;
  reasonForJoining?: string;
  status: ApplicationStatus;
  submittedAtUtc: string;
  reviewedAtUtc?: string;
  reviewerNotes?: string;
}

export const APPLICATION_STATUSES: ApplicationStatus[] = [
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'Waitlisted',
];

const day = 86_400_000;

const seed: Application[] = [
  {
    id: 'app-seed-1',
    firstName: 'Esther',
    lastName: 'Voss',
    email: 'esther.voss@example.com',
    phone: '+1 (646) 555-0188',
    requestedTier: 'Gold',
    referredByName: 'Amelia Hartwell',
    occupation: 'Architect — Voss & Park',
    reasonForJoining: 'Looking for a dining + workspace home base near the studio; love the chef series.',
    status: 'Submitted',
    submittedAtUtc: new Date(Date.now() - 2 * day).toISOString(),
  },
  {
    id: 'app-seed-2',
    firstName: 'Theo',
    lastName: 'Kim',
    email: 'theo.kim@example.com',
    phone: '+1 (917) 555-0144',
    requestedTier: 'Platinum',
    referredByName: 'Priya Raman',
    occupation: 'GP, Northgate Ventures',
    reasonForJoining: 'Need event space for portfolio dinners; spouse interested in spa.',
    status: 'UnderReview',
    submittedAtUtc: new Date(Date.now() - 8 * day).toISOString(),
    reviewerNotes: 'Strong referral; committee meets Tuesday.',
  },
  {
    id: 'app-seed-3',
    firstName: 'Mira',
    lastName: 'Sandoval',
    email: 'mira.sandoval@example.com',
    requestedTier: 'Silver',
    occupation: 'Food writer',
    reasonForJoining: 'Writing a piece on private dining; would love community access.',
    status: 'Waitlisted',
    submittedAtUtc: new Date(Date.now() - 21 * day).toISOString(),
    reviewedAtUtc: new Date(Date.now() - 14 * day).toISOString(),
    reviewerNotes: 'Tier full; revisit Q3.',
  },
  {
    id: 'app-seed-4',
    firstName: 'Marcus',
    lastName: 'Lindgren',
    email: 'marcus.lindgren@example.com',
    requestedTier: 'Bronze',
    occupation: 'Founder, Northwind Studio',
    referredByName: 'Priya Raman',
    reasonForJoining: 'Coworking + occasional dinners.',
    status: 'Approved',
    submittedAtUtc: new Date(Date.now() - 14 * day).toISOString(),
    reviewedAtUtc: new Date(Date.now() - 6 * day).toISOString(),
  },
  {
    id: 'app-seed-5',
    firstName: 'Naomi',
    lastName: 'Albright',
    email: 'naomi.albright@example.com',
    requestedTier: 'Gold',
    occupation: 'Surgeon',
    reasonForJoining: 'Stress recovery — spa, fitness, dining.',
    status: 'Rejected',
    submittedAtUtc: new Date(Date.now() - 40 * day).toISOString(),
    reviewedAtUtc: new Date(Date.now() - 30 * day).toISOString(),
    reviewerNotes: 'Member references did not respond; reopen if she reapplies.',
  },
];

const applicationStore = createMockStore<Application>({
  storageKey: 'crm.mock.applications.v1',
  seed,
  idOf: (a) => a.id,
});

export async function getApplications(): Promise<Application[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Application[]>('/api/applications');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...applicationStore.list()].sort(
        (a, b) => Date.parse(b.submittedAtUtc) - Date.parse(a.submittedAtUtc),
      );
    },
  );
}

export type ApplicationInput = Omit<Application, 'id' | 'submittedAtUtc' | 'reviewedAtUtc' | 'status'> & {
  status?: ApplicationStatus;
};

export async function createApplication(input: ApplicationInput): Promise<Application | null> {
  return apiWithFallback(
    () =>
      authFetchJson<Application>('/api/applications', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(160);
      return applicationStore.add({
        id: mockId('app'),
        submittedAtUtc: new Date().toISOString(),
        status: input.status ?? 'Submitted',
        ...input,
      });
    },
  );
}

export async function updateApplication(
  id: string,
  patch: Partial<Application>,
): Promise<Application | null> {
  return apiWithFallback(
    () =>
      authFetchJson<Application>(`/api/applications/${id}`, {
        method: 'PUT',
        body: JSON.stringify(patch),
      }),
    async () => {
      await delay(120);
      return applicationStore.update(id, patch);
    },
  );
}

export async function deleteApplication(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/applications/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return applicationStore.remove(id);
    },
  );
}

/**
 * Approval flow: mark application Approved and create a Pending member record
 * sized to the requested tier. The new member starts in Pending until first
 * dues clear — that mirrors how a real club operates.
 */
export async function approveApplication(
  id: string,
  reviewerNotes?: string,
): Promise<Application | null> {
  const application = applicationStore.byId(id);
  if (!application) return null;
  const monthlyByTier: Record<MemberTier, number> = {
    Bronze: 49,
    Silver: 129,
    Gold: 299,
    Platinum: 599,
  };
  const yearFromNow = new Date(Date.now() + 365 * day).toISOString();
  await createMember({
    firstName: application.firstName,
    lastName: application.lastName,
    email: application.email,
    phone: application.phone,
    tier: application.requestedTier,
    status: 'Pending',
    duesAmount: monthlyByTier[application.requestedTier],
    duesFrequency: 'Monthly',
    joinedAtUtc: new Date().toISOString(),
    renewsAtUtc: yearFromNow,
    notes: application.reasonForJoining,
  });
  return applicationStore.update(id, {
    status: 'Approved',
    reviewedAtUtc: new Date().toISOString(),
    reviewerNotes: reviewerNotes ?? application.reviewerNotes,
  });
}

export interface ApplicationStats {
  total: number;
  submitted: number;
  underReview: number;
  waitlisted: number;
  approvedThisMonth: number;
  rejectedThisMonth: number;
}

export function computeApplicationStats(apps: Application[]): ApplicationStats {
  const monthAgo = Date.now() - 30 * day;
  let submitted = 0;
  let underReview = 0;
  let waitlisted = 0;
  let approvedThisMonth = 0;
  let rejectedThisMonth = 0;
  for (const a of apps) {
    if (a.status === 'Submitted') submitted++;
    else if (a.status === 'UnderReview') underReview++;
    else if (a.status === 'Waitlisted') waitlisted++;
    const reviewed = a.reviewedAtUtc ? Date.parse(a.reviewedAtUtc) : 0;
    if (a.status === 'Approved' && reviewed >= monthAgo) approvedThisMonth++;
    if (a.status === 'Rejected' && reviewed >= monthAgo) rejectedThisMonth++;
  }
  return {
    total: apps.length,
    submitted,
    underReview,
    waitlisted,
    approvedThisMonth,
    rejectedThisMonth,
  };
}
