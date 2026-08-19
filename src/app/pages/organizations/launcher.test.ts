import { describe, it, expect } from 'vitest';
import type { Organization } from '@/app/api/organizations';
import {
  searchKey, filterWorkspaces, orderWorkspaces, moveHighlight, digitTarget,
  IDLE, beginLaunch, isLaunching, isBusy, monogram, hueFor, roleLabel, returnPath,
} from './launcher';

const org = (id: string, name: string, isOwner = false): Organization =>
  ({ id, name, ownerUserId: 'u', isOwner, role: isOwner ? 0 : 1 } as Organization);

describe('searchKey / filterWorkspaces', () => {
  it('matches accented names from unaccented typing', () => {
    // These are Swiss and French names. "leman" must find "Lac Léman".
    expect(searchKey('Lac Léman')).toBe('lac leman');
    expect(filterWorkspaces([org('1', 'Lac Léman')], 'leman')).toHaveLength(1);
    expect(filterWorkspaces([org('1', 'Zürich Süd')], 'zurich')).toHaveLength(1);
  });

  it('is case insensitive and ignores surrounding space', () => {
    for (const q of ['  PAVILLON ', 'pavillon', 'Pav', '46']) {
      expect(filterWorkspaces([org('1', 'Pavillon46')], q), q).toHaveLength(1);
    }
  });

  it('an empty query matches everything', () => {
    const all = [org('1', 'A'), org('2', 'B')];
    expect(filterWorkspaces(all, '')).toHaveLength(2);
    expect(filterWorkspaces(all, '   ')).toHaveLength(2);
  });

  it('returns nothing rather than everything when nothing matches', () => {
    expect(filterWorkspaces([org('1', 'Pavillon46')], 'zzz')).toEqual([]);
  });
});

describe('orderWorkspaces', () => {
  const orgs = [
    org('c', 'Charlie'), org('a', 'Alpha', true), org('b', 'Bravo'), org('d', 'Delta', true),
  ];

  it('puts the active workspace first, then owned, then alphabetical', () => {
    expect(orderWorkspaces(orgs, 'b').map((o) => o.id)).toEqual(['b', 'a', 'd', 'c']);
  });

  it('falls back to owned-then-name with no active workspace', () => {
    // Owned first (Alpha, Delta), then the rest by name (Bravo, Charlie).
    expect(orderWorkspaces(orgs, null).map((o) => o.id)).toEqual(['a', 'd', 'b', 'c']);
  });

  it('does not mutate the input', () => {
    const before = orgs.map((o) => o.id);
    orderWorkspaces(orgs, 'd');
    expect(orgs.map((o) => o.id)).toEqual(before);
  });

  it('INVARIANT: the order is total, so the list cannot reshuffle', () => {
    // Same-name workspaces must still have a deterministic order, or the grid
    // visibly reorders itself between renders.
    const dupes = [org('z', 'Same'), org('a', 'Same'), org('m', 'Same')];
    const once = orderWorkspaces(dupes, null).map((o) => o.id);
    const twice = orderWorkspaces([...dupes].reverse(), null).map((o) => o.id);
    expect(once).toEqual(twice);
    expect(once).toEqual(['a', 'm', 'z']);
  });
});

describe('moveHighlight', () => {
  it('wraps in both directions', () => {
    expect(moveHighlight(0, -1, 4)).toBe(3);   // the bug a bare % would cause
    expect(moveHighlight(3, 1, 4)).toBe(0);
    expect(moveHighlight(1, 1, 4)).toBe(2);
  });

  it('never returns an out-of-range index', () => {
    for (const start of [-1, 0, 2, 9]) {
      for (const delta of [-3, -1, 1, 5]) {
        const n = moveHighlight(start, delta, 4);
        expect(n).toBeGreaterThanOrEqual(0);
        expect(n).toBeLessThan(4);
      }
    }
  });

  it('reports no selection for an empty list', () => {
    expect(moveHighlight(0, 1, 0)).toBe(-1);
  });
});

describe('digitTarget', () => {
  const items = ['a', 'b', 'c'];
  it('maps 1-9 onto positions, one-based', () => {
    expect(digitTarget('1', items)).toBe('a');
    expect(digitTarget('3', items)).toBe('c');
  });
  it('ignores 0 and non-digits, and out-of-range digits', () => {
    expect(digitTarget('0', items)).toBeNull();
    expect(digitTarget('x', items)).toBeNull();
    expect(digitTarget('Enter', items)).toBeNull();
    expect(digitTarget('9', items)).toBeNull();
  });
});

describe('launch state machine', () => {
  it('starts a launch from idle', () => {
    const s = beginLaunch(IDLE, 'org-1');
    expect(s).toEqual({ phase: 'launching', orgId: 'org-1' });
    expect(isLaunching(s, 'org-1')).toBe(true);
    expect(isLaunching(s, 'org-2')).toBe(false);
    expect(isBusy(s)).toBe(true);
  });

  it('GUARD: a second launch cannot start while one is in flight', () => {
    // The card is clickable, Enter-activatable and digit-selectable, so without
    // this a fast double input starts two animations and navigates twice.
    const first = beginLaunch(IDLE, 'org-1');
    expect(beginLaunch(first, 'org-2')).toBe(first);
    expect(beginLaunch(first, 'org-1')).toBe(first);
  });

  it('is idle to begin with', () => {
    expect(isBusy(IDLE)).toBe(false);
    expect(isLaunching(IDLE, 'anything')).toBe(false);
  });
});

describe('monogram', () => {
  it.each([
    ['Pavillon46', 'PA'],
    ['Lac Léman SA', 'LS'],
    ['Alpine Partners', 'AP'],
    ['X', 'X'],
    ['  spaced   out  ', 'SO'],
    ['', '?'],
  ])('%s → %s', (name, expected) => {
    expect(monogram(name)).toBe(expected);
  });
});

describe('hueFor', () => {
  it('is stable for the same id across calls', () => {
    // A random or index-based hue would change between sessions or when the
    // list reorders, so a workspace would not be recognisable by colour.
    expect(hueFor('org-abc')).toBe(hueFor('org-abc'));
  });

  it('stays in range and differs between ids', () => {
    const a = hueFor('org-abc');
    const b = hueFor('org-xyz');
    for (const h of [a, b]) {
      expect(h).toBeGreaterThanOrEqual(0);
      expect(h).toBeLessThan(360);
    }
    expect(a).not.toBe(b);
  });
});

describe('roleLabel', () => {
  it('names each role', () => {
    expect(roleLabel({ isOwner: false, role: 0 })).toBe('Owner');
    expect(roleLabel({ isOwner: false, role: 1 })).toBe('Member');
    expect(roleLabel({ isOwner: false, role: 2 })).toBe('Manager');
    expect(roleLabel({ isOwner: false, role: 3 })).toBe('Viewer');
  });

  it('trusts isOwner over a stale role number', () => {
    // The same precedence isOrgViewer uses. An owner shown as "Viewer" would be
    // told they cannot edit their own workspace.
    expect(roleLabel({ isOwner: true, role: 3 })).toBe('Owner');
  });

  it('falls back to Member for a role it does not know', () => {
    expect(roleLabel({ isOwner: false, role: 99 })).toBe('Member');
  });
});

describe('returnPath', () => {
  it('sends you back where you were headed', () => {
    expect(returnPath('/leads/abc')).toBe('/leads/abc');
    expect(returnPath('/reports?tab=funnel')).toBe('/reports?tab=funnel');
  });

  it('falls back when there is nothing to go back to', () => {
    expect(returnPath(undefined)).toBe('/dashboard');
    expect(returnPath(null)).toBe('/dashboard');
    expect(returnPath('')).toBe('/dashboard');
    expect(returnPath({ nope: true })).toBe('/dashboard');
  });

  it('refuses to leave the app', () => {
    // A protocol-relative path is the one that actually navigates off-site.
    expect(returnPath('//evil.example/x')).toBe('/dashboard');
    expect(returnPath('https://evil.example')).toBe('/dashboard');
    expect(returnPath('javascript:alert(1)')).toBe('/dashboard');
  });

  it('never sends you back to the launcher', () => {
    // Which would bounce straight back here and look like a hung page.
    expect(returnPath('/organizations')).toBe('/dashboard');
  });

  it('honours an explicit fallback', () => {
    expect(returnPath(undefined, '/settings')).toBe('/settings');
  });
});
