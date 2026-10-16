// Turns a chosen next step into a brief for the existing sales writer.
//
// The step itself is a rule. The writer only drafts the words, and nothing is sent.

import type { CopyTypeId } from '@/app/api/types';

export interface WriterBrief {
  copyTypeId: CopyTypeId;
  goal: string;
  context: string;
}

export type BriefKind = 'outreach' | 'followup' | 'note' | 'close' | 'proposal';

const SHAPE: Record<BriefKind, Pick<WriterBrief, 'copyTypeId' | 'goal'>> = {
  outreach: { copyTypeId: 'sales-email', goal: 'Schedule a meeting' },
  followup: { copyTypeId: 'follow-up', goal: 'Check in on progress' },
  note: { copyTypeId: 'crm-note', goal: 'Follow up after demo' },
  close: { copyTypeId: 'deal-message', goal: 'Close the deal' },
  proposal: { copyTypeId: 'deal-message', goal: 'Follow up after demo' },
};

export function writerBrief(kind: BriefKind, name: string, action: string, because: string): WriterBrief {
  const context = `${name}. ${action} ${because}`.replace(/\s+/g, ' ').trim().slice(0, 400);
  return { ...SHAPE[kind], context };
}

/** Which deal sentences are a message, and which are data entry. */
export function dealBriefKind(action: string): BriefKind | null {
  if (/^Add an expected close date\.$/.test(action)) return null;
  if (/why this deal was lost/.test(action)) return null;
  if (/proposal/i.test(action)) return 'proposal';
  if (/signature|Closed Won|terms/.test(action)) return 'close';
  if (/fit/.test(action)) return 'outreach';
  if (/touch|Update the expected close date|next stage/.test(action)) return 'followup';
  return null;
}
