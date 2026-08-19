// Creating a workspace, in the launchpad's own dark register.
//
// Extracted from the page mostly so the page stays readable, but also because the
// submit path has three outcomes worth keeping in one place: demo mode creates
// locally, the API can return null without throwing, and it can throw. All three
// previously funnelled into the same optimistic "Organization created!" toast in
// one branch and an error in another — the null case is the one that used to lie.

import { useEffect, useState } from 'react';
import { Loader2, Plus, Sparkles, Users, Target, Briefcase, BarChart3 } from 'lucide-react';
import { toast } from 'sonner';
import { createOrganization, type Organization } from '@/app/api/organizations';
import { useOrg } from '@/app/contexts/OrgContext';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Dialog, DialogContent, DialogTitle } from '@/app/components/ui/dialog';

const PERKS = [
  { icon: Users, label: 'Its own team and roles' },
  { icon: Target, label: 'Separate leads' },
  { icon: Briefcase, label: 'Separate pipeline' },
  { icon: BarChart3, label: 'Separate reporting' },
];

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (org: Organization) => void;
}

export function CreateWorkspaceDialog({ open, onOpenChange, onCreated }: Props) {
  const [name, setName] = useState('');
  const [creating, setCreating] = useState(false);
  const { addDemoOrg } = useOrg();

  // Reopening with the last failed attempt still in the box is confusing; and a
  // stale name plus a fresh submit is how you create a duplicate by accident.
  useEffect(() => {
    if (!open) setName('');
  }, [open]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = name.trim();
    if (!trimmed || creating) return;

    setCreating(true);
    try {
      const org = addDemoOrg ? addDemoOrg(trimmed) : await createOrganization(trimmed);
      if (!org) {
        toast.error('Could not create the workspace. Please try again.');
        return;
      }
      onCreated(org);
      setName('');
      onOpenChange(false);
    } catch {
      toast.error('Could not create the workspace. Please try again.');
    } finally {
      setCreating(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="overflow-hidden border-white/10 bg-[#0d0f18] p-0 text-white sm:max-w-[460px] [&>button]:text-white/50 [&>button]:hover:text-white">
        <div className="relative overflow-hidden px-6 pt-6 pb-5">
          <span
            aria-hidden
            className="pointer-events-none absolute -top-24 -right-16 h-56 w-56 rounded-full bg-orange-500/25 blur-3xl"
          />
          <div className="relative flex items-center gap-3.5">
            <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-400 to-amber-500 shadow-lg shadow-orange-500/25">
              <Sparkles className="h-5 w-5 text-white" aria-hidden />
            </span>
            <div>
              <DialogTitle className="text-lg font-bold tracking-tight text-white">
                New workspace
              </DialogTitle>
              <p className="mt-0.5 text-sm text-white/45">A separate world for a separate business</p>
            </div>
          </div>
        </div>

        <form onSubmit={handleCreate} className="px-6 pb-6">
          <Label htmlFor="workspace-name" className="text-xs font-semibold tracking-wide text-white/60 uppercase">
            Name
          </Label>
          <Input
            id="workspace-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Pavillon 46, Lac Léman SA…"
            autoComplete="off"
            className="mt-2 h-11 border-white/10 bg-white/[0.04] text-white placeholder:text-white/25 focus-visible:border-orange-400/60 focus-visible:ring-orange-400/20"
            required
          />

          <ul className="mt-5 grid grid-cols-2 gap-2.5">
            {PERKS.map(({ icon: Icon, label }) => (
              <li key={label} className="flex items-center gap-2 text-[13px] text-white/55">
                <Icon className="h-3.5 w-3.5 shrink-0 text-orange-400/80" aria-hidden />
                {label}
              </li>
            ))}
          </ul>

          <p className="mt-5 rounded-xl border border-white/[0.07] bg-white/[0.03] px-3.5 py-3 text-[13px] leading-relaxed text-white/45">
            Nothing is shared between workspaces — not leads, not deals, not people.
            You will land in its settings to invite your team.
          </p>

          <div className="mt-6 flex items-center gap-3">
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              disabled={creating}
              className="h-11 flex-1 border border-white/10 text-white/70 hover:bg-white/5 hover:text-white"
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={creating || !name.trim()}
              className="h-11 flex-[1.6] bg-gradient-to-r from-orange-500 to-amber-500 font-semibold text-white hover:from-orange-400 hover:to-amber-400"
            >
              {creating
                ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> Creating…</>
                : <><Plus className="mr-2 h-4 w-4" aria-hidden /> Create workspace</>}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export default CreateWorkspaceDialog;
