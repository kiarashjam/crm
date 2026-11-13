// The Companies page asks the server for its filters, its order, the figures above the list
// and the export. It filtered by industry and size, sorted by contacts and deals, and
// exported, over the twenty companies it had been sent.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { CompanyListItem, CompanySummary } from '@/app/api/companies';
import { withQueries } from '@/test/queryWrapper';

const rows: CompanyListItem[] = [
  { id: 'co1', name: 'Hotel Bellevue', industry: 'Hospitality', contactCount: 35, dealCount: 2, dealTotals: [{ currency: 'CHF', value: 40000, dealCount: 2 }] },
  { id: 'co2', name: 'Banque Léman', industry: 'Finance', contactCount: 0, dealCount: 0, dealTotals: [] },
];
const summary: CompanySummary = {
  total: 312, withContacts: 200, withDeals: 90, addedLast7Days: 4,
  dealTotals: [{ currency: 'CHF', value: 1250000, dealCount: 120 }],
  industries: [{ industry: 'Hospitality', count: 140 }, { industry: 'Finance', count: 60 }],
  withoutIndustry: 112,
};

const api = vi.hoisted(() => ({ getCompaniesPaged: vi.fn(), getDealsPaged: vi.fn(), deleteCompany: vi.fn() }));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
const companiesApi = vi.hoisted(() => ({ getCompanySummary: vi.fn(), exportCompanies: vi.fn() }));
vi.mock('@/app/api/companies', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/companies')>('@/app/api/companies')),
  ...companiesApi,
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, COMPANY_DUPLICATES: {} }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
// Which modules the workspace has on; both, unless a test turns one off.
const modules = vi.hoisted(() => ({ sales: true, club: true }));
vi.mock('@/app/hooks/useModules', () => ({ useModules: () => modules }));

const Companies = (await import('./Companies')).default;

const lastQuery = () => api.getCompaniesPaged.mock.calls[api.getCompaniesPaged.mock.calls.length - 1]![0];

function show(url = '/companies') {
  render(withQueries(
    <MemoryRouter initialEntries={[url]}>
      <Companies />
    </MemoryRouter>,
  ));
}

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(companiesApi)]) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  modules.sales = true;
  modules.club = true;
  api.getCompaniesPaged.mockResolvedValue({ items: rows, totalCount: 312, page: 1, pageSize: 20, totalPages: 16, hasNextPage: true, hasPreviousPage: false });
  api.getDealsPaged.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 500, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  companiesApi.getCompanySummary.mockResolvedValue(summary);
  companiesApi.exportCompanies.mockResolvedValue({ exported: 140, total: 140 });
});

describe('Companies', () => {
  it("shows the workspace's figures, and each row's own counts", async () => {
    show();
    await screen.findByText('Hotel Bellevue');
    const tile = (label: string) => screen.getByText(label).closest('li')!;
    await waitFor(() => expect(tile('Total Companies').textContent).toContain('312'));
    await waitFor(() => expect(tile('With Contacts').textContent).toContain('200'));
    expect(screen.getByRole('button', { name: /Top industry, Hospitality, 140 companies/ })).toBeTruthy();
  });

  it('reads the deals to list under each company', async () => {
    show();
    await screen.findByText('Hotel Bellevue');
    await waitFor(() => expect(api.getDealsPaged).toHaveBeenCalled());
  });

  it('reads no deals while Sales is turned off, as the server would refuse them', async () => {
    modules.sales = false;
    show();
    await screen.findByText('Hotel Bellevue');
    expect(api.getDealsPaged).not.toHaveBeenCalled();
  });

  it('asks the server for the filters and the order kept in the address', async () => {
    show('/companies?industry=Hospitality&size=11-50&sort=contacts-desc');
    await waitFor(() => expect(api.getCompaniesPaged).toHaveBeenCalled());
    expect(lastQuery()).toMatchObject({ industry: 'Hospitality', size: '11-50', sortBy: 'contacts', sortDir: 'desc', page: 1 });
    expect(lastQuery().hasIndustry).toBeUndefined();
  });

  it('asks for the companies with no industry', async () => {
    show('/companies?industry=__none__');
    await waitFor(() => expect(lastQuery()).toMatchObject({ hasIndustry: false }));
    expect(lastQuery().industry).toBeUndefined();
  });

  it('filters by the top industry across the whole workspace', async () => {
    show();
    fireEvent.click(await screen.findByRole('button', { name: /Top industry, Hospitality/ }));
    await waitFor(() => expect(lastQuery()).toMatchObject({ industry: 'Hospitality', page: 1 }));
  });

  it("says why a company the club keeps cannot be deleted, in the server's words", async () => {
    const refusal = 'This company has a corporate membership at the club, and the club’s records would be left naming nobody, so it can’t be deleted. Merge it into the company it duplicates instead.';
    api.deleteCompany.mockRejectedValue(new Error(refusal));
    show();
    await screen.findAllByText('Hotel Bellevue');
    fireEvent.pointerDown(screen.getAllByText('Actions for Hotel Bellevue')[0]!.closest('button')!, { button: 0, ctrlKey: false });
    fireEvent.click(await screen.findByRole('menuitem', { name: /Delete/ }));
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(api.deleteCompany).toHaveBeenCalledWith('co1');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('exports every company the list shows, through the server', async () => {
    show('/companies?industry=Hospitality');
    await screen.findByText('Hotel Bellevue');
    fireEvent.click(screen.getByRole('button', { name: 'Export the companies this list shows' }));
    await waitFor(() => expect(companiesApi.exportCompanies).toHaveBeenCalledTimes(1));
    expect(companiesApi.exportCompanies.mock.calls[0]![0]).toMatchObject({ industry: 'Hospitality', sortBy: 'name' });
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Exported 140 companies'));
  });

  it('says the companies could not be loaded, with a retry, instead of the empty first-day screen', async () => {
    api.getCompaniesPaged.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    show();

    expect(await screen.findByText('Companies could not be loaded.')).toBeTruthy();
    expect(screen.queryByText('Build Your Account Base')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Hotel Bellevue')).toBeTruthy();
  });

  it('still lists the companies when the deals their rows show cannot be read', async () => {
    // The deals failing used to blank the whole page.
    api.getDealsPaged.mockRejectedValue(new Error('timeout'));
    show();
    expect(await screen.findByText('Hotel Bellevue')).toBeTruthy();
    expect(screen.queryByText(/could not be loaded/)).toBeNull();
  });
});
