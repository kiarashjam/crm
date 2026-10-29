import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { confirmUnsubscribe, getUnsubscribe, UnsubscribeError, type UnsubscribeTarget } from '@/app/api/unsubscribe';

/**
 * Where the unsubscribe link at the foot of a workspace's email leads.
 *
 * Seen by someone who is not a user of the CRM, so it names the workspace that
 * wrote to them and nothing else. Opening the page changes nothing; the button
 * does. Mail scanners open every link in a message, and a page that unsubscribed
 * on load would unsubscribe everyone whose company runs one.
 */
type PageState = 'loading' | 'ready' | 'done' | 'missing' | 'unreachable';

export default function Unsubscribe() {
  const { token = '' } = useParams();
  const [state, setState] = useState<PageState>('loading');
  const [target, setTarget] = useState<UnsubscribeTarget | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const doneRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let cancelled = false;
    setState('loading');
    getUnsubscribe(token)
      .then((found) => {
        if (cancelled) return;
        setTarget(found);
        setState('ready');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // Only a 404 means the link is not one of ours. Anything else is worth retrying.
        setState(err instanceof UnsubscribeError && err.status === 404 ? 'missing' : 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt]);

  // Move focus to the confirmation, so a screen reader announces it.
  useEffect(() => {
    if (state === 'done') doneRef.current?.focus();
  }, [state]);

  const unsubscribe = async () => {
    setBusy(true);
    setError(null);
    try {
      setTarget(await confirmUnsubscribe(token));
      setState('done');
    } catch (err) {
      const status = err instanceof UnsubscribeError ? err.status : 0;
      setError(
        status === 429 ? 'Too many attempts. Please try again in a minute.'
          : status === 404 ? 'This link is no longer valid.'
          : 'That did not go through. Please check your connection and try again.',
      );
    } finally {
      setBusy(false);
    }
  };

  const from = target?.workspaceName?.trim() || 'this sender';

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {state === 'done' ? (
          <>
            <h1 ref={doneRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              You are unsubscribed
            </h1>
            <p className="mt-2 text-sm text-slate-700">
              {from} will not email {target?.email} again.
            </p>
          </>
        ) : state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">This link is not valid</h1>
            <p className="mt-2 text-sm text-slate-600">
              It may have been copied incompletely. Please use the link in the email, or reply to the email and ask to be removed.
            </p>
          </>
        ) : state === 'unreachable' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">This page could not be loaded</h1>
            <p className="mt-2 text-sm text-slate-600">Please check your connection.</p>
            <button
              type="button"
              onClick={() => setAttempt((n) => n + 1)}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              Try again
            </button>
          </>
        ) : (
          <div aria-busy={state === 'loading' || busy}>
            <h1 className="text-xl font-semibold text-slate-900">
              {state === 'loading' ? 'Loading…' : `Stop emails from ${from}?`}
            </h1>
            {target && (
              <p className="mt-2 text-sm text-slate-700">
                {from} will no longer email <span className="font-medium">{target.email}</span>.
              </p>
            )}
            <p role="alert" className="mt-3 min-h-0 text-sm text-red-600">{error}</p>
            <button
              type="button"
              onClick={unsubscribe}
              disabled={state !== 'ready' || busy}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              {busy ? 'Unsubscribing…' : 'Unsubscribe'}
            </button>
          </div>
        )}
      </div>
    </main>
  );
}
