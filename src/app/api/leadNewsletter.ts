// Lead newsletter: from the Leads page, one email to the leads the filters choose, inviting
// each to book a meeting on the workspace's website with a link and a code (RDV-XXXX) of
// their own. They pick a weekday, then a time.
//
// The server decides everything that matters: who it goes to (the same way for the counts
// the dialog shows and for the send), each lead's link and code, and the email itself, which
// it fills in and renders. This only shows what it answers.
//
// Like contracts, these calls have NO demo-mode fallback: an invitation that pretends to
// have been sent, with a booking link that goes nowhere, is worse than a feature that says
// it needs a server.
//
// Mirrors backend/src/ACI.Application/DTOs/LeadNewsletterDtos.cs (JSON in camelCase).

import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

export type NewsletterLanguage = 'fr' | 'en';

/** Why a lead the filters chose is left out. The server may add reasons: show any other as it is. */
export type NewsletterLeftOutReason = 'no_email' | 'do_not_contact' | 'suppressed' | 'duplicate';

/**
 * Which leads a newsletter goes to. Empty lists (or left out) mean every one; a lead always
 * needs an email address. Statuses and sources are matched by name, ignoring case.
 */
export interface NewsletterAudienceRequest {
  /** Only leads in these statuses; none: every status. */
  includeStatuses?: string[] | null;
  /** Never leads in these statuses ("Not interested", "Lost"…). */
  excludeStatuses?: string[] | null;
  /** Only leads from these sources; none: every source. */
  sources?: string[] | null;
  /** Only leads assigned to these people; none (and not `unassigned`): anyone's. */
  ownerIds?: string[] | null;
  /** Leads assigned to nobody, besides any in `ownerIds`. */
  unassigned?: boolean;
  /** Created on or after this day (UTC), yyyy-mm-dd. */
  createdFrom?: string | null;
  /** Created on or before this day (UTC), yyyy-mm-dd. */
  createdTo?: string | null;
  /** Leads already converted into a contact or deal too. Off: they are left out. */
  includeConverted?: boolean;
}

/** A lead in the audience, or left out of it with why. */
export interface NewsletterPerson {
  leadId: string;
  name: string;
  email?: string | null;
  status: string;
  /** For one left out: "no_email", "do_not_contact", "suppressed" or "duplicate". */
  reason?: NewsletterLeftOutReason | (string & {}) | null;
}

/** Who a newsletter with these filters would reach now, counted by the server. */
export interface NewsletterAudience {
  /** Leads the filters choose. */
  matching: number;
  /** Emails that would go: one per lead, one per address. */
  willReceive: number;
  /** No usable email address. */
  noEmail: number;
  /** Marked do not contact (an unsubscribe marks them so). */
  doNotContact: number;
  /** Their address unsubscribed through the mail service, bounced or complained. */
  suppressed: number;
  /** Another chosen lead has the same address and gets the one email. */
  duplicates: number;
  /** The first of those who get it, for the list's preview. */
  recipients: NewsletterPerson[];
  /** The first of those left out, with why. */
  leftOut: NewsletterPerson[];
  /** The most one newsletter goes to. */
  maxRecipients: number;
  /** A meeting already booked and still to come. */
  alreadyBooked?: number;
}

/** What a newsletter can do in this workspace: whether it can give each lead a booking link, and if not, why. */
export interface NewsletterSetup {
  bookingLinkAvailable: boolean;
  /** Why not, in a sentence that says where to set it. */
  bookingProblem?: string | null;
  bookingPageUrl?: string | null;
  /** The placeholders the email may use: firstName, lastName, name, bookingUrl, bookingCode, workspace. */
  fields: string[];
  workspace?: string | null;
  defaultLanguage: NewsletterLanguage | (string & {});
  fromName?: string | null;
  replyTo?: string | null;
  maxRecipients: number;
}

/** The email: its language, subject and HTML, with placeholders. */
export interface NewsletterEmailRequest {
  language?: NewsletterLanguage | null;
  subject?: string | null;
  html?: string | null;
  /** The lead the preview or test is filled in for; none: a sample person. */
  sampleLeadId?: string | null;
}

/** The email as one lead will read it. Its booking link is a test one. */
export interface NewsletterPreview {
  subject: string;
  html: string;
  /** The plain-text part that goes with it. */
  text: string;
  sampleName: string;
  sampleEmail?: string | null;
  bookingCode: string;
  /** Placeholders nothing fills, as written. */
  unknown: string[];
  /** Why {{bookingUrl}} cannot be filled, when the email uses it and it cannot. */
  bookingProblem?: string | null;
  fromName?: string | null;
  replyTo?: string | null;
}

/**
 * The newsletter to send: the email, the filters, and the id made when Send was pressed.
 * The same id sent again sends nothing more and answers with the first send.
 */
export interface NewsletterSendRequest extends NewsletterEmailRequest {
  sendId: string;
  audience?: NewsletterAudienceRequest | null;
  /** How many the person confirmed it would go to: when the audience has changed since, nothing is sent. */
  expectedRecipients?: number | null;
}

export interface NewsletterSendResult {
  id: string;
  /** "sending" or "sent". */
  status: 'sending' | 'sent' | (string & {});
  recipients: number;
  leftOut: number;
  /** The id had been sent before: nothing more went. */
  alreadySent: boolean;
  createdAtUtc: string;
}

export interface NewsletterTestResult {
  to: string;
  /** "sent", or "queued" when the mail server could not take it yet and it is tried again. */
  status: 'sent' | 'queued' | (string & {});
}

/** Thrown in demo mode, where there is no server to count, render or send anything. */
export class NewsletterNeedsServerError extends Error {
  constructor() {
    super('Inviting leads to a meeting needs a connected server. In demo mode there is nowhere to send it from.');
    this.name = 'NewsletterNeedsServerError';
  }
}

function requireApi(): void {
  if (servesDemoData()) throw new NewsletterNeedsServerError();
}

const BASE = '/api/leads/newsletter';

/** Whether each lead can be given a booking link here (and if not, why), who it goes out as, and its fields. */
export async function getNewsletterSetup(options: RequestOptions = {}): Promise<NewsletterSetup> {
  requireApi();
  return authFetchJson<NewsletterSetup>(`${BASE}/setup`, { signal: options.signal });
}

/** Who a newsletter with these filters would reach now, and who is left out and why. */
export async function getNewsletterAudience(filters: NewsletterAudienceRequest, options: RequestOptions = {}): Promise<NewsletterAudience> {
  requireApi();
  return authFetchJson<NewsletterAudience>(`${BASE}/audience`, { method: 'POST', body: JSON.stringify(filters), signal: options.signal });
}

/** The email as one lead (or a sample person) will read it, rendered by the server. */
export async function previewNewsletter(request: NewsletterEmailRequest, options: RequestOptions = {}): Promise<NewsletterPreview> {
  requireApi();
  return authFetchJson<NewsletterPreview>(`${BASE}/preview`, { method: 'POST', body: JSON.stringify(request), signal: options.signal });
}

/** The email as the preview shows it, marked as a test, to the signed-in person's own address and nobody else. */
/** A test of the email, to the address given, or the signed-in person's own. */
export interface NewsletterTestRequest extends NewsletterEmailRequest {
  to?: string | null;
}

export async function sendNewsletterTest(request: NewsletterTestRequest): Promise<NewsletterTestResult> {
  requireApi();
  return authFetchJson<NewsletterTestResult>(`${BASE}/test`, { method: 'POST', body: JSON.stringify(request) });
}

/** Queues one email per lead the filters choose. Sent again with the same `sendId`, nothing more goes. */
export async function sendNewsletter(request: NewsletterSendRequest): Promise<NewsletterSendResult> {
  requireApi();
  return authFetchJson<NewsletterSendResult>(`${BASE}/send`, { method: 'POST', body: JSON.stringify(request) });
}
