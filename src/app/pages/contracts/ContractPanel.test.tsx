// What the panel does, as opposed to what it renders.
//
// Three defects an audit found here were all of one kind: the panel told the user
// something that was not true, or acted on state that belonged to a different
// contract. None of them is visible in a test of any single function — they are
// about what the component does with what it was handed.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Contract } from '@/app/api/contracts';

const toasts: { kind: string; message: string }[] = [];
vi.mock('sonner', () => ({
  toast: {
    success: (m: string) => { toasts.push({ kind: 'success', message: m }); },
    warning: (m: string) => { toasts.push({ kind: 'warning', message: m }); },
    error: (m: string) => { toasts.push({ kind: 'error', message: m }); },
  },
}));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const api = {
  contracts: [] as Contract[],
  countersign: vi.fn(),
  send: vi.fn(),
};

vi.mock('@/app/api/apiClient', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/apiClient')>('@/app/api/apiClient');
  return { ...actual, isUsingRealApi: () => true };
});

vi.mock('@/app/api/contracts', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/contracts')>('@/app/api/contracts');
  return {
    ...actual,
    listContractsForLead: () => Promise.resolve(api.contracts),
    countersignContract: (...args: unknown[]) => api.countersign(...args),
    sendContract: (...args: unknown[]) => api.send(...args),
    openContractPdf: () => Promise.resolve(true),
  };
});

const { ContractPanel } = await import('./ContractPanel');
const { ApiError } = await import('@/app/api/apiClient');

const contract = (over: Partial<Contract>): Contract => ({
  id: 'c1',
  status: 'signed_by_client',
  title: 'Membership Agreement',
  body: 'The whole agreement.',
  counterpartyName: 'Jean Dupont',
  counterpartyEmail: 'jean@example.ch',
  createdAtUtc: '2026-08-01T09:00:00Z',
  updatedAtUtc: '2026-08-02T09:00:00Z',
  clientSignatureName: 'Jean Dupont',
  clientSignedAtUtc: '2026-08-02T09:00:00Z',
  allowedActions: ['countersign', 'void'],
  unresolvedFields: [],
  events: [],
  ...over,
});

beforeEach(() => {
  toasts.length = 0;
  api.contracts = [];
  api.countersign.mockReset();
  api.send.mockReset();
});

const show = () => render(<ContractPanel leadId="l1" leadName="Jean Dupont" />);

describe('countersigning tells the truth about delivery', () => {
  it('does NOT claim copies went out when none did', async () => {
    // The server stamps executedCopySentAtUtc only when BOTH messages went, and
    // an unconfigured SMTP is a normal outcome the send path is careful never to
    // misreport. The toast said "Signed and sent to everyone" regardless — above a
    // card that simultaneously said the copy had not been emailed.
    api.contracts = [contract({})];
    api.countersign.mockResolvedValue(contract({
      status: 'countersigned', counterSignatureName: 'Kia', executedCopySentAtUtc: undefined,
    }));
    show();

    const user = userEvent.setup();
    await screen.findByText(/Jean Dupont signed on/);
    await user.type(screen.getByPlaceholderText('Type your full name'), 'Kia Jam');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: /sign and send/i }));

    await waitFor(() => expect(toasts.length).toBeGreaterThan(0));
    expect(toasts[0]!.kind).toBe('warning');
    expect(toasts[0]!.message).toMatch(/could not be emailed/i);
    expect(toasts.some((t) => /sent to everyone/i.test(t.message))).toBe(false);
  });

  it('does claim them when they did go', async () => {
    api.contracts = [contract({})];
    api.countersign.mockResolvedValue(contract({
      status: 'countersigned', counterSignatureName: 'Kia',
      executedCopySentAtUtc: '2026-08-03T09:00:00Z',
    }));
    show();

    const user = userEvent.setup();
    await screen.findByText(/Jean Dupont signed on/);
    await user.type(screen.getByPlaceholderText('Type your full name'), 'Kia Jam');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: /sign and send/i }));

    await waitFor(() => expect(toasts.length).toBeGreaterThan(0));
    expect(toasts[0]!.kind).toBe('success');
    expect(toasts[0]!.message).toMatch(/emailed to both parties/i);
  });
});

describe('two contracts on one lead do not share a signature', () => {
  it('typing into one form leaves the other empty', async () => {
    // signName/signAgreed were single values for the whole list, while `editing`
    // and `lastLink` were correctly keyed by id. Nothing stops a lead having two
    // contracts both awaiting our signature — so one consent tick ticked them all,
    // and consent that appears without being given is the worst thing on this panel
    // to get wrong.
    api.contracts = [contract({ id: 'a' }), contract({ id: 'b' })];
    show();

    const user = userEvent.setup();
    await waitFor(() => expect(screen.getAllByPlaceholderText('Type your full name')).toHaveLength(2));
    const [first, second] = screen.getAllByPlaceholderText('Type your full name');
    const [tick1, tick2] = screen.getAllByRole('checkbox');

    await user.type(second!, 'Kia Jam');
    await user.click(tick2!);

    expect((second as HTMLInputElement).value).toBe('Kia Jam');
    expect((first as HTMLInputElement).value).toBe('');
    expect((tick2 as HTMLInputElement).checked).toBe(true);
    expect((tick1 as HTMLInputElement).checked).toBe(false);
  });

  it('signs the contract whose form was filled in, with that form’s name', async () => {
    api.contracts = [contract({ id: 'a' }), contract({ id: 'b' })];
    api.countersign.mockResolvedValue(contract({
      id: 'b', status: 'countersigned', executedCopySentAtUtc: '2026-08-03T09:00:00Z',
    }));
    show();

    const user = userEvent.setup();
    await waitFor(() => expect(screen.getAllByPlaceholderText('Type your full name')).toHaveLength(2));
    await user.type(screen.getAllByPlaceholderText('Type your full name')[1]!, 'Kia Jam');
    await user.click(screen.getAllByRole('checkbox')[1]!);
    await user.click(screen.getAllByRole('button', { name: /sign and send/i })[1]!);

    await waitFor(() => expect(api.countersign).toHaveBeenCalled());
    expect(api.countersign).toHaveBeenCalledWith('b', 'Kia Jam', true);
  });
});

describe('the server’s own message reaches the user', () => {
  it('shows what actually went wrong rather than a shrug', async () => {
    // authFetchJson throws ApiError, never ContractError — so the catch was
    // discarding every real message from the server ("That is not possible at this
    // stage of the contract", "The contract needs an email address to send to")
    // and replacing it with "That did not work. Please try again."
    api.contracts = [contract({ status: 'draft', allowedActions: ['edit', 'send', 'void'] })];
    api.send.mockRejectedValue(
      new ApiError('The contract needs an email address to send to', 400),
    );
    show();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /send for signature/i }));

    await waitFor(() => expect(toasts.length).toBeGreaterThan(0));
    expect(toasts[0]!.kind).toBe('error');
    expect(toasts[0]!.message).toBe('The contract needs an email address to send to');
  });
});

describe('a body that no longer matches its hash is called out', () => {
  it('says so, because the countersign button will refuse', async () => {
    api.contracts = [contract({ bodyMatchesHashAtSend: false })];
    show();
    expect(await screen.findByRole('alert')).toHaveTextContent(/no longer matches what was sent/i);
  });

  it('says nothing when it does match, or when the server did not say', async () => {
    api.contracts = [contract({ bodyMatchesHashAtSend: true })];
    const { unmount } = show();
    await screen.findByText(/Jean Dupont signed on/);
    expect(screen.queryByText(/no longer matches what was sent/i)).toBeNull();
    unmount();

    // An older server that does not send the field must not read as tampered.
    api.contracts = [contract({ bodyMatchesHashAtSend: undefined })];
    show();
    await screen.findByText(/Jean Dupont signed on/);
    expect(screen.queryByText(/no longer matches what was sent/i)).toBeNull();
  });
});
