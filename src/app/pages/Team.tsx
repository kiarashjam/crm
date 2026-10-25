import { useState, useEffect, useMemo } from 'react';
import { toast } from 'sonner';
import {
  Users, UserPlus, Mail, Shield, ShieldCheck, Crown,
  Trash2, Check, X, Clock, Send, RefreshCw, Search,
  UserCircle, AlertCircle, TrendingUp, Target,
  Phone, Copy, Filter, BarChart3,
  Activity, ChevronRight, Handshake, Eye
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { useOrg } from '@/app/contexts/OrgContext';
import {
  getOrgMembers,
  createInvite,
  resendInvite,
  addOrgMember,
  listPendingInvitesForOrg,
  listPendingJoinRequestsForOrg,
  acceptJoinRequest,
  rejectJoinRequest,
  updateMemberRole,
  removeMember,
  isOrgAdmin,
  type OrgMemberDto,
  type InviteDto,
  type JoinRequestDto,
} from '@/app/api/organizations';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogTitle,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';

// Role mapping
const ROLES = {
  0: { label: 'Owner', icon: Crown, color: 'text-amber-600', bg: 'bg-amber-100', bgGradient: 'from-amber-500 to-orange-500' },
  1: { label: 'Member', icon: UserCircle, color: 'text-slate-600', bg: 'bg-slate-100', bgGradient: 'from-slate-500 to-slate-600' },
  2: { label: 'Manager', icon: ShieldCheck, color: 'text-blue-600', bg: 'bg-blue-100', bgGradient: 'from-blue-500 to-indigo-500' },
  3: { label: 'Viewer', icon: Eye, color: 'text-violet-600', bg: 'bg-violet-100', bgGradient: 'from-violet-500 to-purple-500' },
} as const;

function getRoleInfo(role: number) {
  return ROLES[role as keyof typeof ROLES] || ROLES[1];
}

// Member Detail Panel Component
function MemberDetailPanel({
  member,
  onClose,
  isAdmin,
  onRoleChange,
  onRemove,
}: {
  member: OrgMemberDto;
  onClose: () => void;
  isAdmin: boolean;
  onRoleChange: (role: number) => void;
  onRemove: () => void;
}) {
  const roleInfo = getRoleInfo(member.role);
  const RoleIcon = roleInfo.icon;
  
  // Mock stats for demonstration
  const mockStats = {
    leadsAssigned: Math.floor(Math.random() * 50) + 5,
    dealsWon: Math.floor(Math.random() * 20),
    tasksCompleted: Math.floor(Math.random() * 100) + 20,
    activitiesLogged: Math.floor(Math.random() * 200) + 50,
    conversionRate: Math.floor(Math.random() * 40) + 20,
    avgResponseTime: `${Math.floor(Math.random() * 4) + 1}h`,
  };

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[420px] bg-white border-l border-slate-200 shadow-2xl z-50 overflow-y-auto">
      {/* Header */}
      <div className="sticky top-0 bg-white border-b border-slate-200 p-4 z-10">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900">Member Details</h3>
          <Button variant="ghost" size="sm" onClick={onClose}>
            <X className="w-4 h-4" />
          </Button>
        </div>
      </div>

      <div className="p-6 space-y-6">
        {/* Profile Card */}
        <div className="text-center">
          <div className={`w-20 h-20 mx-auto rounded-2xl bg-gradient-to-br ${roleInfo.bgGradient} flex items-center justify-center text-white font-bold text-2xl shadow-xl`}>
            {member.name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase()}
          </div>
          <h2 className="text-xl font-bold text-slate-900 mt-4">{member.name}</h2>
          <p className="text-slate-500">{member.email}</p>
          <div className="flex items-center justify-center gap-2 mt-3">
            <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-medium ${roleInfo.bg} ${roleInfo.color}`}>
              <RoleIcon className="w-4 h-4" />
              {roleInfo.label}
            </span>
          </div>
        </div>

        {/* Quick Actions */}
        <div className="flex gap-2">
          <Button variant="outline" className="flex-1 h-10 text-sm" asChild>
            <a href={`mailto:${member.email}`}>
              <Mail className="w-4 h-4 mr-2" />
              Email
            </a>
          </Button>
          <Button 
            variant="outline" 
            className="flex-1 h-10 text-sm"
            onClick={() => {
              navigator.clipboard.writeText(member.email);
              toast.success('Email copied to clipboard');
            }}
          >
            <Copy className="w-4 h-4 mr-2" />
            Copy Email
          </Button>
        </div>

        {/* Performance Stats */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <BarChart3 className="w-4 h-4 text-violet-500" />
            Performance Overview
          </h4>
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-slate-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-slate-500 text-xs mb-1">
                <Target className="w-3.5 h-3.5" />
                Leads Assigned
              </div>
              <p className="text-lg font-bold text-slate-900">{mockStats.leadsAssigned}</p>
            </div>
            <div className="bg-emerald-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-emerald-600 text-xs mb-1">
                <Handshake className="w-3.5 h-3.5" />
                Deals Won
              </div>
              <p className="text-lg font-bold text-emerald-700">{mockStats.dealsWon}</p>
            </div>
            <div className="bg-blue-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-blue-600 text-xs mb-1">
                <Check className="w-3.5 h-3.5" />
                Tasks Done
              </div>
              <p className="text-lg font-bold text-blue-700">{mockStats.tasksCompleted}</p>
            </div>
            <div className="bg-amber-50 rounded-xl p-3">
              <div className="flex items-center gap-2 text-amber-600 text-xs mb-1">
                <Activity className="w-3.5 h-3.5" />
                Activities
              </div>
              <p className="text-lg font-bold text-amber-700">{mockStats.activitiesLogged}</p>
            </div>
          </div>
        </div>

        {/* Metrics */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <TrendingUp className="w-4 h-4 text-emerald-500" />
            Key Metrics
          </h4>
          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
              <span className="text-sm text-slate-600">Conversion Rate</span>
              <span className="text-sm font-bold text-emerald-600">{mockStats.conversionRate}%</span>
            </div>
            <div className="flex items-center justify-between p-3 bg-slate-50 rounded-xl">
              <span className="text-sm text-slate-600">Avg Response Time</span>
              <span className="text-sm font-bold text-blue-600">{mockStats.avgResponseTime}</span>
            </div>
          </div>
        </div>

        {/* Recent Activity */}
        <div>
          <h4 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
            <Clock className="w-4 h-4 text-slate-500" />
            Recent Activity
          </h4>
          <div className="space-y-2">
            {[
              { action: 'Created a new lead', time: '2 hours ago', icon: UserPlus },
              { action: 'Closed a deal worth $5,000', time: '5 hours ago', icon: Handshake },
              { action: 'Logged a call activity', time: 'Yesterday', icon: Phone },
              { action: 'Completed 3 tasks', time: '2 days ago', icon: Check },
            ].map((activity, i) => (
              <div key={i} className="flex items-start gap-3 p-2 rounded-lg hover:bg-slate-50 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center shrink-0">
                  <activity.icon className="w-4 h-4 text-slate-500" />
                </div>
                <div className="min-w-0">
                  <p className="text-sm text-slate-700">{activity.action}</p>
                  <p className="text-xs text-slate-400">{activity.time}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Admin Actions */}
        {isAdmin && member.role !== 0 && (
          <div className="border-t border-slate-200 pt-6 space-y-4">
            <h4 className="text-sm font-semibold text-slate-700">Admin Actions</h4>
            
            <div>
              <Label className="text-sm text-slate-600 mb-2 block">Change Role</Label>
              <Select
                value={member.role.toString()}
                onValueChange={(v) => onRoleChange(parseInt(v))}
              >
                <SelectTrigger className="h-10">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">
                    <span className="flex items-center gap-2">
                      <UserCircle className="w-4 h-4 text-slate-500" />
                      Member
                    </span>
                  </SelectItem>
                  <SelectItem value="2">
                    <span className="flex items-center gap-2">
                      <ShieldCheck className="w-4 h-4 text-blue-500" />
                      Manager
                    </span>
                  </SelectItem>
                  <SelectItem value="3">
                    <span className="flex items-center gap-2">
                      <Eye className="w-4 h-4 text-violet-500" />
                      Viewer — read-only
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <Button 
              variant="outline" 
              className="w-full text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700"
              onClick={onRemove}
            >
              <Trash2 className="w-4 h-4 mr-2" />
              Remove from Team
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}

export default function Team() {
  const { currentOrgId, currentOrg } = useOrg();
  const [members, setMembers] = useState<OrgMemberDto[]>([]);
  const [pendingInvites, setPendingInvites] = useState<InviteDto[]>([]);
  const [joinRequests, setJoinRequests] = useState<JoinRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [searchQuery, setSearchQuery] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [selectedMember, setSelectedMember] = useState<OrgMemberDto | null>(null);

  // Invite dialog state
  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteEmails, setInviteEmails] = useState<string[]>([]);
  const [inviting, setInviting] = useState(false);

  // Remove member confirmation
  const [removeMemberConfirm, setRemoveMemberConfirm] = useState<OrgMemberDto | null>(null);
  const [removing, setRemoving] = useState(false);

  // Role change state
  const [_changingRole, setChangingRole] = useState<string | null>(null);

  // Join request actions
  const [processingJoinRequest, setProcessingJoinRequest] = useState<string | null>(null);

  const isAdmin = isOrgAdmin(currentOrg);

  const loadData = async () => {
    // With no workspace there is nothing to load — this returned with `loading`
    // still true, and the full-page overlay never went away.
    if (!currentOrgId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      // Every member can list org members. Pending invites and join requests
      // are admin-only on the backend (owner / manager) — guarding by role
      // avoids the 403 that regular members would otherwise see in the
      // console every time the Team page loads.
      const membersPromise = getOrgMembers(currentOrgId);
      const invitesPromise = isAdmin
        ? listPendingInvitesForOrg(currentOrgId).catch(() => [] as InviteDto[])
        : Promise.resolve([] as InviteDto[]);
      const requestsPromise = isAdmin
        ? listPendingJoinRequestsForOrg(currentOrgId).catch(() => [] as JoinRequestDto[])
        : Promise.resolve([] as JoinRequestDto[]);
      const [membersData, invitesData, requestsData] = await Promise.all([
        membersPromise, invitesPromise, requestsPromise,
      ]);
      setMembers(membersData);
      setPendingInvites(invitesData);
      setJoinRequests(requestsData);
    } catch {
      toast.error('Failed to load team data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [currentOrgId]);

  // Stats
  const stats = useMemo(() => {
    const owners = members.filter(m => m.role === 0).length;
    const managers = members.filter(m => m.role === 2).length;
    const regularMembers = members.filter(m => m.role === 1).length;
    const viewers = members.filter(m => m.role === 3).length;
    return { total: members.length, owners, managers, members: regularMembers, viewers };
  }, [members]);

  // Filter members
  const filteredMembers = useMemo(() => {
    return members.filter(m => {
      // Search filter
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        if (!m.name.toLowerCase().includes(q) && !m.email.toLowerCase().includes(q)) {
          return false;
        }
      }
      // Role filter
      if (roleFilter !== 'all' && m.role.toString() !== roleFilter) {
        return false;
      }
      return true;
    });
  }, [members, searchQuery, roleFilter]);

  // Handle adding email to bulk invite
  const addEmailToInvite = () => {
    if (inviteEmail.trim() && !inviteEmails.includes(inviteEmail.trim().toLowerCase())) {
      setInviteEmails(prev => [...prev, inviteEmail.trim().toLowerCase()]);
      setInviteEmail('');
    }
  };

  const removeEmailFromInvite = (email: string) => {
    setInviteEmails(prev => prev.filter(e => e !== email));
  };

  // Handle invite (supports bulk)
  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentOrgId) return;
    
    const emailsToInvite = inviteEmail.trim() 
      ? [...inviteEmails, inviteEmail.trim().toLowerCase()]
      : inviteEmails;
    
    if (emailsToInvite.length === 0) return;
    
    setInviting(true);
    let successCount = 0;
    let failCount = 0;

    for (const email of emailsToInvite) {
      try {
        const invite = await createInvite(currentOrgId, email);
        if (invite) {
          setPendingInvites(prev => [invite, ...prev]);
          successCount++;
        } else {
          failCount++;
        }
      } catch {
        failCount++;
      }
    }

    if (successCount > 0) {
      toast.success(`Sent ${successCount} invitation${successCount > 1 ? 's' : ''}`);
    }
    if (failCount > 0) {
      toast.error(`Failed to send ${failCount} invitation${failCount > 1 ? 's' : ''}`);
    }

    setInviteDialogOpen(false);
    setInviteEmail('');
    setInviteEmails([]);
    setInviting(false);
  };

  // Handle role change
  const [resendingId, setResendingId] = useState<string | null>(null);

  // Direct add: bring in someone who already has an account, without waiting on an
  // invitation email or their acceptance.
  const [addOpen, setAddOpen] = useState(false);
  const [addEmail, setAddEmail] = useState('');
  const [addRole, setAddRole] = useState('3'); // default to the safest role: Viewer
  const [adding, setAdding] = useState(false);

  const handleAddExisting = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentOrgId || !addEmail.trim() || adding) return;
    setAdding(true);
    try {
      const member = await addOrgMember(currentOrgId, addEmail, parseInt(addRole, 10));
      if (member) {
        setMembers((prev) => [...prev, member]);
        toast.success(`${member.name} added as ${getRoleInfo(member.role).label}`);
        setAddOpen(false);
        setAddEmail('');
        setAddRole('3');
      }
    } catch (err) {
      // The server says exactly why — most usefully "no account with that email yet".
      const message = err instanceof Error ? err.message : 'Could not add that person';
      toast.error(message.replace(/^HTTP \d+: /, ''));
    } finally {
      setAdding(false);
    }
  };

  const handleResendInvite = async (invite: InviteDto) => {
    if (resendingId) return;
    setResendingId(invite.id);
    try {
      const updated = await resendInvite(invite.id);
      if (updated) {
        setPendingInvites((prev) => prev.map((i) => (i.id === updated.id ? updated : i)));
        toast.success(`Invitation re-sent to ${invite.email}`);
      }
    } catch (err) {
      // The server explains why (most often: email is not configured, or the
      // sender address is not verified in SendGrid), so show that verbatim.
      const message = err instanceof Error ? err.message : 'Could not resend the invitation';
      toast.error(message.replace(/^HTTP \d+: /, ''));
    } finally {
      setResendingId(null);
    }
  };

  const handleRoleChange = async (member: OrgMemberDto, newRole: number) => {
    if (!currentOrgId) return;
    setChangingRole(member.userId);
    try {
      const success = await updateMemberRole(currentOrgId, member.userId, newRole);
      if (success) {
        setMembers(prev => prev.map(m => 
          m.userId === member.userId ? { ...m, role: newRole } : m
        ));
        if (selectedMember?.userId === member.userId) {
          setSelectedMember({ ...member, role: newRole });
        }
        toast.success(`Updated ${member.name}'s role to ${getRoleInfo(newRole).label}`);
      } else {
        toast.error('Failed to update role');
      }
    } catch {
      toast.error('Failed to update role');
    } finally {
      setChangingRole(null);
    }
  };

  // Handle remove member
  const handleRemoveMember = async () => {
    if (!currentOrgId || !removeMemberConfirm) return;
    setRemoving(true);
    try {
      const success = await removeMember(currentOrgId, removeMemberConfirm.userId);
      if (success) {
        setMembers(prev => prev.filter(m => m.userId !== removeMemberConfirm.userId));
        toast.success(`Removed ${removeMemberConfirm.name} from the team`);
        setRemoveMemberConfirm(null);
        if (selectedMember?.userId === removeMemberConfirm.userId) {
          setSelectedMember(null);
        }
      } else {
        toast.error('Failed to remove member');
      }
    } catch {
      toast.error('Failed to remove member');
    } finally {
      setRemoving(false);
    }
  };

  // Handle join request
  const handleJoinRequest = async (request: JoinRequestDto, accept: boolean) => {
    setProcessingJoinRequest(request.id);
    try {
      const result = accept
        ? await acceptJoinRequest(request.id)
        : await rejectJoinRequest(request.id);
      
      if (result) {
        setJoinRequests(prev => prev.filter(r => r.id !== request.id));
        if (accept) {
          const updatedMembers = await getOrgMembers(currentOrgId!);
          setMembers(updatedMembers);
          toast.success(`${request.userName} has joined the team`);
        } else {
          toast.success(`Declined ${request.userName}'s request`);
        }
      } else {
        toast.error('Failed to process request');
      }
    } catch {
      toast.error('Failed to process request');
    } finally {
      setProcessingJoinRequest(null);
    }
  };

  // Get member initials
  const getInitials = (name: string) => {
    return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Users}
            tone="indigo"
            title="Team"
            subtitle={currentOrg ? `Manage members of ${currentOrg.name}` : 'Manage your organization members'}
            actions={
              <>
                <Button variant="outline" onClick={loadData} className={heroSecondary}>
                  <RefreshCw className="h-4 w-4" />
                  Refresh
                </Button>
                {isAdmin && (
                  <Button onClick={() => setAddOpen(true)} variant="outline" className={heroSecondary}>
                    <UserPlus className="h-4 w-4" />
                    Add existing user
                  </Button>
                )}
                {isAdmin && (
                  <Button onClick={() => setInviteDialogOpen(true)} className={heroPrimary('indigo')}>
                    <UserPlus className="h-4 w-4" />
                    Invite members
                  </Button>
                )}
              </>
            }
            stats={loading ? undefined : [
              { label: 'Total Members', value: stats.total, icon: Users, tone: 'violet', hint: 'active team members' },
              { label: 'Owners', value: stats.owners, icon: Crown, tone: 'amber', hint: 'full admin access' },
              { label: 'Managers', value: stats.managers, icon: ShieldCheck, tone: 'blue', hint: 'can manage resources' },
              { label: 'Pending Invites', value: pendingInvites.length, icon: Mail, tone: 'emerald', hint: 'awaiting response' },
            ]}
          />

        {loading ? (
          <ContentSkeleton rows={5} />
        ) : (
          <div className="space-y-8">
            {/* Join Requests Alert */}
            {isAdmin && joinRequests.length > 0 && (
              <div className="bg-gradient-to-r from-amber-50 to-orange-50 rounded-2xl border border-amber-200 p-5">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center shrink-0">
                    <AlertCircle className="w-5 h-5 text-amber-600" />
                  </div>
                  <div className="flex-1">
                    <h3 className="font-semibold text-slate-900 mb-1">
                      {joinRequests.length} pending join request{joinRequests.length > 1 ? 's' : ''}
                    </h3>
                    <p className="text-sm text-slate-600 mb-4">
                      People are waiting to join your organization
                    </p>
                    <div className="space-y-2">
                      {joinRequests.map(request => (
                        <div 
                          key={request.id}
                          className="flex items-center justify-between gap-4 p-3 bg-white rounded-xl border border-amber-100"
                        >
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-gradient-to-br from-amber-400 to-orange-400 flex items-center justify-center text-white font-semibold text-sm">
                              {getInitials(request.userName)}
                            </div>
                            <div>
                              <p className="font-medium text-slate-900 text-sm">{request.userName}</p>
                              <p className="text-xs text-slate-500">{request.userEmail}</p>
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => handleJoinRequest(request, false)}
                              disabled={processingJoinRequest === request.id}
                              className="h-8 text-slate-500 hover:text-slate-700"
                            >
                              <X className="w-4 h-4" />
                            </Button>
                            <Button
                              size="sm"
                              onClick={() => handleJoinRequest(request, true)}
                              disabled={processingJoinRequest === request.id}
                              className="h-8 gap-1.5 bg-emerald-600 hover:bg-emerald-700"
                            >
                              {processingJoinRequest === request.id ? (
                                <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                              ) : (
                                <Check className="w-3.5 h-3.5" />
                              )}
                              Accept
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Search and filter bar */}
            <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-indigo-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-violet-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-col sm:flex-row gap-3">
                {/* Search Input - Enhanced */}
                <div className="relative flex-1 group">
                  <div className="absolute inset-0 bg-gradient-to-r from-indigo-500/20 via-violet-500/20 to-indigo-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-indigo-100 to-violet-100 flex items-center justify-center group-focus-within:from-indigo-200 group-focus-within:to-violet-200 transition-all duration-300">
                      <Search className="w-4 h-4 text-indigo-500 group-focus-within:text-indigo-600 transition-colors" />
                    </div>
                    <Input
                      type="search"
                      placeholder="Search by name or email..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-indigo-300 focus:ring-4 focus:ring-indigo-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                    />
                    {searchQuery && (
                      <button
                        type="button"
                        onClick={() => setSearchQuery('')}
                        className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-slate-100 hover:bg-red-50 flex items-center justify-center text-slate-400 hover:text-red-500 transition-all duration-200"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>
                
                {/* Role Filter - Enhanced */}
                <div className="relative group/filter">
                  <div className="absolute inset-0 bg-violet-200/50 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-60 transition-all duration-300" />
                  <Select value={roleFilter} onValueChange={setRoleFilter}>
                    <SelectTrigger className="relative w-full sm:w-[180px] h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-violet-50 hover:border-violet-300 focus:border-violet-300 focus:ring-2 focus:ring-violet-200/60 transition-all duration-300">
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-6 rounded-lg bg-violet-100 flex items-center justify-center">
                          <Filter className="w-3.5 h-3.5 text-violet-600" />
                        </div>
                        <SelectValue placeholder="Filter by role" />
                      </div>
                    </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All roles</SelectItem>
                    <SelectItem value="0">
                      <span className="flex items-center gap-2">
                        <Crown className="w-3.5 h-3.5 text-amber-500" />
                        Owners
                      </span>
                    </SelectItem>
                    <SelectItem value="2">
                      <span className="flex items-center gap-2">
                        <ShieldCheck className="w-3.5 h-3.5 text-blue-500" />
                        Managers
                      </span>
                    </SelectItem>
                    <SelectItem value="1">
                      <span className="flex items-center gap-2">
                        <UserCircle className="w-3.5 h-3.5 text-slate-500" />
                        Members
                      </span>
                    </SelectItem>
                    <SelectItem value="3">
                      <span className="flex items-center gap-2">
                        <Eye className="w-3.5 h-3.5 text-violet-500" />
                        Viewers
                      </span>
                    </SelectItem>
                  </SelectContent>
                </Select>
                </div>
              </div>
              
              {/* Active Filter Pills */}
              {(searchQuery || roleFilter !== 'all') && (
                <div className="relative mt-4 flex flex-wrap items-center gap-2">
                  <span className="text-xs text-slate-500">Showing:</span>
                  {searchQuery && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery}&quot;
                      <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-indigo-600 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {roleFilter !== 'all' && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-violet-50 text-violet-700 text-xs font-medium border border-violet-200">
                      <Filter className="w-3 h-3" />
                      {roleFilter === '0' ? 'Owners' : roleFilter === '2' ? 'Managers' : roleFilter === '3' ? 'Viewers' : 'Members'}
                      <button onClick={() => setRoleFilter('all')} className="ml-0.5 hover:text-violet-900 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  <span className="text-xs text-slate-500">
                    {filteredMembers.length} members
                  </span>
                </div>
              )}
            </div>

            {/* Team Members Grid */}
            <section>
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                  <Users className="w-5 h-5 text-violet-500" />
                  Team Members
                  <span className="text-sm font-normal text-slate-500">
                    ({filteredMembers.length})
                  </span>
                </h2>
              </div>

              {filteredMembers.length === 0 ? (
                <EmptyState
                  icon={Users}
                  title="No team members found"
                  description={searchQuery || roleFilter !== 'all' 
                    ? "Try adjusting your search or filter" 
                    : "Invite people to join your organization"}
                  actionLabel="Invite member"
                  onAction={() => setInviteDialogOpen(true)}
                />
              ) : (
                <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
                  {filteredMembers.map((member, index) => {
                    const roleInfo = getRoleInfo(member.role);
                    const RoleIcon = roleInfo.icon;
                    
                    return (
                      <StaggerItem
                        key={member.userId}
                        index={index}
                        className={`group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-violet-200 cursor-pointer overflow-hidden ${liftOnHover}`}
                        onClick={() => setSelectedMember(member)}
                      >
                        {/* Role color strip */}
                        <div className={`absolute top-0 left-0 right-0 h-1 bg-gradient-to-r ${roleInfo.bgGradient}`} />
                        
                        <div className="p-5">
                          <div className="flex items-start gap-4">
                            {/* Avatar */}
                            <div className={`w-14 h-14 rounded-xl bg-gradient-to-br ${roleInfo.bgGradient} flex items-center justify-center text-white font-bold text-lg shadow-lg group-hover:scale-105 transition-transform`}>
                              {getInitials(member.name)}
                            </div>
                            
                            {/* Info */}
                            <div className="flex-1 min-w-0">
                              <div className="flex items-start justify-between gap-2">
                                <div>
                                  <h3 className="font-semibold text-slate-900 truncate group-hover:text-violet-600 transition-colors">
                                    {member.name}
                                  </h3>
                                  <p className="text-sm text-slate-500 truncate">{member.email}</p>
                                </div>
                                <ChevronRight className="w-4 h-4 text-slate-300 group-hover:text-violet-400 transition-colors shrink-0 mt-1" />
                              </div>
                              <div className="mt-3">
                                <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${roleInfo.bg} ${roleInfo.color}`}>
                                  <RoleIcon className="w-3 h-3" />
                                  {roleInfo.label}
                                </span>
                              </div>
                            </div>
                          </div>

                          {/* Quick stats preview */}
                          <div className="mt-4 pt-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
                            <span className="flex items-center gap-1">
                              <Target className="w-3.5 h-3.5" />
                              {Math.floor(Math.random() * 30) + 5} leads
                            </span>
                            <span className="flex items-center gap-1">
                              <Handshake className="w-3.5 h-3.5" />
                              {Math.floor(Math.random() * 10)} deals
                            </span>
                            <span className="flex items-center gap-1">
                              <Check className="w-3.5 h-3.5" />
                              {Math.floor(Math.random() * 50) + 10} tasks
                            </span>
                          </div>
                        </div>
                      </StaggerItem>
                    );
                  })}
                </div>
              )}
            </section>

            {/* Pending Invites */}
            {pendingInvites.length > 0 && (
              <section>
                <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2 mb-4">
                  <Mail className="w-5 h-5 text-blue-500" />
                  Pending Invitations
                  <span className="text-sm font-normal text-slate-500">
                    ({pendingInvites.length})
                  </span>
                </h2>
                <div className="bg-white rounded-2xl border border-slate-200 divide-y divide-slate-100">
                  {pendingInvites.map((invite, index) => (
                    <StaggerItem
                      key={invite.id}
                      index={index}
                      className="flex items-center justify-between gap-4 p-4"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-gradient-to-br from-slate-200 to-slate-300 flex items-center justify-center">
                          <Mail className="w-5 h-5 text-slate-500" />
                        </div>
                        <div>
                          <p className="font-medium text-slate-700">{invite.email}</p>
                          <p className="text-xs text-slate-400 flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            Sent {new Date(invite.createdAtUtc).toLocaleDateString()} · Expires {new Date(invite.expiresAtUtc).toLocaleDateString()}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium bg-blue-100 text-blue-700">
                          <Send className="w-3 h-3" />
                          Pending
                        </span>
                        {isAdmin && (
                          <Button
                            variant="outline"
                            size="sm"
                            className="gap-1.5"
                            disabled={resendingId === invite.id}
                            onClick={() => handleResendInvite(invite)}
                          >
                            <RefreshCw className={`w-3.5 h-3.5 ${resendingId === invite.id ? 'animate-spin' : ''}`} />
                            {resendingId === invite.id ? 'Sending…' : 'Resend email'}
                          </Button>
                        )}
                      </div>
                    </StaggerItem>
                  ))}
                </div>
              </section>
            )}

            {/* Role Explanation */}
            <section className="bg-gradient-to-r from-violet-50 to-purple-50 rounded-2xl p-6 border border-violet-100">
              <h3 className="text-sm font-semibold text-slate-800 mb-4 flex items-center gap-2">
                <Shield className="w-4 h-4 text-violet-500" />
                Understanding Team Roles
              </h3>
              <div className="grid sm:grid-cols-3 gap-4">
                <div className="bg-white/60 rounded-xl p-4 border border-white">
                  <div className="flex items-center gap-3 mb-2">
                    <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-amber-500 to-orange-500 flex items-center justify-center">
                      <Crown className="w-5 h-5 text-white" />
                    </div>
                    <div>
                      <p className="font-semibold text-slate-800">Owner</p>
                      <p className="text-xs text-slate-500">Full Access</p>
                    </div>
                  </div>
                  <ul className="text-xs text-slate-600 space-y-1 mt-3">
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Manage all members</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Organization settings</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Billing & subscription</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> All CRM features</li>
                  </ul>
                </div>
                <div className="bg-white/60 rounded-xl p-4 border border-white">
                  <div className="flex items-center gap-3 mb-2">
                    <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-blue-500 to-indigo-500 flex items-center justify-center">
                      <ShieldCheck className="w-5 h-5 text-white" />
                    </div>
                    <div>
                      <p className="font-semibold text-slate-800">Manager</p>
                      <p className="text-xs text-slate-500">Team Lead</p>
                    </div>
                  </div>
                  <ul className="text-xs text-slate-600 space-y-1 mt-3">
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> View all team data</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Assign leads & tasks</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Generate reports</li>
                    <li className="flex items-center gap-1.5"><X className="w-3 h-3 text-slate-300" /> Cannot manage members</li>
                  </ul>
                </div>
                <div className="bg-white/60 rounded-xl p-4 border border-white">
                  <div className="flex items-center gap-3 mb-2">
                    <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-slate-500 to-slate-600 flex items-center justify-center">
                      <UserCircle className="w-5 h-5 text-white" />
                    </div>
                    <div>
                      <p className="font-semibold text-slate-800">Member</p>
                      <p className="text-xs text-slate-500">Standard Access</p>
                    </div>
                  </div>
                  <ul className="text-xs text-slate-600 space-y-1 mt-3">
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Own leads & deals</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Personal tasks</li>
                    <li className="flex items-center gap-1.5"><Check className="w-3 h-3 text-emerald-500" /> Log activities</li>
                    <li className="flex items-center gap-1.5"><X className="w-3 h-3 text-slate-300" /> Limited to assigned data</li>
                  </ul>
                </div>
              </div>
            </section>
          </div>
        )}
        </main>
      </PageTransition>

      {/* Member Detail Slide-over Panel */}
      {selectedMember && (
        <>
          <div 
            className="fixed inset-0 bg-black/20 z-40"
            onClick={() => setSelectedMember(null)}
          />
          <MemberDetailPanel
            member={selectedMember}
            onClose={() => setSelectedMember(null)}
            isAdmin={isAdmin}
            onRoleChange={(role) => handleRoleChange(selectedMember, role)}
            onRemove={() => {
              setRemoveMemberConfirm(selectedMember);
              setSelectedMember(null);
            }}
          />
        </>
      )}

      {/* Invite Dialog - Enhanced with bulk invite */}
      <Dialog open={inviteDialogOpen} onOpenChange={setInviteDialogOpen}>
        <DialogContent className="sm:max-w-[500px] p-0 gap-0 overflow-hidden">
          {/* Gradient Header */}
          <div className="relative overflow-hidden">
            <div className="absolute inset-0 bg-gradient-to-br from-violet-600 via-violet-500 to-purple-500" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <UserPlus className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Invite Team Members
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Add people to your organization
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleInvite} className="p-6">
            <div className="space-y-4">
              <div className="bg-violet-50 rounded-xl p-4 border border-violet-100">
                <p className="text-sm text-violet-800">
                  <strong>Tip:</strong> You can invite multiple people at once. They'll receive email invitations and join as <strong>Members</strong> by default.
                </p>
              </div>

              <div className="group">
                <Label htmlFor="invite-email" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <Mail className="w-4 h-4 text-violet-500" />
                  Email Addresses
                </Label>
                <div className="flex gap-2">
                  <Input
                    id="invite-email"
                    type="email"
                    value={inviteEmail}
                    onChange={(e) => setInviteEmail(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        addEmailToInvite();
                      }
                    }}
                    placeholder="colleague@company.com"
                    className="h-11 flex-1 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300"
                  />
                  <Button 
                    type="button" 
                    variant="outline" 
                    onClick={addEmailToInvite}
                    className="h-11 px-4"
                  >
                    Add
                  </Button>
                </div>
              </div>

              {/* Email chips */}
              {inviteEmails.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {inviteEmails.map(email => (
                    <span 
                      key={email}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-violet-100 text-violet-700 text-sm"
                    >
                      {email}
                      <button
                        type="button"
                        onClick={() => removeEmailFromInvite(email)}
                        className="hover:text-violet-900"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>

            <div className="flex items-center gap-3 pt-6">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setInviteDialogOpen(false);
                  setInviteEmail('');
                  setInviteEmails([]);
                }}
                className="flex-1 h-11"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={inviting || (!inviteEmail.trim() && inviteEmails.length === 0)}
                className="flex-[2] h-11 bg-gradient-to-r from-violet-600 to-purple-600 hover:from-violet-700 hover:to-purple-700"
              >
                {inviting ? (
                  <span className="flex items-center gap-2">
                    <RefreshCw className="w-4 h-4 animate-spin" />
                    Sending...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <Send className="w-4 h-4" />
                    Send {inviteEmails.length + (inviteEmail.trim() ? 1 : 0) || ''} Invitation{(inviteEmails.length + (inviteEmail.trim() ? 1 : 0)) !== 1 ? 's' : ''}
                  </span>
                )}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* Add an existing user directly (no invitation email needed) */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent className="sm:max-w-[460px]">
          <DialogTitle>Add an existing user</DialogTitle>
          <p className="text-sm text-slate-500">
            Adds someone straight to this organization — no invitation email, and nothing for them
            to accept. They must already have a Cadence account with this email address.
          </p>
          <form onSubmit={handleAddExisting} className="mt-3 space-y-4">
            <div>
              <Label htmlFor="add-email">Email address</Label>
              <Input
                id="add-email"
                type="email"
                required
                value={addEmail}
                onChange={(e) => setAddEmail(e.target.value)}
                placeholder="name@example.com"
                className="mt-1"
              />
            </div>
            <div>
              <Label htmlFor="add-role">Role</Label>
              <Select value={addRole} onValueChange={setAddRole}>
                <SelectTrigger id="add-role" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="3">
                    <span className="flex items-center gap-2"><Eye className="w-4 h-4 text-violet-500" /> Viewer — read-only</span>
                  </SelectItem>
                  <SelectItem value="1">
                    <span className="flex items-center gap-2"><UserCircle className="w-4 h-4 text-slate-500" /> Member</span>
                  </SelectItem>
                  <SelectItem value="2">
                    <span className="flex items-center gap-2"><ShieldCheck className="w-4 h-4 text-blue-500" /> Manager</span>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex justify-end gap-2 pt-1">
              <Button type="button" variant="outline" onClick={() => setAddOpen(false)} disabled={adding}>
                Cancel
              </Button>
              <Button type="submit" disabled={adding || !addEmail.trim()} className="gap-1.5">
                {adding ? <RefreshCw className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
                {adding ? 'Adding…' : 'Add to organization'}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* Remove Member Confirmation */}
      <AlertDialog open={!!removeMemberConfirm} onOpenChange={(open) => !open && setRemoveMemberConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove team member?</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove <strong>{removeMemberConfirm?.name}</strong> from your organization. 
              They will lose access to all organization data. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={removing}>Cancel</AlertDialogCancel>
            <AlertDialogAction 
              onClick={handleRemoveMember} 
              disabled={removing} 
              className="bg-red-600 hover:bg-red-700"
            >
              {removing ? 'Removing...' : 'Remove member'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
