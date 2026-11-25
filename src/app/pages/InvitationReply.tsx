import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { CalendarDays, MapPin, Ticket } from 'lucide-react';
import {
  answerInvitation, getInvitationPage, InvitationPageError, SAMPLE_TOKEN, type InvitationPage,
} from '@/app/api/eventInvitations';
import { PublicPage } from '@/app/components/PublicPage';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { dateLocaleFor, usePageLanguage, useT, type MessageKey } from '@/app/i18n';

/**
 * Where a member's reply link to an event invitation leads: they answer "I'll come" or "I can't
 * come", and may change their mind until the event starts.
 *
 * Seen by a member with no account here, so it shows the club, the event and their own answer,
 * nothing about anyone else. Opening it changes nothing (mail scanners open every link); the
 * buttons do. "I'll come" registers them by the event's own rules, so the page says plainly
 * whether they have a seat or are on the waitlist.
 *
 * It opens in the member's language (the one the club writes to them in), with the event's time
 * in the club's time zone and the price in the club's number format; the picker switches it.
 */
type PageState = 'loading' | 'ready' | 'sample' | 'missing' | 'started' | 'cancelled' | 'closed' | 'off' | 'unreachable';

/** The refusals that close the page for good: the link's own state, not one answer that did not go through. */
const CLOSING: Record<string, PageState> = {
  'Invitation.Started.Conflict': 'started',
  'Invitation.Cancelled.Conflict': 'cancelled',
  // The event was taken back to a draft: closed for now, which is not "it has started".
  'Invitation.Closed.Conflict': 'closed',
};

/** What a refusal of the link means for the page; null when it closes nothing (an answer that met another change, say). */
function stateOf(err: unknown): PageState | null {
  if (!(err instanceof InvitationPageError)) return 'unreachable';
  if (err.status === 404) return 'missing';
  if (err.status === 403) return 'off';
  if (err.status === 409) return (err.code && CLOSING[err.code]) || null;
  return 'unreachable';
}

const TITLES: Partial<Record<PageState, [MessageKey, MessageKey]>> = {
  sample: ['invitation.sampleTitle', 'invitation.sampleText'],
  missing: ['invitation.missingTitle', 'invitation.missingText'],
  started: ['invitation.startedTitle', 'invitation.startedText'],
  cancelled: ['invitation.cancelledTitle', 'invitation.cancelledText'],
  closed: ['invitation.startedTitle', 'invitation.errorClosed'],
  off: ['invitation.startedTitle', 'invitation.offText'],
};

export default function InvitationReply() {
  const { token = '' } = useParams();
  const [state, setState] = useState<PageState>(token === SAMPLE_TOKEN ? 'sample' : 'loading');
  const [page, setPage] = useState<InvitationPage | null>(null);
  const [busy, setBusy] = useState<'coming' | 'declined' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const answerRef = useRef<HTMLParagraphElement>(null);
  const { t, language } = useT();
  usePageLanguage(page?.language);

  useEffect(() => {
    if (token === SAMPLE_TOKEN) return;
    let cancelled = false;
    setState('loading');
    getInvitationPage(token)
      .then((found) => {
        if (cancelled) return;
        setPage(found);
        setState('ready');
      })
      .catch((err: unknown) => {
        if (!cancelled) setState(stateOf(err) ?? 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt]);

  const answer = async (said: 'coming' | 'declined') => {
    setBusy(said);
    setError(null);
    try {
      setPage(await answerInvitation(token, said));
      // Move to what changed, so a screen reader says it.
      requestAnimationFrame(() => answerRef.current?.focus());
    } catch (err) {
      const status = err instanceof InvitationPageError ? err.status : 0;
      // A refusal that closes the link replaces the page; anything else (another change to the
      // list at the same moment, a dropped connection) leaves it, to be tried again.
      const closing = status === 404 || status === 403 || status === 409 ? stateOf(err) : null;
      const code = err instanceof InvitationPageError ? err.code : null;
      if (closing) setState(closing);
      // Refusals of this one answer that say why, in the member's language; the page stays.
      else if (code === 'Invitation.NotActive.Conflict') setError(t('invitation.errorNotActive'));
      else if (code === 'Invitation.TooLate.Conflict') setError(t('invitation.errorTooLate', { count: page?.cancelHours ?? 24 }));
      else setError(status === 429 ? t('invitation.errorTooMany') : t('invitation.errorNetwork'));
    } finally {
      setBusy(null);
    }
  };

  if (state !== 'ready' || !page) {
    const words = TITLES[state];
    return (
      <PublicPage>
        {state === 'loading' ? (
          <h1 className="text-xl font-semibold text-slate-900" aria-busy>{t('common.loading')}</h1>
        ) : state === 'unreachable' || !words ? (
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
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t(words[0])}</h1>
            <p className="mt-2 text-sm text-slate-600">{t(words[1])}</p>
          </>
        )}
      </PublicPage>
    );
  }

  // In the reader's language, in the club's time zone and number format.
  const dateLocale = dateLocaleFor(language, page.locale);
  const when = (() => {
    const options: Intl.DateTimeFormatOptions = { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' };
    try {
      return new Intl.DateTimeFormat(dateLocale, { ...options, timeZone: page.timeZone }).format(new Date(page.startAtUtc));
    } catch {
      return new Intl.DateTimeFormat(dateLocale, options).format(new Date(page.startAtUtc));
    }
  })();
  const price = page.memberPrice > 0 ? formatClubMoney(page.memberPrice, page.currency, page.locale ?? undefined) : t('invitation.free');
  const coming = page.answer === 'coming';
  const declined = page.answer === 'declined';

  return (
    <PublicPage>
      <p className="text-sm text-slate-600">{t('invitation.hello', { name: page.firstName })}</p>
      <p className="mt-1 text-sm text-slate-600">{t('invitation.invites', { club: page.club })}</p>
      <h1 className="mt-2 flex items-start gap-2 text-xl font-semibold text-slate-900">
        <span aria-hidden>{page.coverEmoji}</span>
        <span>{page.eventName}</span>
      </h1>
      {page.description && <p className="mt-2 whitespace-pre-line text-sm text-slate-700">{page.description}</p>}
      <dl className="mt-4 space-y-2 text-sm text-slate-700">
        <div className="flex items-start gap-2">
          <CalendarDays className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden />
          <dt className="sr-only">{t('invitation.when')}</dt>
          <dd>{when}</dd>
        </div>
        {page.location && (
          <div className="flex items-start gap-2">
            <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden />
            <dt className="sr-only">{t('invitation.where')}</dt>
            <dd>{page.location}</dd>
          </div>
        )}
        <div className="flex items-start gap-2">
          <Ticket className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden />
          <dt className="text-slate-600">{t('invitation.price')}</dt>
          <dd className="ml-auto font-medium">{price}</dd>
        </div>
      </dl>
      {page.memberPrice > 0 && <p className="mt-1 text-xs text-slate-500">{t('invitation.ticketOnAccount')}</p>}

      {(coming || declined) && (
        <p
          ref={answerRef}
          tabIndex={-1}
          role="status"
          className={`mt-5 rounded-xl px-3 py-2 text-sm focus:outline-none ${
            coming && page.place === 'waitlist' ? 'bg-amber-50 text-amber-900' : coming ? 'bg-emerald-50 text-emerald-900' : 'bg-slate-100 text-slate-700'
          }`}
        >
          {coming && page.place === 'waitlist'
            ? t('invitation.waitlist', { position: page.waitlistPosition ?? 1 })
            : coming ? t('invitation.seat') : t('invitation.declined')}
        </p>
      )}

      <h2 className="mt-5 text-base font-semibold text-slate-900">{t('invitation.question')}</h2>
      <p role="alert" className="mt-1 min-h-0 text-sm text-red-600">{error}</p>
      <div className="mt-2 grid gap-2 sm:grid-cols-2" aria-busy={busy != null}>
        <button
          type="button"
          onClick={() => void answer('coming')}
          disabled={busy != null || coming}
          aria-pressed={coming}
          className="h-11 rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
        >
          {busy === 'coming' ? t('invitation.busy') : t('invitation.yes')}
        </button>
        <button
          type="button"
          onClick={() => void answer('declined')}
          disabled={busy != null || declined}
          aria-pressed={declined}
          className="h-11 rounded-xl border border-slate-300 bg-white text-sm font-medium text-slate-800 disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
        >
          {busy === 'declined' ? t('invitation.busy') : t('invitation.no')}
        </button>
      </div>
      <p className="mt-3 text-xs text-slate-500">
        {page.cancelHours ? t('invitation.changeUntilHours', { count: page.cancelHours }) : t('invitation.changeUntil')}
      </p>
    </PublicPage>
  );
}
