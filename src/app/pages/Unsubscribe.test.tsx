// The unsubscribe page, as the person who followed the link in an email sees it.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const api = { get: vi.fn(), confirm: vi.fn() };
vi.mock('@/app/api/unsubscribe', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/unsubscribe')>('@/app/api/unsubscribe');
  return {
    ...actual,
    getUnsubscribe: (...a: unknown[]) => api.get(...a),
    confirmUnsubscribe: (...a: unknown[]) => api.confirm(...a),
  };
});

const { UnsubscribeError } = await import('@/app/api/unsubscribe');
const Unsubscribe = (await import('./Unsubscribe')).default;

const show = () => render(
  <MemoryRouter initialEntries={['/unsubscribe/tok']}>
    <Routes><Route path="/unsubscribe/:token" element={<Unsubscribe />} /></Routes>
  </MemoryRouter>,
);

beforeEach(() => {
  api.get.mockReset().mockResolvedValue({ workspaceName: 'Pavillon 46', email: 'jean@example.ch' });
  api.confirm.mockReset().mockResolvedValue({ workspaceName: 'Pavillon 46', email: 'jean@example.ch' });
});

describe('the unsubscribe page', () => {
  it('says whose email stops, and changes nothing until the button is pressed', async () => {
    show();
    expect(await screen.findByRole('heading', { name: 'Stop emails from Pavillon 46?' })).toBeInTheDocument();
    expect(screen.getByText('jean@example.ch')).toBeInTheDocument();
    expect(api.confirm).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toMatch(/Cadence/);
  });

  it('unsubscribes on the button, and says so', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));

    expect(api.confirm).toHaveBeenCalledWith('tok');
    expect(await screen.findByRole('heading', { name: 'You are unsubscribed' })).toHaveFocus();
    expect(screen.getByText('Pavillon 46 will not email jean@example.ch again.')).toBeInTheDocument();
  });

  it('a link that is not one of ours says so, with nothing to press', async () => {
    api.get.mockRejectedValue(new UnsubscribeError('gone', 404));
    show();
    expect(await screen.findByRole('heading', { name: 'This link is not valid' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Unsubscribe' })).toBeNull();
  });

  it('a failed unsubscribe is not shown as done', async () => {
    api.confirm.mockRejectedValue(new UnsubscribeError('offline', 0));
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Unsubscribe' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('did not go through');
    expect(screen.queryByRole('heading', { name: 'You are unsubscribed' })).toBeNull();
  });
});
