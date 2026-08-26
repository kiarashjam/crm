// The page a stranger sees, and the two things it must not do to them.
//
// Declining is terminal — nothing may follow it, not even Void, and there is no
// un-decline anywhere in the product. It used to be a plain text button sitting in
// the same row as Sign, one mis-aimed click from ending the agreement forever, and
// it sent no reason even though the API has always accepted one.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PublicContract } from '@/app/api/contracts';

const api = {
  contract: null as PublicContract | null,
  decline: vi.fn(),
  sign: vi.fn(),
};

vi.mock('@/app/api/contracts', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/contracts')>('@/app/api/contracts');
  return {
    ...actual,
    getPublicContract: () => Promise.resolve(api.contract!),
    declinePublicContract: (...args: unknown[]) => api.decline(...args),
    signPublicContract: (...args: unknown[]) => api.sign(...args),
    publicContractPdfUrl: (t: string) => `/api/public/contracts/${t}/pdf`,
  };
});

const SignContract = (await import('./SignContract')).default;

const publicContract = (over: Partial<PublicContract> = {}): PublicContract => ({
  status: 'sent',
  title: 'Membership Agreement',
  body: 'The whole agreement, in full.',
  counterpartyName: 'Jean Dupont',
  organizationName: 'Club Nautique du Leman',
  sentAtUtc: '2026-08-01T09:00:00Z',
  canSign: true,
  ...over,
});

beforeEach(() => {
  api.contract = publicContract();
  api.decline.mockReset();
  api.sign.mockReset();
});

const show = () => render(
  <MemoryRouter initialEntries={['/sign/tok123']}>
    <Routes><Route path="/sign/:token" element={<SignContract />} /></Routes>
  </MemoryRouter>,
);

describe('declining takes two deliberate steps', () => {
  it('one click does not end the contract', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /do not want to sign/i }));

    // Nothing has been sent yet — it has only asked.
    expect(api.decline).not.toHaveBeenCalled();
    expect(screen.getByText(/decline this contract\?/i)).toBeTruthy();
    expect(screen.getByText(/closes the contract for good/i)).toBeTruthy();
  });

  it('the second click is the one that does it, and carries the reason', async () => {
    api.decline.mockResolvedValue(publicContract({ status: 'declined', canSign: false }));
    show();
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: /do not want to sign/i }));
    await user.type(
      screen.getByLabelText(/why, if you would like to say/i),
      'the fee is more than we budgeted for',
    );
    await user.click(screen.getByRole('button', { name: /yes, decline it/i }));

    await waitFor(() => expect(api.decline).toHaveBeenCalledTimes(1));
    // The reason has always been accepted by the API and displayed by the CRM.
    // Nothing was ever sending it, so every decline arrived unexplained.
    expect(api.decline).toHaveBeenCalledWith('tok123', 'the fee is more than we budgeted for');
  });

  it('can be backed out of', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /do not want to sign/i }));
    await user.click(screen.getByRole('button', { name: /keep reading/i }));

    expect(api.decline).not.toHaveBeenCalled();
    expect(screen.queryByText(/decline this contract\?/i)).toBeNull();
    // And the way back in is still there.
    expect(screen.getByRole('button', { name: /do not want to sign/i })).toBeTruthy();
  });

  it('a reason is offered, not demanded', async () => {
    // Somebody who does not want to explain themselves must still be able to
    // decline; demanding a reason would just produce "." forever.
    api.decline.mockResolvedValue(publicContract({ status: 'declined', canSign: false }));
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /do not want to sign/i }));
    await user.click(screen.getByRole('button', { name: /yes, decline it/i }));

    await waitFor(() => expect(api.decline).toHaveBeenCalledTimes(1));
    expect(api.decline).toHaveBeenCalledWith('tok123', undefined);
  });
});

describe('the page does not claim an email it cannot vouch for', () => {
  const executed = {
    status: 'countersigned' as const,
    canSign: false,
    clientSignatureName: 'Jean Dupont',
    clientSignedAtUtc: '2026-08-02T09:00:00Z',
    counterSignatureName: 'Anaïs Berger',
    counterSignedAtUtc: '2026-08-03T09:00:00Z',
  };

  it('says a copy was emailed only when the server says it was', async () => {
    api.contract = publicContract({ ...executed, executedCopySentAtUtc: '2026-08-03T09:01:00Z' });
    show();
    expect(await screen.findByText(/a copy has been emailed to you/i)).toBeTruthy();
  });

  it('does not say it when nothing was sent', async () => {
    // Instructing somebody to keep an email that does not exist as their record of
    // a signed contract is the worst version of this.
    api.contract = publicContract({ ...executed, executedCopySentAtUtc: undefined });
    show();
    await screen.findByText(/signed by both parties/i);

    expect(screen.queryByText(/a copy has been emailed to you/i)).toBeNull();
    // And it points at something they can actually do about it, rather than
    // leaving them waiting for a message that is never coming.
    expect(screen.getByText(/has not been emailed yet/i).textContent)
      .toMatch(/download it below, or ask Club Nautique du Leman for it/i);
    expect(screen.getByRole('link', { name: /download your signed copy/i })).toBeTruthy();
  });
});
