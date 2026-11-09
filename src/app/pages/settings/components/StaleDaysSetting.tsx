import { useEffect, useState } from 'react';
import { Clock } from 'lucide-react';
import { toast } from 'sonner';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { isOrgAdmin, updateOrganizationSettings } from '@/app/api/organizations';

/** The workspace's default when nobody has changed it; the server's too. */
const DEFAULT_DAYS = 30;

/** Whether text is a setting the server takes: 0 (never) or 1 to 365 whole days. */
export function parseStaleDays(text: string): number | null {
  const trimmed = text.trim();
  if (!/^\d{1,3}$/.test(trimmed)) return null;
  const days = Number(trimmed);
  return days <= 365 ? days : null;
}

/**
 * After how many days without activity an open deal is flagged as stale on the board,
 * for the whole workspace. A pipeline can set its own below. Owner or manager only.
 */
export function StaleDaysSetting() {
  const ctx = useOrgOptional();
  const org = ctx?.currentOrg ?? null;
  const current = org?.staleDealDays ?? DEFAULT_DAYS;
  const [text, setText] = useState(String(current));
  const [saving, setSaving] = useState(false);
  useEffect(() => setText(String(current)), [current]);

  if (!org || !isOrgAdmin(org)) return null;
  const days = parseStaleDays(text);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (days === null || days === current) return;
    setSaving(true);
    try {
      await updateOrganizationSettings(org.id, { staleDealDays: days });
      await ctx?.refreshOrgs();
      toast.success(days === 0 ? 'Deals are no longer flagged as stale' : `Open deals are stale after ${days} quiet days`);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'Could not save the stale-deal days.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={save} className="rounded-xl border border-slate-200 bg-slate-50 p-4">
      <label htmlFor="workspace-stale-days" className="flex items-center gap-2 text-sm font-medium text-slate-800">
        <Clock className="h-4 w-4 text-amber-600" aria-hidden />
        Flag an open deal as stale after
      </label>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <input
          id="workspace-stale-days"
          type="number"
          min={0}
          max={365}
          value={text}
          onChange={(e) => setText(e.target.value)}
          aria-invalid={days === null}
          className="h-10 w-24 rounded-lg border border-slate-300 px-3 text-slate-900"
        />
        <span className="text-sm text-slate-600">days with nothing logged on it and no stage move. 0 never flags a deal.</span>
        <button
          type="submit"
          disabled={saving || days === null || days === current}
          aria-label="Save the workspace's stale days"
          className="h-10 rounded-lg bg-orange-600 px-4 text-sm font-medium text-white disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save'}
        </button>
      </div>
      {days === null && <p className="mt-1 text-xs text-red-600">A whole number of days from 0 to 365.</p>}
      <p className="mt-1 text-xs text-slate-500">Applies to every pipeline that does not set its own.</p>
    </form>
  );
}
