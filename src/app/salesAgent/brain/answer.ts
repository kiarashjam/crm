import { currencyOf, formatMoney, tryParseAmount } from '@/app/lib/money';
import { shiftKey, todayKey } from '../calendarDay';
import type { SuggestableLead } from '../nextAction';
import type { SuggestableContact, SuggestableDeal, SuggestableTask, WorkSuggestion } from '../workspace';
import { itemOnList, type PageScope } from '../pageScope';
import { LEAD_PLAYBOOKS, playbookForKind, playbookForStage, type Playbook } from './playbooks';
import type { LearnedPatterns } from './patterns';
import type { WriterBrief } from '../writerBrief';
import { judgmentText } from './judge';
import { ambiguousPeople, costOfWaiting, emptyMemory, matchPeople, namesIn, pointsBack, recall, remember, whyThisOrder, type AgentMemory } from './reason';
import { activityReport, amountText, duplicateReview, groundedDraft, isOpenDeal, neglectedReport, pipelineTotals, proposeContact, proposeDeal, proposeDealUpdate, recordBrief } from './workflows';

export interface AgentWorld {
  items: WorkSuggestion[];
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  page: PageScope;
  patterns: LearnedPatterns;
  /** Sequences already saved, so an enroll button can name one. */
  sequences?: { id: string; name: string }[];
  /** Workspace setting used when a meeting is saved as a dated task. */
  followUpDays?: number;
  /** Contacts loaded with the queue, used to keep a company in view. */
  contacts?: SuggestableContact[];
  companies?: { id: string; name: string }[];
  /** Lists that failed or were capped. Facts must not be treated as complete when this is set. */
  gaps?: string[];
  tasks?: SuggestableTask[];
  /** Left out when activities are not loaded, so no answer reads "none" into a list it never saw. */
  activities?: AgentActivity[];
}

export interface AgentActivity {
  id: string;
  type: string;
  subject?: string | null;
  leadId?: string | null;
  dealId?: string | null;
  contactId?: string | null;
  name: string;
  createdAt: string;
}

export interface AgentAction {
  id: string;
  label: string;
  kind: 'task' | 'note' | 'draft' | 'open' | 'comment' | 'send' | 'assign' | 'convert' | 'enroll' | 'merge' | 'create-contact' | 'create-deal' | 'update-deal';
  href?: string;
  title?: string;
  body?: string;
  leadId?: string;
  dealId?: string;
  contactId?: string;
  taskId?: string;
  sequenceId?: string;
  to?: string;
  dueDateUtc?: string;
  mergeKind?: 'contacts' | 'companies';
  mergePrimaryId?: string;
  mergeDuplicateIds?: string[];
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  companyId?: string;
  dealValue?: string;
  currency?: string;
  brief?: WriterBrief;
  recipientName?: string;
  recipientType?: 'lead' | 'contact' | 'deal';
}

export interface AgentReply {
  text: string;
  actions: AgentAction[];
  /** The record this answer was about, so the next question can say "that one". */
  focusKey: string | null;
  /** Set when the answer was about something the key alone cannot name, such as a company. */
  memory?: AgentMemory;
}

function clean(value: string): string {
  return value.trim().toLowerCase().replace(/[?.!]+$/g, '');
}

function idsFromHref(href: string): { leadId?: string; dealId?: string; contactId?: string } {
  const match = /^\/(leads|deals|contacts)\/([^/]+)$/.exec(href);
  if (!match) return {};
  const id = decodeURIComponent(match[2] ?? '');
  if (match[1] === 'leads') return { leadId: id };
  if (match[1] === 'deals') return { dealId: id };
  return { contactId: id };
}

function party(world: AgentWorld, person: WorkSuggestion | null, memory?: AgentMemory): AgentMemory {
  if (person) return remember(person, memory, { leads: world.leads, deals: world.deals, contacts: world.contacts });
  return memory ? { ...emptyMemory(), ...memory } : emptyMemory();
}

function sameParty(deal: SuggestableDeal, ctx: AgentMemory): boolean {
  if (ctx.companyId && deal.companyId && deal.companyId === ctx.companyId) return true;
  if (ctx.companyName && deal.companyName && deal.companyName.toLowerCase() === ctx.companyName.toLowerCase()) return true;
  if (ctx.contactId && deal.contactId && deal.contactId === ctx.contactId) return true;
  if (ctx.dealId && deal.id === ctx.dealId) return true;
  return false;
}

function limitLine(world: AgentWorld): string {
  return world.gaps?.length ? `Not loaded: ${world.gaps.join(' ')}` : '';
}

/** A list that did not load at all, as opposed to an earlier copy shown after a failed refresh. */
function notLoaded(world: AgentWorld, label: string): string | null {
  return world.gaps?.find((gap) => gap.startsWith(`${label} could not be loaded`)) ?? null;
}

function otherDeals(world: AgentWorld, person: WorkSuggestion | null, memory?: AgentMemory): AgentReply {
  if (notLoaded(world, 'Deals')) {
    return say('Deals could not be loaded, so I cannot list opportunities.');
  }
  const ctx = party(world, person, memory);
  if (!ctx.companyId && !ctx.companyName && !ctx.contactId && !ctx.dealId && !ctx.leadId) {
    return say('Name the company, contact, or deal. I do not have a current record to attach this question to.');
  }
  const related = world.deals.filter((deal) => sameParty(deal, ctx));
  const others = related.filter((deal) => !ctx.dealId || deal.id !== ctx.dealId);
  const label = ctx.companyName || ctx.name || 'that record';
  const open = (deal: SuggestableDeal): AgentAction => ({ id: `open:deal:${deal.id}`, label: `Open ${deal.name}`, kind: 'open', href: `/deals/${deal.id}` });
  if (others.length === 0) {
    const none = `Recorded: no other deal is loaded for ${label}.`;
    const gap = limitLine(world);
    return say(gap ? `${none}\n${gap}` : none, person ? [openFor(person)] : [], person);
  }
  const shown = others.slice(0, 8);
  const lines = [
    `Recorded deals for ${label}. This list is not a suggestion.`,
    ...shown.map((deal) => `${deal.name}: ${amountText(deal)} · ${deal.dealStageName || deal.stage || 'no stage'}`),
  ];
  if (others.length > shown.length) lines.push(`${others.length - shown.length} more loaded deals are not listed.`);
  const gap = limitLine(world);
  if (gap) lines.push(gap);
  return say(lines.join('\n'), shown.slice(0, 5).map(open), person);
}

function otherPeople(world: AgentWorld, person: WorkSuggestion | null, memory?: AgentMemory): AgentReply {
  const ctx = party(world, person, memory);
  if (!ctx.companyId && !ctx.companyName) {
    return say('Name the company. I do not have a company on the current record.');
  }
  const people = (world.contacts ?? []).filter((contact) => {
    if (contact.isArchived) return false;
    if (ctx.companyId && contact.companyId === ctx.companyId) return contact.id !== ctx.contactId;
    return !!ctx.companyName && contact.companyName?.toLowerCase() === ctx.companyName.toLowerCase() && contact.id !== ctx.contactId;
  });
  const label = ctx.companyName || 'that company';
  if (people.length === 0) {
    return say(`Recorded: no other contact is loaded at ${label}.`, person ? [openFor(person)] : [], person);
  }
  const shown = people.slice(0, 8);
  return say([
    `Recorded contacts at ${label}. This list is not a suggestion.`,
    ...shown.map((contact) => `${contact.name}${contact.email ? ` · ${contact.email}` : ''}`),
  ].join('\n'), shown.slice(0, 5).map((contact) => ({
    id: `open:contact:${contact.id}`,
    label: `Open ${contact.name}`,
    kind: 'open' as const,
    href: `/contacts/${contact.id}`,
  })), person);
}

/** Matches whose name is inside a longer match's name are dropped: "Pavilion renewal" over "Pavilion". */
function longestOnly<T extends { name: string }>(rows: T[]): T[] {
  return rows.filter((row) => !rows.some((other) => other !== row && other.name.length > row.name.length && namesIn(other.name, row.name)));
}

function briefByName(world: AgentWorld, q: string, now: Date): AgentReply | null {
  const blank = emptyMemory();
  type Hit = { kind: 'lead' | 'contact' | 'deal'; id: string; name: string; email?: string | null; companyId?: string | null; companyName?: string | null; contactId?: string | null };
  const hits: Hit[] = longestOnly([
    ...(world.contacts ?? []).map((row): Hit => ({ kind: 'contact', ...row })),
    ...world.leads.map((row): Hit => ({ kind: 'lead', ...row })),
    ...world.deals.map((row): Hit => ({ kind: 'deal', id: row.id, name: row.name, companyId: row.companyId, companyName: row.companyName, contactId: row.contactId })),
  ].filter((row) => namesIn(q, row.name)));
  // A lead and the contact made from it share an email. They are one person, briefed together.
  const people = new Map<string, Hit[]>();
  for (const hit of hits) {
    const identity = hit.kind !== 'deal' && hit.email?.trim() ? `email:${hit.email.trim().toLowerCase()}` : `${hit.kind}:${hit.id}`;
    people.set(identity, [...(people.get(identity) ?? []), hit]);
  }
  if (people.size > 1) {
    return say([
      `${people.size} records match. I will not guess.`,
      ...[...people.values()].slice(0, 5).map((group, index) => `${index + 1}. ${group[0]?.name ?? ''}`),
      'Which one?',
    ].join('\n'));
  }
  const [group] = [...people.values()];
  if (group) {
    const lead = group.find((hit) => hit.kind === 'lead');
    const contact = group.find((hit) => hit.kind === 'contact');
    const deal = group.find((hit) => hit.kind === 'deal');
    const keys = [lead && `lead:${lead.id}`, contact && `contact:${contact.id}`, deal && `deal:${deal.id}`].filter((key): key is string => !!key);
    const focusKey = keys.find((key) => world.items.some((item) => item.key === key)) ?? keys[0] ?? null;
    const first = lead ?? contact ?? deal;
    return recordBrief(world, null, {
      ...blank,
      focusKey,
      name: first?.name ?? null,
      leadId: lead?.id ?? null,
      contactId: contact?.id ?? deal?.contactId ?? null,
      dealId: deal?.id ?? null,
      companyId: first?.companyId ?? null,
      companyName: first?.companyName ?? null,
    }, now);
  }
  const companyNames = new Set<string>();
  for (const row of [...world.leads, ...world.deals, ...(world.contacts ?? [])]) {
    if (row.companyName && namesIn(q, row.companyName)) companyNames.add(row.companyName);
  }
  for (const company of world.companies ?? []) {
    if (namesIn(q, company.name)) companyNames.add(company.name);
  }
  if (companyNames.size > 1) {
    return say(`${companyNames.size} companies match. I will not guess.\n${[...companyNames].slice(0, 5).join('\n')}\nWhich one?`);
  }
  const companyName = companyNames.size === 1 ? [...companyNames][0] : undefined;
  if (companyName) {
    const company = (world.companies ?? []).find((row) => row.name === companyName);
    return recordBrief(world, null, { ...blank, focusKey: null, name: companyName, leadId: null, dealId: null, contactId: null, companyId: company?.id ?? null, companyName }, now);
  }
  return null;
}

function searchLoaded(world: AgentWorld, question: string): AgentReply {
  const query = question.replace(/^.*?\b(find|search for|look up)\b/i, '').trim();
  if (query.length < 2) return say('Name at least two letters to search the records that loaded.');
  const needle = query.toLowerCase();
  const hit = (value?: string | null) => !!value && value.toLowerCase().includes(needle);
  const leadHits = world.leads.filter((lead) => hit(lead.name) || hit(lead.email) || hit(lead.companyName) || hit(lead.phone) || hit(lead.source));
  const dealHits = world.deals.filter((deal) => hit(deal.name) || hit(deal.companyName) || hit(deal.contactName) || hit(deal.stage) || hit(deal.dealStageName));
  const contactHits = (world.contacts ?? []).filter((contact) => hit(contact.name) || hit(contact.email) || hit(contact.companyName) || hit(contact.phone));
  const leads = leadHits.slice(0, 5);
  const deals = dealHits.slice(0, 5);
  const contacts = contactHits.slice(0, 5);
  if (leads.length + deals.length + contacts.length === 0) {
    const gap = limitLine(world);
    return say(gap ? `No loaded lead, deal, or contact matches "${query}".\n${gap}` : `No loaded lead, deal, or contact matches "${query}".`);
  }
  const lines = [`Recorded matches for "${query}". This is not a suggestion.`];
  for (const lead of leads) lines.push(`Lead: ${lead.name}${lead.companyName ? ` · ${lead.companyName}` : ''}`);
  for (const deal of deals) lines.push(`Deal: ${deal.name}${deal.companyName ? ` · ${deal.companyName}` : ''}`);
  for (const contact of contacts) lines.push(`Contact: ${contact.name}${contact.companyName ? ` · ${contact.companyName}` : ''}`);
  const hidden = leadHits.length - leads.length + dealHits.length - deals.length + contactHits.length - contacts.length;
  if (hidden > 0) lines.push(`${hidden} more matches are loaded and not listed.`);
  const gap = limitLine(world);
  if (gap) lines.push(gap);
  const first = leads[0] ? { href: `/leads/${leads[0].id}`, name: leads[0].name } : deals[0] ? { href: `/deals/${deals[0].id}`, name: deals[0].name } : contacts[0] ? { href: `/contacts/${contacts[0].id}`, name: contacts[0].name } : null;
  return say(lines.join('\n'), first ? [{ id: `open:${first.href}`, label: `Open ${first.name}`, kind: 'open', href: first.href }] : []);
}

function say(text: string, actions: AgentAction[] = [], focus: WorkSuggestion | null = null): AgentReply {
  return { text, actions, focusKey: focus?.key ?? null };
}

const REACH = /call|phone|email|reach|note|proposal|signature|follow up|follow-up|contract/i;
const LEAD_HREF = /^\/leads\/([^/]+)$/;

function emptyQueue(world: AgentWorld): string {
  return world.page.mode === 'list' ? 'Nothing on this page is waiting.' : 'Nothing is waiting.';
}

/** Rows on the open list. A record page and the dashboard keep the whole queue. */
function listQueue(world: AgentWorld): WorkSuggestion[] {
  const page = world.page;
  if (page.mode !== 'list') return world.items;
  return world.items.filter((item) => itemOnList(item, page.list));
}

/** The open record when it has a step, otherwise the list the person is looking at. */
function nextQueue(world: AgentWorld): WorkSuggestion[] {
  const page = world.page;
  if (page.mode === 'record') {
    const here = world.items.filter((item) => item.href === page.href);
    if (here.length > 0) return here;
  }
  return listQueue(world);
}

/** The record on screen, when the panel sits on a record page. */
function pageRow(world: AgentWorld): WorkSuggestion | null {
  const page = world.page;
  return page.mode === 'record' ? world.items.find((item) => item.href === page.href) ?? null : null;
}

/**
 * Who a follow-up such as "draft a message" is about: the record named, else the one on
 * screen, else the one the last answer was about ("that one" asks for that first). Never
 * the top of the queue: a write proposed on a record nobody named is a guess.
 */
function target(world: AgentWorld, person: WorkSuggestion | null, memory: AgentMemory | undefined, question: string): WorkSuggestion | null {
  if (person) return person;
  const here = pageRow(world);
  const before = recall(world, memory);
  return pointsBack(question) ? before ?? here : here ?? before;
}

function leadIdFrom(href: string): string | null {
  const id = LEAD_HREF.exec(href)?.[1];
  if (!id) return null;
  try {
    return decodeURIComponent(id);
  } catch {
    return id;
  }
}

/** How to reach a lead, from the phone and email already saved. Null when the step is not a reach. */
export function reachSentence(item: WorkSuggestion, leads: SuggestableLead[]): string | null {
  const id = leadIdFrom(item.href);
  if (!id) return null;
  const lead = leads.find((entry) => entry.id === id);
  if (!lead) return null;
  const reaching = REACH.test(item.action) || item.guide === 'start_outreach' || item.guide === 'try_again' || item.brief != null;
  if (!reaching) return null;
  const phone = lead.phone?.trim() ?? '';
  const email = lead.email?.trim() ?? '';
  if (phone && email) return `Phone ${phone} and email ${email} are both saved.`;
  if (phone) return `Phone ${phone} is saved. No email is saved.`;
  if (email) return `Email ${email} is saved. No phone is saved.`;
  return 'No phone and no email are saved.';
}

function callSlate(world: AgentWorld, items: WorkSuggestion[]): AgentReply {
  const leadById = new Map(world.leads.map((lead) => [lead.id, lead]));
  const shown = items
    .filter((item) => /\b(call|phone)\b/i.test(item.action) || item.guide === 'start_outreach' || item.guide === 'try_again' || item.guide === 'hold_meeting')
    .slice(0, 5)
    .map((item) => {
      const lead = leadById.get(leadIdFrom(item.href) ?? '');
      return { item, phone: lead?.phone?.trim() ?? '' };
    });
  const [firstCall] = shown;
  if (!firstCall) {
    const where = world.page.mode === 'list' ? 'on this page' : 'in the loaded queue';
    return say(`No step ${where} is a call.`);
  }
  const lines = shown.map(({ item, phone }) => `${item.name}: ${item.action}${phone ? ` Phone ${phone}.` : ''}`);
  return say(lines.join('\n'), [openFor(firstCall.item)], firstCall.item);
}

function taskFor(item: WorkSuggestion, title?: string, due?: string): AgentAction {
  const text = title || item.action.replace(/\.$/, '') || `Follow up with ${item.name}`;
  return {
    id: `task:${item.key}:${text.toLowerCase()}:${due ?? ''}`,
    label: `Create task for ${item.name}`,
    kind: 'task',
    title: text,
    dueDateUtc: due,
    href: item.href,
    ...idsFromHref(item.href),
  };
}

function openFor(item: WorkSuggestion): AgentAction {
  return { id: `open:${item.key}`, label: `Open ${item.name}`, kind: 'open', href: item.href };
}

function bookFor(item: WorkSuggestion): Playbook | null {
  if (item.guide && item.guide in LEAD_PLAYBOOKS) return playbookForKind(item.guide as keyof typeof LEAD_PLAYBOOKS);
  if (item.guide) return playbookForStage(item.guide);
  return null;
}

function howTo(item: WorkSuggestion): string {
  const book = bookFor(item);
  if (!book) return `${item.action} ${item.because}`;
  return [item.action, '', 'How:', ...book.steps.map((step, index) => `${index + 1}. ${step}`), '', `Say: ${book.say}`, `Avoid: ${book.avoid}`].join('\n');
}

function briefing(world: AgentWorld): string {
  const top = world.items.slice(0, 3);
  const lines = [
    world.items.length === 0
      ? 'Nothing is waiting in the records that loaded.'
      : `${world.items.length} record${world.items.length === 1 ? '' : 's'} need a step.`,
  ];
  for (const item of top) lines.push(`${item.name}: ${item.action}`);
  lines.push('Calculated from the closed records loaded here:');
  lines.push(...world.patterns.lines);
  if (world.gaps?.length) lines.push(`Not loaded: ${world.gaps.join(' ')}`);
  return lines.join('\n');
}

/** A colon that starts the person's own words. The one in 10:30 does not. */
const PAYLOAD_COLON = /:(?!\d)/;

/**
 * The command part of a question: what comes before the person's own words. Names and
 * intents are read only here, so "send Ada: please add a note" is a send to Ada, and a
 * note that mentions someone else does not turn into "which one?".
 */
function commandOf(question: string): string {
  return question.split(/:(?!\d)|["“]/)[0] ?? question;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * The words a person typed for a note, comment, or task, after the command. A colon or
 * quotes mark them exactly. "for Ada Lovelace" before them is the record, not the text.
 */
function ownWords(question: string, command: RegExp, name: string | null): string {
  const match = command.exec(question);
  if (!match) return '';
  let rest = question.slice((match.index ?? 0) + match[0].length);
  const quoted = /["“](.+?)["”]/.exec(rest);
  if (quoted?.[1]?.trim()) return quoted[1].trim();
  const colon = rest.search(PAYLOAD_COLON);
  if (colon >= 0) return rest.slice(colon + 1).trim();
  if (name) {
    const parts = [name, name.split(/\s+/)[0] ?? ''].filter((part) => part.length >= 2).map(escapeRegExp);
    rest = rest.replace(new RegExp(`^\\s*(?:to|for|on|about|with)\\s+(?:${parts.join('|')})\\b[\\s,.-]*`, 'i'), '');
  }
  return rest.replace(/^\s*(?:to|for|on|about|that|saying)\b\s*/i, '').trim().replace(/[.?!]+$/, '');
}

/** A send to someone whose contact record says "do not contact" is refused. */
function doNotContact(world: AgentWorld, item: WorkSuggestion): boolean {
  const email = item.email?.trim().toLowerCase();
  const { contactId } = idsFromHref(item.href);
  return (world.contacts ?? []).some((contact) => contact.doNotContact
    && (contact.id === contactId || (!!email && contact.email?.trim().toLowerCase() === email)));
}

const WEEKDAYS = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];

/** A day the person named: today, tomorrow, in 3 days, next week, Friday, or 2026-11-02. */
function dayIn(question: string, now: Date): { day: string; text: string } | null {
  const today = todayKey(now);
  const iso = /\b(?:on |by )?(\d{4}-\d{2}-\d{2})\b/i.exec(question);
  if (iso?.[1]) return { day: iso[1], text: iso[0] };
  const word = /\b(?:on |by )?(today|tomorrow|next week)\b/i.exec(question);
  if (word?.[1]) {
    const shift = { today: 0, tomorrow: 1, 'next week': 7 }[word[1].toLowerCase() as 'today' | 'tomorrow' | 'next week'];
    return { day: shiftKey(today, shift), text: word[0] };
  }
  const inDays = /\bin (\d{1,3}) days?\b/i.exec(question);
  if (inDays?.[1]) return { day: shiftKey(today, Number(inDays[1])), text: inDays[0] };
  const weekday = new RegExp(`\\b(?:on |by |next )?(${WEEKDAYS.join('|')})\\b`, 'i').exec(question);
  if (weekday?.[1]) {
    const wanted = WEEKDAYS.indexOf(weekday[1].toLowerCase());
    const ahead = (wanted - now.getDay() + 7) % 7 || 7;
    return { day: shiftKey(today, ahead), text: weekday[0] };
  }
  return null;
}

const TASK = /\b(create a task|add a task|make a task|new task|remind me)\b/i;
const NOTE = /\b(log a note|add a note|make a note|note that|write down)\b/i;
const COMMENT = /\b(comment on|add a comment)\b/i;
/** Questions about lists and totals: a name inside them is a filter, not a person to act on. */
const LISTING = /\b(find|search for|look up|how many|pipeline value|pipeline total|where is the money|show me the money|biggest deal|largest deal|duplicates|who went quiet|gone quiet|gone silent|neglected|who should i call|who do i call|call list|who to call|compare|top three|top 3|rank them|show the order|win rate|what did we learn)\b/;

/**
 * Answer from the records already loaded. Refuses to invent a person, a number, or a send.
 * A change is proposed here and performed only after the desk confirms it.
 */
export function askAgent(question: string, world: AgentWorld, now: Date = new Date(), memory?: AgentMemory): AgentReply {
  if (!clean(question)) return say('Ask what to do next, who went quiet, why that order, or what the closed deals say.');
  const command = commandOf(question);
  const q = clean(command);

  if (/\b(delete|wipe|destroy|erase)\b/.test(q)) {
    return say('I cannot delete records. Nothing was changed.');
  }

  const listing = LISTING.test(q);
  const clash = listing ? null : ambiguousPeople(world.leads, world.contacts, command);
  if (clash) {
    return say([
      `${clash.length} records match. I will not guess.`,
      ...clash.map((row, index) => `${index + 1}. ${row.label}`),
      'Which one?',
    ].join('\n'), clash.map((row) => ({
      id: `open:${row.href}`,
      label: `Open ${row.label}`,
      kind: 'open' as const,
      href: row.href,
    })));
  }

  const matched = listing ? { status: 'none' as const } : matchPeople(world.items, command, memory);
  if (matched.status === 'many') {
    const choices = matched.items.slice(0, 5);
    return say([
      `${matched.items.length} records match. I will not guess.`,
      ...choices.map((item, index) => `${index + 1}. ${item.name}, ${item.list}. ${item.action}`),
      'Which one?',
    ].join('\n'), choices.map(openFor), null);
  }
  const person = matched.status === 'one' ? matched.item : null;

  if (/^(help|what can you do|commands)$/.test(q)) {
    return say([
      'I answer from the records loaded for this workspace, and I keep the last record you asked about.',
      'Ask: brief this, recent activity, who went quiet, pipeline value, draft that, create a contact, create a deal, set the amount, duplicates, what should I do.',
      'A draft opens the sales writer. Send, assign, convert, enroll, a task, a note, or a comment happens only after you press the button.',
    ].join('\n'));
  }

  if (/^(why|explain|and why|why that|why this order|why first)$/.test(q) || /why (that|this) order/.test(q)) {
    const focus = person ?? recall(world, memory);
    if (focus?.action && !/order|first/.test(q)) {
      return say(`${focus.name}. ${focus.action}\n${focus.because}`, [openFor(focus)], focus);
    }
    const rows = listQueue(world);
    const [lead] = rows;
    if (!lead) return say(emptyQueue(world));
    return say(whyThisOrder(rows), [openFor(lead)], lead);
  }

  if (/(what if i wait|if i wait|what happens if i leave|cost of waiting)/.test(q)) {
    const rows = listQueue(world);
    const [lead] = rows;
    if (!lead) return say(emptyQueue(world));
    return say(costOfWaiting(rows), [openFor(lead)], lead);
  }

  if (/\b(compare|top three|top 3|rank them|show the order)\b/.test(q)) {
    const top = listQueue(world).slice(0, 3);
    const [lead] = top;
    if (!lead) return say(emptyQueue(world));
    return say(top.map((item, index) => `${index + 1}. ${item.name} (${item.priority}). ${item.action}`).join('\n'), [openFor(lead)], lead);
  }

  if (/(what did we learn|why do we lose|why are we losing|pattern|what works|closed deals|win rate)/.test(q)) {
    return say(`Calculated from the closed records loaded here:\n${world.patterns.lines.join('\n')}`);
  }

  if (/\b(other opportunities|other opportunity|other deals|their deals|more deals)\b/.test(q)) {
    return otherDeals(world, person, memory);
  }

  if (/\b(who else|other contacts|other people|their contacts)\b/.test(q)) {
    return otherPeople(world, person, memory);
  }

  if (/\b(find|search for|look up)\b/.test(q)) {
    return searchLoaded(world, question);
  }

  if (/\bhow many\b/.test(q) && /\b(leads|deals|contacts|companies)\b/.test(q)) {
    const kind = /\bleads\b/.test(q) ? 'leads' : /\bdeals\b/.test(q) ? 'deals' : /\bcontacts\b/.test(q) ? 'contacts' : 'companies';
    const label = `${kind[0]?.toUpperCase() ?? ''}${kind.slice(1)}`;
    const openOnly = kind === 'deals' && /\bopen\b/.test(q);
    const count = kind === 'leads'
      ? world.leads.length
      : kind === 'deals'
        ? (openOnly ? world.deals.filter(isOpenDeal).length : world.deals.length)
        : kind === 'contacts'
          ? (world.contacts ?? []).length
          : (world.companies ?? []).length;
    const failed = notLoaded(world, label);
    if (failed) return say(failed);
    const line = `Calculated from the records loaded here: ${count} ${openOnly ? 'open ' : ''}${kind}.`;
    const gap = limitLine(world);
    return say(gap ? `${line}\n${gap}` : line);
  }

  if (/(invite|who hasn.t joined|waiting to join|pending invite)/.test(q)) {
    return say('Invitations are not loaded in this panel. The Team page lists who has not joined, and can resend an invite.', [
      { id: 'open-team', label: 'Open team', kind: 'open', href: '/team' },
    ]);
  }

  if (/(who should i call|who do i call|call list|who to call)/.test(q)) {
    return callSlate(world, listQueue(world));
  }

  if (/(quiet|gone silent|no contact|stale|haven.t heard|no touch|neglected|overdue follow-ups|overdue follow ups)/.test(q)) {
    return neglectedReport(world, now);
  }

  if (/\b(recent activity|latest activity|what happened|outstanding|open tasks|commitments)\b/.test(q)) {
    return activityReport(world, person, memory);
  }

  if (/\b(brief|briefing)\b/.test(q) && !/^(brief me|briefing)$/.test(q)) {
    if (person || (memory && /\b(this|that|their|the company|the deal|the contact)\b/.test(q))) {
      return recordBrief(world, person, memory, now);
    }
    const named = briefByName(world, q, now);
    if (named) return named;
    return say('No loaded company, contact, or deal matches that name.');
  }

  if (/(how many|\bcount\b|pipeline size|summary|brief me|good morning|what.s going on|catch me up|rundown)/.test(q)) {
    return say(briefing(world), world.items[0] ? [openFor(world.items[0])] : [], world.items[0] ?? null);
  }

  if (/(what should i do|what.s next|next step|do this next|priorit|where do i start|most important|first thing|start my day|what matters)/.test(q) && !person) {
    const queue = nextQueue(world);
    const judged = judgmentText(queue, world.leads, world.deals, world.patterns, now);
    if (!judged.pick) return say(emptyQueue(world));
    const reach = reachSentence(judged.pick, world.leads);
    const next = queue.find((item) => item.key !== judged.pick?.key);
    const lines = [`Suggestion. ${judged.text}`];
    if (reach) lines.push(reach);
    if (next) lines.push(`Next in line: ${next.name}. ${next.action}`);
    return say(lines.join('\n'), [openFor(judged.pick), taskFor(judged.pick)], judged.pick);
  }

  if (/(where is the money|biggest deal|largest deal|which deal|show me the money|pipeline value|pipeline total)/.test(q)) {
    if (notLoaded(world, 'Deals')) {
      return say('Deals could not be loaded, so there is no pipeline total.');
    }
    const withMoney = world.deals
      .filter(isOpenDeal)
      .map((deal) => ({ deal, amount: tryParseAmount(deal.value) }))
      .filter((row): row is { deal: SuggestableDeal; amount: number } => row.amount !== null && row.amount > 0)
      .sort((a, b) => b.amount - a.amount);
    if (withMoney.length === 0) return say('No open amount is saved on the loaded deals.');
    const currencies = new Set(withMoney.map((row) => currencyOf({ currency: row.deal.currency ?? undefined })));
    const top = withMoney.slice(0, 3).map((row) => {
      const probability = typeof row.deal.probability === 'number' ? ` Recorded probability ${row.deal.probability}%.` : '';
      return `${row.deal.name}: ${formatMoney(row.amount, currencyOf({ currency: row.deal.currency ?? undefined }))}.${probability}`;
    });
    const order = currencies.size > 1 ? 'The deals are in more than one currency, and nothing is converted, so this order compares the numbers only.' : '';
    const gap = limitLine(world);
    const biggest = withMoney[0];
    const match = biggest ? world.items.find((item) => item.href === `/deals/${biggest.deal.id}`) ?? null : null;
    return say(['Recorded amounts.', ...top, order, pipelineTotals(world.deals), gap].filter(Boolean).join('\n'), match ? [openFor(match)] : [], match);
  }

  if (/\b(the )?(late|overdue) one\b/.test(q)) {
    const late = listQueue(world).find((item) => /passed|overdue|past its due/i.test(`${item.action} ${item.because}`));
    if (!late) return say(world.page.mode === 'list' ? 'No step on this page is past a saved date.' : 'No loaded step is past a saved date.');
    return say(`${late.name}: ${late.action}\n${late.because}`, [openFor(late)], late);
  }

  if (/(what.s the risk|what are the risks|what is at risk|risky|what is overdue|what.s overdue|slipping)/.test(q)) {
    const late = listQueue(world).filter((item) => /passed|overdue|reason is still|nobody is assigned/i.test(`${item.action} ${item.because}`));
    const [lead] = late;
    if (!lead) return say(world.page.mode === 'list'
      ? 'No step on this page is late, missing a reason, or missing an owner.'
      : 'No loaded step is late, missing a reason, or missing an owner.');
    return say(late.slice(0, 5).map((item) => `${item.name}: ${item.because}`).join('\n'), [openFor(lead)], lead);
  }

  if (/(^how |how do i|playbook|talk track)/.test(q)) {
    const item = person ?? world.items[0];
    if (!item) return say('There is no open step to explain.');
    return say(howTo(item), [openFor(item)], item);
  }

  if (person && /\b(email|phone)\b/.test(q) && !/\b(draft|write|send|create|add|message|note|task|remind)\b/.test(q)) {
    const ctx = remember(person, memory, { leads: world.leads, deals: world.deals, contacts: world.contacts });
    const lead = ctx.leadId ? world.leads.find((row) => row.id === ctx.leadId) : undefined;
    const contact = ctx.contactId ? (world.contacts ?? []).find((row) => row.id === ctx.contactId) : undefined;
    if (/\bemail\b/.test(q)) {
      const email = lead?.email || contact?.email || person.email || null;
      return say(email ? `Recorded email for ${person.name}: ${email}.` : `No email is saved on ${person.name}.`, [openFor(person)], person);
    }
    const phone = lead?.phone || contact?.phone || null;
    return say(phone ? `Recorded phone for ${person.name}: ${phone}.` : `No phone is saved on ${person.name}.`, [openFor(person)], person);
  }

  // Tasks, notes, and comments carry the person's own words. They come before "draft" and
  // "send", which would otherwise catch "remind me to email Ada" or "write down that…".
  if (COMMENT.test(q)) {
    const item = target(world, person, memory, command);
    const taskId = item ? /^\/tasks\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    if (!item || !taskId) return say('Name the task the comment belongs on.');
    const body = ownWords(question, COMMENT, item.name);
    if (!body) return say(`What should the comment on ${item.name} say? For example: add a comment: waiting on the signed copy.`, [openFor(item)], item);
    return say(`I can add this comment on ${item.name}: "${body}". It is saved only after you confirm.`, [{
      id: `comment:${item.key}:${body.toLowerCase()}`,
      label: `Comment on ${item.name}`,
      kind: 'comment',
      taskId: decodeURIComponent(taskId),
      body,
      href: item.href,
    }], item);
  }

  if (TASK.test(q)) {
    const item = target(world, person, memory, command);
    if (!item) return say('Name the lead, deal, or contact the task is for. I will not attach it to another record.');
    const when = dayIn(question, now);
    const typed = ownWords(when ? question.replace(when.text, ' ') : question, TASK, item.name);
    const title = typed ? `${typed[0]?.toUpperCase() ?? ''}${typed.slice(1)}` : undefined;
    const action = taskFor(item, title, when?.day);
    const due = when ? `, due ${when.day}` : ', with no due date';
    return say(`I can create a task on ${item.name}: "${action.title}"${due}. It is saved only after you confirm.`, [action], item);
  }

  if (NOTE.test(q)) {
    const item = target(world, person, memory, command);
    if (!item) return say('Name the record the note belongs on.');
    const body = ownWords(question, NOTE, item.name);
    if (!body) return say(`What should the note on ${item.name} say? For example: add a note: left a voicemail.`, [openFor(item)], item);
    return say(`I can log this note on ${item.name}: "${body}". It is saved only after you confirm.`, [{
      id: `note:${item.key}:${body.toLowerCase()}`,
      label: `Log note on ${item.name}`,
      kind: 'note',
      body,
      href: item.href,
      ...idsFromHref(item.href),
    }], item);
  }

  if ((/\b(draft|write|compose)\b/.test(q) || /^(email|message)\b/.test(q)) && !/\bsend\b/.test(q)) {
    const item = target(world, person, memory, command);
    if (!item) return say('Name the lead, contact, or deal the draft is for.');
    const draft = groundedDraft(world, item);
    if (!draft) return say(`${item.name} is not a lead, contact, or deal, so there is no message to draft.`, [openFor(item)], item);
    return say(`The draft for ${item.name} opens in the sales writer. Nothing is sent.`, [draft], item);
  }

  if (/\bsend\b/.test(q) && !/contract/.test(q)) {
    const item = target(world, person, memory, command);
    if (!item) return say('Name the person the message is for.');
    if (!item.email?.trim()) return say(`No email is saved on ${item.name}, so nothing can be sent.`, [openFor(item)], item);
    if (doNotContact(world, item)) return say(`${item.name} is marked do not contact. Nothing will be sent.`, [openFor(item)], item);
    // Only words the person wrote are sent. The saved step is an instruction to the rep,
    // not a message to the customer.
    const quoted = /["“](.+?)["”]/.exec(question)?.[1]?.trim();
    const colon = question.search(PAYLOAD_COLON);
    const saying = /\b(?:saying|that says)\s+(.+)$/i.exec(question)?.[1]?.trim();
    const body = quoted || (colon >= 0 ? question.slice(colon + 1).trim() : '') || saying || '';
    if (!body) {
      const draft = groundedDraft(world, item);
      return say(
        `Write the message first. I will not send the saved step as an email. Say it like: send ${item.name}: "Thanks for today. The deck is attached." Or open the draft in the sales writer.`,
        draft ? [draft] : [openFor(item)],
        item,
      );
    }
    const subject = 'Following up';
    return say(`I can send this to ${item.email}.\nSubject: ${subject}\n${body}\nIt goes out only after you confirm.`, [{
      id: `send:${item.key}:${body.toLowerCase()}`,
      label: `Send to ${item.name}`,
      kind: 'send',
      to: item.email,
      title: subject,
      body,
      ...idsFromHref(item.href),
    }], item);
  }

  if (/\b(assign|i.?ll take it|assign to me)\b/.test(q)) {
    const item = target(world, person, memory, command);
    const leadId = item ? leadIdFrom(item.href) : null;
    if (!item || !leadId) return say('Name the lead to assign. Deals and tasks are assigned on their own record.');
    if (!/nobody is assigned/i.test(`${item.action} ${item.because}`)) return say(`${item.name} already has an owner, or no open step says otherwise.`, [openFor(item)], item);
    return say(`I can assign ${item.name} to you. It is saved only after you confirm.`, [{
      id: `assign:${item.key}`,
      label: `Assign ${item.name} to me`,
      kind: 'assign',
      leadId,
      href: item.href,
    }], item);
  }

  if (/\bconvert\b/.test(q)) {
    const item = target(world, person, memory, command);
    const leadId = item ? leadIdFrom(item.href) : null;
    if (!item || !leadId) return say('Name the lead to convert.');
    return say(`I can convert ${item.name} into a contact and a deal. It is saved only after you confirm.`, [{
      id: `convert:${item.key}`,
      label: `Convert ${item.name}`,
      kind: 'convert',
      leadId,
      recipientName: item.name,
      href: item.href,
    }], item);
  }

  if (/\benroll\b/.test(q)) {
    const sequences = (world.sequences ?? []).slice(0, 5);
    const item = target(world, person, memory, command);
    const leadId = item ? leadIdFrom(item.href) : null;
    if (sequences.length === 0) return say('No sequence is saved.');
    if (!item || !leadId) return say('Name the lead to enroll.');
    return say(`Pick the sequence for ${item.name}. It is saved only after you confirm.`, sequences.map((sequence) => ({
      id: `enroll:${item.key}:${sequence.id}`,
      label: `Enroll in ${sequence.name}`,
      kind: 'enroll' as const,
      sequenceId: sequence.id,
      leadId,
      recipientName: item.name,
      to: item.email ?? undefined,
      href: item.href,
    })), item);
  }

  if (/\b(duplicates|duplicate records|lookalikes)\b/.test(q) && !/\bmerge\b/.test(q)) {
    return duplicateReview(world);
  }

  if (/\b(create|add|new)\b/.test(q) && /\b(contact|person)\b/.test(q)) {
    return proposeContact(world, question, memory);
  }

  if (/\b(create|add|new)\b/.test(q) && /\b(deal|opportunity)\b/.test(q)) {
    return proposeDeal(world, question, memory);
  }

  if (/\b(set|change|update)\b/.test(q) && /\b(amount|value|close date|expected close)\b/.test(q)) {
    return proposeDealUpdate(world, question, person, memory);
  }

  if (/\bmerge\b/.test(q)) {
    const groups = world.items.filter((entry) => entry.mergePrimaryId && entry.mergeDuplicateIds?.length).slice(0, 5);
    if (groups.length === 0) return say('No duplicate group is waiting.');
    return say(groups.map((entry) => entry.action).join('\n'), groups.map((entry) => ({
      id: `merge:${entry.key}`,
      label: `Merge into ${entry.name}`,
      kind: 'merge' as const,
      mergeKind: entry.mergeKind,
      mergePrimaryId: entry.mergePrimaryId,
      mergeDuplicateIds: entry.mergeDuplicateIds,
      href: entry.href,
    })), groups[0]);
  }

  if (/\b(move the stage|advance the deal|next stage)\b/.test(q)) {
    const item = target(world, person, memory, command);
    const deal = item?.href.startsWith('/deals/') ? item : null;
    return say('This panel does not load pipeline stages, so it cannot pick the next one. Move the stage on the deal itself.', deal ? [openFor(deal)] : [], deal);
  }

  if (/\b(book|schedule the meeting|schedule a meeting)\b/.test(q)) {
    const item = target(world, person, memory, command);
    if (!item) return say('Name who the meeting is with.');
    const when = dayIn(question, now);
    const due = when?.day ?? shiftKey(todayKey(now), world.followUpDays ?? 3);
    return say(`I can save a meeting task for ${item.name} due ${due}. A club reservation still needs a room on the Reservations page. The task is saved only after you confirm.`, [{
      ...taskFor(item, `Meeting with ${item.name}`, due),
      label: `Schedule meeting for ${item.name}`,
    }], item);
  }

  if (person && /\b(what do we have|tell me about|show me|details on)\b/.test(q)) {
    return recordBrief(world, person, memory, now);
  }

  if (person && /(who is|status of|plan for|about|update on)/.test(q)) {
    return say(`${person.name}, ${person.list}. ${person.action}\n${person.because}`, [openFor(person), taskFor(person)], person);
  }

  if (person) {
    return say(`${person.name}: ${person.action}\n${person.because}`, [openFor(person)], person);
  }

  const top = world.items[0];
  if (!top) return say('Nothing is waiting.\nThat was not a set question, so this is the queue that loaded.');
  return say(`${top.name} is first in the loaded queue: ${top.action}\n${briefing(world)}\nThat was not a set question, so this is the queue that loaded.`, [openFor(top)], top);
}
