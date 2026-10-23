import { parsePipeline, dropoutReasonText } from '@/app/pages/leads/leadPipeline';
import type { SuggestableLead } from '../nextAction';
import type { SuggestableDeal } from '../workspace';

export interface LearnedPatterns {
  /** Sentences a person can read. Empty when there is nothing closed to learn from. */
  lines: string[];
  wonDeals: number;
  lostDeals: number;
  lostLeads: number;
}

function tally(counts: Map<string, number>): string | null {
  const ranked = [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  const top = ranked[0];
  if (!top || top[1] === 0) return null;
  const [reason, count] = top;
  return `${reason} (${count})`;
}

/** What finished records already say. Counts only. No forecast and no invented cause. */
export function learnFrom(leads: SuggestableLead[], deals: SuggestableDeal[]): LearnedPatterns {
  const reasons = new Map<string, number>();
  let lostLeads = 0;
  for (const lead of leads) {
    const pipeline = parsePipeline(lead.pipelineState);
    const reason = dropoutReasonText(pipeline);
    if (!reason) continue;
    lostLeads += 1;
    reasons.set(reason, (reasons.get(reason) ?? 0) + 1);
  }

  const lostReasons = new Map<string, number>();
  let wonDeals = 0;
  let lostDeals = 0;
  for (const deal of deals) {
    const stage = deal.dealStageName || deal.stage || '';
    if (deal.isWon === true || stage === 'Closed Won') {
      wonDeals += 1;
      continue;
    }
    if (deal.isWon === false || stage === 'Closed Lost') {
      lostDeals += 1;
      const reason = deal.closedReason?.trim();
      if (reason) lostReasons.set(reason, (lostReasons.get(reason) ?? 0) + 1);
    }
  }

  const lines: string[] = [];
  if (wonDeals || lostDeals) {
    lines.push(`${wonDeals} deal${wonDeals === 1 ? '' : 's'} won, ${lostDeals} lost.`);
    const closed = wonDeals + lostDeals;
    if (closed >= 2) lines.push(`${wonDeals} of ${closed} closed deals were won.`);
  }
  const leadReason = tally(reasons);
  if (leadReason) lines.push(`Leads most often stop for ${leadReason}.`);
  else if (lostLeads === 0) lines.push('No lead has a saved drop-out reason yet.');
  const dealReason = tally(lostReasons);
  if (dealReason) lines.push(`Lost deals most often cite ${dealReason}.`);
  else if (lostDeals > 0) lines.push('Lost deals have no saved reason, so the pattern cannot say why.');

  return { lines, wonDeals, lostDeals, lostLeads };
}
