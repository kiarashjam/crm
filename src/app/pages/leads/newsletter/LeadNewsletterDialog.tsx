// "Invite to a meeting": a newsletter from the Leads page inviting the chosen leads to book a
// meeting on the workspace's website, each with a link and a code (RDV-XXXX) of their own.
// Three steps: who, the email, send. The server counts who it goes to, renders the email and
// sends it; this dialog chooses, shows and confirms.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRight, CalendarHeart, CalendarX2, Loader2, Mail, RefreshCw, Send, Settings2, Users } from 'lucide-react';
import { ApiError } from '@/app/api/apiClient';
import {
  getNewsletterAudience, getNewsletterSetup, previewNewsletter, sendNewsletter,
  type NewsletterAudience, type NewsletterEmailRequest, type NewsletterLanguage, type NewsletterPreview, type NewsletterSendResult,
  type NewsletterSetup,
} from '@/app/api/leadNewsletter';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { getCurrentUser } from '@/app/lib/auth';
import { IconTile } from '@/app/pages/settings/components/SettingsCard';
import { AudienceStep, type NewsletterMember } from './AudienceStep';
import { EmailStep, type EmailMode } from './EmailStep';
import { SendStep, type SendPhase } from './SendStep';
import { DEFAULT_FILTERS, chosenStatuses, leads, toAudienceRequest, type AudienceFilters } from './audience';
import { buildLetterHtml, isLetterDefault, letterDefaults, subjectDefault, type Letter } from './letter';
import { ProblemNote, StepIndicator, type StepInfo } from './parts';

const STEPS: StepInfo[] = [
  { label: 'Who', icon: Users },
  { label: 'Email', icon: Mail },
  { label: 'Send', icon: Send },
];

/** How long the filters or the words rest before the server is asked again. */
const COUNT_DELAY = 350;
const PREVIEW_DELAY = 400;

const message = (e: unknown) => (e instanceof Error && e.message ? e.message : 'Something went wrong. Try again.');

function newSendId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  // Older browsers: a v4-shaped id from Math.random, good enough to tell two presses apart.
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

const asLanguage = (value: string | null | undefined): NewsletterLanguage | null => (value === 'fr' || value === 'en' ? value : null);

export function LeadNewsletterDialog({ open, onOpenChange, statuses, sources, members }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The workspace's lead statuses, in its order. */
  statuses: string[];
  sources: string[];
  members: NewsletterMember[];
}) {
  // The description's id is Radix's own; read back so the dialog is described by it (the app's
  // DialogContent leaves aria-describedby off unless given one).
  const [describedBy, setDescribedBy] = useState<string | undefined>();
  const describe = useCallback((el: HTMLParagraphElement | null) => setDescribedBy(el?.id || undefined), []);
  const currentUser = useMemo(() => getCurrentUser(), []);
  const [step, setStep] = useState(0);

  // ── What it can do here ────────────────────────────────────────────────────
  const [setup, setSetup] = useState<NewsletterSetup | null>(null);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [setupTry, setSetupTry] = useState(0);

  // ── Who ────────────────────────────────────────────────────────────────────
  const [filters, setFilters] = useState<AudienceFilters>(DEFAULT_FILTERS);
  const [audience, setAudience] = useState<NewsletterAudience | null>(null);
  const [counting, setCounting] = useState(false);
  const [audienceError, setAudienceError] = useState<string | null>(null);
  const [recount, setRecount] = useState(0);

  // ── The email ──────────────────────────────────────────────────────────────
  const [language, setLanguage] = useState<NewsletterLanguage>('fr');
  const languageChosen = useRef(false);
  const [subject, setSubject] = useState(() => subjectDefault('fr'));
  const [letter, setLetter] = useState<Letter>(() => letterDefaults('fr'));
  const [mode, setMode] = useState<EmailMode>('letter');
  const [ownHtml, setOwnHtml] = useState('');
  // undefined: not chosen yet (the first lead it goes to, once known); null: a sample person.
  const [sampleLeadId, setSampleLeadId] = useState<string | null | undefined>(undefined);
  const [preview, setPreview] = useState<NewsletterPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);

  // ── Sending ────────────────────────────────────────────────────────────────
  const [phase, setPhase] = useState<SendPhase>('ready');
  const [result, setResult] = useState<NewsletterSendResult | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);
  // Made once for this newsletter: pressed twice, or tried again after a lost answer, the
  // server sends nothing more and answers with the first send.
  const sendId = useRef(newSendId());

  const letterHtml = useMemo(() => buildLetterHtml(letter, language), [letter, language]);
  const emailHtml = mode === 'letter' ? letterHtml : ownHtml;
  const request = useMemo(() => toAudienceRequest(filters, statuses), [filters, statuses]);
  const requestKey = JSON.stringify(request);
  const noStatus = chosenStatuses(filters, statuses).length === 0;
  const done = phase === 'done';

  const chosenSample = sampleLeadId === undefined ? audience?.recipients[0]?.leadId ?? null : sampleLeadId;
  const emailRequest = useMemo<NewsletterEmailRequest>(
    () => ({ language, subject, html: emailHtml, sampleLeadId: chosenSample }),
    [language, subject, emailHtml, chosenSample],
  );
  const emailKey = JSON.stringify(emailRequest);

  /** Change the language; words still as they came are put in the other language too. */
  const switchLanguage = (next: NewsletterLanguage) => {
    if (next === language) return;
    if (isLetterDefault(letter, language)) setLetter({ ...letterDefaults(next), color: letter.color });
    if (subject === subjectDefault(language)) setSubject(subjectDefault(next));
    setLanguage(next);
  };
  // For the setup's answer, which comes after the render that asked for it.
  const switchLanguageNow = useRef(switchLanguage);
  switchLanguageNow.current = switchLanguage;
  const bodyRef = useRef<HTMLDivElement>(null);

  // The setup, read each time the dialog opens.
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    getNewsletterSetup({ signal: controller.signal })
      .then((s) => {
        setSetup(s);
        setSetupError(null);
        const workspaceLanguage = asLanguage(s.defaultLanguage);
        // The workspace's language, unless one was chosen here already.
        if (workspaceLanguage && !languageChosen.current) {
          languageChosen.current = true;
          switchLanguageNow.current(workspaceLanguage);
        }
      })
      .catch((e: unknown) => { if (!controller.signal.aborted) setSetupError(message(e)); });
    return () => controller.abort();
  }, [open, setupTry]);

  // The count, a moment after the filters stop changing.
  useEffect(() => {
    if (!open || done) return;
    if (noStatus) {
      setCounting(false);
      return;
    }
    const controller = new AbortController();
    setCounting(true);
    const timer = window.setTimeout(() => {
      getNewsletterAudience(JSON.parse(requestKey), { signal: controller.signal })
        .then((a) => { setAudience(a); setAudienceError(null); })
        .catch((e: unknown) => { if (!controller.signal.aborted) setAudienceError(message(e)); })
        .finally(() => { if (!controller.signal.aborted) setCounting(false); });
    }, COUNT_DELAY);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [open, done, noStatus, requestKey, recount]);

  // The preview, once the email step has been reached, a moment after the typing stops.
  const reachedEmail = step >= 1;
  useEffect(() => {
    if (!open || done || !reachedEmail) return;
    const controller = new AbortController();
    setPreviewing(true);
    const timer = window.setTimeout(() => {
      previewNewsletter(JSON.parse(emailKey), { signal: controller.signal })
        .then((p) => { setPreview(p); setPreviewError(null); })
        .catch((e: unknown) => { if (!controller.signal.aborted) setPreviewError(message(e)); })
        .finally(() => { if (!controller.signal.aborted) setPreviewing(false); });
    }, PREVIEW_DELAY);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [open, done, reachedEmail, emailKey]);

  // ── What may be done next ─────────────────────────────────────────────────
  const audienceProblem = noStatus
    ? 'Pick at least one status.'
    : audienceError
      ? 'The count did not load.'
      : !audience
        ? 'Counting…'
        : audience.willReceive === 0
          ? 'Nobody to send to.'
          : audience.willReceive > audience.maxRecipients
            ? `At most ${leads(audience.maxRecipients)}.`
            : null;
  const emailProblem = !subject.trim()
    ? 'Add a subject.'
    : !emailHtml.trim()
      ? 'Write the email first.'
      : previewError
        ? 'The preview did not load.'
        : !preview
          ? 'Loading the preview…'
          : preview.unknown.length > 0
            ? `Nothing fills ${preview.unknown.join(', ')}.`
            : preview.bookingProblem
              ? 'The booking link cannot be filled in yet.'
              : null;
  const reachable = audienceProblem ? 0 : emailProblem ? 1 : 2;
  const waiting = counting || previewing;
  const sendBlocked = audienceProblem ?? emailProblem ?? (waiting ? 'Checking…' : null);
  const testBlocked = !subject.trim() ? 'Add a subject first.' : !emailHtml.trim() ? 'Write the email first.'
    : preview?.unknown.length ? `Nothing fills ${preview.unknown.join(', ')}.`
    : preview?.bookingProblem ? 'The booking link cannot be filled in yet.' : null;

  const go = (next: number) => {
    if (next < 0 || next > 2 || next > reachable || done) return;
    setSendError(null);
    if (next !== 2 && phase === 'confirm') setPhase('ready');
    setStep(next);
  };

  const chooseMode = (next: EmailMode) => {
    if (next === mode) return;
    if (next === 'html') setOwnHtml(letterHtml);
    else if (ownHtml !== letterHtml && !window.confirm('Back to the letter? The changes made to its HTML are lost.')) return;
    setMode(next);
  };

  const send = async () => {
    if (!audience || sendBlocked) return;
    setPhase('sending');
    setSendError(null);
    try {
      const sent = await sendNewsletter({
        sendId: sendId.current,
        language,
        subject,
        html: emailHtml,
        audience: request,
        expectedRecipients: audience.willReceive,
      });
      setResult(sent);
      setPhase('done');
    } catch (e) {
      setSendError(message(e));
      setPhase('ready');
      // Who it goes to changed since it was counted: count again, so the next confirm is of the right number.
      if (e instanceof ApiError && (e.code?.startsWith('Newsletter.Changed') || e.code === 'Newsletter.Nobody' || e.code === 'Newsletter.TooMany')) {
        setRecount((n) => n + 1);
      }
    }
  };

  /** After a send, the next newsletter starts afresh: nobody chosen twice by a forgotten filter. */
  const reset = () => {
    sendId.current = newSendId();
    setStep(0);
    setFilters(DEFAULT_FILTERS);
    setAudience(null);
    setAudienceError(null);
    setLetter(letterDefaults(language));
    setSubject(subjectDefault(language));
    setMode('letter');
    setOwnHtml('');
    setSampleLeadId(undefined);
    setPreview(null);
    setPreviewError(null);
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

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent
        aria-describedby={describedBy}
        // Into the steps, not onto the first button there is ("All" statuses would be one keypress away).
        onOpenAutoFocus={(e) => { e.preventDefault(); bodyRef.current?.focus(); }}
        className="flex h-[calc(100dvh-1rem)] w-[calc(100vw-1rem)] max-w-6xl flex-col gap-0 overflow-hidden rounded-2xl p-0 sm:h-[min(92dvh,920px)] sm:max-w-6xl dark:bg-slate-900"
      >
        <header className="flex flex-wrap items-center gap-x-4 gap-y-3 border-b border-slate-200 py-3.5 pl-4 pr-14 sm:pl-6 dark:border-white/10">
          <div className="flex min-w-0 items-center gap-3">
            <IconTile icon={CalendarHeart} tone="orange" size="sm" />
            <div className="min-w-0">
              <DialogTitle className="truncate text-base font-semibold text-slate-900 dark:text-white">Invite to a meeting</DialogTitle>
              <DialogDescription asChild>
                <p ref={describe} className="truncate text-xs text-slate-500 dark:text-slate-400">Each lead gets their own link and code to book</p>
              </DialogDescription>
            </div>
          </div>
          <StepIndicator className="sm:ml-auto" steps={STEPS} step={step} reachable={reachable} done={done} onGo={go} />
        </header>

        {setup && !setup.bookingLinkAvailable && !done && (
          <div className="flex flex-wrap items-center gap-3 border-b border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-900 sm:px-6 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
            <CalendarX2 className="h-4 w-4 shrink-0 text-amber-600 dark:text-amber-300" aria-hidden />
            <p className="min-w-0 flex-1">
              <span className="font-semibold">No booking link yet.</span>{' '}
              <span className="text-amber-800 dark:text-amber-200/90">{setup.bookingProblem}</span>
            </p>
            <Button asChild size="sm" variant="outline" className="h-8 shrink-0 border-amber-300 bg-white/70 text-amber-900 hover:bg-white dark:border-amber-500/40 dark:bg-transparent dark:text-amber-100">
              <Link to="/settings?tab=welcome"><Settings2 className="h-3.5 w-3.5" aria-hidden /> Set it up</Link>
            </Button>
          </div>
        )}

        <div ref={bodyRef} tabIndex={-1} className="min-h-0 flex-1 overflow-y-auto px-4 py-5 outline-none sm:px-6">
          {setupError && (
            <ProblemNote
              className="mb-4"
              action={(
                <Button type="button" size="sm" variant="ghost" onClick={() => setSetupTry((n) => n + 1)} className="h-7 shrink-0 px-2 text-rose-700 dark:text-rose-200">
                  <RefreshCw className="h-3.5 w-3.5" aria-hidden /> Retry
                </Button>
              )}
            >
              {setupError}
            </ProblemNote>
          )}
          {step === 0 && (
            <AudienceStep
              filters={filters}
              onChange={setFilters}
              statuses={statuses}
              sources={sources}
              members={members}
              currentUserId={currentUser?.id}
              audience={audience}
              counting={counting}
              error={audienceError}
              onRetry={() => setRecount((n) => n + 1)}
            />
          )}
          {step === 1 && (
            <EmailStep
              setup={setup}
              language={language}
              onLanguage={(next) => { languageChosen.current = true; switchLanguage(next); }}
              subject={subject}
              onSubject={setSubject}
              mode={mode}
              onMode={chooseMode}
              letter={letter}
              onLetter={setLetter}
              html={ownHtml}
              onHtml={setOwnHtml}
              recipients={audience?.recipients ?? []}
              sampleLeadId={chosenSample}
              onSample={setSampleLeadId}
              preview={preview}
              previewError={previewError}
              previewing={previewing}
              request={emailRequest}
              testBlocked={testBlocked}
              testTo={currentUser?.email ?? null}
            />
          )}
          {step === 2 && (
            <SendStep
              audience={audience}
              subject={preview?.subject ?? subject}
              language={language}
              phase={phase}
              result={result}
              error={sendError}
              blocked={sendBlocked}
              busy={waiting && !audienceProblem && !emailProblem}
              onAsk={() => { setSendError(null); setPhase('confirm'); }}
              onConfirm={() => void send()}
              onCancel={() => setPhase('ready')}
            />
          )}
        </div>

        <footer className="flex items-center gap-2 border-t border-slate-200 bg-slate-50/80 px-4 py-3 sm:px-6 dark:border-white/10 dark:bg-white/[0.02]">
          {done ? (
            // The button pressed is gone: the focus comes here rather than falling back to the dialog.
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
                  <p className="ml-auto min-w-0 truncate text-xs text-slate-500 dark:text-slate-400" aria-live="polite">
                    {step === 0 ? audienceProblem ?? (audience ? leads(audience.willReceive) : '') : emailProblem ?? ''}
                  </p>
                  <Button
                    type="button"
                    onClick={() => go(step + 1)}
                    disabled={step + 1 > reachable}
                    className="shrink-0 bg-gray-900 text-white hover:bg-gray-800 dark:bg-orange-500 dark:text-white dark:hover:bg-orange-400"
                  >
                    {step === 0 ? 'Next: the email' : 'Next: review'}
                    {(step === 0 ? counting : previewing) && reachable <= step
                      ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
                      : <ArrowRight className="h-4 w-4" aria-hidden />}
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
