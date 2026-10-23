import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { HIDDEN_ON, NOT_RECORDS, itemOnList, pageScope } from './pageScope';

describe('pageScope', () => {
  it('keeps the whole workspace on the dashboard and on pages with no list', () => {
    expect(pageScope('/dashboard')).toEqual({ mode: 'focus' });
    expect(pageScope('/reports')).toEqual({ mode: 'focus' });
    expect(pageScope('/settings')).toEqual({ mode: 'focus' });
    expect(pageScope('/members/mem-1')).toEqual({ mode: 'record', href: '/members/mem-1' });
    expect(pageScope('/members')).toEqual({ mode: 'list', list: 'Members' });
    expect(pageScope('/members/import')).toEqual({ mode: 'list', list: 'Members' });
    expect(pageScope('/calendar')).toEqual({ mode: 'list', list: 'Calendar' });
    expect(pageScope('/visits')).toEqual({ mode: 'list', list: 'Visits' });
    expect(pageScope('/audit')).toEqual({ mode: 'list', list: 'Audit' });
  });

  it('uses the list on a list page, including lead tools', () => {
    expect(pageScope('/leads')).toEqual({ mode: 'list', list: 'Leads' });
    expect(pageScope('/leads/import')).toEqual({ mode: 'list', list: 'Leads' });
    expect(pageScope('/leads/webhook')).toEqual({ mode: 'list', list: 'Leads' });
    expect(pageScope('/deals')).toEqual({ mode: 'list', list: 'Deals' });
  });

  it('the lead routing page is a lead tool, not a lead called "routing"', () => {
    // It used to come back as { mode: 'record', href: '/leads/routing' }, and the
    // panel showed an empty "Plan / This record has no saved plan." on top of it.
    expect(pageScope('/leads/routing')).toEqual({ mode: 'list', list: 'Leads' });
  });

  it('keeps a lead phase on the leads list and reservations on the calendar', () => {
    expect(itemOnList({ href: '/leads/l1', list: 'Outreach' }, 'Leads')).toBe(true);
    expect(itemOnList({ href: '/deals/d1', list: 'Deals' }, 'Leads')).toBe(false);
    expect(itemOnList({ href: '/reservations/r1', list: 'Reservations' }, 'Calendar')).toBe(true);
  });

  it('uses the open record on a detail page', () => {
    expect(pageScope('/leads/l1')).toEqual({ mode: 'record', href: '/leads/l1' });
    expect(pageScope('/deals/d1/')).toEqual({ mode: 'record', href: '/deals/d1' });
  });

  it('stays off the till and the member-portal preview', () => {
    expect(pageScope('/pos')).toEqual({ mode: 'hidden' });
    expect(pageScope('/portal')).toEqual({ mode: 'hidden' });
    expect(pageScope('/portal/')).toEqual({ mode: 'hidden' });
    // A prefix match must not swallow unrelated pages.
    expect(pageScope('/positions')).toEqual({ mode: 'focus' });
  });

  it('INVARIANT: no static route in App.tsx is mistaken for a record', () => {
    // Every route like "/leads/routing" — a fixed word, not ":id" — sitting under
    // a list prefix must resolve to that list, never to a record. Read from the
    // real route table, so a page added later is covered without anyone
    // remembering this file exists.
    const app = readFileSync(resolve(__dirname, '../App.tsx'), 'utf8');
    const routes = [...app.matchAll(/path="(\/[^"]+)"/g)].map((m) => m[1]!);
    expect(routes.length, 'the route table should be readable').toBeGreaterThan(20);

    const statics = routes.filter((r) => /^\/[a-z-]+\/[a-z-]+$/.test(r));
    const misread = statics.filter((r) => pageScope(r).mode === 'record');
    expect(misread, 'static pages read as records — add them to NOT_RECORDS').toEqual([]);

    // And the special cases name pages that actually exist.
    for (const [prefix, words] of Object.entries(NOT_RECORDS)) {
      for (const word of words) expect(routes).toContain(`${prefix}/${word}`);
    }
    for (const hidden of HIDDEN_ON) expect(routes).toContain(hidden);
  });
});
