// Which language the app is in, for everyone below it, and the switch that changes it
// without a reload.
//
// The choice is kept in two places. On the account (GET/PUT /api/settings/language), so
// it follows the person to every browser they sign in from. And in this browser, so the
// sign-in page and the public pages (an application form, an unsubscribe link) open in
// it, and the app opens in it at once instead of flashing the workspace's language
// while the account's answer is on its way.

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { AUTH_SESSION_CHANGED, getAuthToken } from '@/app/lib/auth';
import { getMyLanguage, saveMyLanguage } from '@/app/api/language';
import { catalogueIfLoaded, loadCatalogue, messageFor, type MessageKey } from './catalog';
import { formatMessage, type MessageParams } from './format';
import {
  asLanguage, dateLocaleFor, DEFAULT_LANGUAGE, resolveLanguage, type Language, type LanguageSource,
} from './languages';
import { renderRich, type RichParams } from './rich';
import { setCurrentLanguage } from './current';

const DEVICE_KEY = 'cadence.language';

function readDeviceChoice(): Language | null {
  try {
    return asLanguage(localStorage.getItem(DEVICE_KEY));
  } catch {
    return null;
  }
}

function writeDeviceChoice(language: Language | null): void {
  try {
    if (language) localStorage.setItem(DEVICE_KEY, language);
    else localStorage.removeItem(DEVICE_KEY);
  } catch {
    // Private mode or a full quota: the choice still applies until the tab closes.
  }
}

export interface I18nValue {
  /** The language the page is in. */
  language: Language;
  /** Where it came from: the person's choice, the page, their workspace, their browser, or the default. */
  source: LanguageSource;
  /** The locale numbers and money are written in: the workspace's format, or the language's without one. */
  numberLocale: string;
  /** The locale dates are written in: the language with the workspace's region ("fr-CH"). */
  dateLocale: string;
  /** The words for a key, with its placeholders filled. */
  t: (key: MessageKey, params?: MessageParams) => string;
  /** The same, with React elements for placeholders and tags ("<link>…</link>"). */
  tx: (key: MessageKey, params?: RichParams) => ReactNode;
  /**
   * The person picks a language (null: follow the workspace and the browser). Applied at
   * once, kept in this browser, and on their account when they are signed in.
   */
  choose: (language: Language | null) => Promise<void>;
  /** What a public page knows about its reader's language (see {@link usePageLanguage}). */
  setPageLanguage: (language: string | null) => void;
  /**
   * The app picked a language other than English for someone signed in who never chose one
   * (their workspace's, or their browser's): the app shell says so once and offers the others.
   */
  suggestChoice: boolean;
}

function makeValue(
  language: Language,
  source: LanguageSource,
  workspaceLocale: string | null | undefined,
  choose: I18nValue['choose'],
  setPageLanguage: I18nValue['setPageLanguage'],
  suggestChoice = false,
): I18nValue {
  const numberLocale = workspaceLocale || language;
  const dateLocale = dateLocaleFor(language, workspaceLocale);
  const t = (key: MessageKey, params?: MessageParams) => formatMessage(language, messageFor(language, key), params, numberLocale);
  const tx = (key: MessageKey, params?: RichParams) => renderRich(language, messageFor(language, key), params, numberLocale);
  return { language, source, numberLocale, dateLocale, t, tx, choose, setPageLanguage, suggestChoice };
}

// Outside a provider (a component rendered on its own, as most tests do) the app is in
// English, as it always was.
const noop = async () => {};
const ENGLISH = makeValue(DEFAULT_LANGUAGE, 'default', null, noop, () => {});

const I18nContext = createContext<I18nValue>(ENGLISH);

export { translate, currentDateLocale } from './current';

export function I18nProvider({ children, initialLanguage }: { children: ReactNode; initialLanguage?: Language }) {
  const workspaceLocale = useOrgOptional()?.currentOrg?.locale ?? null;
  const [deviceChoice, setDeviceChoice] = useState<Language | null>(() => initialLanguage ?? readDeviceChoice());
  // undefined: not known yet (signed out, or still asking); null: the account has no choice.
  const [accountChoice, setAccountChoice] = useState<Language | null | undefined>(undefined);
  const [pageLanguage, setPageLanguage] = useState<string | null>(null);

  // The account's choice, when someone is signed in, and again whenever the session
  // changes (a sign-in, a sign-out, another account in another tab).
  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();
    const ask = () => {
      if (!getAuthToken()) {
        setAccountChoice(undefined);
        return;
      }
      getMyLanguage({ signal: controller.signal })
        .then((chosen) => {
          if (cancelled) return;
          setAccountChoice(chosen);
          // Kept here too, so this browser opens in it next time without waiting.
          if (chosen) {
            writeDeviceChoice(chosen);
            setDeviceChoice(chosen);
          }
        })
        .catch(() => {
          // An old server without the endpoint, or a moment offline: keep what this browser has.
        });
    };
    ask();
    window.addEventListener(AUTH_SESSION_CHANGED, ask);
    return () => {
      cancelled = true;
      controller.abort();
      window.removeEventListener(AUTH_SESSION_CHANGED, ask);
    };
  }, []);

  const { language, source } = resolveLanguage({
    chosen: accountChoice ?? deviceChoice,
    page: pageLanguage,
    workspaceLocale,
  });

  // German and French come down the first time they are needed. Until then the page
  // stays in the language it was in.
  const [ready, setReady] = useState<Language>(() => (catalogueIfLoaded(language) ? language : DEFAULT_LANGUAGE));
  useEffect(() => {
    if (catalogueIfLoaded(language)) {
      setReady(language);
      return;
    }
    let cancelled = false;
    loadCatalogue(language).then(() => {
      if (!cancelled) setReady(language);
    }, () => {
      // Offline: carry on in the language already shown.
    });
    return () => {
      cancelled = true;
    };
  }, [language]);
  const shown = catalogueIfLoaded(language) ? language : ready;

  const choose = useCallback(async (next: Language | null) => {
    writeDeviceChoice(next);
    setDeviceChoice(next);
    if (next) await loadCatalogue(next).catch(() => undefined);
    if (!getAuthToken()) return;
    setAccountChoice(next);
    try {
      await saveMyLanguage(next);
    } catch {
      // Not kept on the account this time; it still applies in this browser.
    }
  }, []);

  // Signed in, the account has no choice, and the app went with the workspace's or the
  // browser's language rather than English: worth saying, once.
  const suggestChoice = accountChoice === null && (source === 'workspace' || source === 'browser') && language !== DEFAULT_LANGUAGE;
  const value = useMemo(
    () => makeValue(shown, shown === language ? source : 'default', workspaceLocale, choose, setPageLanguage, suggestChoice),
    [shown, language, source, workspaceLocale, choose, suggestChoice],
  );
  // Code outside components (toasts from API helpers, date helpers) follows the same language.
  setCurrentLanguage({ language: value.language, numberLocale: value.numberLocale, dateLocale: value.dateLocale });

  // The page says what language it is in: screen readers pronounce it, and the browser
  // hyphenates and offers translation by it.
  useEffect(() => {
    try {
      document.documentElement.lang = value.language;
    } catch {
      // No document (not a browser).
    }
  }, [value.language]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

/** The app's language and the words in it. */
export function useT(): I18nValue {
  return useContext(I18nContext);
}

/**
 * What a public page knows about its reader's language, used when they have not chosen
 * one in this browser: a member's own language on their pay or unsubscribe link, the
 * club's on its application form.
 */
export function usePageLanguage(language: string | null | undefined): void {
  const { setPageLanguage } = useT();
  useEffect(() => {
    setPageLanguage(language ?? null);
    return () => setPageLanguage(null);
  }, [language, setPageLanguage]);
}
