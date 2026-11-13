// The club's retention and referral rules, as the server applies them: how much each sign
// weighs in a member's at-risk score and where at risk begins, what happens when a member
// crosses into it (a task for their owner, an email through the outbox, the automation
// trigger), the referral bonus, and the rule proposers and seconders are held to. Saved against
// the version read: a save from an older copy is refused (409) and the latest is offered.
// settings.manage changes them and scores everyone now; everyone else reads them.

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Loader2, Play, Save } from 'lucide-react';
import { PERMISSION } from '@/app/api/organizations';
import {
  runScoringNow, saveRetentionSettings, type EmailPurpose, type ReferralBonusKind, type RetentionSettings, type SponsorRule,
} from '@/app/api/retention';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { useRetentionSettings, useScoringRuns } from '@/app/hooks/queries';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useMay } from '@/app/hooks/useMay';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { useFormat } from '@/app/i18n';

const WEIGHTS: { key: keyof RetentionSettings; label: string; hint: string }[] = [
  { key: 'visitWeight', label: 'Visits', hint: 'Coming less than they used to.' },
  { key: 'spendWeight', label: 'Spending', hint: 'Spending less than they used to (dues left out).' },
  { key: 'balanceWeight', label: 'What they owe', hint: 'Overdue charges, and the reminders they took.' },
  { key: 'eventWeight', label: 'Events', hint: 'Fewer events than before, and no-shows.' },
  { key: 'concernWeight', label: 'Complaints', hint: 'Open complaints and concerns.' },
  { key: 'tenureWeight', label: 'New members', hint: 'Members leave most in their first two years.' },
];

const toInt = (text: string) => {
  const n = Number.parseInt(text, 10);
  return Number.isFinite(n) ? n : 0;
};

export function RetentionSettingsPanel() {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const may = useMay();
  const f = useFormat();
  const canChange = may(PERMISSION.ManageSettings);
  const settings = useRetentionSettings();
  const runs = useScoringRuns();
  const [draft, setDraft] = useState<RetentionSettings | null>(null);

  // A fresh copy from the server (a save, a reload after someone else's) replaces the draft.
  useEffect(() => {
    if (settings.data) setDraft(settings.data);
  }, [settings.data]);

  const save = useMutation({
    mutationFn: (next: RetentionSettings) => {
      const { currency: _currency, updatedAtUtc: _at, updatedByName: _by, ...change } = next;
      return saveRetentionSettings({
        ...change,
        saveEmailSubject: change.saveEmailSubject ?? '',
        saveEmailBody: change.saveEmailBody ?? '',
      });
    },
    onSuccess: (saved) => {
      client.setQueryData(queryKeys.retention.settings(scope), saved);
      toast.success('Retention and referral rules saved.');
    },
    onError: (err) => {
      if (isEditConflict(err)) {
        showEditConflict(err.message, () => { void settings.refetch(); });
        return;
      }
      toast.error(err instanceof Error && err.message ? err.message : 'The rules were not saved.');
    },
  });
  const run = useMutation({
    mutationFn: () => runScoringNow(),
    onSuccess: (r) => {
      void runs.refetch();
      toast.success(r.status === 'failed'
        ? `The scoring stopped: ${r.failure ?? 'see the runs below'}`
        : `${r.membersScored} members scored: ${r.becameAtRisk} newly at risk, ${r.noLongerAtRisk} no longer.`);
    },
    onError: (err) => toast.error(err instanceof Error && err.message ? err.message : 'The scoring did not run.'),
  });

  if (settings.isError && !draft) {
    return <LoadFailed what="The retention and referral rules" error={settings.error} onRetry={() => { void settings.refetch(); }} retrying={settings.isFetching} />;
  }
  if (!draft) {
    return <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>;
  }
  const set = (patch: Partial<RetentionSettings>) => setDraft({ ...draft, ...patch });
  const total = WEIGHTS.reduce((sum, w) => sum + (draft[w.key] as number), 0);
  const ro = !canChange;

  return (
    <div className="space-y-4">
      {draft.version == null && (
        <p className="max-w-3xl text-sm text-slate-500">
          The club has not saved its rules yet: these are the defaults, with every action off and no referral bonus.
        </p>
      )}

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="retention-weights">
        <h3 id="retention-weights" className="font-semibold text-slate-900">How the at-risk score is weighed</h3>
        <p className="mt-1 max-w-3xl text-sm text-slate-500">
          Each member is scored every morning from 0 to 100, each sign against the member&apos;s own past. A sign weighed 0 does not count.
        </p>
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {WEIGHTS.map((w) => (
            <div key={w.key}>
              <Label htmlFor={`weight-${w.key}`}>{w.label}</Label>
              <Input id={`weight-${w.key}`} type="number" min={0} max={100} disabled={ro}
                value={String(draft[w.key] as number)} onChange={(e) => set({ [w.key]: toInt(e.target.value) } as Partial<RetentionSettings>)} />
              <p className="mt-1 text-xs text-slate-500">
                {w.hint} {total > 0 ? `${Math.round(((draft[w.key] as number) / total) * 100)}% of the score.` : ''}
              </p>
            </div>
          ))}
          <div>
            <Label htmlFor="threshold-at-risk">At risk from</Label>
            <Input id="threshold-at-risk" type="number" min={1} max={100} disabled={ro}
              value={String(draft.atRiskThreshold)} onChange={(e) => set({ atRiskThreshold: toInt(e.target.value) })} />
          </div>
          <div>
            <Label htmlFor="threshold-watch">Watch from</Label>
            <Input id="threshold-watch" type="number" min={0} max={99} disabled={ro}
              value={String(draft.watchThreshold)} onChange={(e) => set({ watchThreshold: toInt(e.target.value) })} />
          </div>
        </div>
        {total === 0 && <p className="mt-2 text-sm text-rose-700">At least one sign has to count.</p>}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="retention-actions">
        <h3 id="retention-actions" className="font-semibold text-slate-900">When a member becomes at risk</h3>
        <p className="mt-1 max-w-3xl text-sm text-slate-500">
          Once each time they cross into at risk (not on their first score), and not again within the cooldown. The automation trigger
          &ldquo;When a member becomes at risk&rdquo; runs too, for any rule the club adds on it.
        </p>
        <div className="mt-4 space-y-4">
          <div className="flex flex-wrap items-center gap-3">
            <Switch id="save-task" checked={draft.saveTask} onCheckedChange={(v) => set({ saveTask: v })} disabled={ro} />
            <Label htmlFor="save-task">A task for the member&apos;s owner, due in</Label>
            <Input aria-label="Days until the task is due" type="number" min={0} max={60} className="h-9 w-20" disabled={ro || !draft.saveTask}
              value={String(draft.saveTaskDueDays)} onChange={(e) => set({ saveTaskDueDays: toInt(e.target.value) })} />
            <span className="text-sm text-slate-600">days</span>
          </div>
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-3">
              <Switch id="save-email" checked={draft.saveEmail} onCheckedChange={(v) => set({ saveEmail: v })} disabled={ro} />
              <Label htmlFor="save-email">An email to the member, sent as</Label>
              <Select value={draft.saveEmailPurpose} onValueChange={(v) => set({ saveEmailPurpose: v as EmailPurpose })} disabled={ro || !draft.saveEmail}>
                <SelectTrigger className="h-9 w-56" aria-label="What the email is"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="marketing">Marketing (only with their consent)</SelectItem>
                  <SelectItem value="service">About their membership</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {draft.saveEmail && (
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                <div className="md:col-span-2">
                  <Label htmlFor="save-email-subject">Subject</Label>
                  <Input id="save-email-subject" maxLength={200} disabled={ro} value={draft.saveEmailSubject ?? ''}
                    placeholder="Left empty: the club's own message, in the member's language"
                    onChange={(e) => set({ saveEmailSubject: e.target.value })} />
                </div>
                <div className="md:col-span-2">
                  <Label htmlFor="save-email-body">Message</Label>
                  <Textarea id="save-email-body" rows={4} maxLength={4000} disabled={ro} value={draft.saveEmailBody ?? ''}
                    placeholder="Left empty: the club's own message, in the member's language. {{firstName}}, {{name}} and {{club}} are filled in."
                    onChange={(e) => set({ saveEmailBody: e.target.value })} />
                </div>
              </div>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Label htmlFor="save-cooldown">Not again for the same member within</Label>
            <Input id="save-cooldown" type="number" min={1} max={365} className="h-9 w-24" disabled={ro}
              value={String(draft.saveCooldownDays)} onChange={(e) => set({ saveCooldownDays: toInt(e.target.value) })} />
            <span className="text-sm text-slate-600">days</span>
          </div>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="referral-rules">
        <h3 id="referral-rules" className="font-semibold text-slate-900">Referrals and sponsors</h3>
        <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
          <div>
            <Label>Referral bonus, when the applicant they referred becomes a member</Label>
            <div className="mt-1 flex items-center gap-2">
              <Select value={draft.referralBonus} onValueChange={(v) => set({ referralBonus: v as ReferralBonusKind })} disabled={ro}>
                <SelectTrigger className="h-9 w-44" aria-label="Referral bonus"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">None</SelectItem>
                  <SelectItem value="points">Loyalty points</SelectItem>
                  <SelectItem value="credit">A credit on their account</SelectItem>
                </SelectContent>
              </Select>
              {draft.referralBonus === 'points' && (
                <Input aria-label="Points" type="number" min={1} max={100000} className="h-9 w-28" disabled={ro}
                  value={String(draft.referralBonusPoints)} onChange={(e) => set({ referralBonusPoints: toInt(e.target.value) })} />
              )}
              {draft.referralBonus === 'credit' && (
                <>
                  <Input aria-label="Credit" type="number" min={0.01} step="0.01" className="h-9 w-28" disabled={ro}
                    value={String(draft.referralBonusAmount)} onChange={(e) => set({ referralBonusAmount: Number(e.target.value) || 0 })} />
                  <span className="text-sm text-slate-600">{draft.currency}</span>
                </>
              )}
            </div>
            {draft.referralBonus === 'credit' && (
              <p className="mt-1 text-xs text-slate-500">
                A credit is money: an approval by someone who may not charge accounts leaves it for someone who may. It shows as a correction in the accounting export.
              </p>
            )}
          </div>
          <div>
            <Label>Proposer and seconder</Label>
            <Select value={draft.sponsorRule} onValueChange={(v) => set({ sponsorRule: v as SponsorRule })} disabled={ro}>
              <SelectTrigger className="mt-1 h-9 w-full" aria-label="Sponsor rule"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="off">Not asked for</SelectItem>
                <SelectItem value="optional">Optional, members in good standing</SelectItem>
                <SelectItem value="required">Both required, members in good standing</SelectItem>
              </SelectContent>
            </Select>
            {draft.sponsorRule !== 'off' && (
              <div className="mt-3 space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <Label htmlFor="sponsor-tenure">Members for at least</Label>
                  <Input id="sponsor-tenure" type="number" min={0} max={240} className="h-9 w-20" disabled={ro}
                    value={String(draft.sponsorMinTenureMonths)} onChange={(e) => set({ sponsorMinTenureMonths: toInt(e.target.value) })} />
                  <span className="text-sm text-slate-600">months</span>
                </div>
                <div className="flex items-center gap-2">
                  <Switch id="sponsor-overdue" checked={draft.sponsorNoOverdue} onCheckedChange={(v) => set({ sponsorNoOverdue: v })} disabled={ro} />
                  <Label htmlFor="sponsor-overdue">With nothing overdue</Label>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>

      <WriteOnly permission={PERMISSION.ManageSettings}>
        <div className="flex flex-wrap items-center gap-3">
          <Button type="button" onClick={() => save.mutate(draft)} disabled={save.isPending || total === 0}>
            {save.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Save className="h-4 w-4" aria-hidden />} Save rules
          </Button>
          <Button type="button" variant="outline" onClick={() => run.mutate()} disabled={run.isPending}>
            {run.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Play className="h-4 w-4" aria-hidden />} Score everyone now
          </Button>
          {draft.updatedAtUtc && (
            <span className="text-xs text-slate-500">Last saved {f.dateTime(draft.updatedAtUtc)}{draft.updatedByName ? ` by ${draft.updatedByName}` : ''}.</span>
          )}
        </div>
      </WriteOnly>

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="scoring-runs">
        <h3 id="scoring-runs" className="font-semibold text-slate-900">Scoring runs</h3>
        {runs.isError ? (
          <LoadFailed what="The scoring runs" error={runs.error} onRetry={() => { void runs.refetch(); }} retrying={runs.isFetching} />
        ) : (runs.data ?? []).length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">None yet. The first runs from 05:00 tomorrow, in the club&apos;s time zone.</p>
        ) : (
          <ul className="mt-2 divide-y divide-slate-100 text-sm">
            {(runs.data ?? []).map((r) => (
              <li key={r.id} className="flex flex-wrap justify-between gap-2 py-2">
                <span className="text-slate-800">
                  {f.dateTime(r.startedAtUtc)} · {r.trigger === 'manual' ? 'by hand' : 'daily'}
                  {r.status === 'failed' && <span className="text-rose-700"> · stopped: {r.failure}</span>}
                </span>
                <span className="text-slate-500">
                  {r.membersScored} scored, {r.becameAtRisk} newly at risk, {r.tasksCreated} tasks, {r.emailsQueued} emails
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
