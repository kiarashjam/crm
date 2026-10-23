import { failurePointFor, isPipelineComplete, parsePipeline } from '@/app/pages/leads/leadPipeline';
import { currencyOf, formatMoney, tryParseAmount } from '@/app/lib/money';
import { calendarKey, daysBetween, todayKey } from '../calendarDay';
import type { SuggestableLead } from '../nextAction';
import { dealOutcome, type SuggestableDeal, type SuggestableTask } from '../workspace';
import type { AgentAction, AgentActivity, AgentReply, AgentWorld } from './answer';
import type { AgentMemory } from './reason';
import { emptyMemory, remember } from './reason';

const NEGLECTED_DAYS = 14;

function say(text: string, actions: AgentAction[] = [], focusKey: string | null = null, memory?: AgentMemory): AgentReply {
  return memory ? { text, actions, focusKey, memory } : { text, actions, focusKey };
}

/** The calendar day of a saved date, in this browser's time zone, as the lists show it. */
export function dayOf(iso: string): string {
  return calendarKey(iso) ?? iso.slice(0, 10);
}

/** An amount as the deal's own currency writes it, or the saved text when it is not a number. */
export function amountText(deal: SuggestableDeal): string {
  const amount = tryParseAmount(deal.value);
  if (amount === null) return deal.value?.trim() || 'no amount saved';
  return formatMoney(amount, currencyOf({ currency: deal.currency ?? undefined }));
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
  return memory ? { ...emptyMemory(), ...memory } : emptyMemory();
}

/** Still open: not won, not lost. One rule for every answer that counts deals. */
export function isOpenDeal(deal: SuggestableDeal): boolean {
  return dealOutcome(deal) === 'open';
}

/** A lead whose work is over: converted, dropped out, or through every phase. */
function leadFinished(lead: SuggestableLead): boolean {
  if (lead.isConverted) return true;
  const pipeline = parsePipeline(lead.pipelineState);
  return failurePointFor(pipeline) !== null || isPipelineComplete(pipeline);
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

/** Activities are not part of the lists this panel loads. Saying "none" would be a guess. */
function activitiesMissing(world: AgentWorld): string | null {
  if (world.activities === undefined) return 'Activities are not loaded in this panel. The record page shows its timeline.';
  if (world.gaps?.some((gap) => gap.startsWith('Activities could not be loaded'))) return 'Activities could not be loaded.';
  return null;
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
    if (lead.lastContactedAt) lines.push(`Last contact date: ${dayOf(lead.lastContactedAt)}.`);
    if (typeof lead.leadScore === 'number') lines.push(`Lead score: ${lead.leadScore}.`);
  }
  if (contact) {
    lines.push(`Contact ${contact.name}.`);
    if (contact.email) lines.push(`Email: ${contact.email}.`);
    if (contact.jobTitle) lines.push(`Job title: ${contact.jobTitle}.`);
    if (contact.doNotContact) lines.push('Do not contact is saved.');
  }
  const related = world.deals.filter((row) => isOpenDeal(row) && (
    (companyName && row.companyName?.toLowerCase() === companyName.toLowerCase())
    || (ctx.companyId && row.companyId === ctx.companyId)
    || (ctx.contactId && row.contactId === ctx.contactId)
    || row.id === ctx.dealId
  ));
  if (related.length === 0) lines.push('No open deal for this record is in the loaded list.');
  const shownDeals = related.slice(0, 5);
  for (const row of shownDeals) {
    lines.push(`Deal ${row.name}: ${amountText(row)} · ${row.dealStageName || row.stage || 'no stage'}.`);
  }
  if (related.length > shownDeals.length) lines.push(`${related.length - shownDeals.length} more open deals are loaded and not listed.`);
  const missing = activitiesMissing(world);
  if (missing) {
    lines.push(missing);
  } else {
    const activityAll = linkedActivities(world, ctx);
    const activity = activityAll.slice(0, 5);
    if (activity.length === 0) {
      lines.push('No activity linked to this record is in the loaded list.');
    } else {
      lines.push('Recent activity:');
      for (const row of activity) lines.push(`${dayOf(row.createdAt)} ${row.type}${row.subject ? `: ${row.subject}` : ''}.`);
      if (activityAll.length > activity.length) lines.push(`${activityAll.length - activity.length} more activities are loaded and not listed.`);
    }
  }
  const tasks = linkedTasks(world, ctx);
  if (tasks.length === 0) lines.push('No open task is linked to this record.');
  else {
    lines.push('Outstanding tasks:');
    for (const task of tasks.slice(0, 5)) lines.push(`${task.title}${task.dueDateUtc ? `, due ${dayOf(task.dueDateUtc)}` : ', no due date saved'}.`);
  }
  const step = person?.action
    ? person
    : world.items.find((item) => item.key === ctx.focusKey)
      ?? world.items.find((item) => (deal && item.href === `/deals/${deal.id}`) || (lead && item.href === `/leads/${lead.id}`) || (contact && item.href === `/contacts/${contact.id}`))
      ?? null;
  if (step) lines.push(`Suggestion, from the saved step: ${step.action}`);
  const gap = limitLine(world);
  if (gap) lines.push(gap);
  const href = step?.href ?? (deal ? `/deals/${deal.id}` : lead ? `/leads/${lead.id}` : contact ? `/contacts/${contact.id}` : null);
  const name = step?.name ?? deal?.name ?? lead?.name ?? contact?.name ?? companyName ?? 'Record';
  // The next question about "them" is about this record, even when it has no open step.
  const focusKey = step?.key ?? ctx.focusKey;
  return say(lines.join('\n'), href ? [openHref(href, name)] : [], focusKey, { ...ctx, focusKey });
}

/** Latest activity and open tasks. Dates come from the loaded records. */
export function activityReport(world: AgentWorld, person: { key: string; href: string; name: string } | null, memory?: AgentMemory): AgentReply {
  const ctx = party(world, person, memory);
  const linked = !!(ctx.leadId || ctx.dealId || ctx.contactId);
  const who = ctx.name || ctx.companyName || 'the loaded records';
  const lines = [`Recent activity for ${who}.`];
  const missing = activitiesMissing(world);
  if (missing) {
    lines.push(missing);
  } else {
    const activityAll = linked
      ? linkedActivities(world, ctx)
      : (world.activities ?? []).slice().sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    const activity = activityAll.slice(0, 5);
    if (activity.length === 0) lines.push('No activity is in the loaded list.');
    else {
      for (const row of activity) lines.push(`${dayOf(row.createdAt)} ${row.type}${row.subject ? `: ${row.subject}` : ''} · ${row.name}.`);
      if (activityAll.length > activity.length) lines.push(`${activityAll.length - activity.length} more activities are loaded and not listed.`);
    }
  }
  const taskAll = linked
    ? linkedTasks(world, ctx)
    : (world.tasks ?? []).filter((task) => !task.completed && task.status !== 'completed' && task.status !== 'cancelled');
  const tasks = taskAll.slice(0, 5);
  if (tasks.length === 0) lines.push('No open task is linked.');
  else {
    lines.push('Outstanding tasks:');
    for (const task of tasks) lines.push(`${task.title}${task.dueDateUtc ? `, due ${dayOf(task.dueDateUtc)}` : ''}.`);
    if (taskAll.length > tasks.length) lines.push(`${taskAll.length - tasks.length} more open tasks are loaded and not listed.`);
  }
  return say(lines.join('\n'), person ? [openHref(person.href, person.name)] : [], person?.key ?? ctx.focusKey);
}

/**
 * Neglected means a saved date is at least 14 days old, or a task due date is before today.
 * A missing date is not treated as neglect, and a lead whose work is over is not quiet.
 */
export function neglectedReport(world: AgentWorld, now: Date): AgentReply {
  const today = todayKey(now);
  const quietLeads = world.leads.filter((lead) => {
    if (leadFinished(lead)) return false;
    const key = calendarKey(lead.lastContactedAt);
    return !!key && daysBetween(key, today) >= NEGLECTED_DAYS;
  });
  const quietDeals = world.deals.filter((deal) => {
    if (!isOpenDeal(deal)) return false;
    const key = calendarKey(deal.lastActivityAtUtc);
    return !!key && daysBetween(key, today) >= NEGLECTED_DAYS;
  });
  const lateDeals = world.deals.filter((deal) => {
    if (!isOpenDeal(deal)) return false;
    const key = calendarKey(deal.expectedCloseDateUtc);
    return !!key && key < today;
  });
  const lateTasks = (world.tasks ?? []).filter((task) => {
    if (task.completed || task.status === 'completed' || task.status === 'cancelled') return false;
    const key = calendarKey(task.dueDateUtc);
    return !!key && key < today;
  });
  if (quietLeads.length + quietDeals.length + lateDeals.length + lateTasks.length === 0) {
    return say(`Nothing loaded meets the rule: last contact or last activity at least ${NEGLECTED_DAYS} days ago, a close date before today, or a task due before today. A blank date is not counted.`);
  }
  const lines = [
    `Criteria: last contact or last deal activity at least ${NEGLECTED_DAYS} days before ${today}, an open deal whose close date is before today, or a task due before today. A blank date is not counted, and converted or dropped leads are left out.`,
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
  const gap = limitLine(world);
  if (gap) lines.push(gap);
  const first = quietLeads[0];
  return say(lines.join('\n'), first ? [openHref(`/leads/${first.id}`, first.name)] : [], first ? `lead:${first.id}` : null);
}

function names(deals: SuggestableDeal[]): string {
  const shown = deals.slice(0, 3).map((deal) => deal.name);
  return deals.length > shown.length ? `${shown.join(', ')}, and ${deals.length - shown.length} more` : shown.join(', ');
}

/**
 * Sums open deals per currency. Closed deals, amounts that are not a number, and
 * negative amounts are left out and named. Nothing is converted between currencies.
 */
export function pipelineTotals(deals: SuggestableDeal[]): string {
  const open = deals.filter(isOpenDeal);
  if (open.length === 0) return 'Calculated from the loaded deals: no open deal is in the list.';
  const totals = new Map<string, { sum: number; stages: Map<string, { count: number; sum: number }> }>();
  const unreadable: SuggestableDeal[] = [];
  const negative: SuggestableDeal[] = [];
  for (const deal of open) {
    const amount = tryParseAmount(deal.value);
    if (amount === null) {
      unreadable.push(deal);
      continue;
    }
    if (amount < 0) {
      negative.push(deal);
      continue;
    }
    const currency = currencyOf({ currency: deal.currency ?? undefined });
    const total = totals.get(currency) ?? { sum: 0, stages: new Map() };
    const stage = deal.dealStageName || deal.stage || 'No stage';
    const row = total.stages.get(stage) ?? { count: 0, sum: 0 };
    row.count += 1;
    row.sum += amount;
    total.stages.set(stage, row);
    total.sum += amount;
    totals.set(currency, total);
  }
  const lines = ['Calculated from loaded open deals. Closed won and closed lost are excluded. This is not a forecast.'];
  const currencies = [...totals.entries()].sort((a, b) => b[1].sum - a[1].sum);
  const several = currencies.length > 1;
  for (const [currency, total] of currencies) {
    for (const [stage, row] of [...total.stages.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
      lines.push(`${several ? `${currency} · ` : ''}${stage}: ${formatMoney(row.sum, currency)} across ${row.count} deal${row.count === 1 ? '' : 's'}.`);
    }
  }
  for (const [currency, total] of currencies) {
    lines.push(`Open total${several ? ` in ${currency}` : ''}: ${formatMoney(total.sum, currency)}.`);
  }
  if (several) lines.push('Each currency is totalled on its own. Nothing is converted.');
  if (unreadable.length) {
    lines.push(`${unreadable.length} open deal${unreadable.length === 1 ? ' has' : 's have'} no numeric amount and ${unreadable.length === 1 ? 'is' : 'are'} not in the total: ${names(unreadable)}.`);
  }
  if (negative.length) {
    lines.push(`${negative.length} open deal${negative.length === 1 ? ' has' : 's have'} a negative amount, which looks like a typing mistake, and ${negative.length === 1 ? 'is' : 'are'} not in the total: ${names(negative)}.`);
  }
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
const DAY = /\b\d{4}-\d{2}-\d{2}\b/;

const CURRENCY_WORDS: Record<string, string> = {
  usd: 'USD', eur: 'EUR', gbp: 'GBP', chf: 'CHF', cad: 'CAD', aud: 'AUD', jpy: 'JPY', sek: 'SEK', nok: 'NOK', dkk: 'DKK',
  pln: 'PLN', czk: 'CZK', inr: 'INR', brl: 'BRL', mxn: 'MXN', zar: 'ZAR', sgd: 'SGD', hkd: 'HKD', nzd: 'NZD', aed: 'AED',
  dollars: 'USD', euros: 'EUR', euro: 'EUR', pounds: 'GBP', francs: 'CHF', fr: 'CHF',
  $: 'USD', '€': 'EUR', '£': 'GBP', '¥': 'JPY',
};
const CURRENCY = String.raw`(?:[$€£¥]|\b(?:usd|eur|gbp|chf|cad|aud|jpy|sek|nok|dkk|pln|czk|inr|brl|mxn|zar|sgd|hkd|nzd|aed|dollars|euros?|pounds|francs|fr)\b\.?)`;
const STRONG = /\b(amount|worth|value|valued at|budget|price|priced at|to)\s*$/i;
const WEAK = /\b(for|at|of|is)\s*$/i;
const NUMBER_AT = new RegExp(String.raw`(${CURRENCY}\s?)?(\d[\d,.'’ ]*\d|\d)(\s?[km]\b)?(\s?${CURRENCY})?`, 'gi');

export interface ParsedAmount {
  /** A plain number, as the deal's amount field stores it. */
  value: string;
  currency?: string;
  /** The words that said it, so they can be taken out of a name. */
  text: string;
}

function currencyCode(marker: string | undefined): string | undefined {
  const key = marker?.trim().toLowerCase().replace(/\.$/, '');
  return key ? CURRENCY_WORDS[key] : undefined;
}

/**
 * The amount a question gives, when it says which number is the amount: after
 * "worth", "amount", "value", "to", "for", or next to a currency. A date, a year, or a
 * number inside a name is not an amount. "15,000", "15'000", "15k", and "CHF 15 000"
 * all read as fifteen thousand.
 */
export function amountIn(question: string): ParsedAmount | null {
  let best: { score: number; parsed: ParsedAmount } | null = null;
  for (const match of question.matchAll(NUMBER_AT)) {
    const [text, before, digits, multiplier, after] = match;
    const start = match.index ?? 0;
    const end = start + text.length;
    if (!digits) continue;
    // Part of a date or a time: 2026-11-15, 10:30.
    if (/[-/:]\d/.test(question.slice(end, end + 2)) || /\d[-/:]$/.test(question.slice(Math.max(0, start - 2), start))) continue;
    const lead = question.slice(Math.max(0, start - 24), start);
    const currency = currencyCode(before) ?? currencyCode(after);
    const score = currency ? 3 : STRONG.test(lead) ? 2 : WEAK.test(lead) ? 1 : 0;
    if (score === 0) continue;
    // A bare year after "for" or "of" names a period, not an amount.
    if (score === 1 && /^(19|20)\d\d$/.test(digits)) continue;
    const amount = tryParseAmount(digits.replace(/[\s'’]/g, ''));
    if (amount === null) continue;
    const scale = multiplier?.trim().toLowerCase() === 'k' ? 1_000 : multiplier?.trim().toLowerCase() === 'm' ? 1_000_000 : 1;
    const value = String(Math.round(amount * scale * 100) / 100);
    const keyword = (STRONG.exec(lead) ?? WEAK.exec(lead))?.[0] ?? '';
    const parsed = { value, currency, text: `${keyword}${text}`.trim() };
    if (!best || score > best.score) best = { score, parsed };
  }
  return best?.parsed ?? null;
}

/** The name in "create a deal called Harbor for 15000", with the amount and email taken out. */
function nameFrom(question: string, remove: (string | undefined)[]): string {
  let text = question;
  for (const part of remove) if (part) text = text.replace(part, ' ');
  const called = /\b(?:called|named)\s+(.+)$/i.exec(text);
  const rest = called?.[1]
    ?? text.replace(/^\s*(?:please\s+)?(?:create|add|make|new)\s+(?:an?\s+|the\s+)?(?:new\s+)?(?:deal|opportunity|contact|person)\b\s*(?:for|with)?\s*/i, '');
  return rest.replace(/[,:;]+/g, ' ').replace(/\s+/g, ' ').trim().replace(/[.?!]+$/, '');
}

export function proposeContact(world: AgentWorld, question: string, memory?: AgentMemory): AgentReply {
  const email = question.match(EMAIL)?.[0];
  const name = nameFrom(question, [email]);
  if (!name) return say('I need the contact name before I can create one.');
  if (!email) return say(`I need an email for ${name} before I can create the contact.`);
  const existing = (world.contacts ?? []).find((contact) => contact.email?.toLowerCase() === email.toLowerCase());
  if (existing) {
    return say(`A contact with ${email} is already loaded: ${existing.name}. I did not propose another.`, [openHref(`/contacts/${existing.id}`, existing.name)]);
  }
  const companyId = memory?.companyId && /^[0-9a-f-]{36}$/i.test(memory.companyId) ? memory.companyId : undefined;
  const company = companyId ? ` It will be linked to ${memory?.companyName || 'the current company'}.` : '';
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
  const amount = amountIn(question);
  const name = nameFrom(question, [amount?.text]);
  if (!name) return say('I need the deal name before I can create one.');
  if (!amount) return say(`I need a numeric amount for ${name}, such as "worth 12000", before I can create the deal.`);
  if (Number(amount.value) <= 0) return say(`The amount for ${name} has to be above zero. Nothing was proposed.`);
  const same = world.deals.find((deal) => deal.name.toLowerCase() === name.toLowerCase() && isOpenDeal(deal));
  if (same) {
    return say(`${same.name} is already an open deal in the loaded list. I did not propose a second one.`, [openHref(`/deals/${same.id}`, same.name)]);
  }
  const companyId = memory?.companyId && /^[0-9a-f-]{36}$/i.test(memory.companyId) ? memory.companyId : undefined;
  const contactId = memory?.contactId && /^[0-9a-f-]{36}$/i.test(memory.contactId) ? memory.contactId : undefined;
  const where = companyId && memory?.companyName ? ` for ${memory.companyName}` : '';
  const shown = amount.currency ? formatMoney(Number(amount.value), amount.currency) : `${amount.value}, in the workspace's default currency`;
  return say(`I can create a deal named ${name}${where} with amount ${shown}. Nothing is saved until you confirm.`, [{
    id: `create-deal:${name.toLowerCase()}:${amount.value}:${amount.currency ?? ''}`,
    label: `Create ${name}`,
    kind: 'create-deal',
    title: name,
    dealValue: amount.value,
    currency: amount.currency,
    companyId,
    contactId,
  }]);
}

export function proposeDealUpdate(world: AgentWorld, question: string, person: { key: string; href: string; name: string } | null, memory?: AgentMemory): AgentReply {
  const ctx = party(world, person, memory);
  const deal = ctx.dealId ? world.deals.find((row) => row.id === ctx.dealId) : undefined;
  if (!deal) return say('Name the deal first. I will not change a deal I cannot see in the loaded list.');
  const amount = /\b(amount|value|worth)\b/i.test(question) ? amountIn(question) : null;
  const day = /\b(close date|expected close)\b/i.test(question) ? question.match(DAY)?.[0] : undefined;
  if (!amount && !day) return say(`Say the new amount, like "set the amount to 15000", or a close date like 2026-11-15 for ${deal.name}.`);
  if (amount && Number(amount.value) <= 0) return say(`The amount for ${deal.name} has to be above zero. Nothing was proposed.`);
  const dealCurrency = currencyOf({ currency: deal.currency ?? undefined });
  if (amount?.currency && amount.currency !== dealCurrency) {
    return say(`${deal.name} is in ${dealCurrency}, and the new amount was given in ${amount.currency}. Change the currency on the deal itself. Nothing was proposed.`, [openHref(`/deals/${deal.id}`, deal.name)], `deal:${deal.id}`);
  }
  const lines = [`Recorded deal: ${deal.name}.`];
  if (amount) lines.push(`Recorded amount: ${amountText(deal)}. Proposed amount: ${formatMoney(Number(amount.value), dealCurrency)}.`);
  if (day) lines.push(`Recorded close date: ${deal.expectedCloseDateUtc ? dayOf(deal.expectedCloseDateUtc) : 'none'}. Proposed close date: ${day}.`);
  lines.push('Nothing is saved until you confirm.');
  return say(lines.join('\n'), [{
    id: `update-deal:${deal.id}:${amount?.value ?? ''}:${day ?? ''}`,
    label: `Update ${deal.name}`,
    kind: 'update-deal',
    dealId: deal.id,
    dealValue: amount?.value,
    dueDateUtc: day,
    href: `/deals/${deal.id}`,
  }], `deal:${deal.id}`);
}

export function groundedDraft(world: AgentWorld, item: { key: string; href: string; name: string; action: string; because: string; brief?: { copyTypeId: 'sales-email' | 'follow-up' | 'crm-note' | 'deal-message' | 'workflow-message' } }): AgentAction | null {
  const match = /^\/(leads|deals|contacts)\/([^/]+)$/.exec(item.href);
  if (!match) return null;
  const id = decodeURIComponent(match[2] ?? '');
  const goal = item.action || `Follow up with ${item.name}.`;
  const lines = [`Name: ${item.name}`];
  if (item.action) lines.push(`Saved step: ${item.action}`);
  if (match[1] === 'leads') {
    const lead = world.leads.find((row) => row.id === id);
    if (lead?.companyName) lines.push(`Company: ${lead.companyName}`);
    if (lead?.email) lines.push(`Email: ${lead.email}`);
    if (lead?.phone) lines.push(`Phone: ${lead.phone}`);
    if (lead?.lastContactedAt) lines.push(`Last contact date: ${dayOf(lead.lastContactedAt)}`);
  }
  if (match[1] === 'deals') {
    const deal = world.deals.find((row) => row.id === id);
    if (deal?.companyName) lines.push(`Company: ${deal.companyName}`);
    if (deal?.contactName) lines.push(`Contact: ${deal.contactName}`);
    if (deal?.value) lines.push(`Recorded amount: ${amountText(deal)}`);
    if (deal?.dealStageName || deal?.stage) lines.push(`Stage: ${deal.dealStageName || deal.stage}`);
    if (deal?.expectedCloseDateUtc) lines.push(`Expected close: ${dayOf(deal.expectedCloseDateUtc)}`);
  }
  if (match[1] === 'contacts') {
    const contact = (world.contacts ?? []).find((row) => row.id === id);
    if (contact?.companyName) lines.push(`Company: ${contact.companyName}`);
    if (contact?.email) lines.push(`Email: ${contact.email}`);
    if (contact?.jobTitle) lines.push(`Job title: ${contact.jobTitle}`);
  }
  const latest = (world.activities ?? [])
    .filter((activity) =>
      (match[1] === 'leads' && activity.leadId === id)
      || (match[1] === 'deals' && activity.dealId === id)
      || (match[1] === 'contacts' && activity.contactId === id))
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];
  if (latest) lines.push(`Latest activity: ${dayOf(latest.createdAt)} ${latest.type}${latest.subject ? ` ${latest.subject}` : ''}`);
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
      goal: goal.replace(/\.$/, '').slice(0, 200),
      context: lines.join('. ').slice(0, 900),
    },
    ...ids,
  };
}
