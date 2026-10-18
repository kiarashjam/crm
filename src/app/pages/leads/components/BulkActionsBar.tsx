import { useState } from 'react';
import { Trash2, X, Loader2 } from 'lucide-react';
import { Button } from '@/app/components/ui/button';

interface BulkActionsBarProps {
  count: number;
  members: { userId: string; name: string }[];
  onClear: () => void;
  onBulkDelete: () => Promise<void>;
  onBulkApply: (patch: { source?: string; assignedToUserId?: string }) => Promise<void>;
}

/**
 * Sticky toolbar for the leads that are checked: source and owner, for the whole
 * selection at once.
 *
 * There is deliberately no status field. A lead's status follows its five-phase
 * pipeline and is read-only — the bulk status box that came back with #57 set
 * the text without touching the pipeline, so the badge flagged a disagreement and
 * the next pipeline edit quietly overwrote it, and a typo invented a status
 * outside the agreed list. The server now refuses it too.
 */
export function BulkActionsBar({
  count,
  members,
  onClear,
  onBulkDelete,
  onBulkApply,
}: BulkActionsBarProps) {
  const [busy, setBusy] = useState<null | 'delete' | 'apply'>(null);
  const [source, setSource] = useState('');
  const [owner, setOwner] = useState('');
  const chosen = source.trim() !== '' || owner !== '';

  const runDelete = async () => {
    setBusy('delete');
    try {
      await onBulkDelete();
    } finally {
      setBusy(null);
    }
  };

  const runApply = async () => {
    if (!chosen) return;
    setBusy('apply');
    try {
      await onBulkApply({
        source: source.trim() || undefined,
        assignedToUserId: owner || undefined,
      });
      setSource('');
      setOwner('');
    } finally {
      setBusy(null);
    }
  };

  return (
    <div
      role="toolbar"
      aria-label={`Actions for ${count} selected lead${count === 1 ? '' : 's'}`}
      className="sticky top-2 z-30 mb-3 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-indigo-200/70 bg-white px-4 py-2.5 dark:border-indigo-900 dark:bg-slate-900"
    >
      <span className="text-sm font-semibold text-slate-900 dark:text-white">{count} selected</span>
      <div className="flex flex-wrap items-center gap-2">
        <label className="sr-only" htmlFor="bulk-source">Set source</label>
        <input
          id="bulk-source"
          value={source}
          maxLength={64}
          onChange={(e) => setSource(e.target.value)}
          placeholder="Set source"
          className="h-8 w-32 rounded-lg border border-slate-200 px-2 text-sm dark:border-slate-700 dark:bg-slate-950"
        />
        <label className="sr-only" htmlFor="bulk-owner">Set owner</label>
        <select
          id="bulk-owner"
          value={owner}
          onChange={(e) => setOwner(e.target.value)}
          className="h-8 rounded-lg border border-slate-200 px-2 text-sm dark:border-slate-700 dark:bg-slate-950"
        >
          <option value="">Set owner</option>
          {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        <Button
          variant="outline"
          size="sm"
          disabled={busy !== null || !chosen}
          onClick={runApply}
          title={chosen ? undefined : 'Choose a source or an owner first'}
        >
          {busy === 'apply' ? <Loader2 className="w-4 h-4 animate-spin" aria-label="Applying" /> : 'Apply'}
        </Button>
        <Button variant="outline" size="sm" disabled={busy !== null} onClick={runDelete} className="gap-1.5 text-red-600 border-red-200">
          {busy === 'delete' ? <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" /> : <Trash2 className="w-4 h-4" aria-hidden="true" />}
          Delete
        </Button>
        <Button variant="ghost" size="sm" onClick={onClear} aria-label="Clear selection" className="h-8 w-8 p-0">
          <X className="w-4 h-4" aria-hidden="true" />
        </Button>
      </div>
    </div>
  );
}
