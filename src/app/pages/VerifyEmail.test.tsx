// The confirmation link, and the banner that asks for it.
//
// The server decides what an unconfirmed account may do; these pin what the person
// sees: the banner while the address is unconfirmed and never after, and a link that
// is spent exactly once and then remembered as confirmed in this browser.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { getCurrentUser, setSession } from '@/app/lib/auth';

const verifyEmail = vi.fn(async (_token: string) => undefined);
const resendVerificationEmail = vi.fn(async () => undefined);
const me = vi.fn(async () => ({ id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: false }));

vi.mock('@/app/api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/auth')>()),
  verifyEmail: (token: string) => verifyEmail(token),
  resendVerificationEmail: () => resendVerificationEmail(),
  me: () => me(),
}));

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  isUsingRealApi: () => true,
  servesDemoData: () => false,
}));

import VerifyEmail from './VerifyEmail';
import EmailVerificationBanner from '@/app/components/EmailVerificationBanner';

beforeEach(() => {
  localStorage.clear();
  verifyEmail.mockClear();
  resendVerificationEmail.mockClear();
  me.mockClear();
});

describe('the confirmation link', () => {
  it('spends the token once and remembers the address as confirmed', async () => {
    setSession('jwt', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: false });
    me.mockResolvedValueOnce({ id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: true });

    render(
      <MemoryRouter initialEntries={['/verify-email?token=abc123']}>
        <VerifyEmail />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Your email address is confirmed')).toBeInTheDocument();
    expect(verifyEmail).toHaveBeenCalledTimes(1);
    expect(verifyEmail).toHaveBeenCalledWith('abc123');
    expect(getCurrentUser()?.emailVerified).toBe(true);
    expect(localStorage.getItem('aci_token')).toBe('jwt');
  });

  it('says so when the link has been used or has expired', async () => {
    verifyEmail.mockRejectedValueOnce(new Error('This confirmation link is invalid or has expired.'));

    render(
      <MemoryRouter initialEntries={['/verify-email?token=old']}>
        <VerifyEmail />
      </MemoryRouter>,
    );

    expect(await screen.findByText('This link did not work')).toBeInTheDocument();
    expect(screen.getByText(/invalid or has expired/)).toBeInTheDocument();
  });

  it('does not mark the signed-in account confirmed when the link was for another one', async () => {
    setSession('jwt', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: false });
    // me() still says unconfirmed: the token belonged to someone else's account.

    render(
      <MemoryRouter initialEntries={['/verify-email?token=someone-elses']}>
        <VerifyEmail />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Your email address is confirmed')).toBeInTheDocument();
    expect(getCurrentUser()?.emailVerified).toBe(false);
  });
});

describe('the banner', () => {
  it('asks an unconfirmed account to confirm, and sends a new link', async () => {
    setSession('jwt', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: false });
    const user = userEvent.setup();

    render(<EmailVerificationBanner />);

    expect(screen.getByText(/Confirm your email address/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send a new link' }));
    expect(resendVerificationEmail).toHaveBeenCalledTimes(1);
  });

  it('stays away from a confirmed account, without asking the server', () => {
    setSession('jwt', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: true });

    const { container } = render(<EmailVerificationBanner />);

    expect(container).toBeEmptyDOMElement();
    expect(me).not.toHaveBeenCalled();
  });

  it('goes away when the server says the address was confirmed elsewhere', async () => {
    setSession('jwt', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: false });
    me.mockResolvedValueOnce({ id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: true });

    const { container } = render(<EmailVerificationBanner />);

    await waitFor(() => expect(container).toBeEmptyDOMElement());
    expect(getCurrentUser()?.emailVerified).toBe(true);
  });
});
