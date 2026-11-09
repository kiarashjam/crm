// Settings > Profile offers a personal time zone that nothing reads yet. It says which
// zone days are counted in instead: the workspace's.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { UserSettings } from '@/app/api/types';

vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: { id: 'o', name: 'Brooklyn Club', timeZone: 'America/New_York' } }),
}));

const { ProfileSection } = await import('./ProfileSection');

describe('Settings > Profile > Timezone', () => {
  it("says that days are counted in the workspace's time zone, not in this one", () => {
    render(
      <ProfileSection
        settings={{ timezone: 'Europe/Zurich', language: 'en' } as UserSettings}
        updateSettings={vi.fn()}
        timezones={[{ id: 'Europe/Zurich', displayName: 'Zurich', utcOffset: 60 }]}
        languages={[{ code: 'en', name: 'English', nativeName: 'English' }]}
      />,
    );

    expect(screen.getByText(/Saved for later\. Reports, forecasts and lists by day count days in the workspace's time zone, America\/New York\./)).toBeInTheDocument();
  });
});
