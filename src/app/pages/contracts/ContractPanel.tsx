// The CRM side of the contract flow, on a lead.
//
// Three of the four steps happen here — generate the draft, edit it, countersign
// once they have signed — and the fourth (their signature) is watched from here.
// The panel deliberately shows only the buttons the SERVER says are allowed, from
// `allowedActions`, so a stale client can never offer an action that will be
// refused.
//
// The one thing it must never do is claim a send that did not happen. When SMTP is
// unconfigured the contract is still sent and the link is still live, but nobody
// has been told — so the response's `emailSent` is surfaced, and the signing link
// is shown to be passed on by hand.

import { useCallback, useEffect, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import {
  FileText, Loader2, PenLine, Send, Sparkles, TriangleAlert, Copy, Check,
  ShieldCheck, Clock, Eye, Ban, RefreshCw, MailWarning, ChevronDown, FileDown,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  countersignContract, createContractDraft, listContractsForLead, openContractPdf,
  resendExecutedCopy, sendContract, updateContract, voidContract, ContractError,
  type Contract,
} from '@/app/api/contracts';
import { isUsingRealApi } from '@/app/api/apiClient';
import {
  STATUS_LABELS, checkSignature, type ContractStatus,
} from './contractLifecycle';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { cn } from '@/app/components/ui/utils';
import { ContractProgress } from './ContractProgress';

/** Fields the seeded template asks for that the CRM cannot know. */
const ASK_FOR: { key: string; label: string; placeholder: string }[] = [
  { key: 'contract.fee', label: 'Fee', placeholder: 'CHF 4,800 per year' },
  { key: 'contract.paymentTerms', label: 'Payment terms', placeholder: 'annually in advance' },
  { key: 'contract.startDate', label: 'Start date', placeholder: '1 October 2026' },
  { key: 'contract.term', label: 'Term', placeholder: '12 months' },
  { key: 'contract.noticePeriod', label: 'Notice period', placeholder: '30 days' },
  { key: 'contract.jurisdiction', label: 'Governing law', placeholder: 'Switzerland' },
];

const STATUS_TONE: Record<ContractStatus, string> = {
  draft: 'border-slate-200 bg-slate-50 text-slate-700',
  sent: 'border-amber-200 bg-amber-50 text-amber-800',
  signed_by_client: 'border-indigo-200 bg-indigo-50 text-indigo-800',
  countersigned: 'border-emerald-200 bg-emerald-50 text-emerald-800',
  declined: 'border-rose-200 bg-rose-50 text-rose-800',
  voided: 'border-slate-200 bg-slate-100 text-slate-500',
};

function StatusPill({ status }: { status: ContractStatus }) {
  return (
    <span className={cn(
      'inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-semibold',
      STATUS_TONE[status],
    )}>
      {STATUS_LABELS[status]}
    </span>
  );
}

function CopyLink({ url }: { url: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      onClick={async () => {
        await navigator.clipboard.writeText(url);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
      }}
      className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
    >
      {copied ? <Check className="h-3.5 w-3.5 text-emerald-600" aria-hidden /> : <Copy className="h-3.5 w-3.5" aria-hidden />}
      {copied ? 'Copied' : 'Copy signing link'}
    </button>
  );
}

const EVENT_ICON: Record<string, typeof Clock> = {
  created: Sparkles, edited: PenLine, sent: Send, resent: Send, viewed: Eye,
  signed: ShieldCheck, countersigned: ShieldCheck, emailed: Send,
  declined: Ban, voided: Ban,
};

function AuditTrail({ contract }: { contract: Contract }) {
  const [open, setOpen] = useState(false);
  if (contract.events.length === 0) return null;
  return (
    <div className="mt-4 border-t border-slate-100 pt-3">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex w-full items-center gap-1.5 text-xs font-semibold text-slate-500 hover:text-slate-800"
      >
        <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open && 'rotate-180')} aria-hidden />
        Signature record ({contract.events.length})
      </button>
      <AnimatePresence initial={false}>
        {open && (
          <motion.ul
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: [0.16, 1, 0.3, 1] }}
            className="overflow-hidden"
          >
            {contract.events.map((e) => {
              const Icon = EVENT_ICON[e.type] ?? Clock;
              return (
                <li key={e.id} className="flex items-start gap-2.5 pt-2.5 text-xs">
                  <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md bg-slate-100">
                    <Icon className="h-3 w-3 text-slate-500" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="font-medium text-slate-700">{e.detail || e.type}</span>
                    <span className="mt-0.5 block text-slate-400">
                      {new Date(e.atUtc).toLocaleString()}
                      {e.actorLabel ? ` · ${e.actorLabel}` : ''}
                    </span>
                  </span>
                </li>
              );
            })}
          </motion.ul>
        )}
      </AnimatePresence>
    </div>
  );
}

interface Props {
  leadId: string;
  leadName: string;
  /** Viewers cannot draft or sign anything. */
  readOnly?: boolean;
}

export function ContractPanel({ leadId, leadName, readOnly }: Props) {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [editing, setEditing] = useState<string | null>(null);
  const [draftBody, setDraftBody] = useState('');
  const [signName, setSignName] = useState('');
  const [signAgreed, setSignAgreed] = useState(false);
  const [signProblem, setSignProblem] = useState<string | null>(null);
  const [lastLink, setLastLink] = useState<{ id: string; url: string; emailed: boolean } | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      setContracts(await listContractsForLead(leadId));
    } catch {
      setContracts([]);
    } finally {
      setLoading(false);
    }
  }, [leadId]);

  useEffect(() => { void reload(); }, [reload]);

  const run = async (key: string, fn: () => Promise<void>) => {
    setBusy(key);
    try {
      await fn();
    } catch (e) {
      toast.error(e instanceof ContractError ? e.message : 'That did not work. Please try again.');
    } finally {
      setBusy(null);
    }
  };

  const draft = () => run('draft', async () => {
    const created = await createContractDraft({ leadId, values });
    if (!created) throw new ContractError('The draft could not be created.');
    await reload();
    setEditing(created.id);
    setDraftBody(created.body);
    toast.success('Draft ready — read it through before sending.');
  });

  const saveEdit = (c: Contract) => run(`save-${c.id}`, async () => {
    await updateContract(c.id, { body: draftBody });
    await reload();
    setEditing(null);
    toast.success('Draft saved');
  });

  const send = (c: Contract, resend: boolean) => run(`send-${c.id}`, async () => {
    const result = await sendContract(c.id, resend);
    if (!result) throw new ContractError('The contract could not be sent.');
    await reload();
    setLastLink({ id: c.id, url: result.signingUrl, emailed: result.emailSent });
    if (result.emailSent) {
      toast.success(`Sent to ${c.counterpartyEmail} for signature`);
    } else {
      // Never reported as a success. The contract IS sent; the email is not.
      toast.warning('Contract is ready to sign, but the email could not be sent — copy the link below.');
    }
  });

  const countersign = (c: Contract) => run(`sign-${c.id}`, async () => {
    const gate = checkSignature({
      status: c.status, action: 'countersign', name: signName, agreed: signAgreed,
    });
    if (!gate.ok) {
      setSignProblem(gate.problem);
      return;
    }
    setSignProblem(null);
    await countersignContract(c.id, signName, signAgreed);
    await reload();
    setSignName('');
    setSignAgreed(false);
    toast.success('Signed and sent to everyone');
  });

  if (!isUsingRealApi()) {
    return (
      <Card>
        <Header />
        <div className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50 p-4">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-hidden />
          <p className="text-sm text-slate-600">
            Contracts need a connected backend — a signing link has to point somewhere
            real. This is deliberately not faked in demo mode.
          </p>
        </div>
      </Card>
    );
  }

  return (
    <Card>
      <Header />

      {loading ? (
        <div className="flex items-center gap-2 py-6 text-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading contracts…
        </div>
      ) : contracts.length === 0 ? (
        <div>
          <p className="text-sm leading-relaxed text-slate-600">
            Generate a draft for <strong className="text-slate-900">{leadName}</strong> from your
            template. You can edit every word before anything is sent.
          </p>

          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {ASK_FOR.map((f) => (
              <label key={f.key} className="block">
                <span className="text-xs font-semibold text-slate-600">{f.label}</span>
                <Input
                  value={values[f.key] ?? ''}
                  onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
                  placeholder={f.placeholder}
                  className="mt-1 h-9"
                  disabled={readOnly}
                />
              </label>
            ))}
          </div>
          <p className="mt-2 text-[11px] text-slate-400">
            Anything left blank stays visible in the draft as a placeholder — the
            contract cannot be sent until it is filled in.
          </p>

          <Button
            onClick={() => void draft()}
            disabled={readOnly || busy === 'draft'}
            className="mt-4 gap-2 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-400 hover:to-amber-400"
          >
            {busy === 'draft'
              ? <><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Generating…</>
              : <><Sparkles className="h-4 w-4" aria-hidden /> Generate the draft contract</>}
          </Button>
        </div>
      ) : (
        <ul className="space-y-4">
          {contracts.map((c) => {
            const allowed = new Set(c.allowedActions);
            const link = lastLink?.id === c.id ? lastLink : null;
            return (
              <li key={c.id} className="rounded-2xl border border-slate-200 bg-white p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-bold text-slate-900">{c.title}</p>
                    <p className="mt-0.5 text-xs text-slate-500">
                      {c.counterpartyName} · {c.counterpartyEmail}
                    </p>
                  </div>
                  <StatusPill status={c.status} />
                </div>

                {/* Replaced a bare one-line hint. The pill says which state; this
                    says whose move it is, since when, and what happens next —
                    the questions people actually had. */}
                <div className="mt-3">
                  <ContractProgress contract={c} />
                </div>

                {/* Sending is blocked while any placeholder is unfilled, rather than
                    posting a contract that reads "Dear {{lead.name}},". */}
                {c.unresolvedFields.length > 0 && (
                  <div className="mt-3 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3">
                    <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
                    <p className="text-xs text-amber-900">
                      <strong className="font-semibold">Still to fill in:</strong>{' '}
                      {c.unresolvedFields.join(', ')}. Edit the draft and replace each
                      one — it cannot be sent while any are left.
                    </p>
                  </div>
                )}

                {/* Shown when the email did not go. The contract is live either way,
                    so the link is here to be passed on by hand. */}
                {link && !link.emailed && (
                  <div className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3">
                    <div className="flex items-start gap-2">
                      <MailWarning className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
                      <div className="min-w-0">
                        <p className="text-xs font-semibold text-amber-900">
                          The email could not be sent
                        </p>
                        <p className="mt-0.5 text-xs text-amber-800">
                          Email is not configured on this deployment, so nothing was
                          delivered. The contract is ready to sign — send this link
                          to {c.counterpartyEmail} yourself.
                        </p>
                        <div className="mt-2"><CopyLink url={link.url} /></div>
                      </div>
                    </div>
                  </div>
                )}
                {link && link.emailed && (
                  <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                    <Check className="h-3.5 w-3.5 text-emerald-600" aria-hidden />
                    Emailed to {c.counterpartyEmail}. <CopyLink url={link.url} />
                  </div>
                )}

                {editing === c.id ? (
                  <div className="mt-3">
                    <textarea
                      value={draftBody}
                      onChange={(e) => setDraftBody(e.target.value)}
                      rows={18}
                      spellCheck
                      aria-label="Contract text"
                      className="w-full rounded-xl border border-slate-200 bg-slate-50/60 p-3 font-mono text-[12.5px] leading-relaxed text-slate-800 focus:border-orange-300 focus:bg-white focus:outline-none"
                    />
                    <div className="mt-2 flex gap-2">
                      <Button size="sm" onClick={() => void saveEdit(c)} disabled={busy === `save-${c.id}`}>
                        {busy === `save-${c.id}` && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" aria-hidden />}
                        Save draft
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setEditing(null)}>Cancel</Button>
                    </div>
                  </div>
                ) : (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {/* Available in every state. A draft opens watermarked, so what is
                        reviewed on paper is the sheet the counterparty will get. */}
                    <Button
                      size="sm" variant="outline"
                      onClick={() => void run(`pdf-${c.id}`, async () => {
                        const opened = await openContractPdf(c.id);
                        if (!opened) {
                          toast.warning('Your browser blocked the new tab — allow pop-ups for this site.');
                        }
                      })}
                      disabled={busy === `pdf-${c.id}`}
                      className="gap-1.5"
                    >
                      {busy === `pdf-${c.id}`
                        ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
                        : <FileDown className="h-3.5 w-3.5" aria-hidden />}
                      {c.status === 'countersigned' ? 'Signed PDF' : 'Open as PDF'}
                    </Button>
                    {allowed.has('edit') && !readOnly && (
                      <Button
                        size="sm" variant="outline"
                        onClick={() => { setEditing(c.id); setDraftBody(c.body); }}
                        className="gap-1.5"
                      >
                        <PenLine className="h-3.5 w-3.5" aria-hidden /> Read and edit
                      </Button>
                    )}
                    {allowed.has('send') && !readOnly && (
                      <Button
                        size="sm"
                        onClick={() => void send(c, false)}
                        disabled={busy === `send-${c.id}` || c.unresolvedFields.length > 0}
                        className="gap-1.5 bg-orange-600 hover:bg-orange-500"
                      >
                        {busy === `send-${c.id}`
                          ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
                          : <Send className="h-3.5 w-3.5" aria-hidden />}
                        Send for signature
                      </Button>
                    )}
                    {allowed.has('resend') && !readOnly && (
                      <Button
                        size="sm" variant="outline"
                        onClick={() => void send(c, true)}
                        disabled={busy === `send-${c.id}`}
                        className="gap-1.5"
                      >
                        <RefreshCw className="h-3.5 w-3.5" aria-hidden /> Resend with a new link
                      </Button>
                    )}
                    {c.status === 'countersigned' && !readOnly && (
                      <Button
                        size="sm"
                        variant={c.executedCopySentAtUtc ? 'ghost' : 'outline'}
                        onClick={() => void run(`copy-${c.id}`, async () => {
                          const ok = await resendExecutedCopy(c.id);
                          await reload();
                          if (ok) toast.success('Signed copy emailed to both parties');
                          else toast.warning('Still could not email everyone — check the email settings.');
                        })}
                        disabled={busy === `copy-${c.id}`}
                        className="gap-1.5"
                      >
                        <Send className="h-3.5 w-3.5" aria-hidden />
                        {c.executedCopySentAtUtc ? 'Send the copy again' : 'Email the signed copy'}
                      </Button>
                    )}
                    {allowed.has('void') && !readOnly && (
                      <Button
                        size="sm" variant="ghost"
                        onClick={() => void run(`void-${c.id}`, async () => {
                          await voidContract(c.id);
                          await reload();
                          toast.success('Contract voided and its link disabled');
                        })}
                        disabled={busy === `void-${c.id}`}
                        className="gap-1.5 text-slate-500"
                      >
                        <Ban className="h-3.5 w-3.5" aria-hidden /> Void
                      </Button>
                    )}
                  </div>
                )}

                {/* Executed but undelivered is a real state, and worth seeing. */}
                {c.status === 'countersigned' && !c.executedCopySentAtUtc && (
                  <p className="mt-2 text-xs font-medium text-amber-700">
                    Signed by both parties, but the copy has not been emailed to everyone yet.
                  </p>
                )}

                {/* Step 4: our signature, offered only once they have signed. */}
                {allowed.has('countersign') && !readOnly && (
                  <div className="mt-3 rounded-xl border border-indigo-200 bg-indigo-50/60 p-3.5">
                    <p className="text-xs font-bold text-indigo-900">
                      {c.clientSignatureName} signed on{' '}
                      {c.clientSignedAtUtc ? new Date(c.clientSignedAtUtc).toLocaleString() : '—'}
                    </p>
                    <p className="mt-0.5 text-xs text-indigo-800/80">
                      Add your signature to execute it. Both parties are emailed the
                      finished contract.
                    </p>
                    <Input
                      value={signName}
                      onChange={(e) => { setSignName(e.target.value); setSignProblem(null); }}
                      placeholder="Type your full name"
                      className="mt-2.5 h-10 max-w-sm bg-white font-[cursive] text-base"
                    />
                    <label className="mt-2 flex cursor-pointer items-start gap-2">
                      <input
                        type="checkbox"
                        checked={signAgreed}
                        onChange={(e) => { setSignAgreed(e.target.checked); setSignProblem(null); }}
                        className="mt-0.5 h-4 w-4 shrink-0 rounded border-indigo-300 text-indigo-600"
                      />
                      <span className="text-xs leading-relaxed text-indigo-900">
                        I agree to be bound by this contract on behalf of the organisation.
                      </span>
                    </label>
                    {signProblem && (
                      <p role="alert" className="mt-2 text-xs font-medium text-rose-700">{signProblem}</p>
                    )}
                    <Button
                      size="sm"
                      onClick={() => void countersign(c)}
                      disabled={busy === `sign-${c.id}`}
                      className="mt-3 gap-1.5 bg-indigo-600 hover:bg-indigo-500"
                    >
                      {busy === `sign-${c.id}`
                        ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
                        : <ShieldCheck className="h-3.5 w-3.5" aria-hidden />}
                      Sign and send to everyone
                    </Button>
                  </div>
                )}

                <AuditTrail contract={c} />
              </li>
            );
          })}

          {!readOnly && (
            <li>
              <Button variant="outline" size="sm" onClick={() => void draft()} disabled={busy === 'draft'} className="gap-1.5">
                <Sparkles className="h-3.5 w-3.5" aria-hidden /> New draft
              </Button>
            </li>
          )}
        </ul>
      )}
    </Card>
  );
}

function Card({ children }: { children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
      {children}
    </section>
  );
}

function Header() {
  return (
    <div className="mb-4 flex items-center gap-3">
      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 shadow-lg shadow-orange-500/20">
        <FileText className="h-5 w-5 text-white" aria-hidden />
      </span>
      <div>
        <h2 className="text-base font-bold text-slate-900">Contract</h2>
        <p className="text-xs text-slate-500">
          Draft, send for signature, countersign — the pipeline moves by itself
        </p>
      </div>
    </div>
  );
}

export default ContractPanel;
