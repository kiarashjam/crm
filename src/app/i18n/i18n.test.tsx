import { describe, expect, it, beforeEach } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { formatMessage, pickForm } from './format';
import { asLanguage, dateLocaleFor, regionOf, resolveLanguage } from './languages';
import { I18nProvider, loadCatalogue, translate, useT, useFormat } from './index';
import { LanguageSwitcher } from './LanguageSwitcher';

describe('formatMessage', () => {
  it('fills named placeholders and leaves an unknown one visible', () => {
    expect(formatMessage('en', 'Apply to {club}', { club: 'Pavillon 46' })).toBe('Apply to Pavillon 46');
    expect(formatMessage('en', 'Hello {name}', {})).toBe('Hello {name}');
  });

  it('picks the plural form by each language’s own rule', () => {
    const forms = { one: '{count} membre', other: '{count} membres' };
    // French puts nothing in the singular, German and English in the plural.
    expect(formatMessage('fr', forms, { count: 0 })).toBe('0 membre');
    expect(formatMessage('fr', forms, { count: 1 })).toBe('1 membre');
    expect(formatMessage('fr', forms, { count: 2 })).toBe('2 membres');
    expect(formatMessage('de', { one: '{count} Mitglied', other: '{count} Mitglieder' }, { count: 0 })).toBe('0 Mitglieder');
    expect(formatMessage('en', { one: '{count} member', other: '{count} members' }, { count: 1 })).toBe('1 member');
  });

  it('uses an exact form for nothing at all before the language rule', () => {
    expect(pickForm('en', { '=0': 'No members yet', one: 'One member', other: 'Many' }, 0)).toBe('No members yet');
  });

  it('falls back to "other" for a form the message does not list', () => {
    // French says "1 000 000 de membres" with its own "many" form.
    expect(pickForm('fr', { one: 'un', other: 'plusieurs' }, 1_000_000)).toBe('plusieurs');
  });

  it('writes a number in the workspace’s format, whatever the language', () => {
    const text = formatMessage('fr', '{count} lignes', { count: 1234 }, 'de-CH');
    // de-CH groups with an apostrophe (typographic or straight, by ICU version).
    expect(text).toMatch(/^1.234 lignes$/);
    expect(text).not.toBe('1234 lignes');
  });
});

describe('choosing the language', () => {
  it('takes the person’s choice, then the page, then the workspace, then the browser, then English', () => {
    expect(resolveLanguage({ chosen: 'fr', page: 'de', workspaceLocale: 'de-CH', browser: 'en' })).toEqual({ language: 'fr', source: 'chosen' });
    expect(resolveLanguage({ chosen: null, page: 'de', workspaceLocale: 'fr-CH', browser: 'en' })).toEqual({ language: 'de', source: 'page' });
    expect(resolveLanguage({ workspaceLocale: 'fr-CH', browser: 'de-DE' })).toEqual({ language: 'fr', source: 'workspace' });
    expect(resolveLanguage({ workspaceLocale: 'it-CH', browser: 'de-DE' })).toEqual({ language: 'de', source: 'browser' });
    expect(resolveLanguage({ workspaceLocale: 'it-CH', browser: 'it-IT' })).toEqual({ language: 'en', source: 'default' });
  });

  it('puts a public page’s club after the visitor’s browser, and before English', () => {
    // The club's language says nothing about the visitor: their browser comes first.
    expect(resolveLanguage({ browser: 'fr-CH', club: 'de' })).toEqual({ language: 'fr', source: 'browser' });
    expect(resolveLanguage({ browser: 'it-CH', club: 'de' })).toEqual({ language: 'de', source: 'club' });
    // What the page knows about its reader (a member's own language) still comes first.
    expect(resolveLanguage({ page: 'fr', browser: 'de-CH', club: 'de' })).toEqual({ language: 'fr', source: 'page' });
    expect(resolveLanguage({ browser: 'it-CH', club: 'it' })).toEqual({ language: 'en', source: 'default' });
  });

  it('reads a language from a locale name and passes over ones it does not have', () => {
    expect(asLanguage('de-CH')).toBe('de');
    expect(asLanguage('FR_ch')).toBe('fr');
    expect(asLanguage('it-CH')).toBeNull();
    expect(regionOf('fr-CH')).toBe('CH');
    expect(regionOf('en')).toBeNull();
  });

  it('writes dates in the person’s language with the workspace’s region', () => {
    expect(dateLocaleFor('fr', 'de-CH')).toBe('fr-CH');
    expect(dateLocaleFor('en', 'de-CH')).toBe('en-CH');
    expect(dateLocaleFor('de', null)).toBe('de');
  });
});

function Probe() {
  const { t, language } = useT();
  const fmt = useFormat();
  return (
    <div>
      <p data-testid="lang">{language}</p>
      <p data-testid="text">{t('unsubscribe.button')}</p>
      <p data-testid="date">{fmt.date('2026-11-12T10:00:00Z', 'short')}</p>
      <LanguageSwitcher />
    </div>
  );
}

describe('I18nProvider', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('is English outside a provider, as every page always was', () => {
    render(<Probe />);
    expect(screen.getByTestId('text')).toHaveTextContent('Unsubscribe');
  });

  it('switches the whole page to another language without a reload, and remembers it in this browser', async () => {
    await loadCatalogue('fr');
    await loadCatalogue('de');
    const user = userEvent.setup();
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByTestId('text')).toHaveTextContent('Unsubscribe');

    await user.selectOptions(screen.getByLabelText('Language'), 'fr');
    expect(screen.getByTestId('text')).toHaveTextContent('Se désinscrire');
    expect(document.documentElement.lang).toBe('fr');
    expect(localStorage.getItem('cadence.language')).toBe('fr');
    // Code outside components follows too.
    expect(translate('unsubscribe.button')).toBe('Se désinscrire');

    await user.selectOptions(screen.getByLabelText('Langue'), 'de');
    expect(screen.getByTestId('text')).toHaveTextContent('Abmelden');
  });

  it('opens in the language this browser last chose', async () => {
    await loadCatalogue('de');
    localStorage.setItem('cadence.language', 'de');
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByTestId('lang')).toHaveTextContent('de');
    expect(screen.getByTestId('text')).toHaveTextContent('Abmelden');
  });

  it('fetches a language the first time it is needed', async () => {
    const user = userEvent.setup();
    render(<I18nProvider><Probe /></I18nProvider>);
    await act(async () => {
      await user.selectOptions(screen.getByLabelText('Language'), 'fr');
    });
    expect(await screen.findByText('Se désinscrire')).toBeInTheDocument();
  });
});
