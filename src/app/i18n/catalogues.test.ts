// The catalogues cannot drift: every English key is in German and French, with the same
// placeholders, the same tags and the same plural shape, and nothing else is there.
//
// A key missing from a language would show English in the middle of a French page (the
// layer falls back to English rather than show a key), so this is where it is caught.

import { describe, expect, it } from 'vitest';
import { sections as enSections } from './locales/en';
import { sections as deSections } from './locales/de';
import { sections as frSections } from './locales/fr';
import { placeholdersOf, tagsOf, type Message } from './format';

type Sections = Record<string, Record<string, Message>>;

const others: [string, Sections][] = [
  ['de', deSections as unknown as Sections],
  ['fr', frSections as unknown as Sections],
];
const english = enSections as unknown as Sections;

const isPlural = (m: Message) => typeof m !== 'string';

function texts(m: Message): string[] {
  return typeof m === 'string' ? [m] : Object.values(m).filter((t): t is string => typeof t === 'string');
}

describe('message catalogues', () => {
  it('have the same sections in every language', () => {
    for (const [, sections] of others) expect(Object.keys(sections).sort()).toEqual(Object.keys(english).sort());
  });

  for (const [language, sections] of others) {
    describe(language, () => {
      it('has every English key, and no key English lacks', () => {
        for (const [name, en] of Object.entries(english)) {
          const theirs = sections[name] ?? {};
          const missing = Object.keys(en).filter((k) => !(k in theirs));
          const extra = Object.keys(theirs).filter((k) => !(k in en));
          expect({ section: name, missing }).toEqual({ section: name, missing: [] });
          expect({ section: name, extra }).toEqual({ section: name, extra: [] });
        }
      });

      it('keeps every placeholder and tag of the English message', () => {
        for (const [name, en] of Object.entries(english)) {
          for (const [key, message] of Object.entries(en)) {
            const theirs = sections[name]?.[key];
            if (theirs === undefined) continue;
            expect({ key, placeholders: placeholdersOf(theirs) }).toEqual({ key, placeholders: placeholdersOf(message) });
            expect({ key, tags: tagsOf(theirs) }).toEqual({ key, tags: tagsOf(message) });
          }
        }
      });

      it('gives a plural message every form the language needs', () => {
        for (const [name, en] of Object.entries(english)) {
          for (const [key, message] of Object.entries(en)) {
            const theirs = sections[name]?.[key];
            if (theirs === undefined) continue;
            expect({ key, plural: isPlural(theirs) }).toEqual({ key, plural: isPlural(message) });
            if (typeof theirs !== 'string') {
              // "many" (French millions) falls back to "other"; the rest must be written.
              const needed = new Intl.PluralRules(language).resolvedOptions().pluralCategories.filter((c) => c !== 'many');
              for (const category of needed) expect({ key, category, has: category in theirs }).toEqual({ key, category, has: true });
            }
          }
        }
      });

      it('has no empty message', () => {
        for (const section of Object.values(sections))
          for (const [key, message] of Object.entries(section))
            for (const text of texts(message)) expect({ key, empty: text.trim() === '' }).toEqual({ key, empty: false });
      });
    });
  }

  it('writes German the Swiss way, with ss and never ß', () => {
    for (const section of Object.values(deSections as unknown as Sections))
      for (const [key, message] of Object.entries(section))
        for (const text of texts(message)) expect({ key, sharpS: text.includes('ß') }).toEqual({ key, sharpS: false });
  });

  it('puts a no-break space, never a plain one, before French : ? ! and ;', () => {
    for (const section of Object.values(frSections as unknown as Sections))
      for (const [key, message] of Object.entries(section))
        for (const text of texts(message)) expect({ key, plainSpace: / [:?!;]/.test(text) }).toEqual({ key, plainSpace: false });
  });

  it('gives every English plural message the forms English needs', () => {
    for (const section of Object.values(english))
      for (const [key, message] of Object.entries(section))
        if (typeof message !== 'string') expect({ key, one: 'one' in message, other: 'other' in message }).toEqual({ key, one: true, other: true });
  });
});
