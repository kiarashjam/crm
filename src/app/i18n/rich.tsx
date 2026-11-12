// Messages with React inside them: a link in the middle of a sentence, a name in bold.
//
// Word order differs between languages ("see the <link>privacy notice</link>" puts the
// link last in English and first in German), so a sentence cannot be glued together
// from pieces in code. The catalogue keeps the whole sentence, marks the words a tag
// wraps, and the page says what each tag and placeholder becomes.

import { Fragment, type ReactNode } from 'react';
import type { Language } from './languages';
import { pickForm, type Message } from './format';

/** Placeholder values (text, numbers or elements) and, for each tag, what it wraps its words in. */
export type RichParams = Record<string, ReactNode | ((words: ReactNode) => ReactNode)>;

const TOKEN = /<(\w+)>(.*?)<\/\1>|\{(\w+)\}/gs;

function formatNumber(value: number, locale: string): string {
  try {
    return new Intl.NumberFormat(locale).format(value);
  } catch {
    return String(value);
  }
}

function fill(text: string, params: RichParams, numberLocale: string, keyBase: string): ReactNode[] {
  const out: ReactNode[] = [];
  let last = 0;
  let i = 0;
  for (const match of text.matchAll(TOKEN)) {
    const at = match.index ?? 0;
    if (at > last) out.push(text.slice(last, at));
    const [whole, tag, inner, name] = match;
    const key = `${keyBase}-${i++}`;
    if (tag !== undefined) {
      const wrap = params[tag];
      const words = fill(inner ?? '', params, numberLocale, key);
      out.push(
        <Fragment key={key}>{typeof wrap === 'function' ? wrap(<>{words}</>) : words}</Fragment>,
      );
    } else if (name !== undefined) {
      const value = params[name];
      if (value === undefined || value === null || typeof value === 'function') out.push(whole);
      else if (typeof value === 'number') out.push(formatNumber(value, numberLocale));
      else out.push(<Fragment key={key}>{value}</Fragment>);
    }
    last = at + whole.length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}

/** A message as React nodes, its tags and placeholders replaced by what `params` gives. */
export function renderRich(language: Language, message: Message, params: RichParams = {}, numberLocale: string = language): ReactNode {
  const count = typeof params.count === 'number' ? params.count : 0;
  const text = typeof message === 'string' ? message : pickForm(language, message, count);
  return <>{fill(text, params, numberLocale, 'm')}</>;
}
