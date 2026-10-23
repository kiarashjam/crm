// Whether the next-steps panel and its chat are open, remembered in this browser.
//
// Both can be closed down to an icon and opened again when wanted. The choice holds
// across pages and reloads. Every panel on screen follows a change at once, and so does
// another tab. These keys name nobody, so they are not under the agent's "cadence.agent."
// prefix that signing out clears.

import { useCallback, useEffect, useState } from 'react';

export const PANEL_OPEN_KEY = 'cadence.nextActions.panelOpen';
export const CHAT_OPEN_KEY = 'cadence.nextActions.chatOpen';

const EVENT = 'cadence-next-actions-prefs';

function read(key: string, fallback: boolean): boolean {
  try {
    const stored = localStorage.getItem(key);
    if (stored === 'true') return true;
    if (stored === 'false') return false;
  } catch {
    // Storage blocked: the default stands.
  }
  return fallback;
}

export function writeFlag(key: string, value: boolean): void {
  try {
    localStorage.setItem(key, String(value));
  } catch {
    // A choice that cannot be saved still applies on this page.
  }
  if (typeof window !== 'undefined') window.dispatchEvent(new CustomEvent(EVENT, { detail: { key, value } }));
}

/** A remembered on/off choice that every component reading the same key follows. */
export function useStoredFlag(key: string, fallback: boolean): [boolean, (value: boolean) => void] {
  const [value, setValue] = useState(() => read(key, fallback));
  useEffect(() => {
    const onChange = (event: Event) => {
      const detail = (event as CustomEvent<{ key: string; value: boolean }>).detail;
      if (detail?.key === key) setValue(detail.value);
    };
    const onStorage = (event: StorageEvent) => {
      if (event.key === key) setValue(read(key, fallback));
    };
    window.addEventListener(EVENT, onChange);
    window.addEventListener('storage', onStorage);
    return () => {
      window.removeEventListener(EVENT, onChange);
      window.removeEventListener('storage', onStorage);
    };
  }, [key, fallback]);
  const set = useCallback((next: boolean) => {
    setValue(next);
    writeFlag(key, next);
  }, [key]);
  return [value, set];
}

let focusChatNext = false;

/** The next chat to open should put the cursor in its box: the person asked to type. */
export function requestChatFocus(): void {
  focusChatNext = true;
}

/** True once after requestChatFocus. */
export function takeChatFocusRequest(): boolean {
  const wanted = focusChatNext;
  focusChatNext = false;
  return wanted;
}
