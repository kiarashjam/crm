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
  let id = match[2];
  try {
    id = decodeURIComponent(id);
  } catch {
    id = match[2];
  }
  if (match[1] === 'leads') return { leadId: id };
  if (match[1] === 'deals') return { dealId: id };
  return { contactId: id };
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
  const contact = ids.contactId ? records?.contacts?.find((row) => row.id === ids.contactId) : undefined;
  return {
    focusKey: item.key,
    name: item.name,
    leadId: ids.leadId ?? null,
    dealId: ids.dealId ?? null,
    contactId: ids.contactId ?? deal?.contactId ?? null,
    companyId: lead?.companyId || deal?.companyId || contact?.companyId || null,
    companyName: lead?.companyName || deal?.companyName || contact?.companyName || null,
  };
}

function tokens(value: string): string[] {
  return value.toLowerCase().split(/[^a-z0-9]+/).filter((part) => part.length >= 3);
}

export type PersonMatch =
  | { status: 'one'; item: WorkSuggestion }
  | { status: 'many'; items: WorkSuggestion[] }
  | { status: 'none' };

/**
 * Two loaded people share a name in the question, and no single full name is contained.
 * Company and deal names are left to the company and search answers.
 */
export function ambiguousPeople(
  leads: { id: string; name: string }[],
  contacts: { id: string; name: string }[] | undefined,
  question: string,
): { label: string; href: string }[] | null {
  const q = question.trim().toLowerCase();
  const words = tokens(q);
  const people = [
    ...leads.map((lead) => ({ name: lead.name.trim(), href: `/leads/${lead.id}` })),
    ...(contacts ?? []).map((contact) => ({ name: contact.name.trim(), href: `/contacts/${contact.id}` })),
  ].filter((person) => person.name.length >= 3);
  const full = people.filter((person) => q.includes(person.name.toLowerCase()));
  full.sort((a, b) => b.name.length - a.name.length);
  const longest = full[0];
  const specific = longest
    ? full.filter((person) => person.href === longest.href || !longest.name.toLowerCase().includes(person.name.toLowerCase()))
    : [];
  if (specific.length === 1) return null;
  if (specific.length > 1) {
    return specific.slice(0, 5).map((person) => ({ label: person.name, href: person.href }));
  }
  const partial = people.filter((person) => tokens(person.name).some((part) => words.includes(part)));
  const unique = [...new Map(partial.map((person) => [person.href, person])).values()];
  if (unique.length < 2) return null;
  return unique.slice(0, 5).map((person) => ({ label: person.name, href: person.href }));
}

/** Match a name, an email, or the last record. Tied names stay a question. */
export function matchPeople(items: WorkSuggestion[], question: string, memory?: AgentMemory): PersonMatch {
  const q = question.trim().toLowerCase();
  const byEmail = items.filter((item) => {
    const email = item.email?.trim().toLowerCase();
    return !!email && q.includes(email);
  });
  if (byEmail.length === 1) return { status: 'one', item: byEmail[0] };
  if (byEmail.length > 1) return { status: 'many', items: byEmail };

  const full = items.filter((item) => q.includes(item.name.toLowerCase()));
  full.sort((a, b) => b.name.length - a.name.length);
  const longest = full[0];
  const specific = longest ? full.filter((item) => item === longest || !longest.name.toLowerCase().includes(item.name.toLowerCase())) : [];
  if (specific.length === 1) return { status: 'one', item: specific[0] };
  if (specific.length > 1) return { status: 'many', items: specific };

  const words = tokens(q);
  const scored = items
    .map((item) => {
      const parts = tokens(item.name);
      const score = parts.filter((part) => words.some((word) => part === word || part.startsWith(word))).length;
      return { item, score };
    })
    .filter((row) => row.score > 0)
    .sort((a, b) => b.score - a.score || b.item.name.length - a.item.name.length);
  if (scored.length === 1 || (scored.length > 1 && scored[0].score > scored[1].score)) return { status: 'one', item: scored[0].item };
  if (scored.length > 1) return { status: 'many', items: scored.map((row) => row.item) };

  const ordinal = /\b(the )?(first|1st) (one|row)\b/.test(q) ? 0 : /\b(the )?(second|2nd) (one|row)\b/.test(q) ? 1 : /\b(the )?(third|3rd) (one|row)\b/.test(q) ? 2 : -1;
  if (ordinal >= 0 && items[ordinal]) return { status: 'one', item: items[ordinal] };

  const pointsBack = /^(why|explain|and why|what about that|that one|this one|them|do that)$/.test(q)
    || /\b(that one|this one|the same one|their|theirs|that company|that deal|that contact)\b/.test(q);
  if (!pointsBack || !memory?.focusKey) return { status: 'none' };
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
