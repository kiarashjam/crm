// A company at the club, on its page: its house account as the server keeps it (opened only by
// whoever handles the money), its corporate membership with the nominees in its seats (seated,
// swapped and removed by whoever runs the members, the server's refusals said as they are), its
// private events, a read that failed said so with a retry, and nothing at all with the club off.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { ClientAccount, CorporateMembership, PrivateEvent } from '@/app/api/corporate';
import type { Member } from '@/app/api/members';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[], clubEnabled = true) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH',
      timeZone: 'Europe/Zurich', clubEnabled,
    },
  };
};

const api = vi.hoisted(() => ({
  account: vi.fn<(kind: string, id: string) => Promise<ClientAccount | null>>(),
  open: vi.fn<(kind: string, id: string) => Promise<ClientAccount>>(),
  charges: vi.fn<(id: string) => Promise<unknown[]>>(),
  memberships: vi.fn<(query: unknown) => Promise<CorporateMembership[]>>(),
  events: vi.fn<(query: unknown) => Promise<PrivateEvent[]>>(),
  seat: vi.fn<(id: string, memberId: string) => Promise<CorporateMembership>>(),
  remove: vi.fn<(id: string, nomineeId: string, reason?: string) => Promise<CorporateMembership>>(),
  members: vi.fn<() => Promise<Member[]>>(),
}));
vi.mock('@/app/api/corporate', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/corporate')>()),
  getClientAccount: (kind: string, id: string) => api.account(kind, id),
  openClientAccount: (kind: string, id: string) => api.open(kind, id),
  getAccountCharges: (id: string) => api.charges(id),
  getCorporateMemberships: (query: unknown) => api.memberships(query),
  getPrivateEvents: (query: unknown) => api.events(query),
  addNominee: (id: string, memberId: string) => api.seat(id, memberId),
  removeNominee: (id: string, nomineeId: string, reason?: string) => api.remove(id, nomineeId, reason),
}));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: () => api.members(),
}));

const { ClientClubCard } = await import('./ClientClubCard');

const OPEN: ClientAccount = { kind: 'company', id: 'acme', name: 'Acme AG', open: true, currency: 'CHF', balance: -1200, owed: 1200, credit: 0, openCharges: 2 };
const CLOSED: ClientAccount = { ...OPEN, open: false, balance: 0, owed: 0, openCharges: 0 };

function member(id: string, first: string): Member {
  return {
    id, firstName: first, lastName: 'Keller', email: `${first.toLowerCase()}@keller.ch`, tier: 'Gold', status: 'Active', joinedAtUtc: '2025-01-01T00:00:00Z',
    renewsAtUtc: '2026-12-31T00:00:00Z', duesAmount: 0, duesFrequency: 'Monthly', houseAccountBalance: 0,
  } as Member;
}

const MEMBERSHIP: CorporateMembership = {
  id: 'cm-1', companyId: 'acme', companyName: 'Acme AG', name: 'Acme corporate', seats: 2, seatsUsed: 1, status: 'Active', nomineeCharges: 'company',
  startsAtUtc: '2026-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z', termMonths: 12, notRenewing: false, renewalState: 'current', daysToRenewal: 49,
  nominees: [{ id: 'n-anna', memberId: 'm-anna', memberName: 'Anna Keller', addedAtUtc: '2026-02-01T00:00:00Z', ownDuesRunning: true }],
  formerNominees: [], createdAtUtc: '2026-01-01T00:00:00Z', version: 'v1',
};

const show = () => render(withQueries(<MemoryRouter><ClientClubCard kind="company" id="acme" name="Acme AG" /></MemoryRouter>));

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.account.mockResolvedValue(OPEN);
  api.charges.mockResolvedValue([]);
  api.memberships.mockResolvedValue([MEMBERSHIP]);
  api.events.mockResolvedValue([]);
  api.members.mockResolvedValue([member('m-anna', 'Anna'), member('m-ben', 'Ben')]);
  as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
});

describe("a company's club card", () => {
  it('shows what the company owes, its membership with its seats and nominees, and asks only for its own records', async () => {
    show();

    expect(await screen.findByText('1 of 2 taken')).toBeInTheDocument();
    expect(screen.getByText(/1.200/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Anna Keller' })).toHaveAttribute('href', '/members/m-anna');
    expect(screen.getByText(/their own dues still run/)).toBeInTheDocument();
    expect(api.account).toHaveBeenCalledWith('company', 'acme');
    expect(api.memberships).toHaveBeenCalledWith({ companyId: 'acme' });
    expect(api.events).toHaveBeenCalledWith({ companyId: 'acme' });
  });

  it('opens an account for whoever handles the money, and offers nothing to open to anyone else', async () => {
    api.account.mockResolvedValueOnce(CLOSED).mockResolvedValue(OPEN);
    api.open.mockResolvedValue(OPEN);
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Open a house account' }));

    await waitFor(() => expect(api.open).toHaveBeenCalledWith('company', 'acme'));
    expect(toast.success).toHaveBeenCalledWith('Acme AG has a house account at the club now.');
  });

  it('does not offer to open the account to someone who cannot handle money', async () => {
    as([PERMISSION.ClubMembers]);
    api.account.mockResolvedValue(CLOSED);
    show();

    expect(await screen.findByText(/has no house account yet/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open a house account' })).not.toBeInTheDocument();
  });

  it("seats a member, and says the server's refusal as it is when every seat is taken", async () => {
    api.seat.mockRejectedValue(new ApiError('Its one seat is taken. Swap a nominee, remove one, or give the membership more seats.', 409, undefined, true, 'CorporateMembership.SeatsFull.Conflict'));
    show();

    const panel = await screen.findByRole('article', { name: 'Acme corporate' });
    // Anna holds a seat already, so only Ben is offered.
    await within(panel).findByRole('option', { name: 'Ben Keller' });
    expect(within(panel).queryByRole('option', { name: 'Anna Keller' })).not.toBeInTheDocument();
    await userEvent.selectOptions(within(panel).getByLabelText('Give a seat to'), 'm-ben');
    await userEvent.click(within(panel).getByRole('button', { name: 'Seat' }));

    await waitFor(() => expect(api.seat).toHaveBeenCalledWith('cm-1', 'm-ben'));
    expect(toast.error).toHaveBeenCalledWith('Its one seat is taken. Swap a nominee, remove one, or give the membership more seats.');
  });

  it('removes a nominee with the reason given', async () => {
    api.remove.mockResolvedValue({ ...MEMBERSHIP, seatsUsed: 0, nominees: [] });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Remove Anna Keller' }));
    await userEvent.type(screen.getByLabelText('Why (optional)'), 'Left Acme');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(api.remove).toHaveBeenCalledWith('cm-1', 'n-anna', 'Left Acme'));
    expect(toast.success).toHaveBeenCalledWith('Anna Keller no longer holds a seat.');
  });

  it('offers seats and renewal only to whoever runs the members', async () => {
    as([PERMISSION.ManageMoney]);
    show();

    await screen.findByRole('article', { name: 'Acme corporate' });
    expect(screen.queryByLabelText('Give a seat to')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Renew' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove Anna Keller' })).not.toBeInTheDocument();
  });

  it('says a read that failed, and reads it again when asked', async () => {
    api.memberships.mockRejectedValueOnce(new Error('The server did not answer.')).mockResolvedValue([MEMBERSHIP]);
    show();

    const failed = await screen.findByRole('alert');
    expect(failed).toHaveTextContent('The corporate membership could not be loaded.');
    await userEvent.click(within(failed).getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('article', { name: 'Acme corporate' })).toBeInTheDocument();
  });

  it('shows nothing, and asks nothing, with the club turned off', () => {
    as([PERMISSION.ClubMembers], false);
    const { container } = show();

    expect(container).toBeEmptyDOMElement();
    expect(api.account).not.toHaveBeenCalled();
  });
});
