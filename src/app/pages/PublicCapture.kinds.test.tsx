// A workspace whose leads are establishments asks on its public form for the place, then
// for who is writing, who becomes its contact person. A people-only form is as it was.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const api = vi.hoisted(() => ({ get: vi.fn(), submit: vi.fn() }));
vi.mock('@/app/api/leadOps', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/leadOps')>('@/app/api/leadOps');
  return {
    ...actual,
    getPublicCaptureForm: (...a: unknown[]) => api.get(...a),
    submitPublicCapture: (...a: unknown[]) => api.submit(...a),
  };
});

const PublicCapture = (await import('./PublicCapture')).default;

const show = () => render(
  <MemoryRouter initialEntries={['/capture/tok']}>
    <Routes><Route path="/capture/:token" element={<PublicCapture />} /></Routes>
  </MemoryRouter>,
);

beforeEach(() => {
  api.get.mockReset();
  api.submit.mockReset();
  api.submit.mockResolvedValue('Merci');
});

describe('the public form for establishments', () => {
  it('asks for the place, then who is writing, and sends both', async () => {
    api.get.mockResolvedValue({ name: 'Work with Bonap', leadKinds: 'establishment' });
    show();
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText(/^Establishment name/), 'Trattoria Bella');
    await user.selectOptions(screen.getByLabelText('Type'), 'restaurant');
    await user.type(screen.getByLabelText('City'), 'Lausanne');
    await user.type(screen.getByLabelText('Establishment website'), 'trattoria.ch');
    await user.type(screen.getByLabelText(/^Your name/), 'Marco Rossi');
    await user.type(screen.getByLabelText('Your role'), 'Owner');
    await user.type(screen.getByLabelText(/^Email/), 'marco@trattoria.ch');
    expect(screen.queryByLabelText('Company')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Merci')).toBeInTheDocument();
    const body = api.submit.mock.calls[0]?.[1];
    expect(body).toMatchObject({
      kind: 'establishment',
      establishmentName: 'Trattoria Bella',
      establishmentType: 'restaurant',
      city: 'Lausanne',
      businessWebsite: 'trattoria.ch',
      name: 'Marco Rossi',
      jobTitle: 'Owner',
      email: 'marco@trattoria.ch',
      // The honeypot stays the honeypot: empty.
      website: '',
    });
    expect(body).not.toHaveProperty('company');
  });

  it('lets the visitor say which they are where the workspace has both', async () => {
    api.get.mockResolvedValue({ name: 'Contact us', leadKinds: 'both' });
    show();
    const user = userEvent.setup();

    expect(await screen.findByRole('radio', { name: 'For myself' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByLabelText('Company')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'For a business' }));
    expect(screen.getByLabelText(/^Establishment name/)).toBeInTheDocument();
  });

  it('keeps a people-only form as it was, and sends no kind', async () => {
    api.get.mockResolvedValue({ name: 'Join the club', leadKinds: 'person' });
    show();
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText(/^Name/), 'Jean');
    await user.type(screen.getByLabelText(/^Email/), 'jean@example.ch');
    expect(screen.queryByRole('radiogroup')).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Establishment name/)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await screen.findByText('Merci');
    expect(api.submit.mock.calls[0]?.[1]).not.toHaveProperty('kind');
    expect(api.submit.mock.calls[0]?.[1]).not.toHaveProperty('establishmentName');
  });
});
