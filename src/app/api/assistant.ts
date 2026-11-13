// The assistant on the server: whether a language model is set up, the workspace's settings
// for it (its monthly limit, morning briefs), asking it a question, and the morning brief.
//
// Asking goes to POST /api/agent/chat. The model reads the records as the signed-in person
// sees them and proposes changes rather than making them: each proposal comes back for the
// person to confirm, and only that confirm sends it, from the app, to the endpoint the app's
// own page would use (with that endpoint's permission checks). Demo data has no model behind
// it: there the chat answers from its built-in rules, as it does whenever none is set up.

import { ApiError, authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

/** A month's allowance of calls to the language model. */
export interface AiAllowance {
  used: number;
  /** Calls the month allows; 0 means the AI is off for this workspace. */
  limit: number;
  /** The month, as its first day (yyyy-MM-dd). */
  month: string;
  /** The first day of next month, when the count starts again. */
  resetsOn: string;
  /** Calls refused this month because the allowance was used up. */
  refused: number;
  inputTokens: number;
  outputTokens: number;
  left: number;
}

/** Settings › Assistant. Anyone in the workspace may read it; changing it takes settings.manage. */
export interface AssistantSettings {
  /** Whether a language model is set up on the server. */
  configured: boolean;
  provider: string;
  model: string;
  /** The limit that applies: the workspace's own, else the server's default. */
  monthlyLimit: number;
  /** The workspace's own limit; null when it uses the server's default. */
  customLimit: number | null;
  defaultLimit: number;
  maxLimit: number;
  /** Whether the workspace's people get a morning brief. */
  morningBriefs: boolean;
  usage: AiAllowance;
  version?: string | null;
}

export interface AssistantSettingsUpdate {
  monthlyLimit?: number;
  useDefaultLimit?: boolean;
  morningBriefs?: boolean;
  version?: string | null;
}

/** What demo data shows: no model, so the chat answers from its rules. */
const DEMO_SETTINGS: AssistantSettings = {
  configured: false,
  provider: 'OpenAI',
  model: '',
  monthlyLimit: 0,
  customLimit: null,
  defaultLimit: 0,
  maxLimit: 0,
  morningBriefs: true,
  usage: { used: 0, limit: 0, month: '', resetsOn: '', refused: 0, inputTokens: 0, outputTokens: 0, left: 0 },
  version: null,
};

export async function getAssistantSettings(options: RequestOptions = {}): Promise<AssistantSettings> {
  if (servesDemoData()) return DEMO_SETTINGS;
  return authFetchJson<AssistantSettings>('/api/agent/settings', { signal: options.signal });
}

/** Saves a change; a stale version is refused with 409 (someone else changed the settings). */
export async function saveAssistantSettings(update: AssistantSettingsUpdate): Promise<AssistantSettings> {
  if (servesDemoData()) throw new Error('The assistant’s settings are kept by the live app; demo data has none.');
  const headers: Record<string, string> = {};
  if (update.version) headers['If-Match'] = `"${update.version}"`;
  return authFetchJson<AssistantSettings>('/api/agent/settings', { method: 'PUT', body: JSON.stringify(update), headers });
}

// ── the chat ────────────────────────────────────────────────────────────────

export type ProposalKind = 'task' | 'activity' | 'deal-stage' | 'email-draft' | 'booking' | 'charge';

/**
 * A change the assistant proposes. Nothing is written when it arrives: the chat shows it as a
 * card, and only Confirm sends `payload` to the endpoint the app's own form would use.
 */
export interface AssistantProposal {
  /** Sent once, however often Confirm is pressed; also the Idempotency-Key of a charge. */
  id: string;
  kind: ProposalKind;
  title: string;
  /** The facts the card shows, read from the records, one per line. */
  details: string[];
  payload: Record<string, unknown>;
  /** The record it is about, to open. */
  link?: string | null;
}

export interface AssistantTurn {
  role: 'user' | 'assistant';
  text: string;
}

export interface AssistantQuestion {
  message: string;
  /** Earlier turns of this conversation, oldest first. */
  history?: AssistantTurn[];
  /** The page the person is on, so "this lead" means something. */
  page?: string;
  language?: string;
}

export interface AssistantReply {
  reply: string;
  proposals: AssistantProposal[];
  /** The lookups it made, in order, so the person can see what it read. */
  lookedUp: string[];
  usage: AiAllowance;
  /** It stopped before it finished (the month's allowance ran out, or it used every step it may take). */
  incomplete: boolean;
}

/**
 * Asks the assistant. A refusal comes back as an ApiError the chat reads: 503 (no model set
 * up), 502 (the model did not answer properly) and 429 (asked too fast, the team is asking
 * too much at once, or the month's allowance is used up) are answered from the built-in rules.
 */
export async function askAssistant(question: AssistantQuestion, options: RequestOptions = {}): Promise<AssistantReply> {
  if (servesDemoData()) throw new ApiError('No language model is set up for demo data.', 503, '/api/agent/chat', true, 'Assistant.NotConfigured');
  const reply = await authFetchJson<AssistantReply>('/api/agent/chat', { method: 'POST', body: JSON.stringify(question), signal: options.signal });
  return {
    reply: reply?.reply ?? '',
    proposals: Array.isArray(reply?.proposals) ? reply.proposals : [],
    lookedUp: Array.isArray(reply?.lookedUp) ? reply.lookedUp : [],
    usage: reply?.usage ?? DEMO_SETTINGS.usage,
    incomplete: reply?.incomplete === true,
  };
}

// ── the morning brief ───────────────────────────────────────────────────────

export interface MorningBriefLine {
  text: string;
  /** The page to open for it. */
  link?: string | null;
}

export interface MorningBriefSection {
  /** tasks, stale_deals, owing, arrivals, events or renewals. */
  key: string;
  title: string;
  /** How many there are in all; `lines` holds the first few. */
  count: number;
  lines: MorningBriefLine[];
}

export interface MorningBrief {
  /** The day on the workspace's calendar, yyyy-MM-dd. */
  day: string;
  timeZone: string;
  workspaceName: string;
  headline: string;
  sections: MorningBriefSection[];
  items: number;
}

export interface SentMorningBrief {
  sentAtUtc: string;
  /** Its words as they went out. */
  text: string;
  writtenByModel: boolean;
  emailed: boolean;
}

export interface MorningBriefView {
  /** The day worked out now. */
  now: MorningBrief;
  /** The brief that went out this morning; null when none did. */
  thisMorning: SentMorningBrief | null;
}

/** The person's brief: built on the server from the records as they may see them. Demo data has none. */
export async function getMorningBrief(options: RequestOptions = {}): Promise<MorningBriefView | null> {
  if (servesDemoData()) return null;
  return authFetchJson<MorningBriefView>('/api/agent/brief', { signal: options.signal });
}
