// The club's member portal, in Settings > Welcome & portal: turning it on (settings.manage),
// how close to the start members may still cancel there, its address for the club's website,
// inviting members (club.members), and who has used it, with a way to sign someone out.
//
// It starts off in every workspace. When the workspace also hands new members to its own
// portal (the handoff further down this tab), this says so plainly: members would then have
// two places to sign in.

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { AlertTriangle, CheckCircle2, Copy, DoorOpen, Loader2, Lock, Mail, Send, UserX } from 'lucide-react';
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

  return (
    <section aria-labelledby="member-portal-heading" className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5">
      <div className="flex flex-wrap items-start gap-3">
        <div className="min-w-0 flex-1">
          <h3 id="member-portal-heading" className="flex items-center gap-2 text-lg font-semibold text-slate-900">
            <DoorOpen className="h-5 w-5 text-pink-600" aria-hidden /> Member portal
          </h3>
          <p className="mt-1 text-sm text-slate-600">
            Your members sign in with a link emailed to them, no password, to see their statements and pay, sign up for events,
            book, and choose what they hear from you, in French, German or English. Each member sees only their own account
            (and, for a household&apos;s primary member, the household&apos;s).
          </p>
        </div>
        {s && (
          <span className={cn(
            'inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold',
            s.enabled ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500',
          )}>
            {s.enabled ? <CheckCircle2 className="h-3.5 w-3.5" aria-hidden /> : null}
            {s.enabled ? 'On' : 'Off'}
          </span>
        )}
      </div>

      {settings.isError ? (
        <LoadFailed what="The member portal's settings" error={settings.error} onRetry={() => void settings.refetch()} retrying={settings.isFetching} />
      ) : !s ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
      ) : (
        <>
          {s.handsOverToOwnPortal && (
            <div role="status" className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
              <p>
                {s.enabled ? (
                  <><strong>Your members have two places to sign in.</strong> This workspace also hands new members to your own portal (the handoff below), which sends them its own welcome. Tell them which one to use, or turn one of the two off.</>
                ) : (
                  <><strong>This workspace hands new members to your own portal</strong> (the handoff below). Turning the member portal on gives them a second place to sign in: say in your welcome which one to use, or turn the handoff off.</>
                )}
              </p>
            </div>
          )}

          {!canManage && (
            <p className="flex items-start gap-2 text-sm text-slate-600">
              <Lock className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-hidden />
              Turning the portal on or off and its rules take the Manage settings permission.
            </p>
          )}

          <label className="flex items-center gap-3 text-sm text-slate-800">
            <Switch
              checked={s.enabled}
              onCheckedChange={toggle}
              disabled={!canManage || save.isPending}
              aria-label="Members can sign in to the member portal"
            />
            Members can sign in to the member portal
          </label>

          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <Label htmlFor="portal-cancel-hours">Members may cancel there up to</Label>
              <div className="mt-1.5 flex items-center gap-2">
                <Input
                  id="portal-cancel-hours"
                  type="number"
                  min={0}
                  max={720}
                  value={hours}
                  onChange={(e) => setHours(e.target.value)}
                  disabled={!canManage}
                  className="w-24"
                />
                <span className="text-sm text-slate-600">hours before the start</span>
                {canManage && hours !== String(s.memberCancelHours) && (
                  <Button type="button" variant="outline" size="sm" onClick={saveHours} disabled={save.isPending}>Save</Button>
                )}
              </div>
              <p className="mt-1 text-xs text-slate-500">
                For a place on an event and a booking. Closer to the start, they ask you. Members book only the resources you open to
                them (Reservations, Resources: members can book it in the portal).
              </p>
            </div>
            {s.portalUrl && (
              <div>
                <Label htmlFor="portal-url-copy">Its address</Label>
                <div className="mt-1.5 flex gap-2">
                  <Input id="portal-url-copy" readOnly value={s.portalUrl} className="font-mono text-xs" />
                  <Button type="button" variant="outline" onClick={() => void copy(s.portalUrl!)}><Copy className="h-4 w-4" aria-hidden /> Copy</Button>
                </div>
                <p className="mt-1 text-xs text-slate-500">
                  {s.enabled ? 'Put it on your website: members ask for their sign-in link there.' : 'It opens once the portal is on.'}
                </p>
              </div>
            )}
          </div>

          <div className="space-y-3 border-t border-slate-100 pt-4">
            <div className="flex flex-wrap items-center gap-3">
              <div className="min-w-0 flex-1">
                <h4 className="font-medium text-slate-900">Members</h4>
                <p className="text-sm text-slate-500">
                  {usage.data ? `${signedIn} of ${people.length} have signed in; ${invited} invited.` : 'Who was invited, and who has signed in.'}
                </p>
              </div>
              {canInvite && s.enabled && (
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
            </div>
            {inviteResult && <p role="status" className="rounded-lg bg-slate-50 p-3 text-sm text-slate-700">{inviteResult}</p>}
            {!s.enabled && <p className="text-sm text-slate-500">Members can be invited once the portal is on.</p>}

            {usage.isError ? (
              <LoadFailed what="Who has used the member portal" error={usage.error} onRetry={() => void usage.refetch()} retrying={usage.isFetching} />
            ) : !usage.data ? (
              <p className="text-sm text-slate-500">Loading…</p>
            ) : people.length === 0 ? (
              <p className="text-sm text-slate-500">No members yet.</p>
            ) : (
              <>
                <Input
                  type="search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Find a member"
                  aria-label="Find a member"
                  className="max-w-xs"
                />
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 text-left text-xs uppercase tracking-wide text-slate-500">
                        <th scope="col" className="py-2 pr-3 font-medium">Member</th>
                        <th scope="col" className="py-2 pr-3 font-medium">Invited</th>
                        <th scope="col" className="py-2 pr-3 font-medium">Last signed in</th>
                        <th scope="col" className="py-2 pr-3 font-medium">Signed in on</th>
                        <th scope="col" className="py-2 font-medium"><span className="sr-only">Actions</span></th>
                      </tr>
                    </thead>
                    <tbody>
                      {matching.slice(0, SHOWN).map((p) => (
                        <tr key={p.memberId} className="border-b border-slate-100 last:border-0">
                          <td className="py-2 pr-3">
                            <span className="block font-medium text-slate-900">{p.name}</span>
                            <span className="block text-xs text-slate-500">
                              {p.email ?? 'No email address'}
                              {p.fromHandoff && ' · handed to your own portal too'}
                            </span>
                          </td>
                          <td className="whitespace-nowrap py-2 pr-3 text-slate-600">
                            {p.invitedAtUtc ? f.date(p.invitedAtUtc) : '—'}
                            {p.invitationsSent > 1 && <span className="text-xs text-slate-400"> ({p.invitationsSent}×)</span>}
                          </td>
                          <td className="whitespace-nowrap py-2 pr-3 text-slate-600">{p.lastSeenAtUtc ? f.dateTime(p.lastSeenAtUtc) : 'Never'}</td>
                          <td className="py-2 pr-3 text-slate-600">
                            {p.activeSessions > 0 ? `${p.activeSessions} device${p.activeSessions === 1 ? '' : 's'}` : '—'}
                          </td>
                          <td className="py-2 text-right">
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
        </>
      )}
    </section>
  );
}
