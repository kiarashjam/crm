// The production crash, pinned.
//
// The Leads page threw "Cannot read properties of undefined (reading
// 'toLowerCase')" during an API outage, because the search filter trusted
// `Lead.name` and `Lead.email` to be present — a compile-time claim about data
// the server controls. Most of these tests are the shapes that caused it.

import { describe, it, expect } from 'vitest';
import type { Lead } from '@/app/api/types';
import { matchesLeadSearch, leadInitials } from './leadText';

const lead = (over: Partial<Lead>): Partial<Lead> => ({
  id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'New', ...over,
});

describe('matchesLeadSearch — the crash', () => {
  it('does NOT throw on a lead with no name or email', () => {
    // The exact production failure. A search box must never be able to crash the
    // list it is filtering.
    const broken = { id: 'x' } as Partial<Lead>;
    expect(() => matchesLeadSearch(broken, 'jean')).not.toThrow();
    expect(matchesLeadSearch(broken, 'jean')).toBe(false);
  });

  it('does not throw on any field being null, undefined or a non-string', () => {
    for (const bad of [null, undefined, 42, {}, []] as unknown[]) {
      const l = { id: 'x', name: bad, email: bad, phone: bad } as unknown as Partial<Lead>;
      expect(() => matchesLeadSearch(l, 'anything'), String(bad)).not.toThrow();
      expect(matchesLeadSearch(l, 'anything'), String(bad)).toBe(false);
    }
  });

  it('a broken record is simply not a match, rather than a match or a crash', () => {
    // It must not accidentally match everything either: a page full of records
    // that all match any query is its own kind of wrong.
    expect(matchesLeadSearch({ id: 'x' }, 'a')).toBe(false);
  });
});

describe('matchesLeadSearch — searching', () => {
  it('matches on name, email and phone', () => {
    expect(matchesLeadSearch(lead({}), 'dupont')).toBe(true);
    expect(matchesLeadSearch(lead({}), 'example.ch')).toBe(true);
    expect(matchesLeadSearch(lead({ phone: '+41 22 555 0134' }), '555')).toBe(true);
  });

  it('ignores case on both sides', () => {
    expect(matchesLeadSearch(lead({ name: 'JEAN DUPONT' }), 'jean')).toBe(true);
    expect(matchesLeadSearch(lead({}), 'DUPONT')).toBe(true);
  });

  it('an empty query matches everything, so clearing the box restores the list', () => {
    // Returning false here would empty the page the moment somebody deleted
    // their search text.
    for (const q of ['', '   ', '\t']) {
      expect(matchesLeadSearch(lead({}), q), JSON.stringify(q)).toBe(true);
      expect(matchesLeadSearch({ id: 'x' }, q), JSON.stringify(q)).toBe(true);
    }
  });

  it('tolerates padding around the stored value and the query', () => {
    expect(matchesLeadSearch(lead({ name: '  Jean Dupont  ' }), '  jean  ')).toBe(true);
  });

  it('does not match on something absent', () => {
    expect(matchesLeadSearch(lead({}), 'zzz')).toBe(false);
  });
});

describe('leadInitials', () => {
  it('takes the first and last word, because the surname is what people recognise', () => {
    expect(leadInitials('Jean Dupont')).toBe('JD');
    expect(leadInitials('Jean Michel Dupont')).toBe('JD');
  });

  it('uses the first two letters of a single name', () => {
    expect(leadInitials('Léa')).toBe('LÉ');
    expect(leadInitials('Ng')).toBe('NG');
  });

  it('copes with a single letter', () => {
    expect(leadInitials('X')).toBe('X');
  });

  it('never throws, and never renders empty', () => {
    // An avatar with nothing in it reads as a rendering bug rather than as
    // missing data, so an absent name gets a visible placeholder.
    for (const bad of [undefined, null, '', '   ', 42, {}, []] as unknown[]) {
      expect(() => leadInitials(bad), String(bad)).not.toThrow();
      expect(leadInitials(bad), String(bad)).toBe('?');
    }
  });

  it('collapses odd whitespace instead of producing blanks', () => {
    // `'Jean  Dupont'.split(' ')` yields an empty middle element, and taking
    // [0] of that is undefined — which is how the original crashed.
    expect(leadInitials('Jean   Dupont')).toBe('JD');
    expect(leadInitials('\tJean\nDupont ')).toBe('JD');
  });
});
