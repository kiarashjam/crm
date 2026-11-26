import { createContext, lazy, Suspense, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import type { EmailComposerDialogProps } from '@/app/components/EmailComposerDialog';

// Fetched the first time someone composes an email, not with every page (the public site included).
const EmailComposerDialog = lazy(() => import('@/app/components/EmailComposerDialog'));

/** What to email, and to whom: everything the composer takes but its opening and closing. */
export type ComposeEmail = Omit<EmailComposerDialogProps, 'open' | 'onOpenChange'>;

const ComposerContext = createContext<((email: ComposeEmail) => void) | null>(null);

/**
 * One email composer for the whole app, so any "Email" button (a lead card, a contact in a
 * list, a deal) opens the same window with the prepared emails, instead of the person's own
 * email app with nothing in it.
 */
export function EmailComposerProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<ComposeEmail | null>(null);
  const [open, setOpen] = useState(false);
  const compose = useCallback((next: ComposeEmail) => {
    setEmail(next);
    setOpen(true);
  }, []);
  const value = useMemo(() => compose, [compose]);
  return (
    <ComposerContext.Provider value={value}>
      {children}
      {email && (
        <Suspense fallback={null}>
          <EmailComposerDialog {...email} open={open} onOpenChange={setOpen} />
        </Suspense>
      )}
    </ComposerContext.Provider>
  );
}

/**
 * Opens the composer. Outside the provider (a test rendering one page), it falls back to the
 * person's own email app, so a button never does nothing.
 */
export function useEmailComposer(): (email: ComposeEmail) => void {
  const compose = useContext(ComposerContext);
  return useCallback((email: ComposeEmail) => {
    if (compose) compose(email);
    else if (typeof window !== 'undefined') window.location.href = `mailto:${encodeURIComponent(email.to)}`;
  }, [compose]);
}
