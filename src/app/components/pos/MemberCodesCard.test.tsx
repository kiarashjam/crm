// A member's codes at the POS: their member number or card code, given by whoever manages club
// members (typed as the card shows it, or made new), shown with the QR code the server worked
// out, and taken away again. Others read them and are offered nothing to change.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { MemberCode } from '@/app/api/pos';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
};
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({ list: vi.fn(), add: vi.fn(), remove: vi.fn() }));
vi.mock('@/app/api/pos', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/pos')>()),
  getMemberCodes: (...a: unknown[]) => api.list(...a),
  addMemberCode: (...a: unknown[]) => api.add(...a),
  removeMemberCode: (...a: unknown[]) => api.remove(...a),
}));

const { MemberCodesCard } = await import('./MemberCodesCard');

const confirm = vi.fn<(message?: string) => boolean>();
Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });

const CARD: MemberCode = {
  id: 'c1', memberId: 'm1', kind: 'card', code: '1042', label: 'Member card', createdAtUtc: '2026-11-10T10:00:00Z', createdByName: 'Claire',
  qrRows: Array.from({ length: 21 }, (_, i) => (i % 2 ? '1'.repeat(21) : '0'.repeat(21))),
};
const CUSTOMER: MemberCode = { id: 'c2', memberId: 'm1', kind: 'pos-customer', code: '777', label: 'Lightspeed customer Jean Dupont', createdAtUtc: '2026-11-10T11:00:00Z' };

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  confirm.mockReset();
  api.list.mockResolvedValue([CARD, CUSTOMER]);
  as([PERMISSION.ClubMembers]);
});

describe('a member\'s codes at the POS', () => {
  it('lists the codes, with the QR code of a card code', async () => {
    const user = userEvent.setup();
    render(withQueries(<MemberCodesCard memberId="m1" />));
    expect(await screen.findByText('1042')).toBeInTheDocument();
    expect(screen.getByText(/Lightspeed customer Jean Dupont/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'QR' }));
    expect(screen.getByRole('img', { name: 'QR code for 1042' })).toBeInTheDocument();
  });

  it('gives a typed code or a new one, and takes one away once confirmed', async () => {
    api.add.mockResolvedValue({ ...CARD, id: 'c3', code: 'M-2042' });
    api.remove.mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(withQueries(<MemberCodesCard memberId="m1" />));
    await user.type(await screen.findByLabelText('Member code'), 'm-2042');
    await user.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(api.add).toHaveBeenCalledWith('m1', 'm-2042'));
    expect(toast.success).toHaveBeenCalledWith('Code M-2042 names this member at the POS');

    await user.click(screen.getByRole('button', { name: 'Make a new code' }));
    await waitFor(() => expect(api.add).toHaveBeenLastCalledWith('m1', undefined));

    confirm.mockReturnValueOnce(false);
    await user.click(screen.getByRole('button', { name: 'Take away the code 1042' }));
    expect(api.remove).not.toHaveBeenCalled();
    confirm.mockReturnValueOnce(true);
    await user.click(screen.getByRole('button', { name: 'Take away the code 1042' }));
    await waitFor(() => expect(api.remove).toHaveBeenCalledWith('m1', 'c1'));
  });

  it('offers nothing to change to someone who does not manage club members', async () => {
    as([PERMISSION.FrontDesk]);
    render(withQueries(<MemberCodesCard memberId="m1" />));
    expect(await screen.findByText('1042')).toBeInTheDocument();
    expect(screen.queryByLabelText('Member code')).toBeNull();
    expect(screen.queryByRole('button', { name: /Take away/ })).toBeNull();
  });
});
