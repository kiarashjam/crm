// "Invite members" on an event: staff choose an audience of members (the one campaigns use),
// write the letter in English, German and French, preview it as one member, test it on
// themselves and send it. Each member gets a reply link of their own to a public page where
// they answer "I'll come" (a seat while there is one, else the waitlist, by the event's own
// rules) or "I can't come", and may change their mind until the event starts. The event page
// lists who answered, reminds those who have not (once each), and estimates the attendance.
//
// The server decides everything that matters: who it goes to (the same way for the counts and
// the send), each member's letter in their language, their link. Like the lead newsletter, these
// calls have NO demo-mode fallback: an invitation that pretends to have gone, with a reply link
// that answers nothing, is worse than a feature that says it needs a server.
//
// Mirrors backend/src/ACI.Application/Services/EventInvitations.cs (JSON in camelCase).

import { authFetchJson, getApiBaseUrl, servesDemoData, type RequestOptions } from './apiClient';
import { cleanAudience, type MemberAudience, type MemberAudienceFilter } from './memberAudience';

export type LetterLanguage = 'en' | 'de' | 'fr';

export const LETTER_LANGUAGES: { code: LetterLanguage; name: string }[] = [
  { code: 'en', name: 'English' },
  { code: 'de', name: 'Deutsch' },
  { code: 'fr', name: 'Français' },
];

export interface InvitationLetter {
  subject?: string | null;
  body?: string | null;
}

/** The letter in each language; a member gets the one in their own language (English when none is set). */
export type InvitationLetters = Partial<Record<LetterLanguage, InvitationLetter>>;

export interface InvitationSetup {
  /** The default letter in each language. */
  defaults: Record<LetterLanguage, InvitationLetter>;
  /** The placeholders the letter may use: firstName, name, eventName, eventDate, eventPlace, memberPrice, replyLink, club. */
  fields: string[];
  fromName?: string | null;
  replyTo?: string | null;
  timeZone: string;
  /** Why reply links cannot be made here, when they cannot. */
  replyLinkProblem?: string | null;
  /** Why the event cannot be invited to (a draft, cancelled, over, started), when it cannot. */
  closed?: string | null;
  maxRecipients: number;
}

export interface InvitationPreview {
  language: LetterLanguage | (string & {});
  subject: string;
  body: string;
  sampleName: string;
  sampleEmail?: string | null;
  /** Placeholders nothing fills, as written. */
  unknown: string[];
  fromName?: string | null;
  replyTo?: string | null;
}

export interface InvitationTestResult {
  to: string;
  status: 'sent' | 'queued' | (string & {});
  language: string;
}

export interface InvitationSendResult {
  id: string;
  status: 'sending' | 'sent' | (string & {});
  recipients: number;
  leftOut: number;
  /** The id had been sent before: nothing more went. */
  alreadySent: boolean;
  createdAtUtc: string;
}

/** Where an invited member stands. */
export type InvitationState = 'no_answer' | 'coming' | 'waitlist' | 'declined' | 'cancelled' | 'came' | 'no_show';

export interface EventInvitation {
  id: string;
  memberId: string;
  memberName: string;
  email?: string | null;
  language: string;
  state: InvitationState | (string & {});
  invitedAtUtc: string;
  answeredAtUtc?: string | null;
  remindedAtUtc?: string | null;
  /** What the outbox says about the invitation email: queued, sent, delivered, opened, bounced, suppressed, failed, cancelled. */
  emailStatus?: string | null;
  waitlistPosition?: number | null;
}

export interface EventInvitationCounts {
  invited: number;
  coming: number;
  waitlist: number;
  declined: number;
  noAnswer: number;
  cancelled: number;
  came: number;
  noShow: number;
  reminded: number;
  /** Members a reminder would still go to: no answer, and no reminder yet. */
  canRemind: number;
}

export interface EventInvitations {
  invitations: EventInvitation[];
  counts: EventInvitationCounts;
  /** Whether members can still answer: the event is on and has not started. */
  open: boolean;
}

export interface InvitationRemindResult {
  reminded: number;
  alreadyReminded: number;
  notSent: number;
}

/** How many of those with a seat are likely to come: an estimate from each member's past show rate. */
export interface ExpectedAttendance {
  expected: number;
  seats: number;
  members: number;
  membersWithHistory: number;
  guests: number;
  checkedIn: number;
  /** The club's average show rate (0 to 1), or 0.9 while it has no history. */
  clubRate: number;
  clubHasHistory: boolean;
}

/** Thrown in demo mode, where there is no server to count, write or send anything. */
export class InvitationsNeedServerError extends Error {
  constructor() {
    super('Inviting members needs a connected server. In demo mode there is nowhere to send the invitations from.');
    this.name = 'InvitationsNeedServerError';
  }
}

function requireApi(): void {
  if (servesDemoData()) throw new InvitationsNeedServerError();
}

const base = (eventId: string) => `/api/events/${encodeURIComponent(eventId)}/invitations`;

export async function getInvitationSetup(eventId: string, options: RequestOptions = {}): Promise<InvitationSetup> {
  requireApi();
  return authFetchJson<InvitationSetup>(`${base(eventId)}/setup`, { signal: options.signal });
}

/** Who invitations with this audience would reach now, and who is left out and why. */
export async function getInvitationAudience(eventId: string, audience: MemberAudienceFilter, options: RequestOptions = {}): Promise<MemberAudience> {
  requireApi();
  return authFetchJson<MemberAudience>(`${base(eventId)}/audience`, { method: 'POST', body: JSON.stringify(cleanAudience(audience)), signal: options.signal });
}

export interface InvitationPreviewRequest {
  letters?: InvitationLetters | null;
  /** The member it is filled in for; none: a sample person. */
  sampleMemberId?: string | null;
  /** For a sample person: the language to show. */
  language?: LetterLanguage | null;
}

export async function previewInvitation(eventId: string, request: InvitationPreviewRequest, options: RequestOptions = {}): Promise<InvitationPreview> {
  requireApi();
  return authFetchJson<InvitationPreview>(`${base(eventId)}/preview`, { method: 'POST', body: JSON.stringify(request), signal: options.signal });
}

/** The invitation as the preview shows it, marked as a test, to the signed-in person alone. */
export async function sendInvitationTest(eventId: string, request: InvitationPreviewRequest): Promise<InvitationTestResult> {
  requireApi();
  return authFetchJson<InvitationTestResult>(`${base(eventId)}/test`, { method: 'POST', body: JSON.stringify(request) });
}

export interface InvitationSendRequest {
  sendId: string;
  audience: MemberAudienceFilter;
  letters?: InvitationLetters | null;
  /** How many the person confirmed it would go to: when the audience has changed since, nothing is sent. */
  expectedRecipients?: number | null;
}

/** Queues one invitation per member the audience chooses. Sent again with the same `sendId`, nothing more goes. */
export async function sendInvitations(eventId: string, request: InvitationSendRequest): Promise<InvitationSendResult> {
  requireApi();
  return authFetchJson<InvitationSendResult>(`${base(eventId)}/send`, {
    method: 'POST',
    body: JSON.stringify({ ...request, audience: cleanAudience(request.audience) }),
  });
}

export async function getEventInvitations(eventId: string, options: RequestOptions = {}): Promise<EventInvitations> {
  requireApi();
  return authFetchJson<EventInvitations>(base(eventId), { signal: options.signal });
}

/** Reminds those who have not answered: one reminder per member, however often it is pressed. */
export async function remindInvitations(eventId: string): Promise<InvitationRemindResult> {
  requireApi();
  return authFetchJson<InvitationRemindResult>(`${base(eventId)}/remind`, { method: 'POST' });
}

export async function getExpectedAttendance(eventId: string, options: RequestOptions = {}): Promise<ExpectedAttendance> {
  requireApi();
  return authFetchJson<ExpectedAttendance>(`/api/events/${encodeURIComponent(eventId)}/expected-attendance`, { signal: options.signal });
}

// ── the member's reply page (public) ─────────────────────────────────────────

/** What the reply page shows: the club, the event, and the member's own answer. Nothing about anyone else. */
export interface InvitationPage {
  club: string;
  firstName: string;
  /** The member's language (en, de, fr), which the page opens in. */
  language: string;
  /** The club's region, for its number and date formats. */
  locale?: string | null;
  /** The club's time zone, which the event's time is written in. */
  timeZone: string;
  eventName: string;
  description?: string | null;
  location?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  coverEmoji: string;
  memberPrice: number;
  currency: string;
  answer: 'none' | 'coming' | 'declined' | (string & {});
  /** With "coming": a seat, or the waitlist. */
  place?: 'seat' | 'waitlist' | (string & {}) | null;
  waitlistPosition?: number | null;
}

export class InvitationPageError extends Error {
  readonly status: number;
  readonly code: string | null;
  constructor(message: string, status: number, code: string | null) {
    super(message);
    this.name = 'InvitationPageError';
    this.status = status;
    this.code = code;
  }
}

/** The token a preview and a test carry: the page shows it as a sample. */
export const SAMPLE_TOKEN = 'sample';

async function call(token: string, init?: RequestInit): Promise<InvitationPage> {
  const apiBase = getApiBaseUrl();
  if (!apiBase) throw new InvitationPageError('VITE_API_URL is not set', 0, null);
  let res: Response;
  try {
    res = await fetch(`${apiBase}/api/public/invitations/${encodeURIComponent(token)}`, init);
  } catch {
    // Offline, DNS, CORS: no answer at all, which is not the same as a dead link.
    throw new InvitationPageError('Could not reach the server', 0, null);
  }
  if (!res.ok) {
    let code: string | null = null;
    try {
      const problem = (await res.json()) as { errorCode?: string };
      code = typeof problem.errorCode === 'string' ? problem.errorCode : null;
    } catch {
      // No problem body: the status says enough.
    }
    throw new InvitationPageError('Not available', res.status, code);
  }
  return res.json() as Promise<InvitationPage>;
}

/** The page for a reply link. Opening it changes nothing: mail scanners open every link. */
export const getInvitationPage = (token: string) => call(token);

/** The member's answer: "coming" or "declined". */
export const answerInvitation = (token: string, answer: 'coming' | 'declined') =>
  call(token, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ answer }) });
