// A day, typed in the order the workspace's region writes it (31.12.2026 in Switzerland) or
// picked from a calendar.
//
// The browser's own date field shows the browser's order: on an English (US) laptop a Swiss
// club's front desk was offered mm/dd/yyyy, and 03/04 meant the 4th of March to the field
// and the 3rd of April to the person typing it. This field shows and reads the workspace's
// order, still accepts 2026-12-31, and hands the page the same "yyyy-mm-dd" a date input
// did, so nothing that saves the value changes.

import { useId, useMemo, useState, type InputHTMLAttributes } from 'react';
import { CalendarDays } from 'lucide-react';
import { de, enGB, enUS, fr } from 'date-fns/locale';
import { Calendar } from '@/app/components/ui/calendar';
import { Input } from '@/app/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';
import { useFormat, useT } from '@/app/i18n';

type Part = 'day' | 'month' | 'year';

/** The order and separator a locale writes a numeric date in: day, month, year and "." for de-CH. */
export function dateOrder(locale: string): { order: Part[]; separator: string } {
  try {
    const parts = new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', year: 'numeric' }).formatToParts(new Date(2026, 11, 31));
    const order = parts.filter((p) => p.type === 'day' || p.type === 'month' || p.type === 'year').map((p) => p.type as Part);
    const separator = parts.find((p) => p.type === 'literal')?.value.trim() || '.';
    if (order.length === 3) return { order, separator };
  } catch {
    // An unknown locale: the Swiss order below.
  }
  return { order: ['day', 'month', 'year'], separator: '.' };
}

const pad = (n: number) => String(n).padStart(2, '0');

function iso(year: number, month: number, day: number): string | null {
  if (!Number.isInteger(year) || year < 1000 || year > 9999 || month < 1 || month > 12 || day < 1) return null;
  const last = new Date(year, month, 0).getDate();
  return day > last ? null : `${year}-${pad(month)}-${pad(day)}`;
}

/**
 * The day typed, as "yyyy-mm-dd", or null when it is not one (yet). Reads the locale's order
 * ("31.12.2026", "31.12.26", "31/12/2026", "31122026" in Switzerland) and ISO "2026-12-31"
 * in any locale. A two-digit year is this century.
 */
export function parseDay(text: string, locale: string): string | null {
  const t = text.trim();
  if (!t) return null;
  const isoMatch = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t);
  if (isoMatch) return iso(Number(isoMatch[1]), Number(isoMatch[2]), Number(isoMatch[3]));
  const { order } = dateOrder(locale);
  let groups = t.split(/[^\d]+/).filter(Boolean);
  const only = groups.length === 1 ? groups[0] ?? '' : '';
  if (only.length === 8 || only.length === 6) {
    // Digits only, in the locale's order: 31122026 or 311226.
    const digits = only;
    const yearLength = digits.length === 8 ? 4 : 2;
    const sizes = order.map((p) => (p === 'year' ? yearLength : 2));
    let at = 0;
    groups = sizes.map((size) => {
      const piece = digits.slice(at, at + size);
      at += size;
      return piece;
    });
  }
  if (groups.length !== 3) return null;
  const value: Record<Part, number> = { day: 0, month: 0, year: 0 };
  order.forEach((part, i) => {
    value[part] = Number(groups[i]);
  });
  const yearText = groups[order.indexOf('year')] ?? '';
  if (yearText.length === 2) value.year += 2000;
  else if (yearText.length !== 4) return null;
  return iso(value.year, value.month, value.day);
}

/** "yyyy-mm-dd" written in the locale's numeric order: 31.12.2026. */
export function writeDay(value: string, locale: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!m) return '';
  const { order, separator } = dateOrder(locale);
  const parts: Record<Part, string> = { year: m[1] ?? '', month: m[2] ?? '', day: m[3] ?? '' };
  return order.map((p) => parts[p]).join(separator);
}

function calendarLocale(locale: string) {
  const [language, region] = locale.split('-');
  if (language === 'de') return de;
  if (language === 'fr') return fr;
  return region === 'US' || !region ? enUS : enGB;
}

export interface DateInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type' | 'defaultValue'> {
  /** The day, "yyyy-mm-dd", or "" for none. */
  value: string;
  /** Called with the day once the text is one ("yyyy-mm-dd"), or "" when it is cleared. */
  onChange: (value: string) => void;
  /** The locale to read and write in; the workspace's region with the person's language when left out. */
  locale?: string;
}

export function DateInput({ value, onChange, locale, className, disabled, id, ...rest }: DateInputProps) {
  const { t } = useT();
  const format = useFormat();
  const shownLocale = locale ?? format.dateLocale;
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const hintId = `${inputId}-hint`;
  // What is being typed; null shows the value, written the locale's way.
  const [draft, setDraft] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const { order, separator } = useMemo(() => dateOrder(shownLocale), [shownLocale]);
  const placeholder = order.map((p) => t(p === 'day' ? 'date.dd' : p === 'month' ? 'date.mm' : 'date.yyyy')).join(separator);
  const text = draft ?? writeDay(value, shownLocale);
  const invalid = draft !== null && draft.trim() !== '' && parseDay(draft, shownLocale) === null;
  const selected = /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T12:00:00`) : undefined;

  return (
    <div className="relative">
      <Input
        {...rest}
        id={inputId}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        disabled={disabled}
        value={text}
        placeholder={placeholder}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? hintId : rest['aria-describedby']}
        onChange={(e) => {
          const next = e.target.value;
          setDraft(next);
          // As the browser's own date field does: the day once the text is one, and no day
          // while it is not. Keeping the last day read instead left a page holding a day from
          // half a year typed ("31.12.20" on the way to 2027 reads 2020) behind an error.
          onChange(next.trim() === '' ? '' : parseDay(next, shownLocale) ?? '');
        }}
        onBlur={(e) => {
          // A day typed is written back the locale's way; anything else stays to be corrected.
          if (draft === null || draft.trim() === '' || parseDay(draft, shownLocale)) setDraft(null);
          rest.onBlur?.(e);
        }}
        className={cn('pr-10', className)}
      />
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <button
            type="button"
            disabled={disabled}
            aria-label={t('date.pick')}
            className="absolute inset-y-0 right-0 flex w-9 items-center justify-center rounded-r-md text-slate-500 hover:text-slate-800 disabled:opacity-50"
          >
            <CalendarDays className="h-4 w-4" aria-hidden />
          </button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="end">
          <Calendar
            mode="single"
            selected={selected}
            defaultMonth={selected}
            locale={calendarLocale(shownLocale)}
            weekStartsOn={shownLocale.endsWith('-US') ? 0 : 1}
            onSelect={(day) => {
              if (!day) return;
              setDraft(null);
              onChange(`${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}`);
              setOpen(false);
            }}
          />
        </PopoverContent>
      </Popover>
      {invalid && (
        <p id={hintId} className="mt-1 text-xs text-red-600">{t('date.invalid', { pattern: placeholder })}</p>
      )}
    </div>
  );
}
