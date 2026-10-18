import { useEffect, useState } from 'react';
import { ConversionReport, getConversionReport } from '@/app/api/leadOps';
import { getOrgMembers, type OrgMemberDto } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';

export function ConversionReportPanel() {
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  const [report, setReport] = useState<ConversionReport | null>(null);
  const [failed, setFailed] = useState(false);
  const [members, setMembers] = useState<OrgMemberDto[]>([]);

  useEffect(() => {
    getConversionReport().then(setReport).catch(() => setFailed(true));
  }, []);

  useEffect(() => {
    if (!orgId) return;
    getOrgMembers(orgId).then(setMembers).catch(() => setMembers([]));
  }, [orgId]);

  // The report groups by user id; this printed the raw GUIDs.
  const ownerName = (key: string) => {
    if (key === 'Unassigned') return key;
    const m = members.find((x) => x.userId === key);
    return m ? m.name || m.email : 'Former member';
  };

  if (failed || !report) return null;

  return (
    <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5">
      <h2 className="text-base font-semibold text-slate-900">Lead conversion</h2>
      <p className="mb-3 text-xs text-slate-500">Counted on the server from every lead in this organization.</p>
      <Slice title="By source" rows={report.bySource} label={(k) => k} />
      <Slice title="By owner" rows={report.byOwner} label={ownerName} />
    </section>
  );
}

function Slice({ title, rows, label }: { title: string; rows: ConversionReport['bySource']; label: (key: string) => string }) {
  if (rows.length === 0) return null;
  return (
    <div className="mb-4">
      <h3 className="mb-1 text-sm font-medium text-slate-700">{title}</h3>
      <table className="w-full text-left text-sm">
        <thead className="text-xs text-slate-500">
          <tr>
            <th>Group</th><th>Created</th><th>Qualified</th><th>Converted</th><th>Rate</th><th>Hours to reply</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={`${title}-${row.key}`} className="border-t border-slate-100">
              <td className="py-1">{label(row.key)}</td>
              <td>{row.created}</td>
              <td>{row.qualified}</td>
              <td>{row.converted}</td>
              <td>{Math.round(row.conversionRate * 100)}%</td>
              <td>{row.avgHoursToFirstResponse ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
