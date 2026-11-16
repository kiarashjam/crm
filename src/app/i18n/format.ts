// Turning a catalogue message into text: placeholders filled, the plural form picked.
//
// A message is a string with named placeholders ("Apply to {club}"), or, when it says
// how many of something there are, one string per plural form. Which form a number
// takes is the language's own rule (Intl.PluralRules): French says "1 membre" and also
// "0 membre", German and English say "0 Mitglieder", "0 members". A form a language
// does not list falls back to "other"; "=0" is an exact form for nothing at all ("No
// members yet"), checked before the rule.

import type { Language } from './languages';

export type PluralForms = {
  '=0'?: string;
  zero?: string;
  one?: string;
  two?: string;
  few?: string;
  many?: string;
  other: string;
};

export type Message = string | PluralForms;

/** Values for a message's placeholders. `count` also picks the plural form. */
export type MessageParams = Record<string, string | number | null | undefined>;

const pluralRules = new Map<Language, Intl.PluralRules>();

function rulesFor(language: Language): Intl.PluralRules {
  let rules = pluralRules.get(language);
  if (!rules) {
    rules = new Intl.PluralRules(language);
    pluralRules.set(language, rules);
  }
  return rules;
}

/** The text of a message for `count`: its exact "=0" form, else the language's plural form, else "other". */
export function pickForm(language: Language, forms: PluralForms, count: number): string {
  if (count === 0 && forms['=0'] !== undefined) return forms['=0'];
  const category = rulesFor(language).select(count) as keyof PluralForms;
  return forms[category] ?? forms.other;
}

/** Splits text on its placeholders: "Hi {name}!" gives ["Hi ", {name}, "!"]. */
const PLACEHOLDER = /\{(\w+)\}/g;

/**
 * Fills a message. A number is written in `numberLocale` (the workspace's format), so a
 * count of 1234 reads "1’234" in a de-CH workspace. A placeholder with no value is left
 * as it is, which a test and anyone reading the page can see.
 */
export function formatMessage(
  language: Language,
  message: Message,
  params?: MessageParams,
  numberLocale?: string,
): string {
  const count = typeof params?.count === 'number' ? params.count : undefined;
  const text = typeof message === 'string' ? message : pickForm(language, message, count ?? 0);
  if (!params) return text;
  return text.replace(PLACEHOLDER, (whole, name: string) => {
    const value = params[name];
    if (value === undefined || value === null) return whole;
    if (typeof value === 'number') return formatNumber(value, numberLocale ?? language);
    return value;
  });
}

function formatNumber(value: number, locale: string): string {
  try {
    return new Intl.NumberFormat(locale).format(value);
  } catch {
    return String(value);
  }
}

/** The placeholders a message uses, in every form: what a translation has to keep. */
export function placeholdersOf(message: Message): string[] {
  const texts = typeof message === 'string' ? [message] : Object.values(message).filter((t): t is string => typeof t === 'string');
  const names = new Set<string>();
  for (const text of texts) for (const m of text.matchAll(PLACEHOLDER)) if (m[1]) names.add(m[1]);
  return [...names].sort();
}

/** The tags a message wraps words in ("<link>privacy notice</link>"), for rich text. */
export function tagsOf(message: Message): string[] {
  const texts = typeof message === 'string' ? [message] : Object.values(message).filter((t): t is string => typeof t === 'string');
  const names = new Set<string>();
  for (const text of texts) for (const m of text.matchAll(/<(\w+)>/g)) if (m[1]) names.add(m[1]);
  return [...names].sort();
}
