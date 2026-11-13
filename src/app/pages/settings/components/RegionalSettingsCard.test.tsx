// The workspace's currency, locale and time zone had nowhere to be set: deals without a
// currency showed as dollars and the forecast's months began at UTC midnight. The owner
// or a manager sets them here, everyone else sees them, and nothing pretends to save.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Organization } from '@/app/api/organizations';

const state = vi.hoisted(() => ({
  org: null as Organization | null,
  demo: false,
  refreshOrgs: vi.fn(async () => {}),
  update: vi.fn(),
}));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ currentOrg: state.org, refreshOrgs: state.refreshOrgs }),
}));
vi.mock('@/app/api/apiClient', () => ({ servesDemoData: () => state.demo }));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  updateOrganizationSettings: (...args: unknown[]) => state.update(...args),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { RegionalSettingsCard } = await import('./RegionalSettingsCard');

const workspace = (over: Partial<Organization> = {}): Organization => ({
  id: 'org-1', name: 'Pavillon 46', ownerUserId: 'me', isOwner: true, role: 0,
  currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich', ...over,
});

beforeEach(() => {
  state.org = workspace();
  state.demo = false;
  state.refreshOrgs.mockClear();
  state.update.mockReset();
  state.update.mockImplementation(async (_id: string, s: object) => ({ ...state.org, ...s }));
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the workspace currency, number format and time zone', () => {
  it('lets a manager change them, saves what was chosen and reads the workspace again', async () => {
    state.org = workspace({ isOwner: false, role: 2 });
    const user = userEvent.setup();
    render(<RegionalSettingsCard />);

    expect(screen.getByLabelText('Currency')).toHaveValue('CHF');
    expect(screen.getByLabelText('Number format')).toHaveValue('de-CH');
    expect(screen.getByLabelText('Time zone')).toHaveValue('Europe/Zurich');
    // Nothing to save until something changes.
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();

    await user.selectOptions(screen.getByLabelText('Currency'), 'EUR');
    await user.selectOptions(screen.getByLabelText('Number format'), 'fr-CH');
    await user.selectOptions(screen.getByLabelText('Time zone'), 'Europe/Paris');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(state.refreshOrgs).toHaveBeenCalledTimes(1));
    expect(state.update).toHaveBeenCalledWith('org-1', { currency: 'EUR', locale: 'fr-CH', timeZone: 'Europe/Paris' });
    expect(toast.success).toHaveBeenCalled();
  });

  it('sends only what changed, so a zone the server cannot read does not hold up a new currency', async () => {
    // A Windows host without ICU knows no IANA zone, and the server refuses the whole
    // save over one field it cannot read: the unchanged Europe/Zurich went with every save.
    const user = userEvent.setup();
    render(<RegionalSettingsCard />);

    await user.selectOptions(screen.getByLabelText('Currency'), 'EUR');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(state.update).toHaveBeenCalledTimes(1));
    expect(state.update).toHaveBeenCalledWith('org-1', { currency: 'EUR' });
  });

  it("shows a sales rep the workspace's settings without letting them change them", () => {
    state.org = workspace({ isOwner: false, role: 1, currency: 'EUR' });
    render(<RegionalSettingsCard />);

    expect(screen.getByLabelText('Currency')).toHaveValue('EUR');
    expect(screen.getByLabelText('Currency')).toBeDisabled();
    expect(screen.getByLabelText('Time zone')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
    expect(screen.getByText(/takes managing the workspace's settings/i)).toBeInTheDocument();
  });

  it("says why the server refused, and does not claim it saved", async () => {
    state.update.mockRejectedValue(new Error('That is not a time zone this server knows.'));
    const user = userEvent.setup();
    render(<RegionalSettingsCard />);

    await user.selectOptions(screen.getByLabelText('Time zone'), 'Europe/Paris');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('That is not a time zone this server knows.'));
    expect(toast.success).not.toHaveBeenCalled();
    expect(state.refreshOrgs).not.toHaveBeenCalled();
    // The choice stays on screen to try again.
    expect(screen.getByLabelText('Time zone')).toHaveValue('Europe/Paris');
  });

  it('keeps a setting from elsewhere on offer rather than showing another one', () => {
    state.org = workspace({ currency: 'SEK', locale: 'sv-SE', timeZone: 'Europe/Stockholm' });
    render(<RegionalSettingsCard />);

    expect(screen.getByLabelText('Currency')).toHaveValue('SEK');
    expect(screen.getByLabelText('Number format')).toHaveValue('sv-SE');
    expect(screen.getByLabelText('Time zone')).toHaveValue('Europe/Stockholm');
  });

  it('names the pages that count days in the zone rather than every list', () => {
    // The Tasks page and the Calendar still count days on the viewer's clock.
    render(<RegionalSettingsCard />);

    expect(screen.getByText(/reports, forecasts, deal close dates and the activity lists count days and months in the time zone/)).toBeInTheDocument();
    expect(screen.queryByText(/lists by day/)).toBeNull();
  });

  it('lets the workspace choose semicolons for its CSV exports, as Excel in Switzerland reads them', async () => {
    const user = userEvent.setup();
    render(<RegionalSettingsCard />);

    expect(screen.getByLabelText('CSV exports')).toHaveValue('comma');
    await user.selectOptions(screen.getByLabelText('CSV exports'), 'semicolon');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(state.update).toHaveBeenCalledWith('org-1', { csvSemicolon: true }));
  });

  it('shows a workspace that chose semicolons as such', () => {
    state.org = workspace({ csvSemicolon: true });
    render(<RegionalSettingsCard />);
    expect(screen.getByLabelText('CSV exports')).toHaveValue('semicolon');
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('is not offered in the demo, which has no server to keep it', () => {
    state.demo = true;
    const { container } = render(<RegionalSettingsCard />);
    expect(container).toBeEmptyDOMElement();
  });
});
