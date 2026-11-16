import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { confirmUnsubscribe, getUnsubscribe, UnsubscribeError, type UnsubscribeTarget } from '@/app/api/unsubscribe';
import { PublicPage } from '@/app/components/PublicPage';
import { usePageLanguage, useT } from '@/app/i18n';

/**
 * Where the unsubscribe link at the foot of a workspace's email leads.
 *
 * Seen by someone who is not a user of the CRM, so it names the workspace that
 * wrote to them and nothing else. Opening the page changes nothing; the button
 * does. Mail scanners open every link in a message, and a page that unsubscribed
 * on load would unsubscribe everyone whose company runs one.
 *
 * A club member's link opens in the language the club writes to them in; anyone else's
 * follows their browser. The picker above the card switches it.
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
  const { t, tx } = useT();
  usePageLanguage(target?.language);

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
        status === 429 ? t('unsubscribe.errorTooMany')
          : status === 404 ? t('unsubscribe.errorInvalid')
          : t('unsubscribe.errorNetwork'),
      );
    } finally {
      setBusy(false);
    }
  };

  const from = target?.workspaceName?.trim() || t('unsubscribe.thisSender');
  // A club member's link stops news and offers only: what their membership needs still comes.
  const marketing = target?.scope === 'marketing';
  const strong = (words: ReactNode) => <span className="font-medium">{words}</span>;

  return (
    <PublicPage>
        {state === 'done' ? (
          <>
            <h1 ref={doneRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              {t('unsubscribe.doneTitle')}
            </h1>
            <p className="mt-2 text-sm text-slate-700">
              {t(marketing ? 'unsubscribe.doneMarketing' : 'unsubscribe.doneAll', { from, email: target?.email })}
            </p>
          </>
        ) : state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t('unsubscribe.missingTitle')}</h1>
            <p className="mt-2 text-sm text-slate-600">{t('unsubscribe.missingText')}</p>
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
        ) : (
          <div aria-busy={state === 'loading' || busy}>
            <h1 className="text-xl font-semibold text-slate-900">
              {state === 'loading' ? t('common.loading') : t(marketing ? 'unsubscribe.askMarketing' : 'unsubscribe.askAll', { from })}
            </h1>
            {target && (
              <p className="mt-2 text-sm text-slate-700">
                {tx(marketing ? 'unsubscribe.explainMarketing' : 'unsubscribe.explainAll', { from, email: target.email, b: strong })}
              </p>
            )}
            <p role="alert" className="mt-3 min-h-0 text-sm text-red-600">{error}</p>
            <button
              type="button"
              onClick={unsubscribe}
              disabled={state !== 'ready' || busy}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              {busy ? t('unsubscribe.busy') : t('unsubscribe.button')}
            </button>
          </div>
        )}
    </PublicPage>
  );
}
