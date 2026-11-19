// Step two: the email. The words on the left (or its HTML), and on the right the email as the
// server renders it for one of the leads, at a desktop's or a phone's width, with a test sent
// to oneself.

import { useEffect, useId, useRef, useState, type ComponentType, type ReactNode } from 'react';
import {
  CalendarCheck, CheckCircle2, Code2, Feather, Heading1, Images, KeyRound, Languages, Loader2, Mail, Monitor, PenLine, Send,
  Signature, Smartphone, TriangleAlert,
} from 'lucide-react';
import {
  sendNewsletterTest, type NewsletterEmailRequest, type NewsletterLanguage, type NewsletterPerson, type NewsletterPreview,
  type NewsletterSetup, type NewsletterTestResult,
} from '@/app/api/leadNewsletter';
import { PlaceholderChips, insertAt } from '@/app/components/onboarding';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import { ImageSlot, useEmailImageLibrary } from './ImageSlot';
import type { Letter, LetterImages } from './letter';
import { PreviewFrame, ProblemNote, Segmented } from './parts';

export type EmailMode = 'letter' | 'html';

const LANGUAGES: { id: NewsletterLanguage; label: string }[] = [
  { id: 'fr', label: 'FR' },
  { id: 'en', label: 'EN' },
];

const MODES: { id: EmailMode; label: string; hint: string; icon: typeof PenLine }[] = [
  { id: 'letter', label: "Founders' Letter", hint: 'The welcome letter’s design', icon: Feather },
  { id: 'html', label: 'Your own HTML', hint: 'Start from the letter, edit the code', icon: Code2 },
];

/** The letter's words, each in its own box. */
type TextKey = 'kicker' | 'title' | 'accent' | 'heading' | 'message' | 'quote' | 'button' | 'duration' | 'place' | 'signoff' | 'signature';
const TEXT_KEYS: TextKey[] = ['kicker', 'title', 'accent', 'heading', 'message', 'quote', 'button', 'duration', 'place', 'signoff', 'signature'];

const SAMPLE = '__sample__';

export function EmailStep({
  setup, language, onLanguage, subject, onSubject, mode, onMode, letter, onLetter, welcomeImages, html, onHtml,
  recipients, sampleLeadId, onSample, preview, previewError, previewing, request, testBlocked, testTo,
}: {
  setup: NewsletterSetup | null;
  language: NewsletterLanguage;
  onLanguage: (next: NewsletterLanguage) => void;
  subject: string;
  onSubject: (next: string) => void;
  mode: EmailMode;
  onMode: (next: EmailMode) => void;
  letter: Letter;
  onLetter: (next: Letter) => void;
  /** The welcome letter's images, offered in each image's picker. */
  welcomeImages?: LetterImages;
  /** The HTML of one's own, in HTML mode. */
  html: string;
  onHtml: (next: string) => void;
  /** Who the preview can be shown for, besides a sample person. */
  recipients: NewsletterPerson[];
  sampleLeadId: string | null;
  onSample: (leadId: string | null) => void;
  preview: NewsletterPreview | null;
  previewError: string | null;
  previewing: boolean;
  /** The email as it is now: what a test sends. */
  request: NewsletterEmailRequest;
  /** Why a test cannot go yet, or null. */
  testBlocked: string | null;
  /** The signed-in person's address: where the test goes unless another is typed. */
  testTo: string | null;
}) {
  const ids = useId();
  const [device, setDevice] = useState<'desktop' | 'mobile'>('desktop');
  // The box a field button adds to: the one last typed in.
  const lastField = useRef<string>(`${ids}-message`);
  const [testing, setTesting] = useState(false);
  // Where the test goes: the person's own address to start with, or anyone they type.
  const [testAddress, setTestAddress] = useState(testTo ?? '');
  useEffect(() => { setTestAddress((now) => now || testTo || ''); }, [testTo]);
  const [test, setTest] = useState<{ ok: true; result: NewsletterTestResult } | { ok: false; text: string } | null>(null);

  // A test said about an email since changed says nothing about this one.
  const requestKey = JSON.stringify(request);
  useEffect(() => { setTest(null); }, [requestKey]);

  const edit = (change: Partial<Letter>) => onLetter({ ...letter, ...change });
  const editImages = (change: Partial<LetterImages>) => edit({ images: { ...letter.images, ...change } });
  const setPhoto = (index: 0 | 1, src: string | null) => {
    const photos = [letter.images.photos[0] ?? null, letter.images.photos[1] ?? null];
    photos[index] = src;
    editImages({ photos: photos.filter((p): p is string => Boolean(p)) });
  };
  const library = useEmailImageLibrary();
  const suggestions = [welcomeImages?.cover, ...(welcomeImages?.photos ?? []), welcomeImages?.logo].filter((u): u is string => Boolean(u));
  const boxes: Record<string, { value: string; set: (value: string) => void }> = {
    [`${ids}-subject`]: { value: subject, set: onSubject },
    [`${ids}-html`]: { value: html, set: onHtml },
  };
  for (const key of TEXT_KEYS) boxes[`${ids}-${key}`] = { value: letter[key], set: (value) => edit({ [key]: value }) };

  /** One of the letter's words: its label and its box, which field buttons add to. */
  const text = (key: TextKey, label: string, o: { rows?: number; max?: number; className?: string; hint?: string } = {}) => {
    const id = `${ids}-${key}`;
    const common = { id, value: letter[key], onFocus: typingIn(id), 'aria-describedby': o.hint ? `${id}-hint` : undefined };
    return (
      <div className="min-w-0">
        <Label htmlFor={id}>{label}</Label>
        {o.rows ? (
          <Textarea {...common} onChange={(e) => edit({ [key]: e.target.value })} rows={o.rows} className={cn('mt-1 leading-relaxed', o.className)} />
        ) : (
          <Input {...common} onChange={(e) => edit({ [key]: e.target.value })} maxLength={o.max ?? 200} className={cn('mt-1', o.className)} />
        )}
        {o.hint && <p id={`${id}-hint`} className="mt-1 text-[11px] text-slate-400">{o.hint}</p>}
      </div>
    );
  };
  const typingIn = (id: string) => () => { lastField.current = id; };
  const addField = (token: string) => {
    const shown = (id: string) => Boolean(boxes[id] && document.getElementById(id));
    const id = shown(lastField.current) ? lastField.current : mode === 'html' ? `${ids}-html` : `${ids}-message`;
    const box = boxes[id];
    if (box) box.set(insertAt(id, box.value, token));
  };

  const sendTest = async () => {
    setTesting(true);
    setTest(null);
    try {
      setTest({ ok: true, result: await sendNewsletterTest({ ...request, to: testAddress.trim() || null }) });
    } catch (e) {
      setTest({ ok: false, text: e instanceof Error ? e.message : 'The test was not sent.' });
    } finally {
      setTesting(false);
    }
  };

  const fields = setup?.fields ?? [];
  const from = preview?.fromName ?? setup?.fromName;
  const replyTo = preview?.replyTo ?? setup?.replyTo;

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
      <div className="min-w-0 space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            <Languages className="h-3.5 w-3.5" aria-hidden /> <span className="sr-only">Language</span>
          </span>
          <Segmented label="Language" value={language} options={LANGUAGES} onChange={onLanguage} />
        </div>

        <div className="grid grid-cols-2 gap-1 rounded-xl bg-slate-100 p-1 dark:bg-white/5" role="radiogroup" aria-label="How the email is made">
          {MODES.map((m) => (
            <button
              key={m.id}
              type="button"
              role="radio"
              aria-checked={mode === m.id}
              onClick={() => onMode(m.id)}
              className={cn(
                'flex min-w-0 items-center gap-2 rounded-lg px-3 py-2 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
                mode === m.id ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white' : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200',
              )}
            >
              <m.icon className={cn('h-4 w-4 shrink-0', mode === m.id && 'text-orange-500')} aria-hidden />
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium">{m.label}</span>
                <span className="block truncate text-[11px] font-normal text-slate-400">{m.hint}</span>
              </span>
            </button>
          ))}
        </div>

        <div>
          <Label htmlFor={`${ids}-subject`}>Subject</Label>
          <Input
            id={`${ids}-subject`}
            value={subject}
            onChange={(e) => onSubject(e.target.value)}
            onFocus={typingIn(`${ids}-subject`)}
            maxLength={300}
            className="mt-1"
          />
        </div>

        {mode === 'letter' ? (
          <div className="space-y-3">
            <Section title="Heading" icon={Heading1}>
              {text('kicker', 'Small heading', { max: 60, className: 'uppercase tracking-[0.18em] text-xs text-orange-700 dark:text-orange-300' })}
              <div className="grid gap-3 sm:grid-cols-2">
                {text('title', 'Title', { max: 80, className: 'font-serif text-base' })}
                {text('accent', 'Then, in coral', { max: 80, className: 'font-serif text-base italic text-orange-700 dark:text-orange-300' })}
              </div>
            </Section>

            <Section title="Letter" icon={Feather} hint="**bold** · blank line: new paragraph">
              {text('heading', 'Greeting', { className: 'font-serif text-base italic' })}
              {text('message', 'Message', { rows: 6, className: 'min-h-[8rem]' })}
              {text('quote', 'Highlighted line', { rows: 2, className: 'font-serif italic', hint: 'Set between two lavender lines; a second line is in lavender. Empty: left out.' })}
            </Section>

            <Section title="Booking box" icon={CalendarCheck} hint="With their code, RDV-XXXX">
              {text('button', 'Button', { max: 80 })}
              <div className="grid gap-3 sm:grid-cols-2">
                {text('duration', 'Duration', { max: 60 })}
                {text('place', 'Place', { max: 120 })}
              </div>
            </Section>

            <Section title="Sign-off" icon={Signature}>
              <div className="grid gap-3 sm:grid-cols-2">
                {text('signoff', 'Sign-off', { className: 'font-serif italic' })}
                {text('signature', 'Signature', { className: 'font-serif text-base italic' })}
              </div>
            </Section>

            <Section title="Images" icon={Images} hint="From your welcome letter">
              <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
                <ImageSlot label="Cover" wide value={letter.images.cover} onChange={(cover) => editImages({ cover })} library={library} suggestions={suggestions} />
                <ImageSlot label="Photo 1" value={letter.images.photos[0]} onChange={(src) => setPhoto(0, src)} library={library} suggestions={suggestions} />
                <ImageSlot label="Photo 2" value={letter.images.photos[1]} onChange={(src) => setPhoto(1, src)} library={library} suggestions={suggestions} />
                <ImageSlot label="Logo" wide dark value={letter.images.logo} onChange={(logo) => editImages({ logo })} library={library} suggestions={suggestions} />
              </div>
              <label className="flex items-center gap-2.5 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700 dark:bg-white/5 dark:text-slate-200">
                <Switch checked={letter.steps} onCheckedChange={(steps) => edit({ steps })} aria-label="Show how it works" />
                <span>Show <em>how it works</em>: choose a day, then a time, we welcome you</span>
              </label>
            </Section>
          </div>
        ) : (
          <div className="overflow-hidden rounded-xl border border-slate-800 bg-slate-950 shadow-sm">
            <div className="flex items-center gap-2 border-b border-white/10 px-3 py-2">
              <Code2 className="h-3.5 w-3.5 text-slate-400" aria-hidden />
              <Label htmlFor={`${ids}-html`} className="font-mono text-xs font-medium text-slate-300">HTML</Label>
              <span className="ml-auto text-[11px] text-slate-400">{html.split('\n').length} lines</span>
            </div>
            <Textarea
              id={`${ids}-html`}
              value={html}
              onChange={(e) => onHtml(e.target.value)}
              onFocus={typingIn(`${ids}-html`)}
              rows={16}
              spellCheck={false}
              className="max-h-[28rem] min-h-[14rem] overflow-y-auto rounded-none border-0 bg-transparent font-mono text-xs leading-relaxed text-emerald-200 shadow-none focus-visible:ring-0 dark:bg-transparent"
            />
          </div>
        )}

        {fields.length > 0 && (
          <div className="rounded-xl border border-slate-200 p-3 dark:border-white/10">
            <p className="mb-2 text-xs font-medium text-slate-500 dark:text-slate-400">Add a field</p>
            <PlaceholderChips fields={fields} tone="orange" onPick={addField} />
          </div>
        )}
      </div>

      <div className="min-w-0 space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Preview</p>
          {recipients.length > 0 && (
            <Select value={sampleLeadId ?? SAMPLE} onValueChange={(v) => onSample(v === SAMPLE ? null : v)}>
              <SelectTrigger size="sm" className="h-8 w-auto max-w-[13rem] gap-1.5 text-xs" aria-label="Preview for">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {recipients.map((r) => <SelectItem key={r.leadId} value={r.leadId}>{r.name}</SelectItem>)}
                <SelectItem value={SAMPLE}>A sample person</SelectItem>
              </SelectContent>
            </Select>
          )}
          <Segmented
            className="ml-auto"
            label="Preview width"
            value={device}
            onChange={setDevice}
            options={[{ id: 'desktop', label: 'Desktop', icon: Monitor }, { id: 'mobile', label: 'Mobile', icon: Smartphone }]}
          />
        </div>

        {previewError ? (
          <ProblemNote>{previewError}</ProblemNote>
        ) : preview ? (
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
            <div className="space-y-1 border-b border-slate-100 bg-slate-50/80 px-4 py-3 text-xs dark:border-slate-700 dark:bg-slate-800/60">
              {from && <Envelope label="From">{from}</Envelope>}
              <Envelope label="To">{preview.sampleEmail ? `${preview.sampleName} <${preview.sampleEmail}>` : preview.sampleName}</Envelope>
              {replyTo && <Envelope label="Reply to">{replyTo}</Envelope>}
              <div className="flex flex-wrap items-center gap-2 pt-0.5">
                <p className="flex min-w-0 items-center gap-1.5 text-sm font-semibold text-slate-800 dark:text-slate-100">
                  <Mail className="h-3.5 w-3.5 shrink-0 text-orange-500" aria-hidden />
                  <span className="truncate">{preview.subject || <span className="font-normal italic text-slate-400">No subject</span>}</span>
                </p>
                <span className="ml-auto inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 font-mono text-[11px] font-semibold text-slate-700 ring-1 ring-slate-200 dark:bg-white/5 dark:text-slate-200 dark:ring-white/10" title="Their booking code">
                  <KeyRound className="h-3 w-3" aria-hidden /> {preview.bookingCode}
                </span>
              </div>
              {previewing && <Loader2 className="float-right -mt-5 h-3.5 w-3.5 animate-spin text-slate-400" aria-label="Updating the preview" />}
            </div>
            {(preview.unknown.length > 0 || preview.bookingProblem) && (
              <div role="alert" className="flex items-start gap-2 border-b border-amber-200 bg-amber-50 px-4 py-2.5 text-xs text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
                <TriangleAlert className="mt-px h-3.5 w-3.5 shrink-0 text-amber-600" aria-hidden />
                <span>
                  {preview.unknown.length > 0 ? `Nothing fills ${preview.unknown.join(', ')}.` : preview.bookingProblem}
                </span>
              </div>
            )}
            <PreviewFrame html={preview.html} device={device} />
          </div>
        ) : (
          <div className="flex h-72 items-center justify-center rounded-xl border border-dashed border-slate-200 text-slate-400 dark:border-white/10">
            <Loader2 className="h-5 w-5 animate-spin" aria-label="Loading the preview" />
          </div>
        )}

        <form
          className="rounded-xl border border-slate-200 bg-slate-50/70 p-3 dark:border-white/10 dark:bg-white/[0.03]"
          onSubmit={(e) => { e.preventDefault(); void sendTest(); }}
        >
          <Label htmlFor={`${ids}-test-to`} className="flex items-center gap-1.5">
            <Send className="h-3.5 w-3.5 text-orange-500" aria-hidden /> Send a test to
          </Label>
          <div className="mt-1.5 flex gap-2">
            <div className="min-w-0 flex-1">
              <Input
                id={`${ids}-test-to`}
                type="email"
                value={testAddress}
                onChange={(e) => setTestAddress(e.target.value)}
                placeholder={testTo ?? 'name@example.com'}
                autoComplete="email"
                aria-describedby={`${ids}-test-hint`}
                className="bg-white dark:bg-white/5"
              />
            </div>
            <Button type="submit" disabled={testing || Boolean(testBlocked)} className="shrink-0">
              {testing ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Send className="h-4 w-4" aria-hidden />} Send test
            </Button>
          </div>
          <p id={`${ids}-test-hint`} className="mt-1.5 text-xs text-slate-500 dark:text-slate-400">
            {testBlocked ?? 'To you or anyone you type, marked as a test. Its booking link books nothing.'}
          </p>
          {test && (
            test.ok ? (
              <p role="status" className="mt-2 flex items-start gap-1.5 text-sm text-emerald-700 dark:text-emerald-300">
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
                {test.result.status === 'queued' ? `Queued for ${test.result.to}: it goes in a minute.` : `Sent to ${test.result.to}.`}
              </p>
            ) : (
              <ProblemNote className="mt-2">{test.text}</ProblemNote>
            )
          )}
        </form>
      </div>
    </div>
  );
}

function Envelope({ label, children }: { label: string; children: string }) {
  return (
    <p className="truncate text-slate-500 dark:text-slate-400">
      <span className="inline-block w-16 font-medium text-slate-400 dark:text-slate-500">{label}</span>
      {children}
    </p>
  );
}

function Section({ title, icon: Icon, hint, children }: {
  title: string;
  icon: ComponentType<{ className?: string }>;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white/70 p-3.5 dark:border-white/10 dark:bg-white/[0.02]">
      <h4 className="mb-2.5 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
        <Icon className="h-3.5 w-3.5 text-orange-500" aria-hidden /> {title}
        {hint && <span className="ml-auto truncate pl-2 text-[11px] font-normal normal-case tracking-normal text-slate-400">{hint}</span>}
      </h4>
      <div className="space-y-3">{children}</div>
    </section>
  );
}
