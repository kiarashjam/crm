// The Contacts page asks the server for every filter, the order, the figures above the
// list and the export. It used to receive twenty contacts and work all of that out from
// those twenty: "Total Contacts 20" for 640 people, a company filter that missed everyone
// on a later page, an export of one page, and no way to list archived contacts at all.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Contact } from '@/app/api/types';
import type { ContactStats } from '@/app/api/contacts';

const people: Contact[] = [
  { id: 'c1', name: 'Anna Keller', email: 'anna@example.ch', createdAtUtc: '2026-10-01T10:00:00Z' },
  { id: 'c2', name: 'Bruno Meier', email: 'bruno@example.ch', createdAtUtc: '2026-10-02T10:00:00Z' },
];
const archivedPerson: Contact = { id: 'c9', name: 'Old Member', email: 'old@example.ch', isArchived: true };
const stats: ContactStats = {
  total: 640, withCompany: 210, withPhone: 333, activeLast30Days: 120, quietOver90Days: 400, addedLast7Days: 12, archived: 3,
};

const api = vi.hoisted(() => ({
  getContactsPaged: vi.fn(), getCompanies: vi.fn(), getDealsPaged: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
const contactsApi = vi.hoisted(() => ({
  getContactStats: vi.fn(), exportContacts: vi.fn(), archiveContact: vi.fn(), unarchiveContact: vi.fn(),
}));
vi.mock('@/app/api/contacts', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/contacts')>('@/app/api/contacts')),
  ...contactsApi,
}));
vi.mock('@/app/api/tasks', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/tasks')>('@/app/api/tasks')),
  getTasks: async () => [],
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, CONTACT_DUPLICATES: {} }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Contacts = (await import('./Contacts')).default;

const lastQuery = () => api.getContactsPaged.mock.calls[api.getContactsPaged.mock.calls.length - 1]![0];

function show(url = '/contacts') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Contacts />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(contactsApi)]) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  api.getContactsPaged.mockImplementation(async (q: { status?: string }) => (q.status === 'archived'
    ? { items: [archivedPerson], totalCount: 1, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false }
    : { items: people, totalCount: 640, page: 1, pageSize: 20, totalPages: 32, hasNextPage: true, hasPreviousPage: false }));
  api.getCompanies.mockResolvedValue([{ id: 'co1', name: 'Hotel Bellevue' }]);
  api.getDealsPaged.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  contactsApi.getContactStats.mockResolvedValue(stats);
  contactsApi.exportContacts.mockResolvedValue({ exported: 640, total: 640 });
});

describe('Contacts', () => {
  it("shows the workspace's figures, not one page's", async () => {
    show();
    expect(await screen.findByRole('button', { name: /Archived, 3/ })).toBeTruthy();
    const tile = (label: string) => screen.getByText(label).closest('li')!;
    await waitFor(() => expect(tile('Total Contacts').textContent).toContain('640'));
    expect(tile('Total Contacts').textContent).not.toContain('20');
    await waitFor(() => expect(screen.getByRole('button', { name: /Need attention, 400/ }).textContent).toContain('400'));
  });

  it('asks the server for the filters and the order kept in the address', async () => {
    show('/contacts?company=some&activity=quiet&sort=lastActivity-desc&search=keller');
    await waitFor(() => expect(api.getContactsPaged).toHaveBeenCalled());
    expect(lastQuery()).toMatchObject({
      search: 'keller', status: 'active', hasCompany: true, activity: 'quiet', sortBy: 'lastActivity', sortDir: 'desc',
    });
    expect(lastQuery().companyId).toBeUndefined();
  });

  it('asks for the contacts of one company by its id', async () => {
    show('/contacts?company=co1');
    await waitFor(() => expect(lastQuery()).toMatchObject({ companyId: 'co1' }));
    expect(lastQuery().hasCompany).toBeUndefined();
  });

  it('lists archived contacts, so one can be found and restored', async () => {
    show();
    await screen.findByText('Anna Keller');
    fireEvent.click(within(screen.getByRole('group', { name: 'Show contacts' })).getByRole('button', { name: 'Archived' }));

    expect(await screen.findByText('Old Member')).toBeTruthy();
    expect(lastQuery()).toMatchObject({ status: 'archived', page: 1 });
    expect(screen.queryByText('Anna Keller')).toBeNull();
  });

  it('says why an archived contact cannot be restored', async () => {
    contactsApi.unarchiveContact.mockRejectedValue(new Error("An active contact already uses 'old@example.ch', so this one cannot be restored beside it."));
    show('/contacts?status=archived');
    await screen.findByText('Old Member');
    const trigger = screen.getByText('Actions for Old Member').closest('button')!;
    fireEvent.pointerDown(trigger, { button: 0, ctrlKey: false });
    fireEvent.click(await screen.findByRole('menuitem', { name: /Unarchive/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("An active contact already uses 'old@example.ch', so this one cannot be restored beside it."));
    expect(contactsApi.unarchiveContact).toHaveBeenCalledWith('c9');
  });

  it('exports every contact the list shows, through the server', async () => {
    show('/contacts?status=all&company=co1');
    await screen.findByText('Anna Keller');
    fireEvent.click(screen.getByRole('button', { name: 'Export the contacts this list shows' }));

    await waitFor(() => expect(contactsApi.exportContacts).toHaveBeenCalledTimes(1));
    expect(contactsApi.exportContacts.mock.calls[0]![0]).toMatchObject({ status: 'all', companyId: 'co1', sortBy: 'name', sortDir: 'asc' });
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Exported 640 contacts'));
  });

  it('says when an export holds only the first rows of a longer list', async () => {
    contactsApi.exportContacts.mockResolvedValue({ exported: 50000, total: 61200 });
    show();
    await screen.findByText('Anna Keller');
    fireEvent.click(screen.getByRole('button', { name: 'Export the contacts this list shows' }));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(`Exported the first ${(50000).toLocaleString()} of ${(61200).toLocaleString()} contacts`));
  });
});
