import { useState } from 'react';
import { Check, UserCircle } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';
import type { OrgMemberDto } from '@/app/api/organizations';

interface AssignPopoverProps {
  /** Currently assigned user id (undefined = unassigned). */
  assigneeId?: string;
  members: OrgMemberDto[];
  /** Called with the new user id, or '' to unassign. */
  onAssign: (userId: string) => void;
  /** The clickable element that opens the picker. */
  trigger: React.ReactNode;
}

/**
 * Inline owner picker for a lead card: lists org members (+ Unassigned) and
 * reassigns without leaving the list. Stops click propagation so the card
 * doesn't navigate to the detail page.
 */
export function AssignPopover({ assigneeId, members, onAssign, trigger }: AssignPopoverProps) {
  const [open, setOpen] = useState(false);

  const pick = (userId: string) => {
    onAssign(userId);
    setOpen(false);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild onClick={(e) => e.stopPropagation()}>
        {trigger}
      </PopoverTrigger>
      <PopoverContent align="start" className="w-60 p-1.5" onClick={(e) => e.stopPropagation()}>
        <div className="px-2 py-1.5 text-[11px] font-semibold uppercase tracking-wider text-slate-400">
          Assign owner
        </div>
        <div className="max-h-64 overflow-y-auto">
          <button
            type="button"
            onClick={() => pick('')}
            className={cn(
              'flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-slate-50',
              !assigneeId && 'bg-slate-50',
            )}
          >
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100 text-slate-400">
              <UserCircle className="h-4 w-4" />
            </span>
            <span className="flex-1 text-slate-600">Unassigned</span>
            {!assigneeId && <Check className="h-4 w-4 text-indigo-600" />}
          </button>
          {members.map((m) => {
            const active = m.userId === assigneeId;
            return (
              <button
                key={m.userId}
                type="button"
                onClick={() => pick(m.userId)}
                className={cn(
                  'flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-slate-50',
                  active && 'bg-indigo-50/60',
                )}
              >
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-[11px] font-bold text-white ring-2 ring-white">
                  {(m.name?.[0] ?? '?').toUpperCase()}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium text-slate-800">{m.name}</span>
                  {m.email && <span className="block truncate text-[11px] text-slate-400">{m.email}</span>}
                </span>
                {active && <Check className="h-4 w-4 shrink-0 text-indigo-600" />}
              </button>
            );
          })}
          {members.length === 0 && (
            <p className="px-2 py-3 text-center text-xs text-slate-400">No team members to assign.</p>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
