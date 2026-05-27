import { useState } from 'react';
import { Info, RotateCcw } from 'lucide-react';
import { isDemoMode } from '@/app/lib/auth';

const DEMO_STORAGE_KEYS = [
  'crm.mock.leads.v1',
  'crm.mock.contacts.v1',
  'crm.mock.deals.v1',
  'crm.mock.tasks.v1',
  'crm.mock.activities.v1',
];

function resetDemoData(): void {
  try {
    DEMO_STORAGE_KEYS.forEach((key) => localStorage.removeItem(key));
  } catch {
    // ignore — Safari private mode etc.
  }
}

/**
 * Full-width banner shown on every page when in demo mode. Invisible when not in demo.
 *
 * Demo data persists in localStorage so changes survive across page loads (without
 * this, every "Add lead" would silently fail). The "Reset" button wipes that
 * local store so a fresh demo experience is one click away.
 */
export default function DemoBanner() {
  const [resetting, setResetting] = useState(false);
  if (!isDemoMode()) return null;

  const handleReset = () => {
    if (resetting) return;
    if (!window.confirm('Reset all demo data back to the original sample? Any changes you made in this browser will be lost.')) {
      return;
    }
    setResetting(true);
    resetDemoData();
    // Hard reload so every list re-seeds from mockData.
    window.location.reload();
  };

  return (
    <div
      className="w-full bg-amber-50 border-b border-amber-200/80 py-2 px-[var(--page-padding)]"
      role="status"
      aria-live="polite"
    >
      <div className="flex items-center justify-center gap-3 text-amber-800 text-sm font-medium">
        <Info className="w-4 h-4 shrink-0 text-amber-600" aria-hidden />
        <span className="flex-1 max-w-3xl text-center">
          Demo mode — sample data only, saved locally in your browser. Connect an API or sign in to use real data.
        </span>
        <button
          type="button"
          onClick={handleReset}
          disabled={resetting}
          className="inline-flex items-center gap-1 rounded-md border border-amber-300 bg-white/60 px-2 py-0.5 text-xs font-semibold text-amber-800 hover:bg-white disabled:opacity-60 transition-colors"
          aria-label="Reset demo data"
        >
          <RotateCcw className={`w-3 h-3 ${resetting ? 'animate-spin' : ''}`} aria-hidden />
          {resetting ? 'Resetting…' : 'Reset demo data'}
        </button>
      </div>
    </div>
  );
}
