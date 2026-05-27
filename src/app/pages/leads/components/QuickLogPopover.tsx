import { useState } from 'react';
import { Phone, Mail, Calendar, Briefcase, Loader2, Send } from 'lucide-react';
import { toast } from 'sonner';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';

type QuickLogType = 'call' | 'email' | 'meeting' | 'note';

const TYPES: { id: QuickLogType; label: string; icon: typeof Phone; tone: string }[] = [
  { id: 'call', label: 'Call', icon: Phone, tone: 'text-blue-600 bg-blue-50 border-blue-200' },
  { id: 'email', label: 'Email', icon: Mail, tone: 'text-purple-600 bg-purple-50 border-purple-200' },
  { id: 'meeting', label: 'Meeting', icon: Calendar, tone: 'text-emerald-600 bg-emerald-50 border-emerald-200' },
  { id: 'note', label: 'Note', icon: Briefcase, tone: 'text-amber-600 bg-amber-50 border-amber-200' },
];

interface QuickLogPopoverProps {
  /** Called with the activity payload; should persist and resolve. */
  onSubmit: (payload: { type: QuickLogType; body: string }) => Promise<void>;
  /** The trigger element. Click stops propagation so the card doesn't navigate. */
  trigger: React.ReactNode;
}

/**
 * Lets the user log a quick call/email/meeting/note directly from the lead card
 * without opening the detail modal. One textarea + type selector + submit.
 */
export function QuickLogPopover({ onSubmit, trigger }: QuickLogPopoverProps) {
  const [open, setOpen] = useState(false);
  const [type, setType] = useState<QuickLogType>('note');
  const [body, setBody] = useState('');
  const [saving, setSaving] = useState(false);

  const reset = () => {
    setType('note');
    setBody('');
  };

  const handleSubmit = async () => {
    const trimmed = body.trim();
    if (!trimmed || saving) return;
    setSaving(true);
    try {
      await onSubmit({ type, body: trimmed });
      toast.success(`Logged ${type}`);
      reset();
      setOpen(false);
    } catch (err) {
      console.error('Failed to log activity', err);
      toast.error('Failed to log activity');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) reset();
      }}
    >
      <PopoverTrigger
        asChild
        onClick={(e) => e.stopPropagation()}
      >
        {trigger}
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-80 p-3 space-y-3"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="text-[11px] uppercase tracking-wider text-slate-400 font-semibold">
          Log activity
        </div>
        <div className="grid grid-cols-4 gap-1.5">
          {TYPES.map((t) => {
            const Icon = t.icon;
            const active = type === t.id;
            return (
              <button
                key={t.id}
                type="button"
                onClick={() => setType(t.id)}
                className={cn(
                  'flex flex-col items-center gap-1 rounded-lg border px-2 py-2 text-xs transition-all',
                  active
                    ? `${t.tone} font-semibold ring-2 ring-offset-1 ring-indigo-300`
                    : 'border-slate-200 text-slate-600 hover:bg-slate-50',
                )}
                aria-pressed={active}
              >
                <Icon className="w-4 h-4" />
                {t.label}
              </button>
            );
          })}
        </div>
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder={`What happened? (${type})`}
          rows={3}
          autoFocus
          className="w-full resize-none rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
          onKeyDown={(e) => {
            if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') {
              e.preventDefault();
              handleSubmit();
            }
          }}
        />
        <div className="flex items-center justify-between gap-2">
          <span className="text-[11px] text-slate-400">
            <kbd className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-[10px]">⌘</kbd>
            {' + '}
            <kbd className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-[10px]">Enter</kbd>
            {' to save'}
          </span>
          <Button
            size="sm"
            onClick={handleSubmit}
            disabled={!body.trim() || saving}
            className="gap-1.5"
          >
            {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
            Log
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
