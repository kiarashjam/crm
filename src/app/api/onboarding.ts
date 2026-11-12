// Welcome & portal: when a lead becomes a deal, the person is handed to the
// workspace's own portal (Pavillon 46's member area, say). The portal opens their
// account and sends the welcome email with a link to set a password, and reports
// each step back. CadenceE never sends the welcome email itself.
//
// Server only: in demo mode there is no portal to hand anyone to, so the settings
// read as "not set up" and nothing is sent.

import { authFetchJson, servesDemoData } from './apiClient';

export type OnboardingLanguage = 'fr' | 'en';

export interface OnboardingSettings {
  webhookUrl?: string | null;
  hasSecret: boolean;
  secretHint?: string | null;
  enabled: boolean;
  emailSubject?: string | null;
  emailHtml?: string | null;
  defaultLanguage: OnboardingLanguage;
  addToClubMembers: boolean;
  /** An address and a secret: handoffs can go. */
  configured: boolean;
  updatedAtUtc?: string | null;
  /** Placeholders CadenceE fills. */
  cadenceFields: string[];
  /** Placeholders the portal fills: the set-password link, the portal's address, the login. */
  receiverFields: string[];
}

export interface SaveOnboardingSettings {
  webhookUrl?: string | null;
  enabled?: boolean;
  emailSubject?: string | null;
  emailHtml?: string | null;
  defaultLanguage?: OnboardingLanguage;
  addToClubMembers?: boolean;
}

export interface OnboardingPreviewRequest {
  dealId?: string;
  name?: string;
  email?: string;
  company?: string;
  dealName?: string;
  welcomeMessage?: string;
  startsOnUtc?: string | null;
  startUndetermined?: boolean;
  language?: OnboardingLanguage;
  emailSubject?: string | null;
  emailHtml?: string | null;
}

export interface OnboardingPreview {
  subject: string;
  html: string;
  /** No HTML of the workspace's own: the portal sends its own design, and this is a stand-in. */
  usesReceiverDesign: boolean;
  /** Placeholders nobody fills, as written. */
  unknown: string[];
}

export interface StartOnboardingRequest {
  leadId?: string;
  welcomeMessage?: string;
  /** yyyy-mm-dd; left out, the start is still to be decided. */
  startsOnUtc?: string | null;
  startUndetermined?: boolean;
  language?: OnboardingLanguage;
  emailSubject?: string | null;
  emailHtml?: string | null;
  addToClubMembers?: boolean;
}

export type OnboardingStatus = 'queued' | 'delivered' | 'account_ready' | 'welcomed' | 'active' | 'failed';

export interface OnboardingStep {
  key: string;
  atUtc: string;
  detail?: string | null;
}

export interface OnboardingDelivery {
  status: string;
  attempts: number;
  lastStatusCode?: number | null;
  lastError?: string | null;
  sentAtUtc?: string | null;
  nextAttemptAtUtc?: string | null;
}

export interface Onboarding {
  id: string;
  dealId: string;
  leadId?: string | null;
  contactId?: string | null;
  clubMemberId?: string | null;
  name: string;
  email: string;
  language: OnboardingLanguage;
  welcomeMessage?: string | null;
  startsOnUtc?: string | null;
  startUndetermined: boolean;
  status: OnboardingStatus;
  steps: OnboardingStep[];
  delivery?: OnboardingDelivery | null;
  externalAccountId?: string | null;
  canResend: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

const NOT_SET_UP: OnboardingSettings = {
  webhookUrl: null,
  hasSecret: false,
  enabled: false,
  defaultLanguage: 'fr',
  addToClubMembers: true,
  configured: false,
  cadenceFields: ['firstName', 'lastName', 'name', 'email', 'company', 'dealName', 'welcomeMessage', 'startDate', 'senderName', 'workspace'],
  receiverFields: ['setPasswordUrl', 'portalUrl', 'loginEmail'],
};

const DEMO_MESSAGE = 'Welcome & portal hands people to your own portal, so it works with the server, not in demo mode.';

export async function getOnboardingSettings(): Promise<OnboardingSettings> {
  if (servesDemoData()) return NOT_SET_UP;
  return authFetchJson<OnboardingSettings>('/api/onboarding/settings');
}

export async function saveOnboardingSettings(request: SaveOnboardingSettings): Promise<OnboardingSettings> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<OnboardingSettings>('/api/onboarding/settings', { method: 'PUT', body: JSON.stringify(request) });
}

/** A new signing secret, shown this once. */
export async function newOnboardingSecret(): Promise<string> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return (await authFetchJson<{ secret: string }>('/api/onboarding/settings/secret', { method: 'POST' })).secret;
}

export async function testOnboardingPortal(): Promise<{ ok: boolean; statusCode?: number | null; error?: string | null }> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson('/api/onboarding/settings/test', { method: 'POST' });
}

export async function previewOnboarding(request: OnboardingPreviewRequest): Promise<OnboardingPreview> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<OnboardingPreview>('/api/onboarding/preview', { method: 'POST', body: JSON.stringify(request) });
}

export async function startOnboarding(dealId: string, request: StartOnboardingRequest): Promise<Onboarding> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<Onboarding>(`/api/deals/${dealId}/onboarding`, { method: 'POST', body: JSON.stringify(request) });
}

export async function getDealOnboarding(dealId: string): Promise<Onboarding[]> {
  if (servesDemoData()) return [];
  return (await authFetchJson<Onboarding[]>(`/api/deals/${dealId}/onboarding`)) ?? [];
}

export async function resendOnboarding(id: string): Promise<Onboarding> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<Onboarding>(`/api/onboarding/${id}/resend`, { method: 'POST' });
}

/** Whether the portal has nothing more to report for now: they are in, or it stopped. */
export function onboardingSettled(o: Onboarding): boolean {
  return o.status === 'active' || o.status === 'failed' || (o.status === 'welcomed' && Date.now() - Date.parse(o.updatedAtUtc) > 60_000);
}
