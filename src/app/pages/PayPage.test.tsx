// The page a club's pay link opens, for a member with no account here.
//
// Opening it changes nothing; the button asks the server for Stripe's page and goes there.
// Back from Stripe, it thanks them and does not offer to pay again while Stripe confirms. A
// link that is no good, or a club that cannot take payments now, says so plainly.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PublicPayPage } from '@/app/api/payments';

const api = vi.hoisted(() => ({ read: vi.fn(), start: vi.fn() }));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPayPage: (...a: unknown[]) => api.read(...a),
  startCheckout: (...a: unknown[]) => api.start(...a),
}));
const leave = vi.hoisted(() => ({ leaveFor: vi.fn() }));
vi.mock('@/app/lib/leavePage', () => leave);

const { PayLinkError } = await import('@/app/api/payments');
const { default: PayPage } = await import('./PayPage');

const OWED: PublicPayPage = {
  clubName: 'Pavillon 46', memberFirstName: 'Jean', purpose: 'account', description: 'What you owe Pavillon 46 now',
  amount: 339, currency: 'CHF', methods: ['card', 'twint'], available: true, autoPay: false,
};

beforeEach(() => {
  api.read.mockReset().mockResolvedValue(OWED);
  api.start.mockReset().mockResolvedValue({ url: 'https://checkout.stripe.com/c/pay/cs_test_1' });
  leave.leaveFor.mockReset();
});

const show = (search = '') => render(
  <MemoryRouter initialEntries={[`/pay/tok-1${search}`]}>
    <Routes><Route path="/pay/:token" element={<PayPage />} /></Routes>
  </MemoryRouter>,
);

describe('the pay page', () => {
  it("says whose club, what and how, and goes to Stripe's page for the link", async () => {
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Hello Jean')).toBeInTheDocument();
    expect(screen.getByText('Pavillon 46')).toBeInTheDocument();
    expect(screen.getByText('Pay by Card or TWINT on Stripe\'s secure page.')).toBeInTheDocument();
    expect(api.read).toHaveBeenCalledWith('tok-1');
    expect(api.start).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /^Pay / }));

    await waitFor(() => expect(leave.leaveFor).toHaveBeenCalledWith('https://checkout.stripe.com/c/pay/cs_test_1'));
    expect(api.start).toHaveBeenCalledWith('tok-1');
  });

  it("says why the payment could not start, in the server's words", async () => {
    api.start.mockRejectedValue(new PayLinkError('There is nothing to pay: the charges are paid, or the account is in credit.', 400));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /^Pay / }));

    expect(await screen.findByRole('alert')).toHaveTextContent('There is nothing to pay');
    expect(leave.leaveFor).not.toHaveBeenCalled();
  });

  it('thanks them on the way back from Stripe, and offers no second payment', async () => {
    show('?status=done');

    expect(await screen.findByRole('heading', { name: 'Thank you' })).toBeInTheDocument();
    expect(screen.getByText(/shows on your account as soon as Stripe confirms it/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Pay/ })).toBeNull();
  });

  it('says nothing was paid when they came back without paying, and lets them try again', async () => {
    show('?status=cancelled');

    expect(await screen.findByText(/The payment was not made/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Pay / })).toBeEnabled();
  });

  it('says plainly when there is nothing to pay or the club cannot take payments now', async () => {
    api.read.mockResolvedValue({ ...OWED, amount: 0, available: false, reason: 'There is nothing to pay: everything this link was for is paid.' });
    show();

    expect(await screen.findByText('There is nothing to pay: everything this link was for is paid.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it("asks for the member's agreement to dues on the card when saving one for them", async () => {
    api.read.mockResolvedValue({ ...OWED, purpose: 'setup', amount: 0, methods: ['card'], autoPay: true, description: 'Save a card for your membership dues' });
    const user = userEvent.setup();
    show();

    expect(await screen.findByText(/you agree that Pavillon 46 charges your membership dues to it/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Save a card' }));

    await waitFor(() => expect(leave.leaveFor).toHaveBeenCalled());
  });

  it('says a link that is no good is not valid, and one that could not be read can be tried again', async () => {
    api.read.mockRejectedValueOnce(new PayLinkError('This payment link is not valid any more.', 404));
    const { unmount } = show();
    expect(await screen.findByText('This payment link is not valid')).toBeInTheDocument();
    unmount();

    api.read.mockRejectedValueOnce(new PayLinkError('Could not reach the server', 0));
    const user = userEvent.setup();
    show();
    await user.click(await screen.findByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Hello Jean')).toBeInTheDocument();
  });
});
