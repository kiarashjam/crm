// The member who referred an applicant, their proposer and their seconder: members of the club,
// picked from the members list rather than a name typed in, so the referral bonus can be given
// and the referrals report can count them. The server holds proposers and seconders to the
// club's rule (and refuses the approval without them when the rule requires both); the hints
// here say beforehand who would be refused, and why.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Loader2 } from 'lucide-react';
import { updateApplication, type Application } from '@/app/api/applications';
import type { Member } from '@/app/api/members';
import { getSponsorStanding } from '@/app/api/retention';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { useClubMembers, useRetentionSettings } from '@/app/hooks/queries';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { isEditConflict } from '@/app/lib/editConflict';

/** The three ids, '' for none. */
export interface Sponsors {
  referredByMemberId: string;
  proposerMemberId: string;
  seconderMemberId: string;
}

export const NO_SPONSORS: Sponsors = { referredByMemberId: '', proposerMemberId: '', seconderMemberId: '' };

export function sponsorsOf(app: Application): Sponsors {
  return {
    referredByMemberId: app.referredByMemberId ?? '',
    proposerMemberId: app.proposerMemberId ?? '',
    seconderMemberId: app.seconderMemberId ?? '',
  };
}

// A select's items cannot have an empty value: "none" stands for nobody.
const NONE = 'none';
const nameOf = (m: Member) => `${m.firstName} ${m.lastName}`.trim();

function MemberPicker({ label, value, onChange, members, exclude }: {
  label: string;
  value: string;
  onChange: (id: string) => void;
  members: Member[];
  exclude: string[];
}) {
  const choices = members.filter((m) => !exclude.includes(m.id) || m.id === value);
  return (
    <div>
      <Label>{label}</Label>
      <Select value={value || NONE} onValueChange={(v) => onChange(v === NONE ? '' : v)}>
        <SelectTrigger aria-label={label}><SelectValue placeholder="Nobody" /></SelectTrigger>
        <SelectContent>
          <SelectItem value={NONE}>Nobody</SelectItem>
          {choices.map((m) => <SelectItem key={m.id} value={m.id}>{nameOf(m)}{m.status !== 'Active' ? ` (${m.status.toLowerCase()})` : ''}</SelectItem>)}
        </SelectContent>
      </Select>
    </div>
  );
}

/** The three pickers, with a line under them for a sponsor the club's rule would refuse. */
export function SponsorFields({ value, onChange, applicantMemberId }: {
  value: Sponsors;
  onChange: (next: Sponsors) => void;
  /** The applicant's own member record, when they have one: nobody sponsors themselves. */
  applicantMemberId?: string | null;
}) {
  const scope = useWorkspaceScope();
  const members = useClubMembers();
  const settings = useRetentionSettings();
  const rule = settings.data?.sponsorRule ?? 'off';
  const sponsors = [value.proposerMemberId, value.seconderMemberId].filter(Boolean);
  const standing = useQuery({
    queryKey: [...queryKeys.referrals.member(scope, 'standing'), sponsors],
    queryFn: ({ signal }) => getSponsorStanding(sponsors, { signal }),
    enabled: rule !== 'off' && sponsors.length > 0,
  });
  const list = (members.data ?? []).filter((m) => m.id !== applicantMemberId);
  const refused = (standing.data ?? []).filter((s) => !s.inGoodStanding);

  return (
    <div className="space-y-3">
      {members.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading members…</p>
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
          <MemberPicker label="Referred by" value={value.referredByMemberId} members={list} exclude={[]}
            onChange={(id) => onChange({ ...value, referredByMemberId: id })} />
          <MemberPicker label="Proposed by" value={value.proposerMemberId} members={list} exclude={[value.seconderMemberId]}
            onChange={(id) => onChange({ ...value, proposerMemberId: id })} />
          <MemberPicker label="Seconded by" value={value.seconderMemberId} members={list} exclude={[value.proposerMemberId]}
            onChange={(id) => onChange({ ...value, seconderMemberId: id })} />
        </div>
      )}
      {rule === 'required' && (!value.proposerMemberId || !value.seconderMemberId) && (
        <p className="text-xs text-slate-500">The club asks for a proposer and a seconder before an application is approved.</p>
      )}
      {refused.map((s) => (
        <p key={s.memberId} className="text-xs text-amber-800">{s.name} may not sponsor by the club&rsquo;s rule: {s.reasons.join('; ')}.</p>
      ))}
    </div>
  );
}

/** Who referred, proposed and seconded an applicant, as chips on the application. */
export function SponsorChips({ app }: { app: Application }) {
  const chip = (label: string, id?: string | null, name?: string | null) =>
    id ? (
      <Link key={label} to={`/members/${id}`} className="rounded-full bg-violet-50 px-2 py-0.5 font-medium text-violet-700 ring-1 ring-violet-200 hover:bg-violet-100">
        {label} {name ?? 'a member'}
      </Link>
    ) : null;
  const chips = [
    chip('Referred by', app.referredByMemberId, app.referredByMemberName),
    chip('Proposed by', app.proposerMemberId, app.proposerName),
    chip('Seconded by', app.seconderMemberId, app.seconderName),
  ].filter(Boolean);
  if (chips.length === 0) return null;
  return <span className="flex flex-wrap items-center gap-1.5 text-xs">{chips}</span>;
}

/** Sets the referrer, proposer and seconder on an application still to be decided. */
export function SponsorsDialog({ app, onClose, onSaved }: { app: Application | null; onClose: () => void; onSaved: () => void }) {
  const [value, setValue] = useState<Sponsors>(NO_SPONSORS);
  const [shownFor, setShownFor] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  if (app && shownFor !== app.id) {
    setShownFor(app.id);
    setValue(sponsorsOf(app));
  }

  const save = async () => {
    if (!app) return;
    setSaving(true);
    try {
      await updateApplication(app.id, { ...value, version: app.version });
      toast.success('Sponsors saved.');
      onSaved();
      onClose();
    } catch (err) {
      toast.error(isEditConflict(err)
        ? 'Someone changed this application after you opened it. It has been reloaded: choose again.'
        : err instanceof Error && err.message ? err.message : 'The sponsors were not saved.');
      if (isEditConflict(err)) onSaved();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={app !== null} onOpenChange={(open) => { if (!open) { setShownFor(null); onClose(); } }}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Referrer and sponsors{app ? ` for ${app.firstName} ${app.lastName}` : ''}</DialogTitle>
          <DialogDescription>
            The member who introduced them earns the club&rsquo;s referral bonus when they become a member. The proposer and seconder vouch for them.
          </DialogDescription>
        </DialogHeader>
        {app && <SponsorFields value={value} onChange={setValue} applicantMemberId={app.memberId} />}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />} Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
