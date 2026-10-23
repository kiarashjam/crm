import { calendarKey, daysBetween, todayKey } from '../calendarDay';
import type { SuggestableDeal, SuggestableTask } from '../workspace';
import type { AgentAction, AgentActivity, AgentReply, AgentWorld } from './answer';
import type { AgentMemory } from './reason';
import { remember } from './reason';

const NEGLECTED_DAYS = 14;

function say(text: string, actions: AgentAction[] = [], focusKey: string | null = null): AgentReply {
  return { text, actions, focusKey };
}

function openHref(href: string, name: string): AgentAction {
  return { id: `open:${href}`, label: `Open ${name}`, kind: 'open', href };
}

function limitLine(world: AgentWorld): string {
  return world.gaps?.length ? `Not loaded: ${world.gaps.join(' ')}` : '';
}

function party(world: AgentWorld, person: { key: string; href: string; name: string } | null, memory?: AgentMemory): AgentMemory {
  if (person) {
    const item = world.items.find((row) => row.key === person.key) ?? world.items.find((row) => row.href === person.href);
    if (item) return remember(item, memory, { leads: world.leads, deals: world.deals, contacts: world.contacts });
  }
  return memory ?? { focusKey: null, name: null, leadId: null, dealId: null, contactId: null, companyId: null, companyName: null };
}

function dealOpen(deal: SuggestableDeal): boolean {
  if (deal.isWon === true || deal.isWon === false) return false;
  const stage = (deal.dealStageName || deal.stage || '').toLowerCase();
  return stage !== 'closed won' && stage !== 'closed lost';
}

export function amountOf(value?: string | null): number | null {
  if (!value) return null;
  const trimmed = value.trim();
  const negative = trimmed.startsWith('-');
  const cleaned = trimmed.replace(/[^0-9.]/g, '');
  if (!/^\d+(\.\d{1,2})?$/.test(cleaned)) return null;
  const amount = Number(cleaned);
  if (!Number.isFinite(amount)) return null;
  return negative ? -amount : amount;
}

function linkedActivities(world: AgentWorld, ctx: AgentMemory): AgentActivity[] {
  return (world.activities ?? [])
    .filter((activity) =>
      (ctx.leadId && activity.leadId === ctx.leadId)
      || (ctx.dealId && activity.dealId === ctx.dealId)
      || (ctx.contactId && activity.contactId === ctx.contactId))
    .slice()
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

function linkedTasks(world: AgentWorld, ctx: AgentMemory): SuggestableTask[] {
  return (world.tasks ?? []).filter((task) => {
    if (task.completed || task.status === 'completed' || task.status === 'cancelled') return false;
    return (ctx.leadId && task.leadId === ctx.leadId)
      || (ctx.dealId && task.dealId === ctx.dealId)
      || (ctx.contactId && task.contactId === ctx.contactId);
  });
}

/** A briefing uses only fields, activities, and tasks already loaded for one company, contact, or deal. */
export function recordBrief(world: AgentWorld, person: { key: string; href: string; name: string; action: string; because: string } | null, memory: AgentMemory | undefined, _now: Date): AgentReply {
  const ctx = party(world, person, memory);
  if (!ctx.leadId && !ctx.dealId && !ctx.contactId && !ctx.companyId && !ctx.companyName && !ctx.name) {
    return say('Name the company, contact, or deal to brief.');
  }
  const lead = ctx.leadId ? world.leads.find((row) => row.id === ctx.leadId) : undefined;
  const deal = ctx.dealId ? world.deals.find((row) => row.id === ctx.dealId) : undefined;
  const contact = ctx.contactId ? (world.contacts ?? []).find((row) => row.id === ctx.contactId) : undefined;
  const companyName = ctx.companyName || lead?.companyName || deal?.companyName || contact?.companyName || null;
  const lines = [`Briefing for ${companyName || ctx.name}. Recorded fields only.`];
  if (lead) {
    lines.push(`Lead ${lead.name}. Status is not repeated beyond the saved next step.`);
    if (lead.email) lines.push(`Email: ${lead.email}.`);
    if (lead.phone) lines.push(`Phone: ${lead.phone}.`);
    if (lead.lastContactedAt) lines.push(`Last contact date: ${lead.lastContactedAt.slice(0, 10)}.`);
    if (typeof lead.leadScore === 'number') lines.push(`Lead score: ${lead.leadScore}.`);
  }
  if (contact) {
    lines.push(`Contact ${contact.name}.`);
    if (contact.email) lines.push(`Email: ${contact.email}.`);
    if (contact.jobTitle) lines.push(`Job title: ${contact.jobTitle}.`);
    if (contact.doNotContact) lines.push('Do not contact is saved.');
  }
  const related = world.deals.filter((row) => dealOpen(row) && (
    (companyName && row.companyName?.toLowerCase() === companyName.toLowerCase())
    || (ctx.companyId && row.companyId === ctx.companyId)
    || (ctx.contactId && row.contactId === ctx.contactId)
    || row.id === ctx.dealId
  ));
  if (related.length === 0) lines.push('No open deal for this record is in the loaded list.');
  const shownDeals = related.slice(0, 5);
  for (const row of shownDeals) {
    lines.push(`Deal ${row.name}: ${row.value ?? 'no amount saved'} · ${row.dealStageName || row.stage || 'no stage'}.`);
  }
  if (related.length > shownDeals.length) lines.push(`${related.length - shownDeals.length} more open deals are loaded and not listed.`);
  const activityAll = linkedActivities(world, ctx);
  const activity = activityAll.slice(0, 5);
  if (world.gaps?.some((gap) => gap.startsWith('Activities could not'))) {
    lines.push('Activities could not be loaded.');
  } else if (activity.length === 0) {
    lines.push('No activity linked to this record is in the loaded list.');
  } else {
    lines.push('Recent activity:');
    for (const row of activity) lines.push(`${row.createdAt.slice(0, 10)} ${row.type}${row.subject ? `: ${row.subject}` : ''}.`);
    if (activityAll.length > activity.length) lines.push(`${activityAll.length - activity.length} more activities are loaded and not listed.`);
  }
  const tasks = linkedTasks(world, ctx);
  if (tasks.length === 0) lines.push('No open task is linked to this record.');
  else {
    lines.push('Outstanding tasks:');
    for (const task of tasks.slice(0, 5)) lines.push(`${task.title}${task.dueDateUtc ? `, due ${task.dueDateUtc.slice(0, 10)}` : ', no due date saved'}.`);
  }
  const step = person ?? world.items.find((item) => item.key === ctx.focusKey) ?? world.items.find((item) => deal && item.href === `/deals/${deal.id}`) ?? null;
  if (step) lines.push(`Suggestion, from the saved step: ${step.action}`);
  const gap = limitLine(world);
  if (gap) lines.push(gap);
  const href = step?.href ?? (deal ? `/deals/${deal.id}` : lead ? `/leads/${lead.id}` : contact ? `/contacts/${contact.id}` : null);
  const name = step?.name ?? deal?.name ?? lead?.name ?? contact?.name ?? companyName ?? 'Record';
  return say(lines.join('\n'), href ? [openHref(href, name)] : [], step?.key ?? null);
}

/** Latest activity and open tasks. Dates come from the loaded records. */
export function activityReport(world: AgentWorld, person: { key: string; href: string; name: string } | null, memory?: AgentMemory): AgentReply {
  const ctx = party(world, person, memory);
  if (world.gaps?.some((gap) => gap.startsWith('Activities could not')) && (world.activities ?? []).length === 0) {
    return say('Activities could not be loaded, so I cannot describe recent activity.');
  }
  const activityAll = ctx.leadId || ctx.dealId || ctx.contactId
    ? linkedActivities(world, ctx)
    : (world.activities ?? []).slice().sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const activity = activityAll.slice(0, 5);
  const taskAll = ctx.leadId || ctx.dealId || ctx.contactId
    ? linkedTasks(world, ctx)
    : (world.tasks ?? []).filter((task) => !task.completed && task.status !== 'completed' && task.status !== 'cancelled');
  const tasks = taskAll.slice(0, 5);
  const who = ctx.name || ctx.companyName || 'the loaded records';
  const lines = [`Recent activity for ${who}.`];
  if (activity.length === 0) lines.push('No activity is in the loaded list.');
  else {
    for (const row of activity) lines.push(`${row.createdAt.slice(0, 10)} ${row.type}${row.subject ? `: ${row.subject}` : ''} · ${row.name}.`);
    if (activityAll.length > activity.length) lines.push(`${activityAll.length - activity.length} more activities are loaded and not listed.`);
  }
  if (tasks.length === 0) lines.push('No open task is linked.');
  else {
    lines.push('Outstanding tasks:');
    for (const task of tasks) lines.push(`${task.title}${task.dueDateUtc ? `, due ${task.dueDateUtc.slice(0, 10)}` : ''}.`);
    if (taskAll.length > tasks.length) lines.push(`${taskAll.length - tasks.length} more open tasks are loaded and not listed.`);
  }
  return say(lines.join('\n'), person ? [openHref(person.href, person.name)] : [], person?.key ?? ctx.focusKey);
}

/**
 * Neglected means a saved date is at least 14 days old, or a task due date is before today.
 * A missing date is not treated as neglect.
 */
export function neglectedReport(world: AgentWorld, now: Date): AgentReply {
  const today = todayKey(now);
  const quietLeads = world.leads.filter((lead) => {
    if (lead.isConverted || !lead.lastContactedAt) return false;
    const key = lead.lastContactedAt.slice(0, 10);
    return /^\d{4}-\d{2}-\d{2}$/.test(key) && daysBetween(key, today) >= NEGLECTED_DAYS;
  });
  const quietDeals = world.deals.filter((deal) => {
    if (!dealOpen(deal) || !deal.lastActivityAtUtc) return false;
    const key = calendarKey(deal.lastActivityAtUtc);
    return !!key && daysBetween(key, today) >= NEGLECTED_DAYS;
  });
  const lateDeals = world.deals.filter((deal) => {
    if (!dealOpen(deal) || !deal.expectedCloseDateUtc) return false;
    const key = calendarKey(deal.expectedCloseDateUtc);
    return !!key && key < today;
  });
  const lateTasks = (world.tasks ?? []).filter((task) => {
    if (task.completed || task.status === 'completed' || task.status === 'cancelled' || !task.dueDateUtc) return false;
    const key = calendarKey(task.dueDateUtc);
    return !!key && key < today;
  });
  if (quietLeads.length + quietDeals.length + lateDeals.length + lateTasks.length === 0) {
    return say(`Nothing loaded meets the rule: last contact or last activity at least ${NEGLECTED_DAYS} days ago, a close date before today, or a task due before today. A blank date is not counted.`);
  }
  const lines = [
    `Criteria: last contact or last deal activity at least ${NEGLECTED_DAYS} days before ${today}, an open deal whose close date is before today, or a task due before today. A blank date is not counted.`,
  ];
  const clip = (label: string, names: string[]) => {
    const shown = names.slice(0, 8);
    const rest = names.length - shown.length;
    lines.push(`${label}: ${shown.join(', ')}${rest > 0 ? `, and ${rest} more` : ''}.`);
  };
  if (quietLeads.length) clip('Leads', quietLeads.map((lead) => lead.name));
  if (quietDeals.length) clip(`Deals quiet for ${NEGLECTED_DAYS} days`, quietDeals.map((deal) => deal.name));
  if (lateDeals.length) clip('Close date already passed', lateDeals.map((deal) => deal.name));
  if (lateTasks.length) clip('Overdue tasks', lateTasks.map((task) => task.title));
  const first = quietLeads[0];
  return say(lines.join('\n'), first ? [openHref(`/leads/${first.id}`, first.name)] : [], first ? `lead:${first.id}` : null);
}

/** Sums only open deals whose saved amount is a plain number. Closed deals are left out. */
export function pipelineTotals(deals: SuggestableDeal[]): string {
  const open = deals.filter(dealOpen);
  if (open.length === 0) return 'Calculated from the loaded deals: no open deal is in the list.';
  const stages = new Map<string, { count: number; sum: number; skipped: number }>();
  for (const deal of open) {
    const stage = deal.dealStageName || deal.stage || 'No stage';
    const row = stages.get(stage) ?? { count: 0, sum: 0, skipped: 0 };
    row.count += 1;
    const amount = amountOf(deal.value);
    if (amount === null) row.skipped += 1;
    else row.sum += amount;
    stages.set(stage, row);
  }
  const lines = ['Calculated from loaded open deals. Closed won and closed lost are excluded. This is not a forecast.'];
  let total = 0;
  let skipped = 0;
  for (const [stage, row] of [...stages.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    total += row.sum;
    skipped += row.skipped;
    const note = row.skipped ? `, ${row.skipped} amount not numeric and left out` : '';
    lines.push(`${stage}: ${row.sum} across ${row.count} deal${row.count === 1 ? '' : 's'}${note}.`);
  }
  lines.push(`Open total of numeric amounts: ${total}.`);
  if (skipped) lines.push(`${skipped} open deal${skipped === 1 ? '' : 's'} had no numeric amount and ${skipped === 1 ? 'is' : 'are'} not in the total.`);
  return lines.join('\n');
}

export function duplicateReview(world: AgentWorld): AgentReply {
  const groups = world.items.filter((item) => item.mergePrimaryId && item.mergeDuplicateIds?.length).slice(0, 5);
  if (groups.length === 0) return say('No duplicate group is in the loaded records. Nothing was merged.');
  return say([
    'Possible duplicates for review. Nothing is merged or deleted.',
    ...groups.map((group) => group.action),
  ].join('\n'), [openHref('/duplicates', 'Duplicates')]);
}

const EMAIL = /[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/i;
const AMOUNT = /\b\d+(?:\.\d{1,2})?\b/;
const DAY = /\b\d{4}-\d{2}-\d{2}\b/;

function cleanName(question: string, email?: string): string {
  return question
    .replace(EMAIL, '')
    .replace(/\b(create|add|new|a|an|the|contact|person|deal|opportunity|named|called|email|for|worth|amount|value)\b/gi, '')
    .replace(email ?? '', '')
    .replace(/[,:]/g, ' ')
    .replace(AMOUNT, '')
    .replace(/\s+/g, ' ')
    .trim();
}

export function proposeContact(world: AgentWorld, question: string, memory?: AgentMemory): AgentReply {
  const email = question.match(EMAIL)?.[0];
  const name = cleanName(question, email);
  if (!name) return say('I need the contact name before I can create one.');
  if (!email) return say(`I need an email for ${name} before I can create the contact.`);
  if (!EMAIL.test(email)) return say(`"${email}" is not a usable email, so the contact was not proposed.`);
  const existing = (world.contacts ?? []).find((contact) => contact.email?.toLowerCase() === email.toLowerCase());
  if (existing) {
    return say(`A contact with ${email} is already loaded: ${existing.name}. I did not propose another.`, [openHref(`/contacts/${existing.id}`, existing.name)]);
  }
  const companyId = memory?.companyId && /^[0-9a-f-]{36}$/i.test(memory.companyId) ? memory.companyId : undefined;
  const company = companyId ? ` It will be linked to the current company.` : '';
  return say(`I can create a contact named ${name} at ${email}.${company} Nothing is saved until you confirm.`, [{
    id: `create-contact:${email.toLowerCase()}`,
    label: `Create ${name}`,
    kind: 'create-contact',
    contactName: name,
    contactEmail: email,
    companyId,
  }]);
}

export function proposeDeal(world: AgentWorld, question: string, memory?: AgentMemory): AgentReply {
  const amountMatch = question.match(AMOUNT);
  const name = cleanName(question);
  if (!name) return say('I need the deal name before I can create one.');
  if (!amountMatch) return say(`I need a numeric amount for ${name}, such as 12000, before I can create the deal.`);
  const value = amountMatch[0];
  const same = world.deals.find((deal) => deal.name.toLowerCase() === name.toLowerCase() && dealOpen(deal));
  if (same) {
    return say(`${same.name} is already an open deal in the loaded list. I did not propose a second one.`, [openHref(`/deals/${same.id}`, same.name)]);
  }
  const companyId = memory?.companyId && /^[0-9a-f-]{36}$/i.test(memory.companyId) ? memory.companyId : undefined;
  const contactId = memory?.contactId && /^[0-9a-f-]{36}$/i.test(memory.contactId) ? memory.contactId : undefined;
  const where = memory?.companyName ? ` for ${memory.companyName}` : '';
  return say(`I can create a deal named ${name}${where} with amount ${value}. Nothing is saved until you confirm.`, [{
    id: `create-deal:${name.toLowerCase()}:${value}`,
    label: `Create ${name}`,
    kind: 'create-deal',
    title: name,
    dealValue: value,
    companyId,
    contactId,
  }]);
}

export function proposeDealUpdate(world: AgentWorld, question: string, person: { key: string; href: string; name: string } | null, memory?: AgentMemory): AgentReply {
  const ctx = party(world, person, memory);
  const deal = ctx.dealId ? world.deals.find((row) => row.id === ctx.dealId) : undefined;
  if (!deal) return say('Name the deal first. I will not change a deal I cannot see in the loaded list.');
  const amount = /\b(amount|value)\b/i.test(question) ? question.match(AMOUNT)?.[0] : undefined;
  const day = /\b(close date|expected close)\b/i.test(question) ? question.match(DAY)?.[0] : undefined;
  if (!amount && !day) return say(`Say the new amount or a close date like 2026-11-15 for ${deal.name}.`);
  const lines = [`Recorded deal: ${deal.name}.`];
  if (amount) lines.push(`Recorded amount: ${deal.value ?? 'none'}. Proposed amount: ${amount}.`);
  if (day) lines.push(`Recorded close date: ${deal.expectedCloseDateUtc?.slice(0, 10) ?? 'none'}. Proposed close date: ${day}.`);
  lines.push('Nothing is saved until you confirm.');
  return say(lines.join('\n'), [{
    id: `update-deal:${deal.id}:${amount ?? ''}:${day ?? ''}`,
    label: `Update ${deal.name}`,
    kind: 'update-deal',
    dealId: deal.id,
    dealValue: amount,
    dueDateUtc: day,
    href: `/deals/${deal.id}`,
  }], `deal:${deal.id}`);
}

export function groundedDraft(world: AgentWorld, item: { key: string; href: string; name: string; action: string; because: string; brief?: { copyTypeId: 'sales-email' | 'follow-up' | 'crm-note' | 'deal-message' | 'workflow-message' } }): AgentAction | null {
  const match = /^\/(leads|deals|contacts)\/([^/]+)$/.exec(item.href);
  if (!match) return null;
  const id = decodeURIComponent(match[2]);
  const lines = [`Name: ${item.name}`, `Saved step: ${item.action}`];
  if (match[1] === 'leads') {
    const lead = world.leads.find((row) => row.id === id);
    if (lead?.companyName) lines.push(`Company: ${lead.companyName}`);
    if (lead?.email) lines.push(`Email: ${lead.email}`);
    if (lead?.phone) lines.push(`Phone: ${lead.phone}`);
    if (lead?.lastContactedAt) lines.push(`Last contact date: ${lead.lastContactedAt.slice(0, 10)}`);
  }
  if (match[1] === 'deals') {
    const deal = world.deals.find((row) => row.id === id);
    if (deal?.companyName) lines.push(`Company: ${deal.companyName}`);
    if (deal?.contactName) lines.push(`Contact: ${deal.contactName}`);
    if (deal?.value) lines.push(`Recorded amount: ${deal.value}`);
    if (deal?.dealStageName || deal?.stage) lines.push(`Stage: ${deal.dealStageName || deal.stage}`);
    if (deal?.expectedCloseDateUtc) lines.push(`Expected close: ${deal.expectedCloseDateUtc.slice(0, 10)}`);
  }
  if (match[1] === 'contacts') {
    const contact = (world.contacts ?? []).find((row) => row.id === id);
    if (contact?.companyName) lines.push(`Company: ${contact.companyName}`);
    if (contact?.email) lines.push(`Email: ${contact.email}`);
    if (contact?.jobTitle) lines.push(`Job title: ${contact.jobTitle}`);
  }
  const latest = (world.activities ?? []).find((activity) =>
    (match[1] === 'leads' && activity.leadId === id)
    || (match[1] === 'deals' && activity.dealId === id)
    || (match[1] === 'contacts' && activity.contactId === id));
  if (latest) lines.push(`Latest activity: ${latest.createdAt.slice(0, 10)} ${latest.type}${latest.subject ? ` ${latest.subject}` : ''}`);
  lines.push('Use only these recorded fields.');
  const ids = match[1] === 'leads' ? { leadId: id } : match[1] === 'deals' ? { dealId: id } : { contactId: id };
  return {
    id: `draft:${item.key}`,
    label: `Draft message for ${item.name}`,
    kind: 'draft',
    href: '/dashboard',
    recipientName: item.name,
    recipientType: match[1] === 'leads' ? 'lead' : match[1] === 'deals' ? 'deal' : 'contact',
    brief: {
      copyTypeId: item.brief?.copyTypeId ?? 'follow-up',
      goal: item.action.replace(/\.$/, '').slice(0, 200),
      context: lines.join('. ').slice(0, 900),
    },
    ...ids,
  };
}
