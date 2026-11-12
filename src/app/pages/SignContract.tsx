// The page a counterparty lands on from a signing link.
//
// Public, unauthenticated, and the only page in the app written for someone who
// does not work here. That shapes everything: no app chrome, no navigation into
// the CRM, no jargon, and the contract itself is the largest thing on the screen
// because reading it is the point.
//
// The signature is a typed name plus an explicit tick. Both are required — the
// name is the mark, and the tick is what turns typing it into an act of agreement.
// What that is worth legally is stated plainly on the page rather than implied,
// because a signer deserves to know they are giving a simple electronic signature
// and not a qualified one.

import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  CheckCircle2, Loader2, PenLine, ShieldCheck, TriangleAlert, XCircle, FileText, Info,
  FileDown,
} from 'lucide-react';
import {
  declinePublicContract, getPublicContract, publicContractPdfUrl, signPublicContract,
  ContractError, type PublicContract,
} from '@/app/api/contracts';
import {
  MAX_SIGNATURE_NAME_LENGTH, checkSignature, isSignatureNameValid,
} from '@/app/pages/contracts/contractLifecycle';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { cn } from '@/app/components/ui/utils';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';
import { useFormat, useT, type MessageKey } from '@/app/i18n';

/** Bold words inside a translated sentence. */
const bold = (words: ReactNode) => <strong className="font-semibold text-slate-700">{words}</strong>;

/** What to say when the signature gate stops a signature, in the page's language. */
const GATE_PROBLEMS: Record<NonNullable<ReturnType<typeof checkSignature>['reason']>, MessageKey> = {
  closed: 'sign.problemClosed',
  notReady: 'sign.problemNotReady',
  name: 'sign.problemName',
  consent: 'sign.problemConsent',
};

/**
 * What happens after they sign.
 *
 * Someone opening a signing link from an email has no idea what the sequence is:
 * whether signing is the end of it, whether they get a copy, whether anyone else
 * has to do anything. Three lines answers that, and the reassurance that nothing
 * binds them until they sign is the reason people feel able to read it properly
 * rather than hunting for the catch.
 */
function WhatHappensNext({ organizationName }: { organizationName: string }) {
  const { t, tx } = useT();
  const steps = [
    t('sign.nextStep1'),
    t('sign.nextStep2', { organization: organizationName }),
    t('sign.nextStep3'),
  ];
  return (
    <section
      className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4 sm:p-5"
      aria-label={t('sign.nextTitle')}
    >
      <h2 className="text-[11px] font-semibold tracking-[0.12em] text-slate-500 uppercase">
        {t('sign.nextTitle')}
      </h2>
      <ol className="mt-3 grid gap-2.5 sm:grid-cols-3 sm:gap-3">
        {steps.map((text, i) => (
          <li key={i} className="flex items-start gap-2.5">
            <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white text-[10px] font-bold text-slate-500 ring-1 ring-slate-200">
              {i + 1}
            </span>
            <span className="text-[13px] leading-snug text-slate-700">{text}</span>
          </li>
        ))}
      </ol>
      <p className="mt-3.5 border-t border-slate-200 pt-3 text-[12.5px] text-slate-500">
        {tx('sign.nextNotAgreed', { b: bold })}
      </p>
    </section>
  );
}

/** Renders the contract body. */
function ContractBody({ body }: { body: string }) {
  const { t } = useT();
  return (
    // `whitespace-pre-wrap` rather than a Markdown renderer: the exact characters
    // that were signed are what must appear, and a renderer is a layer that can
    // silently change emphasis, swallow a stray bracket, or interpret a clause
    // number as a list.
    <div
      className={cn(
        'max-h-[52vh] overflow-y-auto rounded-2xl border border-slate-200 bg-white p-5 sm:p-7',
        'font-mono text-[13px] leading-relaxed whitespace-pre-wrap text-slate-800',
      )}
      // Scrollable region needs to be reachable and announced.
      tabIndex={0}
      role="region"
      aria-label={t('sign.contractText')}
    >
      {body}
    </div>
  );
}

function SignedBlock({ contract }: { contract: PublicContract }) {
  const { t } = useT();
  const format = useFormat();
  const fmt = (at?: string) => (at ? format.dateTime(at) : '—');
  const organization = contract.organizationName;
  return (
    <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
      <div className="flex items-center gap-2.5">
        <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-hidden />
        <h2 className="text-sm font-bold text-emerald-900">
          {contract.counterSignatureName ? t('sign.signedBoth') : t('sign.signedYours')}
        </h2>
      </div>
      <dl className="mt-3 grid gap-x-6 gap-y-1.5 text-[13px] sm:grid-cols-2">
        <div>
          <dt className="text-emerald-800/70">{t('sign.youSignedAs')}</dt>
          <dd className="font-semibold text-emerald-900">{contract.clientSignatureName ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-emerald-800/70">{t('sign.on')}</dt>
          <dd className="font-semibold text-emerald-900">{fmt(contract.clientSignedAtUtc)}</dd>
        </div>
        {contract.counterSignatureName && (
          <>
            <div>
              <dt className="text-emerald-800/70">{t('sign.orgSignedAs', { organization })}</dt>
              <dd className="font-semibold text-emerald-900">{contract.counterSignatureName}</dd>
            </div>
            <div>
              <dt className="text-emerald-800/70">{t('sign.on')}</dt>
              <dd className="font-semibold text-emerald-900">{fmt(contract.counterSignedAtUtc)}</dd>
            </div>
          </>
        )}
      </dl>
      <p className="mt-3 text-xs leading-relaxed text-emerald-800/80">
        {contract.counterSignatureName
          // Only claimed when the server says both copies actually went. It used
          // to state it as fact and tell them to keep the email as their record —
          // on a deployment where the send had failed, that was an instruction to
          // rely on something that did not exist.
          ? contract.executedCopySentAtUtc
            ? t('sign.copyEmailed')
            : t('sign.copyNotEmailed', { organization })
          : t('sign.orgWillSign', { organization })}
      </p>
    </div>
  );
}

export default function SignContract() {
  const { token = '' } = useParams<{ token: string }>();
  const { t, tx } = useT();
  const [contract, setContract] = useState<PublicContract | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [declining, setDeclining] = useState(false);
  const [confirmingDecline, setConfirmingDecline] = useState(false);
  const [declineReason, setDeclineReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setContract(await getPublicContract(token));
    } catch (e) {
      // The server's own wording is more use here than a status code — "this link
      // has expired" tells the signer what to do next.
      setLoadError(e instanceof ContractError ? e.message : t('sign.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [token, t]);

  useEffect(() => { void load(); }, [load]);

  const submit = async () => {
    if (!contract) return;
    // Checked locally first so the reason appears under the field instead of
    // arriving as a server error; the server checks the same things again.
    const gate = checkSignature({
      status: contract.status, action: 'client_sign', name, agreed,
    });
    if (!gate.ok) {
      setProblem(gate.reason ? t(GATE_PROBLEMS[gate.reason]) : gate.problem);
      return;
    }
    setSubmitting(true);
    setProblem(null);
    try {
      setContract(await signPublicContract(token, name, agreed));
    } catch (e) {
      setProblem(e instanceof ContractError ? e.message : t('sign.recordFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  /**
   * Declining, once confirmed.
   *
   * `declined` is terminal in the state machine: nothing at all may follow it, not
   * even Void, and there is no un-decline anywhere in the product. So the click
   * that reaches here has to be deliberate — see the confirmation step, which
   * exists because this used to be a plain text button sitting in the same row as
   * Sign, one mis-aimed click from ending the agreement forever.
   *
   * The reason is asked for at the same time. The API has always accepted one and
   * the CRM has always displayed it; nothing was ever sending it, so every decline
   * arrived with no explanation at all.
   */
  const decline = async () => {
    setDeclining(true);
    setProblem(null);
    try {
      setContract(await declinePublicContract(token, declineReason.trim() || undefined));
    } catch (e) {
      setProblem(e instanceof ContractError ? e.message : t('sign.declineFailed'));
    } finally {
      setDeclining(false);
    }
  };

  if (loading) {
    return (
      <Shell>
        <div className="flex items-center justify-center gap-2 py-24 text-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
          {t('sign.opening')}
        </div>
      </Shell>
    );
  }

  if (loadError || !contract) {
    return (
      <Shell>
        <div className="mx-auto max-w-md py-16 text-center">
          <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-amber-100">
            <TriangleAlert className="h-7 w-7 text-amber-600" aria-hidden />
          </span>
          <h1 className="mt-5 text-xl font-bold text-slate-900">{t('sign.wontOpenTitle')}</h1>
          <p className="mt-2 text-sm leading-relaxed text-slate-600">{loadError}</p>
          <p className="mt-4 text-xs text-slate-500">{t('sign.askNewLink')}</p>
        </div>
      </Shell>
    );
  }

  const alreadySigned = !!contract.clientSignatureName;

  return (
    <Shell>
      <motion.div
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.45, ease: [0.16, 1, 0.3, 1] }}
      >
        <div className="flex items-start gap-3.5">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 shadow-lg shadow-orange-500/20">
            <FileText className="h-5 w-5 text-white" aria-hidden />
          </span>
          <div className="min-w-0">
            <p className="text-[11px] font-semibold tracking-[0.14em] text-orange-700 uppercase">
              {contract.organizationName}
            </p>
            <h1 className="mt-0.5 text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">
              {contract.title}
            </h1>
            <p className="mt-1 text-sm text-slate-500">{t('sign.forParty', { name: contract.counterpartyName })}</p>
          </div>
        </div>

        <div className="mt-6">
          <ContractBody body={contract.body} />
          {/* Their own copy, before and after signing. Signing something you could
              not keep a copy of is a reasonable thing to hesitate over, and the
              PDF is the readable version of the same words shown above. */}
          <a
            href={publicContractPdfUrl(token)}
            target="_blank"
            rel="noopener noreferrer"
            className={cn(
              'mt-3 inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white',
              'px-3 py-1.5 text-xs font-semibold text-slate-600',
              'transition hover:border-slate-300 hover:text-slate-900',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400',
            )}
          >
            <FileDown className="h-3.5 w-3.5" aria-hidden />
            {alreadySigned ? t('sign.downloadSigned') : t('sign.downloadToRead')}
          </a>
        </div>

        <div className="mt-6">
          {alreadySigned ? (
            <SignedBlock contract={contract} />
          ) : !contract.canSign ? (
            <div className="flex items-start gap-2.5 rounded-2xl border border-slate-200 bg-slate-50 p-5">
              <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-slate-500" aria-hidden />
              <div>
                <h2 className="text-sm font-bold text-slate-800">{t('sign.notAvailableTitle')}</h2>
                <p className="mt-1 text-sm text-slate-600">{contract.blocked}</p>
              </div>
            </div>
          ) : (
            <>
            <WhatHappensNext organizationName={contract.organizationName} />
            <form
              className="mt-4 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6"
              onSubmit={(e) => { e.preventDefault(); void submit(); }}
            >
              <div className="flex items-center gap-2">
                <PenLine className="h-4 w-4 text-orange-500" aria-hidden />
                <h2 className="text-sm font-bold text-slate-900">{t('sign.formTitle')}</h2>
              </div>

              <label htmlFor="signature" className="mt-4 block text-sm font-medium text-slate-700">
                {t('sign.typeName')}
              </label>
              <Input
                id="signature"
                value={name}
                onChange={(e) => { setName(e.target.value); setProblem(null); }}
                placeholder={t('sign.namePlaceholder')}
                maxLength={MAX_SIGNATURE_NAME_LENGTH}
                autoComplete="name"
                aria-describedby="signature-help"
                className={cn(
                  'mt-1.5 h-12 max-w-md text-lg',
                  // The signature reads as a signature.
                  'font-[cursive]',
                  problem && !isSignatureNameValid(name) && 'border-rose-300 focus-visible:ring-rose-200',
                )}
              />
              <p id="signature-help" className="mt-1.5 text-xs text-slate-500">{t('sign.nameHelp')}</p>

              <label className="mt-4 flex cursor-pointer items-start gap-2.5">
                <input
                  type="checkbox"
                  checked={agreed}
                  onChange={(e) => { setAgreed(e.target.checked); setProblem(null); }}
                  className="mt-0.5 h-4 w-4 shrink-0 rounded border-slate-300 text-orange-700"
                />
                <span className="text-sm leading-relaxed text-slate-700">{t('sign.agree')}</span>
              </label>

              {problem && (
                <p role="alert" className="mt-3 text-sm font-medium text-rose-700">{problem}</p>
              )}

              <div className="mt-5 flex flex-wrap items-center gap-3">
                <Button
                  type="submit"
                  disabled={submitting}
                  className="h-11 bg-gradient-to-r from-orange-500 to-amber-500 px-6 font-semibold hover:from-orange-400 hover:to-amber-400"
                >
                  {submitting
                    ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> {t('sign.recording')}</>
                    : <><ShieldCheck className="mr-2 h-4 w-4" aria-hidden /> {t('sign.submit')}</>}
                </Button>
                {!confirmingDecline && (
                  <button
                    type="button"
                    onClick={() => setConfirmingDecline(true)}
                    className="text-sm font-medium text-slate-500 underline-offset-4 hover:text-slate-800 hover:underline"
                  >
                    {t('sign.dontWant')}
                  </button>
                )}
              </div>

              {/* The second step. Declining cannot be undone by anybody, so it is
                  not something to do by mis-aiming at the button beside Sign. */}
              {confirmingDecline && (
                <div className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-4">
                  <h3 className="text-sm font-bold text-rose-900">{t('sign.declineTitle')}</h3>
                  <p className="mt-1 text-[13px] leading-relaxed text-rose-800">
                    {t('sign.declineText', { organization: contract.organizationName })}
                  </p>
                  <label htmlFor="decline-reason" className="mt-3 block text-xs font-medium text-rose-900">
                    {t('sign.declineWhy')} <span className="font-normal opacity-70">{t('sign.optional')}</span>
                  </label>
                  <textarea
                    id="decline-reason"
                    rows={2}
                    value={declineReason}
                    onChange={(e) => setDeclineReason(e.target.value)}
                    maxLength={1000}
                    placeholder={t('sign.declinePlaceholder')}
                    className={cn(
                      'mt-1 w-full rounded-lg border border-rose-200 bg-white px-3 py-2 text-sm text-slate-800',
                      'focus:border-rose-400 focus:outline-none focus:ring-2 focus:ring-rose-100',
                    )}
                  />
                  <div className="mt-3 flex flex-wrap items-center gap-3">
                    <Button
                      type="button"
                      onClick={() => void decline()}
                      disabled={declining}
                      className="h-10 bg-rose-600 px-4 font-semibold hover:bg-rose-500"
                    >
                      {declining
                        ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> {t('sign.recordingShort')}</>
                        : t('sign.declineYes')}
                    </Button>
                    <button
                      type="button"
                      onClick={() => { setConfirmingDecline(false); setDeclineReason(''); }}
                      disabled={declining}
                      className="text-sm font-medium text-slate-600 underline-offset-4 hover:text-slate-900 hover:underline"
                    >
                      {t('sign.keepReading')}
                    </button>
                  </div>
                </div>
              )}
            </form>
            </>
          )}
        </div>

        {/* Said plainly rather than buried. Someone signing deserves to know what
            kind of signature they are giving — and the honest answer is a simple
            electronic signature, which is enforceable for ordinary commercial
            agreements but is NOT the qualified signature that Swiss law requires
            where written form is mandatory. */}
        <div className="mt-7 flex items-start gap-2.5 rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden />
          <p className="text-[12.5px] leading-relaxed text-slate-500">
            {tx('sign.aboutText', { b: bold, organization: contract.organizationName })}
          </p>
        </div>
      </motion.div>
    </Shell>
  );
}

/**
 * Deliberately plain: no CRM navigation for someone who does not work here. The one
 * control above the contract is the language picker.
 */
function Shell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-100 to-slate-50 px-4 py-10 sm:py-14">
      <main className="mx-auto w-full max-w-3xl">
        <div className="mb-4 flex justify-end">
          <LanguageSwitcher />
        </div>
        {children}
      </main>
    </div>
  );
}
