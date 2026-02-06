import { Link } from 'react-router-dom';
import { Building2, UserPlus, Mail, Check, X, Users } from 'lucide-react';
import { toast } from 'sonner';
import { isUsingRealApi } from '@/app/api/apiClient';
import type { InviteDto, JoinRequestDto, OrgMemberDto } from '@/app/api/organizations';

export interface OrganizationSectionProps {
  organizations: { id: string; name: string; isOwner: boolean }[];
  currentOrg: { id: string; name: string; isOwner: boolean } | null;
  setCurrentOrg: (id: string) => void;
  inviteEmail: string;
  setInviteEmail: (val: string) => void;
  handleInvite: (e: React.FormEvent) => void;
  inviting: boolean;
  ownerInvites: InviteDto[];
  ownerJoinRequests: JoinRequestDto[];
  handleAcceptJr: (jr: JoinRequestDto) => void;
  handleRejectJr: (jr: JoinRequestDto) => void;
  actingJrId: string | null;
  orgMembers: OrgMemberDto[];
  handleMemberRoleChange: (member: OrgMemberDto, newRole: number) => void;
  actingMemberId: string | null;
  removeMemberConfirm: OrgMemberDto | null;
  setRemoveMemberConfirm: (m: OrgMemberDto | null) => void;
  handleRemoveMember: (m: OrgMemberDto) => void;
  currentUser: { id: string } | null;
}

export function OrganizationSection({
  organizations,
  currentOrg,
  setCurrentOrg,
  inviteEmail,
  setInviteEmail,
  handleInvite,
  inviting,
  ownerInvites,
  ownerJoinRequests,
  handleAcceptJr,
  handleRejectJr,
  actingJrId,
  orgMembers,
  handleMemberRoleChange,
  actingMemberId,
  removeMemberConfirm: _removeMemberConfirm,
  setRemoveMemberConfirm,
  handleRemoveMember: _handleRemoveMember,
  currentUser,
}: OrganizationSectionProps) {
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Building2 className="w-5 h-5 text-orange-600" />
          Organization & Team
        </h2>
        <p className="text-slate-600 text-sm mt-1">Manage your organization and team members</p>
      </div>

      <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
        <p className="font-medium text-emerald-800">Cadence is your CRM</p>
        <p className="text-sm text-emerald-700 mt-1">All data stays in Cadence — no external CRM required</p>
      </div>

      {isUsingRealApi() && organizations.length > 0 && (
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-2">Current Organization</label>
          <select
            value={currentOrg?.id ?? ''}
            onChange={(e) => {
              const id = e.target.value;
              if (id) {
                setCurrentOrg(id);
                toast.success('Organization switched.');
              }
            }}
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
          >
            {organizations.map((org) => (
              <option key={org.id} value={org.id}>
                {org.name} {org.isOwner ? '(Owner)' : ''}
              </option>
            ))}
          </select>
          <Link to="/organizations" className="inline-block mt-2 text-sm text-orange-600 hover:text-orange-700 font-medium">
            Manage all organizations
          </Link>
        </div>
      )}

      {isUsingRealApi() && currentOrg?.isOwner && (
        <>
          <div className="pt-6 border-t border-slate-200">
            <h3 className="font-semibold text-slate-900 mb-4 flex items-center gap-2">
              <UserPlus className="w-4 h-4" />
              Invite Team Members
            </h3>
            <form onSubmit={handleInvite} className="flex gap-2">
              <input
                type="email"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
                placeholder="email@example.com"
                className="flex-1 min-w-0 h-11 px-4 border border-slate-300 rounded-xl text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
              />
              <button
                type="submit"
                disabled={!inviteEmail.trim() || inviting}
                className="h-11 px-6 bg-orange-600 hover:bg-orange-700 disabled:opacity-50 text-white font-medium rounded-xl transition-colors"
              >
                {inviting ? 'Sending...' : 'Send Invite'}
              </button>
            </form>
          </div>

          {ownerInvites.length > 0 && (
            <div>
              <h3 className="font-semibold text-slate-900 mb-3 flex items-center gap-2">
                <Mail className="w-4 h-4" />
                Pending Invites
              </h3>
              <div className="space-y-2">
                {ownerInvites.map((i) => (
                  <div key={i.id} className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
                    <span className="text-sm text-slate-700">{i.email}</span>
                    <span className="text-xs text-slate-400">Pending</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {ownerJoinRequests.length > 0 && (
            <div>
              <h3 className="font-semibold text-slate-900 mb-3">Join Requests</h3>
              <div className="space-y-2">
                {ownerJoinRequests.map((jr) => (
                  <div key={jr.id} className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
                    <div>
                      <p className="font-medium text-slate-900 text-sm">{jr.userName}</p>
                      <p className="text-xs text-slate-500">{jr.userEmail}</p>
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => handleAcceptJr(jr)}
                        disabled={actingJrId === jr.id}
                        className="p-2 text-emerald-600 hover:bg-emerald-100 rounded-lg disabled:opacity-50"
                        title="Accept"
                      >
                        <Check className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRejectJr(jr)}
                        disabled={actingJrId === jr.id}
                        className="p-2 text-red-600 hover:bg-red-100 rounded-lg disabled:opacity-50"
                        title="Reject"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="pt-6 border-t border-slate-200">
            <h3 className="font-semibold text-slate-900 mb-4 flex items-center gap-2">
              <Users className="w-4 h-4" />
              Team Members ({orgMembers.length})
            </h3>
            {orgMembers.length === 0 ? (
              <p className="text-sm text-slate-500">No members yet. Invite your team above.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-200">
                      <th className="text-left py-3 font-medium text-slate-600">Member</th>
                      <th className="text-left py-3 font-medium text-slate-600">Role</th>
                      <th className="text-right py-3 font-medium text-slate-600">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {orgMembers.map((m) => {
                      const isOwner = m.role === 0;
                      const isSelf = currentUser?.id === m.userId;
                      return (
                        <tr key={m.userId} className="border-b border-slate-100 last:border-0">
                          <td className="py-3">
                            <div className="flex items-center gap-3">
                              <div className="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center text-xs font-medium text-slate-600">
                                {m.name?.charAt(0) || '?'}
                              </div>
                              <div>
                                <p className="font-medium text-slate-900">{m.name}</p>
                                <p className="text-xs text-slate-500">{m.email}</p>
                              </div>
                            </div>
                          </td>
                          <td className="py-3">
                            {isOwner ? (
                              <span className="inline-flex items-center px-2 py-1 bg-orange-100 text-orange-700 text-xs font-medium rounded-full">
                                Owner
                              </span>
                            ) : (
                              <select
                                value={m.role}
                                onChange={(e) => handleMemberRoleChange(m, parseInt(e.target.value, 10))}
                                disabled={actingMemberId === m.userId}
                                className="h-8 px-2 border border-slate-300 rounded-lg text-sm bg-white disabled:opacity-60"
                              >
                                <option value={1}>Member</option>
                                <option value={2}>Manager</option>
                              </select>
                            )}
                          </td>
                          <td className="py-3 text-right">
                            {!isOwner && !isSelf && (
                              <button
                                type="button"
                                onClick={() => setRemoveMemberConfirm(m)}
                                disabled={actingMemberId === m.userId}
                                className="text-red-600 hover:text-red-700 hover:bg-red-50 px-3 py-1.5 rounded-lg text-sm font-medium disabled:opacity-50"
                              >
                                Remove
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
