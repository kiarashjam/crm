// Membership applications: the step from a lead to a member.
//
// An application is typed in by staff, started from a lead's or a contact's page, or sent
// by a stranger on the club's public form. The server approves it in one transaction: the
// member at their tier's dues, linked to their lead and contact, with the consent they gave,
// their dues subscription, their initiation fee on the ledger, the household it names and a
// welcome through the outbox. Approving again (a retry, a double click, a second reviewer)
// answers with the member made the first time, and posts nothing more.
//
// Approval used to be three requests from this browser (read, add a member, mark approved),
// so a retry or two reviewers at once made two members, who were never billed. Demo data
// approves in this browser, as one person working alone.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, getApiBaseUrl, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { createMember, type MemberTier } from './members';
import type { Household } from './households';

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
  /** staff, form (the public form), lead (started from a lead or contact) or moved. */
  source?: string;
  submittedAtUtc: string;
  reviewedAtUtc?: string;
  reviewerNotes?: string;
  leadId?: string;
  leadName?: string;
  contactId?: string;
  contactName?: string;
  householdId?: string;
  householdName?: string;
  householdRole?: string;
  /** The member the approval made (or found). */
  memberId?: string;
  memberName?: string;
  approvedAtUtc?: string;
  /** staff, contract (a countersigned membership contract), deposit or moved. */
  approvalTrigger?: string;
  contractId?: string;
  duesSubscriptionId?: string;
  initiationChargeId?: string;
  /** Approved by someone who may not charge accounts: the dues and fee are left for someone who may. */
  moneyPending?: boolean;
  /** The marketing answer given on the public form: granted, withdrawn or unknown. */
  marketingConsent?: string;
  version?: string;
}

export const APPLICATION_STATUSES: ApplicationStatus[] = ['Submitted', 'UnderReview', 'Approved', 'Rejected', 'Waitlisted'];

/** An application still to be decided: it can be approved. */
export function isOpen(status: ApplicationStatus): boolean {
  return status === 'Submitted' || status === 'UnderReview' || status === 'Waitlisted';
}

/** The statuses an application can be moved to by hand from each status (approval has its own button). */
export const STATUS_MOVES: Record<ApplicationStatus, ApplicationStatus[]> = {
  Submitted: ['UnderReview', 'Waitlisted', 'Rejected'],
  UnderReview: ['Submitted', 'Waitlisted', 'Rejected'],
  Waitlisted: ['Submitted', 'UnderReview', 'Rejected'],
  Rejected: ['Submitted'],
  Approved: [],
};

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
    source: 'staff',
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
    source: 'form',
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
    source: 'staff',
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
    source: 'staff',
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
    source: 'staff',
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

function demoFind(id: string): Application {
  const found = applicationStore.list().find((a) => a.id === id);
  if (!found) throw new Error('That application is no longer there.');
  return found;
}

// ── the list ─────────────────────────────────────────────────────────────────

export async function getApplications(options: RequestOptions = {}): Promise<Application[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Application[]>('/api/applications', { signal: options.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...applicationStore.list()].sort((a, b) => Date.parse(b.submittedAtUtc) - Date.parse(a.submittedAtUtc));
    },
  );
}

export interface ApplicationInput {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  requestedTier: MemberTier;
  referredByName?: string;
  occupation?: string;
  reasonForJoining?: string;
  leadId?: string;
  contactId?: string;
  householdId?: string;
  householdRole?: string;
}

export async function createApplication(input: ApplicationInput): Promise<Application> {
  return apiWithFallback(
    () => authFetchJson<Application>('/api/applications', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return applicationStore.add({ id: mockId('app'), submittedAtUtc: new Date().toISOString(), status: 'Submitted', source: 'staff', ...input });
    },
  );
}

/** Changes an application's details (not its status: see setApplicationStatus and approveApplication). */
export async function updateApplication(id: string, patch: Partial<ApplicationInput> & { reviewerNotes?: string; version?: string }): Promise<Application> {
  return apiWithFallback(
    () => authFetchJson<Application>(`/api/applications/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      demoFind(id);
      const { version: _version, ...fields } = patch;
      return applicationStore.update(id, fields)!;
    },
  );
}

/**
 * Moves an application: to review, to the waitlist, rejected, or back to Submitted to
 * reopen it. A rejected one must be reopened before it can be approved.
 */
export async function setApplicationStatus(id: string, status: ApplicationStatus, reviewerNotes?: string, version?: string): Promise<Application> {
  return apiWithFallback(
    () => authFetchJson<Application>(`/api/applications/${id}/status`, { method: 'POST', body: JSON.stringify({ status, reviewerNotes, version }) }),
    async () => {
      await delay(100);
      const app = demoFind(id);
      if (!STATUS_MOVES[app.status].includes(status)) throw new Error(`An application that is ${app.status} cannot be moved to ${status}.`);
      return applicationStore.update(id, {
        status,
        ...(status === 'Waitlisted' || status === 'Rejected' ? { reviewedAtUtc: new Date().toISOString() } : {}),
        ...(reviewerNotes !== undefined ? { reviewerNotes } : {}),
      })!;
    },
  );
}

export async function deleteApplication(id: string): Promise<void> {
  await apiWithFallback(
    async () => {
      await ensureOk(await authFetch(`/api/applications/${id}`, { method: 'DELETE' }));
    },
    async () => {
      await delay(100);
      applicationStore.remove(id);
    },
  );
}

// ── approval ─────────────────────────────────────────────────────────────────

export interface ApproveOptions {
  reviewerNotes?: string;
  /** The tier to admit them at, when not the one they asked for. */
  tier?: string;
  /** A household to join (else the application's), as what, and whether their bills roll up (money.manage). */
  householdId?: string;
  householdRole?: string;
  rollUpBilling?: boolean;
  /** Start the dues and charge the initiation fee (yes unless false; money.manage). */
  startDues?: boolean;
  chargeInitiationFee?: boolean;
  /** Queue the welcome email (yes unless false). */
  sendWelcome?: boolean;
  version?: string;
}

/** What an approval did. */
export interface ApprovalResult {
  application: Application;
  memberId: string;
  memberName: string;
  /** A new member was made; false when the applicant already was one, or an earlier press did it. */
  memberCreated: boolean;
  /** This press approved it; false when an earlier one had. */
  approved: boolean;
  duesStarted: boolean;
  feeCharged: boolean;
  joinedHousehold: boolean;
  welcomeQueued: boolean;
  moneyPending: boolean;
  /** What was not done and why, in sentences. */
  notes: string[];
}

/**
 * Approves the application on the server, in one go. Pressed again on an approved one, it
 * answers with the member already made, and finishes the dues and fee left pending when
 * the person pressing may charge accounts.
 */
export async function approveApplication(id: string, options: ApproveOptions = {}): Promise<ApprovalResult> {
  return apiWithFallback(
    () => authFetchJson<ApprovalResult>(`/api/applications/${id}/approve`, { method: 'POST', body: JSON.stringify(options) }),
    async () => {
      await delay(160);
      const app = demoFind(id);
      if (app.status === 'Approved' && app.memberId) {
        return {
          application: app, memberId: app.memberId, memberName: app.memberName ?? `${app.firstName} ${app.lastName}`,
          memberCreated: false, approved: false, duesStarted: false, feeCharged: false, joinedHousehold: false,
          welcomeQueued: false, moneyPending: false, notes: [],
        };
      }
      if (!isOpen(app.status)) throw new Error('A rejected application must be reopened before it can be approved.');
      const member = await createMember({
        firstName: app.firstName,
        lastName: app.lastName,
        email: app.email,
        phone: app.phone,
        tier: options.tier ?? app.requestedTier,
        status: 'Active',
        joinedAtUtc: new Date().toISOString(),
        renewsAtUtc: new Date(Date.now() + 365 * day).toISOString(),
        notes: app.reasonForJoining,
      });
      const updated = applicationStore.update(id, {
        status: 'Approved',
        reviewedAtUtc: new Date().toISOString(),
        approvedAtUtc: new Date().toISOString(),
        approvalTrigger: 'staff',
        reviewerNotes: options.reviewerNotes ?? app.reviewerNotes,
        memberId: member?.id,
        memberName: `${app.firstName} ${app.lastName}`,
      })!;
      return {
        application: updated, memberId: member?.id ?? '', memberName: `${app.firstName} ${app.lastName}`,
        memberCreated: true, approved: true, duesStarted: false, feeCharged: false, joinedHousehold: false,
        welcomeQueued: false, moneyPending: false,
        notes: ['Demo data: no dues, fee or welcome email are posted in this browser.'],
      };
    },
  );
}

/** Starts an application for a lead, from what the lead says (or answers the open one). */
export async function startApplicationFromLead(leadId: string): Promise<Application> {
  return apiWithFallback(
    () => authFetchJson<Application>(`/api/applications/from-lead/${leadId}`, { method: 'POST' }),
    async () => {
      const { getLeadById } = await import('./leads');
      const lead = await getLeadById(leadId);
      if (!lead) throw new Error('That lead is no longer there.');
      return demoStart(lead.name, lead.email, { leadId });
    },
  );
}

/** Starts an application for a contact (or answers the open one). */
export async function startApplicationFromContact(contactId: string): Promise<Application> {
  return apiWithFallback(
    () => authFetchJson<Application>(`/api/applications/from-contact/${contactId}`, { method: 'POST' }),
    async () => {
      const { getContactById } = await import('./contacts');
      const contact = await getContactById(contactId);
      if (!contact) throw new Error('That contact is no longer there.');
      return demoStart(contact.name, contact.email, { contactId });
    },
  );
}

async function demoStart(name: string, email: string, link: { leadId?: string; contactId?: string }): Promise<Application> {
  const open = applicationStore.list().find((a) => isOpen(a.status)
    && ((link.leadId && a.leadId === link.leadId) || (link.contactId && a.contactId === link.contactId)));
  if (open) return open;
  const [first, ...rest] = name.trim().split(/\s+/);
  return createApplication({ firstName: first ?? name, lastName: rest.join(' '), email, requestedTier: 'Gold', ...link });
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
    const reviewed = Date.parse(a.approvedAtUtc ?? a.reviewedAtUtc ?? '') || 0;
    if (a.status === 'Approved' && reviewed >= monthAgo) approvedThisMonth++;
    if (a.status === 'Rejected' && reviewed >= monthAgo) rejectedThisMonth++;
  }
  return { total: apps.length, submitted, underReview, waitlisted, approvedThisMonth, rejectedThisMonth };
}

// ── the public form ──────────────────────────────────────────────────────────

/** The club's public application form: one link per workspace. */
export interface ApplicationForm {
  id: string;
  publicToken: string;
  /** Where the form is, on this app: /apply/{token}. */
  publicPath: string;
  isActive: boolean;
  intro?: string;
  successMessage?: string;
  consentText: string;
  createdAtUtc: string;
}

/** The workspace's public form, or null before one is opened. Demo data has none. */
export async function getApplicationForm(options: RequestOptions = {}): Promise<ApplicationForm | null> {
  return apiWithFallback(
    async () => (await authFetchJson<{ form: ApplicationForm | null }>('/api/applications/form', { signal: options.signal }))?.form ?? null,
    async () => null,
  );
}

const DEMO_FORM = 'The public application form is opened from the live app: a stranger applies to the club’s server, not to this browser.';

/** Opens, closes or rewords the public form (settings.manage). */
export async function saveApplicationForm(input: { isActive?: boolean; intro?: string; successMessage?: string; consentText?: string }): Promise<ApplicationForm> {
  return apiWithFallback(
    () => authFetchJson<ApplicationForm>('/api/applications/form', { method: 'PUT', body: JSON.stringify(input) }),
    async () => {
      throw new Error(DEMO_FORM);
    },
  );
}

/** Gives the form a new link; the old one stops working (settings.manage). */
export async function rotateApplicationForm(): Promise<ApplicationForm> {
  return apiWithFallback(
    () => authFetchJson<ApplicationForm>('/api/applications/form/rotate', { method: 'POST' }),
    async () => {
      throw new Error(DEMO_FORM);
    },
  );
}

export interface PublicTier {
  name: string;
  price: number;
  cadence: string;
  initiationFee?: number | null;
  currency: string;
}

export interface PublicApplicationForm {
  clubName: string;
  intro?: string | null;
  consentText: string;
  tiers: PublicTier[];
  /** Whether the form shows the marketing checkbox, and whether it must be ticked (the workspace's privacy settings). */
  marketingConsent?: 'off' | 'optional' | 'required';
  /** The club's language (from its locale: en, de, fr), for a visitor whose browser speaks none of the app's. */
  language?: string | null;
}

export interface PublicApplication {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  requestedTier?: string;
  occupation?: string;
  referredByName?: string;
  reasonForJoining?: string;
  marketingConsent: boolean;
  /** The honeypot: always empty when a person fills the form in. */
  website?: string;
  /** The language the form was filled in (en, de, fr): the thank-you, and the member's language once approved. */
  language?: string;
}

/** A public form request that failed, with its status (404: no such form; 429: too many). */
export class PublicApplicationError extends Error {
  readonly status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'PublicApplicationError';
    this.status = status;
  }
}

async function publicFetch(path: string, init?: RequestInit): Promise<Response> {
  const base = getApiBaseUrl();
  if (!base) throw new PublicApplicationError('VITE_API_URL is not set', 0);
  try {
    return await fetch(`${base}${path}`, init);
  } catch {
    throw new PublicApplicationError('Could not reach the server', 0);
  }
}

/**
 * The form a stranger sees: no sign-in, the link is the whole of it. `language` is the one
 * the page is read in, so the standard words the server adds (the news checkbox, when the
 * club wrote none) are in it too.
 */
export async function getPublicApplicationForm(token: string, language?: string): Promise<PublicApplicationForm> {
  const query = language ? `?lang=${encodeURIComponent(language)}` : '';
  const res = await publicFetch(`/api/public/apply/${encodeURIComponent(token)}${query}`);
  if (!res.ok) throw new PublicApplicationError('Form not available', res.status);
  return res.json() as Promise<PublicApplicationForm>;
}

/** Sends the application. Resolves to the thank-you the club wrote. */
export async function submitPublicApplication(token: string, body: PublicApplication): Promise<string | null> {
  const res = await publicFetch(`/api/public/apply/${encodeURIComponent(token)}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new PublicApplicationError('Could not send', res.status);
  try {
    return ((await res.json()) as { message?: string | null })?.message?.trim() || null;
  } catch {
    return null;
  }
}

// ── who is who: members, leads and contacts ──────────────────────────────────

export interface MemberSummary {
  id: string;
  name: string;
  email: string;
  tier: string;
  status: string;
  joinedAtUtc: string;
}

/** What a lead's or a contact's page shows of the club. */
export interface PersonMembership {
  contactId?: string;
  members: MemberSummary[];
  applications: Application[];
  household?: Household | null;
}

/** What a member's page links to on the sales side. */
export interface MemberLinks {
  memberId: string;
  leadId?: string;
  leadName?: string;
  leadStatus?: string;
  contactId?: string;
  contactName?: string;
  contactEmail?: string;
  applications: Application[];
  household?: Household | null;
}

export async function getLeadMembership(leadId: string, options: RequestOptions = {}): Promise<PersonMembership> {
  return apiWithFallback(
    () => authFetchJson<PersonMembership>(`/api/leads/${leadId}/membership`, { signal: options.signal }),
    async () => ({ members: [], applications: applicationStore.list().filter((a) => a.leadId === leadId), household: null }),
  );
}

export async function getContactMembership(contactId: string, options: RequestOptions = {}): Promise<PersonMembership> {
  return apiWithFallback(
    () => authFetchJson<PersonMembership>(`/api/contacts/${contactId}/membership`, { signal: options.signal }),
    async () => ({ contactId, members: [], applications: applicationStore.list().filter((a) => a.contactId === contactId), household: null }),
  );
}

export async function getMemberLinks(memberId: string, options: RequestOptions = {}): Promise<MemberLinks> {
  return apiWithFallback(
    () => authFetchJson<MemberLinks>(`/api/members/${memberId}/links`, { signal: options.signal }),
    async () => ({ memberId, applications: applicationStore.list().filter((a) => a.memberId === memberId), household: null }),
  );
}
