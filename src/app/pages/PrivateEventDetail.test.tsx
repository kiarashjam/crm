// One private event's page: its menu and money as the server keeps them, the deposit and invoice
// offered only to whoever handles the money, cancelling asking what becomes of a deposit asked,
// the door checking guests in, and a page that could not be read said so with a retry.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { PrivateEvent } from '@/app/api/corporate';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
};

const api = vi.hoisted(() => ({
  get: vi.fn<(id: string) => Promise<PrivateEvent | null>>(),
  deposit: vi.fn<(id: string, amount: number) => Promise<PrivateEvent>>(),
  cancel: vi.fn<(id: string, reason: string, deposit?: string) => Promise<PrivateEvent>>(),
  checkIn: vi.fn<(id: string, guestId: string, undo?: boolean) => Promise<PrivateEvent>>(),
}));
vi.mock('@/app/api/corporate', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/corporate')>()),
  getPrivateEvent: (id: string) => api.get(id),
  requestPrivateEventDeposit: (id: string, amount: number) => api.deposit(id, amount),
  cancelPrivateEvent: (id: string, reason: string, deposit?: string) => api.cancel(id, reason, deposit),
  checkInPrivateEventGuest: (id: string, guestId: string, undo?: boolean) => api.checkIn(id, guestId, undo),
}));
// The pickers' lists are not what these tests are about.
vi.mock('@/app/api/catalog', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/catalog')>()), getCatalog: async () => [] }));
vi.mock('@/app/api/members', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/members')>()), getMembers: async () => [] }));
vi.mock('@/app/api/reservations', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/reservations')>()), getResources: async () => [] }));

const { default: PrivateEventDetail } = await import('./PrivateEventDetail');

const PARTY: PrivateEvent = {
  id: 'pe-1', title: 'Acme summer party', companyId: 'acme', companyName: 'Acme AG', startAtUtc: '2026-12-13T17:00:00Z', endAtUtc: '2026-12-13T22:00:00Z',
  guestCount: 30, status: 'Enquiry', currency: 'CHF',
  spaces: [{ reservationId: 'r-1', resourceId: 'room-1', resourceName: 'Private dining room', status: 'Pending' }],
  lines: [{ id: 'l-1', name: 'Chef’s dinner', quantity: 30, unitPrice: 120, total: 3600, vatRate: 8.1, catalogItemId: 'cat-1' }],
  guests: [{ id: 'g-tom', name: 'Tom Berger' }],
  guestsCheckedIn: 0,
  money: { billToKind: 'company', billToAccountId: 'acme', billToName: 'Acme AG', linesTotal: 3600, depositState: 'none', depositPaid: 0, invoiced: false, invoiceChargeIds: [], invoiceTotal: 0, invoicePaid: 0 },
  createdAtUtc: '2026-11-13T10:00:00Z', version: 'v1',
};

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/private-events/pe-1']}>
    <Routes><Route path="/private-events/:id" element={<PrivateEventDetail />} /></Routes>
  </MemoryRouter>,
));

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.get.mockResolvedValue(PARTY);
  as([PERMISSION.ClubMembers, PERMISSION.ManageMoney, PERMISSION.FrontDesk, PERMISSION.EditRecords]);
});

describe("a private event's page", () => {
  it('shows its client, spaces held, menu and what it comes to', async () => {
    show();

    expect(await screen.findByRole('heading', { name: 'Acme summer party' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Acme AG' })).toHaveAttribute('href', '/companies/acme');
    expect(screen.getByText('Private dining room (Pending)')).toBeInTheDocument();
    expect(screen.getByText('Chef’s dinner')).toBeInTheDocument();
    expect(screen.getAllByText(/3.600/).length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith('pe-1');
  });

  it('asks the deposit for whoever handles the money', async () => {
    api.deposit.mockResolvedValue({ ...PARTY, money: { ...PARTY.money, depositAmount: 1000, depositState: 'charged' } });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Ask a deposit' }));
    await userEvent.type(screen.getByLabelText('Amount (CHF)'), '1000');
    await userEvent.click(screen.getByRole('button', { name: 'Ask the deposit' }));

    await waitFor(() => expect(api.deposit).toHaveBeenCalledWith('pe-1', 1000));
    expect(toast.success).toHaveBeenCalledWith('The deposit is on Acme AG’s account.');
  });

  it('offers no deposit or invoice to someone who cannot handle money, and says who does', async () => {
    as([PERMISSION.ClubMembers]);
    show();

    await screen.findByRole('heading', { name: 'Acme summer party' });
    expect(screen.queryByRole('button', { name: 'Ask a deposit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Post the final invoice' })).not.toBeInTheDocument();
    expect(screen.getByText(/done by whoever handles the club’s money/)).toBeInTheDocument();
  });

  it('asks what becomes of a deposit asked before it cancels, and sends the choice', async () => {
    api.get.mockResolvedValue({ ...PARTY, money: { ...PARTY.money, depositAmount: 500, depositState: 'paid', depositPaid: 500 } });
    api.cancel.mockResolvedValue({ ...PARTY, status: 'Cancelled' });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel the event' }));
    const dialog = screen.getByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Why'), 'Acme postponed');
    const confirm = within(dialog).getByRole('button', { name: 'Cancel the event' });
    expect(confirm).toBeDisabled();
    await userEvent.click(within(dialog).getByLabelText('Give it back as house credit'));
    await userEvent.click(confirm);

    await waitFor(() => expect(api.cancel).toHaveBeenCalledWith('pe-1', 'Acme postponed', 'release'));
  });

  it('lets the door check a guest in, and nothing more', async () => {
    as([PERMISSION.FrontDesk]);
    api.checkIn.mockResolvedValue({ ...PARTY, guests: [{ id: 'g-tom', name: 'Tom Berger', checkedInAtUtc: '2026-12-13T17:30:00Z' }], guestsCheckedIn: 1 });
    show();

    await userEvent.click(await screen.findByRole('button', { name: 'Check Tom Berger in' }));

    await waitFor(() => expect(api.checkIn).toHaveBeenCalledWith('pe-1', 'g-tom', undefined));
    expect(screen.queryByRole('button', { name: 'Take Tom Berger off the list' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
  });

  it('says a page that could not be read, and reads it again when asked', async () => {
    api.get.mockRejectedValueOnce(new Error('The server did not answer.')).mockResolvedValue(PARTY);
    show();

    const failed = await screen.findByRole('alert');
    expect(failed).toHaveTextContent('The server did not answer.');
    await userEvent.click(within(failed).getByRole('button', { name: /Try again/ }));

    expect(await screen.findByRole('heading', { name: 'Acme summer party' })).toBeInTheDocument();
  });
});
