// Settings > Account > Logout was a link to /login. The session stayed in storage, so
// the next person at a shared reception PC typed /dashboard and was in as the last one.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { getAuthToken, getCurrentUser, isLoggedIn, setSession } from '@/app/lib/auth';
import type { UserSettings } from '@/app/api/types';
import { AccountSection } from './AccountSection';

function Landed() {
  return <div data-testid="landed">{useLocation().pathname}</div>;
}

function renderSection() {
  return render(
    <MemoryRouter initialEntries={['/settings?tab=account']}>
      <Routes>
        <Route
          path="/settings"
          element={(
            <AccountSection
              settings={{ defaultCurrency: 'CHF', defaultFollowUpDays: 3, showActivityStatus: true, allowAnalytics: true } as UserSettings}
              updateSettings={vi.fn()}
              currencies={[]}
              handleReset={vi.fn()}
            />
          )}
        />
        <Route path="*" element={<Landed />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => localStorage.clear());

describe('Settings > Account > Logout', () => {
  it('ends the session in this browser, then shows the sign-in page', async () => {
    setSession('jwt', { id: 'u1', name: 'Front desk', email: 'desk@club.ch' });
    localStorage.setItem('aci_org_id', 'org-1');
    const user = userEvent.setup();
    renderSection();

    await user.click(screen.getByText('Logout'));

    expect(screen.getByTestId('landed')).toHaveTextContent('/login');
    expect(getAuthToken()).toBeNull();
    expect(getCurrentUser()).toBeNull();
    expect(isLoggedIn()).toBe(false);
    expect(localStorage.getItem('aci_org_id')).toBeNull();
  });
});

describe('Settings > Account > deleting and exporting', () => {
  // "Delete Account" asked for DELETE, promised permanent deletion, and went home with
  // everything intact; "Export Data" did nothing. Neither may pretend.
  it('offers no button that claims to delete or export, and says how to ask instead', () => {
    renderSection();

    expect(screen.queryByRole('button', { name: /delete/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /export/i })).toBeNull();
    expect(screen.queryByText(/permanently deleted/i)).toBeNull();
    expect(screen.getByText(/cannot delete an account from this page yet/i)).toBeInTheDocument();
    expect(screen.getByText(/Export CSV on the Leads, Contacts/)).toBeInTheDocument();
  });
});
