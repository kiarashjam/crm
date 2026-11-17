import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertTriangle, BookmarkPlus, Check, Eye, FileText, Loader2, Mail, PenLine, Search, Send, Sparkles, X,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog, DialogContent, DialogDescription, DialogTitle,
} from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Label } from '@/app/components/ui/label';
import { cn } from '@/app/components/ui/utils';
import { sendEmail, cancelEmail, createActivity, generateCopy, servesDemoData, mailtoHref } from '@/app/api';
import { ApiError } from '@/app/api/apiClient';
import {
  createEmailTemplate, getEmailTemplates, EMAIL_LANGUAGES,
  type EmailLanguage, type EmailTemplate, type EmailTemplateList,
} from '@/app/api/emailTemplates';
import { getEmailSending, type EmailSending } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { getCurrentUser } from '@/app/lib/auth';
import {
  PLACEHOLDER_LABELS, blanksIn, fillPlaceholders, mergeValues, splitBlanks, unknownPlaceholders,
} from './email/mergeText';

/**
 * Refusals that are the answer, not a fault to work around in the person's own email
 * app. A placeholder with nothing to fill it is one too: the email app gets the words as
 * typed, and would send "Hi {{firstName}}," to the person.
 */
const REFUSALS = new Set(['Contact.DoNotContact', 'Email.Suppressed', 'Email.MergeField']);

/** The language this person last wrote in, so the composer opens in it. */
const LANGUAGE_KEY = 'cadence.email.language';
function rememberedLanguage(): EmailLanguage | null {
  try {
    const v = localStorage.getItem(LANGUAGE_KEY);
    return v === 'fr' || v === 'en' ? v : null;
  } catch {
    return null;
  }
}
function rememberLanguage(language: EmailLanguage) {
  try { localStorage.setItem(LANGUAGE_KEY, language); } catch { /* a private window keeps nothing */ }
}

export interface EmailComposerContext {
  leadId?: string;
  contactId?: string;
  dealId?: string;
}

export interface EmailComposerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Default recipient (editable). */
  to: string;
  /** Who it is to, for the preview: their first name, last name and full name. */
  recipientName?: string;
  /** Their company, for the preview's {{company}}. */
  company?: string;
  defaultSubject?: string;
  defaultBody?: string;
  /** Associates the sent message + logged activity with a record. */
  context?: EmailComposerContext;
  /** Extra context for the AI draft (e.g. "Acme Corp · enterprise lead"). */
  aiContext?: string;
  /** Called after a successful send (e.g. to refresh a timeline). */
  onSent?: () => void;
}

const languageName = (code: EmailLanguage) => (code === 'fr' ? 'French' : 'English');

/** Put text at the cursor of the field with this id, or at its end. */
function insertAtCursor(id: string, current: string, token: string, set: (next: string) => void) {
  const el = typeof document !== 'undefined' ? (document.getElementById(id) as HTMLTextAreaElement | HTMLInputElement | null) : null;
  if (!el || el.selectionStart == null) { set(current + token); return; }
  const start = el.selectionStart;
  const end = el.selectionEnd ?? start;
  set(current.slice(0, start) + token + current.slice(end));
  requestAnimationFrame(() => {
    el.focus();
    el.setSelectionRange(start + token.length, start + token.length);
  });
}

/**
 * The email composer: start from one of the workspace's prepared emails in French or
 * English, or from nothing, see it as the person will read it, and send it from the
 * workspace through the server's outbox, which writes it on the record's timeline once
 * it has gone. Demo mode simulates, and logs the `email` activity here.
 *
 * It says "Email sent" only when the server sent it. When the mail server did not take
 * it yet, it says the email is queued and offers to cancel it. When a send fails, the
 * toast gives the server's reason and offers to open the message in the person's own
 * email app instead — except when the recipient asked not to be emailed, their address
 * bounced, or a placeholder had nothing to fill it.
 */
export default function EmailComposerDialog({
  open, onOpenChange, to, recipientName, company, defaultSubject = '', defaultBody = '', context, aiContext, onSent,
}: EmailComposerDialogProps) {
  const org = useOrgOptional()?.currentOrg ?? null;
  const me = getCurrentUser();
  const [toAddr, setToAddr] = useState(to);
  const [cc, setCc] = useState('');
  const [showCc, setShowCc] = useState(false);
  const [subject, setSubject] = useState(defaultSubject);
  const [body, setBody] = useState(defaultBody);
  const [sending, setSending] = useState(false);
  const [drafting, setDrafting] = useState(false);

  const [list, setList] = useState<EmailTemplateList | null>(null);
  const [listFailed, setListFailed] = useState(false);
  const [sender, setSender] = useState<EmailSending | null>(null);
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [language, setLanguage] = useState<EmailLanguage>(() => rememberedLanguage() ?? 'en');
  /** What the chosen prepared email (or the opening text) put in, to tell whether the person has changed it. */
  const [loaded, setLoaded] = useState({ subject: defaultSubject, body: defaultBody });
  /** A change of prepared email or language that would replace what the person wrote, waiting for a yes. */
  const [pending, setPending] = useState<{ templateId: string | null; language: EmailLanguage } | null>(null);
  const [view, setView] = useState<'write' | 'preview'>('write');
  const [search, setSearch] = useState('');
  const [saveAs, setSaveAs] = useState<{ name: string; category: string } | null>(null);
  const [savingTemplate, setSavingTemplate] = useState(false);
  const languageChosen = useRef(rememberedLanguage() !== null);

  // Re-seed the fields each time the dialog is opened for a new context.
  useEffect(() => {
    if (!open) return;
    setToAddr(to);
    setSubject(defaultSubject);
    setBody(defaultBody);
    setLoaded({ subject: defaultSubject, body: defaultBody });
    setCc('');
    setShowCc(false);
    setTemplateId(null);
    setPending(null);
    setView('write');
    setSearch('');
    setSaveAs(null);
    let live = true;
    setListFailed(false);
    getEmailTemplates()
      .then((l) => {
        if (!live) return;
        setList(l);
        if (!languageChosen.current) setLanguage(l.defaultLanguage);
      })
      .catch(() => { if (live) setListFailed(true); });
    if (org?.id) getEmailSending(org.id).then((s) => { if (live) setSender(s); }).catch(() => { /* the line says the workspace's name */ });
    return () => { live = false; };
  }, [open, to, defaultSubject, defaultBody, org?.id]);

  const templates = useMemo(() => list?.templates ?? [], [list]);
  const chosen = templates.find((t) => t.id === templateId) ?? null;
  const chosenVersion = chosen?.versions.find((v) => v.language === language) ?? null;
  const known = list?.placeholders ?? Object.keys(PLACEHOLDER_LABELS);
  const changed = subject !== loaded.subject || body !== loaded.body;

  /** Put a prepared email (or a blank one) in, in a language. */
  const apply = (id: string | null, lang: EmailLanguage) => {
    setPending(null);
    setTemplateId(id);
    setLanguage(lang);
    rememberLanguage(lang);
    languageChosen.current = true;
    if (id === null) {
      setSubject(defaultSubject);
      setBody(defaultBody);
      setLoaded({ subject: defaultSubject, body: defaultBody });
      return;
    }
    const version = templates.find((t) => t.id === id)?.versions.find((v) => v.language === lang);
    if (version) {
      setSubject(version.subject);
      setBody(version.body);
      setLoaded({ subject: version.subject, body: version.body });
    }
  };

  /** Ask first when it would replace what the person has written. */
  const choose = (id: string | null, lang: EmailLanguage) => {
    if (id === templateId && lang === language) return;
    // Writing one's own: the language is only remembered, nothing is replaced.
    if (id === null && templateId === null) {
      setLanguage(lang);
      rememberLanguage(lang);
      languageChosen.current = true;
      return;
    }
    const replaces = Boolean(templates.find((t) => t.id === id)?.versions.some((v) => v.language === lang)) || id === null;
    if (changed && replaces && (subject.trim() || body.trim())) setPending({ templateId: id, language: lang });
    else apply(id, lang);
  };

  const values = useMemo(
    () => mergeValues({ name: recipientName, email: toAddr, company }, me?.name, org?.name),
    [recipientName, toAddr, company, me?.name, org?.name],
  );
  const preview = useMemo(() => {
    const s = fillPlaceholders(subject, values);
    const b = fillPlaceholders(body, values);
    return { subject: s.text, body: b.text, unfilled: [...new Set([...s.unfilled, ...b.unfilled])] };
  }, [subject, body, values]);
  const blanks = useMemo(() => blanksIn(`${subject}\n${body}`), [subject, body]);
  const unknown = useMemo(() => unknownPlaceholders(`${subject}\n${body}`, known), [subject, body, known]);

  const groups = useMemo(() => {
    const q = search.trim().toLowerCase();
    const shown = templates.filter((t) => !q || `${t.name} ${t.category ?? ''} ${t.description ?? ''} ${t.versions.map((v) => v.subject).join(' ')}`.toLowerCase().includes(q));
    const byCategory = new Map<string, EmailTemplate[]>();
    for (const t of shown) {
      const key = t.category?.trim() || 'Other';
      byCategory.set(key, [...(byCategory.get(key) ?? []), t]);
    }
    return [...byCategory.entries()];
  }, [templates, search]);
  const categories = useMemo(() => [...new Set(templates.map((t) => t.category?.trim()).filter(Boolean) as string[])].sort(), [templates]);

  const fromLine = (() => {
    const name = org?.name ?? 'Your workspace';
    if (servesDemoData()) return { who: name, note: 'Demo mode: sending is simulated, and it is noted on the timeline.' };
    const address = sender?.verified && sender.sendingAddress ? sender.sendingAddress : sender?.sharedAddress;
    const replies = me?.email ? `Replies come to you at ${me.email}` : 'Replies come to you';
    const captured = sender?.repliesCaptured ? ' and onto the timeline' : '';
    const note = sender?.sendingAddress && !sender.verified
      ? `${replies}${captured}. Your own address, ${sender.sendingAddress}, is used once its domain is verified.`
      : `${replies}${captured}.`;
    return { who: address ? `${name} <${address}>` : name, note };
  })();

  const aiDraft = async () => {
    setDrafting(true);
    try {
      const goal = subject.trim()
        ? `Write a concise, friendly email about: ${subject.trim()}`
        : 'Write a concise, friendly email to start a conversation';
      const copy = await generateCopy({
        copyTypeId: 'sales-email',
        goal: `${goal}. Write it in ${languageName(language)}.`,
        context: aiContext || `Recipient: ${recipientName ? `${recipientName} <${toAddr}>` : toAddr}`,
        length: 'medium',
      });
      if (copy) { setBody(copy); toast.success('Draft generated'); }
      else toast.error('Could not generate a draft');
    } catch {
      toast.error('Could not generate a draft');
    } finally {
      setDrafting(false);
    }
  };

  /** What stops it going: left to fill in, or a placeholder nothing fills. */
  const problem = (): string | null => {
    if (!toAddr.trim()) return 'A recipient is required';
    if (!subject.trim()) return 'A subject is required';
    if (blanks.length > 0) return `Fill in before sending: ${blanks.join(', ')}`;
    if (unknown.length > 0) return `${unknown.join(', ')} is not something Cadence can fill in. Use ${known.map((k) => `{{${k}}}`).join(', ')}.`;
    return null;
  };

  const handleSend = async () => {
    const stop = problem();
    if (stop) { toast.error(stop); return; }
    setSending(true);
    try {
      const sent = await sendEmail({
        to: toAddr.trim(),
        cc: cc.trim() || undefined,
        subject: subject.trim(),
        body,
        leadId: context?.leadId,
        contactId: context?.contactId,
        dealId: context?.dealId,
        language,
      });
      if (!sent) { toast.error('Failed to send email'); return; }
      if (servesDemoData()) {
        // Demo mode has no server to keep the timeline, so it is mirrored here.
        createActivity({
          type: 'email',
          subject: `Email: ${subject.trim()}`,
          body,
          leadId: context?.leadId,
          contactId: context?.contactId,
          dealId: context?.dealId,
        }).catch(() => { /* non-fatal */ });
        toast.success('Email sent (simulated in demo mode)');
      } else if (sent.status === 'queued') {
        // Not sent yet, and not lost: the server tries again, and puts it on the
        // timeline once it has gone. No email-app way out here, which would send it twice.
        const queuedId = sent.id;
        toast.info('Not sent yet: the mail server did not take it. Cadence keeps trying, and it shows on the timeline once it has gone.', {
          duration: 15_000,
          action: {
            label: 'Cancel sending',
            onClick: () => {
              cancelEmail(queuedId)
                .then(() => toast.success('Cancelled. It will not be sent.'))
                .catch((error: unknown) => toast.error(error instanceof Error && error.message ? error.message : 'It could not be cancelled.'));
            },
          },
        });
      } else {
        // The server writes it on the timeline; writing it here as well showed it twice.
        toast.success('Email sent');
      }
      onOpenChange(false);
      onSent?.();
    } catch (err) {
      const reason = err instanceof Error && err.message ? err.message : 'Failed to send email';
      // A "do not contact" or bounced-address refusal is the answer, not a fault to
      // work around: the email-app button sent the same message from the rep's own
      // mailbox and noted it on the timeline.
      if (err instanceof ApiError && err.code && REFUSALS.has(err.code)) {
        toast.error(reason, { duration: 12_000 });
        return;
      }
      toast.error(reason, {
        duration: 12_000,
        action: { label: 'Open in email app', onClick: handleOpenInMailApp },
      });
    } finally {
      setSending(false);
    }
  };

  /** Hand the message to the person's own email app, and note it on the timeline. */
  const handleOpenInMailApp = () => {
    if (!toAddr.trim()) { toast.error('A recipient is required'); return; }
    if (!subject.trim()) { toast.error('A subject is required'); return; }
    // The email app gets the words as they are: the placeholders filled here, as for the preview.
    const filledSubject = fillPlaceholders(subject.trim(), values).text;
    const filledBody = fillPlaceholders(body, values).text;
    window.location.href = mailtoHref({ to: toAddr.trim(), cc: cc.trim() || undefined, subject: filledSubject, body: filledBody });
    createActivity({
      type: 'email',
      subject: `Email: ${filledSubject}`,
      body: filledBody,
      leadId: context?.leadId,
      contactId: context?.contactId,
      dealId: context?.dealId,
    }).catch(() => { /* non-fatal */ });
    toast.success('Opened in your email app. Send it from there — it is noted on the timeline.');
    onOpenChange(false);
    onSent?.();
  };

  const saveTemplate = async () => {
    if (!saveAs) return;
    if (!saveAs.name.trim()) { toast.error('Give the prepared email a name'); return; }
    if (!subject.trim() || !body.trim()) { toast.error('Write a subject and some text first'); return; }
    setSavingTemplate(true);
    try {
      const made = await createEmailTemplate({
        name: saveAs.name.trim(),
        category: saveAs.category.trim() || null,
        versions: [{ language, subject: subject.trim(), body }],
      });
      setList((l) => (l ? { ...l, templates: [...l.templates, made] } : l));
      setTemplateId(made.id);
      setLoaded({ subject: made.versions[0]?.subject ?? subject, body: made.versions[0]?.body ?? body });
      setSaveAs(null);
      toast.success(`Saved as "${made.name}" (${languageName(language)}). Add the ${languageName(language === 'fr' ? 'en' : 'fr')} version in Prepared emails.`);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'It could not be saved');
    } finally {
      setSavingTemplate(false);
    }
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if ((e.metaKey || e.ctrlKey) && e.key === 'Enter' && !sending) {
      e.preventDefault();
      void handleSend();
    }
  };

  const pendingTemplate = pending ? templates.find((t) => t.id === pending.templateId) : null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[92vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-5xl" onKeyDown={onKeyDown}>
        {/* Header */}
        <div className="flex items-center gap-3 border-b border-slate-200 bg-gradient-to-r from-indigo-600 via-violet-600 to-fuchsia-500 px-5 py-4 text-white">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/20 ring-1 ring-white/30">
            <Mail className="h-5 w-5" />
          </div>
          <div className="min-w-0">
            <DialogTitle className="text-lg font-semibold text-white">
              {recipientName ? `Email ${recipientName}` : 'New email'}
            </DialogTitle>
            <DialogDescription className="truncate text-sm text-white/80">
              Start from a prepared email or write your own. It goes out from your workspace and is noted on the timeline.
            </DialogDescription>
          </div>
        </div>

        <div className="grid min-h-0 flex-1 grid-cols-1 sm:grid-cols-[17rem_1fr]">
          {/* Prepared emails */}
          <aside aria-label="Prepared emails" className="hidden min-h-0 flex-col border-r border-slate-200 bg-slate-50/70 sm:flex">
            <div className="p-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Find a prepared email"
                  aria-label="Find a prepared email"
                  className="h-9 bg-white pl-8"
                />
              </div>
            </div>
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto px-2 pb-3">
              <button
                type="button"
                onClick={() => choose(null, language)}
                aria-pressed={templateId === null}
                className={cn(
                  'flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition',
                  templateId === null ? 'bg-white font-medium text-indigo-700 shadow-sm ring-1 ring-indigo-200' : 'text-slate-700 hover:bg-white',
                )}
              >
                <PenLine className="h-4 w-4 shrink-0" /> Write your own
              </button>
              {!list && !listFailed && (
                <p className="flex items-center gap-2 px-2.5 text-xs text-slate-500"><Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading prepared emails…</p>
              )}
              {listFailed && <p className="px-2.5 text-xs text-slate-500">The prepared emails could not be loaded. You can still write your own.</p>}
              {list && groups.length === 0 && (
                <p className="px-2.5 text-xs text-slate-500">{search ? 'None matches.' : 'No prepared emails yet.'}</p>
              )}
              {groups.map(([category, items]) => (
                <div key={category}>
                  <p className="px-2.5 pb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-400">{category}</p>
                  <ul className="space-y-0.5">
                    {items.map((t) => {
                      const selected = t.id === templateId;
                      const langs = t.versions.map((v) => v.language);
                      return (
                        <li key={t.id}>
                          <button
                            type="button"
                            onClick={() => choose(t.id, langs.includes(language) ? language : langs[0] ?? language)}
                            aria-pressed={selected}
                            aria-label={`${t.name}${EMAIL_LANGUAGES.filter((l) => !langs.includes(l.code)).map((l) => `, no ${languageName(l.code)} version`).join('')}`}
                            title={t.description ?? undefined}
                            className={cn(
                              'w-full rounded-lg px-2.5 py-2 text-left transition',
                              selected ? 'bg-white shadow-sm ring-1 ring-indigo-200' : 'hover:bg-white',
                            )}
                          >
                            <span className="flex items-start gap-2">
                              <FileText className={cn('mt-0.5 h-4 w-4 shrink-0', selected ? 'text-indigo-600' : 'text-slate-400')} />
                              <span className={cn('line-clamp-2 min-w-0 flex-1 text-sm', selected ? 'font-medium text-indigo-700' : 'text-slate-700')}>{t.name}</span>
                              <span className="flex gap-0.5" aria-hidden>
                                {EMAIL_LANGUAGES.map((l) => (
                                  <span
                                    key={l.code}
                                    className={cn(
                                      'rounded px-1 text-[10px] font-semibold',
                                      langs.includes(l.code) ? 'bg-slate-200 text-slate-600' : 'bg-transparent text-slate-300 line-through',
                                    )}
                                  >
                                    {l.short}
                                  </span>
                                ))}
                              </span>
                            </span>
                            {t.description && <span className="mt-0.5 block truncate pl-6 text-xs text-slate-500">{t.description}</span>}
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              ))}
            </div>
            <div className="border-t border-slate-200 p-3">
              <Link
                to="/templates?tab=email"
                onClick={() => onOpenChange(false)}
                className="text-xs font-medium text-indigo-700 hover:underline"
              >
                Manage prepared emails
              </Link>
            </div>
          </aside>

          {/* The email */}
          <div className="flex min-h-0 flex-col">
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
              {/* On a phone, the prepared emails as a list to pick from. */}
              <div className="sm:hidden">
                <Label htmlFor="email-template">Prepared email</Label>
                <select
                  id="email-template"
                  value={templateId ?? ''}
                  onChange={(e) => {
                    const id = e.target.value || null;
                    const langs = templates.find((t) => t.id === id)?.versions.map((v) => v.language) ?? [];
                    choose(id, id && !langs.includes(language) ? langs[0] ?? language : language);
                  }}
                  className="mt-1 h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="">Write your own</option>
                  {templates.map((t) => <option key={t.id} value={t.id}>{t.category ? `${t.category} · ` : ''}{t.name}</option>)}
                </select>
              </div>

              <div className="rounded-lg border border-slate-200 bg-slate-50/60 px-3 py-2 text-sm">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  <span className="w-10 shrink-0 text-xs font-medium uppercase tracking-wide text-slate-400">From</span>
                  <span className="font-medium text-slate-800" data-testid="email-from">{fromLine.who}</span>
                </div>
                <p className="mt-0.5 pl-12 text-xs text-slate-500">{fromLine.note}</p>
              </div>

              <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
                <div>
                  <div className="flex items-center justify-between">
                    <Label htmlFor="email-to">To</Label>
                    {!showCc && (
                      <button type="button" onClick={() => setShowCc(true)} className="text-xs font-medium text-indigo-600 hover:text-indigo-800">
                        Add Cc
                      </button>
                    )}
                  </div>
                  <Input id="email-to" type="email" value={toAddr} onChange={(e) => setToAddr(e.target.value)} placeholder="recipient@example.com" className="mt-1" />
                </div>
                <div>
                  <span className="mb-1 block text-sm font-medium" id="email-language-label">Language</span>
                  <div role="radiogroup" aria-labelledby="email-language-label" className="inline-flex rounded-lg border border-slate-200 bg-white p-0.5">
                    {EMAIL_LANGUAGES.map((l) => (
                      <button
                        key={l.code}
                        type="button"
                        role="radio"
                        aria-checked={language === l.code}
                        onClick={() => choose(templateId, l.code)}
                        className={cn(
                          'rounded-md px-3 py-1.5 text-sm font-medium transition',
                          language === l.code ? 'bg-slate-900 text-white shadow' : 'text-slate-500 hover:text-slate-800',
                        )}
                      >
                        {l.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
              {showCc && (
                <div>
                  <Label htmlFor="email-cc">Cc</Label>
                  <Input id="email-cc" type="email" value={cc} onChange={(e) => setCc(e.target.value)} placeholder="cc@example.com" className="mt-1" />
                </div>
              )}

              {pending && (
                <div role="alert" className="flex flex-wrap items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  <span className="flex-1">
                    {pending.templateId === null
                      ? 'Start again from a blank email? What you wrote will be replaced.'
                      : `Replace what you wrote with “${pendingTemplate?.name ?? 'the prepared email'}” in ${languageName(pending.language)}?`}
                  </span>
                  <Button type="button" size="sm" variant="outline" onClick={() => setPending(null)}>Keep my text</Button>
                  <Button type="button" size="sm" onClick={() => apply(pending.templateId, pending.language)}>Replace</Button>
                </div>
              )}
              {chosen && !chosenVersion && (
                <div className="flex flex-wrap items-center gap-2 rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900">
                  <span className="flex-1">“{chosen.name}” has no {languageName(language)} version yet. The text below is unchanged.</span>
                  {chosen.versions[0] && (
                    <Button type="button" size="sm" variant="outline" onClick={() => choose(chosen.id, chosen.versions[0]!.language)}>
                      Use the {languageName(chosen.versions[0].language)} one
                    </Button>
                  )}
                </div>
              )}

              <div>
                <Label htmlFor="email-subject">Subject</Label>
                <Input id="email-subject" value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="Subject" className="mt-1" />
              </div>

              <div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div role="tablist" aria-label="Write or preview" className="inline-flex rounded-lg bg-slate-100 p-0.5 text-sm">
                    <button
                      type="button" role="tab" aria-selected={view === 'write'} onClick={() => setView('write')}
                      className={cn('inline-flex items-center gap-1.5 rounded-md px-3 py-1', view === 'write' ? 'bg-white font-medium shadow-sm' : 'text-slate-500')}
                    >
                      <PenLine className="h-3.5 w-3.5" /> Write
                    </button>
                    <button
                      type="button" role="tab" aria-selected={view === 'preview'} onClick={() => setView('preview')}
                      className={cn('inline-flex items-center gap-1.5 rounded-md px-3 py-1', view === 'preview' ? 'bg-white font-medium shadow-sm' : 'text-slate-500')}
                    >
                      <Eye className="h-3.5 w-3.5" /> Preview
                    </button>
                  </div>
                  <button
                    type="button"
                    onClick={aiDraft}
                    disabled={drafting}
                    className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:opacity-60"
                  >
                    {drafting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
                    {drafting ? 'Drafting…' : 'AI draft'}
                  </button>
                </div>

                {view === 'write' ? (
                  <>
                    <Label htmlFor="email-body" className="sr-only">Message</Label>
                    <Textarea
                      id="email-body"
                      value={body}
                      onChange={(e) => setBody(e.target.value)}
                      rows={12}
                      placeholder="Write your message…"
                      className="mt-2 min-h-[16rem] resize-y font-[inherit] leading-relaxed"
                    />
                    <div className="mt-2 flex flex-wrap items-center gap-1">
                      <span className="mr-1 text-xs text-slate-500">Insert:</span>
                      {known.map((k) => (
                        <button
                          key={k}
                          type="button"
                          onClick={() => insertAtCursor('email-body', body, `{{${k}}}`, setBody)}
                          title={`{{${k}}}: filled in for each person when it is sent`}
                          className="rounded-md border border-indigo-200 bg-indigo-50 px-1.5 py-0.5 text-[11px] font-medium text-indigo-700 transition hover:-translate-y-px hover:bg-indigo-100"
                        >
                          {PLACEHOLDER_LABELS[k] ?? k}
                        </button>
                      ))}
                      <button
                        type="button"
                        onClick={() => insertAtCursor('email-body', body, '[[…]]', setBody)}
                        title="A blank you fill in before sending: the email is not sent while one is left"
                        className="rounded-md border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-800 transition hover:-translate-y-px hover:bg-amber-100"
                      >
                        Blank to fill in
                      </button>
                    </div>
                  </>
                ) : (
                  <div className="mt-2 rounded-lg border border-slate-200 bg-white p-4" aria-label="Preview">
                    <p className="border-b border-slate-100 pb-2 text-sm font-semibold text-slate-900">
                      {splitBlanks(preview.subject).map((p, i) => (p.blank
                        ? <mark key={i} className="rounded bg-amber-100 px-1 text-amber-900">{p.text}</mark>
                        : <span key={i}>{p.text}</span>))}
                    </p>
                    <div className="whitespace-pre-wrap pt-3 text-sm leading-relaxed text-slate-800">
                      {splitBlanks(preview.body).map((p, i) => (p.blank
                        ? <mark key={i} className="rounded bg-amber-100 px-1 text-amber-900">{p.text}</mark>
                        : <span key={i}>{p.text}</span>))}
                    </div>
                    <p className="mt-3 border-t border-slate-100 pt-2 text-xs text-slate-500">
                      As {recipientName || toAddr || 'they'} will read it. Your workspace's footer is added when it is sent.
                      {preview.unfilled.length > 0 && ` ${preview.unfilled.join(', ')} is filled in from their record when it is sent, or the email is held back if it has nothing.`}
                    </p>
                  </div>
                )}
              </div>

              {(blanks.length > 0 || unknown.length > 0) && (
                <div className="space-y-1 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900" data-testid="email-left-to-fill">
                  {blanks.length > 0 && (
                    <p className="flex flex-wrap items-center gap-1">
                      <AlertTriangle className="h-4 w-4" /> Fill in before sending:
                      {blanks.map((b) => <span key={b} className="rounded bg-amber-100 px-1.5 font-medium">{b}</span>)}
                    </p>
                  )}
                  {unknown.length > 0 && (
                    <p>{unknown.join(', ')} is not something Cadence can fill in.</p>
                  )}
                </div>
              )}

              {saveAs && (
                <div className="space-y-2 rounded-lg border border-indigo-200 bg-indigo-50/60 p-3">
                  <p className="text-sm font-medium text-indigo-900">Save this as a prepared email ({languageName(language)})</p>
                  <div className="grid gap-2 sm:grid-cols-2">
                    <div>
                      <Label htmlFor="save-template-name" className="text-xs">Name</Label>
                      <Input id="save-template-name" value={saveAs.name} onChange={(e) => setSaveAs({ ...saveAs, name: e.target.value })} className="mt-1 h-9 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="save-template-category" className="text-xs">Category</Label>
                      <Input id="save-template-category" list="email-template-categories" value={saveAs.category} onChange={(e) => setSaveAs({ ...saveAs, category: e.target.value })} placeholder="e.g. Follow-up" className="mt-1 h-9 bg-white" />
                      <datalist id="email-template-categories">
                        {categories.map((c) => <option key={c} value={c} />)}
                      </datalist>
                    </div>
                  </div>
                  <div className="flex justify-end gap-2">
                    <Button type="button" size="sm" variant="outline" onClick={() => setSaveAs(null)}><X className="h-3.5 w-3.5" /> Cancel</Button>
                    <Button type="button" size="sm" onClick={saveTemplate} disabled={savingTemplate}>
                      {savingTemplate ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />} Save
                    </Button>
                  </div>
                </div>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-2 border-t border-slate-200 bg-white px-5 py-3">
              {!saveAs && !servesDemoData() && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setSaveAs({ name: chosen && changed ? `${chosen.name} (my version)` : subject.replace(/\{\{[^}]*\}\}|\[\[[^\]]*\]\]/g, '').trim().slice(0, 80), category: chosen?.category ?? '' })}
                  className="gap-1.5 text-slate-600"
                >
                  <BookmarkPlus className="h-4 w-4" /> Save as prepared email
                </Button>
              )}
              <span className="ml-auto hidden text-xs text-slate-400 sm:inline">Ctrl + Enter to send</span>
              {/* Kept together, on the right, when the bar wraps on a phone. */}
              <div className="ml-auto flex gap-2 sm:ml-0">
                <Button variant="outline" onClick={() => onOpenChange(false)} disabled={sending}>Cancel</Button>
                <Button onClick={handleSend} disabled={sending} className="gap-1.5">
                  {sending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                  {sending ? 'Sending…' : 'Send'}
                </Button>
              </div>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
