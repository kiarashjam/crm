// Settings > Profile offers a personal time zone, which times the weekly report digest.
// It says so, and which zone days are counted in everywhere else: the workspace's.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { UserSettings } from '@/app/api/types';

vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: { id: 'o', name: 'Brooklyn Club', timeZone: 'America/New_York' } }),
}));

const { ProfileSection } = await import('./ProfileSection');

describe('Settings > Profile > Timezone', () => {
  it("says this zone times the digest and days are counted in the workspace's, not that it does nothing", () => {
    render(
      <ProfileSection
        settings={{ timezone: 'Europe/Zurich', language: 'en' } as UserSettings}
        updateSettings={vi.fn()}
        timezones={[{ id: 'Europe/Zurich', displayName: 'Zurich', utcOffset: 60 }]}
        languages={[{ code: 'en', name: 'English', nativeName: 'English' }]}
      />,
    );

    // WeeklyDigestService sends the digest at 07:00 on Monday in this zone, as Settings >
    // Notifications says; "Saved for later" told people it did nothing.
    expect(screen.getByText(/weekly report digest goes out on Monday from 7:00 in this time zone/)).toBeInTheDocument();
    expect(screen.getByText(/count days in the workspace's time zone, America\/New York\./)).toBeInTheDocument();
    expect(screen.queryByText(/saved for later/i)).toBeNull();
  });
});
