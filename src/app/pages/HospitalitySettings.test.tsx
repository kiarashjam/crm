// Hospitality settings: everything on the page is something the server applies.
//
// The page said "Changes apply to all new transactions once saved", and later "Not
// applied yet" next to the tiers, dues, multipliers and points, as nothing read them. The
// tiers and prices are now the catalogue the server takes its prices from, saved one item
// at a time against the version read; the points and billing settings are the ones the
// server applies; and billing now is the server's run, for whoever manages the money.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { DEFAULT_BILLING, type HospitalitySettings as Settings } from '@/app/api/hospitalitySettings';
import type { CatalogItem, CatalogItemInput } from '@/app/api/catalog';
import type { BillingPreview, BillingRun } from '@/app/api/billing';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import { resetClubCurrency, useClubMoney } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

/** Signed in to a club as a member holding these permissions; none given, a personal workspace. */
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions },
  };
};

// Against the real API: demo data offers everything, as its workspaces are the visitor's own.
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = {
  get: vi.fn<() => Promise<Settings>>(),
  update: vi.fn<(patch: Partial<Settings>) => Promise<Settings>>(),
};
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: () => api.get(),
  updateHospitalitySettings: (patch: Partial<Settings>) => api.update(patch),
}));

const catalog = {
  list: vi.fn<() => Promise<CatalogItem[]>>(),
  update: vi.fn<(id: string, patch: CatalogItemInput) => Promise<CatalogItem>>(),
  create: vi.fn<(input: CatalogItemInput) => Promise<CatalogItem>>(),
};
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: () => catalog.list(),
  updateCatalogItem: (id: string, patch: CatalogItemInput) => catalog.update(id, patch),
  createCatalogItem: (input: CatalogItemInput) => catalog.create(input),
}));

const billing = {
  runs: vi.fn<() => Promise<BillingRun[]>>(),
  preview: vi.fn<() => Promise<BillingPreview>>(),
  run: vi.fn<() => Promise<BillingRun>>(),
};
vi.mock('@/app/api/billing', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/billing')>()),
  getBillingRuns: () => billing.runs(),
  getBillingPreview: () => billing.preview(),
  runBillingNow: () => billing.run(),
}));

const { default: HospitalitySettings } = await import('./HospitalitySettings');

/** Somewhere else in the app that shows money, open at the same time. */
function OtherClubPage() {
  const { format } = useClubMoney();
  return <p data-testid="elsewhere">{format(12.5)}</p>;
}

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const SETTINGS: Settings = {
  currency: 'CHF',
  basePointsPerDollar: 10,
  visitPointsByVenue: { Dining: 50, Bar: 40, Spa: 60, Gym: 20, Coworking: 30, Event: 50, Lounge: 25, Rooftop: 30 },
  eventCheckInPoints: 50,
  billing: DEFAULT_BILLING,
};

const tier = (name: string, price: number, sortOrder: number, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id: `t-${name}`, kind: 'Membership', name, price, currency: 'CHF', vatRate: 0, cadence: 'Monthly', initiationFee: 0,
  loyaltyMultiplier: 1, benefits: [], chargeKind: 'Dues', isActive: true, sortOrder,
  createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', version: `v-${name}-1`, ...extra,
});

const ITEMS: CatalogItem[] = [
  tier('Bronze', 49, 0),
  tier('Gold', 299, 1, { initiationFee: 1000, loyaltyMultiplier: 1.5 }),
  {
    ...tier('Guest fee', 25, 0), id: 'f-guest', kind: 'Fee', cadence: undefined, initiationFee: undefined,
    loyaltyMultiplier: undefined, vatRate: 8.1, chargeKind: 'Other', version: 'v-guest-1',
  },
];

const RUN: BillingRun = {
  id: 'run-1', trigger: 'manual', status: 'succeeded', startedAtUtc: '2026-11-10T08:00:00Z', finishedAtUtc: '2026-11-10T08:00:02Z',
  startedByName: 'Anna Keller', steps: ['billing', 'overdue'], subscriptionsBilled: 2, chargesPosted: 3, amountBilled: 897,
  currency: 'CHF', overdueCharges: 1, subscriptionsPastDue: 1, remindersQueued: 0, membersLapsed: 0, membersReinstated: 0,
  statementsQueued: 0, failures: [],
};

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  org.current = null;
  toast.success.mockReset();
  toast.error.mockReset();
  api.get.mockReset().mockResolvedValue(SETTINGS);
  api.update.mockReset().mockImplementation(async (patch) => ({ ...SETTINGS, ...patch }));
  catalog.list.mockReset().mockResolvedValue(ITEMS);
  catalog.update.mockReset().mockImplementation(async (id, patch) => ({ ...ITEMS.find((i) => i.id === id)!, ...patch, version: 'v-next' }));
  catalog.create.mockReset();
  billing.runs.mockReset().mockResolvedValue([]);
  billing.preview.mockReset().mockResolvedValue({ subscriptions: 0, periods: 0, amountByCurrency: {}, items: [] });
  billing.run.mockReset().mockResolvedValue(RUN);
});

const show = () => render(withQueries(
  <MemoryRouter>
    <HospitalitySettings />
    <OtherClubPage />
  </MemoryRouter>,
));

/** The card of the tier of this name. */
const card = async (name: string) => (await screen.findByLabelText(`${name} dues`)).closest('article') as HTMLElement;

describe('hospitality settings', () => {
  it('shows the tiers from the catalogue and nothing marked as not applied', async () => {
    show();

    expect(await screen.findByLabelText('Gold dues')).toHaveValue(299);
    expect(screen.getByLabelText('Gold initiation fee')).toHaveValue(1000);
    expect(screen.getByLabelText('Gold loyalty multiplier')).toHaveValue(1.5);
    expect(screen.getByLabelText('Club currency')).toHaveTextContent('CHF');
    expect(screen.queryByText('Not applied yet')).toBeNull();
    expect(screen.queryByText(/Only the club currency is applied/)).toBeNull();
  });

  it('saves a tier on its own, against the version it was read at', async () => {
    const user = userEvent.setup();
    show();

    const gold = await card('Gold');
    await user.clear(within(gold).getByLabelText('Gold dues'));
    await user.type(within(gold).getByLabelText('Gold dues'), '320');
    await user.click(within(gold).getByRole('button', { name: 'Save tier' }));

    await waitFor(() => expect(catalog.update).toHaveBeenCalledTimes(1));
    expect(catalog.update.mock.calls[0]![0]).toBe('t-Gold');
    expect(catalog.update.mock.calls[0]![1]).toMatchObject({ price: 320, initiationFee: 1000, version: 'v-Gold-1' });
    // The catalogue is read again, so the card shows what the server now holds.
    await waitFor(() => expect(catalog.list.mock.calls.length).toBeGreaterThan(1));
    // The settings document is not where tiers live any more.
    expect(api.update).not.toHaveBeenCalled();
  });

  it("shows the server's sentence when a tier changed by someone else is refused", async () => {
    const user = userEvent.setup();
    catalog.update.mockRejectedValue(new Error('Someone else changed Gold meanwhile. Reload to see their change.'));
    show();

    await user.click(within(await card('Gold')).getByRole('button', { name: 'Take off sale' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Someone else changed Gold meanwhile. Reload to see their change.'));
    expect(catalog.update.mock.calls[0]![1]).toEqual({ isActive: false, version: 'v-Gold-1' });
  });

  it('lists the other prices, and reprices one', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('tab', { name: 'Price catalogue' }));
    const row = (await screen.findByText('Guest fee')).closest('tr') as HTMLElement;
    expect(row).toHaveTextContent('8.1%');
    await user.click(within(row).getByRole('button', { name: 'Reprice' }));
    await user.clear(within(row).getByLabelText('Guest fee price'));
    await user.type(within(row).getByLabelText('Guest fee price'), '30');
    await user.click(within(row).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(catalog.update).toHaveBeenCalledWith('f-guest', { price: 30, version: 'v-guest-1' }));
  });

  it('saves a new club currency and every open club page switches to it', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByLabelText('Club currency'));
    await user.click(await screen.findByRole('option', { name: /EUR/ }));
    await user.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(api.update).toHaveBeenCalled());
    expect(api.update.mock.calls[0]![0]).toMatchObject({ currency: 'EUR' });
    // "EUR" in the workspace's de-CH number format, "€" in some others.
    await waitFor(() => expect(screen.getByTestId('elsewhere')).toHaveTextContent(/€|EUR/));
  });

  it('saves the points rules the server applies, the points for coming to an event among them', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('tab', { name: 'Points' }));
    await user.clear(await screen.findByLabelText('Spa check-in points'));
    await user.type(screen.getByLabelText('Spa check-in points'), '80');
    // The door's check-in at an event earns these on the server, times the tier's multiplier.
    await user.clear(screen.getByLabelText('Event attendance points'));
    await user.type(screen.getByLabelText('Event attendance points'), '75');
    await user.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(api.update).toHaveBeenCalled());
    expect(api.update.mock.calls[0]![0].visitPointsByVenue).toMatchObject({ Spa: 80, Dining: 50 });
    expect(api.update.mock.calls[0]![0].eventCheckInPoints).toBe(75);
  });

  it('turns on automatic billing and lapsing, and saves them with the settings', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('tab', { name: 'Billing' }));
    await user.click(await screen.findByRole('switch', { name: 'Bill subscriptions automatically' }));
    await user.click(screen.getByRole('switch', { name: 'Lapse members who do not pay' }));
    await user.clear(screen.getByLabelText('Grace period'));
    await user.type(screen.getByLabelText('Grace period'), '30');
    await user.clear(screen.getByLabelText('Reminder days'));
    await user.type(screen.getByLabelText('Reminder days'), '7, 21');
    await user.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(api.update).toHaveBeenCalled());
    expect(api.update.mock.calls[0]![0].billing).toEqual({
      ...DEFAULT_BILLING, automaticBilling: true, lapseEnabled: true, gracePeriodDays: 30, reminderDays: [7, 21],
    });
  });

  it('shows the sentence the server refuses a setting with', async () => {
    const user = userEvent.setup();
    api.update.mockRejectedValue(new Error('Reminders are one to five numbers of days after the due date.'));
    show();

    await user.click(await screen.findByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Reminders are one to five numbers of days after the due date.'));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('bills what is due for whoever manages the money, and shows the run', async () => {
    const user = userEvent.setup();
    as([PERMISSION.ManageMoney]);
    billing.preview.mockResolvedValue({ subscriptions: 2, periods: 3, amountByCurrency: { CHF: 897 }, items: [] });
    billing.runs.mockResolvedValueOnce([]).mockResolvedValue([RUN]);
    show();

    await user.click(await screen.findByRole('tab', { name: 'Billing' }));
    expect(await screen.findByText(/3 periods for 2 subscriptions/)).toBeInTheDocument();
    expect(screen.getByText('No run yet.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Bill what is due now/ }));

    await waitFor(() => expect(billing.run).toHaveBeenCalledTimes(1));
    expect(toast.success).toHaveBeenCalledWith('Billed 3 periods for 2 subscriptions');
    expect(await screen.findByText('Started by Anna Keller')).toBeInTheDocument();
    // Managing the money does not change the settings (it may change the catalogue's prices).
    expect(screen.queryByRole('button', { name: /Save changes/ })).toBeNull();
  });

  it('lets whoever manages the settings see the runs but not bill', async () => {
    const user = userEvent.setup();
    as([PERMISSION.ManageSettings]);
    billing.runs.mockResolvedValue([RUN]);
    show();

    await user.click(await screen.findByRole('tab', { name: 'Billing' }));
    expect(await screen.findByText('Started by Anna Keller')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Bill what is due now/ })).toBeNull();
    expect(screen.getByRole('button', { name: /Save changes/ })).toBeInTheDocument();
  });

  it('prices a new item in the workspace currency the accounts are kept in, not the club currency label', async () => {
    const user = userEvent.setup();
    // The club labels its amounts in euros; the workspace (every house account) is in francs.
    api.get.mockResolvedValue({ ...SETTINGS, currency: 'EUR' });
    catalog.create.mockResolvedValue(tier('Junior', 30, 2));
    show();

    await user.click(await screen.findByRole('button', { name: /Add a tier/ }));
    expect(screen.getByText('Price each period (CHF)')).toBeInTheDocument();
    await user.type(screen.getByLabelText('New item name'), 'Junior');
    await user.type(screen.getByLabelText('New item price'), '30');
    await user.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(catalog.create).toHaveBeenCalledTimes(1));
    expect(catalog.create.mock.calls[0]![0]).toMatchObject({ kind: 'Membership', name: 'Junior', price: 30 });
    // Left to the server, which prices it in the workspace's currency: priced in the label's,
    // it could be charged to no member's account.
    expect(catalog.create.mock.calls[0]![0]).not.toHaveProperty('currency');
  });

  it('says what is due could not be read, rather than reading for ever, and reads it again', async () => {
    const user = userEvent.setup();
    as([PERMISSION.ManageMoney]);
    billing.preview.mockRejectedValueOnce(new Error('The server did not answer.'))
      .mockResolvedValue({ subscriptions: 1, periods: 2, amountByCurrency: { CHF: 598 }, items: [] });
    billing.runs.mockRejectedValueOnce(new Error('The server did not answer.')).mockResolvedValue([RUN]);
    show();

    await user.click(await screen.findByRole('tab', { name: 'Billing' }));
    expect(await screen.findByText('What is due could not be read.')).toBeInTheDocument();
    expect(screen.queryByText(/Reading what is due/)).toBeNull();
    expect(screen.getByText('The runs could not be read.')).toBeInTheDocument();

    const [retryDue, retryRuns] = screen.getAllByRole('button', { name: 'Try again' });
    await user.click(retryDue!);
    expect(await screen.findByText(/2 periods for 1 subscription/)).toBeInTheDocument();
    await user.click(retryRuns!);
    expect(await screen.findByText('Started by Anna Keller')).toBeInTheDocument();
  });

  it('offers nothing to change, and reads no runs, for a member who manages neither', async () => {
    const user = userEvent.setup();
    as([PERMISSION.EditRecords]);
    show();

    expect(await screen.findByLabelText('Gold dues')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save tier' })).toBeNull();
    expect(screen.queryByRole('button', { name: /Add a tier/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Save changes/ })).toBeNull();
    await user.click(screen.getByRole('tab', { name: 'Billing' }));
    expect(await screen.findByText(/sees the runs here/)).toBeInTheDocument();
    expect(billing.runs).not.toHaveBeenCalled();
    expect(billing.preview).not.toHaveBeenCalled();
  });
});
