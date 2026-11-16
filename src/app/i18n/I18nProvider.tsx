// Which language the app is in, for everyone below it, and the switch that changes it
// without a reload.
//
// The choice is kept in two places. On the account (GET/PUT /api/settings/language), so
// it follows the person to every browser they sign in from. And in this browser, so the
// sign-in page and the public pages (an application form, an unsubscribe link) open in
// it, and the app opens in it at once instead of flashing the workspace's language
// while the account's answer is on its way.
//
// The browser's copy says whose account it came from. A club's front desk shares one
// browser: the copy of one person's language is theirs, and is not the language of the
// next person to sign in there, who reads their own choice or, without one, their
// workspace's.

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { AUTH_SESSION_CHANGED, getAuthToken, getCurrentUser } from '@/app/lib/auth';
import { getMyLanguage, saveMyLanguage } from '@/app/api/language';
import { catalogueIfLoaded, loadCatalogue, messageFor, type MessageKey } from './catalog';
import { formatMessage, type MessageParams } from './format';
import {
  asLanguage, dateLocaleFor, DEFAULT_LANGUAGE, resolveLanguage, type Language, type LanguageSource,
} from './languages';
import { renderRich, type RichParams } from './rich';
import { setCurrentLanguage } from './current';

const DEVICE_KEY = 'cadence.language';
/** The account the language kept in this browser was copied from; none when it was picked signed out. */
const DEVICE_ACCOUNT_KEY = 'cadence.language.account';

/**
 * What a public page knows about the language it is read in: "reader" when it is the
 * visitor's own (a member's language on their pay link, the ?lang= on a form's link), which
 * comes before their browser; "club" when it is only the club's (from its locale), which
 * comes after it.
 */
export type PageLanguageKind = 'reader' | 'club';

/** The language kept in this browser, and whose account it is a copy of (null: picked signed out). */
interface DeviceChoice {
  language: Language | null;
  account: string | null;
}

function readDeviceChoice(): DeviceChoice {
  try {
    const language = asLanguage(localStorage.getItem(DEVICE_KEY));
    return { language, account: language ? localStorage.getItem(DEVICE_ACCOUNT_KEY) : null };
  } catch {
    return { language: null, account: null };
  }
}

function writeDeviceChoice(language: Language | null, account: string | null): void {
  try {
    if (language) localStorage.setItem(DEVICE_KEY, language);
    else localStorage.removeItem(DEVICE_KEY);
    if (language && account) localStorage.setItem(DEVICE_ACCOUNT_KEY, account);
    else localStorage.removeItem(DEVICE_ACCOUNT_KEY);
  } catch {
    // Private mode or a full quota: the choice still applies until the tab closes.
  }
}

/** Who is signed in, when someone is: whose account a choice made now belongs to. */
function signedInAccount(): string | null {
  return getAuthToken() ? getCurrentUser()?.id ?? null : null;
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
  /** What a public page knows about its reader's language, or its club's (see {@link usePageLanguage}). */
  setPageLanguage: (language: string | null, kind?: PageLanguageKind) => void;
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
  const [deviceChoice, setDeviceChoice] = useState<DeviceChoice>(
    () => (initialLanguage ? { language: initialLanguage, account: null } : readDeviceChoice()),
  );
  // undefined: not known yet (signed out, or still asking); null: the account has no choice.
  const [accountChoice, setAccountChoice] = useState<Language | null | undefined>(undefined);
  // Whose session this is, so the browser's copy of someone else's language is not taken for theirs.
  const [account, setAccount] = useState<string | null>(signedInAccount);
  const [pageLanguage, setPageLanguageState] = useState<Record<PageLanguageKind, string | null>>({ reader: null, club: null });
  const setPageLanguage = useCallback((language: string | null, kind: PageLanguageKind = 'reader') => {
    setPageLanguageState((was) => (was[kind] === language ? was : { ...was, [kind]: language }));
  }, []);
  // Counts the person's choices, so an answer from the account that was asked for before
  // the latest one can be told apart and dropped.
  const choices = useRef(0);

  // The account's choice, when someone is signed in, and again whenever the session
  // changes (a sign-in, a sign-out, another account in another tab).
  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();
    const ask = () => {
      const asking = signedInAccount();
      setAccount(asking);
      // Another person's answer, still on its way, is not this one's.
      setAccountChoice(undefined);
      if (!getAuthToken()) return;
      const asked = choices.current;
      getMyLanguage({ signal: controller.signal })
        .then((chosen) => {
          // A language picked while the account was being asked (on the sign-in page, as
          // the session began) is newer than this answer and is being saved over it; and an
          // answer for someone who has signed out since is nobody's now.
          if (cancelled || asked !== choices.current || asking !== signedInAccount()) return;
          setAccountChoice(chosen);
          // Kept here too, so this browser opens in it next time without waiting. A copy of
          // this person's earlier choice goes when their account no longer has one.
          const kept = readDeviceChoice();
          if (chosen) {
            writeDeviceChoice(chosen, asking);
            setDeviceChoice({ language: chosen, account: asking });
          } else if (kept.account !== null && kept.account === asking) {
            writeDeviceChoice(null, null);
            setDeviceChoice({ language: null, account: null });
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

  // The browser's copy stands in for the person while their account is being asked, and for
  // nobody in particular while no one is signed in. A copy of someone else's account is that
  // person's language, not this one's; one picked here signed out is whoever is here now.
  const deviceLanguage = !account || deviceChoice.account === null || deviceChoice.account === account
    ? deviceChoice.language
    : null;
  const chosen = accountChoice === undefined
    ? deviceLanguage
    : accountChoice ?? (deviceChoice.account === null ? deviceChoice.language : null);
  const { language, source } = resolveLanguage({
    chosen,
    page: pageLanguage.reader,
    workspaceLocale,
    club: pageLanguage.club,
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
    choices.current += 1;
    const owner = signedInAccount();
    writeDeviceChoice(next, owner);
    setDeviceChoice({ language: next, account: owner });
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
    [shown, language, source, workspaceLocale, choose, setPageLanguage, suggestChoice],
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
 * What a public page knows about the language it is read in, used when the visitor has not
 * chosen one in this browser. "reader" (the default) is the visitor's own: a member's
 * language on their pay or unsubscribe link, the ?lang= a club's website put on its form's
 * link; it comes before the browser. "club" is only the club's, from its locale; it comes
 * after the browser, for a visitor whose browser speaks none of the app's languages.
 */
export function usePageLanguage(language: string | null | undefined, kind: PageLanguageKind = 'reader'): void {
  const { setPageLanguage } = useT();
  useEffect(() => {
    setPageLanguage(language ?? null, kind);
    return () => setPageLanguage(null, kind);
  }, [language, kind, setPageLanguage]);
}
