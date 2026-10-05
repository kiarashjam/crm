import { useEffect } from 'react';

/** Short brand, same role as the trailing "Hattrick" in a Hattrick tab. */
export const APP_TITLE = 'Cadence';

/** `{name} » {section} » Cadence`, matching Hattrick's `{player} » Players » …`. */
export function recordTabTitle(name?: string | null, section?: string | null): string {
  const who = name?.trim();
  const area = section?.trim();
  if (who && area) return `${who} » ${area} » ${APP_TITLE}`;
  if (who) return `${who} » ${APP_TITLE}`;
  if (area) return `${area} » ${APP_TITLE}`;
  return APP_TITLE;
}

export function recordHref(path: string, name: string | undefined, section: string): string {
  const params = new URLSearchParams();
  const who = name?.trim();
  if (who) params.set('name', who);
  params.set('section', section);
  return `${path}?${params.toString()}`;
}

function hintedParam(key: string): string {
  if (typeof window === 'undefined') return '';
  return new URLSearchParams(window.location.search).get(key)?.trim() ?? '';
}

/**
 * While a record is open, the browser tab leads with that record's name.
 * A `?name=` hint (and optional `?section=`) applies before the record finishes loading.
 * Leaving the page restores the short app title.
 */
export function useDocumentTitle(name?: string | null, section?: string) {
  const who = name?.trim() || hintedParam('name');
  const area = section?.trim() || hintedParam('section');
  const title = recordTabTitle(who, area);

  if (typeof document !== 'undefined' && (who || area)) {
    document.title = title;
  }

  useEffect(() => {
    if (!who && !area) return;
    document.title = title;
    return () => {
      document.title = APP_TITLE;
    };
  }, [title, who, area]);
}
