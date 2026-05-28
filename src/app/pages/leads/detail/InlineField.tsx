import { useEffect, useRef, useState } from 'react';
import { Check, Loader2, Pencil, X } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';

interface InlineFieldProps {
  label: string;
  value: string;
  /** Persist the new value. Resolve true on success. */
  onSave: (next: string) => Promise<boolean>;
  /** "input" (default) for single-line, "textarea" for multi-line. */
  variant?: 'input' | 'textarea';
  type?: 'text' | 'email' | 'tel' | 'url' | 'number';
  placeholder?: string;
  /** Optional icon to render to the left of the value. */
  icon?: React.ReactNode;
  /** Optional explicit display node (overrides default text rendering). */
  renderValue?: (value: string) => React.ReactNode;
  readOnly?: boolean;
  emptyHint?: string;
}

/**
 * Click-to-edit field. The whole row is the trigger; on click it swaps the
 * read-only display for an input/textarea. Enter saves (textarea: Cmd+Enter),
 * Escape cancels, blur saves. A subtle pencil icon hints at editability on
 * hover, so the page doesn't look like a noisy form.
 */
export function InlineField({
  label,
  value,
  onSave,
  variant = 'input',
  type = 'text',
  placeholder,
  icon,
  renderValue,
  readOnly,
  emptyHint = 'Not set',
}: InlineFieldProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [saving, setSaving] = useState(false);
  const inputRef = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);

  useEffect(() => {
    if (!editing) setDraft(value);
  }, [value, editing]);

  useEffect(() => {
    if (editing) inputRef.current?.focus();
  }, [editing]);

  const enterEdit = () => {
    if (readOnly) return;
    setDraft(value);
    setEditing(true);
  };

  const commit = async () => {
    if (saving) return;
    const next = draft.trim();
    if (next === value.trim()) {
      setEditing(false);
      return;
    }
    setSaving(true);
    const ok = await onSave(next);
    setSaving(false);
    if (ok) setEditing(false);
  };

  const cancel = () => {
    setDraft(value);
    setEditing(false);
  };

  return (
    <div className="group">
      <div className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 mb-1">
        {label}
      </div>
      {editing ? (
        <div className="flex items-start gap-1.5">
          {variant === 'textarea' ? (
            <textarea
              ref={inputRef as React.RefObject<HTMLTextAreaElement>}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') { e.preventDefault(); cancel(); }
                if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') { e.preventDefault(); commit(); }
              }}
              onBlur={commit}
              rows={3}
              placeholder={placeholder}
              className="flex-1 resize-none rounded-lg border border-indigo-300 bg-white px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-400/40"
            />
          ) : (
            <input
              ref={inputRef as React.RefObject<HTMLInputElement>}
              type={type}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') { e.preventDefault(); cancel(); }
                if (e.key === 'Enter') { e.preventDefault(); commit(); }
              }}
              onBlur={commit}
              placeholder={placeholder}
              className="flex-1 rounded-lg border border-indigo-300 bg-white px-3 py-1.5 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-400/40"
            />
          )}
          <button
            type="button"
            onMouseDown={(e) => e.preventDefault()}
            onClick={commit}
            disabled={saving}
            className="flex h-8 w-8 items-center justify-center rounded-md text-emerald-600 hover:bg-emerald-50 disabled:opacity-50"
            aria-label="Save"
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
          </button>
          <button
            type="button"
            onMouseDown={(e) => e.preventDefault()}
            onClick={cancel}
            disabled={saving}
            className="flex h-8 w-8 items-center justify-center rounded-md text-slate-400 hover:bg-slate-100"
            aria-label="Cancel"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      ) : (
        <button
          type="button"
          onClick={enterEdit}
          disabled={readOnly}
          className={cn(
            'group/row -mx-2 flex w-full items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors',
            readOnly
              ? 'cursor-default text-slate-700'
              : 'cursor-text hover:bg-slate-50 focus:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400/40',
          )}
        >
          <span className="flex min-w-0 items-center gap-2">
            {icon && (
              <span className="shrink-0 text-slate-400 transition-colors group-hover/row:text-indigo-500">
                {icon}
              </span>
            )}
            <span className={cn(
              'min-w-0 truncate transition-colors',
              !value && 'italic text-slate-400',
              value && 'group-hover/row:text-slate-900',
            )}>
              {renderValue ? renderValue(value) : (value || emptyHint)}
            </span>
          </span>
          {!readOnly && (
            <Pencil className="h-3.5 w-3.5 shrink-0 text-slate-300 opacity-0 transition-all duration-200 group-hover/row:opacity-100 group-hover/row:text-slate-500" />
          )}
        </button>
      )}
    </div>
  );
}
