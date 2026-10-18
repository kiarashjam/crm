// The "saved in this browser only" notice.
//
// It has one job: tell people, on the page where it matters, that what they are
// entering never reaches their team. Shown too eagerly it trains people to ignore
// it; shown too rarely, entries quietly stay on one laptop. So it appears only
// once a page's own feature has actually fallen back to the browser, and never
// with demo data, where everything is local on purpose.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import LocalOnlyNotice, { localResourcesFor } from './LocalOnlyNotice';
import { markLocalOnly, resetLocalOnly } from '@/app/api/localOnly';

const NOTICE = /saved in this browser only/i;

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <LocalOnlyNotice />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  vi.stubEnv('VITE_API_URL', 'http://api.test');
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pat', email: 'pat@example.com' }));
});
afterEach(() => vi.unstubAllEnvs());

describe('LocalOnlyNotice', () => {
  it('stays hidden until the page has actually fallen back', () => {
    renderAt('/members');
    expect(screen.queryByText(NOTICE)).toBeNull();
  });

  it('appears as soon as the page’s feature falls back', () => {
    renderAt('/members');
    act(() => markLocalOnly('/api/members'));
    expect(screen.getByRole('status')).toHaveTextContent(NOTICE);
  });

  it('ignores a fallback that belongs to another page', () => {
    markLocalOnly('/api/gift-cards');
    renderAt('/members');
    expect(screen.queryByText(NOTICE)).toBeNull();
  });

  it('covers a sub-resource on a server-backed page', () => {
    markLocalOnly('/api/deals/7f3c2a10-0000-4000-8000-000000000001/line-items');
    renderAt('/deals/7f3c2a10-0000-4000-8000-000000000001');
    expect(screen.getByRole('status')).toHaveTextContent(NOTICE);
  });

  it('never shows with demo data', () => {
    vi.stubEnv('VITE_API_URL', '');
    markLocalOnly('/api/members');
    renderAt('/members');
    expect(screen.queryByText(NOTICE)).toBeNull();
  });
});

describe('localResourcesFor', () => {
  it('matches the specific route before the general one', () => {
    expect(localResourcesFor('/members/import')).toEqual(['members']);
    expect(localResourcesFor('/members/m-1')).toContain('gift-cards');
  });

  it('returns nothing for pages that are fully on the server', () => {
    expect(localResourcesFor('/leads')).toEqual([]);
    expect(localResourcesFor('/pipeline')).toEqual([]);
  });
});
