// The prefetch table restates part of the route table, so these tests exist to
// stop the restatement from being wrong in the two ways that matter: a path that
// maps to the wrong page, and a key with no loader behind it.

import { describe, it, expect } from 'vitest';
import { routeChunkKey, PREFETCHABLE } from './prefetchRoute';

describe('routeChunkKey', () => {
  it('maps top-level pages to themselves', () => {
    expect(routeChunkKey('/dashboard')).toBe('dashboard');
    expect(routeChunkKey('/leads')).toBe('leads');
    expect(routeChunkKey('/reports')).toBe('reports');
  });

  it('maps a record page to its detail page, not to the list', () => {
    // Warming the Leads list for /leads/<id> would fetch a chunk the user is not
    // about to render and still leave them on a spinner.
    expect(routeChunkKey('/leads/3f2504e0-4f89-11d3-9a0c-0305e82c3301')).toBe('leads/detail');
    expect(routeChunkKey('/deals/12')).toBe('deals/detail');
    expect(routeChunkKey('/companies/abc')).toBe('companies/detail');
  });

  it('does not mistake a sub-page for a record id', () => {
    expect(routeChunkKey('/leads/import')).toBe('leads/import');
    expect(routeChunkKey('/leads/webhook')).toBe('leads/webhook');
  });

  it('ignores the query string and the hash', () => {
    expect(routeChunkKey('/reports?tab=funnel')).toBe('reports');
    expect(routeChunkKey('/leads/9?edit=1')).toBe('leads/detail');
    expect(routeChunkKey('/dashboard#top')).toBe('dashboard');
  });

  it('tolerates a trailing slash', () => {
    expect(routeChunkKey('/leads/')).toBe('leads');
  });

  it('returns null rather than guessing', () => {
    expect(routeChunkKey('')).toBeNull();
    expect(routeChunkKey('dashboard')).toBeNull();
    expect(routeChunkKey('/')).toBeNull();
    expect(routeChunkKey('/leads/9/notes/4')).toBeNull();
    expect(routeChunkKey('/something-new')).toBe('something-new'); // key exists, loader will not
  });
});

describe('the table itself', () => {
  it('has a loader for every key routeChunkKey can produce for a known route', () => {
    const known = [
      '/dashboard', '/leads', '/leads/import', '/leads/webhook', '/leads/x',
      '/deals', '/deals/x', '/tasks', '/tasks/x', '/contacts', '/contacts/x',
      '/companies', '/companies/x', '/reports', '/activities', '/sequences',
      '/settings', '/team', '/send', '/templates', '/history',
    ];
    for (const path of known) {
      const key = routeChunkKey(path);
      expect(key, path).not.toBeNull();
      expect(PREFETCHABLE, path).toContain(key);
    }
  });

  it('has no loader for an unknown first segment', () => {
    // The safety net: an unrecognised path prefetches nothing at all.
    expect(PREFETCHABLE).not.toContain(routeChunkKey('/something-new'));
  });
});
