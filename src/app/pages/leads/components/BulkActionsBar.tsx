import { useState } from 'react';
import { Trash2, X, Loader2 } from 'lucide-react';
import { Button } from '@/app/components/ui/button';

interface BulkActionsBarProps {
  count: number;
  onClear: () => void;
  onBulkDelete: () => Promise<void>;
}

/**
 * Sticky toolbar shown above the leads list when leads are selected.
 *
 * Bulk status change used to live here and has been removed, not hidden: a lead's
 * status is derived from its own 5-phase pipeline, and setting thirty leads to
 * "Qualified" in one click would assert progress that none of their pipelines
 * records. The next pipeline edit on any of them would silently undo it, which is
 * worse than not offering it. Moving a batch forward means recording the step that
 * actually happened, one lead at a time.
 */
export function BulkActionsBar({
  count,
  onClear,
  onBulkDelete,
}: BulkActionsBarProps) {
  const [busy, setBusy] = useState<null | 'delete'>(null);

  const runDelete = async () => {
    setBusy('delete');
    try {
      await onBulkDelete();
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="sticky top-2 z-30 mb-3 flex items-center justify-between gap-3 rounded-2xl border border-indigo-200/70 bg-gradient-to-r from-indigo-50 via-white to-violet-50 px-4 py-2.5 shadow-lg shadow-indigo-100/60 backdrop-blur">
      <div className="flex items-center gap-3 min-w-0">
        <span className="inline-flex items-center justify-center w-8 h-8 rounded-full bg-indigo-600 text-white text-sm font-semibold shadow">
          {count}
        </span>
        <div className="flex flex-col">
          <span className="text-sm font-semibold text-slate-900">
            {count} lead{count === 1 ? '' : 's'} selected
          </span>
          <span className="text-xs text-slate-500">Status follows each lead's own pipeline</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={busy !== null}
          onClick={runDelete}
          className="gap-1.5 text-red-600 hover:text-red-700 hover:bg-red-50 border-red-200"
        >
          {busy === 'delete' ? (
            <Loader2 className="w-4 h-4 animate-spin" />
          ) : (
            <Trash2 className="w-4 h-4" />
          )}
          Delete
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClear}
          disabled={busy !== null}
          aria-label="Clear selection"
          className="h-8 w-8 p-0 text-slate-500 hover:text-slate-700"
        >
          <X className="w-4 h-4" />
        </Button>
      </div>
    </div>
  );
}
