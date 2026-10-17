import { useState } from 'react';
import { Trash2, X, Loader2 } from 'lucide-react';
import { Button } from '@/app/components/ui/button';

interface BulkActionsBarProps {
  count: number;
  members: { userId: string; name: string }[];
  onClear: () => void;
  onBulkDelete: () => Promise<void>;
  onBulkApply: (patch: { status?: string; source?: string; assignedToUserId?: string }) => Promise<void>;
}

/**
 * Sticky toolbar for the leads that are checked.
 * Status, source, and owner are written together for the whole selection.
 */
export function BulkActionsBar({
  count,
  members,
  onClear,
  onBulkDelete,
  onBulkApply,
}: BulkActionsBarProps) {
  const [busy, setBusy] = useState<null | 'delete' | 'apply'>(null);
  const [status, setStatus] = useState('');
  const [source, setSource] = useState('');
  const [owner, setOwner] = useState('');

  const runDelete = async () => {
    setBusy('delete');
    try {
      await onBulkDelete();
    } finally {
      setBusy(null);
    }
  };

  const runApply = async () => {
    setBusy('apply');
    try {
      await onBulkApply({
        status: status || undefined,
        source: source || undefined,
        assignedToUserId: owner || undefined,
      });
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="sticky top-2 z-30 mb-3 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-indigo-200/70 bg-white px-4 py-2.5">
      <span className="text-sm font-semibold text-slate-900">{count} selected</span>
      <div className="flex flex-wrap items-center gap-2">
        <input value={status} onChange={(e) => setStatus(e.target.value)} placeholder="Status" className="h-8 w-28 rounded-lg border border-slate-200 px-2 text-sm" />
        <input value={source} onChange={(e) => setSource(e.target.value)} placeholder="Source" className="h-8 w-28 rounded-lg border border-slate-200 px-2 text-sm" />
        <select value={owner} onChange={(e) => setOwner(e.target.value)} className="h-8 rounded-lg border border-slate-200 px-2 text-sm">
          <option value="">Owner</option>
          {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        <Button variant="outline" size="sm" disabled={busy !== null} onClick={runApply}>
          {busy === 'apply' ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Apply'}
        </Button>
        <Button variant="outline" size="sm" disabled={busy !== null} onClick={runDelete} className="gap-1.5 text-red-600 border-red-200">
          {busy === 'delete' ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
          Delete
        </Button>
        <Button variant="ghost" size="sm" onClick={onClear} aria-label="Clear selection" className="h-8 w-8 p-0">
          <X className="w-4 h-4" />
        </Button>
      </div>
    </div>
  );
}
