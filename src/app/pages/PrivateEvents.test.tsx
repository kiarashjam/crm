// The private events page: the server's events as they are, a new one made for the company it was
// asked from (with the spaces to hold), a clash with a booking said as the server says it, and a
// list that could not be read said so with a way to try again.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { PrivateEvent, PrivateEventInput } from '@/app/api/corporate';
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
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich', salesEnabled: false },
  };
};

const api = vi.hoisted(() => ({
  list: vi.fn<(query: unknown) => Promise<PrivateEvent[]>>(),
  create: vi.fn<(input: PrivateEventInput) => Promise<PrivateEvent>>(),
}));
vi.mock('@/app/api/corporate', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/corporate')>()),
  getPrivateEvents: (query: unknown) => api.list(query),
  createPrivateEvent: (input: PrivateEventInput) => api.create(input),
}));
vi.mock('@/app/api/companies', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/companies')>()),
  getCompanies: async () => [{ id: 'acme', name: 'Acme AG' }, { id: 'bolt', name: 'Bolt GmbH' }],
}));
vi.mock('@/app/api/contacts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/contacts')>()),
  getContacts: async () => [{ id: 'lena', name: 'Lena Graf' }],
}));
vi.mock('@/app/api/reservations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/reservations')>()),
  getResources: async () => [{ id: 'room-1', name: 'Private dining room', kind: 'Dining', capacity: 24 }],
}));

const { default: PrivateEvents } = await import('./PrivateEvents');

const PARTY: PrivateEvent = {
  id: 'pe-1', title: 'Acme summer party', companyId: 'acme', companyName: 'Acme AG', startAtUtc: '2026-12-13T17:00:00Z', endAtUtc: '2026-12-13T22:00:00Z',
  guestCount: 30, status: 'Confirmed', currency: 'CHF',
  spaces: [{ reservationId: 'r-1', resourceId: 'room-1', resourceName: 'Private dining room', status: 'Confirmed' }],
  lines: [], guests: [], guestsCheckedIn: 0,
  money: { linesTotal: 4650, depositState: 'none', depositPaid: 0, invoiced: false, invoiceChargeIds: [], invoiceTotal: 0, invoicePaid: 0 },
  createdAtUtc: '2026-11-13T10:00:00Z', version: 'v1',
};

const show = (path = '/private-events') => render(withQueries(
  <MemoryRouter initialEntries={[path]}>
    <Routes>
      <Route path="/private-events" element={<PrivateEvents />} />
      <Route path="/private-events/:id" element={<p>The event page</p>} />
    </Routes>
  </MemoryRouter>,
));

beforeEach(() => {
  api.list.mockReset();
  api.create.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.list.mockResolvedValue([PARTY]);
  as([PERMISSION.ClubMembers]);
});

describe('the private events page', () => {
  it("lists the events coming up, with their client, spaces and menu's worth", async () => {
    show();

    const row = (await screen.findByRole('link', { name: 'Acme summer party' })).closest('tr')!;
    expect(row).toHaveTextContent('Acme AG');
    expect(row).toHaveTextContent('Private dining room');
    expect(row).toHaveTextContent('Confirmed');
    expect(row).toHaveTextContent(/4.650/);
    expect(api.list).toHaveBeenCalledWith(expect.objectContaining({ fromUtc: expect.any(String) }));
  });

  it('makes an event for the company it was asked from, holding the spaces chosen, and opens it', async () => {
    api.create.mockResolvedValue({ ...PARTY, id: 'pe-2', title: 'Acme board dinner', status: 'Enquiry' });
    show('/private-events?new=1&companyId=acme');

    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Company')).toHaveValue('acme'));
    await userEvent.type(within(dialog).getByLabelText('Title'), 'Acme board dinner');
    await userEvent.click(await within(dialog).findByLabelText('Private dining room'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Make the event' }));

    await waitFor(() => expect(api.create).toHaveBeenCalledWith(expect.objectContaining({
      title: 'Acme board dinner', companyId: 'acme', resourceIds: ['room-1'],
    })));
    expect(await screen.findByText('The event page')).toBeInTheDocument();
  });

  it("says the server's refusal when a space is taken, and keeps what was typed", async () => {
    api.create.mockRejectedValue(new ApiError('Private dining room is already booked 19:00 to 20:00 by Anna Keller (confirmed), so it can’t take this booking.', 409, undefined, true, 'Reservation.Overlap.Conflict'));
    show('/private-events?new=1&companyId=acme');

    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Company')).toHaveValue('acme'));
    await userEvent.type(within(dialog).getByLabelText('Title'), 'Acme board dinner');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Make the event' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringContaining('Private dining room is already booked')));
    expect(within(screen.getByRole('dialog')).getByLabelText('Title')).toHaveValue('Acme board dinner');
  });

  it('offers no new event to someone who does not run the members', async () => {
    as([PERMISSION.FrontDesk]);
    show();

    await screen.findByRole('link', { name: 'Acme summer party' });
    expect(screen.queryByRole('button', { name: /New private event/ })).not.toBeInTheDocument();
  });

  it('says a list that could not be read, and reads it again when asked', async () => {
    api.list.mockRejectedValueOnce(new Error('The server did not answer.')).mockResolvedValue([PARTY]);
    show();

    const failed = await screen.findByRole('alert');
    await userEvent.click(within(failed).getByRole('button', { name: /Try again/ }));

    expect(await screen.findByRole('link', { name: 'Acme summer party' })).toBeInTheDocument();
  });
});
