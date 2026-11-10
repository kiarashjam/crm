import { useEffect, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { getPayPage, PayLinkError, startCheckout, type PublicPayPage } from '@/app/api/payments';
import { METHOD_LABEL } from '@/app/components/payments/paymentLabels';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { leaveFor } from '@/app/lib/leavePage';

/**
 * Where a club's pay link leads: a member, with no account here, pays what they owe (or
 * saves a card) on Stripe's own page.
 *
 * Opening the link changes nothing: it says whose club, for what and how much, worked out
 * now, so a payment made since the link was sent is not asked for again. The button asks
 * the server for Stripe's page and goes there. Coming back, the page thanks them and says
 * the payment shows once Stripe confirms it; it does not offer to pay again meanwhile.
 */
type PageState = 'loading' | 'ready' | 'missing' | 'unreachable';

function methodsText(methods: string[]): string {
  const names = methods.map((m) => METHOD_LABEL[m] ?? m);
  if (names.length <= 1) return names[0] ?? 'card';
  return `${names.slice(0, -1).join(', ')} or ${names[names.length - 1]}`;
}

export default function PayPage() {
  const { token = '' } = useParams();
  const [search] = useSearchParams();
  const returned = search.get('status');
  const [state, setState] = useState<PageState>('loading');
  const [page, setPage] = useState<PublicPayPage | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let cancelled = false;
    setState('loading');
    getPayPage(token)
      .then((found) => {
        if (cancelled) return;
        setPage(found);
        setState('ready');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // Only a 404 means the link is not valid (any more). Anything else is worth retrying.
        setState(err instanceof PayLinkError && err.status === 404 ? 'missing' : 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt]);

  useEffect(() => {
    if (state === 'ready' && returned === 'done') headingRef.current?.focus();
  }, [state, returned]);

  const go = async () => {
    setBusy(true);
    setError(null);
    try {
      const { url } = await startCheckout(token);
      leaveFor(url);
    } catch (err) {
      const status = err instanceof PayLinkError ? err.status : 0;
      setError(
        status === 429 ? 'Too many attempts. Please try again in a minute.'
          : err instanceof PayLinkError && status !== 0 && err.message !== 'Not available' ? err.message
          : 'That did not go through. Please check your connection and try again.',
      );
      setBusy(false);
    }
  };

  const club = page?.clubName?.trim() || 'The club';
  const setup = page?.purpose === 'setup';
  const amount = page ? formatClubMoney(page.amount, page.currency || 'CHF') : '';

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">This payment link is not valid</h1>
            <p className="mt-2 text-sm text-slate-600">
              It may have expired, or been copied incompletely. Please ask the club for a new one.
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
        ) : state === 'loading' || !page ? (
          <div aria-busy="true">
            <h1 className="text-xl font-semibold text-slate-900">Loading…</h1>
          </div>
        ) : returned === 'done' ? (
          <>
            <h1 ref={headingRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              {setup ? 'Your card is saved' : 'Thank you'}
            </h1>
            <p className="mt-2 text-sm text-slate-700">
              {setup
                ? `${club} can now charge your card${page.autoPay ? ' for your membership dues when they are billed' : ' when you ask'}. Stripe keeps the card's details; the club sees only its last digits.`
                : `Your payment to ${club} went through Stripe. It shows on your account as soon as Stripe confirms it, usually within a minute; a direct debit takes a few days.`}
            </p>
          </>
        ) : (
          <div aria-busy={busy}>
            <p className="text-sm font-medium text-slate-500">{club}</p>
            <h1 className="mt-1 text-xl font-semibold text-slate-900">
              {setup ? `Save a card, ${page.memberFirstName}` : `Hello ${page.memberFirstName}`}
            </h1>
            {returned === 'cancelled' && (
              <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
                {setup ? 'No card was saved.' : 'The payment was not made.'} You can try again below.
              </p>
            )}
            <p className="mt-3 text-sm text-slate-700">{page.description}</p>
            {!setup && page.available && <p className="mt-4 text-3xl font-bold text-slate-900">{amount}</p>}
            {page.available ? (
              <>
                <p className="mt-2 text-sm text-slate-500">
                  {setup
                    ? `On Stripe's secure page, with a ${methodsText(page.methods)}.`
                    : `Pay by ${methodsText(page.methods)} on Stripe's secure page.`}
                </p>
                {setup && page.autoPay && (
                  <p className="mt-2 text-sm text-slate-700">
                    By saving it you agree that {club} charges your membership dues to it when they are billed. You can ask
                    the club to stop at any time.
                  </p>
                )}
                <p role="alert" className="mt-3 min-h-0 text-sm text-red-600">{error}</p>
                <button
                  type="button"
                  onClick={() => void go()}
                  disabled={busy}
                  className="mt-4 h-11 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
                >
                  {busy ? 'Opening Stripe…' : setup ? 'Save a card' : `Pay ${amount}`}
                </button>
              </>
            ) : (
              <p className="mt-4 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">{page.reason}</p>
            )}
          </div>
        )}
      </div>
    </main>
  );
}
