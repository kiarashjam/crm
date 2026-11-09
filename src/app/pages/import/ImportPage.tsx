// Importing a spreadsheet of leads, contacts or companies.
//
// The server reads the file, suggests what each column is, and says what every row
// would do (a dry run) before anything is written; then it imports the file as one
// batch that can be undone. This page asks the questions in between: which column is
// which, and what to do with a row that matches a record already here.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertTriangle, ArrowLeft, Check, CheckCircle2, Download, FileSpreadsheet, FileWarning, Loader2,
  RotateCcw, Sparkles, TriangleAlert, Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { Button } from '@/app/components/ui/button';
import { Checkbox } from '@/app/components/ui/checkbox';
import { Label } from '@/app/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/app/components/ui/radio-group';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { getLeadSources, getLeadStatuses } from '@/app/api';
import type { LeadSource, LeadStatus } from '@/app/api/types';
import {
  analyzeImport, previewImport, readImportFile, runImport,
  type ImportAction, type ImportAnalysis, type ImportFile, type ImportKind, type ImportOptions, type ImportResult, type ImportRow,
} from '@/app/api/imports';
import RecentImports, { UndoImportDialog } from './RecentImports';
import { IMPORT_KINDS, count, type ImportKindInfo } from './importKinds';

type Step = 'upload' | 'columns' | 'check' | 'importing' | 'done';

const STEPS: { step: Step; label: string }[] = [
  { step: 'upload', label: 'Upload' },
  { step: 'columns', label: 'Columns' },
  { step: 'check', label: 'Check' },
  { step: 'done', label: 'Import' },
];

/** How many rows a table shows at once; the counts above it cover them all. */
const ROWS_SHOWN = 200;

const SEPARATOR_NAMES: Record<ImportAnalysis['separator'], string> = {
  semicolon: 'semicolons',
  comma: 'commas',
  tab: 'tabs',
};

const ACTION_BADGES: Record<ImportAction, { label: string; className: string }> = {
  create: { label: 'Add', className: 'bg-emerald-100 text-emerald-800' },
  update: { label: 'Update', className: 'bg-blue-100 text-blue-800' },
  skip: { label: 'Skip', className: 'bg-slate-100 text-slate-700' },
  error: { label: 'Problem', className: 'bg-red-100 text-red-800' },
};

const selectClass = 'h-10 w-full rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900';

const CONNECTION_LOST =
  'The connection to the server was lost before the import finished. The server may still have imported the file: check Recent imports below before importing it again.';

/** What still has to be chosen before the file can be checked, or null when nothing. */
export function missingChoice(kind: ImportKind, columns: (string | null)[], duplicates: 'skip' | 'update'): string | null {
  const used = new Set(columns.filter((c): c is string => c !== null));
  if (kind === 'company') return used.has('name') ? null : 'Choose the column that holds the company name.';
  if (!used.has('email')) return 'Choose the column that holds the email address.';
  // Updating can go by email alone; adding someone needs their name.
  if (duplicates === 'skip' && !used.has('name') && !used.has('firstName') && !used.has('lastName'))
    return 'Choose the column that holds the name, or the first and last name columns.';
  return null;
}

/** "Add 2 leads and update 1", or null when there is nothing to do. */
function importLabel(info: ImportKindInfo, result: ImportResult): string | null {
  const parts: string[] = [];
  if (result.created > 0) parts.push(`add ${count(result.created, info)}`);
  if (result.updated > 0) parts.push(`update ${result.updated.toLocaleString()}`);
  if (parts.length === 0) return null;
  const text = parts.join(' and ');
  return text[0]!.toUpperCase() + text.slice(1);
}

function downloadTemplate(info: ImportKindInfo) {
  // With a byte-order mark, so Excel opens "Müller" as Müller.
  const blob = new Blob(['﻿' + info.sample], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = info.sampleFileName;
  a.click();
  URL.revokeObjectURL(url);
}

function message(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

interface ImportPageProps {
  kind: ImportKind;
}

export default function ImportPage({ kind }: ImportPageProps) {
  const info = IMPORT_KINDS[kind];
  const navigate = useNavigate();
  const fileInput = useRef<HTMLInputElement>(null);

  const [step, setStep] = useState<Step>('upload');
  const [file, setFile] = useState<ImportFile | null>(null);
  const [analysis, setAnalysis] = useState<ImportAnalysis | null>(null);
  const [columns, setColumns] = useState<(string | null)[]>([]);
  const [duplicates, setDuplicates] = useState<'skip' | 'update'>('skip');
  // Off unless asked: an imported list is people the workspace already knows, and each
  // row used to set off the welcome email, sequences and webhooks meant for a new inquiry.
  const [runAutomations, setRunAutomations] = useState(false);
  const [defaultStatus, setDefaultStatus] = useState('');
  const [defaultSource, setDefaultSource] = useState('csv_import');
  const [statuses, setStatuses] = useState<LeadStatus[]>([]);
  const [sources, setSources] = useState<LeadSource[]>([]);
  const [preview, setPreview] = useState<ImportResult | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [busy, setBusy] = useState<'reading' | 'checking' | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [filter, setFilter] = useState<ImportAction | 'all'>('all');
  const [undoOpen, setUndoOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    if (kind !== 'lead' || servesDemoData()) return;
    Promise.all([getLeadStatuses(), getLeadSources()])
      .then(([s, src]) => { setStatuses(s ?? []); setSources(src ?? []); })
      .catch(() => { /* The defaults still work without the lists: New, and csv_import. */ });
  }, [kind]);

  const options = useCallback((): ImportOptions => ({
    columns,
    duplicates,
    ...(kind === 'lead'
      ? { runAutomations, defaultStatus: defaultStatus || undefined, defaultSource: defaultSource.trim() || undefined }
      : {}),
  }), [columns, duplicates, kind, runAutomations, defaultStatus, defaultSource]);

  const reset = () => {
    setStep('upload');
    setFile(null);
    setAnalysis(null);
    setColumns([]);
    setPreview(null);
    setResult(null);
    setProblem(null);
    setFilter('all');
  };

  const handleFile = async (picked: File) => {
    setProblem(null);
    setBusy('reading');
    try {
      const read = await readImportFile(picked);
      const analyzed = await analyzeImport(kind, read);
      setFile(read);
      setAnalysis(analyzed);
      setColumns(analyzed.columns.map((c) => c.field));
      setPreview(null);
      setStep('columns');
    } catch (e) {
      setProblem(message(e, 'The file could not be read.'));
    } finally {
      setBusy(null);
    }
  };

  const choose = (index: number, field: string | null) => {
    setPreview(null);
    // A field goes into one column: choosing it here takes it from wherever it was.
    setColumns((current) => current.map((c, i) => (i === index ? field : c === field && field !== null ? null : c)));
  };

  const check = async () => {
    if (!file) return;
    setProblem(null);
    setBusy('checking');
    try {
      setPreview(await previewImport(kind, file, options()));
      setFilter('all');
      setStep('check');
    } catch (e) {
      setProblem(message(e, 'The file could not be checked.'));
    } finally {
      setBusy(null);
    }
  };

  const doImport = async () => {
    if (!file) return;
    setProblem(null);
    setStep('importing');
    try {
      const done = await runImport(kind, file, options());
      setResult(done);
      setStep('done');
      setRefreshKey((k) => k + 1);
      const changed = done.created + done.updated;
      if (changed > 0) toast.success(`Imported ${changed.toLocaleString()} ${changed === 1 ? info.one : info.many}`);
      if (done.failed > 0) toast.error(`${done.failed.toLocaleString()} rows were not imported`);
    } catch (e) {
      if (e instanceof ApiError && e.status >= 400 && e.status < 500) {
        // Refused before anything was written (the file, the columns, a Viewer): say
        // why, and keep the checked file here to change and try again.
        setProblem(message(e, 'The import did not run.'));
        setStep('check');
        return;
      }
      // The connection dropped, or the server or the hosting in front of it stopped
      // waiting. Once started, an import keeps going on the server, so it may well have
      // run: importing the file again from here could add its rows twice. Recent
      // imports shows it instead, still running or finished.
      reset();
      setProblem(CONNECTION_LOST);
      setRefreshKey((k) => k + 1);
    }
  };

  const missing = analysis ? missingChoice(kind, columns, duplicates) : null;
  const stepIndex = step === 'importing' ? 3 : STEPS.findIndex((s) => s.step === step);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <PageHero
          icon={info.icon}
          tone={info.tone}
          title={info.title}
          subtitle={info.subtitle}
          back={{ to: info.listPath, label: `Back to ${info.listName}` }}
        >
          <ol className="flex items-center gap-2 sm:gap-4" aria-label="Steps">
            {STEPS.map((s, i) => {
              const complete = i < stepIndex || step === 'done';
              const active = i === stepIndex && step !== 'done';
              return (
                <li key={s.step} className="flex items-center gap-2" aria-current={active ? 'step' : undefined}>
                  <span className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium ${
                    complete ? 'bg-emerald-500 text-white' : active ? 'bg-orange-500 text-white' : 'bg-white/80 text-slate-500 ring-1 ring-slate-200'
                  }`}>
                    {complete ? <Check className="h-4 w-4" aria-hidden /> : i + 1}
                  </span>
                  <span className={`text-xs font-medium ${active || complete ? 'text-slate-700' : 'text-slate-500'}`}>{s.label}</span>
                </li>
              );
            })}
          </ol>
        </PageHero>

        {servesDemoData() ? (
          <div className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-white p-6">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden />
            <p className="text-sm text-slate-600">
              Importing needs a connected backend: the server reads the file, checks every row against your
              {' '}{info.many} before anything is written, and can undo the import afterwards. This is deliberately
              not faked in demo mode.
            </p>
          </div>
        ) : (
          <div className="space-y-6">
            {problem && (
              <div className="flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4" role="alert">
                <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-red-600" aria-hidden />
                <p className="text-sm text-red-800">{problem}</p>
              </div>
            )}

            {step === 'upload' && (
              <>
                <div
                  onDragEnter={(e) => { e.preventDefault(); setDragActive(true); }}
                  onDragOver={(e) => { e.preventDefault(); setDragActive(true); }}
                  onDragLeave={(e) => { e.preventDefault(); setDragActive(false); }}
                  onDrop={(e) => {
                    e.preventDefault();
                    setDragActive(false);
                    const dropped = e.dataTransfer.files?.[0];
                    if (dropped) void handleFile(dropped);
                  }}
                  className={`rounded-2xl border-2 border-dashed p-10 text-center transition-all ${
                    dragActive ? 'border-orange-400 bg-orange-50' : 'border-slate-200 bg-white'
                  }`}
                >
                  <input
                    ref={fileInput}
                    id="import-file"
                    type="file"
                    accept=".csv,.txt,.tsv,text/csv,text/plain,text/tab-separated-values"
                    className="sr-only"
                    onChange={(e) => {
                      const picked = e.target.files?.[0];
                      e.target.value = '';
                      if (picked) void handleFile(picked);
                    }}
                  />
                  <FileSpreadsheet className="mx-auto mb-4 h-10 w-10 text-slate-500" aria-hidden />
                  <h3 className="mb-1 text-lg font-semibold text-slate-800">Drop a spreadsheet saved as CSV here</h3>
                  <p className="mb-4 text-sm text-slate-500">
                    Columns separated by semicolons (as Excel saves in Switzerland), commas or tabs. Up to 2,000 rows a file.
                  </p>
                  <Button onClick={() => fileInput.current?.click()} disabled={busy === 'reading'} className="gap-2 bg-orange-600 hover:bg-orange-500">
                    {busy === 'reading' ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Upload className="h-4 w-4" aria-hidden />}
                    {busy === 'reading' ? 'Reading the file…' : 'Choose a file'}
                  </Button>
                </div>

                <div className="rounded-xl border border-slate-200 bg-white p-6">
                  <h4 className="mb-1 font-semibold text-slate-800">Start from a template</h4>
                  <p className="mb-3 text-sm text-slate-500">
                    A header row names the columns. Nothing is written until you have checked what each row will do,
                    and an import can be undone afterwards.
                  </p>
                  <Button variant="outline" onClick={() => downloadTemplate(info)} className="gap-2">
                    <Download className="h-4 w-4" aria-hidden /> Download a template
                  </Button>
                </div>

                <RecentImports kind={kind} refreshKey={refreshKey} />
              </>
            )}

            {step === 'columns' && analysis && file && (
              <>
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-4">
                  <div className="flex items-center gap-3">
                    <FileSpreadsheet className="h-5 w-5 text-emerald-600" aria-hidden />
                    <div>
                      <p className="font-medium text-slate-800">{file.fileName}</p>
                      <p className="text-sm text-slate-500">
                        {analysis.rowCount.toLocaleString()} rows, columns separated by {SEPARATOR_NAMES[analysis.separator]}, read as {analysis.encoding}
                      </p>
                    </div>
                  </div>
                  <Button variant="outline" size="sm" onClick={reset}>Choose another file</Button>
                </div>

                <section className="overflow-hidden rounded-xl border border-slate-200 bg-white" aria-labelledby="import-columns">
                  <div className="border-b border-slate-100 px-6 py-4">
                    <h3 id="import-columns" className="font-semibold text-slate-800">What is in each column?</h3>
                    <p className="mt-1 text-sm text-slate-500">
                      Suggested from the headers. A column left out is not imported.
                    </p>
                  </div>
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">
                        <tr>
                          <th className="px-4 py-2">Column in the file</th>
                          <th className="px-4 py-2">First values</th>
                          <th className="px-4 py-2">Import into</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {analysis.columns.map((column) => {
                          const standard = analysis.fields.filter((f) => !f.custom);
                          const custom = analysis.fields.filter((f) => f.custom);
                          return (
                            <tr key={column.index}>
                              <td className="px-4 py-2 font-medium text-slate-800">{column.header}</td>
                              <td className="max-w-xs px-4 py-2 text-slate-500">
                                <span className="line-clamp-2">{column.samples.join(' · ') || '—'}</span>
                              </td>
                              <td className="w-64 px-4 py-2">
                                <select
                                  aria-label={`Import ${column.header} into`}
                                  className={selectClass}
                                  value={columns[column.index] ?? ''}
                                  onChange={(e) => choose(column.index, e.target.value || null)}
                                >
                                  <option value="">Don’t import</option>
                                  {standard.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
                                  {custom.length > 0 && (
                                    <optgroup label="Custom fields">
                                      {custom.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
                                    </optgroup>
                                  )}
                                </select>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </section>

                <section className="space-y-4 rounded-xl border border-slate-200 bg-white p-6" aria-labelledby="import-matches">
                  <div>
                    <h3 id="import-matches" className="font-semibold text-slate-800">Rows that match {info.many === 'companies' ? 'a company' : 'someone'} already here</h3>
                    <p className="mt-1 text-sm text-slate-500">
                      A row matches by {info.matchedBy}.{info.matchNote && <> {info.matchNote}</>}
                    </p>
                  </div>
                  <RadioGroup value={duplicates} onValueChange={(v) => { setPreview(null); setDuplicates(v === 'update' ? 'update' : 'skip'); }}>
                    <div className="flex items-start gap-3">
                      <RadioGroupItem value="skip" id="import-skip" className="mt-0.5" />
                      <Label htmlFor="import-skip" className="text-sm font-normal text-slate-700">
                        Skip them, and leave what is here as it is
                      </Label>
                    </div>
                    <div className="flex items-start gap-3">
                      <RadioGroupItem value="update" id="import-update" className="mt-0.5" />
                      <Label htmlFor="import-update" className="text-sm font-normal text-slate-700">
                        Update them with the file's values. An empty cell never clears anything
                      </Label>
                    </div>
                  </RadioGroup>

                  {kind === 'lead' && (
                    <div className="flex flex-wrap gap-4 border-t border-slate-100 pt-4">
                      <label className="text-sm text-slate-700">
                        Status when the file has none
                        <select className={`mt-1 ${selectClass} w-48`} value={defaultStatus} onChange={(e) => { setPreview(null); setDefaultStatus(e.target.value); }}>
                          <option value="">The workspace's first (New)</option>
                          {statuses.map((s) => <option key={s.id} value={s.name}>{s.name}</option>)}
                        </select>
                      </label>
                      <label className="text-sm text-slate-700">
                        Source when the file has none
                        <select className={`mt-1 ${selectClass} w-48`} value={defaultSource} onChange={(e) => { setPreview(null); setDefaultSource(e.target.value); }}>
                          <option value="csv_import">csv_import</option>
                          {sources.map((s) => <option key={s.id} value={s.name}>{s.name}</option>)}
                        </select>
                      </label>
                    </div>
                  )}

                  {kind === 'lead' && (
                    <div className="flex items-start gap-3 border-t border-slate-100 pt-4">
                      <Checkbox
                        id="import-run-automations"
                        checked={runAutomations}
                        onCheckedChange={(checked) => setRunAutomations(checked === true)}
                        className="mt-0.5"
                      />
                      <div>
                        <Label htmlFor="import-run-automations" className="text-sm font-medium text-slate-800">
                          Run automations for these leads
                        </Label>
                        <p className="mt-1 text-sm text-slate-500">
                          {runAutomations
                            ? 'Each lead added runs your "new lead" automations, which can send emails and start sequences, and a status the file changes runs your status automations.'
                            : 'Imported leads are added quietly: no automations, emails, sequences or webhooks, and no first-response alerts.'}
                        </p>
                      </div>
                    </div>
                  )}
                </section>

                {missing && (
                  <div className="flex items-center gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4">
                    <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" aria-hidden />
                    <p className="text-sm text-amber-800">{missing}</p>
                  </div>
                )}

                <div className="flex items-center justify-between">
                  <Button variant="outline" onClick={reset} className="gap-2"><ArrowLeft className="h-4 w-4" aria-hidden /> Back</Button>
                  <Button onClick={check} disabled={missing !== null || busy === 'checking'} className="gap-2 bg-orange-600 hover:bg-orange-500">
                    {busy === 'checking' && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
                    Check the file
                  </Button>
                </div>
              </>
            )}

            {step === 'check' && preview && (
              <>
                <Outcome info={info} result={preview} dryRun filter={filter} onFilter={setFilter} />
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <Button variant="outline" onClick={() => setStep('columns')} className="gap-2">
                    <ArrowLeft className="h-4 w-4" aria-hidden /> Back to the columns
                  </Button>
                  {importLabel(info, preview) ? (
                    <Button onClick={doImport} className="gap-2 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600">
                      <Sparkles className="h-4 w-4" aria-hidden /> {importLabel(info, preview)}
                    </Button>
                  ) : (
                    <p className="text-sm text-slate-500">Nothing in this file would be added or changed.</p>
                  )}
                </div>
              </>
            )}

            {step === 'importing' && (
              <div className="flex flex-col items-center justify-center py-16 text-center" role="status">
                <Loader2 className="mb-6 h-12 w-12 animate-spin text-orange-500" aria-hidden />
                <h3 className="mb-2 text-xl font-semibold text-slate-800">Importing {info.many}…</h3>
                <p className="max-w-md text-slate-500">
                  A large file can take a minute or two. If you leave this page, the import still finishes, and shows
                  under Recent imports.
                </p>
              </div>
            )}

            {step === 'done' && result && (
              <>
                <Outcome info={info} result={result} dryRun={false} filter={filter} onFilter={setFilter} />
                <div className="flex flex-wrap items-center justify-center gap-3">
                  {result.batchId && result.created > 0 && (
                    <Button variant="outline" onClick={() => setUndoOpen(true)} className="gap-2">
                      <RotateCcw className="h-4 w-4" aria-hidden /> Undo this import
                    </Button>
                  )}
                  <Button variant="outline" onClick={reset} className="gap-2">
                    <Upload className="h-4 w-4" aria-hidden /> Import another file
                  </Button>
                  <Button onClick={() => navigate(info.listPath)} className="gap-2 bg-orange-600 hover:bg-orange-500">
                    View {info.many}
                  </Button>
                </div>
                <UndoImportDialog
                  kind={kind}
                  batch={undoOpen && result.batchId && file ? { id: result.batchId, fileName: file.fileName } : null}
                  onClose={() => setUndoOpen(false)}
                  onUndone={() => { setUndoOpen(false); setRefreshKey((k) => k + 1); reset(); }}
                />
              </>
            )}
          </div>
        )}
      </main>
    </div>
  );
}

interface OutcomeProps {
  info: ImportKindInfo;
  result: ImportResult;
  dryRun: boolean;
  filter: ImportAction | 'all';
  onFilter: (filter: ImportAction | 'all') => void;
}

/** What each row would do, or did: the counts, then the rows, one kind at a time. */
function Outcome({ info, result, dryRun, filter, onFilter }: OutcomeProps) {
  const tiles: { action: ImportAction; label: string; value: number }[] = [
    { action: 'create', label: dryRun ? 'To add' : 'Added', value: result.created },
    { action: 'update', label: dryRun ? 'To update' : 'Updated', value: result.updated },
    { action: 'skip', label: 'Skipped', value: result.skipped },
    { action: 'error', label: dryRun ? 'Problems' : 'Not imported', value: result.failed },
  ];
  const rows = useMemo(
    () => (filter === 'all' ? result.rows : result.rows.filter((r) => r.action === filter)),
    [filter, result.rows],
  );

  return (
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white" aria-labelledby="import-outcome">
      <div className="border-b border-slate-100 px-6 py-4">
        <h3 id="import-outcome" className="flex items-center gap-2 font-semibold text-slate-800">
          {dryRun
            ? <>Checked: nothing has been written yet</>
            : result.failed === 0
              ? <><CheckCircle2 className="h-5 w-5 text-emerald-600" aria-hidden /> Import complete</>
              : <><FileWarning className="h-5 w-5 text-amber-600" aria-hidden /> Imported, with rows that were not</>}
        </h3>
        <p className="mt-1 text-sm text-slate-500">
          {dryRun
            ? `What each of the ${result.totalRows.toLocaleString()} rows would do.`
            : `${result.totalRows.toLocaleString()} rows read.`}
        </p>
      </div>
      <div className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4">
        {tiles.map((t) => (
          <button
            key={t.action}
            type="button"
            onClick={() => onFilter(filter === t.action ? 'all' : t.action)}
            aria-pressed={filter === t.action}
            className={`rounded-lg border p-3 text-left transition-colors ${
              filter === t.action ? 'border-orange-300 bg-orange-50' : 'border-slate-200 hover:bg-slate-50'
            }`}
          >
            <span className="block text-2xl font-bold text-slate-800">{t.value.toLocaleString()}</span>
            <span className="text-sm text-slate-500">{t.label}</span>
          </button>
        ))}
      </div>
      <RowsTable info={info} rows={rows} />
    </section>
  );
}

function RowsTable({ info, rows }: { info: ImportKindInfo; rows: ImportRow[] }) {
  if (rows.length === 0) return <p className="px-6 pb-6 text-sm text-slate-500">No rows here.</p>;
  return (
    <div className="overflow-x-auto border-t border-slate-100">
      <table className="w-full text-sm">
        <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">
          <tr>
            <th className="px-4 py-2">Row</th>
            <th className="px-4 py-2">{info.one === 'company' ? 'Company' : 'Name'}</th>
            <th className="px-4 py-2">What happens</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.slice(0, ROWS_SHOWN).map((r) => (
            <tr key={r.row} className="align-top">
              <td className="px-4 py-2 text-slate-500">{r.row}</td>
              <td className="px-4 py-2 font-medium text-slate-800">{r.label}</td>
              <td className="px-4 py-2">
                <span className={`mr-2 inline-block rounded-full px-2 py-0.5 text-xs font-medium ${ACTION_BADGES[r.action].className}`}>
                  {ACTION_BADGES[r.action].label}
                </span>
                {r.message && <span className="text-slate-700">{r.message}</span>}
                {r.changes.length > 0 && (
                  <ul className="mt-1 list-disc pl-5 text-slate-600">
                    {r.changes.map((c) => <li key={c}>{c}</li>)}
                  </ul>
                )}
                {r.notes.length > 0 && (
                  <ul className="mt-1 space-y-0.5 text-slate-500">
                    {r.notes.map((n) => <li key={n}>{n}</li>)}
                  </ul>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length > ROWS_SHOWN && (
        <p className="border-t border-slate-100 px-6 py-3 text-center text-sm text-slate-500">
          Showing the first {ROWS_SHOWN.toLocaleString()} of {rows.length.toLocaleString()} rows. The counts above cover them all.
        </p>
      )}
    </div>
  );
}
