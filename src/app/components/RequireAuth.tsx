import { Navigate, useLocation } from 'react-router-dom';
import { getCurrentUser } from '@/app/lib/auth';

/**
 * Wraps routes that require the user to be logged in (or in demo mode).
 * Redirects to /login when not authenticated, preserving the intended URL in state.
 */
export default function RequireAuth({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const user = getCurrentUser();

  if (user == null) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
}
