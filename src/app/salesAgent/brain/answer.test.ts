import { describe, expect, it } from 'vitest';
import { formatMoney } from '@/app/lib/money';
import { emptyMemory, matchPeople, rowFor } from './reason';
import { askAgent, type AgentWorld } from './answer';
import { learnFrom } from './patterns';
import { amountIn } from './workflows';
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
    page: { mode: 'focus' },
    patterns: learnFrom([], []),
    ...extra,
  };
}

const NOW = new Date(2026, 9, 22, 12, 0, 0);

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
    const reply = askAgent('draft that one', world(), new Date(), { ...emptyMemory(), focusKey: 'lead:l1' });
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
    expect(brief.text).toContain(formatMoney(48000, 'USD'));
    expect(brief.text).toContain('Intro call');
    expect(brief.text).toContain('Send pricing');
    expect(brief.text).toContain('Suggestion');

    const draft = askAgent('draft that', world({
      items: [deal],
      deals: [{ id: 'd1', name: 'Pavilion renewal', companyName: 'Pavilion', value: '48000', stage: 'Proposal' }],
    }), new Date(), { focusKey: 'deal:d1', name: 'Pavilion renewal', leadId: null, dealId: 'd1', contactId: null, companyId: null, companyName: 'Pavilion' });
    expect(draft.actions[0]?.kind).toBe('draft');
    expect(draft.actions[0]?.brief?.context).toContain(`Recorded amount: ${formatMoney(48000, 'USD')}`);
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
    expect(totals.text).toContain(`Proposal: ${formatMoney(10000, 'USD')} across 1 deal`);
    expect(totals.text).not.toContain('99');

    const missing = askAgent('create a deal called Northwind', world());
    expect(missing.text).toContain('numeric amount');
    expect(missing.actions).toHaveLength(0);
    const proposed = askAgent('create a deal called Northwind for 15000', world());
    expect(proposed.actions[0]?.kind).toBe('create-deal');
    expect(proposed.actions[0]?.title).toBe('Northwind');
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

  it('ignores a thin-probability amount when weighing', () => {
    const rich: WorkSuggestion = { ...item, key: 'deal:d9', href: '/deals/d9', name: 'Pavilion', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.', priority: 72 };
    const reply = askAgent('what should I do?', world({
      items: [{ ...item, priority: 90 }, rich],
      leads: [{ id: 'l1', name: 'Ada Lovelace', email: 'ada@example.com' }],
      deals: [{ id: 'd9', name: 'Pavilion', value: '48000', probability: 20 }],
    }));
    expect(reply.focusKey).toBe('lead:l1');
  });
});

describe('names are words, not fragments', () => {
  const theodore: WorkSuggestion = { ...item, key: 'lead:t1', href: '/leads/t1', name: 'Theodore Ng' };
  const candice: WorkSuggestion = { ...item, key: 'lead:c1', href: '/leads/c1', name: 'Candice Roy' };
  const ann: WorkSuggestion = { ...item, key: 'lead:a1', href: '/leads/a1', name: 'Ann' };

  it('does not read "the" as Theodore, "can" as Candice, or "planning" as Ann', () => {
    const items = [theodore, candice, ann];
    expect(matchPeople(items, 'what can I do with the proposal').status).toBe('none');
    expect(matchPeople(items, 'help with planning').status).toBe('none');
    expect(matchPeople(items, 'draft something for Ann')).toEqual({ status: 'one', item: ann });
    expect(matchPeople(items, 'brief Theo')).toEqual({ status: 'one', item: theodore });
  });

  it('does not read a command word as the start of a name', () => {
    const callum: WorkSuggestion = { ...item, key: 'lead:c2', href: '/leads/c2', name: 'Callum Withers' };
    expect(matchPeople([callum], 'call them back').status).toBe('none');
    expect(matchPeople([callum], 'what should I do with this').status).toBe('none');
    expect(matchPeople([callum], 'brief Callum').status).toBe('one');
  });

  it('matches an email as a whole address', () => {
    const ada = { ...item, email: 'ada@example.com' };
    expect(matchPeople([ada], 'send it to xada@example.com').status).toBe('none');
    expect(matchPeople([ada], 'send it to ada@example.com').status).toBe('one');
  });

  it('does not run a name filter over a list question', () => {
    const reply = askAgent('how many leads does Ada have', world({ items: [item, { ...item, key: 'lead:l2', href: '/leads/l2', name: 'Ada Wong' }] }));
    expect(reply.text).toContain('Calculated from the records loaded here: 1 leads.');
  });

  it('treats a lead and the contact made from it as one person', () => {
    const reply = askAgent('brief Ada Lovelace', world({
      contacts: [{ id: 'c1', name: 'Ada Lovelace', email: 'ada@example.com', jobTitle: 'Engineer' }],
    }));
    expect(reply.text).not.toMatch(/will not guess/);
    expect(reply.text).toContain('Lead Ada Lovelace');
    expect(reply.text).toContain('Job title: Engineer');
  });

  it('does not read "account" as a request to count', () => {
    const northwind: WorkSuggestion = { ...item, key: 'deal:d1', href: '/deals/d1', name: 'Northwind', list: 'Deals', action: 'Send the proposal.', because: 'The deal is in Proposal.' };
    const reply = askAgent('what is up with the Northwind account', world({ items: [item, northwind] }));
    expect(reply.focusKey).toBe('deal:d1');
    expect(reply.text).not.toContain('records need a step');
  });
});

describe('follow-ups stay on the record they were about', () => {
  const other: WorkSuggestion = { ...item, key: 'lead:l2', href: '/leads/l2', name: 'Grace Hopper', email: 'grace@example.com', brief: { copyTypeId: 'follow-up', goal: 'x', context: 'y' } };

  it('refuses a write that names nobody, instead of taking the top row', () => {
    const many = world({ items: [other, item] });
    for (const question of ['draft a message', 'send it', 'create a task', 'convert', 'enroll', 'assign to me', 'book a meeting']) {
      const reply = askAgent(question, { ...many, sequences: [{ id: 's1', name: 'Welcome' }] });
      expect(reply.actions.filter((action) => action.kind !== 'open'), question).toEqual([]);
      expect(reply.text, question).not.toContain('Grace Hopper');
    }
  });

  it('reads the remembered record by its id when it has no open step', () => {
    const quietLead = { id: 'l9', name: 'Mary Shelley', email: 'mary@example.com' };
    const reply = askAgent('draft that one', world({ items: [other], leads: [quietLead] }), NOW, { ...emptyMemory(), focusKey: 'lead:l9', leadId: 'l9' });
    expect(reply.actions[0]?.kind).toBe('draft');
    expect(reply.actions[0]?.leadId).toBe('l9');
    expect(reply.text).toContain('Mary Shelley');
  });

  it('refuses when the remembered record is gone', () => {
    const reply = askAgent('draft that one', world({ items: [other] }), NOW, { ...emptyMemory(), focusKey: 'lead:gone', leadId: 'gone' });
    expect(reply.actions).toEqual([]);
    expect(reply.text).not.toContain('Grace Hopper');
  });

  it('uses the record on screen before an older memory', () => {
    const reply = askAgent('add a note: left a voicemail', world({ items: [item, other], page: { mode: 'record', href: '/leads/l2' } }), NOW, { ...emptyMemory(), focusKey: 'lead:l1', leadId: 'l1' });
    expect(reply.actions[0]?.leadId).toBe('l2');
    const back = askAgent('add a note on that one: left a voicemail', world({ items: [item, other], page: { mode: 'record', href: '/leads/l2' } }), NOW, { ...emptyMemory(), focusKey: 'lead:l1', leadId: 'l1' });
    expect(back.actions[0]?.leadId).toBe('l1');
  });

  it('a brief by name moves the focus to that record', () => {
    const quietLead = { id: 'l9', name: 'Mary Shelley', email: 'mary@example.com' };
    const reply = askAgent('brief Mary Shelley', world({ leads: [quietLead, { id: 'l1', name: 'Ada Lovelace' }] }), NOW, { ...emptyMemory(), focusKey: 'lead:l1', leadId: 'l1' });
    expect(reply.focusKey).toBe('lead:l9');
    expect(reply.memory?.leadId).toBe('l9');
    expect(reply.text).not.toContain('Send the first note.');
    expect(rowFor({ items: [], leads: [quietLead], deals: [] }, reply.focusKey)?.name).toBe('Mary Shelley');
  });
});

describe('sends carry only words the person wrote', () => {
  const ada = { ...item, email: 'ada@example.com' };

  it('will not send the saved step as the email body', () => {
    const reply = askAgent('send this to ada@example.com', world({ items: [ada] }));
    expect(reply.actions.some((action) => action.kind === 'send')).toBe(false);
    expect(reply.actions[0]?.kind).toBe('draft');
    expect(reply.text).toMatch(/will not send the saved step/);
  });

  it('sends the typed message after a confirm', () => {
    const reply = askAgent('send Ada Lovelace: Thanks for today, the deck is attached.', world({ items: [ada] }));
    const send = reply.actions.find((action) => action.kind === 'send');
    expect(send?.to).toBe('ada@example.com');
    expect(send?.body).toBe('Thanks for today, the deck is attached.');
    expect(reply.text).toMatch(/only after you confirm/i);
  });

  it('reads the message after the colon as the message, not as a command', () => {
    const reply = askAgent('send Ada Lovelace: please add a note to your calendar for Friday', world({ items: [ada] }));
    const send = reply.actions.find((action) => action.kind === 'send');
    expect(send?.body).toBe('please add a note to your calendar for Friday');
    expect(reply.actions.some((action) => action.kind === 'note' || action.kind === 'task')).toBe(false);
  });

  it('refuses a person marked do not contact', () => {
    const reply = askAgent('send Ada Lovelace: hello', world({
      items: [ada],
      contacts: [{ id: 'c1', name: 'Ada Lovelace', email: 'ADA@example.com', doNotContact: true }],
    }));
    expect(reply.actions.some((action) => action.kind === 'send')).toBe(false);
    expect(reply.text).toMatch(/do not contact/i);
  });
});

describe('tasks and notes use the person\'s own words', () => {
  it('reads "remind me to email" as a task, with the typed title and day', () => {
    const reply = askAgent('remind me to email Ada Lovelace the deck tomorrow', world(), NOW);
    const task = reply.actions[0];
    expect(task?.kind).toBe('task');
    expect(task?.title).toBe('Email Ada Lovelace the deck');
    expect(task?.dueDateUtc).toBe('2026-10-23');
    expect(task?.leadId).toBe('l1');
  });

  it('keeps the record out of the title and reads a weekday', () => {
    const reply = askAgent('create a task for Ada Lovelace to send pricing on friday', world(), NOW);
    expect(reply.actions[0]?.title).toBe('Send pricing');
    expect(reply.actions[0]?.dueDateUtc).toBe('2026-10-23');
  });

  it('a name inside the note text does not change which record it is on', () => {
    const wong: WorkSuggestion = { ...item, key: 'lead:l2', href: '/leads/l2', name: 'Ada Wong' };
    const reply = askAgent('add a note: Ada called back about pricing, Ada\'s husband can\'t come', world({
      items: [item, wong],
      leads: [{ id: 'l1', name: 'Ada Lovelace' }, { id: 'l2', name: 'Ada Wong' }],
      page: { mode: 'record', href: '/leads/l1' },
    }));
    expect(reply.text).not.toMatch(/will not guess/);
    expect(reply.actions[0]?.leadId).toBe('l1');
    expect(reply.actions[0]?.body).toBe("Ada called back about pricing, Ada's husband can't come");
  });

  it('a note is the typed text, and a note with no text is a question', () => {
    const note = askAgent('write down that Ada Lovelace wants a call in March', world());
    expect(note.actions[0]?.kind).toBe('note');
    expect(note.actions[0]?.body).toBe('Ada Lovelace wants a call in March');
    const empty = askAgent('add a note for Ada Lovelace', world());
    expect(empty.actions.every((action) => action.kind !== 'note')).toBe(true);
    expect(empty.text).toMatch(/What should the note/);
  });
});

describe('money', () => {
  it('totals each currency on its own and leaves out negative amounts', () => {
    const reply = askAgent('pipeline value', world({
      deals: [
        { id: 'd1', name: 'Gala', value: '10000', currency: 'CHF', stage: 'Proposal' },
        { id: 'd2', name: 'Retreat', value: '5000', currency: 'USD', stage: 'Proposal' },
        { id: 'd3', name: 'Typo', value: '-200', currency: 'CHF', stage: 'Proposal' },
        { id: 'd4', name: 'Euro style', value: '1.234,50', currency: 'CHF', stage: 'Qualification' },
      ],
    }));
    expect(reply.text).toContain(`Open total in CHF: ${formatMoney(11234.5, 'CHF')}`);
    expect(reply.text).toContain(`Open total in USD: ${formatMoney(5000, 'USD')}`);
    expect(reply.text).toContain('Nothing is converted');
    expect(reply.text).toMatch(/negative amount.*Typo/);
    expect(reply.text).toContain('more than one currency');
  });

  it('reads the amount from an amount phrase, not from a year or a date', () => {
    expect(amountIn('create a deal called Harbor 2026 worth 15,000')).toMatchObject({ value: '15000' });
    expect(amountIn("create a deal for the gala worth CHF 12'500")).toMatchObject({ value: '12500', currency: 'CHF' });
    expect(amountIn('create a deal called Harbor for 15k')).toMatchObject({ value: '15000' });
    expect(amountIn('set the amount to 1.234,56')).toMatchObject({ value: '1234.56' });
    expect(amountIn('create a deal for 2027 renewal')).toBeNull();
    expect(amountIn('close date 2026-11-15')).toBeNull();
    expect(amountIn('create a deal called Harbor 2')).toBeNull();

    const proposed = askAgent('create a deal called Harbor 2026 worth €15,000', world());
    expect(proposed.actions[0]?.title).toBe('Harbor 2026');
    expect(proposed.actions[0]?.dealValue).toBe('15000');
    expect(proposed.actions[0]?.currency).toBe('EUR');
  });

  it('will not change a deal into another currency', () => {
    const reply = askAgent('set the amount to USD 5000', world({
      deals: [{ id: 'd1', name: 'Gala', value: '10000', currency: 'CHF', stage: 'Proposal' }],
    }), NOW, { ...emptyMemory(), focusKey: 'deal:d1', dealId: 'd1' });
    expect(reply.actions.some((action) => action.kind === 'update-deal')).toBe(false);
    expect(reply.text).toContain('in CHF');
  });
});

describe('what was not loaded is said, not guessed', () => {
  it('a stale list is named; a list that never loaded is refused', () => {
    const stale = askAgent('how many deals?', world({ deals: [{ id: 'd1', name: 'A' }], gaps: ['Deals could not be refreshed, so an earlier copy is used.'] }));
    expect(stale.text).toContain('1 deals');
    expect(stale.text).toContain('earlier copy');
    const none = askAgent('pipeline value', world({ deals: [], gaps: ['Deals could not be loaded.'] }));
    expect(none.text).toBe('Deals could not be loaded, so there is no pipeline total.');
  });

  it('does not report "no activity" when activities were never loaded', () => {
    const reply = askAgent('recent activity on Ada Lovelace', world({ tasks: [{ id: 't1', title: 'Call back', status: 'todo', leadId: 'l1' }] }));
    expect(reply.text).toContain('Activities are not loaded in this panel');
    expect(reply.text).not.toContain('No activity');
    expect(reply.text).toContain('Call back');
  });

  it('points to Team for invitations instead of saying none are waiting', () => {
    const reply = askAgent('any pending invites?', world());
    expect(reply.text).not.toMatch(/no team invitation/i);
    expect(reply.actions[0]?.href).toBe('/team');
  });

  it('leaves converted and dropped leads out of "who went quiet"', () => {
    const reply = askAgent('who went quiet?', world({
      leads: [
        { id: 'l1', name: 'Ada Lovelace', lastContactedAt: '2020-01-01' },
        { id: 'l2', name: 'Done Deal', lastContactedAt: '2020-01-01', isConverted: true },
        { id: 'l3', name: 'Not Keen', lastContactedAt: '2020-01-01', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }) },
      ],
    }), new Date('2020-02-01T00:00:00Z'));
    expect(reply.text).toContain('Ada Lovelace');
    expect(reply.text).not.toContain('Done Deal');
    expect(reply.text).not.toContain('Not Keen');
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
