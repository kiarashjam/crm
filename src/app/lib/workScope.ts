// Whose work a page shows: the rep's own ("mine") or the whole team's. Each place that
// offers the switch remembers it on its own, in this browser: a manager may want the
// team's calendar and their own tasks. Every place starts on "mine".

import { useCallback, useState } from 'react';

export type WorkScope = 'mine' | 'team';
export type WorkScopePlace = 'tasks' | 'assistant' | 'calendar';

const keyOf = (place: WorkScopePlace) => `cadence.workScope.${place}`;

export function readWorkScope(place: WorkScopePlace): WorkScope {
  try {
    return localStorage.getItem(keyOf(place)) === 'team' ? 'team' : 'mine';
  } catch {
    // Storage blocked: start on the rep's own work.
    return 'mine';
  }
}

export function writeWorkScope(place: WorkScopePlace, scope: WorkScope): void {
  try {
    localStorage.setItem(keyOf(place), scope);
  } catch {
    // A choice that cannot be kept still applies on this page.
  }
}

/** The scope for this place, and a setter that remembers the choice. */
export function useWorkScope(place: WorkScopePlace): [WorkScope, (scope: WorkScope) => void] {
  const [scope, setScope] = useState<WorkScope>(() => readWorkScope(place));
  const choose = useCallback((next: WorkScope) => {
    writeWorkScope(place, next);
    setScope(next);
  }, [place]);
  return [scope, choose];
}
