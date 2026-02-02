import { Info } from 'lucide-react';
import { isDemoMode } from '@/app/lib/auth';

/**
 * Full-width banner shown on every page when in demo mode. Invisible when not in demo.
 */
export default function DemoBanner() {
  if (!isDemoMode()) return null;

  return (
    <div
      className="w-full bg-amber-50 border-b border-amber-200/80 py-2 px-[var(--page-padding)]"
      role="status"
      aria-live="polite"
    >
      <div className="flex items-center justify-center gap-2 text-amber-800 text-sm font-medium">
        <Info className="w-4 h-4 shrink-0 text-amber-600" aria-hidden />
        <span>
          Demo mode — You're viewing sample data. Data is not saved to a backend. Connect an API or sign in with a real account to use your own data.
        </span>
      </div>
    </div>
  );
}
