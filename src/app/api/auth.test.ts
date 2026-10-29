// Turning 2FA on or off ends every earlier session on the server, this browser's too.
// The answer carries the token to carry on with; without storing it, the next request
// would be refused and the person who just secured their account signed out.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { getAuthToken, getCurrentOrganizationId, getCurrentUser, setCurrentOrganizationId, setSession } from '@/app/lib/auth';

const authFetchJson = vi.fn();

vi.mock('./apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./apiClient')>()),
  authFetchJson: (...args: unknown[]) => authFetchJson(...args),
}));

import { twoFactorDisable, twoFactorEnable } from './auth';

beforeEach(() => {
  localStorage.clear();
  authFetchJson.mockReset();
  setSession('old-token', { id: 'u1', name: 'Kia', email: 'kia@club.ch', emailVerified: true });
  setCurrentOrganizationId('org-1');
});

describe('changing 2FA', () => {
  it('keeps this browser signed in with the token the server hands back', async () => {
    authFetchJson.mockResolvedValueOnce({ token: 'after-enable' });
    await twoFactorEnable('pw', '123456');
    expect(authFetchJson).toHaveBeenCalledWith('/api/auth/2fa/enable', expect.objectContaining({ method: 'POST' }));
    expect(getAuthToken()).toBe('after-enable');

    authFetchJson.mockResolvedValueOnce({ token: 'after-disable' });
    await twoFactorDisable('pw', '654321');
    expect(getAuthToken()).toBe('after-disable');

    // Same person, same workspace: only the token changes.
    expect(getCurrentUser()?.id).toBe('u1');
    expect(getCurrentOrganizationId()).toBe('org-1');
  });

  it('leaves the token alone when the change is refused', async () => {
    authFetchJson.mockRejectedValueOnce(new Error('The two-factor authentication code is invalid'));
    await expect(twoFactorEnable('pw', '000000')).rejects.toThrow('invalid');
    expect(getAuthToken()).toBe('old-token');
  });
});
