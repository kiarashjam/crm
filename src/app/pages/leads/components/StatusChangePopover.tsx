import { useState } from 'react';
import { Check, ChevronDown, Loader2 } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';

interface StatusChangePopoverProps {
  currentStatus: string;
  statuses: string[];
  onChange: (newStatus: string) => Promise<void>;
  disabled?: boolean;
  /** Existing badge classes — passed through so the trigger looks identical to a static badge. */
  className?: string;
  /** Optional inline content rendered before the status label (e.g. animated dot). */
  prefix?: React.ReactNode;
}

/**
 * Wraps a lead status badge with a popover that lets the user change the status
 * without opening the detail modal. The trigger swallows the row-click so opening
 * the popover doesn't navigate into the lead.
 */
export function StatusChangePopover({
  currentStatus,
  statuses,
  onChange,
  disabled,
  className,
  prefix,
}: StatusChangePopoverProps) {
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const handlePick = async (status: string) => {
    if (status === currentStatus) {
      setOpen(false);
      return;
    }
    setSaving(true);
    try {
      await onChange(status);
      setOpen(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled || saving}
          onClick={(e) => e.stopPropagation()}
          aria-label={`Change status, current ${currentStatus}`}
          className={cn(
            'group/badge inline-flex items-center gap-1.5 cursor-pointer transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1 disabled:opacity-60 disabled:cursor-not-allowed',
            className,
          )}
        >
          {prefix}
          {currentStatus}
          {saving ? (
            <Loader2 className="w-3 h-3 animate-spin" />
          ) : (
            <ChevronDown className="w-3 h-3 opacity-70 group-hover/badge:opacity-100" />
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-56 p-1"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-2 py-1.5 text-[11px] uppercase tracking-wider text-slate-400 font-semibold">
          Change status
        </div>
        <div className="flex flex-col">
          {statuses.map((status) => {
            const active = status === currentStatus;
            return (
              <button
                key={status}
                type="button"
                onClick={() => handlePick(status)}
                className={cn(
                  'flex items-center justify-between gap-2 rounded-md px-2 py-1.5 text-sm text-left transition-colors',
                  active
                    ? 'bg-indigo-50 text-indigo-900 font-semibold'
                    : 'hover:bg-slate-50 text-slate-700',
                )}
              >
                <span>{status}</span>
                {active && <Check className="w-4 h-4 text-indigo-600" />}
              </button>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}
