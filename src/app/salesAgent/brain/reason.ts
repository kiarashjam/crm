import type { SuggestableLead } from '../nextAction';
import type { SuggestableContact, SuggestableDeal, WorkSuggestion } from '../workspace';

/** The record the previous answer was about. Follow-ups use the ids, then read current records. */
export interface AgentMemory {
  focusKey: string | null;
  name: string | null;
  leadId: string | null;
  dealId: string | null;
  contactId: string | null;
  companyId: string | null;
  companyName: string | null;
}

export function emptyMemory(): AgentMemory {
  return {
    focusKey: null,
    name: null,
    leadId: null,
    dealId: null,
    contactId: null,
    companyId: null,
    companyName: null,
  };
}

function idsFromHref(href: string): { leadId?: string; dealId?: string; contactId?: string } {
  const match = /^\/(leads|deals|contacts)\/([^/]+)$/.exec(href);
  if (!match) return {};
  const raw = match[2] ?? '';
  let id = raw;
  try {
    id = decodeURIComponent(raw);
  } catch {
    id = raw;
  }
  if (match[1] === 'leads') return { leadId: id };
  if (match[1] === 'deals') return { dealId: id };
  return { contactId: id };
}

interface Loaded {
  items: WorkSuggestion[];
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  contacts?: SuggestableContact[];
}

/**
 * The row for a record key ("lead:…", "deal:…", "contact:…"), read from the loaded lists.
 * A record with no open step still has a row, with no step on it. Null when it is not loaded.
 */
export function rowFor(world: Loaded, key: string | null | undefined): WorkSuggestion | null {
  if (!key) return null;
  const listed = world.items.find((item) => item.key === key);
  if (listed) return listed;
  const at = key.indexOf(':');
  const kind = key.slice(0, at);
  const id = key.slice(at + 1);
  if (at < 1 || !id) return null;
  const blank = { action: '', because: '', priority: 0 };
  if (kind === 'lead') {
    const lead = world.leads.find((row) => row.id === id);
    return lead ? { ...blank, key, href: `/leads/${id}`, name: lead.name, list: 'Leads', email: lead.email } : null;
  }
  if (kind === 'deal') {
    const deal = world.deals.find((row) => row.id === id);
    return deal ? { ...blank, key, href: `/deals/${id}`, name: deal.name, list: 'Deals' } : null;
  }
  if (kind === 'contact') {
    const contact = (world.contacts ?? []).find((row) => row.id === id);
    return contact ? { ...blank, key, href: `/contacts/${id}`, name: contact.name, list: 'Contacts', email: contact.email } : null;
  }
  return null;
}

/**
 * The record the last answer was about, read again by its saved ids. Null when it is
 * no longer loaded: a follow-up never falls through to some other row.
 */
export function recall(world: Loaded, memory: AgentMemory | undefined): WorkSuggestion | null {
  if (!memory) return null;
  const row = rowFor(world, memory.focusKey);
  if (row || /^(lead|deal|contact):/.test(memory.focusKey ?? '')) return row;
  // A step that is not itself a record, such as a sequence step, names its record by id.
  return rowFor(world, memory.leadId ? `lead:${memory.leadId}` : null)
    ?? rowFor(world, memory.dealId ? `deal:${memory.dealId}` : null)
    ?? rowFor(world, memory.contactId ? `contact:${memory.contactId}` : null);
}

/** Keep the company and record ids for the focused row. A clarifying question keeps the previous context. */
export function remember(
  item: WorkSuggestion | null,
  previous: AgentMemory | undefined,
  records?: { leads?: SuggestableLead[]; deals?: SuggestableDeal[]; contacts?: SuggestableContact[] },
): AgentMemory {
  if (!item) return previous?.focusKey ? { ...emptyMemory(), ...previous } : (previous ?? emptyMemory());
  const ids = idsFromHref(item.href);
  const lead = ids.leadId ? records?.leads?.find((row) => row.id === ids.leadId) : undefined;
  const deal = ids.dealId ? records?.deals?.find((row) => row.id === ids.dealId) : undefined;
  // A lead and the contact made from it are one person: the shared email links them.
  const email = lead?.email?.trim().toLowerCase();
  const sameContact = email ? records?.contacts?.find((row) => row.email?.trim().toLowerCase() === email) : undefined;
  const contact = ids.contactId ? records?.contacts?.find((row) => row.id === ids.contactId) : sameContact;
  return {
    focusKey: item.key,
    name: item.name,
    leadId: ids.leadId ?? null,
    dealId: ids.dealId ?? null,
    contactId: ids.contactId ?? deal?.contactId ?? sameContact?.id ?? null,
    companyId: lead?.companyId || deal?.companyId || contact?.companyId || null,
    companyName: lead?.companyName || deal?.companyName || contact?.companyName || null,
  };
}

/** Words in a question or a name. Letters in any script, so "José" stays one word. */
function words(value: string): string[] {
  return value.toLowerCase().split(/[^\p{L}\p{N}]+/u).filter(Boolean);
}

/**
 * Words that are never read as part of a name. Without them "the" picked Theodore,
 * "can" picked Candice, and "draft that" matched anyone whose name starts with "dra".
 */
const NOT_A_NAME = new Set([
  'about', 'activity', 'activities', 'add', 'after', 'again', 'all', 'also', 'amount', 'and', 'any', 'are', 'assign',
  'before', 'biggest', 'book', 'brief', 'briefing', 'call', 'calls', 'can', 'change', 'close', 'comment', 'companies',
  'company', 'compare', 'contact', 'contacts', 'convert', 'could', 'create', 'date', 'day', 'days', 'deal', 'deals',
  'did', 'does', 'done', 'draft', 'else', 'email', 'emails', 'enroll', 'explain', 'find', 'first', 'follow', 'for',
  'from', 'get', 'give', 'had', 'has', 'have', 'her', 'here', 'him', 'his', 'how', 'into', 'its', 'just', 'largest',
  'last', 'late', 'lead', 'leads', 'let', 'like', 'list', 'log', 'look', 'mail', 'make', 'many', 'meeting', 'merge',
  'message', 'money', 'more', 'most', 'move', 'much', 'need', 'new', 'next', 'not', 'note', 'notes', 'now', 'one',
  'only', 'open', 'order', 'other', 'our', 'over', 'overdue', 'people', 'person', 'phone', 'pipeline', 'plan',
  'please', 'priority', 'rank', 'recent', 'record', 'records', 'remind', 'risk', 'same', 'schedule', 'search',
  'second', 'see', 'send', 'set', 'should', 'show', 'some', 'stage', 'status', 'step', 'steps', 'still', 'summary',
  'take', 'task', 'tasks', 'tell', 'than', 'thanks', 'that', 'the', 'their', 'theirs', 'them', 'then', 'there',
  'these', 'they', 'third', 'this', 'those', 'three', 'today', 'tomorrow', 'top', 'two', 'update', 'value', 'very',
  'want', 'was', 'week', 'were', 'what', 'whats', 'when', 'where', 'which', 'who', 'why', 'will', 'with', 'worth',
  'would', 'write', 'yet', 'you', 'your',
]);

/** Question words that could be part of a name. */
function askedWords(question: string): string[] {
  return words(question).filter((word) => word.length >= 3 && !NOT_A_NAME.has(word));
}

/** A part of a name matches a word in full, or a word of four letters or more that starts it. */
function partMatches(part: string, word: string): boolean {
  return part === word || (word.length >= 4 && part.startsWith(word));
}

/** The whole name appears in the question as words, not inside another word ("Ann" is not in "planning"). */
export function namesIn(question: string, name: string): boolean {
  const wanted = words(name).join(' ');
  if (wanted.length < 3) return false;
  return ` ${words(question).join(' ')} `.includes(` ${wanted} `);
}

const EMAILS = /[\p{L}\p{N}._%+-]+@[\p{L}\p{N}.-]+\.[\p{L}]{2,}/gu;

function emailsIn(question: string): Set<string> {
  return new Set((question.match(EMAILS) ?? []).map((email) => email.toLowerCase()));
}

/** The question points at the record the last answer was about. */
export function pointsBack(question: string): boolean {
  const q = question.trim().toLowerCase().replace(/[?.!]+$/g, '');
  return /^(why|explain|and why|what about that|that one|this one|them|do that)$/.test(q)
    || /\b(that one|this one|the same one|their|theirs|that company|that deal|that contact|that lead|that record)\b/.test(q)
    || /\b(draft|send|write|email|book|schedule|assign|convert|enroll|brief|move|update|call) (that|this|it|them|him|her)\b/.test(q);
}

export type PersonMatch =
  | { status: 'one'; item: WorkSuggestion }
  | { status: 'many'; items: WorkSuggestion[] }
  | { status: 'none' };

interface Person {
  name: string;
  href: string;
  /** Who the record is, across lists: a lead and the contact made from it are one person. */
  identity: string;
}

/**
 * Two loaded people share a name in the question, and no single full name is contained.
 * Company and deal names are left to the company and search answers.
 */
export function ambiguousPeople(
  leads: { id: string; name: string; email?: string | null }[],
  contacts: { id: string; name: string; email?: string | null }[] | undefined,
  question: string,
): { label: string; href: string }[] | null {
  const identity = (email: string | null | undefined, href: string) =>
    email?.trim() ? `email:${email.trim().toLowerCase()}` : href;
  const all: Person[] = [
    ...(contacts ?? []).map((contact) => ({ name: contact.name.trim(), href: `/contacts/${contact.id}`, identity: identity(contact.email, `/contacts/${contact.id}`) })),
    ...leads.map((lead) => ({ name: lead.name.trim(), href: `/leads/${lead.id}`, identity: identity(lead.email, `/leads/${lead.id}`) })),
  ].filter((person) => person.name.length >= 3);
  // One row per person. The contact is listed first, so it is the one kept.
  const people = [...new Map(all.map((person) => [person.identity, person])).values()];
  const full = people.filter((person) => namesIn(question, person.name));
  full.sort((a, b) => b.name.length - a.name.length);
  const longest = full[0];
  const specific = longest
    ? full.filter((person) => person.href === longest.href || !namesIn(longest.name, person.name))
    : [];
  if (specific.length === 1) return null;
  if (specific.length > 1) {
    return specific.slice(0, 5).map((person) => ({ label: person.name, href: person.href }));
  }
  const asked = askedWords(question);
  const partial = people.filter((person) => words(person.name).some((part) => asked.some((word) => partMatches(part, word))));
  if (partial.length < 2) return null;
  return partial.slice(0, 5).map((person) => ({ label: person.name, href: person.href }));
}

/** Match a name, an email, or the last record. Tied names stay a question. */
export function matchPeople(items: WorkSuggestion[], question: string, memory?: AgentMemory): PersonMatch {
  const q = question.trim().toLowerCase();
  const asked = emailsIn(q);
  const byEmail = items.filter((item) => {
    const email = item.email?.trim().toLowerCase();
    return !!email && asked.has(email);
  });
  const [onlyByEmail] = byEmail;
  if (byEmail.length === 1 && onlyByEmail) return { status: 'one', item: onlyByEmail };
  if (byEmail.length > 1) return { status: 'many', items: byEmail };

  const full = items.filter((item) => namesIn(q, item.name));
  full.sort((a, b) => b.name.length - a.name.length);
  const longest = full[0];
  const specific = longest ? full.filter((item) => item === longest || !namesIn(longest.name, item.name)) : [];
  const [onlySpecific] = specific;
  if (specific.length === 1 && onlySpecific) return { status: 'one', item: onlySpecific };
  if (specific.length > 1) return { status: 'many', items: specific };

  const wanted = askedWords(q);
  const scored = items
    .map((item) => {
      const parts = words(item.name);
      const score = parts.filter((part) => wanted.some((word) => partMatches(part, word))).length;
      return { item, score };
    })
    .filter((row) => row.score > 0)
    .sort((a, b) => b.score - a.score || b.item.name.length - a.item.name.length);
  const [topScore, runnerUp] = scored;
  if (topScore && (!runnerUp || topScore.score > runnerUp.score)) return { status: 'one', item: topScore.item };
  if (scored.length > 1) return { status: 'many', items: scored.map((row) => row.item) };

  const ordinal = /\b(the )?(first|1st) (one|row)\b/.test(q) ? 0 : /\b(the )?(second|2nd) (one|row)\b/.test(q) ? 1 : /\b(the )?(third|3rd) (one|row)\b/.test(q) ? 2 : -1;
  if (ordinal >= 0 && items[ordinal]) return { status: 'one', item: items[ordinal] };

  if (!pointsBack(q) || !memory?.focusKey) return { status: 'none' };
  const focused = items.find((item) => item.key === memory.focusKey);
  return focused ? { status: 'one', item: focused } : { status: 'none' };
}

/** Match a full name, a single name part, or "that one" from the last answer. Tied names are not guessed. */
export function resolvePerson(
  items: WorkSuggestion[],
  question: string,
  memory?: AgentMemory,
): WorkSuggestion | null {
  const match = matchPeople(items, question, memory);
  return match.status === 'one' ? match.item : null;
}

/** Why the first row outranks the second, using the priorities already on the list. */
export function whyThisOrder(items: WorkSuggestion[]): string {
  const first = items[0];
  const second = items[1];
  if (!first) return 'Nothing is waiting, so there is no order to explain.';
  if (!second) return `${first.name} is the only open step. ${first.because}`;
  const gap = first.priority - second.priority;
  const comparison = gap > 0
    ? `${second.name} is next, ${gap} lower, with: ${second.action}`
    : gap < 0
      ? `${second.name} has a higher saved priority (${second.priority}). ${first.name} is ahead on this list because the extra weights outrank that number.`
      : `${second.name} has the same priority. ${first.name} stays first by name order.`;
  return [
    `${first.name} is first because the saved priority is ${first.priority}.`,
    first.action,
    first.because,
    comparison,
    'A missing reason, a meeting today, and an overdue date sit above ordinary stage work. A date that is only soon raises a step. It does not replace it.',
  ].join('\n');
}

/** What gets worse if the first step is left for later. Only facts already on the row. */
export function costOfWaiting(items: WorkSuggestion[]): string {
  const first = items[0];
  if (!first) return 'Nothing is waiting.';
  const second = items[1];
  const lines = [
    `Leaving ${first.name} does not clear it. The step stays: ${first.action}`,
    first.because,
  ];
  if (/passed|overdue|missing|no reason|today/i.test(`${first.action} ${first.because}`)) {
    lines.push('That row is already late or incomplete, so waiting keeps a known hole on the record.');
  }
  if (second) lines.push(`${second.name} does not move up until ${first.name} is cleared or no longer owed.`);
  return lines.join('\n');
}
