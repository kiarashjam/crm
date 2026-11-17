// The club's member portal, in Settings > Welcome & portal: turning it on (settings.manage),
// how close to the start members may still cancel there, its address for the club's website,
// inviting members (club.members), and who has used it, with a way to sign someone out.
//
// It starts off in every workspace. When the workspace also hands new members to its own
// portal (the Welcome handoff view of this tab), this says so plainly: members would then have
// two places to sign in.

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { AlertTriangle, Clock, Copy, DoorOpen, ExternalLink, Globe, Loader2, Lock, Mail, Search, Send, UserX, Users } from 'lucide-react';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import {
  endMemberPortalSessions, getMemberPortalSettings, getMemberPortalUsage, inviteAllToMemberPortal, inviteToMemberPortal,
  saveMemberPortalSettings, type InviteAllResult, type MemberPortalSettings, type MemberPortalUse,
} from '@/app/api/memberPortal';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { cn } from '@/app/components/ui/utils';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useFormat } from '@/app/i18n';
import { HelpTip, SettingsCard, StatusPill } from './SettingsCard';

const SHOWN = 100;

/** What inviting everyone did, in one sentence or two. */
export function inviteAllSummary(r: InviteAllResult): string {
  const parts = [`Invited ${r.invited} member${r.invited === 1 ? '' : 's'}.`];
  const left: string[] = [];
  if (r.alreadyInvited > 0) left.push(`${r.alreadyInvited} invited before`);
  if (r.noEmail > 0) left.push(`${r.noEmail} without an email address`);
  if (r.refused > 0) left.push(`${r.refused} whose address bounced or unsubscribed`);
  if (left.length > 0) parts.push(`Left out: ${left.join(', ')}.`);
  if (r.remaining > 0) parts.push(`${r.remaining} more to invite: press Invite again to carry on.`);
  return parts.join(' ');
}

export function MemberPortalSettingsCard() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const f = useFormat();
  const canManage = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
  const canInvite = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ClubMembers);
  const live = !servesDemoData();
  const settingsKey = queryKeys.club.portalSettings(scope);
  const usageKey = queryKeys.club.portalUsage(scope);
  const settings = useQuery({ queryKey: settingsKey, queryFn: ({ signal }) => getMemberPortalSettings({ signal }), enabled: live });
  const usage = useQuery({ queryKey: usageKey, queryFn: ({ signal }) => getMemberPortalUsage({ signal }), enabled: live });
  const [hours, setHours] = useState('');
  const [search, setSearch] = useState('');
  const [inviteResult, setInviteResult] = useState<string | null>(null);

  useEffect(() => {
    if (settings.data) setHours(String(settings.data.memberCancelHours));
  }, [settings.data]);

  const save = useMutation({
    mutationFn: (change: { enabled?: boolean; memberCancelHours?: number }) => saveMemberPortalSettings(change, settings.data?.version),
    onSuccess: (saved: MemberPortalSettings, change) => {
      client.setQueryData(settingsKey, saved);
      if (change.enabled !== undefined) {
        toast.success(saved.enabled ? 'The member portal is on.' : 'The member portal is off, and every member was signed out of it.');
        void client.invalidateQueries({ queryKey: usageKey });
      } else {
        toast.success('Saved.');
      }
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error('Someone else changed these settings meanwhile. They are shown as they are now: make your change again.');
        void settings.refetch();
        return;
      }
      toast.error(err instanceof Error ? err.message : 'The setting could not be saved.');
    },
  });

  const inviteAll = useMutation({
    mutationFn: inviteAllToMemberPortal,
    onSuccess: (r) => {
      setInviteResult(inviteAllSummary(r));
      void client.invalidateQueries({ queryKey: usageKey });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'The invitations could not be sent.'),
  });

  const putUse = (use: MemberPortalUse) =>
    client.setQueryData<MemberPortalUse[]>(usageKey, (list) => list?.map((u) => (u.memberId === use.memberId ? use : u)));

  const invite = useMutation({
    mutationFn: (memberId: string) => inviteToMemberPortal(memberId),
    onSuccess: (use) => {
      putUse(use);
      toast.success(`Invitation sent to ${use.email ?? use.name}.`);
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'The invitation could not be sent.'),
  });

  const revoke = useMutation({
    mutationFn: (memberId: string) => endMemberPortalSessions(memberId),
    onSuccess: (use) => {
      putUse(use);
      toast.success(`${use.name} is signed out of the member portal everywhere.`);
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'They could not be signed out.'),
  });

  const people = useMemo(() => usage.data ?? [], [usage.data]);
  const matching = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? people.filter((p) => p.name.toLowerCase().includes(q) || (p.email ?? '').toLowerCase().includes(q)) : people;
  }, [people, search]);

  if (!live) return null;

  const s = settings.data;
  const signedIn = people.filter((p) => p.firstSignInAtUtc).length;
  const invited = people.filter((p) => p.invitedAtUtc).length;

  const toggle = (on: boolean) => {
    if (!on && !window.confirm('Turn the member portal off? Every member signed in is signed out, and sign-in links already sent stop working.')) return;
    save.mutate({ enabled: on });
  };

  const saveHours = () => {
    const n = Number(hours);
    if (!Number.isInteger(n) || n < 0 || n > 720) {
      toast.error('Members may cancel up to between 0 and 720 hours (30 days) before.');
      return;
    }
    save.mutate({ memberCancelHours: n });
  };

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success('Copied');
    } catch {
      toast.error('Could not copy');
    }
  };

  const switchAside = s && (
    <>
      <StatusPill tone={s.enabled ? 'good' : 'idle'}>{s.enabled ? 'On' : 'Off'}</StatusPill>
      <Switch
        checked={s.enabled}
        onCheckedChange={toggle}
        disabled={!canManage || save.isPending}
        aria-label="Members can sign in to the member portal"
      />
    </>
  );

  return (
    <div className="space-y-5">
      <SettingsCard
        icon={DoorOpen}
        tone="pink"
        title={(
          <>
            Member portal
            <HelpTip label="About the member portal">
              Your members sign in with a link emailed to them, no password, to see their statements and pay, sign up for events,
              book, and choose what they hear from you, in French, German or English. Each member sees only their own account
              (and, for a household&apos;s primary member, the household&apos;s).
            </HelpTip>
          </>
        )}
        description="Members sign in by emailed link to pay, book and sign up."
        aside={switchAside}
      >
        {settings.isError ? (
          <LoadFailed what="The member portal's settings" error={settings.error} onRetry={() => void settings.refetch()} retrying={settings.isFetching} />
        ) : !s ? (
          <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
        ) : (
          <div className="space-y-4">
            {s.handsOverToOwnPortal && (
              <div role="status" className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
                <p>
                  {s.enabled ? (
                    <><strong>Your members have two places to sign in.</strong> This workspace also hands new members to your own portal, which sends its own welcome. Tell them which to use, or turn one off.</>
                  ) : (
                    <><strong>This workspace hands new members to your own portal.</strong> Turning this on gives them a second place to sign in.</>
                  )}
                </p>
              </div>
            )}

            {!canManage && (
              <p className="flex items-center gap-2 text-xs text-slate-500">
                <Lock className="h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />
                Turning the portal on or off and its rules take the Manage settings permission.
              </p>
            )}

            <div className="grid gap-3 md:grid-cols-2">
              <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                <div className="flex items-center gap-1.5">
                  <Clock className="h-4 w-4 text-slate-400" aria-hidden />
                  <Label htmlFor="portal-cancel-hours">Members can cancel up to</Label>
                  <HelpTip label="About cancelling in the portal">
                    For a place on an event and a booking. Closer to the start, they ask you. Members book only the resources you open to
                    them (Reservations, Resources: members can book it in the portal).
                  </HelpTip>
                </div>
                <div className="mt-1.5 flex flex-wrap items-center gap-2">
                  <Input
                    id="portal-cancel-hours"
                    type="number"
                    min={0}
                    max={720}
                    value={hours}
                    onChange={(e) => setHours(e.target.value)}
                    disabled={!canManage}
                    className="w-20 bg-white text-center tabular-nums"
                  />
                  <span className="text-sm text-slate-600">hours before the start</span>
                  {canManage && hours !== String(s.memberCancelHours) && (
                    <Button type="button" size="sm" onClick={saveHours} disabled={save.isPending}>Save</Button>
                  )}
                </div>
              </div>
              {s.portalUrl && (
                <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <div className="flex h-7 items-center gap-1.5">
                    <Globe className="h-4 w-4 text-slate-400" aria-hidden />
                    <Label htmlFor="portal-url-copy">Portal address</Label>
                  </div>
                  <div className="mt-1.5 flex gap-2">
                    <Input id="portal-url-copy" readOnly value={s.portalUrl} className="bg-white font-mono text-xs" />
                    <Button type="button" variant="outline" onClick={() => void copy(s.portalUrl!)}><Copy className="h-4 w-4" aria-hidden /> Copy</Button>
                    {s.enabled && (
                      <Button asChild variant="outline" size="icon" aria-label="Open the member portal">
                        <a href={s.portalUrl} target="_blank" rel="noreferrer"><ExternalLink className="h-4 w-4" aria-hidden /></a>
                      </Button>
                    )}
                  </div>
                  <p className="mt-1.5 text-xs text-slate-500">
                    {s.enabled ? 'Put it on your website: members ask for their link there.' : 'It opens once the portal is on.'}
                  </p>
                </div>
              )}
            </div>
          </div>
        )}
      </SettingsCard>

      {s && (
        <SettingsCard
          icon={Users}
          tone="violet"
          title="Members"
          description="Who was invited, and who has signed in."
          aside={canInvite && s.enabled && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={inviteAll.isPending}
              onClick={() => {
                if (window.confirm('Email an invitation to every member with an email address who has not been invited yet? It is sent from your Welcome & portal sender, in each member’s language.')) inviteAll.mutate();
              }}
            >
              {inviteAll.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Send className="h-4 w-4" aria-hidden />} Invite every member
            </Button>
          )}
        >
          <div className="space-y-4">
            {usage.data && (
              <dl aria-label="Members at a glance" className="grid grid-cols-3 gap-2 sm:gap-3">
                <Stat label="Members" value={people.length} />
                <Stat label="Invited" value={invited} of={people.length} tone="bg-sky-500" />
                <Stat label="Signed in" value={signedIn} of={people.length} tone="bg-emerald-500" />
              </dl>
            )}
            {inviteResult && <p role="status" className="rounded-xl bg-slate-50 p-3 text-sm text-slate-700">{inviteResult}</p>}
            {!s.enabled && <p className="text-sm text-slate-500">Members can be invited once the portal is on.</p>}

            {usage.isError ? (
              <LoadFailed what="Who has used the member portal" error={usage.error} onRetry={() => void usage.refetch()} retrying={usage.isFetching} />
            ) : !usage.data ? (
              <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
            ) : people.length === 0 ? (
              <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-slate-200 p-8 text-center">
                <Users className="h-6 w-6 text-slate-300" aria-hidden />
                <p className="text-sm text-slate-500">No members yet.</p>
              </div>
            ) : (
              <>
                <div className="relative max-w-xs">
                  <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
                  <Input
                    type="search"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Find a member"
                    aria-label="Find a member"
                    className="pl-9"
                  />
                </div>
                <div className="-mx-4 overflow-x-auto sm:mx-0">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 text-left text-xs text-slate-500">
                        <th scope="col" className="py-2 pl-4 pr-3 font-medium sm:pl-0">Member</th>
                        <th scope="col" className="py-2 pr-3 font-medium">Portal</th>
                        <th scope="col" className="hidden py-2 pr-3 font-medium md:table-cell">Last signed in</th>
                        <th scope="col" className="hidden py-2 pr-3 font-medium md:table-cell">Devices</th>
                        <th scope="col" className="py-2 pr-4 font-medium sm:pr-0"><span className="sr-only">Actions</span></th>
                      </tr>
                    </thead>
                    <tbody>
                      {matching.slice(0, SHOWN).map((p) => (
                        <tr key={p.memberId} className="border-b border-slate-100 transition-colors last:border-0 hover:bg-slate-50/60 dark:border-white/5">
                          <td className="py-2.5 pl-4 pr-3 sm:pl-0">
                            <div className="flex items-center gap-3">
                              <span aria-hidden className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-pink-100 to-orange-100 text-xs font-semibold text-pink-700 dark:from-pink-500/20 dark:to-orange-500/20 dark:text-pink-200">
                                {initials(p.name)}
                              </span>
                              <div className="min-w-0">
                                <span className="block truncate font-medium text-slate-900">{p.name}</span>
                                <span className="block truncate text-xs text-slate-500">
                                  {p.email ?? 'No email address'}
                                  {p.fromHandoff && ' · also in your own portal'}
                                </span>
                              </div>
                            </div>
                          </td>
                          <td className="whitespace-nowrap py-2.5 pr-3">
                            {p.firstSignInAtUtc ? (
                              <StatusPill tone="good">Signed in</StatusPill>
                            ) : p.invitedAtUtc ? (
                              <StatusPill tone="info">
                                Invited {f.date(p.invitedAtUtc)}{p.invitationsSent > 1 && ` (${p.invitationsSent}×)`}
                              </StatusPill>
                            ) : (
                              <StatusPill tone="idle">Not invited</StatusPill>
                            )}
                          </td>
                          <td className="hidden whitespace-nowrap py-2.5 pr-3 text-slate-600 md:table-cell">{p.lastSeenAtUtc ? f.dateTime(p.lastSeenAtUtc) : 'Never'}</td>
                          <td className="hidden py-2.5 pr-3 text-slate-600 md:table-cell">
                            {p.activeSessions > 0 ? `${p.activeSessions} device${p.activeSessions === 1 ? '' : 's'}` : '—'}
                          </td>
                          <td className="py-2.5 pr-4 text-right sm:pr-0">
                            <div className="flex justify-end gap-1.5">
                              {canInvite && s.enabled && p.canInvite && (
                                <Button
                                  type="button"
                                  variant="outline"
                                  size="sm"
                                  disabled={invite.isPending && invite.variables === p.memberId}
                                  onClick={() => invite.mutate(p.memberId)}
                                >
                                  <Mail className="h-3.5 w-3.5" aria-hidden /> {p.invitedAtUtc ? 'Invite again' : 'Invite'}
                                </Button>
                              )}
                              {canInvite && p.activeSessions > 0 && (
                                <Button
                                  type="button"
                                  variant="outline"
                                  size="sm"
                                  disabled={revoke.isPending && revoke.variables === p.memberId}
                                  onClick={() => {
                                    if (window.confirm(`Sign ${p.name} out of the member portal on every device? Sign-in links they have not used stop working too.`)) revoke.mutate(p.memberId);
                                  }}
                                >
                                  <UserX className="h-3.5 w-3.5" aria-hidden /> Sign out
                                </Button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {matching.length > SHOWN && (
                  <p className="text-xs text-slate-500">Showing {SHOWN} of {matching.length}. Find a member by name or email to see others.</p>
                )}
              </>
            )}
          </div>
        </SettingsCard>
      )}
    </div>
  );
}

/** "Anna Keller" → "AK"; one name, its first two letters. */
function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0] ?? '';
  const last = parts.length > 1 ? parts[parts.length - 1] ?? '' : '';
  return (last ? (first[0] ?? '') + (last[0] ?? '') : first.slice(0, 2)).toUpperCase() || '?';
}

/** A count, and for a share of the members, a bar of how far along it is. */
function Stat({ label, value, of, tone }: { label: string; value: number; of?: number; tone?: string }) {
  const share = of ? Math.round((value / of) * 100) : 0;
  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd className="mt-0.5 text-2xl font-semibold tabular-nums text-slate-900">
        {value}
        {of !== undefined && <span className="ml-1 text-sm font-normal text-slate-400">/ {of}</span>}
      </dd>
      {of !== undefined && (
        <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-200 dark:bg-white/10" aria-hidden>
          <div className={cn('h-full rounded-full transition-[width] duration-500', tone)} style={{ width: `${share}%` }} />
        </div>
      )}
    </div>
  );
}
