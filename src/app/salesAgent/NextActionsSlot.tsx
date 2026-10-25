import { useLayoutEffect, useState, useSyncExternalStore } from 'react';
import { cn } from '@/app/components/ui/utils';

/**
 * Where the sales agent's panel sits on a page: right after the page's own title
 * block, rather than wedged between the app header and the page.
 *
 * Every page renders `<AppHeader />`, which renders the one panel, and the page
 * puts a `<NextActionsSlot />` after its title. The panel draws itself into the
 * slot through a portal. A page without a slot keeps the panel under the header,
 * so a new page never loses it.
 *
 * The slot registers in a layout effect: the panel moves into it in the same
 * commit, before the browser paints, so it never flashes in the old place.
 *
 * The slot holds the spacing below the panel (`className`, a bottom margin by
 * default), and takes no room at all while it is empty — on a record with
 * nothing owed, say — so a page never shows a gap where the panel would be.
 */

const slots: HTMLElement[] = [];
const listeners = new Set<() => void>();

function current(): HTMLElement | null {
  return slots[slots.length - 1] ?? null;
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function notify(): void {
  for (const listener of listeners) listener();
}

export function NextActionsSlot({ className = 'mb-5' }: { className?: string }) {
  const [element, setElement] = useState<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    if (!element) return undefined;
    slots.push(element);
    notify();
    return () => {
      const at = slots.lastIndexOf(element);
      if (at >= 0) slots.splice(at, 1);
      notify();
    };
  }, [element]);

  return <div ref={setElement} data-next-actions-slot="" className={cn('empty:hidden', className)} />;
}

/** The slot on the current page, or null when the page has none. */
export function useNextActionsSlot(): HTMLElement | null {
  return useSyncExternalStore(subscribe, current, () => null);
}
