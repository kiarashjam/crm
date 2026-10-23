// One-press questions for the ask box.
//
// The starters fit the page: on a record they are about that record, elsewhere about
// the queue. After an answer about a record, the follow-ups name it, so pressing one
// asks about that record and not whatever is on screen.

import type { AgentWorld } from './brain/answer';
import type { AgentMemory } from './brain/reason';

function onRecord(world: AgentWorld): { href: string; kind: 'leads' | 'deals' | 'contacts' | 'other' } | null {
  if (world.page.mode !== 'record') return null;
  const href = world.page.href;
  const kind = /^\/(leads|deals|contacts)\//.exec(href)?.[1] as 'leads' | 'deals' | 'contacts' | undefined;
  return { href, kind: kind ?? 'other' };
}

/** Questions to start with, before anything has been asked. */
export function starterSuggestions(world: AgentWorld): string[] {
  const record = onRecord(world);
  if (record && record.kind !== 'other') {
    return ['Brief this', 'What should I do next?', 'Draft a message', 'Recent activity', 'Add a task for tomorrow'];
  }
  const list = ['What should I do next?'];
  if (world.items.some((item) => /\b(call|phone)\b/i.test(item.action))) list.push('Who should I call?');
  list.push('Who went quiet?');
  if (world.deals.length > 0) list.push('Pipeline value');
  if (world.items.length > 1) list.push('Why this order?');
  if (world.items.some((item) => item.mergePrimaryId)) list.push('Any duplicates?');
  return list.slice(0, 5);
}

/** Questions to ask next, about the record the last answer was about when there is one. */
export function followUpSuggestions(world: AgentWorld, memory: AgentMemory, lastQuestion: string | null): string[] {
  const name = memory.name?.trim();
  const aboutRecord = !!name && !!(memory.leadId || memory.dealId || memory.contactId);
  const list = aboutRecord
    ? [`Brief ${name}`, `Draft a message to ${name}`, `Recent activity for ${name}`, `Add a task for ${name} tomorrow`]
    : ['What should I do next?', 'Who went quiet?', ...(world.deals.length > 0 ? ['Pipeline value'] : [])];
  const asked = lastQuestion?.trim().toLowerCase();
  return list.filter((text) => text.toLowerCase() !== asked).slice(0, 4);
}
