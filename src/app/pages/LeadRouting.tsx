import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  AssignmentRule,
  CaptureForm,
  ConversionReport,
  LeadQueue,
  ScoringRule,
  Territory,
  createCaptureForm,
  getAssignmentRules,
  getCaptureForms,
  getConversionReport,
  getQueues,
  getScoringRules,
  getSla,
  getTerritories,
  saveAssignmentRule,
  saveQueue,
  saveScoringRules,
  saveSla,
  saveTerritory,
} from '@/app/api/leadOps';

export default function LeadRouting() {
  const navigate = useNavigate();
  const [sla, setSla] = useState(4);
  const [rules, setRules] = useState<ScoringRule[]>([]);
  const [assignment, setAssignment] = useState<AssignmentRule[]>([]);
  const [territories, setTerritories] = useState<Territory[]>([]);
  const [queues, setQueues] = useState<LeadQueue[]>([]);
  const [forms, setForms] = useState<CaptureForm[]>([]);
  const [report, setReport] = useState<ConversionReport | null>(null);
  const [ruleName, setRuleName] = useState('Website round robin');
  const [ruleKind, setRuleKind] = useState('Source');
  const [ruleMatch, setRuleMatch] = useState('Website');
  const [territoryName, setTerritoryName] = useState('');
  const [countries, setCountries] = useState('');
  const [queueName, setQueueName] = useState('');
  const [queueMembers, setQueueMembers] = useState('');
  const [formName, setFormName] = useState('Website');
  const [useQueue, setUseQueue] = useState(false);

  const load = () => {
    Promise.all([
      getSla(), getScoringRules(), getAssignmentRules(), getTerritories(), getQueues(), getCaptureForms(), getConversionReport(),
    ]).then(([s, scoring, assign, terr, q, f, conversion]) => {
      setSla(s.hours);
      setRules(scoring);
      setAssignment(assign);
      setTerritories(terr);
      setQueues(q);
      setForms(f);
      setReport(conversion);
    }).catch(() => toast.error('Lead settings could not be loaded'));
  };

  useEffect(() => { load(); }, []);

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-8 px-4 py-8">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">Lead routing</h1>
            <p className="text-sm text-slate-500">Scoring, assignment, queues, capture, and conversion.</p>
          </div>
          <Button variant="outline" onClick={() => navigate('/leads')}>Back to leads</Button>
        </div>

        <Section title="First response">
          <div className="flex items-end gap-3">
            <label className="text-sm text-slate-700">
              Hours before a new lead is late
              <Input className="mt-1 w-28" type="number" min={1} max={168} value={sla} onChange={(e) => setSla(Number(e.target.value))} />
            </label>
            <Button onClick={() => saveSla(sla).then((s) => { setSla(s.hours); toast.success('Saved'); }).catch(() => toast.error('Could not save'))}>Save</Button>
          </div>
        </Section>

        <Section title="Score rules">
          <div className="space-y-2">
            {rules.map((rule, index) => (
              <div key={rule.id} className="grid grid-cols-1 gap-2 sm:grid-cols-4">
                <Input value={rule.field} onChange={(e) => updateRule(index, { field: e.target.value })} />
                <Input value={rule.matchValue ?? ''} placeholder="Match" onChange={(e) => updateRule(index, { matchValue: e.target.value })} />
                <Input type="number" value={rule.points} onChange={(e) => updateRule(index, { points: Number(e.target.value) })} />
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={rule.isActive} onChange={(e) => updateRule(index, { isActive: e.target.checked })} />
                  Active
                </label>
              </div>
            ))}
          </div>
          <div className="mt-3 flex gap-2">
            <Button variant="outline" onClick={() => setRules([...rules, { id: crypto.randomUUID(), field: 'source', matchValue: '', points: 5, isActive: true }])}>Add rule</Button>
            <Button onClick={() => saveScoringRules(rules).then(setRules).then(() => toast.success('Score rules saved')).catch(() => toast.error('Could not save'))}>Save score rules</Button>
          </div>
          <p className="mt-2 text-xs text-slate-500">Fields: source, status, country, industry, hasPhone, hasJobTitle, hasCompany, contactedWithinDays, emailOpens, staleDays.</p>
        </Section>

        <Section title="Assignment rules">
          <div className="grid gap-2 sm:grid-cols-3">
            <Input value={ruleName} onChange={(e) => setRuleName(e.target.value)} placeholder="Name" />
            <select className="h-10 rounded-md border border-slate-200 px-2 text-sm" value={ruleKind} onChange={(e) => setRuleKind(e.target.value)}>
              <option>RoundRobin</option>
              <option>Source</option>
              <option>Territory</option>
            </select>
            <Input value={ruleMatch} onChange={(e) => setRuleMatch(e.target.value)} placeholder="Source match" />
          </div>
          <label className="mt-2 flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" checked={useQueue} onChange={(e) => setUseQueue(e.target.checked)} />
            Assign inside the first queue
          </label>
          <Button className="mt-3" onClick={() => saveAssignmentRule({
            name: ruleName,
            kind: ruleKind,
            sourceMatch: ruleKind === 'Source' ? ruleMatch : undefined,
            territoryId: ruleKind === 'Territory' ? territories[0]?.id : undefined,
            queueId: useQueue ? queues[0]?.id : undefined,
            isActive: true,
            priority: assignment.length,
          }).then(() => { toast.success('Rule saved'); load(); }).catch(() => toast.error('Could not save'))}>Add rule</Button>
          <ul className="mt-3 space-y-1 text-sm text-slate-700">
            {assignment.map((rule) => (
              <li key={rule.id}>{rule.isActive ? 'On' : 'Off'} · {rule.kind} · {rule.name}{rule.sourceMatch ? ` · ${rule.sourceMatch}` : ''}</li>
            ))}
          </ul>
        </Section>

        <Section title="Territories and queues">
          <div className="grid gap-2 sm:grid-cols-2">
            <Input value={territoryName} onChange={(e) => setTerritoryName(e.target.value)} placeholder="Territory name" />
            <Input value={countries} onChange={(e) => setCountries(e.target.value)} placeholder="Countries, comma separated" />
          </div>
          <Button className="mt-3" variant="outline" onClick={() => saveTerritory({ name: territoryName, countries }).then(() => { toast.success('Territory saved'); load(); })}>Add territory</Button>
          <ul className="mt-2 text-sm">{territories.map((t) => <li key={t.id}>{t.name}: {t.countries}</li>)}</ul>
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            <Input value={queueName} onChange={(e) => setQueueName(e.target.value)} placeholder="Queue name" />
            <Input value={queueMembers} onChange={(e) => setQueueMembers(e.target.value)} placeholder="Member user ids, comma separated" />
          </div>
          <Button className="mt-3" variant="outline" onClick={() => saveQueue({
            name: queueName,
            memberUserIds: queueMembers.split(',').map((s) => s.trim()).filter(Boolean),
          }).then(() => { toast.success('Queue saved'); load(); })}>Add queue</Button>
          <ul className="mt-2 text-sm">{queues.map((q) => <li key={q.id}>{q.name} · {q.memberUserIds.length} people</li>)}</ul>
        </Section>

        <Section title="Public capture form">
          <div className="flex gap-2">
            <Input value={formName} onChange={(e) => setFormName(e.target.value)} />
            <Button onClick={() => createCaptureForm(formName).then(() => { toast.success('Form created'); load(); })}>Create</Button>
          </div>
          <ul className="mt-3 space-y-1 text-sm">
            {forms.map((form) => (
              <li key={form.id}>
                {form.name} · {window.location.origin}{form.publicPath}
              </li>
            ))}
          </ul>
        </Section>

        <Section title="Conversion report">
          <ReportTable title="By source" rows={report?.bySource ?? []} />
          <ReportTable title="By owner" rows={report?.byOwner ?? []} />
        </Section>
      </main>
    </div>
  );

  function updateRule(index: number, patch: Partial<ScoringRule>) {
    setRules(rules.map((rule, i) => i === index ? { ...rule, ...patch } : rule));
  }
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5">
      <h2 className="mb-3 text-base font-semibold text-slate-900">{title}</h2>
      {children}
    </section>
  );
}

function ReportTable({ title, rows }: { title: string; rows: ConversionReport['bySource'] }) {
  return (
    <div className="mb-4">
      <h3 className="mb-2 text-sm font-medium text-slate-700">{title}</h3>
      <table className="w-full text-left text-sm">
        <thead className="text-xs text-slate-500">
          <tr>
            <th>Group</th><th>Created</th><th>Qualified</th><th>Converted</th><th>Rate</th><th>Hours to first reply</th><th>Hours to convert</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className="border-t border-slate-100">
              <td className="py-1">{row.key}</td>
              <td>{row.created}</td>
              <td>{row.qualified}</td>
              <td>{row.converted}</td>
              <td>{Math.round(row.conversionRate * 100)}%</td>
              <td>{row.avgHoursToFirstResponse ?? '—'}</td>
              <td>{row.avgHoursToConvert ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
