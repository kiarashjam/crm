import { describe, expect, it } from 'vitest';
import { askAgent, type AgentWorld } from './answer';
import { learnFrom } from './patterns';
import type { WorkSuggestion } from '../workspace';

const item: WorkSuggestion = {
  key: 'lead:l1',
  href: '/leads/l1',
  name: 'Ada Lovelace',
  list: 'Outreach',
  action: 'Send the first note.',
  because: 'Outreach has not started.',
  priority: 80,
  guide: 'start_outreach',
};

function world(extra: Partial<AgentWorld> = {}): AgentWorld {
  return {
    items: [item],
    leads: [{ id: 'l1', name: 'Ada Lovelace', email: 'ada@example.com', lastContactedAt: '2020-01-01' }],
    deals: [],
    plan: null,
    page: { mode: 'focus' },
    patterns: learnFrom([], []),
    invites: [],
    contractHint: null,
    ...extra,
  };
}

describe('askAgent', () => {
  it('names the next step and offers a task that is not created yet', () => {
    const reply = askAgent('what should I do next?', world());
    expect(reply.text).toContain('Ada Lovelace');
    expect(reply.text).toContain('Send the first note.');
    expect(reply.actions.map((action) => action.kind)).toContain('task');
    expect(reply.actions.find((action) => action.kind === 'task')?.leadId).toBe('l1');
  });

  it('refuses to invent a person who is not loaded', () => {
    const reply = askAgent('draft a note for Grace Hopper', world());
    expect(reply.text).toMatch(/could not match|Name the lead/i);
    expect(reply.actions.every((action) => !action.body?.includes('Grace'))).toBe(true);
  });

  it('explains the playbook for a named step', () => {
    const reply = askAgent('how do I handle Ada Lovelace', world());
    expect(reply.text).toContain('How:');
    expect(reply.text).toContain('Avoid:');
  });

  it('reports who went quiet from the saved contact date', () => {
    const reply = askAgent('who went quiet?', world(), new Date('2020-02-01T00:00:00Z'));
    expect(reply.text).toContain('Ada Lovelace');
  });

  it('says when there is nothing to learn yet', () => {
    const reply = askAgent('what did we learn?', world());
    expect(reply.text).toContain('No lead has a saved drop-out reason yet.');
  });

  it('explains why the first row outranks the second', () => {
    const second: WorkSuggestion = { ...item, key: 'deal:d1', href: '/deals/d1', name: 'Northwind', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72, guide: 'Proposal' };
    const reply = askAgent('why this order?', world({ items: [item, second] }));
    expect(reply.text).toContain('Ada Lovelace');
    expect(reply.text).toContain('Northwind');
    expect(reply.text).toContain('8 lower');
  });

  it('uses the last record when the next question says that one', () => {
    const reply = askAgent('draft that one', world(), new Date(), { focusKey: 'lead:l1' });
    expect(reply.text).toContain('Ada Lovelace');
    expect(reply.actions[0]?.kind).toBe('draft');
  });

  it('weighs a large deal above a higher rule priority and says so', () => {
    const small: WorkSuggestion = { ...item, priority: 90 };
    const rich: WorkSuggestion = { ...item, key: 'deal:d9', href: '/deals/d9', name: 'Pavilion', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72, guide: 'Proposal' };
    const reply = askAgent('what should I do?', world({
      items: [small, rich],
      deals: [{ id: 'd9', name: 'Pavilion', value: '48000', probability: 80 }],
    }));
    expect(reply.text).toContain('Pavilion');
    expect(reply.text).toContain('48');
    expect(reply.focusKey).toBe('deal:d9');
  });

  it('offers a send that is not performed yet', () => {
    const reply = askAgent('send it', world({ items: [{ ...item, email: 'ada@example.com' }] }));
    expect(reply.actions.find((action) => action.kind === 'send')?.to).toBe('ada@example.com');
    expect(reply.text).toMatch(/only after you confirm/i);
  });

  it('lists other deals at the same company and does not invent a club from remember', () => {
    const current: WorkSuggestion = { ...item, key: 'deal:d1', href: '/deals/d1', name: 'Pavilion renewal', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72 };
    const reply = askAgent('what about their other opportunities?', world({
      items: [current],
      deals: [
        { id: 'd1', name: 'Pavilion renewal', companyId: 'c1', companyName: 'Pavilion', value: '10000', stage: 'Proposal' },
        { id: 'd2', name: 'Pavilion expansion', companyId: 'c1', companyName: 'Pavilion', value: '48000', stage: 'Qualification' },
      ],
    }), new Date(), {
      focusKey: 'deal:d1',
      name: 'Pavilion renewal',
      leadId: null,
      dealId: 'd1',
      contactId: null,
      companyId: 'c1',
      companyName: 'Pavilion',
    });
    expect(reply.text).toContain('Recorded deals');
    expect(reply.text).toContain('Pavilion expansion');
    expect(reply.text).not.toContain('Pavilion renewal:');
    expect(reply.actions.some((action) => action.href === '/deals/d2')).toBe(true);

    const remembered = askAgent('remember Ada Lovelace', world());
    expect(remembered.text).toContain('Ada Lovelace');
    expect(remembered.text).not.toMatch(/club record/i);
  });

  it('asks which record when two names match and counts only loaded deals', () => {
    const counted = askAgent('how many deals?', world({
      deals: [{ id: 'd1', name: 'A' }, { id: 'd2', name: 'B' }],
      gaps: ['Contracts were read for 40 of 80 open leads.'],
    }));
    expect(counted.text).toContain('Calculated from the records loaded here: 2 deals.');
    expect(counted.text).toContain('Not loaded:');

    const found = askAgent('search for Pavilion', world({
      deals: [{ id: 'd9', name: 'Expansion', companyName: 'Pavilion', value: '10', stage: 'Proposal' }],
    }));
    expect(found.text).toContain('Recorded matches');
    expect(found.text).toContain('Expansion');
    expect(found.actions[0]?.href).toBe('/deals/d9');
  });

  it('briefs a deal from recorded fields and drafts only those fields', () => {
    const deal: WorkSuggestion = { ...item, key: 'deal:d1', href: '/deals/d1', name: 'Pavilion renewal', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72 };
    const brief = askAgent('brief this', world({
      items: [deal],
      deals: [{ id: 'd1', name: 'Pavilion renewal', companyName: 'Pavilion', value: '48000', stage: 'Proposal', expectedCloseDateUtc: '2026-11-01' }],
      activities: [{ id: 'a1', type: 'call', subject: 'Intro call', dealId: 'd1', name: 'Pavilion renewal', createdAt: '2026-10-01' }],
      tasks: [{ id: 't1', title: 'Send pricing', status: 'todo', dueDateUtc: '2026-10-20', dealId: 'd1' }],
    }), new Date('2026-10-22T00:00:00Z'), {
      focusKey: 'deal:d1', name: 'Pavilion renewal', leadId: null, dealId: 'd1', contactId: null, companyId: null, companyName: 'Pavilion',
    });
    expect(brief.text).toContain('Recorded fields only');
    expect(brief.text).toContain('48000');
    expect(brief.text).toContain('Intro call');
    expect(brief.text).toContain('Send pricing');
    expect(brief.text).toContain('Suggestion');

    const draft = askAgent('draft that', world({
      items: [deal],
      deals: [{ id: 'd1', name: 'Pavilion renewal', companyName: 'Pavilion', value: '48000', stage: 'Proposal' }],
    }), new Date(), { focusKey: 'deal:d1', name: 'Pavilion renewal', leadId: null, dealId: 'd1', contactId: null, companyId: null, companyName: 'Pavilion' });
    expect(draft.actions[0]?.kind).toBe('draft');
    expect(draft.actions[0]?.brief?.context).toContain('Recorded amount: 48000');
    expect(draft.actions[0]?.brief?.context).toContain('Use only these recorded fields');
    expect(draft.actions[0]?.brief?.goal).toBe('Send the proposal');
  });

  it('calculates open pipeline totals and proposes a deal only when the amount is present', () => {
    const totals = askAgent('where is the money?', world({
      deals: [
        { id: 'd1', name: 'Open', value: '10000', stage: 'Proposal', isWon: null },
        { id: 'd2', name: 'Won', value: '99999', stage: 'Closed Won', isWon: true },
      ],
    }));
    expect(totals.text).toContain('Calculated from loaded open deals');
    expect(totals.text).toContain('Proposal: 10000 across 1 deal');
    expect(totals.text).not.toContain('99999');

    const missing = askAgent('create a deal called Northwind', world());
    expect(missing.text).toContain('numeric amount');
    expect(missing.actions).toHaveLength(0);
    const proposed = askAgent('create a deal called Northwind for 15000', world());
    expect(proposed.actions[0]?.kind).toBe('create-deal');
    expect(proposed.actions[0]?.dealValue).toBe('15000');
    expect(proposed.text).toContain('until you confirm');
  });

  it('lists duplicates for review and does not merge them', () => {
    const group: WorkSuggestion = {
      ...item,
      key: 'duplicates:contacts',
      href: '/duplicates',
      name: 'Ada Lovelace',
      list: 'Duplicates',
      action: 'Merge 1 duplicate into Ada Lovelace.',
      mergePrimaryId: 'c1',
      mergeDuplicateIds: ['c2'],
      mergeKind: 'contacts',
    };
    const reply = askAgent('any duplicates?', world({ items: [group] }));
    expect(reply.text).toContain('Nothing is merged');
    expect(reply.actions.every((action) => action.kind !== 'merge')).toBe(true);
    expect(reply.actions[0]?.href).toBe('/duplicates');
  });

  it('does not pick between two people who share a name', () => {
    const other: WorkSuggestion = { ...item, key: 'lead:l2', href: '/leads/l2', name: 'Ada Wong', priority: 10 };
    const reply = askAgent('what about Ada', world({ items: [item, other] }));
    expect(reply.focusKey).toBeNull();
    expect(reply.text).toMatch(/could not match|will not guess/i);
  });

  it('answers where do I start with the loaded next step', () => {
    const reply = askAgent('where do I start?', world());
    expect(reply.focusKey).toBe('lead:l1');
    expect(reply.text).toContain('Send the first note.');
    expect(reply.text).toContain('ada@example.com');
  });

  it('stays on the open list and the open record', () => {
    const deal: WorkSuggestion = { ...item, key: 'deal:d1', href: '/deals/d1', name: 'Northwind', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 99 };
    const onDeals = askAgent('what should I do?', world({ items: [item, deal], page: { mode: 'list', list: 'Deals' } }));
    expect(onDeals.focusKey).toBe('deal:d1');
    expect(onDeals.text).not.toContain('Ada Lovelace');
    const onLead = askAgent('what should I do?', world({ items: [deal, item], page: { mode: 'record', href: '/leads/l1' } }));
    expect(onLead.focusKey).toBe('lead:l1');
  });

  it('names the phone on a call and the overdue row', () => {
    const call: WorkSuggestion = { ...item, action: 'Make the first call and log whether they answered.', guide: 'start_outreach' };
    const calls = askAgent('who should I call?', world({
      items: [call],
      leads: [{ id: 'l1', name: 'Ada Lovelace', phone: '555-0100', email: 'ada@example.com' }],
    }));
    expect(calls.text).toContain('555-0100');
    expect(calls.focusKey).toBe('lead:l1');
    const late: WorkSuggestion = { ...item, key: 'task:t1', href: '/tasks/t1', name: 'Send deck', action: 'Complete this task. It is past its due date.', because: 'The due date is before today.', priority: 93 };
    const overdue = askAgent('the overdue one', world({ items: [item, late] }));
    expect(overdue.focusKey).toBe('task:t1');
  });

  it('matches a saved email and ignores a thin-probability amount', () => {
    const sent = askAgent('send this to ada@example.com', world({ items: [{ ...item, email: 'ada@example.com' }] }));
    expect(sent.focusKey).toBe('lead:l1');
    expect(sent.actions[0]?.kind).toBe('send');
    const rich: WorkSuggestion = { ...item, key: 'deal:d9', href: '/deals/d9', name: 'Pavilion', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72 };
    const reply = askAgent('what should I do?', world({
      items: [{ ...item, priority: 90 }, rich],
      leads: [{ id: 'l1', name: 'Ada Lovelace', email: 'ada@example.com' }],
      deals: [{ id: 'd9', name: 'Pavilion', value: '48000', probability: 20 }],
    }));
    expect(reply.focusKey).toBe('lead:l1');
  });
});

describe('learnFrom', () => {
  it('counts a saved drop-out reason and a won deal', () => {
    const learned = learnFrom(
      [{ id: 'l1', name: 'Ada', pipelineState: JSON.stringify({ outreachStatus: 'not_interested', dropoutReason: 'not_within_budget' }) }],
      [{ id: 'd1', name: 'Pavilion', isWon: true, stage: 'Closed Won' }],
    );
    expect(learned.wonDeals).toBe(1);
    expect(learned.lostLeads).toBe(1);
    expect(learned.lines.join(' ')).toContain('Membership Fee Considered Too High');
  });

  it('states how many closed deals were won', () => {
    const learned = learnFrom([], [
      { id: 'd1', name: 'A', isWon: true, stage: 'Closed Won' },
      { id: 'd2', name: 'B', isWon: false, stage: 'Closed Lost' },
    ]);
    expect(learned.lines.join(' ')).toContain('1 of 2 closed deals were won.');
  });
});
