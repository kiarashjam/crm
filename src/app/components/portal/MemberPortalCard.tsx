// A member's use of the member portal, on their page: whether they were invited and when they
// last signed in, with a way to invite them and to sign them out of it everywhere. Inviting and
// signing out take club.members; everyone else reads it. Nothing shows while the API is not
// connected (the demo data has no portal).

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { DoorOpen, Loader2, Mail, UserX } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import {
  endMemberPortalSessions, getMemberPortalSettings, getMemberPortalUse, inviteToMemberPortal, type MemberPortalUse,
} from '@/app/api/memberPortal';
import { PERMISSION } from '@/app/api/organizations';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { Button } from '@/app/components/ui/button';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useFormat } from '@/app/i18n';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export function MemberPortalCard({ memberId }: { memberId: string }) {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const f = useFormat();
  const live = !servesDemoData();
  const useKey = queryKeys.club.portalUse(scope, memberId);
  const settings = useQuery({
    queryKey: queryKeys.club.portalSettings(scope),
    queryFn: ({ signal }) => getMemberPortalSettings({ signal }),
    enabled: live,
  });
  const use = useQuery({ queryKey: useKey, queryFn: ({ signal }) => getMemberPortalUse(memberId, { signal }), enabled: live });

  const took = (saved: MemberPortalUse) => {
    client.setQueryData(useKey, saved);
    void client.invalidateQueries({ queryKey: queryKeys.club.portalUsage(scope) });
  };
  const invite = useMutation({
    mutationFn: () => inviteToMemberPortal(memberId),
    onSuccess: (saved) => {
      took(saved);
      toast.success(`Invitation sent to ${saved.email ?? saved.name}.`);
    },
    onError: (err) => toast.error(message(err, 'The invitation could not be sent.')),
  });
  const revoke = useMutation({
    mutationFn: () => endMemberPortalSessions(memberId),
    onSuccess: (saved) => {
      took(saved);
      toast.success(`${saved.name} is signed out of the member portal everywhere.`);
    },
    onError: (err) => toast.error(message(err, 'They could not be signed out.')),
  });

  if (!live) return null;
  const on = settings.data?.enabled ?? false;
  const u = use.data;

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <h3 className="mb-3 inline-flex items-center gap-2 font-semibold text-slate-900">
        <DoorOpen className="h-4 w-4" aria-hidden /> Member portal
      </h3>
      {use.isError || settings.isError ? (
        <LoadFailed
          what="Their use of the member portal"
          error={use.error ?? settings.error}
          onRetry={() => { void use.refetch(); void settings.refetch(); }}
          retrying={use.isFetching || settings.isFetching}
        />
      ) : !u || !settings.data ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
      ) : (
        <div className="space-y-3 text-sm">
          {!on && (
            <p className="text-slate-600">
              The member portal is off.{' '}
              <Link to="/settings?tab=welcome" className="font-medium text-slate-800 underline underline-offset-2">Welcome &amp; portal</Link>{' '}
              turns it on.
            </p>
          )}
          <dl className="space-y-1.5">
            <div className="flex justify-between gap-2">
              <dt className="text-slate-500">Invited</dt>
              <dd className="font-medium text-slate-900">
                {u.invitedAtUtc ? f.date(u.invitedAtUtc) : 'Not yet'}
                {u.invitationsSent > 1 && <span className="font-normal text-slate-500"> ({u.invitationsSent} times)</span>}
              </dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt className="text-slate-500">Last signed in</dt>
              <dd className="font-medium text-slate-900">{u.lastSeenAtUtc ? f.dateTime(u.lastSeenAtUtc) : 'Never'}</dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt className="text-slate-500">Signed in now on</dt>
              <dd className="font-medium text-slate-900">
                {u.activeSessions > 0 ? `${u.activeSessions} device${u.activeSessions === 1 ? '' : 's'}` : 'No device'}
              </dd>
            </div>
          </dl>
          {u.fromHandoff && (
            <p className="text-xs text-slate-500">They were handed to your own portal from a deal too, so they may sign in there as well.</p>
          )}
          {!u.canInvite && <p className="text-xs text-slate-500">They have no email address to be invited at.</p>}
          <WriteOnly permission={PERMISSION.ClubMembers}>
            <div className="flex flex-wrap gap-2">
              {on && u.canInvite && (
                <Button type="button" variant="outline" size="sm" onClick={() => invite.mutate()} disabled={invite.isPending}>
                  {invite.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Mail className="h-3.5 w-3.5" aria-hidden />}
                  {u.invitedAtUtc ? 'Invite again' : 'Invite'}
                </Button>
              )}
              {u.activeSessions > 0 && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={revoke.isPending}
                  onClick={() => {
                    if (window.confirm(`Sign ${u.name} out of the member portal on every device? Sign-in links they have not used stop working too.`)) revoke.mutate();
                  }}
                >
                  <UserX className="h-3.5 w-3.5" aria-hidden /> Sign out everywhere
                </Button>
              )}
            </div>
          </WriteOnly>
        </div>
      )}
    </div>
  );
}
