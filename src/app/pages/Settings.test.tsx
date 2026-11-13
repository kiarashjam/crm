// Opening Settings asked the server for 2FA "setup", which answered with the live
// TOTP secret whenever 2FA was on, and kept it in React state. Opening the page now
// asks only whether 2FA is on.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';

const twoFactorSetup = vi.fn(async () => ({ enabled: true, secret: 'JBSWY3DPEHPK3PXP', otpauthUri: 'otpauth://totp/x' }));
const twoFactorStatus = vi.fn(async () => ({ enabled: true }));

vi.mock('@/app/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api')>()),
  getUserSettings: vi.fn(async () => ({
    brandName: 'Club', brandTone: 'professional', theme: 'light', dataDensity: 'comfortable',
    sidebarCollapsed: false, showWelcomeBanner: true, timezone: 'UTC', language: 'en',
    emailNotificationsEnabled: true, emailOnNewLead: true, emailOnDealUpdate: true, emailOnTaskDue: true,
    emailOnTeamMention: true, inAppNotificationsEnabled: true, inAppSoundEnabled: true,
    browserNotificationsEnabled: false, defaultFollowUpDays: 3, defaultCurrency: 'CHF',
    showActivityStatus: true, allowAnalytics: true,
  })),
  getTimezones: vi.fn(async () => []),
  getCurrencies: vi.fn(async () => []),
  getLanguages: vi.fn(async () => []),
  twoFactorSetup: () => twoFactorSetup(),
  twoFactorStatus: () => twoFactorStatus(),
}));

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('./McpSetup', () => ({ ClaudeSection: () => null }));
// The workspace in use: none (personal) unless a test sets one.
let currentOrg: Record<string, unknown> | null = null;
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ organizations: currentOrg ? [currentOrg] : [], currentOrg, setCurrentOrg: vi.fn(), refreshOrgs: vi.fn() }),
  useOrgOptional: () => (currentOrg ? { currentOrg } : null),
}));
vi.mock('@/app/contexts/ThemeContext', () => ({
  useTheme: () => ({ updateAppearance: vi.fn() }),
  useThemeOptional: () => null,
}));

import Settings from './Settings';

beforeEach(() => {
  twoFactorSetup.mockClear();
  twoFactorStatus.mockClear();
  currentOrg = null;
});

// The 2FA code boxes (input-otp) start timers at 0, 10 and 50 ms each time they render, and
// never clear them. This test can end within those 50 ms; a timer that then fires after the
// file's DOM is gone throws "window is not defined", and Vitest fails the whole run although
// every test passed (it did on PR #91). So the page is unmounted and the timers are let run
// out while there is still a DOM, as Login.twoFactor.test.tsx does.
afterEach(async () => {
  cleanup();
  await new Promise((resolve) => setTimeout(resolve, 100));
});

describe('opening Settings', () => {
  it('asks only whether 2FA is on, never for the secret', async () => {
    render(withQueries(
      <MemoryRouter initialEntries={['/settings?tab=security']}>
        <Settings />
      </MemoryRouter>,
    ));

    expect(await screen.findByText('Enabled')).toBeInTheDocument();
    expect(twoFactorStatus).toHaveBeenCalledTimes(1);
    expect(twoFactorSetup).not.toHaveBeenCalled();
    expect(screen.queryByText('JBSWY3DPEHPK3PXP')).toBeNull();
  });
});

describe('the Sales module turned off', () => {
  it('leaves the pipelines out of Settings, and keeps Welcome & portal for the club’s member portal', async () => {
    currentOrg = { id: 'org-1', name: 'Pavillon 46', role: 'owner', salesEnabled: false, clubEnabled: true };
    render(withQueries(
      <MemoryRouter initialEntries={['/settings?tab=welcome']}>
        <Settings />
      </MemoryRouter>,
    ));

    expect((await screen.findAllByText('Team')).length).toBeGreaterThan(0);
    expect(screen.queryByText('Pipelines')).toBeNull();
    expect(screen.getAllByText('Welcome & portal').length).toBeGreaterThan(0);
    // The hand-over a deal's contact is given belongs to Sales: said, not offered.
    expect(screen.getByText(/Handing a deal's contact to your own portal belongs to the Sales module/)).toBeInTheDocument();
  });

  it('leaves Welcome & portal out too when the Club module is off as well', async () => {
    currentOrg = { id: 'org-1', name: 'Pavillon 46', role: 'owner', salesEnabled: false, clubEnabled: false };
    render(withQueries(
      <MemoryRouter initialEntries={['/settings?tab=profile']}>
        <Settings />
      </MemoryRouter>,
    ));

    expect((await screen.findAllByText('Team')).length).toBeGreaterThan(0);
    expect(screen.queryByText('Welcome & portal')).toBeNull();
  });

  it('offers them while it is on', async () => {
    currentOrg = { id: 'org-1', name: 'Pavillon 46', role: 'owner', salesEnabled: true, clubEnabled: true };
    render(withQueries(
      <MemoryRouter initialEntries={['/settings?tab=profile']}>
        <Settings />
      </MemoryRouter>,
    ));

    expect((await screen.findAllByText('Pipelines')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Welcome & portal').length).toBeGreaterThan(0);
  });
});
