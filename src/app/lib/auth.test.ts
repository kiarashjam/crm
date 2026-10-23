import { beforeEach, describe, expect, it } from 'vitest';
import { clearSession, setSession } from './auth';

describe('signing out', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('forgets the sales agent conversations kept in this browser', () => {
    setSession('token', { id: 'user-1', name: 'Rep', email: 'rep@example.com' });
    localStorage.setItem('cadence.agent.turns.user-1:org-a', '[{"role":"you","text":"brief Ada Lovelace"}]');
    localStorage.setItem('cadence.agent.focus.user-1:org-a', '{"name":"Ada Lovelace"}');
    sessionStorage.setItem('cadence.agent.turns.user-1:org-b', '[]');
    localStorage.setItem('cadence.nextActions.open.home', 'true');

    clearSession();

    expect(localStorage.getItem('aci_token')).toBeNull();
    expect(localStorage.getItem('cadence.agent.turns.user-1:org-a')).toBeNull();
    expect(localStorage.getItem('cadence.agent.focus.user-1:org-a')).toBeNull();
    expect(sessionStorage.getItem('cadence.agent.turns.user-1:org-b')).toBeNull();
    // A layout preference names nobody and stays.
    expect(localStorage.getItem('cadence.nextActions.open.home')).toBe('true');
  });
});
