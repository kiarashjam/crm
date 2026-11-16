// The company page's delete. The server refuses to delete a company the club still keeps (its
// corporate membership, money on its house account, a private event to come) and says why; the
// page used to read the refusal as "not deleted" and say nothing at all.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/components/CustomFieldsCard', () => ({ default: () => null }));
vi.mock('@/app/components/AttachmentsCard', () => ({ default: () => null }));
vi.mock('@/app/components/corporate/ClientClubCard', () => ({ ClientClubCard: () => null }));
vi.mock('@/app/hooks/useModules', () => ({ useModules: () => ({ sales: false, club: true }) }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const api = vi.hoisted(() => ({ getContactsPaged: vi.fn(), getDealsPaged: vi.fn(), deleteCompany: vi.fn() }));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/apiClient', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/apiClient')>('@/app/api/apiClient')),
  servesDemoData: () => false,
  authFetchJson: async () => ({ id: 'acme', name: 'Acme AG', industry: 'Finance', createdAtUtc: '2026-01-01T00:00:00Z', version: 'v1' }),
}));

const { default: CompanyDetail } = await import('./CompanyDetail');

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/companies/acme']}>
    <Routes>
      <Route path="/companies/:id" element={<CompanyDetail />} />
      <Route path="/companies" element={<p>The companies</p>} />
    </Routes>
  </MemoryRouter>,
));

const deleteIt = async () => {
  await userEvent.click(await screen.findByRole('button', { name: 'Delete company' }));
  await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }));
};

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(toast)]) fn.mockReset();
  api.getContactsPaged.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
});

describe('deleting a company', () => {
  it("says why a company the club keeps cannot be deleted, in the server's words, and stays on it", async () => {
    const refusal = 'This company has a corporate membership at the club, and the club’s records would be left naming nobody, so it can’t be deleted. Merge it into the company it duplicates instead.';
    api.deleteCompany.mockRejectedValue(new Error(refusal));
    show();

    await deleteIt();

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(api.deleteCompany).toHaveBeenCalledWith('acme');
    expect(screen.queryByText('The companies')).not.toBeInTheDocument();
  });

  it('goes back to the companies once it is deleted', async () => {
    api.deleteCompany.mockResolvedValue(true);
    show();

    await deleteIt();

    expect(await screen.findByText('The companies')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Company deleted');
  });
});
