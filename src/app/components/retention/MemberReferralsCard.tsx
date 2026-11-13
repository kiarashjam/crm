// Referrals and sponsorship on a member's page: who referred them (a member, set here or carried
// from their application), who proposed and seconded them, whom they have brought in, proposed
// and seconded, the referral bonuses, and whether they may sponsor by the club's rule.
// club.members names the referrer and gives a bonus the club offers (a credit also takes
// money.manage, which the server checks). Nothing shows while the API is not connected.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Gift, Handshake, Loader2, UserCheck, UserX } from 'lucide-react';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { PERMISSION } from '@/app/api/organizations';
import { awardReferralBonus, setReferrer, type ReferralBonus, type ReferralPerson } from '@/app/api/retention';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { Button } from '@/app/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { useClubMembers, useMemberReferrals, useRetentionSettings } from '@/app/hooks/queries';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { useFormat } from '@/app/i18n';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function PersonLink({ person }: { person?: ReferralPerson }) {
  if (!person) return <span className="text-slate-500">Nobody named</span>;
  return <Link to={`/members/${person.memberId}`} className="font-medium text-slate-900 hover:underline">{person.name}</Link>;
}

export function MemberReferralsCard({ memberId }: { memberId: string }) {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const f = useFormat();
  const { locale } = useRegional();
  const referrals = useMemberReferrals(memberId);
  const [picking, setPicking] = useState(false);
  const members = useClubMembers();
  // Whether the club holds sponsors to a rule: without one, standing is nobody's business.
  const settings = useRetentionSettings();
  const key = queryKeys.referrals.member(scope, memberId);

  const save = useMutation({
    mutationFn: (referrerId: string | null) => setReferrer(memberId, referrerId),
    onSuccess: (saved) => {
      client.setQueryData(key, saved);
      setPicking(false);
      toast.success(saved.referredBy ? `${saved.referredBy.name} is named as the member who referred them.` : 'No referrer is named now.');
    },
    onError: (err) => toast.error(message(err, 'The referrer could not be saved.')),
  });
  const award = useMutation({
    mutationFn: () => awardReferralBonus(memberId),
    onSuccess: (bonus) => {
      void client.invalidateQueries({ queryKey: key });
      toast.success(bonus.kind === 'credit'
        ? `${bonus.referrerName} was given a credit of ${formatClubMoney(bonus.amount, bonus.currency, locale)}.`
        : `${bonus.referrerName} was given ${bonus.points} points.`);
    },
    onError: (err) => {
      // Someone gave it, or is giving it, at the same moment: show theirs rather than the button.
      if (err instanceof ApiError && err.status === 409) void client.invalidateQueries({ queryKey: key });
      toast.error(message(err, 'The bonus could not be given.'));
    },
  });

  if (servesDemoData()) return null;
  const r = referrals.data;
  const bonusText = (b: ReferralBonus) =>
    `${b.kind === 'credit' ? formatClubMoney(b.amount, b.currency, locale) : `${b.points} points`}`
    + `${b.status === 'awarded' ? `, given ${b.awardedAtUtc ? f.date(b.awardedAtUtc) : ''}` : b.status === 'pending' ? ', waiting' : ', not given'}`;
  const choices = (members.data ?? []).filter((m) => m.id !== memberId);

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <h3 className="mb-3 inline-flex items-center gap-2 font-semibold text-slate-900">
        <Handshake className="h-4 w-4" aria-hidden /> Referrals &amp; sponsors
      </h3>
      {referrals.isError ? (
        <LoadFailed what="Their referrals" error={referrals.error} onRetry={() => { void referrals.refetch(); }} retrying={referrals.isFetching} />
      ) : !r ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
      ) : (
        <div className="space-y-3 text-sm">
          <dl className="space-y-1.5">
            <div className="flex justify-between gap-2">
              <dt className="text-slate-500">Referred by</dt>
              <dd className="text-right"><PersonLink person={r.referredBy} /></dd>
            </div>
            {(r.proposedBy || r.secondedBy) && (
              <>
                <div className="flex justify-between gap-2">
                  <dt className="text-slate-500">Proposed by</dt>
                  <dd className="text-right"><PersonLink person={r.proposedBy} /></dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-slate-500">Seconded by</dt>
                  <dd className="text-right"><PersonLink person={r.secondedBy} /></dd>
                </div>
              </>
            )}
          </dl>

          <WriteOnly permission={PERMISSION.ClubMembers}>
            {picking ? (
              <div className="flex flex-wrap items-center gap-2">
                <Select onValueChange={(id) => save.mutate(id)} disabled={save.isPending || members.isLoading}>
                  <SelectTrigger className="h-8 w-56" aria-label="Member who referred them">
                    <SelectValue placeholder={members.isLoading ? 'Loading members…' : 'Choose the member'} />
                  </SelectTrigger>
                  <SelectContent>
                    {choices.map((m) => <SelectItem key={m.id} value={m.id}>{`${m.firstName} ${m.lastName}`.trim()}</SelectItem>)}
                  </SelectContent>
                </Select>
                <Button type="button" size="sm" variant="ghost" onClick={() => setPicking(false)}>Cancel</Button>
              </div>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button type="button" size="sm" variant="outline" onClick={() => setPicking(true)}>
                  <UserCheck className="h-3.5 w-3.5" aria-hidden /> {r.referredBy ? 'Change referrer' : 'Name a referrer'}
                </Button>
                {r.referredBy && (
                  <Button type="button" size="sm" variant="ghost" disabled={save.isPending}
                    onClick={() => { if (window.confirm(`Take ${r.referredBy!.name} off as the member who referred them?`)) save.mutate(null); }}>
                    <UserX className="h-3.5 w-3.5" aria-hidden /> Remove
                  </Button>
                )}
              </div>
            )}
          </WriteOnly>

          {r.bonusForThem && (
            <p className="text-xs text-slate-600">
              Referral bonus for {r.bonusForThem.referrerName}: {bonusText(r.bonusForThem)}
              {r.bonusForThem.reason ? `. ${r.bonusForThem.reason}` : '.'}
            </p>
          )}
          {r.bonusAvailable && (
            <WriteOnly permission={PERMISSION.ClubMembers}>
              <Button type="button" size="sm" variant="outline" onClick={() => award.mutate()} disabled={award.isPending}>
                {award.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Gift className="h-3.5 w-3.5" aria-hidden />}
                Give {r.referredBy?.name ?? 'their referrer'} the referral bonus
              </Button>
            </WriteOnly>
          )}

          {r.referred.length > 0 && (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">They brought in</p>
              <ul className="mt-1 space-y-0.5">
                {r.referred.map((p) => (
                  <li key={p.memberId} className="flex justify-between gap-2">
                    <PersonLink person={p} />
                    <span className="text-xs text-slate-500">{p.status} · joined {f.date(p.joinedAtUtc)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
          {(r.proposed.length > 0 || r.seconded.length > 0) && (
            <p className="text-xs text-slate-600">
              Proposed {r.proposed.length} {r.proposed.length === 1 ? 'applicant' : 'applicants'}, seconded {r.seconded.length}.
            </p>
          )}
          {r.bonusesEarned.length > 0 && (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Bonuses earned</p>
              <ul className="mt-1 space-y-0.5 text-xs text-slate-700">
                {r.bonusesEarned.map((b) => <li key={b.id}>For {b.referredName}: {bonusText(b)}</li>)}
              </ul>
            </div>
          )}
          {r.standing && settings.data && settings.data.sponsorRule !== 'off' && (
            <p className={r.standing.inGoodStanding ? 'text-xs text-emerald-700' : 'text-xs text-amber-800'}>
              {r.standing.inGoodStanding
                ? 'May propose or second applicants by the club’s rule.'
                : `May not propose or second now: ${r.standing.reasons.join('; ')}.`}
            </p>
          )}
        </div>
      )}
    </div>
  );
}
