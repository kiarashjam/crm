// Opening Settings asked the server for 2FA "setup", which answered with the live
// TOTP secret whenever 2FA was on, and kept it in React state. Opening the page now
// asks only whether 2FA is on.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

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
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ organizations: [], currentOrg: null, setCurrentOrg: vi.fn(), refreshOrgs: vi.fn() }),
  useOrgOptional: () => null,
}));
vi.mock('@/app/contexts/ThemeContext', () => ({
  useTheme: () => ({ updateAppearance: vi.fn() }),
  useThemeOptional: () => null,
}));

import Settings from './Settings';

beforeEach(() => {
  twoFactorSetup.mockClear();
  twoFactorStatus.mockClear();
});

describe('opening Settings', () => {
  it('asks only whether 2FA is on, never for the secret', async () => {
    render(
      <MemoryRouter initialEntries={['/settings?tab=security']}>
        <Settings />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Enabled')).toBeInTheDocument();
    expect(twoFactorStatus).toHaveBeenCalledTimes(1);
    expect(twoFactorSetup).not.toHaveBeenCalled();
    expect(screen.queryByText('JBSWY3DPEHPK3PXP')).toBeNull();
  });
});
