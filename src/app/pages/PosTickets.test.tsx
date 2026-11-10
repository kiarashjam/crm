// POS tickets: the tickets Lightspeed closed that need staff, matched to a member (and the POS
// customer remembered), tried again or set aside with a reason, each against the version read;
// and the connection, set up by someone who takes payments. Others read the tickets and are not
// offered what the server would refuse them.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Member } from '@/app/api/members';
import type { PosSettings, PosStatus, PosTicket } from '@/app/api/pos';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions,
      currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich',
    },
  };
};

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const ANNA: Member = {
  id: 'm2', firstName: 'Anna', lastName: 'Meier', email: 'anna@example.ch', tier: 'Silver', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z', duesAmount: 129, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [ANNA],
}));

const api = vi.hoisted(() => ({
  status: vi.fn(), tickets: vi.fn(), match: vi.fn(), retry: vi.fn(), ignore: vi.fn(),
  settings: vi.fn(), save: vi.fn(), locations: vi.fn(), importNow: vi.fn(), connect: vi.fn(),
}));
vi.mock('@/app/api/pos', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/pos')>()),
  getPosStatus: () => api.status(),
  getPosTickets: (...a: unknown[]) => api.tickets(...a),
  matchPosTicket: (...a: unknown[]) => api.match(...a),
  retryPosTicket: (...a: unknown[]) => api.retry(...a),
  ignorePosTicket: (...a: unknown[]) => api.ignore(...a),
  getPosSettings: () => api.settings(),
  savePosSettings: (...a: unknown[]) => api.save(...a),
  getPosLocations: () => api.locations(),
  importPosNow: () => api.importNow(),
  connectPos: () => api.connect(),
}));

const { default: PosTickets } = await import('./PosTickets');

const prompt = vi.fn<(message?: string) => string | null>();
Object.defineProperty(window, 'prompt', { value: prompt, configurable: true, writable: true });

const STATUS: PosStatus = { provider: 'lightspeed-k', serverConfigured: true, connected: true, importing: true, locationName: 'Restaurant', needsAttention: 2 };

const WAITING: PosTicket = {
  id: 't1', provider: 'lightspeed-k', externalId: '46100001:R40', receiptNumber: 'R40', closedAtUtc: '2026-11-10T12:00:00Z', currency: 'CHF',
  total: 28, onAccountAmount: 28, paidAmount: 0, tipAmount: 0, tenders: 'House account 28.00', staffName: 'Mariah', tableName: '12',
  customerName: 'Anna Meier', hasCustomer: true, status: 'unmatched',
  note: 'Put on the house account, and no member\'s code is on it. Say whose it is.', attempts: 1, importedAtUtc: '2026-11-10T12:05:00Z',
  lines: [{ name: 'Club sandwich', quantity: 1, total: 28, vatRate: 8.1, discount: 0 }],
  payments: [{ method: 'House account', means: 'HouseAccount', amount: 28, tip: 0 }],
  version: 'v1',
};
const FAILED: PosTicket = {
  ...WAITING, id: 't2', externalId: '46100001:R50', receiptNumber: 'R50', hasCustomer: false, customerName: undefined, status: 'failed',
  memberId: 'm1', memberName: 'Jean Dupont', note: 'Paid by \'Rechnung\', which is not known as the house account, card, cash or TWINT.',
  payments: [{ method: 'Rechnung', means: 'Unknown', amount: 28, tip: 0 }], version: 'v2',
};

const SETTINGS: PosSettings = {
  provider: 'lightspeed-k', serverConfigured: true, connected: true, connectedAtUtc: '2026-11-01T09:00:00Z', connectedByName: 'Claire',
  tokenUnreadable: false, enabled: true, locationId: '46100001', locationName: 'Restaurant', importFromUtc: '2026-11-01T00:00:00Z',
  methodMap: { RCH: 'HouseAccount' }, defaultVenue: 'Dining', lastImportAtUtc: '2026-11-10T12:05:00Z', lastImportCount: 3,
  redirectUri: 'https://api.example.ch/api/public/pos/lightspeed/callback', version: 'sv1',
};

function renderAt(path = '/pos-tickets') {
  return render(withQueries(<MemoryRouter initialEntries={[path]}><PosTickets /></MemoryRouter>));
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  prompt.mockReset();
  api.status.mockResolvedValue(STATUS);
  api.tickets.mockResolvedValue([WAITING, FAILED]);
  api.settings.mockResolvedValue(SETTINGS);
  api.locations.mockResolvedValue([{ id: '46100001', name: 'Restaurant' }, { id: '46100002', name: 'Bar' }]);
  as([PERMISSION.ManageMoney]);
});

describe('POS tickets', () => {
  it('lists the tickets that need staff, with why', async () => {
    renderAt();
    expect(await screen.findByText('Receipt R40')).toBeInTheDocument();
    expect(screen.getByText(/no member's code is on it/)).toBeInTheDocument();
    expect(screen.getByText(/Rechnung', which is not known/)).toBeInTheDocument();
    expect(api.tickets).toHaveBeenCalledWith('attention', expect.anything());
    expect(screen.getByRole('tab', { name: 'Needs attention (2)' })).toBeInTheDocument();
  });

  it('matches a ticket to a member and remembers the POS customer, against the version read', async () => {
    api.match.mockResolvedValue({ ...WAITING, status: 'posted', memberId: 'm2', memberName: 'Anna Meier', version: 'v9' });
    const user = userEvent.setup();
    renderAt();
    await user.click(await screen.findByRole('button', { name: 'Match receipt R40 to a member' }));

    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('option', { name: /Anna Meier/ }));
    expect(within(dialog).getByRole('checkbox')).toBeChecked();
    await user.click(within(dialog).getByRole('button', { name: 'Post to Anna Meier' }));

    await waitFor(() => expect(api.match).toHaveBeenCalledWith('t1', 'm2', true, 'v1'));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/posted to Anna Meier's account/)));
  });

  it('tries a ticket again, and sets one aside only with a reason', async () => {
    api.retry.mockResolvedValue({ ...FAILED, status: 'posted' });
    api.ignore.mockResolvedValue({ ...WAITING, status: 'ignored' });
    const user = userEvent.setup();
    renderAt();
    await user.click(await screen.findByRole('button', { name: 'Try receipt R50 again' }));
    await waitFor(() => expect(api.retry).toHaveBeenCalledWith('t2', 'v2'));

    prompt.mockReturnValueOnce('  ');
    await user.click(screen.getByRole('button', { name: 'Set receipt R40 aside' }));
    expect(api.ignore).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Say why the ticket is not a member\'s.');

    prompt.mockReturnValueOnce('The chef\'s own lunch');
    await user.click(screen.getByRole('button', { name: 'Set receipt R40 aside' }));
    await waitFor(() => expect(api.ignore).toHaveBeenCalledWith('t1', 'The chef\'s own lunch', 'v1'));
  });

  it('offers nothing to act with to someone who does not take payments', async () => {
    as([PERMISSION.FrontDesk]);
    const user = userEvent.setup();
    renderAt();
    expect(await screen.findByText('Receipt R40')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Match receipt/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Set receipt/ })).toBeNull();
    await user.click(screen.getByRole('tab', { name: 'Connection' }));
    expect(await screen.findByText(/Only someone who takes payments connects the POS/)).toBeInTheDocument();
    expect(api.settings).not.toHaveBeenCalled();
  });

  it('saves the connection settings with what each payment method means, and imports on request', async () => {
    api.save.mockResolvedValue(SETTINGS);
    api.importNow.mockResolvedValue({ read: 5, imported: 2, posted: 1, unmatched: 1, failed: 0, skipped: 0, finishedAtUtc: '2026-11-10T12:10:00Z' });
    const user = userEvent.setup();
    renderAt('/pos-tickets?tab=connection');

    expect(await screen.findByText(/Connected on .* by Claire/)).toBeInTheDocument();
    expect(screen.getByLabelText('Location')).toHaveValue('46100001');
    await user.click(screen.getByRole('button', { name: 'Add a payment method' }));
    await user.type(screen.getByLabelText('Payment method 2'), 'VOUCHER');
    await user.selectOptions(screen.getByLabelText('What payment method 2 means'), 'Skip');
    await user.click(screen.getByRole('button', { name: 'Save POS settings' }));
    await waitFor(() => expect(api.save).toHaveBeenCalledWith(expect.objectContaining({
      enabled: true, locationId: '46100001', defaultVenue: 'Dining', methodMap: { RCH: 'HouseAccount', VOUCHER: 'Skip' }, version: 'sv1',
    })));

    await user.click(screen.getByRole('button', { name: 'Import now' }));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('2 new tickets: 1 posted, 1 need a member, 0 not posted'));
  });

  it('says when the server has no Lightspeed client, and when Lightspeed sent the browser back connected', async () => {
    api.settings.mockResolvedValue({ ...SETTINGS, serverConfigured: false, connected: false, enabled: false });
    renderAt('/pos-tickets?tab=connection&connected=1');
    expect(await screen.findByText(/Lightspeed is not set up on this server yet/)).toBeInTheDocument();
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Lightspeed connected/)));
  });
});
