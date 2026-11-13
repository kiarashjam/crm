// What the workspace's leads are: people (someone with a name and an email), establishments
// (a business such as a restaurant, with its address and a person to speak to), or both.
// It belongs to the workspace, like its currency: whoever manages its settings sets it,
// and everyone adding a lead sees the same questions. People only is the default, and a
// workspace on it sees nothing of establishments anywhere.

import { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { CheckCircle2, Layers, Loader2, Store, UserRound } from 'lucide-react';
import { toast } from 'sonner';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION, updateOrganizationSettings } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { useModules } from '@/app/hooks/useModules';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { leadKindsMode, type LeadKindsMode } from '@/app/lib/leadKinds';

const OPTIONS: { value: LeadKindsMode; label: string; text: string; icon: typeof UserRound }[] = [
  { value: 'person', label: 'People', text: 'Each lead is a person, with a name and an email.', icon: UserRound },
  {
    value: 'establishment',
    label: 'Establishments',
    text: 'Each lead is a business, such as a restaurant: its address, its type, and a person to speak to.',
    icon: Store,
  },
  { value: 'both', label: 'Both', text: 'Whoever adds a lead says first which it is.', icon: Layers },
];

export function LeadKindsCard() {
  // Optional: the settings page has the workspace, and a test or preview without one shows nothing.
  const orgContext = useOrgOptional();
  const currentOrg = orgContext?.currentOrg ?? null;
  const saved = leadKindsMode(currentOrg?.leadKinds);
  const [value, setValue] = useState<LeadKindsMode>(saved);
  const [saving, setSaving] = useState(false);
  const orgId = currentOrg?.id;
  useEffect(() => setValue(saved), [orgId, saved]);
  const salesOn = useModules().sales;

  // Demo workspaces have no server to keep it; their leads are people. Leads are the Sales
  // module's, so a workspace that has turned Sales off has none to ask about.
  if (!currentOrg || servesDemoData() || !salesOn) return null;
  const canEdit = orgCan(currentOrg, PERMISSION.ManageSettings);

  const save = async () => {
    setSaving(true);
    try {
      await updateOrganizationSettings(currentOrg.id, { leadKinds: value });
      await orgContext?.refreshOrgs();
      toast.success('Saved: new leads are asked for accordingly.');
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The setting was not saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="lead-kinds-settings" className="pt-6 border-t border-slate-200">
      <h3 id="lead-kinds-settings" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <Store className="w-4 h-4" aria-hidden />
        Your leads are
      </h3>
      <p className="text-sm text-slate-600 mb-4">
        What a new lead asks for. Leads already saved keep what they are; changing this only changes what is asked from now on.
      </p>
      <div role="radiogroup" aria-labelledby="lead-kinds-settings" className="grid gap-3 sm:grid-cols-3">
        {OPTIONS.map((o, i) => {
          const selected = value === o.value;
          const Icon = o.icon;
          return (
            <motion.button
              key={o.value}
              type="button"
              role="radio"
              aria-checked={selected}
              disabled={!canEdit || saving}
              onClick={() => setValue(o.value)}
              initial={{ opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: i * 0.05 }}
              className={cn(
                'relative flex flex-col items-start gap-2 rounded-xl border-2 p-4 text-left transition disabled:cursor-not-allowed',
                selected
                  ? 'border-orange-400 bg-gradient-to-br from-orange-50 to-pink-50 shadow-sm'
                  : 'border-slate-200 bg-white hover:border-slate-300',
                !canEdit && !selected && 'opacity-60',
              )}
            >
              <span className={cn(
                'flex h-9 w-9 items-center justify-center rounded-lg',
                selected ? 'bg-gradient-to-br from-orange-500 to-pink-500 text-white' : 'bg-slate-100 text-slate-500',
              )}>
                <Icon className="h-4 w-4" aria-hidden />
              </span>
              <span className="font-medium text-slate-900">{o.label}</span>
              <span className="text-xs text-slate-500">{o.text}</span>
              {selected && <CheckCircle2 className="absolute right-3 top-3 h-4 w-4 text-orange-500" aria-hidden />}
            </motion.button>
          );
        })}
      </div>
      {canEdit ? (
        <div className="mt-4 flex justify-end">
          <Button type="button" onClick={() => void save()} disabled={saving || value === saved}>
            {saving && <Loader2 className="h-4 w-4 animate-spin" />}
            Save
          </Button>
        </div>
      ) : (
        <p className="mt-3 text-xs text-slate-500">Changing this takes the Manage settings permission.</p>
      )}
    </section>
  );
}
