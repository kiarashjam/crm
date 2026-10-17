import { describe, expect, it } from 'vitest';
import { pageScope } from './pageScope';

describe('pageScope', () => {
  it('keeps the whole workspace on the dashboard and on pages with no list', () => {
    expect(pageScope('/dashboard')).toEqual({ mode: 'focus' });
    expect(pageScope('/reports')).toEqual({ mode: 'focus' });
    expect(pageScope('/settings')).toEqual({ mode: 'focus' });
    expect(pageScope('/members/mem-1')).toEqual({ mode: 'focus' });
  });

  it('uses the list on a list page, including lead tools', () => {
    expect(pageScope('/leads')).toEqual({ mode: 'list', list: 'Leads' });
    expect(pageScope('/leads/import')).toEqual({ mode: 'list', list: 'Leads' });
    expect(pageScope('/deals')).toEqual({ mode: 'list', list: 'Deals' });
  });

  it('uses the open record on a detail page', () => {
    expect(pageScope('/leads/l1')).toEqual({ mode: 'record', href: '/leads/l1' });
    expect(pageScope('/deals/d1/')).toEqual({ mode: 'record', href: '/deals/d1' });
  });
});
