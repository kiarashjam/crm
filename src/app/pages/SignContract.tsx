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

import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  CheckCircle2, Loader2, PenLine, ShieldCheck, TriangleAlert, XCircle, FileText, Info,
} from 'lucide-react';
import {
  declinePublicContract, getPublicContract, signPublicContract,
  ContractError, type PublicContract,
} from '@/app/api/contracts';
import { checkSignature, isSignatureNameValid } from '@/app/pages/contracts/contractLifecycle';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { cn } from '@/app/components/ui/utils';

/** Renders the contract body. */
function ContractBody({ body }: { body: string }) {
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
      aria-label="Contract text"
    >
      {body}
    </div>
  );
}

function SignedBlock({ contract }: { contract: PublicContract }) {
  const fmt = (at?: string) => (at ? new Date(at).toLocaleString() : '—');
  return (
    <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
      <div className="flex items-center gap-2.5">
        <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-hidden />
        <h2 className="text-sm font-bold text-emerald-900">
          {contract.counterSignatureName ? 'Signed by both parties' : 'Your signature is recorded'}
        </h2>
      </div>
      <dl className="mt-3 grid gap-x-6 gap-y-1.5 text-[13px] sm:grid-cols-2">
        <div>
          <dt className="text-emerald-800/70">You signed as</dt>
          <dd className="font-semibold text-emerald-900">{contract.clientSignatureName ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-emerald-800/70">On</dt>
          <dd className="font-semibold text-emerald-900">{fmt(contract.clientSignedAtUtc)}</dd>
        </div>
        {contract.counterSignatureName && (
          <>
            <div>
              <dt className="text-emerald-800/70">{contract.organizationName} signed as</dt>
              <dd className="font-semibold text-emerald-900">{contract.counterSignatureName}</dd>
            </div>
            <div>
              <dt className="text-emerald-800/70">On</dt>
              <dd className="font-semibold text-emerald-900">{fmt(contract.counterSignedAtUtc)}</dd>
            </div>
          </>
        )}
      </dl>
      <p className="mt-3 text-xs leading-relaxed text-emerald-800/80">
        {contract.counterSignatureName
          ? 'A copy has been emailed to you. Keep that email as your record.'
          : `${contract.organizationName} has been told you signed, and will add their signature. You will be emailed the finished copy.`}
      </p>
    </div>
  );
}

export default function SignContract() {
  const { token = '' } = useParams<{ token: string }>();
  const [contract, setContract] = useState<PublicContract | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [declining, setDeclining] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setContract(await getPublicContract(token));
    } catch (e) {
      // The server's own wording is more use here than a status code — "this link
      // has expired" tells the signer what to do next.
      setLoadError(e instanceof ContractError ? e.message : 'This signing link could not be opened.');
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => { void load(); }, [load]);

  const submit = async () => {
    if (!contract) return;
    // Checked locally first so the reason appears under the field instead of
    // arriving as a server error; the server checks the same things again.
    const gate = checkSignature({
      status: contract.status, action: 'client_sign', name, agreed,
    });
    if (!gate.ok) {
      setProblem(gate.problem);
      return;
    }
    setSubmitting(true);
    setProblem(null);
    try {
      setContract(await signPublicContract(token, name, agreed));
    } catch (e) {
      setProblem(e instanceof ContractError ? e.message : 'Your signature could not be recorded.');
    } finally {
      setSubmitting(false);
    }
  };

  const decline = async () => {
    setDeclining(true);
    setProblem(null);
    try {
      setContract(await declinePublicContract(token));
    } catch (e) {
      setProblem(e instanceof ContractError ? e.message : 'That could not be recorded.');
    } finally {
      setDeclining(false);
    }
  };

  if (loading) {
    return (
      <Shell>
        <div className="flex items-center justify-center gap-2 py-24 text-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
          Opening the contract…
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
          <h1 className="mt-5 text-xl font-bold text-slate-900">This link will not open</h1>
          <p className="mt-2 text-sm leading-relaxed text-slate-600">{loadError}</p>
          <p className="mt-4 text-xs text-slate-500">
            Reply to the email you received and ask for a new link.
          </p>
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
            <p className="text-[11px] font-semibold tracking-[0.14em] text-orange-600 uppercase">
              {contract.organizationName}
            </p>
            <h1 className="mt-0.5 text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">
              {contract.title}
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              For {contract.counterpartyName}. Please read it in full before signing.
            </p>
          </div>
        </div>

        <div className="mt-6">
          <ContractBody body={contract.body} />
        </div>

        <div className="mt-6">
          {alreadySigned ? (
            <SignedBlock contract={contract} />
          ) : !contract.canSign ? (
            <div className="flex items-start gap-2.5 rounded-2xl border border-slate-200 bg-slate-50 p-5">
              <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" aria-hidden />
              <div>
                <h2 className="text-sm font-bold text-slate-800">Not available for signature</h2>
                <p className="mt-1 text-sm text-slate-600">{contract.blocked}</p>
              </div>
            </div>
          ) : (
            <form
              className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6"
              onSubmit={(e) => { e.preventDefault(); void submit(); }}
            >
              <div className="flex items-center gap-2">
                <PenLine className="h-4 w-4 text-orange-500" aria-hidden />
                <h2 className="text-sm font-bold text-slate-900">Sign this contract</h2>
              </div>

              <label htmlFor="signature" className="mt-4 block text-sm font-medium text-slate-700">
                Type your full name
              </label>
              <Input
                id="signature"
                value={name}
                onChange={(e) => { setName(e.target.value); setProblem(null); }}
                placeholder="e.g. Jean Dupont"
                autoComplete="name"
                aria-describedby="signature-help"
                className={cn(
                  'mt-1.5 h-12 max-w-md text-lg',
                  // The signature reads as a signature.
                  'font-[cursive]',
                  problem && !isSignatureNameValid(name) && 'border-rose-300 focus-visible:ring-rose-200',
                )}
              />
              <p id="signature-help" className="mt-1.5 text-xs text-slate-500">
                Typing your name here is your signature.
              </p>

              <label className="mt-4 flex cursor-pointer items-start gap-2.5">
                <input
                  type="checkbox"
                  checked={agreed}
                  onChange={(e) => { setAgreed(e.target.checked); setProblem(null); }}
                  className="mt-0.5 h-4 w-4 shrink-0 rounded border-slate-300 text-orange-600"
                />
                <span className="text-sm leading-relaxed text-slate-700">
                  I have read the contract above and I agree to be bound by it.
                </span>
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
                    ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> Recording your signature…</>
                    : <><ShieldCheck className="mr-2 h-4 w-4" aria-hidden /> Sign the contract</>}
                </Button>
                <button
                  type="button"
                  onClick={() => void decline()}
                  disabled={declining}
                  className="text-sm font-medium text-slate-500 underline-offset-4 hover:text-slate-800 hover:underline"
                >
                  {declining ? 'Recording…' : 'I do not want to sign this'}
                </button>
              </div>
            </form>
          )}
        </div>

        {/* Said plainly rather than buried. Someone signing deserves to know what
            kind of signature they are giving — and the honest answer is a simple
            electronic signature, which is enforceable for ordinary commercial
            agreements but is NOT the qualified signature that Swiss law requires
            where written form is mandatory. */}
        <div className="mt-7 flex items-start gap-2.5 rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-hidden />
          <p className="text-[12.5px] leading-relaxed text-slate-500">
            <strong className="font-semibold text-slate-700">About this signature.</strong>{' '}
            Signing records your typed name together with the date, time, your network
            address and a fingerprint of this exact text. That is a simple electronic
            signature. It is not a qualified electronic signature under Swiss ZertES
            or EU eIDAS, which some kinds of agreement require. If you are unsure
            whether that matters here, ask {contract.organizationName} before signing.
          </p>
        </div>
      </motion.div>
    </Shell>
  );
}

/** Deliberately plain: no CRM navigation for someone who does not work here. */
function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-100 to-slate-50 px-4 py-10 sm:py-14">
      <main className="mx-auto w-full max-w-3xl">{children}</main>
    </div>
  );
}
