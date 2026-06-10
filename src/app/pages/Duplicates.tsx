import { useEffect, useState } from 'react';
import { Copy, Loader2, GitMerge, CheckCircle2, Users, Building2 } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { Button } from '@/app/components/ui/button';
import { Badge } from '@/app/components/ui/badge';
import { cn } from '@/app/components/ui/utils';
import {
  findDuplicateContacts, findDuplicateCompanies, mergeContacts, mergeCompanies,
  type DuplicateGroup,
} from '@/app/api';
import type { Contact, Company } from '@/app/api/types';

interface Row { id: string; title: string; subtitle: string }

export default function Duplicates() {
  const [contactGroups, setContactGroups] = useState<DuplicateGroup<Contact>[]>([]);
  const [companyGroups, setCompanyGroups] = useState<DuplicateGroup<Company>[]>([]);
  const [loading, setLoading] = useState(true);

  const scan = () => {
    setLoading(true);
    Promise.all([findDuplicateContacts(), findDuplicateCompanies()])
      .then(([c, co]) => { setContactGroups(c); setCompanyGroups(co); })
      .catch(() => toast.error('Failed to scan for duplicates'))
      .finally(() => setLoading(false));
  };
  useEffect(() => { scan(); }, []);

  const total = contactGroups.length + companyGroups.length;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Copy}
            iconGradient="from-rose-500 to-pink-500"
            title="Duplicates"
            subtitle="Find and merge duplicate contacts and companies."
            actions={
              <Button onClick={scan} disabled={loading} className="gap-2 h-10 rounded-xl border border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30 disabled:opacity-60">
                {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Copy className="h-4 w-4" />} Re-scan
              </Button>
            }
            stats={[
              { label: 'Contact duplicates', value: contactGroups.length, icon: Users, tone: 'rose' },
              { label: 'Company duplicates', value: companyGroups.length, icon: Building2, tone: 'violet' },
              { label: 'Groups found', value: contactGroups.length + companyGroups.length, icon: Copy, tone: 'amber' },
            ]}
          />

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : total === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-200 bg-white py-16 text-center">
              <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-400" />
              <p className="mt-3 text-sm font-medium text-slate-700">No duplicates found</p>
              <p className="mt-1 text-sm text-slate-500">Your contacts and companies look clean.</p>
            </div>
          ) : (
            <div className="space-y-8">
              {contactGroups.length > 0 && (
                <section>
                  <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
                    <Users className="h-4 w-4" /> Contacts ({contactGroups.length})
                  </h2>
                  <div className="space-y-3">
                    {contactGroups.map((g) => (
                      <GroupCard
                        key={g.key}
                        reason={g.reason}
                        rows={g.records.map((c) => ({ id: c.id, title: c.name, subtitle: c.email || c.phone || '—' }))}
                        onMerge={async (primaryId, dupeIds) => {
                          const ok = await mergeContacts(primaryId, dupeIds);
                          if (ok) { toast.success('Contacts merged'); scan(); } else toast.error('Merge failed');
                        }}
                      />
                    ))}
                  </div>
                </section>
              )}
              {companyGroups.length > 0 && (
                <section>
                  <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
                    <Building2 className="h-4 w-4" /> Companies ({companyGroups.length})
                  </h2>
                  <div className="space-y-3">
                    {companyGroups.map((g) => (
                      <GroupCard
                        key={g.key}
                        reason={g.reason}
                        rows={g.records.map((c) => ({ id: c.id, title: c.name, subtitle: c.domain || c.website || c.industry || '—' }))}
                        onMerge={async (primaryId, dupeIds) => {
                          const ok = await mergeCompanies(primaryId, dupeIds);
                          if (ok) { toast.success('Companies merged'); scan(); } else toast.error('Merge failed');
                        }}
                      />
                    ))}
                  </div>
                </section>
              )}
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

function GroupCard({ reason, rows, onMerge }: { reason: string; rows: Row[]; onMerge: (primaryId: string, dupeIds: string[]) => Promise<void> }) {
  const [primaryId, setPrimaryId] = useState(rows[0]?.id ?? '');
  const [merging, setMerging] = useState(false);

  const handleMerge = async () => {
    const dupeIds = rows.filter((r) => r.id !== primaryId).map((r) => r.id);
    if (dupeIds.length === 0) return;
    setMerging(true);
    try { await onMerge(primaryId, dupeIds); } finally { setMerging(false); }
  };

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between">
        <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200">{reason}</Badge>
        <Button size="sm" onClick={handleMerge} disabled={merging || rows.length < 2} className="gap-1.5">
          {merging ? <Loader2 className="h-4 w-4 animate-spin" /> : <GitMerge className="h-4 w-4" />} Merge {rows.length} → 1
        </Button>
      </div>
      <p className="mb-2 text-xs text-slate-400">Choose the record to keep; the others are merged into it.</p>
      <ul className="space-y-1.5">
        {rows.map((r) => (
          <li key={r.id}>
            <label className={cn(
              'flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-2 transition-colors',
              primaryId === r.id ? 'border-indigo-300 bg-indigo-50/50' : 'border-slate-100 hover:bg-slate-50',
            )}>
              <input type="radio" name={`primary-${rows[0]?.id}`} checked={primaryId === r.id} onChange={() => setPrimaryId(r.id)} className="accent-indigo-600" />
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium text-slate-800">{r.title}</span>
                <span className="block truncate text-xs text-slate-500">{r.subtitle}</span>
              </span>
              {primaryId === r.id && <span className="ml-auto text-[11px] font-semibold text-indigo-600">Keep</span>}
            </label>
          </li>
        ))}
      </ul>
    </div>
  );
}
