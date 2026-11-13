// What a person sees when the server refuses a change to a deal's line items.
//
// Line items now live on the server, which refuses some changes: a Viewer's, a
// quantity of 0 left behind by a cleared field. The card saved inline edits
// fire-and-forget and had no catch anywhere, because the old browser-only fallback
// swallowed every failure. Against the server, a refusal would have vanished and
// the screen would have kept showing a number that was never saved.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import DealLineItemsCard from './DealLineItemsCard';

const line = { id: 'li-1', dealId: 'deal-1', name: 'Growth plan', quantity: 2, unitPrice: 4800 };

const api = vi.hoisted(() => ({
  getProducts: vi.fn(),
  getDealLineItems: vi.fn(),
  addDealLineItem: vi.fn(),
  updateDealLineItem: vi.fn(),
  deleteDealLineItem: vi.fn(),
  updateDeal: vi.fn(),
  lineItemsTotal: (items: { quantity: number; unitPrice: number }[]) => items.reduce((s, l) => s + l.quantity * l.unitPrice, 0),
}));
vi.mock('@/app/api', () => api);

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

beforeEach(() => {
  vi.clearAllMocks();
  api.getProducts.mockResolvedValue([]);
  api.getDealLineItems.mockResolvedValue([line]);
});

describe('DealLineItemsCard', () => {
  it("puts back the server's value when an edit is refused, and says why", async () => {
    api.updateDealLineItem.mockRejectedValue(new Error('Quantity must be more than 0'));
    render(<DealLineItemsCard dealId="deal-1" />);
    const quantity = await screen.findByLabelText('Quantity of Growth plan');

    fireEvent.change(quantity, { target: { value: '' } });
    fireEvent.blur(quantity, { target: { value: '' } });

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Quantity must be more than 0'));
    await waitFor(() => expect(screen.getByLabelText('Quantity of Growth plan')).toHaveValue(2));
  });

  it('brings a line back when removing it is refused', async () => {
    api.deleteDealLineItem.mockRejectedValue(new Error('You have read-only access to this organization.'));
    render(<DealLineItemsCard dealId="deal-1" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Remove Growth plan' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('You have read-only access to this organization.'));
    expect(await screen.findByLabelText('Quantity of Growth plan')).toBeInTheDocument();
  });

  it("sets the deal's amount to the cent, which the old text field refused", async () => {
    // 7 x 14.20 is 99.39999999999999 in floating point.
    api.getDealLineItems.mockResolvedValue([{ ...line, name: 'Canapé platter', quantity: 7, unitPrice: 14.2 }]);
    api.updateDeal.mockResolvedValue({ id: 'deal-1' });
    const synced = vi.fn();
    render(<DealLineItemsCard dealId="deal-1" currency="CHF" onValueSynced={synced} />);

    fireEvent.click(await screen.findByRole('button', { name: /set deal value/i }));

    await waitFor(() => expect(api.updateDeal).toHaveBeenCalledWith('deal-1', { amount: 99.4 }));
    expect(synced).toHaveBeenCalledWith(99.4);
  });

  it("saves a line's VAT rate, and takes it off with the dash", async () => {
    // A contract drafted on the deal prints each line's rate in its fee table, so the rate is
    // kept on the line; "—" is no rate given, which is not the same as 0 %.
    api.updateDealLineItem.mockResolvedValue({ ...line, vatRate: 8.1 });
    render(<DealLineItemsCard dealId="deal-1" currency="CHF" />);
    const rate = await screen.findByLabelText('VAT rate of Growth plan');

    fireEvent.change(rate, { target: { value: '8.1' } });
    await waitFor(() => expect(api.updateDealLineItem).toHaveBeenCalledWith('li-1', { vatRate: 8.1 }));

    fireEvent.change(rate, { target: { value: '' } });
    await waitFor(() => expect(api.updateDealLineItem).toHaveBeenLastCalledWith('li-1', { vatRate: null }));
  });

  it('adds a line with the VAT rate picked for it', async () => {
    api.addDealLineItem.mockResolvedValue({ id: 'li-2', dealId: 'deal-1', name: 'Locker', quantity: 1, unitPrice: 120, vatRate: 8.1 });
    render(<DealLineItemsCard dealId="deal-1" currency="CHF" />);
    await screen.findByLabelText('Quantity of Growth plan');

    fireEvent.change(screen.getByPlaceholderText('Item name'), { target: { value: 'Locker' } });
    fireEvent.change(screen.getByLabelText('Unit price'), { target: { value: '120' } });
    fireEvent.change(screen.getByLabelText('VAT rate'), { target: { value: '8.1' } });
    fireEvent.click(screen.getByRole('button', { name: /add/i }));

    await waitFor(() => expect(api.addDealLineItem).toHaveBeenCalledWith('deal-1', expect.objectContaining({ name: 'Locker', unitPrice: 120, vatRate: 8.1 })));
  });

  it('shows why a new line was refused', async () => {
    api.addDealLineItem.mockRejectedValue(new Error('You have read-only access to this organization.'));
    render(<DealLineItemsCard dealId="deal-1" />);
    await screen.findByLabelText('Quantity of Growth plan');

    fireEvent.change(screen.getByPlaceholderText('Item name'), { target: { value: 'Onboarding' } });
    fireEvent.change(screen.getByLabelText('Unit price'), { target: { value: '1500' } });
    fireEvent.click(screen.getByRole('button', { name: /add/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('You have read-only access to this organization.'));
  });
});
