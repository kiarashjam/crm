// The house-charge screen: a sale rung up from the price catalogue, line by line with each
// line's VAT rate, how the member paid, and a reason for any discount, posted to the server.
//
// It used to post one charge typed in by hand, with no VAT and nothing to say a bill was paid
// at the table, beside gift card and points tiles that were switched off. Now the server prices
// the lines, posts them through the ledger and gives the points; the screen sends what was rung
// up and shows what the server answered.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { Member } from '@/app/api/members';
import type { CatalogItem } from '@/app/api/catalog';
import type { PosStatus } from '@/app/api/pos';
import type { Sale } from '@/app/api/sales';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -50,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));
vi.mock('@/app/api/loyalty', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/loyalty')>()),
  getLoyaltyLedger: async () => [],
}));

const item = (id: string, kind: CatalogItem['kind'], name: string, price: number, vatRate: number, isActive = true): CatalogItem => ({
  id, kind, name, price, vatRate, currency: 'CHF', benefits: [], chargeKind: kind === 'Product' ? 'Retail' : 'Bar', isActive, sortOrder: 0,
  createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', version: 'v1',
});
const CATALOG: CatalogItem[] = [
  item('cat-negroni', 'MenuItem', 'Negroni', 18.5, 8.1),
  item('cat-book', 'Product', 'Club history', 40, 2.6),
  item('cat-gold', 'Membership', 'Gold', 299, 0),
  item('cat-old', 'MenuItem', 'Old cocktail', 12, 8.1, false),
];
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: async () => CATALOG,
}));

const pos = vi.hoisted(() => ({ status: { provider: 'lightspeed-k', serverConfigured: true, connected: false, importing: false, needsAttention: 0 } as PosStatus }));
vi.mock('@/app/api/pos', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/pos')>()),
  getPosStatus: async () => pos.status,
}));

const sales = vi.hoisted(() => ({ post: vi.fn(), list: vi.fn() }));
vi.mock('@/app/api/sales', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/sales')>()),
  postSale: (...a: unknown[]) => sales.post(...a),
  getSales: (...a: unknown[]) => sales.list(...a),
}));

const PosTerminal = (await import('./PosTerminal')).default;

const POSTED: Sale = {
  id: 's1', memberId: 'm1', memberName: 'Jean Dupont', source: 'desk', venue: 'Bar', tender: 'HouseAccount',
  subtotal: 77, discount: 0, total: 77, paidAmount: 0, owedAmount: 77, currency: 'CHF', pointsAwarded: 1155,
  chargeIds: ['c1', 'c2'], paymentIds: [], soldAtUtc: '2026-11-10T18:00:00Z', createdAtUtc: '2026-11-10T18:00:00Z', lines: [],
};

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  sales.post.mockReset();
  sales.list.mockReset();
  sales.list.mockResolvedValue([]);
  pos.status = { provider: 'lightspeed-k', serverConfigured: true, connected: false, importing: false, needsAttention: 0 };
});

async function openJean() {
  const user = userEvent.setup();
  render(withQueries(<MemoryRouter><PosTerminal /></MemoryRouter>));
  await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));
  return user;
}

describe('the house-charge screen', () => {
  it('rings up catalogue items with their VAT and puts them on the house account', async () => {
    sales.post.mockResolvedValue(POSTED);
    const user = await openJean();

    // Only what is on sale and sold at a till: no tier, nothing taken off sale.
    expect(await screen.findByRole('button', { name: 'Add Negroni' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add Gold' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Add Old cocktail' })).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Add Negroni' }));
    await user.click(screen.getByRole('button', { name: 'Add Negroni' }));
    await user.click(screen.getByRole('button', { name: 'Add Club history' }));
    expect(screen.getByLabelText('Quantity of Negroni')).toHaveValue(2);
    const total = screen.getByLabelText('Sale total');
    expect(within(total).getByText(/VAT 8\.1% included/)).toBeInTheDocument();
    expect(within(total).getByText(/VAT 2\.6% included/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Put .*77.* on the house account/ }));

    await waitFor(() => expect(sales.post).toHaveBeenCalledTimes(1));
    const [input, key] = sales.post.mock.calls[0]!;
    expect(input).toEqual({
      memberId: 'm1', venue: 'Bar', tender: 'HouseAccount',
      lines: [{ catalogItemId: 'cat-negroni', quantity: 2 }, { catalogItemId: 'cat-book', quantity: 1 }],
      discountAmount: undefined, discountPercent: undefined, discountReason: undefined, notes: undefined,
    });
    expect(key).toMatch(/^desk:/);
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/Jean Dupont's house account · \+1,?155 points/)));
    // The sale is done: the screen is ready for the next one.
    expect(screen.queryByLabelText('Quantity of Negroni')).toBeNull();
  });

  it('records a sale paid by card at the till as paid', async () => {
    sales.post.mockResolvedValue({ ...POSTED, tender: 'Card', paidAmount: 18.5, owedAmount: 0, total: 18.5, pointsAwarded: 277 });
    const user = await openJean();
    await user.click(await screen.findByRole('button', { name: 'Add Negroni' }));
    await user.click(screen.getByRole('button', { name: 'Card' }));
    await user.click(screen.getByRole('button', { name: /Record .*18.50.* paid by Card/ }));

    await waitFor(() => expect(sales.post).toHaveBeenCalledWith(expect.objectContaining({ tender: 'Card' }), expect.any(String)));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/paid by Card · \+277 points/)));
  });

  it('asks for a reason before posting a discount, and sends the reason with it', async () => {
    sales.post.mockResolvedValue(POSTED);
    const user = await openJean();
    await user.click(await screen.findByRole('button', { name: 'Add Club history' }));
    await user.click(within(screen.getByRole('group', { name: 'Discount' })).getByRole('button', { name: '%' }));
    await user.type(screen.getByLabelText('Discount (%)'), '10');

    const post = screen.getByRole('button', { name: /Put .*36.* on the house account/ });
    expect(post).toBeDisabled();
    expect(screen.getByText('Say why the discount was given.')).toBeInTheDocument();

    await user.type(screen.getByLabelText(/Reason for the discount/), 'Birthday');
    expect(post).toBeEnabled();
    await user.click(post);
    await waitFor(() => expect(sales.post).toHaveBeenCalledWith(
      expect.objectContaining({ discountPercent: 10, discountReason: 'Birthday', discountAmount: undefined }), expect.any(String)));
  });

  it('keeps the sale and its key when the server refuses, so trying again cannot post it twice', async () => {
    sales.post.mockRejectedValueOnce(new Error('Negroni is no longer on sale, so it can\'t be charged.')).mockResolvedValueOnce(POSTED);
    const user = await openJean();
    await user.click(await screen.findByRole('button', { name: 'Add Negroni' }));
    await user.click(screen.getByRole('button', { name: /on the house account/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Negroni is no longer on sale, so it can\'t be charged.'));
    expect(screen.getByLabelText('Quantity of Negroni')).toHaveValue(1);
    await user.click(screen.getByRole('button', { name: /on the house account/ }));
    await waitFor(() => expect(sales.post).toHaveBeenCalledTimes(2));
    expect(sales.post.mock.calls[1]![1]).toBe(sales.post.mock.calls[0]![1]);
  });

  it('offers no gift card or points tile that pays nothing, and keeps check-in and tabs', async () => {
    await openJean();
    expect(screen.queryByRole('button', { name: /Redeem gift card/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Redeem points/ })).toBeNull();
    expect(screen.getByRole('button', { name: /Check in/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Open tab/ })).toBeEnabled();
  });

  it('says when Lightspeed brings the tickets in on its own, and how many wait for staff', async () => {
    pos.status = { provider: 'lightspeed-k', serverConfigured: true, connected: true, importing: true, locationName: 'Pavillon 46 Restaurant', needsAttention: 2 };
    render(withQueries(<MemoryRouter><PosTerminal /></MemoryRouter>));
    expect(await screen.findByText(/tickets from Pavillon 46 Restaurant come in on their own/)).toBeInTheDocument();
    expect(screen.getByText(/2 tickets wait for staff/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /POS tickets/ })).toHaveAttribute('href', '/pos-tickets');
  });
});
