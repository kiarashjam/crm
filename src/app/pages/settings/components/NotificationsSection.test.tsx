// The weekly report digest is a switch that works, so it is not labelled
// "coming soon" like the ones that are only saved.

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import type { UserSettings } from '@/app/api/types';
import { NotificationsSection } from './NotificationsSection';

const SETTINGS = {
  emailNotificationsEnabled: true, emailOnNewLead: true, emailOnDealUpdate: true, emailOnTaskDue: true,
  emailOnTeamMention: true, inAppNotificationsEnabled: true, inAppSoundEnabled: true, browserNotificationsEnabled: false,
} as UserSettings;

describe('the weekly report digest switch', () => {
  it('starts off, turns on, and is not marked coming soon', () => {
    const updateSettings = vi.fn();
    render(<NotificationsSection settings={SETTINGS} updateSettings={updateSettings} />);

    const toggle = screen.getByText('Weekly report digest').closest('label')!;
    const sw = within(toggle).getByRole('switch');
    expect(sw).toHaveAttribute('aria-checked', 'false');
    expect(within(toggle).queryByText('Coming soon')).toBeNull();

    fireEvent.click(sw);
    expect(updateSettings).toHaveBeenCalledWith({ weeklyDigestEnabled: true });
  });
});
