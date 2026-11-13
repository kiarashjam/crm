// Signing in to a club's member portal: ask for a link by email, then use it.
//
// The link carries its secret in the address's fragment (#token=…), which a browser never
// sends to a server. Using it takes a press of Continue: mail scanners open links to look at
// them, and one that used the link on opening would leave the member with a spent link. A
// link the club's own portal hands over (?handoff=1) is opened by the member's own browser,
// straight from that portal, so it signs in at once.

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getPortalClub, PortalError, redeemPortalLink, requestPortalLink } from '@/app/api/portal';
import { usePageLanguage, useT } from '@/app/i18n';
import { timed } from '@/app/hooks/queries/queryClient';
import { buttonClass, inputClass, LoadFailed, PortalCard, portalErrorText, usePortalTitle } from './portalCommon';

function useClub(portalKey: string) {
  const club = useQuery({
    queryKey: ['member-portal', portalKey, 'club'],
    queryFn: ({ signal }) => timed(signal, (s) => getPortalClub(portalKey, s)),
  });
  usePageLanguage(club.data?.language, 'club');
  usePortalTitle(club.data?.clubName);
  return club;
}

/** The portal's address is unknown, or the club has it closed: said, with nothing to fill in. */
function Unavailable({ missing, club }: { missing: boolean; club?: string }) {
  const { t } = useT();
  return missing ? (
    <>
      <h1 className="text-xl font-semibold text-slate-900">{t('portal.signIn.missingTitle')}</h1>
      <p className="mt-2 text-sm text-slate-600">{t('portal.signIn.missingText')}</p>
    </>
  ) : (
    <>
      <h1 className="text-xl font-semibold text-slate-900">{t('portal.signIn.closedTitle')}</h1>
      <p className="mt-2 text-sm text-slate-600">{t('portal.signIn.closedText', { club: club ?? '' })}</p>
    </>
  );
}

export function PortalSignIn({ portalKey }: { portalKey: string }) {
  const { t } = useT();
  const club = useClub(portalKey);
  const [email, setEmail] = useState('');
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await requestPortalLink(portalKey, email.trim());
      setSentTo(email.trim());
    } catch (err) {
      setError(portalErrorText(err, t));
    } finally {
      setBusy(false);
    }
  };

  const name = club.data?.clubName ?? '';
  return (
    <PortalCard>
      {club.isPending ? (
        <p aria-busy="true" className="text-sm text-slate-500">{t('common.loading')}</p>
      ) : club.isError ? (
        club.error instanceof PortalError && club.error.status === 404
          ? <Unavailable missing />
          : <LoadFailed onRetry={() => void club.refetch()} />
      ) : !club.data.open ? (
        <Unavailable missing={false} club={name} />
      ) : sentTo ? (
        <div aria-live="polite">
          <p className="text-sm font-medium text-slate-500">{name}</p>
          <h1 className="mt-1 text-xl font-semibold text-slate-900">{t('portal.signIn.sentTitle')}</h1>
          <p className="mt-2 text-sm text-slate-700">{t('portal.signIn.sentText', { email: sentTo, club: name })}</p>
          <button type="button" onClick={() => setSentTo(null)} className="mt-4 text-sm font-medium text-pink-700 underline-offset-2 hover:underline">
            {t('portal.signIn.again')}
          </button>
        </div>
      ) : (
        <form onSubmit={(e) => void submit(e)} noValidate>
          <p className="text-sm font-medium text-slate-500">{name}</p>
          <h1 className="mt-1 text-xl font-semibold text-slate-900">{t('portal.title')}</h1>
          <p className="mt-2 text-sm text-slate-600">{t('portal.signIn.intro', { club: name })}</p>
          <label htmlFor="portal-email" className="mt-5 block text-sm font-medium text-slate-700">{t('portal.signIn.email')}</label>
          <input
            id="portal-email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className={`${inputClass} mt-1.5`}
          />
          <p role="alert" className="mt-2 min-h-0 text-sm text-red-600">{error}</p>
          <button type="submit" disabled={busy || email.trim().length === 0} className={`${buttonClass} mt-3 w-full`}>
            {busy ? t('portal.signIn.sending') : t('portal.signIn.send')}
          </button>
        </form>
      )}
    </PortalCard>
  );
}

/** Where an emailed link lands: Continue uses it, and the account opens. */
export function PortalLinkLanding({ portalKey }: { portalKey: string }) {
  const { t } = useT();
  const navigate = useNavigate();
  const [search] = useSearchParams();
  const club = useClub(portalKey);
  const [token] = useState(() => new URLSearchParams(window.location.hash.replace(/^#/, '')).get('token') ?? '');
  const [state, setState] = useState<'ready' | 'busy' | 'invalid' | 'closed' | 'error'>(token ? 'ready' : 'invalid');
  const [error, setError] = useState<string | null>(null);
  const autoTried = useRef(false);

  // The secret is taken off the address bar, so it is not left in the history or shown to
  // whoever looks over a shoulder; it is kept in this page only until it is used.
  useEffect(() => {
    if (window.location.hash) window.history.replaceState(null, '', window.location.pathname + window.location.search);
  }, []);

  const go = async () => {
    setState('busy');
    setError(null);
    try {
      await redeemPortalLink(portalKey, token);
      navigate(`/member-portal/${encodeURIComponent(portalKey)}`, { replace: true });
    } catch (err) {
      if (err instanceof PortalError && (err.code === 'Portal.LinkInvalid' || err.status === 404)) setState('invalid');
      else if (err instanceof PortalError && (err.code === 'Portal.Off' || err.code === 'Module.Off')) setState('closed');
      else {
        setError(portalErrorText(err, t));
        setState('error');
      }
    }
  };

  // A link from the club's own portal is followed by the member's browser straight from there.
  useEffect(() => {
    if (search.get('handoff') === '1' && token && !autoTried.current) {
      autoTried.current = true;
      void go();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const name = club.data?.clubName ?? '';
  return (
    <PortalCard>
      {state === 'invalid' ? (
        <>
          <h1 className="text-xl font-semibold text-slate-900">{t('portal.link.invalidTitle')}</h1>
          <p className="mt-2 text-sm text-slate-600">{t('portal.link.invalidText')}</p>
          <Link to={`/member-portal/${encodeURIComponent(portalKey)}`} className={`${buttonClass} mt-4 w-full`}>
            {t('portal.link.askNew')}
          </Link>
        </>
      ) : state === 'closed' ? (
        <Unavailable missing={false} club={name} />
      ) : (
        <div aria-busy={state === 'busy'}>
          <p className="text-sm font-medium text-slate-500">{name}</p>
          <h1 className="mt-1 text-xl font-semibold text-slate-900">{t('portal.link.title', { club: name })}</h1>
          <p className="mt-2 text-sm text-slate-600">{t('portal.link.text')}</p>
          <p role="alert" className="mt-2 min-h-0 text-sm text-red-600">{error}</p>
          <button type="button" onClick={() => void go()} disabled={state === 'busy'} className={`${buttonClass} mt-3 w-full`}>
            {state === 'busy' ? t('portal.link.signingIn') : t('portal.link.continue')}
          </button>
        </div>
      )}
    </PortalCard>
  );
}
