import { useState } from 'react';
import { Trash2, X, Loader2, CircleDot } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from '@/app/components/ui/dropdown-menu';

interface BulkActionsBarProps {
  count: number;
  statuses: string[];
  onClear: () => void;
  onBulkStatusChange: (newStatus: string) => Promise<void>;
  onBulkDelete: () => Promise<void>;
}

/**
 * Sticky toolbar that appears above the leads list when one or more leads are
 * selected. Provides bulk delete and bulk status change.
 */
export function BulkActionsBar({
  count,
  statuses,
  onClear,
  onBulkStatusChange,
  onBulkDelete,
}: BulkActionsBarProps) {
  const [busy, setBusy] = useState<null | 'status' | 'delete'>(null);

  const runStatus = async (status: string) => {
    setBusy('status');
    try {
      await onBulkStatusChange(status);
    } finally {
      setBusy(null);
    }
  };

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
          <span className="text-xs text-slate-500">Choose a bulk action below</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="outline"
              size="sm"
              disabled={busy !== null || statuses.length === 0}
              className="gap-1.5"
            >
              {busy === 'status' ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <CircleDot className="w-4 h-4" />
              )}
              Change status
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel>Set status for {count} lead{count === 1 ? '' : 's'}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {statuses.map((status) => (
              <DropdownMenuItem
                key={status}
                onClick={() => runStatus(status)}
                className="cursor-pointer"
              >
                {status}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
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
