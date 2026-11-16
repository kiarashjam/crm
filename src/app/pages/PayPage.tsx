import { useEffect, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { getPayPage, PayLinkError, startCheckout, type PublicPayPage } from '@/app/api/payments';
import { PublicPage } from '@/app/components/PublicPage';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { leaveFor } from '@/app/lib/leavePage';
import { usePageLanguage, useT, type I18nValue, type MessageKey } from '@/app/i18n';

/**
 * Where a club's pay link leads: a member, with no account here, pays what they owe (or
 * saves a card) on Stripe's own page.
 *
 * Opening the link changes nothing: it says whose club, for what and how much, worked out
 * now, so a payment made since the link was sent is not asked for again. The button asks
 * the server for Stripe's page and goes there. Coming back, the page thanks them and says
 * the payment shows once Stripe confirms it; it does not offer to pay again meanwhile.
 *
 * It reads in the member's language (the one the club writes to them in), and the amount
 * is written in the club's number format, as on its statements.
 */
type PageState = 'loading' | 'ready' | 'missing' | 'unreachable';

const METHOD_KEYS: Record<string, MessageKey> = {
  card: 'pay.method.card',
  twint: 'pay.method.twint',
  sepa_debit: 'pay.method.sepa_debit',
};

function methodsText(methods: string[], t: I18nValue['t']): string {
  const names = methods.map((m) => (METHOD_KEYS[m] ? t(METHOD_KEYS[m]) : m));
  if (names.length <= 1) return names[0] ?? t('pay.method.card');
  return `${names.slice(0, -1).join(', ')} ${t('common.or')} ${names[names.length - 1]}`;
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
  const { t } = useT();
  usePageLanguage(page?.language);

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
        status === 429 ? t('pay.errorTooMany')
          : err instanceof PayLinkError && status !== 0 && err.message !== 'Not available' ? err.message
          : t('pay.errorNetwork'),
      );
      setBusy(false);
    }
  };

  const club = page?.clubName?.trim() || t('pay.theClub');
  const setup = page?.purpose === 'setup';
  // In the club's number format when the server says it, as its statements write amounts.
  const amount = page ? formatClubMoney(page.amount, page.currency || 'CHF', page.locale ?? undefined) : '';

  return (
    <PublicPage>
        {state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t('pay.missingTitle')}</h1>
            <p className="mt-2 text-sm text-slate-600">{t('pay.missingText')}</p>
          </>
        ) : state === 'unreachable' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t('public.pageUnreachableTitle')}</h1>
            <p className="mt-2 text-sm text-slate-600">{t('common.checkConnection')}</p>
            <button
              type="button"
              onClick={() => setAttempt((n) => n + 1)}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              {t('common.tryAgain')}
            </button>
          </>
        ) : state === 'loading' || !page ? (
          <div aria-busy="true">
            <h1 className="text-xl font-semibold text-slate-900">{t('common.loading')}</h1>
          </div>
        ) : returned === 'done' ? (
          <>
            <h1 ref={headingRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              {setup ? t('pay.cardSavedTitle') : t('public.thanksTitle')}
            </h1>
            <p className="mt-2 text-sm text-slate-700">
              {setup
                ? t(page.autoPay ? 'pay.cardSavedAutoPay' : 'pay.cardSavedOnRequest', { club })
                : t('pay.paidText', { club })}
            </p>
          </>
        ) : (
          <div aria-busy={busy}>
            <p className="text-sm font-medium text-slate-500">{club}</p>
            <h1 className="mt-1 text-xl font-semibold text-slate-900">
              {setup ? t('pay.saveCardHeading', { name: page.memberFirstName }) : t('pay.hello', { name: page.memberFirstName })}
            </h1>
            {returned === 'cancelled' && (
              <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
                {setup ? t('pay.cancelledSetup') : t('pay.cancelledPayment')}
              </p>
            )}
            <p className="mt-3 text-sm text-slate-700">{page.description}</p>
            {!setup && page.available && <p className="mt-4 text-3xl font-bold text-slate-900">{amount}</p>}
            {page.available ? (
              <>
                <p className="mt-2 text-sm text-slate-500">
                  {setup
                    ? t('pay.setupMethods', { methods: methodsText(page.methods, t) })
                    : t('pay.payBy', { methods: methodsText(page.methods, t) })}
                </p>
                {setup && page.autoPay && (
                  <p className="mt-2 text-sm text-slate-700">{t('pay.autoPayAgreement', { club })}</p>
                )}
                <p role="alert" className="mt-3 min-h-0 text-sm text-red-600">{error}</p>
                <button
                  type="button"
                  onClick={() => void go()}
                  disabled={busy}
                  className="mt-4 h-11 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
                >
                  {busy ? t('pay.opening') : setup ? t('pay.saveCard') : t('pay.payAmount', { amount })}
                </button>
              </>
            ) : (
              <p className="mt-4 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">{page.reason}</p>
            )}
          </div>
        )}
    </PublicPage>
  );
}
