// "Invite members" on an event: three steps, modelled on "Invite to a meeting" for leads. Who
// (the member audience campaigns use, with who is left out and why), the letter (each language's
// subject and words, a preview as one member, a test to oneself), then the send (once, with an
// id made here, so a second press or a retry sends nothing more). The server counts, writes each
// member's letter in their language with their own reply link, and sends; this chooses, shows
// and confirms.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, ArrowRight, CalendarHeart, FlaskConical, Loader2, Mail, RefreshCw, RotateCcw, Send, Users } from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/app/api/apiClient';
import type { ClubEvent } from '@/app/api/events';
import {
  getInvitationAudience, getInvitationSetup, LETTER_LANGUAGES, previewInvitation, sendInvitations, sendInvitationTest,
  type InvitationLetter, type InvitationPreview, type InvitationSendResult, type InvitationSetup, type LetterLanguage,
} from '@/app/api/eventInvitations';
import { members, type MemberAudience, type MemberAudienceFilter } from '@/app/api/memberAudience';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { AudienceFilters } from '@/app/components/audience/AudienceFilters';
import { AudienceSummary } from '@/app/components/audience/AudienceSummary';
import { ProblemNote, StepIndicator, type StepInfo } from '@/app/pages/leads/newsletter/parts';
import { useClubCatalog, useClubEvents } from '@/app/hooks/queries';
import { MEMBER_TIERS } from '@/app/api/members';

const STEPS: StepInfo[] = [
  { label: 'Who', icon: Users },
  { label: 'Letter', icon: Mail },
  { label: 'Send', icon: Send },
];

/** How long the filters or the words rest before the server is asked again. */
const COUNT_DELAY = 350;
const PREVIEW_DELAY = 400;

type Letters = Record<LetterLanguage, { subject: string; body: string }>;
const EMPTY_LETTERS: Letters = { en: { subject: '', body: '' }, de: { subject: '', body: '' }, fr: { subject: '', body: '' } };

const message = (e: unknown) => (e instanceof Error && e.message ? e.message : 'Something went wrong. Try again.');

function newSendId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  // Older browsers: a v4-shaped id from Math.random, good enough to tell two presses apart.
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

function lettersFrom(defaults: Record<LetterLanguage, InvitationLetter> | undefined): Letters {
  const one = (l: LetterLanguage) => ({ subject: defaults?.[l]?.subject ?? '', body: defaults?.[l]?.body ?? '' });
  return { en: one('en'), de: one('de'), fr: one('fr') };
}

/** What is wrong with one language's letter before the server is asked, or null. */
function letterProblem(letter: { subject: string; body: string }, name: string): string | null {
  if (!letter.subject.trim()) return `The ${name} letter has no subject.`;
  if (!letter.body.trim()) return `The ${name} letter is empty.`;
  if (!/\{\{\s*replyLink\s*(\|[^{}]*)?\}\}/i.test(letter.body)) return `The ${name} letter has no {{replyLink}}: members could not answer.`;
  return null;
}

export function InviteMembersDialog({ event, open, onOpenChange }: {
  event: ClubEvent;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [describedBy, setDescribedBy] = useState<string | undefined>();
  const describe = useCallback((el: HTMLParagraphElement | null) => setDescribedBy(el?.id || undefined), []);
  const [step, setStep] = useState(0);

  // ── What it can do here ────────────────────────────────────────────────────
  const [setup, setSetup] = useState<InvitationSetup | null>(null);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [setupTry, setSetupTry] = useState(0);

  // ── Who ────────────────────────────────────────────────────────────────────
  const [filters, setFilters] = useState<MemberAudienceFilter>({ statuses: ['Active'] });
  const [audience, setAudience] = useState<MemberAudience | null>(null);
  const [counting, setCounting] = useState(false);
  const [audienceError, setAudienceError] = useState<string | null>(null);
  const [recount, setRecount] = useState(0);
  const catalog = useClubCatalog('Membership');
  const events = useClubEvents(open);
  const tiers = useMemo(() => {
    const names = (catalog.data ?? []).map((t) => t.name);
    return names.length ? names : MEMBER_TIERS;
  }, [catalog.data]);

  // ── The letter ─────────────────────────────────────────────────────────────
  const [letters, setLetters] = useState<Letters>(EMPTY_LETTERS);
  const edited = useRef(false);
  const [language, setLanguage] = useState<LetterLanguage>('en');
  // undefined: the first member it goes to, once known; null: a sample person in the language shown.
  const [sampleMemberId, setSampleMemberId] = useState<string | null | undefined>(undefined);
  const [preview, setPreview] = useState<InvitationPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [testing, setTesting] = useState(false);

  // ── Sending ────────────────────────────────────────────────────────────────
  const [phase, setPhase] = useState<'ready' | 'confirm' | 'sending' | 'done'>('ready');
  const [result, setResult] = useState<InvitationSendResult | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);
  // Made once for this send: pressed twice, or tried again after a lost answer, the server
  // sends nothing more and answers with the first send.
  const sendId = useRef(newSendId());
  const done = phase === 'done';

  const filtersKey = JSON.stringify(filters);
  const chosenSample = sampleMemberId === undefined ? audience?.recipients[0]?.memberId ?? null : sampleMemberId;
  const previewRequest = useMemo(() => ({
    letters,
    sampleMemberId: chosenSample,
    language: chosenSample ? null : language,
  }), [letters, chosenSample, language]);
  const previewKey = JSON.stringify(previewRequest);

  // The setup, read each time the dialog opens; its default letters fill the editor until someone writes in it.
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    getInvitationSetup(event.id, { signal: controller.signal })
      .then((s) => {
        setSetup(s);
        setSetupError(null);
        if (!edited.current) setLetters(lettersFrom(s.defaults));
      })
      .catch((e: unknown) => { if (!controller.signal.aborted) setSetupError(message(e)); });
    return () => controller.abort();
  }, [open, event.id, setupTry]);

  // The count, a moment after the filters stop changing.
  useEffect(() => {
    if (!open || done) return;
    const controller = new AbortController();
    setCounting(true);
    const timer = window.setTimeout(() => {
      getInvitationAudience(event.id, JSON.parse(filtersKey) as MemberAudienceFilter, { signal: controller.signal })
        .then((a) => { setAudience(a); setAudienceError(null); })
        .catch((e: unknown) => { if (!controller.signal.aborted) setAudienceError(message(e)); })
        .finally(() => { if (!controller.signal.aborted) setCounting(false); });
    }, COUNT_DELAY);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [open, done, event.id, filtersKey, recount]);

  // The preview, once the letter step has been reached, a moment after the typing stops.
  const reachedLetter = step >= 1;
  useEffect(() => {
    if (!open || done || !reachedLetter) return;
    const controller = new AbortController();
    setPreviewing(true);
    const timer = window.setTimeout(() => {
      previewInvitation(event.id, JSON.parse(previewKey), { signal: controller.signal })
        .then((p) => { setPreview(p); setPreviewError(null); })
        .catch((e: unknown) => { if (!controller.signal.aborted) setPreviewError(message(e)); })
        .finally(() => { if (!controller.signal.aborted) setPreviewing(false); });
    }, PREVIEW_DELAY);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [open, done, reachedLetter, event.id, previewKey]);

  // ── What may be done next ─────────────────────────────────────────────────
  const closed = setup?.closed ?? setup?.replyLinkProblem ?? null;
  const audienceProblem = audienceError
    ? 'The count did not load.'
    : !audience
      ? 'Counting…'
      : audience.willReceive === 0
        ? 'Nobody to invite.'
        : setup && audience.willReceive > setup.maxRecipients
          ? `At most ${members(setup.maxRecipients)}.`
          : null;
  const letterIssue = LETTER_LANGUAGES.map((l) => letterProblem(letters[l.code], l.name)).find(Boolean) ?? null;
  const letterStepProblem = letterIssue
    ?? (previewError ? 'The preview did not load.' : preview?.unknown.length ? `Nothing fills ${preview.unknown.join(', ')}.` : null);
  const reachable = audienceProblem ? 0 : letterStepProblem ? 1 : 2;
  const sendBlocked = closed ?? audienceProblem ?? letterStepProblem ?? (counting || previewing ? 'Checking…' : null);

  const go = (next: number) => {
    if (next < 0 || next > 2 || next > reachable || done) return;
    setSendError(null);
    if (next !== 2 && phase === 'confirm') setPhase('ready');
    setStep(next);
  };

  const write = (code: LetterLanguage, patch: Partial<{ subject: string; body: string }>) => {
    edited.current = true;
    setLetters((now) => ({ ...now, [code]: { ...now[code], ...patch } }));
  };

  const insert = (field: string) => write(language, { body: `${letters[language].body}{{${field}}}` });

  const test = async () => {
    setTesting(true);
    try {
      const sent = await sendInvitationTest(event.id, previewRequest);
      toast.success(sent.status === 'sent'
        ? `Test sent to ${sent.to}. Members have not received it.`
        : `Test queued for ${sent.to}: the mail server did not take it yet, and it is tried again shortly.`);
    } catch (e) {
      toast.error(message(e));
    } finally {
      setTesting(false);
    }
  };

  const send = async () => {
    if (!audience || sendBlocked) return;
    setPhase('sending');
    setSendError(null);
    try {
      const sent = await sendInvitations(event.id, {
        sendId: sendId.current,
        audience: filters,
        letters,
        expectedRecipients: audience.willReceive,
      });
      setResult(sent);
      setPhase('done');
    } catch (e) {
      setSendError(message(e));
      setPhase('ready');
      // Who it goes to changed since it was counted: count again, so the next confirm is of the right number.
      if (e instanceof ApiError && (e.code?.startsWith('Invitation.Changed') || e.code === 'Invitation.Nobody' || e.code === 'Invitation.TooMany')) {
        setRecount((n) => n + 1);
      }
    }
  };

  /** After a send, the next one starts afresh: nobody invited twice by a forgotten filter. */
  const reset = () => {
    sendId.current = newSendId();
    setStep(0);
    setFilters({ statuses: ['Active'] });
    setAudience(null);
    setPreview(null);
    setSampleMemberId(undefined);
    setPhase('ready');
    setResult(null);
    setSendError(null);
  };

  const close = (next: boolean) => {
    // Not while it is being sent: the answer is what says whether it went.
    if (!next && phase === 'sending') return;
    onOpenChange(next);
    if (!next && done) reset();
  };

  const current = letters[language];

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent
        aria-describedby={describedBy}
        className="flex h-[calc(100dvh-1rem)] w-[calc(100vw-1rem)] max-w-5xl flex-col gap-0 overflow-hidden rounded-2xl p-0 sm:h-[min(92dvh,900px)] sm:max-w-5xl"
      >
        <header className="flex flex-wrap items-center gap-x-4 gap-y-3 border-b border-slate-200 py-3.5 pl-4 pr-14 sm:pl-6">
          <div className="flex min-w-0 items-center gap-3">
            <CalendarHeart className="h-5 w-5 shrink-0 text-pink-600" aria-hidden />
            <div className="min-w-0">
              <DialogTitle className="truncate text-base font-semibold text-slate-900">Invite members to {event.name}</DialogTitle>
              <DialogDescription asChild>
                <p ref={describe} className="truncate text-xs text-slate-500">Each member gets the letter in their language, with a link of their own to answer</p>
              </DialogDescription>
            </div>
          </div>
          <StepIndicator className="sm:ml-auto" steps={STEPS} step={step} reachable={reachable} done={done} onGo={go} />
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-5 sm:px-6">
          {setupError && (
            <ProblemNote
              className="mb-4"
              action={(
                <Button type="button" size="sm" variant="ghost" onClick={() => setSetupTry((n) => n + 1)} className="h-7 shrink-0 px-2 text-rose-700">
                  <RefreshCw className="h-3.5 w-3.5" aria-hidden /> Retry
                </Button>
              )}
            >
              {setupError}
            </ProblemNote>
          )}
          {closed && !done && <ProblemNote className="mb-4">{closed}</ProblemNote>}

          {step === 0 && (
            <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
              <AudienceFilters idPrefix="invite" value={filters} onChange={setFilters} tiers={tiers} events={(events.data ?? []).filter((e) => e.id !== event.id)} />
              <div className="h-fit rounded-xl border border-fuchsia-200 bg-fuchsia-50 p-3 lg:sticky lg:top-0">
                <AudienceSummary
                  audience={audience}
                  loading={counting}
                  error={audienceError}
                  onRetry={() => setRecount((n) => n + 1)}
                  noun="invitation"
                />
                <p className="mt-2 text-xs text-slate-600">
                  Invitations are marketing: only members who agreed to it by email get one, and each ends with the link that stops them.
                  Members already on the list, or invited already, are left out.
                </p>
              </div>
            </div>
          )}

          {step === 1 && (
            <div className="grid gap-6 lg:grid-cols-2">
              <div className="space-y-3">
                <div role="tablist" aria-label="Language of the letter" className="flex gap-1.5">
                  {LETTER_LANGUAGES.map((l) => (
                    <button
                      key={l.code}
                      type="button"
                      role="tab"
                      aria-selected={language === l.code}
                      onClick={() => setLanguage(l.code)}
                      className={`rounded-full px-3 py-1 text-sm ${language === l.code ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'}`}
                    >
                      {l.name}
                    </button>
                  ))}
                </div>
                <p className="text-xs text-slate-500">Each member gets the letter in their own language (English when none is set on their record).</p>
                <div>
                  <Label htmlFor="invite-subject">Subject</Label>
                  <Input id="invite-subject" value={current.subject} onChange={(e) => write(language, { subject: e.target.value })} />
                </div>
                <div>
                  <Label htmlFor="invite-body">Letter</Label>
                  <Textarea id="invite-body" rows={14} value={current.body} onChange={(e) => write(language, { body: e.target.value })} className="font-mono text-sm" />
                </div>
                <div className="flex flex-wrap items-center gap-1.5" aria-label="Placeholders">
                  {(setup?.fields ?? []).map((f) => (
                    <button key={f} type="button" onClick={() => insert(f)} className="rounded-md bg-indigo-50 px-2 py-0.5 font-mono text-xs text-indigo-800 ring-1 ring-inset ring-indigo-200 hover:bg-indigo-100">
                      {`{{${f}}}`}
                    </button>
                  ))}
                  {setup && (
                    <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-xs"
                      onClick={() => write(language, { subject: setup.defaults[language]?.subject ?? '', body: setup.defaults[language]?.body ?? '' })}>
                      <RotateCcw className="h-3.5 w-3.5" aria-hidden /> Back to the default text
                    </Button>
                  )}
                </div>
                <p className="text-xs text-slate-500">
                  {'{{eventDate}}'} is written in the member’s language and the club’s time zone; {'{{replyLink}}'} is their own link to answer.
                </p>
                {letterIssue && <ProblemNote>{letterIssue}</ProblemNote>}
              </div>

              <div className="space-y-3">
                <div className="flex flex-wrap items-end gap-2">
                  <div className="min-w-0 flex-1">
                    <Label htmlFor="invite-sample">Preview as</Label>
                    <select
                      id="invite-sample"
                      className="mt-1 h-9 w-full rounded-md border border-slate-200 bg-white px-2 text-sm"
                      value={chosenSample ?? ''}
                      onChange={(e) => setSampleMemberId(e.target.value || null)}
                    >
                      <option value="">A sample person, in the language shown</option>
                      {(audience?.recipients ?? []).map((p) => <option key={p.memberId} value={p.memberId}>{p.name} ({p.language})</option>)}
                    </select>
                  </div>
                  <Button type="button" variant="outline" size="sm" onClick={() => void test()} disabled={testing || !!letterIssue || !!preview?.unknown.length}>
                    {testing ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <FlaskConical className="h-3.5 w-3.5" aria-hidden />} Send a test to me
                  </Button>
                </div>
                <article aria-label="Preview" aria-busy={previewing} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                  {previewError ? (
                    <p className="text-sm text-rose-700">{previewError}</p>
                  ) : !preview ? (
                    <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading the preview…</p>
                  ) : (
                    <>
                      <p className="text-xs text-slate-500">
                        To {preview.sampleName}{preview.sampleEmail ? ` <${preview.sampleEmail}>` : ''} · from {preview.fromName ?? 'the club'}
                        {preview.replyTo ? ` · replies to ${preview.replyTo}` : ''}
                      </p>
                      <p className="mt-1 font-semibold text-slate-900">{preview.subject}</p>
                      <div className="mt-3 whitespace-pre-wrap text-sm text-slate-800">{preview.body}</div>
                      {preview.unknown.length > 0 && <ProblemNote className="mt-3">Nothing fills {preview.unknown.join(', ')}.</ProblemNote>}
                    </>
                  )}
                </article>
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="mx-auto max-w-xl space-y-4" aria-live="polite">
              {done && result ? (
                <div role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
                  <p className="font-semibold">
                    {result.alreadySent ? 'These invitations had been sent already: nothing more went.' : `${members(result.recipients)} invited.`}
                  </p>
                  <p className="mt-1">
                    The emails go out over the next minutes. Their answers show on this page as they come in.
                    {result.leftOut > 0 ? ` ${members(result.leftOut)} were left out.` : ''}
                  </p>
                </div>
              ) : (
                <>
                  {audience && <AudienceSummary audience={audience} loading={counting} noun="invitation" />}
                  {sendError && <ProblemNote>{sendError}</ProblemNote>}
                  {phase === 'confirm' ? (
                    <div className="rounded-xl border border-slate-200 bg-white p-4">
                      <p className="text-sm text-slate-800">
                        Invite <strong>{members(audience?.willReceive ?? 0)}</strong> to {event.name}? Each is emailed once, with their own link to answer.
                      </p>
                      <div className="mt-3 flex gap-2">
                        <Button type="button" onClick={() => void send()}><Send className="h-4 w-4" aria-hidden /> Send the invitations</Button>
                        <Button type="button" variant="ghost" onClick={() => setPhase('ready')}>Not yet</Button>
                      </div>
                    </div>
                  ) : (
                    <Button type="button" onClick={() => { setSendError(null); setPhase('confirm'); }} disabled={!!sendBlocked || phase === 'sending'}>
                      {phase === 'sending' ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Send className="h-4 w-4" aria-hidden />}
                      {audience ? `Invite ${members(audience.willReceive)}` : 'Invite'}
                    </Button>
                  )}
                  {sendBlocked && phase !== 'sending' && <p className="text-xs text-slate-500">{sendBlocked}</p>}
                </>
              )}
            </div>
          )}
        </div>

        <footer className="flex items-center gap-2 border-t border-slate-200 bg-slate-50/80 px-4 py-3 sm:px-6">
          {done ? (
            <Button type="button" className="ml-auto" autoFocus onClick={() => close(false)}>Done</Button>
          ) : (
            <>
              {step > 0 && (
                <Button type="button" variant="ghost" onClick={() => go(step - 1)} disabled={phase === 'sending'}>
                  <ArrowLeft className="h-4 w-4" aria-hidden /> Back
                </Button>
              )}
              {step < 2 && (
                <>
                  <p className="ml-auto min-w-0 truncate text-xs text-slate-500" aria-live="polite">
                    {step === 0 ? audienceProblem ?? (audience ? members(audience.willReceive) : '') : letterStepProblem ?? ''}
                  </p>
                  <Button type="button" onClick={() => go(step + 1)} disabled={step + 1 > reachable} className="shrink-0">
                    {step === 0 ? 'Next: the letter' : 'Next: review'}
                    <ArrowRight className="h-4 w-4" aria-hidden />
                  </Button>
                </>
              )}
            </>
          )}
        </footer>
      </DialogContent>
    </Dialog>
  );
}
