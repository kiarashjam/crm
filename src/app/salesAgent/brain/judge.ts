import { daysBetween, todayKey } from '../calendarDay';
import type { SuggestableLead } from '../nextAction';
import type { WorkSuggestion, SuggestableDeal } from '../workspace';
import type { LearnedPatterns } from './patterns';

const QUIET_DAYS = 14;

export interface Weight {
  name: string;
  /** Points added on top of the rule priority. The rule list itself is unchanged. */
  points: number;
  because: string;
}

export interface Judgment {
  item: WorkSuggestion;
  /** Rule priority plus the weights below. */
  score: number;
  weights: Weight[];
}

const UNOWNED = /nobody is assigned/i;
const ALREADY_LATE = /passed|overdue/i;
const MISSING_REASON = /reason is still/i;
const SECOND_CALL = /already called today|once more later today/i;
const LONG_SILENCE = /Nothing has been recorded in (\d+) days/i;
const RECORD_HREF = /^\/(leads|deals)\/([^/]+)$/;
const DAY_KEY = /^\d{4}-\d{2}-\d{2}$/;
/** A same-day retry waits until afternoon. The rule already keeps it under work that is due. */
const SECOND_CALL_HOUR = 15;

function money(value: string | null | undefined): number | null {
  if (!value) return null;
  const amount = Number(String(value).replace(/[^0-9.]/g, ''));
  return Number.isFinite(amount) && amount > 0 ? amount : null;
}

function indexById<T extends { id: string }>(rows: T[]): Map<string, T> {
  const map = new Map<string, T>();
  for (const row of rows) map.set(row.id, row);
  return map;
}

function recordId(href: string, kind: 'leads' | 'deals'): string | null {
  const match = RECORD_HREF.exec(href);
  if (!match || match[1] !== kind) return null;
  try {
    return decodeURIComponent(match[2]);
  } catch {
    return match[2];
  }
}

function weighIndexed(
  item: WorkSuggestion,
  leadById: Map<string, SuggestableLead>,
  dealById: Map<string, SuggestableDeal>,
  now: Date,
): Judgment {
  const weights: Weight[] = [];
  const evidence = `${item.action} ${item.because}`;
  if (UNOWNED.test(evidence)) {
    weights.push({ name: 'No owner', points: 6, because: 'Nobody is assigned, so the step has no person on it.' });
  }
  if (ALREADY_LATE.test(evidence)) {
    weights.push({ name: 'Already late', points: 8, because: 'A saved date is already behind today.' });
  }
  if (MISSING_REASON.test(evidence)) {
    weights.push({ name: 'Missing reason', points: 10, because: 'A closed record with no reason blocks the pattern.' });
  }
  if (SECOND_CALL.test(evidence)) {
    if (now.getHours() < SECOND_CALL_HOUR) {
      weights.push({ name: 'Second call waits', points: -12, because: 'They were already called today, and it is still before 15:00.' });
    } else {
      weights.push({ name: 'Second call window', points: 4, because: 'They were already called earlier today, and it is 15:00 or later.' });
    }
  }
  const silence = LONG_SILENCE.exec(evidence);
  if (silence && Number(silence[1]) >= QUIET_DAYS) {
    weights.push({ name: 'Long silence', points: 5, because: `Nothing has been recorded in ${silence[1]} days.` });
  }

  const leadId = recordId(item.href, 'leads');
  const lead = leadId ? leadById.get(leadId) : undefined;
  if (typeof lead?.leadScore === 'number' && lead.leadScore >= 70) {
    const points = lead.leadScore >= 90 ? 10 : 6;
    weights.push({ name: 'Lead score', points, because: `Lead score is ${lead.leadScore}.` });
  }
  if (lead?.lastContactedAt) {
    const key = lead.lastContactedAt.slice(0, 10);
    if (DAY_KEY.test(key) && daysBetween(key, todayKey(now)) >= QUIET_DAYS) {
      weights.push({ name: 'Gone quiet', points: 5, because: `Last contact on the lead is ${key}.` });
    }
  }

  const dealId = recordId(item.href, 'deals');
  const deal = dealId ? dealById.get(dealId) : undefined;
  const amount = money(deal?.value);
  if (amount !== null && amount >= 10000) {
    let points = amount >= 40000 ? 22 : 12;
    const probability = deal?.probability;
    if (typeof probability === 'number' && probability < 40) {
      points = Math.max(6, points - 8);
      weights.push({
        name: 'Larger deal',
        points,
        because: `The deal amount is ${deal?.value}, and saved probability is ${probability}%, so the amount counts for less.`,
      });
    } else {
      weights.push({ name: 'Larger deal', points, because: `The deal amount is ${deal?.value}.` });
    }
  }
  if (typeof deal?.probability === 'number' && deal.probability >= 70) {
    weights.push({ name: 'High probability', points: 4, because: `Saved probability is ${deal.probability}%.` });
  }

  const extra = weights.reduce((sum, weight) => sum + weight.points, 0);
  return { item, score: item.priority + extra, weights };
}

/** Extra weight on top of the rule priority. Every point names the field it came from. */
export function weigh(item: WorkSuggestion, leads: SuggestableLead[], deals: SuggestableDeal[], now: Date): Judgment {
  return weighIndexed(item, indexById(leads), indexById(deals), now);
}

export function judgeAll(items: WorkSuggestion[], leads: SuggestableLead[], deals: SuggestableDeal[], now: Date): Judgment[] {
  const leadById = indexById(leads);
  const dealById = indexById(deals);
  return items
    .map((item) => weighIndexed(item, leadById, dealById, now))
    .sort((a, b) => b.score - a.score || a.item.name.localeCompare(b.item.name));
}

/**
 * The rule list and the weighted judgment, written out when they disagree.
 * Does not invent a fact that is not on a weight.
 */
export function judgmentText(items: WorkSuggestion[], leads: SuggestableLead[], deals: SuggestableDeal[], patterns: LearnedPatterns, now: Date): { text: string; pick: WorkSuggestion | null } {
  if (items.length === 0) return { text: 'Nothing is waiting.', pick: null };
  const ranked = judgeAll(items, leads, deals, now);
  const best = ranked[0];
  const ruleFirst = [...items].sort((a, b) => b.priority - a.priority || a.name.localeCompare(b.name))[0];
  const lines = [
    `Rule order starts with ${ruleFirst.name} at priority ${ruleFirst.priority}. ${ruleFirst.action}`,
  ];
  if (best.item.key !== ruleFirst.key) {
    const why = best.weights.map((weight) => `${weight.name}: ${weight.because}`).join(' ');
    lines.push(`Weighing the saved fields, ${best.item.name} scores ${best.score} against ${ruleFirst.name} at ${weigh(ruleFirst, leads, deals, now).score}. ${why}`);
    lines.push(`Clear ${ruleFirst.name} if the hole in the record matters more. Work ${best.item.name} if the weight above matters more.`);
  } else if (best.weights.length > 0) {
    lines.push(best.weights.map((weight) => `${weight.name}: ${weight.because}`).join(' '));
  }
  const pattern = patterns.lines.find((line) => line.startsWith('Leads most often'));
  if (pattern) lines.push(pattern);
  return { text: lines.join('\n'), pick: best.item };
}
