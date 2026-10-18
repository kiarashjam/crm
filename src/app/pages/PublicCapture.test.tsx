// The public form, as the stranger who fills it in sees it.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const api = { get: vi.fn(), submit: vi.fn() };
vi.mock('@/app/api/leadOps', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/leadOps')>('@/app/api/leadOps');
  return {
    ...actual,
    getPublicCaptureForm: (...a: unknown[]) => api.get(...a),
    submitPublicCapture: (...a: unknown[]) => api.submit(...a),
  };
});

const { PublicCaptureError } = await import('@/app/api/leadOps');
const PublicCapture = (await import('./PublicCapture')).default;

const show = () => render(
  <MemoryRouter initialEntries={['/capture/tok']}>
    <Routes><Route path="/capture/:token" element={<PublicCapture />} /></Routes>
  </MemoryRouter>,
);

beforeEach(() => {
  api.get.mockReset();
  api.submit.mockReset();
  api.get.mockResolvedValue({ name: 'Join the club', successMessage: 'Merci' });
});

describe('the public capture form', () => {
  it('never tells a visitor about the CRM', async () => {
    show();
    await screen.findByRole('heading', { name: 'Join the club' });
    expect(document.body.textContent).not.toMatch(/Cadence|become a lead/i);
  });

  it('shows the thank-you the workspace configured, and moves focus to it', async () => {
    api.submit.mockResolvedValue('Merci, à bientôt.');
    show();
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/^Name/), 'Jean');
    await user.type(screen.getByLabelText(/^Email/), 'jean@example.ch');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Merci, à bientôt.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Thank you' })).toHaveFocus();
    // The honeypot is sent empty when a person fills the form in.
    expect(api.submit).toHaveBeenCalledWith('tok', expect.objectContaining({ website: '' }));
  });

  it('a dead link says so, and offers no fields to fill in', async () => {
    api.get.mockRejectedValue(new PublicCaptureError('gone', 404));
    show();
    expect(await screen.findByRole('heading', { name: /no longer available/ })).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Email/)).toBeNull();
  });

  it('a network failure is not mistaken for a dead link, and can be retried', async () => {
    api.get.mockRejectedValueOnce(new PublicCaptureError('offline', 0));
    show();
    expect(await screen.findByRole('heading', { name: /could not be loaded/ })).toBeInTheDocument();
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByRole('heading', { name: 'Join the club' })).toBeInTheDocument();
  });

  it('says plainly when it is being rate limited', async () => {
    api.submit.mockRejectedValue(new PublicCaptureError('slow down', 429));
    show();
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/^Name/), 'Jean');
    await user.type(screen.getByLabelText(/^Email/), 'jean@example.ch');
    await user.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/try again in a minute/));
  });

  it('the honeypot is invisible to people and to assistive technology', async () => {
    show();
    await screen.findByRole('heading', { name: 'Join the club' });
    // Not reachable by role or label, not in the tab order.
    expect(screen.queryByRole('textbox', { name: 'Website' })).toBeNull();
    const trap = document.querySelector('input[tabindex="-1"]');
    expect(trap).not.toBeNull();
    expect(trap!.closest('[aria-hidden="true"]')).not.toBeNull();
  });
});
