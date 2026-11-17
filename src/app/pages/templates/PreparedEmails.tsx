import { useEffect, useMemo, useState } from 'react';
import { Copy, Eye, FileText, Globe2, Languages, Loader2, Mail, PenLine, Plus, Search, Sparkles, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import { WriteOnly } from '@/app/components/WriteOnly';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { getCurrentUser } from '@/app/lib/auth';
import {
  EMAIL_LANGUAGES, createEmailTemplate, deleteEmailTemplate, getEmailTemplates, updateEmailTemplate,
  type EmailLanguage, type EmailTemplate, type EmailTemplateList, type SaveEmailTemplate,
} from '@/app/api/emailTemplates';
import {
  PLACEHOLDER_LABELS, blanksIn, fillPlaceholders, mergeValues, splitBlanks, unknownPlaceholders,
} from '@/app/components/email/mergeText';

type Draft = {
  id: string | null;
  name: string;
  category: string;
  description: string;
  versions: Record<EmailLanguage, { subject: string; body: string }>;
};

const emptyDraft = (): Draft => ({
  id: null, name: '', category: '', description: '',
  versions: { fr: { subject: '', body: '' }, en: { subject: '', body: '' } },
});

function draftOf(t: EmailTemplate, copy = false): Draft {
  const d = emptyDraft();
  for (const v of t.versions) d.versions[v.language] = { subject: v.subject, body: v.body };
  return { ...d, id: copy ? null : t.id, name: copy ? `${t.name} (copy)` : t.name, category: t.category ?? '', description: t.description ?? '' };
}

/** Someone to show a prepared email as they would read it. */
const SAMPLE = { name: 'Camille Dubois', email: 'camille.dubois@example.ch', company: 'Dubois & Fils' };

/**
 * The workspace's prepared emails, in French and English side by side: what the composer
 * offers when someone emails a lead, a contact or a deal's contact.
 */
export default function PreparedEmails({ tabs }: { tabs: React.ReactNode }) {
  const org = useOrgOptional()?.currentOrg ?? null;
  const me = getCurrentUser();
  const [list, setList] = useState<EmailTemplateList | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<EmailTemplate | null>(null);
  const [deleting, setDeleting] = useState(false);
  const loading = !list && !failed;
  usePageLoading(loading);

  const load = () => {
    setFailed(null);
    getEmailTemplates().then(setList).catch((e: unknown) => setFailed(e instanceof Error && e.message ? e.message : 'The prepared emails could not be loaded.'));
  };
  useEffect(load, []);

  const templates = useMemo(() => list?.templates ?? [], [list]);
  const known = list?.placeholders ?? Object.keys(PLACEHOLDER_LABELS);
  const categories = useMemo(() => [...new Set(templates.map((t) => t.category?.trim()).filter(Boolean) as string[])].sort(), [templates]);
  const bothLanguages = templates.filter((t) => EMAIL_LANGUAGES.every((l) => t.versions.some((v) => v.language === l.code))).length;
  const groups = useMemo(() => {
    const q = search.trim().toLowerCase();
    const shown = templates.filter((t) => !q || `${t.name} ${t.category ?? ''} ${t.description ?? ''} ${t.versions.map((v) => `${v.subject} ${v.body}`).join(' ')}`.toLowerCase().includes(q));
    const byCategory = new Map<string, EmailTemplate[]>();
    for (const t of shown) byCategory.set(t.category?.trim() || 'Other', [...(byCategory.get(t.category?.trim() || 'Other') ?? []), t]);
    return [...byCategory.entries()];
  }, [templates, search]);

  const save = async () => {
    if (!draft) return;
    const request: SaveEmailTemplate = {
      name: draft.name.trim(),
      category: draft.category.trim() || null,
      description: draft.description.trim() || null,
      versions: EMAIL_LANGUAGES.map((l) => ({ language: l.code, subject: draft.versions[l.code].subject, body: draft.versions[l.code].body })),
    };
    setSaving(true);
    try {
      const saved = draft.id ? await updateEmailTemplate(draft.id, request) : await createEmailTemplate(request);
      setList((l) => l && ({ ...l, templates: draft.id ? l.templates.map((t) => (t.id === saved.id ? saved : t)) : [...l.templates, saved] }));
      toast.success(draft.id ? `“${saved.name}” saved` : `“${saved.name}” is ready to use`);
      setDraft(null);
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'It could not be saved.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!confirmDelete) return;
    setDeleting(true);
    try {
      await deleteEmailTemplate(confirmDelete.id);
      setList((l) => l && ({ ...l, templates: l.templates.filter((t) => t.id !== confirmDelete.id) }));
      toast.success(`“${confirmDelete.name}” deleted`);
      setConfirmDelete(null);
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'It could not be deleted.');
    } finally {
      setDeleting(false);
    }
  };

  return (
    <>
      <PageHero
        icon={Mail}
        tone="indigo"
        title="Prepared emails"
        subtitle="Emails ready to send, in French and English. Pick one when you email someone, and change it before it goes."
        actions={
          <WriteOnly>
            <button type="button" onClick={() => setDraft(emptyDraft())} className={`inline-flex items-center text-sm ${heroPrimary('indigo')}`}>
              <Plus className="h-4 w-4" /> New prepared email
            </button>
          </WriteOnly>
        }
        stats={loading ? undefined : [
          { label: 'Prepared emails', value: templates.length, icon: Mail, tone: 'indigo', hint: `${categories.length} ${categories.length === 1 ? 'category' : 'categories'}` },
          { label: 'In both languages', value: bothLanguages, icon: Languages, tone: 'violet', hint: 'French and English' },
          { label: 'Missing a language', value: templates.length - bothLanguages, icon: Globe2, tone: 'amber', hint: 'only one version' },
          { label: 'Your own', value: templates.filter((t) => !t.starter).length, icon: Sparkles, tone: 'teal', hint: `${templates.filter((t) => t.starter).length} from the starter set` },
        ]}
      />
      {tabs}

      {loading ? (
        <ContentSkeleton rows={6} />
      ) : failed ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {failed} <button type="button" onClick={load} className="ml-2 font-medium underline">Try again</button>
        </div>
      ) : (
        <>
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div className="relative w-full max-w-sm">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Find a prepared email" aria-label="Find a prepared email" className="bg-white pl-9" />
            </div>
            <p className="text-sm text-slate-500">
              Placeholders such as <code className="rounded bg-slate-100 px-1">{'{{firstName}}'}</code> are filled in for each person; <code className="rounded bg-amber-50 px-1 text-amber-800">[[blanks]]</code> are filled in by you before sending.
            </p>
          </div>

          {templates.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center">
              <Mail className="mx-auto h-8 w-8 text-indigo-400" />
              <p className="mt-3 font-medium text-slate-800">No prepared emails yet</p>
              <p className="mt-1 text-sm text-slate-500">Write the emails you send often, once, in French and English.</p>
              <WriteOnly>
                <Button className="mt-4" onClick={() => setDraft(emptyDraft())}><Plus className="h-4 w-4" /> New prepared email</Button>
              </WriteOnly>
            </div>
          ) : groups.length === 0 ? (
            <p className="text-sm text-slate-500">None matches “{search}”.</p>
          ) : (
            <div className="space-y-8">
              {groups.map(([category, items]) => (
                <section key={category} aria-label={category}>
                  <h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-500">{category}</h2>
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {items.map((t, i) => (
                      <StaggerItem key={t.id} index={i}>
                        <article className={cn('flex h-full flex-col rounded-2xl border border-slate-200 bg-white p-4 shadow-sm', liftOnHover)} aria-label={t.name}>
                          <div className="flex items-start gap-3">
                            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-violet-500 text-white shadow-sm">
                              <FileText className="h-4 w-4" />
                            </div>
                            <div className="min-w-0 flex-1">
                              <h3 className="truncate font-semibold text-slate-900">{t.name}</h3>
                              {t.description && <p className="mt-0.5 line-clamp-2 text-xs text-slate-500">{t.description}</p>}
                            </div>
                          </div>
                          <ul className="mt-3 flex-1 space-y-1.5">
                            {EMAIL_LANGUAGES.map((l) => {
                              const v = t.versions.find((x) => x.language === l.code);
                              return (
                                <li key={l.code} className="flex items-start gap-2 text-sm">
                                  <span className={cn('mt-0.5 rounded px-1.5 text-[10px] font-bold', v ? 'bg-slate-900 text-white' : 'bg-amber-100 text-amber-800')}>{l.short}</span>
                                  {v
                                    ? <span className="min-w-0 truncate text-slate-700">{v.subject}</span>
                                    : <span className="text-amber-700">No {l.label} version yet</span>}
                                </li>
                              );
                            })}
                          </ul>
                          <div className="mt-4 flex items-center gap-1 border-t border-slate-100 pt-3">
                            <WriteOnly>
                              <Button size="sm" variant="outline" onClick={() => setDraft(draftOf(t))} aria-label={`Edit ${t.name}`}><PenLine className="h-3.5 w-3.5" /> Edit</Button>
                              <Button size="sm" variant="ghost" onClick={() => setDraft(draftOf(t, true))} aria-label={`Copy ${t.name}`}><Copy className="h-3.5 w-3.5" /> Copy</Button>
                              <Button size="sm" variant="ghost" onClick={() => setConfirmDelete(t)} aria-label={`Delete ${t.name}`} className="ml-auto text-rose-600 hover:bg-rose-50 hover:text-rose-700"><Trash2 className="h-3.5 w-3.5" /></Button>
                            </WriteOnly>
                          </div>
                        </article>
                      </StaggerItem>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          )}
        </>
      )}

      <Dialog open={Boolean(draft)} onOpenChange={(open) => { if (!open && !saving) setDraft(null); }}>
        <DialogContent className="flex max-h-[92vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-6xl">
          {draft && (
            <>
              <div className="border-b border-slate-200 bg-gradient-to-r from-indigo-600 via-violet-600 to-fuchsia-500 px-5 py-4 text-white">
                <DialogTitle className="text-lg font-semibold text-white">{draft.id ? `Edit “${draft.name || 'prepared email'}”` : 'New prepared email'}</DialogTitle>
                <DialogDescription className="text-sm text-white/80">
                  Write it in French and English. Either may be left empty and added later.
                </DialogDescription>
              </div>
              <div className="min-h-0 flex-1 space-y-4 overflow-y-auto p-5">
                <div className="grid gap-3 md:grid-cols-[2fr_1fr_2fr]">
                  <div>
                    <Label htmlFor="pe-name">Name</Label>
                    <Input id="pe-name" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} placeholder="e.g. Invitation to visit the club" className="mt-1" maxLength={120} />
                  </div>
                  <div>
                    <Label htmlFor="pe-category">Category</Label>
                    <Input id="pe-category" list="pe-categories" value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} placeholder="e.g. Follow-up" className="mt-1" maxLength={60} />
                    <datalist id="pe-categories">{categories.map((c) => <option key={c} value={c} />)}</datalist>
                  </div>
                  <div>
                    <Label htmlFor="pe-description">When to use it</Label>
                    <Input id="pe-description" value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} placeholder="e.g. After a first call" className="mt-1" maxLength={300} />
                  </div>
                </div>
                <div className="grid gap-4 lg:grid-cols-2">
                  {EMAIL_LANGUAGES.map((l) => (
                    <VersionEditor
                      key={l.code}
                      language={l.code}
                      label={l.label}
                      value={draft.versions[l.code]}
                      known={known}
                      onChange={(v) => setDraft({ ...draft, versions: { ...draft.versions, [l.code]: v } })}
                      preview={{ senderName: me?.name ?? 'You', workspace: org?.name ?? 'Your workspace' }}
                    />
                  ))}
                </div>
              </div>
              <div className="flex items-center justify-end gap-2 border-t border-slate-200 bg-white px-5 py-3">
                <Button variant="outline" onClick={() => setDraft(null)} disabled={saving}>Cancel</Button>
                <Button onClick={save} disabled={saving} className="gap-1.5">
                  {saving && <Loader2 className="h-4 w-4 animate-spin" />}
                  {draft.id ? 'Save changes' : 'Create'}
                </Button>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(confirmDelete)} onOpenChange={(open) => { if (!open && !deleting) setConfirmDelete(null); }}>
        <DialogContent className="sm:max-w-md">
          <DialogTitle>Delete “{confirmDelete?.name}”?</DialogTitle>
          <DialogDescription>
            It goes from the composer for everyone in the workspace, in both languages. Emails already sent with it are not affected.
          </DialogDescription>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setConfirmDelete(null)} disabled={deleting}>Keep it</Button>
            <Button variant="destructive" onClick={remove} disabled={deleting}>{deleting && <Loader2 className="h-4 w-4 animate-spin" />} Delete</Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** One language of a prepared email: its subject and text, with placeholders to insert and a preview. */
function VersionEditor({
  language, label, value, known, onChange, preview,
}: {
  language: EmailLanguage;
  label: string;
  value: { subject: string; body: string };
  known: string[];
  onChange: (next: { subject: string; body: string }) => void;
  preview: { senderName: string; workspace: string };
}) {
  const [showPreview, setShowPreview] = useState(false);
  const bodyId = `pe-body-${language}`;
  const unknown = unknownPlaceholders(`${value.subject}\n${value.body}`, known);
  const blanks = blanksIn(`${value.subject}\n${value.body}`);
  const values = mergeValues(SAMPLE, preview.senderName, preview.workspace);
  const insert = (token: string) => {
    const el = document.getElementById(bodyId) as HTMLTextAreaElement | null;
    const start = el?.selectionStart ?? value.body.length;
    const end = el?.selectionEnd ?? start;
    onChange({ ...value, body: value.body.slice(0, start) + token + value.body.slice(end) });
    requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(start + token.length, start + token.length); });
  };
  const empty = !value.subject.trim() && !value.body.trim();

  return (
    <section aria-label={`${language === 'fr' ? 'French' : 'English'} version`} className="flex flex-col rounded-xl border border-slate-200 bg-slate-50/50 p-4">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 font-semibold text-slate-900">
          <span className="rounded bg-slate-900 px-1.5 text-[11px] font-bold text-white">{language.toUpperCase()}</span> {label}
        </h3>
        <button type="button" onClick={() => setShowPreview((p) => !p)} className="inline-flex items-center gap-1 text-xs font-medium text-indigo-700 hover:underline" aria-pressed={showPreview}>
          <Eye className="h-3.5 w-3.5" /> {showPreview ? 'Back to writing' : 'Preview'}
        </button>
      </div>
      {showPreview ? (
        <div className="flex-1 rounded-lg border border-slate-200 bg-white p-4 text-sm">
          {empty ? <p className="text-slate-500">Nothing written in {label} yet.</p> : (
            <>
              <p className="border-b border-slate-100 pb-2 font-semibold">
                {splitBlanks(fillPlaceholders(value.subject, values).text).map((p, i) => (p.blank ? <mark key={i} className="rounded bg-amber-100 px-1">{p.text}</mark> : <span key={i}>{p.text}</span>))}
              </p>
              <div className="whitespace-pre-wrap pt-3 leading-relaxed">
                {splitBlanks(fillPlaceholders(value.body, values).text).map((p, i) => (p.blank ? <mark key={i} className="rounded bg-amber-100 px-1">{p.text}</mark> : <span key={i}>{p.text}</span>))}
              </div>
              <p className="mt-3 border-t border-slate-100 pt-2 text-xs text-slate-500">As {SAMPLE.name} of {SAMPLE.company} would read it.</p>
            </>
          )}
        </div>
      ) : (
        <div className="flex flex-1 flex-col space-y-2">
          <div>
            <Label htmlFor={`pe-subject-${language}`} className="text-xs">Subject</Label>
            <Input id={`pe-subject-${language}`} value={value.subject} onChange={(e) => onChange({ ...value, subject: e.target.value })} className="mt-1 bg-white" maxLength={300} />
          </div>
          <div className="flex flex-1 flex-col">
            <Label htmlFor={bodyId} className="text-xs">Text</Label>
            <Textarea id={bodyId} value={value.body} onChange={(e) => onChange({ ...value, body: e.target.value })} rows={14} className="mt-1 min-h-[18rem] flex-1 resize-y bg-white leading-relaxed" />
          </div>
          <div className="flex flex-wrap items-center gap-1">
            {known.map((k) => (
              <button key={k} type="button" onClick={() => insert(`{{${k}}}`)} className="rounded-md border border-indigo-200 bg-indigo-50 px-1.5 py-0.5 text-[11px] font-medium text-indigo-700 hover:bg-indigo-100">
                {PLACEHOLDER_LABELS[k] ?? k}
              </button>
            ))}
            <button type="button" onClick={() => insert('[[…]]')} className="rounded-md border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-800 hover:bg-amber-100">
              Blank to fill in
            </button>
          </div>
        </div>
      )}
      {unknown.length > 0 && <p className="mt-2 text-xs text-rose-700">{unknown.join(', ')} is not something Cadence can fill in.</p>}
      {blanks.length > 0 && !showPreview && <p className="mt-2 text-xs text-amber-800">To fill in when sending: {blanks.join(', ')}</p>}
    </section>
  );
}
