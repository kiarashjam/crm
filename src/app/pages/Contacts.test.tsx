// The Contacts page asks the server for every filter, the order, the figures above the
// list and the export. It used to receive twenty contacts and work all of that out from
// those twenty: "Total Contacts 20" for 640 people, a company filter that missed everyone
// on a later page, an export of one page, and no way to list archived contacts at all.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Contact } from '@/app/api/types';
import type { ContactStats } from '@/app/api/contacts';
import { withQueries } from '@/test/queryWrapper';

const people: Contact[] = [
  {
    id: 'c1', name: 'Anna Keller', email: 'anna@example.ch', createdAtUtc: '2026-10-01T10:00:00Z',
    phone: '+41 79 123 45 67', jobTitle: 'CFO', companyId: 'co1', version: 'v1',
  },
  { id: 'c2', name: 'Bruno Meier', email: 'bruno@example.ch', createdAtUtc: '2026-10-02T10:00:00Z' },
];
const archivedPerson: Contact = { id: 'c9', name: 'Old Member', email: 'old@example.ch', isArchived: true };
const stats: ContactStats = {
  total: 640, withCompany: 210, withPhone: 333, activeLast30Days: 120, quietOver90Days: 400, addedLast7Days: 12, archived: 3,
};

const api = vi.hoisted(() => ({
  getContactsPaged: vi.fn(), getCompanies: vi.fn(), getDealsPaged: vi.fn(), updateContact: vi.fn(), getContactById: vi.fn(),
  getSavedViews: vi.fn(),
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
// The figures above the list count up to their value, and the number shown while they
// count sits beside the final one screen readers get ("201" + "640"). With reduced
// motion each figure is shown at once, so no number caught mid-count can read as twenty.
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, CONTACT_DUPLICATES: {} }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
// Which modules the workspace has on; both, unless a test turns one off.
const modules = vi.hoisted(() => ({ sales: true, club: true }));
vi.mock('@/app/hooks/useModules', () => ({ useModules: () => modules }));

const Contacts = (await import('./Contacts')).default;

const lastQuery = () => api.getContactsPaged.mock.calls[api.getContactsPaged.mock.calls.length - 1]![0];

function show(url = '/contacts') {
  render(withQueries(
    <MemoryRouter initialEntries={[url]}>
      <Contacts />
    </MemoryRouter>,
  ));
}

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(contactsApi)]) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  modules.sales = true;
  modules.club = true;
  api.getContactsPaged.mockImplementation(async (q: { status?: string }) => (q.status === 'archived'
    ? { items: [archivedPerson], totalCount: 1, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false }
    : { items: people, totalCount: 640, page: 1, pageSize: 20, totalPages: 32, hasNextPage: true, hasPreviousPage: false }));
  api.getCompanies.mockResolvedValue([{ id: 'co1', name: 'Hotel Bellevue' }]);
  api.getDealsPaged.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  contactsApi.getContactStats.mockResolvedValue(stats);
  contactsApi.exportContacts.mockResolvedValue({ exported: 640, total: 640 });
  api.getSavedViews.mockResolvedValue([]);
});

describe('Contacts', () => {
  it('opens a saved view with its search, in the box and in what the server is asked', async () => {
    api.getSavedViews.mockResolvedValue([{
      id: 'v1', list: 'contacts', name: 'The Kellers', shared: false, mine: true, isDefault: false,
      query: { search: 'Keller', status: 'archived' }, columns: null, version: 'v1', updatedAtUtc: '2026-11-13T00:00:00Z',
    }]);
    const user = userEvent.setup();
    show();
    await screen.findByText('Anna Keller');
    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /^The Kellers/ }));
    await waitFor(() => expect(lastQuery()).toMatchObject({ search: 'Keller', status: 'archived' }));
    // And it stays so once the page has written its search into the address.
    await new Promise((resolve) => setTimeout(resolve, 500));
    expect(lastQuery()).toMatchObject({ search: 'Keller', status: 'archived' });
    expect(screen.getByLabelText('Search contacts')).toHaveValue('Keller');
  });

  it("shows the workspace's figures, not one page's", async () => {
    show();
    expect(await screen.findByRole('button', { name: /Archived, 3/ })).toBeTruthy();
    const tile = (label: string) => screen.getByText(label).closest('li')!;
    await waitFor(() => expect(tile('Total Contacts').textContent).toContain('640'));
    expect(tile('Total Contacts').textContent).not.toContain('20');
    await waitFor(() => expect(screen.getByRole('button', { name: /Need attention, 400/ }).textContent).toContain('400'));
  });

  it('reads the deals beside each contact, and shows how many in the table', async () => {
    show();
    await screen.findByText('Anna Keller');
    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    expect(await screen.findByRole('columnheader', { name: 'Deals' })).toBeTruthy();
    await waitFor(() => expect(api.getDealsPaged).toHaveBeenCalled());
  });

  it('asks the server for no deals while Sales is turned off, which would refuse them, and drops their column', async () => {
    modules.sales = false;
    show();
    await screen.findByText('Anna Keller');
    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    expect(await screen.findByRole('columnheader', { name: 'Email' })).toBeTruthy();
    expect(screen.queryByRole('columnheader', { name: 'Deals' })).toBeNull();
    expect(api.getDealsPaged).not.toHaveBeenCalled();
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

  it('says the contacts could not be loaded, with a retry, instead of the empty first-day screen', async () => {
    api.getContactsPaged.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    contactsApi.getContactStats.mockRejectedValueOnce(new Error('offline'));
    show();

    expect(await screen.findByText('Contacts could not be loaded.')).toBeTruthy();
    expect(screen.queryByText('Build Your Contact Network')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Anna Keller')).toBeTruthy();
  });

  it("still lists the contacts when their rows' deals and the companies cannot be read", async () => {
    // One slow side list used to blank the whole page.
    api.getDealsPaged.mockRejectedValue(new Error('timeout'));
    api.getCompanies.mockRejectedValue(new Error('timeout'));
    show();
    expect(await screen.findByText('Anna Keller')).toBeTruthy();
    expect(screen.queryByText(/could not be loaded/)).toBeNull();
  });

  it('cancels the read for a search typed over, so it cannot land over the newer one', async () => {
    show();
    await screen.findByText('Anna Keller');
    const signals = new Map<string, AbortSignal>();
    api.getContactsPaged.mockImplementation((q: { search?: string }, req?: { signal?: AbortSignal }) => {
      if (q.search && req?.signal) signals.set(q.search, req.signal);
      // The earlier search never answers on its own.
      return q.search === 'kel'
        ? new Promise(() => {})
        : Promise.resolve({ items: people.slice(0, 1), totalCount: 1, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false });
    });

    const box = screen.getByLabelText('Search contacts');
    fireEvent.change(box, { target: { value: 'kel' } });
    await waitFor(() => expect(signals.has('kel')).toBe(true));
    fireEvent.change(box, { target: { value: 'keller' } });
    await waitFor(() => expect(signals.has('keller')).toBe(true));

    await waitFor(() => expect(signals.get('kel')!.aborted).toBe(true));
    expect(signals.get('keller')!.aborted).toBe(false);
  });

  describe('editing a contact', () => {
    const openEdit = async () => {
      show();
      await screen.findByText('Anna Keller');
      fireEvent.pointerDown(screen.getByText('Actions for Anna Keller').closest('button')!, { button: 0, ctrlKey: false });
      fireEvent.click(await screen.findByRole('menuitem', { name: /Edit/ }));
      return screen.findByLabelText(/Phone/);
    };

    it('sends an emptied field as null so it is taken off, with the version it was opened on', async () => {
      api.updateContact.mockResolvedValue({ contact: { ...people[0], phone: undefined, jobTitle: undefined }, error: null });
      // The contact's company is not among the companies the form offers.
      api.getCompanies.mockResolvedValue([{ id: 'co2', name: 'Hotel Schweizerhof' }]);
      const phone = await openEdit();

      fireEvent.change(phone, { target: { value: '' } });
      fireEvent.change(screen.getByLabelText(/Job Title/), { target: { value: '' } });
      fireEvent.click(screen.getByRole('button', { name: /Update Contact/ }));

      await waitFor(() => expect(api.updateContact).toHaveBeenCalledTimes(1));
      expect(api.updateContact.mock.calls[0]).toEqual(['c1', expect.objectContaining({
        phone: null, jobTitle: null, version: 'v1',
        // Still linked: a company the form's list does not hold is kept, not taken off.
        companyId: 'co1',
      })]);
      await waitFor(() => expect(toast.success).toHaveBeenCalled());
    });

    it('says someone else changed the contact, and Reload opens the latest copy', async () => {
      api.updateContact.mockResolvedValue({
        contact: null, conflict: true,
        error: 'Someone else changed this contact after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.',
      });
      api.getContactById.mockResolvedValue({ ...people[0], jobTitle: 'Chief Financial Officer', version: 'v2' });
      await openEdit();

      fireEvent.change(screen.getByLabelText(/Job Title/), { target: { value: 'Treasurer' } });
      fireEvent.click(screen.getByRole('button', { name: /Update Contact/ }));

      await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(1));
      const [message, options] = toast.error.mock.calls[0] as [string, { action: { label: string; onClick: () => void } }];
      expect(message).toMatch(/someone else changed this contact/i);
      expect(options.action.label).toBe('Reload');
      expect(toast.success).not.toHaveBeenCalled();

      options.action.onClick();
      expect(await screen.findByDisplayValue('Chief Financial Officer')).toBeTruthy();
      api.updateContact.mockResolvedValue({ contact: people[0], error: null });
      fireEvent.click(screen.getByRole('button', { name: /Update Contact/ }));
      await waitFor(() => expect(api.updateContact).toHaveBeenCalledTimes(2));
      expect(api.updateContact.mock.calls[1]![1]).toMatchObject({ jobTitle: 'Chief Financial Officer', version: 'v2' });
    });
  });
});
