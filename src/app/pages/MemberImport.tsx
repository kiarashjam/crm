// Bulk member import — staff paste or upload a CSV of members and create
// them in one batch. Useful for migrations from another club system.

import { useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Upload,
  Crown,
  ArrowLeft,
  Check,
  AlertTriangle,
  Loader2,
  Download,
  FileText,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  createMember,
  MEMBER_TIERS,
  MEMBER_STATUSES,
  TIER_DUES,
  type MemberTier,
  type MemberStatus,
} from '@/app/api/members';

interface ParsedRow {
  rowIndex: number;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  tier: MemberTier;
  status: MemberStatus;
  error?: string;
}

const SAMPLE_CSV = `firstName,lastName,email,phone,tier,status
Eleanor,Tanaka,eleanor.tanaka@example.com,+1 (415) 555-0188,Gold,Active
Felix,Ashford,felix.ashford@example.com,,Silver,Active
Magnolia,Vega,magnolia.vega@example.com,+1 (212) 555-0123,Platinum,Pending
`;

function parseCsv(text: string): ParsedRow[] {
  const trimmed = text.trim();
  if (!trimmed) return [];
  const lines = trimmed.split(/\r?\n/);
  if (lines.length === 0) return [];

  const header = (lines[0] ?? '').split(',').map((h) => h.trim().toLowerCase());
  const idx = (name: string) => header.indexOf(name);
  const iFirst = idx('firstname');
  const iLast = idx('lastname');
  const iEmail = idx('email');
  const iPhone = idx('phone');
  const iTier = idx('tier');
  const iStatus = idx('status');

  const rows: ParsedRow[] = [];
  for (let i = 1; i < lines.length; i++) {
    const cells = (lines[i] ?? '').split(',').map((c) => c.trim());
    const firstName = iFirst >= 0 ? cells[iFirst] ?? '' : '';
    const lastName = iLast >= 0 ? cells[iLast] ?? '' : '';
    const email = iEmail >= 0 ? cells[iEmail] ?? '' : '';
    const phone = iPhone >= 0 ? cells[iPhone] ?? '' : '';
    const tierRaw = iTier >= 0 ? cells[iTier] ?? 'Silver' : 'Silver';
    const statusRaw = iStatus >= 0 ? cells[iStatus] ?? 'Pending' : 'Pending';
    const tier: MemberTier = MEMBER_TIERS.find((t) => t.toLowerCase() === tierRaw.toLowerCase()) ?? 'Silver';
    const status: MemberStatus =
      MEMBER_STATUSES.find((s) => s.toLowerCase() === statusRaw.toLowerCase()) ?? 'Pending';

    let error: string | undefined;
    if (!firstName) error = 'Missing first name';
    else if (!lastName) error = 'Missing last name';
    else if (!email) error = 'Missing email';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) error = 'Invalid email';

    rows.push({
      rowIndex: i,
      firstName,
      lastName,
      email,
      phone,
      tier,
      status,
      error,
    });
  }
  return rows;
}

const day = 86_400_000;

export default function MemberImport() {
  const navigate = useNavigate();
  const fileInput = useRef<HTMLInputElement>(null);
  const [csvText, setCsvText] = useState('');
  const [importing, setImporting] = useState(false);
  const [imported, setImported] = useState(0);
  const [errored, setErrored] = useState(0);

  const rows = useMemo(() => parseCsv(csvText), [csvText]);
  const validRows = useMemo(() => rows.filter((r) => !r.error), [rows]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const text = String(reader.result ?? '');
      setCsvText(text);
    };
    reader.readAsText(file);
  };

  const importRows = async () => {
    if (validRows.length === 0) {
      toast.error('No valid rows to import');
      return;
    }
    setImporting(true);
    setImported(0);
    setErrored(0);
    try {
      let ok = 0;
      let bad = 0;
      for (const row of validRows) {
        try {
          await createMember({
            firstName: row.firstName,
            lastName: row.lastName,
            email: row.email,
            phone: row.phone || undefined,
            tier: row.tier,
            status: row.status,
            duesAmount: TIER_DUES[row.tier],
            duesFrequency: 'Monthly',
            joinedAtUtc: new Date().toISOString(),
            renewsAtUtc: new Date(Date.now() + 365 * day).toISOString(),
          });
          ok++;
          setImported(ok);
        } catch {
          bad++;
          setErrored(bad);
        }
      }
      toast.success(`Imported ${ok} member${ok === 1 ? '' : 's'}${bad > 0 ? ` · ${bad} failed` : ''}`);
      if (ok > 0) {
        setTimeout(() => navigate('/members'), 1200);
      }
    } finally {
      setImporting(false);
    }
  };

  const downloadSample = () => {
    const blob = new Blob([SAMPLE_CSV], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'members-sample.csv';
    a.click();
    URL.revokeObjectURL(url);
  };

  const errorCount = rows.length - validRows.length;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <Link
            to="/members"
            className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 mb-4"
          >
            <ArrowLeft className="w-4 h-4" /> Back to members
          </Link>

          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-indigo-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-indigo-500 to-violet-500 flex items-center justify-center shadow-2xl shadow-indigo-500/30">
                <Upload className="w-8 h-8 text-white" />
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">
                  Import members
                </h1>
                <p className="text-slate-400 mt-1">
                  Paste a CSV or upload a file. Each row becomes a new member record.
                </p>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2 space-y-4">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
                <div className="flex items-center justify-between mb-3">
                  <Label className="text-sm font-semibold text-slate-900">CSV data</Label>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={downloadSample}>
                      <Download className="w-3.5 h-3.5 mr-1.5" /> Sample
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => fileInput.current?.click()}
                    >
                      <FileText className="w-3.5 h-3.5 mr-1.5" /> Upload file
                    </Button>
                    <input
                      ref={fileInput}
                      type="file"
                      accept=".csv,text/csv"
                      className="hidden"
                      onChange={handleFileChange}
                    />
                  </div>
                </div>
                <Textarea
                  value={csvText}
                  onChange={(e) => setCsvText(e.target.value)}
                  rows={12}
                  placeholder={SAMPLE_CSV}
                  className="font-mono text-xs"
                />
                <p className="text-xs text-slate-500 mt-2">
                  Required columns: <code>firstName</code>, <code>lastName</code>,{' '}
                  <code>email</code>. Optional: <code>phone</code>, <code>tier</code>,{' '}
                  <code>status</code>. Dues default to the tier's monthly rate.
                </p>
              </div>

              {rows.length > 0 && (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <div className="px-5 py-3 border-b border-slate-200 flex items-center justify-between">
                    <h3 className="font-semibold text-slate-900">
                      Preview · {rows.length} rows
                    </h3>
                    <p className="text-xs text-slate-500">
                      {validRows.length} valid{' '}
                      {errorCount > 0 && <span className="text-rose-600">· {errorCount} with errors</span>}
                    </p>
                  </div>
                  <div className="max-h-80 overflow-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-slate-50 sticky top-0">
                        <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                          <th className="px-4 py-2 w-6"></th>
                          <th className="px-4 py-2">Name</th>
                          <th className="px-4 py-2">Email</th>
                          <th className="px-4 py-2">Tier</th>
                          <th className="px-4 py-2">Status</th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((r) => (
                          <tr key={r.rowIndex} className="border-b border-slate-100">
                            <td className="px-4 py-2">
                              {r.error ? (
                                <AlertTriangle className="w-3.5 h-3.5 text-rose-500" />
                              ) : (
                                <Check className="w-3.5 h-3.5 text-emerald-500" />
                              )}
                            </td>
                            <td className="px-4 py-2 text-slate-700">
                              {r.firstName} {r.lastName}
                              {r.error && (
                                <p className="text-xs text-rose-600 mt-0.5">{r.error}</p>
                              )}
                            </td>
                            <td className="px-4 py-2 text-slate-500">{r.email}</td>
                            <td className="px-4 py-2">
                              <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">
                                {r.tier}
                              </span>
                            </td>
                            <td className="px-4 py-2 text-slate-500">{r.status}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>

            {/* Summary panel */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 self-start">
              <h3 className="font-semibold text-slate-900 mb-3">Ready to import</h3>
              <dl className="space-y-2 text-sm mb-4">
                <div className="flex justify-between">
                  <dt className="text-slate-500">Total rows</dt>
                  <dd className="font-medium text-slate-900">{rows.length}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-500">Valid</dt>
                  <dd className="font-medium text-emerald-700">{validRows.length}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-500">Errors</dt>
                  <dd className="font-medium text-rose-700">{errorCount}</dd>
                </div>
                {importing && (
                  <>
                    <div className="flex justify-between border-t border-slate-100 pt-2">
                      <dt className="text-slate-500">Imported</dt>
                      <dd className="font-medium text-slate-900">
                        {imported} / {validRows.length}
                      </dd>
                    </div>
                    {errored > 0 && (
                      <div className="flex justify-between">
                        <dt className="text-slate-500">Failed</dt>
                        <dd className="font-medium text-rose-700">{errored}</dd>
                      </div>
                    )}
                  </>
                )}
              </dl>
              <Button
                onClick={importRows}
                disabled={importing || validRows.length === 0}
                className="w-full"
              >
                {importing ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Importing
                  </>
                ) : (
                  <>
                    <Crown className="w-4 h-4 mr-2" /> Import {validRows.length || ''} member
                    {validRows.length === 1 ? '' : 's'}
                  </>
                )}
              </Button>
              <p className="text-xs text-slate-400 mt-3">
                Members start at Pending unless you set status. Dues match the tier defaults; tune
                per-member afterwards.
              </p>
            </div>
          </div>
        </main>
      </PageTransition>
    </div>
  );
}
