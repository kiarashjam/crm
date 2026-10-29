import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { clearSession } from './auth';

/**
 * Sign out of this browser and go to the sign-in page. Every "Logout" and "Sign in
 * as someone else" uses this: a plain link to /login leaves the session in storage,
 * so the next person at a shared reception PC is still signed in as the last one.
 */
export function useSignOut(): () => void {
  const navigate = useNavigate();
  return useCallback(() => {
    clearSession();
    navigate('/login', { replace: true });
  }, [navigate]);
}
