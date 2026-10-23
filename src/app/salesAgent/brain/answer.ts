import { shiftKey, todayKey } from '../calendarDay';
import type { SuggestableLead } from '../nextAction';
import type { RecordPlan } from '../recordPlan';
import type { SuggestableContact, SuggestableDeal, SuggestableTask, WorkSuggestion } from '../workspace';
import { itemOnList, type PageScope } from '../pageScope';
import { LEAD_PLAYBOOKS, playbookForKind, playbookForStage, type Playbook } from './playbooks';
import type { LearnedPatterns } from './patterns';
import type { WriterBrief } from '../writerBrief';
import { judgmentText } from './judge';
import { ambiguousPeople, costOfWaiting, emptyMemory, matchPeople, remember, whyThisOrder, type AgentMemory } from './reason';
import { activityReport, amountOf, duplicateReview, groundedDraft, neglectedReport, pipelineTotals, proposeContact, proposeDeal, proposeDealUpdate, recordBrief } from './workflows';

export interface AgentWorld {
  items: WorkSuggestion[];
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  plan: RecordPlan | null;
  page: PageScope;
  patterns: LearnedPatterns;
  /** Emails with an invitation that has not been accepted. */
  invites: { id?: string; email: string }[];
  /** Contract hint for the open lead, when one is loaded. */
  contractHint: string | null;
  /** Totals that are not a single next step: pipeline value, copy history, required fields. */
  facts?: string[];
  /** Sequences already saved, so an enroll button can name one when there is only one. */
  sequences?: { id: string; name: string }[];
  /** Workspace setting used when a meeting is saved as a dated task. */
  followUpDays?: number;
  /** Contacts loaded with the queue, used to keep a company in view. */
  contacts?: SuggestableContact[];
  companies?: { id: string; name: string }[];
  /** Lists that failed or were capped. Facts must not be treated as complete when this is set. */
  gaps?: string[];
  tasks?: SuggestableTask[];
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
  kind: 'task' | 'note' | 'draft' | 'open' | 'resend' | 'comment' | 'send' | 'assign' | 'convert' | 'enroll' | 'merge' | 'stage' | 'create-contact' | 'create-deal' | 'update-deal';
  href?: string;
  title?: string;
  body?: string;
  leadId?: string;
  dealId?: string;
  contactId?: string;
  taskId?: string;
  inviteId?: string;
  sequenceId?: string;
  to?: string;
  dueDateUtc?: string;
  stageId?: string;
  stageName?: string;
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
}

function clean(value: string): string {
  return value.trim().toLowerCase().replace(/[?.!]+$/g, '');
}

function idsFromHref(href: string): { leadId?: string; dealId?: string; contactId?: string } {
  const match = /^\/(leads|deals|contacts)\/([^/]+)$/.exec(href);
  if (!match) return {};
  const id = decodeURIComponent(match[2]);
  if (match[1] === 'leads') return { leadId: id };
  if (match[1] === 'deals') return { dealId: id };
  return { contactId: id };
}

function party(world: AgentWorld, person: WorkSuggestion | null, memory?: AgentMemory): AgentMemory {
  if (person) return remember(person, memory, { leads: world.leads, deals: world.deals, contacts: world.contacts });
  return memory?.focusKey ? { ...emptyMemory(), ...memory } : emptyMemory();
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

function otherDeals(world: AgentWorld, person: WorkSuggestion | null, memory?: AgentMemory): AgentReply {
  if (world.gaps?.some((gap) => gap.startsWith('Deals could not'))) {
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
    ...shown.map((deal) => `${deal.name}: ${deal.value ?? 'no amount saved'} · ${deal.dealStageName || deal.stage || 'no stage'}`),
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

function briefByName(world: AgentWorld, q: string, memory: AgentMemory | undefined, now: Date): AgentReply | null {
  const blank = emptyMemory();
  const leads = world.leads.filter((lead) => lead.name.length >= 3 && q.includes(lead.name.toLowerCase()));
  const contacts = (world.contacts ?? []).filter((contact) => contact.name.length >= 3 && q.includes(contact.name.toLowerCase()));
  const deals = world.deals.filter((deal) => deal.name.length >= 3 && q.includes(deal.name.toLowerCase()));
  const people = [...leads, ...contacts, ...deals];
  if (people.length > 1) {
    return say([
      `${people.length} records match. I will not guess.`,
      ...people.slice(0, 5).map((row, index) => `${index + 1}. ${row.name}`),
      'Which one?',
    ].join('\n'));
  }
  const lead = leads.length === 1 ? leads[0] : undefined;
  if (lead) {
    return recordBrief(world, null, { ...blank, ...memory, focusKey: memory?.focusKey ?? null, name: lead.name, leadId: lead.id, dealId: null, contactId: null, companyId: lead.companyId ?? null, companyName: lead.companyName ?? null }, now);
  }
  const contact = contacts.length === 1 ? contacts[0] : undefined;
  if (contact) {
    return recordBrief(world, null, { ...blank, ...memory, focusKey: memory?.focusKey ?? null, name: contact.name, leadId: null, dealId: null, contactId: contact.id, companyId: contact.companyId ?? null, companyName: contact.companyName ?? null }, now);
  }
  const deal = deals.length === 1 ? deals[0] : undefined;
  if (deal) {
    return recordBrief(world, null, { ...blank, ...memory, focusKey: memory?.focusKey ?? null, name: deal.name, leadId: null, dealId: deal.id, contactId: deal.contactId ?? null, companyId: deal.companyId ?? null, companyName: deal.companyName ?? null }, now);
  }
  const companyNames = new Set<string>();
  for (const row of [...world.leads, ...world.deals, ...(world.contacts ?? [])]) {
    if (row.companyName && row.companyName.length >= 3 && q.includes(row.companyName.toLowerCase())) companyNames.add(row.companyName);
  }
  for (const company of world.companies ?? []) {
    if (company.name.length >= 3 && q.includes(company.name.toLowerCase())) companyNames.add(company.name);
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
  if (world.page.mode !== 'list') return world.items;
  return world.items.filter((item) => itemOnList(item, world.page.list));
}

/** The open record when it has a step, otherwise the list the person is looking at. */
function nextQueue(world: AgentWorld): WorkSuggestion[] {
  if (world.page.mode === 'record') {
    const here = world.items.filter((item) => item.href === world.page.href);
    if (here.length > 0) return here;
  }
  return listQueue(world);
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
  if (shown.length === 0) {
    const where = world.page.mode === 'list' ? 'on this page' : 'in the loaded queue';
    return say(`No step ${where} is a call.`);
  }
  const lines = shown.map(({ item, phone }) => `${item.name}: ${item.action}${phone ? ` Phone ${phone}.` : ''}`);
  return say(lines.join('\n'), [openFor(shown[0].item)], shown[0].item);
}

function taskFor(item: WorkSuggestion): AgentAction {
  return {
    id: `task:${item.key}`,
    label: `Create task for ${item.name}`,
    kind: 'task',
    title: item.action.replace(/\.$/, ''),
    href: item.href,
    ...idsFromHref(item.href),
  };
}

function noteFor(item: WorkSuggestion, body: string): AgentAction {
  return {
    id: `note:${item.key}`,
    label: `Log note on ${item.name}`,
    kind: 'note',
    body,
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
  if (world.contractHint) lines.push(`Open contract: ${world.contractHint}`);
  if (world.invites.length) lines.push(`${world.invites.length} team invitation${world.invites.length === 1 ? '' : 's'} still waiting.`);
  lines.push('Calculated from the closed records loaded here:');
  lines.push(...world.patterns.lines);
  lines.push(...(world.facts ?? []));
  if (world.gaps?.length) lines.push(`Not loaded: ${world.gaps.join(' ')}`);
  return lines.join('\n');
}

/**
 * Answer from the records already loaded. Refuses to invent a person, a number, or a send.
 * Writing a task or a note is proposed here and performed only after the desk confirms it.
 */
export function askAgent(question: string, world: AgentWorld, now: Date = new Date(), memory?: AgentMemory): AgentReply {
  const q = clean(question);
  if (!q) return say('Ask what to do next, who went quiet, why that order, or what the closed deals say.');

  if (/\b(delete|wipe|destroy|erase)\b/.test(q)) {
    return say('I cannot delete records. Nothing was changed.');
  }

  const listing = /\b(find|search for|look up|how many|pipeline value|duplicates|quiet|neglected)\b/.test(q);
  const clash = listing ? null : ambiguousPeople(world.leads, world.contacts, question);
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

  const matched = matchPeople(world.items, question, memory);
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
      'A draft opens the sales writer. Send, assign, convert, enroll, a task, a note, a comment, or an invite resend happens only after you press the button.',
    ].join('\n'));
  }

  if (/^(why|explain|and why|why that|why this order|why first)$/.test(q) || /why (that|this) order/.test(q)) {
    const focus = person ?? world.items.find((item) => item.key === memory?.focusKey) ?? null;
    if (focus && !/order|first/.test(q)) {
      return say(`${focus.name}. ${focus.action}\n${focus.because}`, [openFor(focus)], focus);
    }
    const rows = listQueue(world);
    if (rows.length === 0) return say(emptyQueue(world));
    return say(whyThisOrder(rows), [openFor(rows[0])], rows[0]);
  }

  if (/(what if i wait|if i wait|what happens if i leave|cost of waiting)/.test(q)) {
    const rows = listQueue(world);
    if (rows.length === 0) return say(emptyQueue(world));
    return say(costOfWaiting(rows), [openFor(rows[0])], rows[0]);
  }

  if (/\b(compare|top three|top 3|rank them|show the order)\b/.test(q)) {
    const top = listQueue(world).slice(0, 3);
    if (top.length === 0) return say(emptyQueue(world));
    return say(top.map((item, index) => `${index + 1}. ${item.name} (${item.priority}). ${item.action}`).join('\n'), [openFor(top[0])], top[0]);
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
    const openOnly = kind === 'deals' && /\bopen\b/.test(q);
    const count = kind === 'leads'
      ? world.leads.length
      : kind === 'deals'
        ? (openOnly ? world.deals.filter((deal) => deal.isWon !== true && deal.isWon !== false && !/closed won|closed lost/i.test(`${deal.dealStageName ?? ''} ${deal.stage ?? ''}`)).length : world.deals.length)
        : kind === 'contacts'
          ? (world.contacts ?? []).length
          : (world.companies ?? []).length;
    const failed = world.gaps?.find((gap) => gap.toLowerCase().startsWith(kind));
    const line = failed ? failed : `Calculated from the records loaded here: ${count} ${openOnly ? 'open ' : ''}${kind}.`;
    const gap = limitLine(world);
    return say(gap && !failed ? `${line}\n${gap}` : line);
  }

  if (/(invite|who hasn.t joined|waiting to join|pending invite)/.test(q)) {
    if (world.invites.length === 0) return say('No team invitation is waiting.');
    const first = world.invites[0];
    const actions: AgentAction[] = [{ id: 'open-team', label: 'Open team', kind: 'open', href: '/team' }];
    if (first.id) actions.push({ id: `resend:${first.id}`, label: `Resend invite to ${first.email}`, kind: 'resend', inviteId: first.id });
    return say(
      world.invites.map((invite) => `${invite.email} has not accepted yet.`).join('\n'),
      actions,
    );
  }

  if (!person && /\b(club|members?|charges?|reservations?|applications?|subscriptions?|campaigns?)\b/.test(q)) {
    const club = world.items.filter((item) => /Members|Charges|Reservations|Applications|Subscriptions|Campaigns|Events|Gift|Tabs|Communications|Donations|Households|Loyalty/.test(item.list));
    if (club.length === 0) return say('No club record that loaded is waiting on a step.');
    const top = club.slice(0, 5);
    return say(top.map((item) => `${item.name} (${item.list}): ${item.action}`).join('\n'), [openFor(top[0])], top[0]);
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
    if (person || (memory?.focusKey && /\b(this|that|their|the company|the deal|the contact)\b/.test(q))) {
      return recordBrief(world, person, memory, now);
    }
    const named = briefByName(world, q, memory, now);
    if (named) return named;
    return say('No loaded company, contact, or deal matches that name.');
  }

  if (/(how many|count|pipeline size|summary|brief me|good morning|what.s going on|catch me up|rundown)/.test(q)) {
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
    if (world.contractHint) lines.push(`On the open contract: ${world.contractHint}`);
    return say(lines.join('\n'), [openFor(judged.pick), taskFor(judged.pick)], judged.pick);
  }

  if (/(where is the money|biggest deal|largest deal|which deal|show me the money|pipeline value)/.test(q)) {
    if (world.gaps?.some((gap) => gap.startsWith('Deals could not'))) {
      return say('Deals could not be loaded, so there is no pipeline total.');
    }
    const openDeals = world.deals.filter((deal) => deal.isWon !== true && deal.isWon !== false && !/closed won|closed lost/i.test(`${deal.dealStageName ?? ''} ${deal.stage ?? ''}`));
    const withMoney = openDeals
      .map((deal) => ({ deal, amount: amountOf(deal.value) }))
      .filter((row): row is { deal: SuggestableDeal; amount: number } => row.amount !== null)
      .sort((a, b) => b.amount - a.amount);
    if (withMoney.length === 0 && (world.facts ?? []).length === 0) return say('No open amount is saved on the loaded deals.');
    const top = withMoney.slice(0, 3).map((row) => {
      const probability = typeof row.deal.probability === 'number' ? ` Recorded probability ${row.deal.probability}%.` : '';
      return `${row.deal.name}: ${row.deal.value}.${probability}`;
    }).join('\n');
    const stages = (world.facts ?? []).filter((line) => line.includes('across')).join('\n');
    const calculated = pipelineTotals(world.deals);
    const gap = limitLine(world);
    const match = withMoney[0] ? world.items.find((item) => item.href === `/deals/${withMoney[0].deal.id}`) ?? null : null;
    return say(['Recorded amounts.', top, stages, calculated, gap].filter(Boolean).join('\n'), match ? [openFor(match)] : [], match);
  }

  if (/\b(the )?(late|overdue) one\b/.test(q)) {
    const late = listQueue(world).find((item) => /passed|overdue|past its due/i.test(`${item.action} ${item.because}`));
    if (!late) return say(world.page.mode === 'list' ? 'No step on this page is past a saved date.' : 'No loaded step is past a saved date.');
    return say(`${late.name}: ${late.action}\n${late.because}`, [openFor(late)], late);
  }

  if (/(what.s the risk|what are the risks|what is at risk|risky|what is overdue|what.s overdue|slipping)/.test(q)) {
    const late = listQueue(world).filter((item) => /passed|overdue|reason is still|nobody is assigned/i.test(`${item.action} ${item.because}`));
    if (late.length === 0) {
      return say(world.page.mode === 'list'
        ? 'No step on this page is late, missing a reason, or missing an owner.'
        : 'No loaded step is late, missing a reason, or missing an owner.');
    }
    return say(late.slice(0, 5).map((item) => `${item.name}: ${item.because}`).join('\n'), [openFor(late[0])], late[0]);
  }

  if (/(^how |how do i|playbook|talk track)/.test(q)) {
    const item = person ?? world.items[0];
    if (!item) return say('There is no open step to explain.');
    return say(howTo(item), [openFor(item)], item);
  }

  if (person && /\b(email|phone)\b/.test(q) && !/\b(draft|write|send|create|add|message)\b/.test(q)) {
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

  if (/(draft|write|email|message)/.test(q) && !/\bsend\b/.test(q)) {
    const remembered = world.items.find((item) => item.key === memory?.focusKey) ?? null;
    const item = person ?? remembered ?? world.items.find((entry) => entry.brief) ?? null;
    if (!item) return say('Name the lead, contact, or deal the draft is for.');
    const draft = groundedDraft(world, item);
    if (!draft) return say(`${item.name} is not a lead, contact, or deal, so there is no message to draft.`, [openFor(item)], item);
    return say(`The draft for ${item.name} opens in the sales writer. Nothing is sent.`, [draft], item);
  }

  if (/\bsend\b/.test(q) && !/contract/.test(q)) {
    const remembered = world.items.find((item) => item.key === memory?.focusKey) ?? null;
    const item = person ?? remembered ?? world.items.find((entry) => entry.email) ?? null;
    if (!item) return say('Name the person the message is for.');
    if (!item.email?.trim()) return say(`No email is saved on ${item.name}, so nothing can be sent.`, [openFor(item)], item);
    return say(`I can send a short note to ${item.email}. It goes out only after you confirm.`, [{
      id: `send:${item.key}`,
      label: `Send to ${item.name}`,
      kind: 'send',
      to: item.email,
      title: `Following up with ${item.name}`,
      body: `${item.action}\n\n${item.because}`,
      ...idsFromHref(item.href),
    }], item);
  }

  if (/\b(assign|i.?ll take it|assign to me)\b/.test(q)) {
    const item = person ?? world.items.find((entry) => entry.key === memory?.focusKey) ?? world.items.find((entry) => /nobody is assigned/i.test(entry.because)) ?? null;
    const leadId = item ? /^\/leads\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    if (!item || !leadId) return say('Name the lead to assign. Deals and tasks are assigned on their own record.');
    if (!/nobody is assigned/i.test(`${item.action} ${item.because}`)) return say(`${item.name} already has an owner.`, [openFor(item)], item);
    return say(`I can assign ${item.name} to you. It is saved only after you confirm.`, [{
      id: `assign:${item.key}`,
      label: `Assign ${item.name} to me`,
      kind: 'assign',
      leadId: decodeURIComponent(leadId),
      href: item.href,
    }], item);
  }

  if (/\bconvert\b/.test(q)) {
    const item = person ?? world.items.find((entry) => entry.guide === 'convert') ?? null;
    const leadId = item ? /^\/leads\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    if (!item || !leadId) return say('No loaded lead is ready to convert.');
    return say(`I can convert ${item.name} into a contact and a deal. It is saved only after you confirm.`, [{
      id: `convert:${item.key}`,
      label: `Convert ${item.name}`,
      kind: 'convert',
      leadId: decodeURIComponent(leadId),
      recipientName: item.name,
      href: item.href,
    }], item);
  }

  if (/\benroll\b/.test(q)) {
    const sequences = (world.sequences ?? []).slice(0, 5);
    const item = person ?? world.items.find((entry) => entry.href.startsWith('/leads/')) ?? null;
    const leadId = item ? /^\/leads\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    if (sequences.length === 0) return say('No sequence is saved.');
    if (!item || !leadId) return say('Name the lead to enroll.');
    const id = decodeURIComponent(leadId);
    return say(`Pick the sequence for ${item.name}. It is saved only after you confirm.`, sequences.map((sequence) => ({
      id: `enroll:${item.key}:${sequence.id}`,
      label: `Enroll in ${sequence.name}`,
      kind: 'enroll' as const,
      sequenceId: sequence.id,
      leadId: id,
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
    const item = person ?? world.items.find((entry) => entry.href.startsWith('/deals/')) ?? null;
    const dealId = item ? /^\/deals\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    const deal = dealId ? world.deals.find((entry) => entry.id === decodeURIComponent(dealId)) : undefined;
    if (!item || !deal?.nextStageName) return say('No loaded deal has a next stage on its pipeline.');
    return say(`I can move ${item.name} to ${deal.nextStageName}. It is saved only after you confirm.`, [{
      id: `stage:${item.key}`,
      label: `Move to ${deal.nextStageName}`,
      kind: 'stage',
      dealId: deal.id,
      stageName: deal.nextStageName,
      stageId: deal.nextStageId ?? undefined,
      href: item.href,
    }], item);
  }

  if (/\b(book|schedule the meeting|schedule a meeting)\b/.test(q)) {
    const item = person ?? world.items.find((entry) => entry.key === memory?.focusKey) ?? world.items[0];
    if (!item) return say('Name who the meeting is with.');
    const days = world.followUpDays ?? 3;
    const due = shiftKey(todayKey(now), days);
    return say(`I can save a meeting task for ${item.name} due ${due}. A club reservation still needs a room on the Reservations page. The task is saved only after you confirm.`, [{
      ...taskFor(item),
      id: `book:${item.key}`,
      label: `Schedule meeting for ${item.name}`,
      title: `Meeting with ${item.name}`,
      dueDateUtc: due,
    }], item);
  }

  if (/(comment on|add a comment)/.test(q)) {
    const item = person ?? world.items.find((entry) => entry.key === memory?.focusKey) ?? world.items.find((entry) => entry.href.startsWith('/tasks/'));
    const taskId = item ? /^\/tasks\/([^/]+)$/.exec(item.href)?.[1] : undefined;
    if (!item || !taskId) return say('Name the task the comment belongs on.');
    const body = question.replace(/^.*?(comment on|add a comment)\s*/i, '').trim() || item.action;
    return say(`I can add this comment on ${item.name}. It is saved only after you confirm.`, [{
      id: `comment:${item.key}`,
      label: `Comment on ${item.name}`,
      kind: 'comment',
      taskId: decodeURIComponent(taskId),
      body,
      href: item.href,
    }], item);
  }

  if (/(create a task|add a task|remind me|make a task)/.test(q)) {
    const item = person ?? world.items.find((entry) => entry.key === memory?.focusKey) ?? world.items[0];
    if (!item) return say('Name who the task is for. Nothing is waiting to attach it to.');
    return say(`I can create a task on ${item.name}: ${item.action} It is saved only after you confirm.`, [taskFor(item)], item);
  }

  if (/(log a note|add a note|note that|write down)/.test(q)) {
    const onPage = world.page.mode === 'record' ? world.items.find((entry) => entry.href === world.page.href) ?? null : null;
    const item = person ?? world.items.find((entry) => entry.key === memory?.focusKey) ?? onPage;
    if (!item) return say('Name the record the note belongs on.');
    const body = question.replace(/^.*?(log a note|add a note|note that|write down)\s*/i, '').trim() || item.action;
    return say(`I can log this note on ${item.name}. It is saved only after you confirm.`, [noteFor(item, body)], item);
  }

  if (person && /\b(what do we have|tell me about|show me|details on)\b/.test(q)) {
    return recordBrief(world, person, memory, now);
  }

  if (person && /(who is|status of|plan for|about|update on)/.test(q)) {
    const planBit = world.plan && world.plan.name === person.name
      ? `\n${world.plan.lines.map((line) => `${line.state}: ${line.title}`).join('\n')}`
      : '';
    return say(`${person.name}, ${person.list}. ${person.action}\n${person.because}${planBit}`, [openFor(person), taskFor(person)], person);
  }

  if (person) {
    return say(`${person.name}: ${person.action}\n${person.because}`, [openFor(person)], person);
  }

  if (world.contractHint && /contract/.test(q)) {
    return say(world.contractHint);
  }

  const words = q.split(/[^a-z0-9]+/).filter((word) => word.length >= 3);
  const named = world.items.filter((item) => words.some((word) => item.name.toLowerCase().includes(word)));
  if (named.length > 1) {
    return say('I could not match that to one loaded record. I will not guess when two names fit equally.');
  }
  const top = world.items[0];
  if (!top) return say('Nothing is waiting.\nThat was not a set question, so this is the queue that loaded.');
  return say(`${top.name} is first in the loaded queue: ${top.action}\n${briefing(world)}\nThat was not a set question, so this is the queue that loaded.`, [openFor(top)], top);
}
