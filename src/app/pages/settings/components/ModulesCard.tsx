// Which halves of Cadence the workspace uses: Sales, Club, or both.
//
// A sales team saw twenty club pages it never uses, and a club saw all the sales
// tooling. Turning a module off takes it out of everyone's menu, its pages say it is
// off, and the server refuses its requests; nothing is deleted, and turning it on again
// brings it all back. The server keeps one module on, and refuses to turn one off while
// work of its own still runs that nobody could then see or stop (the club's automatic
// billing or a scheduled campaign, leads in outreach sequences); its refusal says what to
// stop first, and this card shows it.

import { useState } from 'react';
import { Blocks } from 'lucide-react';
import { toast } from 'sonner';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { MODULE_LABELS, moduleOn, orgCan, PERMISSION, updateWorkspaceModules, type WorkspaceModule } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';

const WHAT: Readonly<Record<WorkspaceModule, string>> = {
  sales: 'Leads, deals and pipelines, outreach sequences, contracts and the sales reports.',
  club: 'Members, households and applications, house accounts and payments, events, bookings, campaigns, the house-charge screen and the POS, and the club reports.',
};

export function ModulesCard() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const [saving, setSaving] = useState<WorkspaceModule | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);

  // Demo workspaces have no server to keep the choice; they have both modules.
  if (!org || !currentOrg || servesDemoData()) return null;
  const { refreshOrgs } = org;
  const canEdit = orgCan(currentOrg, PERMISSION.ManageSettings);
  const on = { sales: moduleOn(currentOrg, 'sales'), club: moduleOn(currentOrg, 'club') };

  const toggle = async (module: WorkspaceModule) => {
    const turningOn = !on[module];
    setSaving(module);
    setRefusal(null);
    try {
      await updateWorkspaceModules(currentOrg.id, { [module]: turningOn });
      await refreshOrgs();
      toast.success(turningOn ? `${MODULE_LABELS[module]} is on again.` : `${MODULE_LABELS[module]} is turned off. Nothing in it was deleted.`);
    } catch (err) {
      const reason = err instanceof Error && err.message ? err.message : `${MODULE_LABELS[module]} could not be changed.`;
      setRefusal(reason);
      toast.error(reason);
    } finally {
      setSaving(null);
    }
  };

  return (
    <section aria-labelledby="workspace-modules" className="pt-6 border-t border-slate-200">
      <h3 id="workspace-modules" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <Blocks className="w-4 h-4" aria-hidden />
        Modules
      </h3>
      <p className="text-sm text-slate-600 mb-4">
        Turn off what this workspace does not use: it leaves everyone&apos;s menu and its pages say it is off.
        Nothing is deleted, and turning it back on brings everything back as it was. Contacts, companies, tasks,
        activities, the calendar and the team belong to both and always stay.
      </p>
      <ul className="grid gap-3 sm:grid-cols-2">
        {(['sales', 'club'] as const).map((module) => {
          const isOn = on[module];
          const lastOn = isOn && !on[module === 'sales' ? 'club' : 'sales'];
          return (
            <li key={module} className="rounded-xl border border-slate-200 p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium text-slate-900">{MODULE_LABELS[module]}</p>
                  <p className="mt-1 text-xs text-slate-600">{WHAT[module]}</p>
                </div>
                <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${isOn ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600'}`}>
                  {isOn ? 'On' : 'Off'}
                </span>
              </div>
              {canEdit && (
                <button
                  type="button"
                  onClick={() => void toggle(module)}
                  disabled={saving !== null || lastOn}
                  title={lastOn ? 'One module always stays on.' : undefined}
                  className="mt-3 h-9 px-4 rounded-lg border border-slate-300 text-sm font-medium text-slate-700 hover:border-slate-400 disabled:opacity-50"
                >
                  {saving === module ? 'Saving…' : isOn ? `Turn ${MODULE_LABELS[module]} off` : `Turn ${MODULE_LABELS[module]} on`}
                </button>
              )}
            </li>
          );
        })}
      </ul>
      {refusal && (
        <p role="alert" className="mt-3 text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">{refusal}</p>
      )}
      {!canEdit && (
        <p className="mt-3 text-xs text-slate-500">Turning modules on or off takes managing the workspace&apos;s settings, which the owner and managers have.</p>
      )}
    </section>
  );
}
