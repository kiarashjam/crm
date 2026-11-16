// Dates and numbers written the way the reader expects: month and day names in their
// language, day before month as the workspace's region writes it (12.11.2026 in
// Switzerland), numbers in the workspace's own format.
//
// Pages used toLocaleDateString(undefined, …), which is the browser's language and
// region: a Swiss club's front desk on an English-language laptop read "11/12/2026"
// for the 11th of December, and the next colleague read "12.11.2026" for the same day.

import { useMemo } from 'react';
import { useT } from './I18nProvider';

type DateInput = Date | string | number | null | undefined;

function toDate(value: DateInput): Date | null {
  if (value === null || value === undefined || value === '') return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

function safeFormat(locale: string, options: Intl.DateTimeFormatOptions, d: Date): string {
  try {
    return new Intl.DateTimeFormat(locale, options).format(d);
  } catch {
    return new Intl.DateTimeFormat(undefined, options).format(d);
  }
}

export interface Formats {
  /** The locale dates are written in ("fr-CH"). */
  dateLocale: string;
  /** The locale numbers are written in (the workspace's, "de-CH"). */
  numberLocale: string;
  /** A day: "12 nov. 2026" (medium), "12.11.26" (short), "jeudi 12 novembre 2026" (full). Empty for no date. */
  date: (value: DateInput, style?: 'short' | 'medium' | 'long' | 'full') => string;
  /** A day and a time. */
  dateTime: (value: DateInput, style?: 'short' | 'medium') => string;
  /** A time of day, 24-hour where the region writes it so. */
  time: (value: DateInput) => string;
  /** Any other date format, in the reader's locale. */
  custom: (value: DateInput, options: Intl.DateTimeFormatOptions) => string;
  /** A number in the workspace's format. */
  number: (value: number, options?: Intl.NumberFormatOptions) => string;
}

export function makeFormats(dateLocale: string, numberLocale: string): Formats {
  return {
    dateLocale,
    numberLocale,
    date: (value, style = 'medium') => {
      const d = toDate(value);
      return d ? safeFormat(dateLocale, { dateStyle: style }, d) : '';
    },
    dateTime: (value, style = 'medium') => {
      const d = toDate(value);
      return d ? safeFormat(dateLocale, { dateStyle: style, timeStyle: 'short' }, d) : '';
    },
    time: (value) => {
      const d = toDate(value);
      return d ? safeFormat(dateLocale, { timeStyle: 'short' }, d) : '';
    },
    custom: (value, options) => {
      const d = toDate(value);
      return d ? safeFormat(dateLocale, options, d) : '';
    },
    number: (value, options) => {
      try {
        return new Intl.NumberFormat(numberLocale, options).format(value);
      } catch {
        return String(value);
      }
    },
  };
}

/** Formatters in the reader's language and the workspace's formats. */
export function useFormat(): Formats {
  const { dateLocale, numberLocale } = useT();
  return useMemo(() => makeFormats(dateLocale, numberLocale), [dateLocale, numberLocale]);
}
