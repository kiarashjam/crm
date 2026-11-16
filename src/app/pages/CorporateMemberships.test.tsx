// The corporate memberships page: every company's membership with its seats, renewal and what the
// company owes, ended ones only when asked for, and a list that could not be read said so with a
// way to try again.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION } from '@/app/api/organizations';
import type { CorporateMembership, CorporateQuery } from '@/app/api/corporate';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

const api = vi.hoisted(() => ({ list: vi.fn<(query: CorporateQuery) => Promise<CorporateMembership[]>>() }));
vi.mock('@/app/api/corporate', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/corporate')>()),
  getCorporateMemberships: (query: CorporateQuery) => api.list(query),
}));

const { default: CorporateMemberships } = await import('./CorporateMemberships');

const ACME: CorporateMembership = {
  id: 'cm-1', companyId: 'acme', companyName: 'Acme AG', name: 'Acme corporate', seats: 4, seatsUsed: 3, status: 'Active', nomineeCharges: 'company',
  startsAtUtc: '2026-01-01T00:00:00Z', renewsAtUtc: '2026-12-01T00:00:00Z', termMonths: 12, notRenewing: false, renewalState: 'due', daysToRenewal: 18,
  nominees: [], formerNominees: [], createdAtUtc: '2026-01-01T00:00:00Z', version: 'v1',
  account: { kind: 'company', id: 'acme', name: 'Acme AG', open: true, currency: 'CHF', balance: -1200, owed: 1200, credit: 0, openCharges: 2 },
};
const BOLT: CorporateMembership = { ...ACME, id: 'cm-2', companyId: 'bolt', companyName: 'Bolt GmbH', name: 'Bolt corporate', status: 'Ended', renewalState: 'ended', endedAtUtc: '2026-06-30T00:00:00Z' };

const show = () => render(withQueries(<MemoryRouter><CorporateMemberships /></MemoryRouter>));

beforeEach(() => {
  api.list.mockReset();
  api.list.mockResolvedValue([ACME, BOLT]);
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions: [PERMISSION.ClubMembers], currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
});

describe('the corporate memberships page', () => {
  it('lists each company’s membership with its seats, renewal and what it owes, leaving ended ones out', async () => {
    show();

    const row = (await screen.findByRole('link', { name: 'Acme AG' })).closest('tr')!;
    expect(screen.getByRole('link', { name: 'Acme AG' })).toHaveAttribute('href', '/companies/acme');
    expect(within(row).getByText('3 / 4')).toBeInTheDocument();
    expect(within(row).getByText('Renewal due')).toBeInTheDocument();
    expect(row).toHaveTextContent(/1.200/);
    expect(screen.queryByRole('link', { name: 'Bolt GmbH' })).not.toBeInTheDocument();
  });

  it('asks the server for the status chosen, and for those renewing soon', async () => {
    show();
    await screen.findByRole('link', { name: 'Acme AG' });

    await userEvent.selectOptions(screen.getByLabelText('Status'), 'Ended');
    expect(await screen.findByRole('link', { name: 'Bolt GmbH' })).toBeInTheDocument();
    await userEvent.click(screen.getByLabelText('Renewing in the next 60 days'));

    await waitFor(() => expect(api.list).toHaveBeenLastCalledWith({ status: 'Ended', renewingWithinDays: 60 }));
  });

  it('says a list that could not be read, and reads it again when asked', async () => {
    api.list.mockRejectedValueOnce(new Error('The server did not answer.')).mockResolvedValue([ACME]);
    show();

    const failed = await screen.findByRole('alert');
    await userEvent.click(within(failed).getByRole('button', { name: /Try again/ }));

    expect(await screen.findByRole('link', { name: 'Acme AG' })).toBeInTheDocument();
  });
});
