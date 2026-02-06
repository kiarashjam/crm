import { useState, useEffect, useMemo } from 'react';
import { toast } from 'sonner';
import {
  Users, UserPlus, Mail, Shield, ShieldCheck, Crown,
  Trash2, Check, X, Clock, Send, RefreshCw, Search,
  UserCircle, AlertCircle, TrendingUp, Target,
  Phone, Copy, Filter, BarChart3,
  Activity, ChevronRight, Handshake
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
  listPendingInvitesForOrg,
  listPendingJoinRequestsForOrg,
  acceptJoinRequest,
  rejectJoinRequest,
  updateMemberRole,
  removeMember,
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

// Role mapping
const ROLES = {
  0: { label: 'Owner', icon: Crown, color: 'text-amber-600', bg: 'bg-amber-100', bgGradient: 'from-amber-500 to-orange-500' },
  1: { label: 'Member', icon: UserCircle, color: 'text-slate-600', bg: 'bg-slate-100', bgGradient: 'from-slate-500 to-slate-600' },
  2: { label: 'Manager', icon: ShieldCheck, color: 'text-blue-600', bg: 'bg-blue-100', bgGradient: 'from-blue-500 to-indigo-500' },
} as const;

function getRoleInfo(role: number) {
  return ROLES[role as keyof typeof ROLES] || ROLES[1];
}

// Stat Card Component
function StatCard({ 
  icon: Icon, 
  label, 
  value, 
  subtext, 
  gradient,
  colorClass = 'slate'
}: { 
  icon: React.ElementType; 
  label: string; 
  value: number | string; 
  subtext?: string;
  gradient: string;
  colorClass?: string;
}) {
  const colorMap: Record<string, { border: string; bg: string; text: string; shadow: string }> = {
    violet: { border: 'border-violet-100', bg: 'from-violet-50 to-violet-100', text: 'text-violet-600', shadow: 'hover:shadow-violet-100' },
    amber: { border: 'border-amber-100', bg: 'from-amber-50 to-amber-100', text: 'text-amber-600', shadow: 'hover:shadow-amber-100' },
    blue: { border: 'border-blue-100', bg: 'from-blue-50 to-blue-100', text: 'text-blue-600', shadow: 'hover:shadow-blue-100' },
    emerald: { border: 'border-emerald-100', bg: 'from-emerald-50 to-emerald-100', text: 'text-emerald-600', shadow: 'hover:shadow-emerald-100' },
    slate: { border: 'border-slate-200/80', bg: 'from-slate-100 to-slate-50', text: 'text-slate-600', shadow: 'hover:shadow-slate-100' },
  };
  const colors = colorMap[colorClass] || colorMap.slate!;
  
  return (
    <div className={`group relative bg-white rounded-2xl border ${colors.border} p-5 shadow-sm hover:shadow-xl ${colors.shadow} transition-all duration-300 overflow-hidden`}>
      <div className={`absolute top-0 right-0 w-20 h-20 bg-gradient-to-br ${colors.bg} rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform`} />
      <div className="relative">
        <div className={`w-10 h-10 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center mb-3 group-hover:scale-110 transition-transform shadow-lg`}>
          <Icon className="w-5 h-5 text-white" />
        </div>
        <p className={`text-3xl font-bold ${colors.text === 'text-slate-600' ? 'text-slate-900' : colors.text} tracking-tight`}>{value}</p>
        <p className="text-xs font-medium text-slate-500 mt-1">{label}</p>
        {subtext && <p className="text-[10px] text-slate-400 mt-0.5">{subtext}</p>}
      </div>
    </div>
  );
}

// Member Detail Panel Component
function MemberDetailPanel({
  member,
  onClose,
  isOwner,
  onRoleChange,
  onRemove,
}: {
  member: OrgMemberDto;
  onClose: () => void;
  isOwner: boolean;
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
        {isOwner && member.role !== 0 && (
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

  const isOwner = currentOrg?.isOwner ?? false;

  const loadData = async () => {
    if (!currentOrgId) return;
    setLoading(true);
    try {
      const [membersData, invitesData, requestsData] = await Promise.all([
        getOrgMembers(currentOrgId),
        listPendingInvitesForOrg(currentOrgId),
        listPendingJoinRequestsForOrg(currentOrgId),
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
    return { total: members.length, owners, managers, members: regularMembers };
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
          {/* Enhanced Header Section with Dark Decorative Elements */}
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
          {/* Decorative blur elements */}
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
            <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
            <div className="absolute inset-0 opacity-5" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")` }} />
          </div>
          
          <div className="relative px-6 lg:px-8 py-8 lg:py-10">
            <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
              <div className="flex items-center gap-5">
                <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                  <Users className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Team</h1>
                  <p className="text-slate-400 mt-1">
                    {currentOrg ? `Manage members of ${currentOrg.name}` : 'Manage your organization members'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                <Button 
                  variant="outline"
                  onClick={loadData}
                  className="gap-2 h-10 rounded-xl border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30"
                >
                  <RefreshCw className="w-4 h-4" />
                  Refresh
                </Button>
                {isOwner && (
                  <Button 
                    onClick={() => setInviteDialogOpen(true)} 
                    className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white"
                  >
                    <UserPlus className="w-4 h-4" />
                    Invite members
                  </Button>
                )}
              </div>
            </div>
          </div>
        </div>

          {/* Stats Cards */}
          {!loading && (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
              <StatCard 
                icon={Users} 
                label="Total Members" 
                value={stats.total}
                subtext="Active team members"
                gradient="from-violet-500 to-purple-500"
                colorClass="violet"
              />
              <StatCard 
                icon={Crown} 
                label="Owners" 
                value={stats.owners}
                subtext="Full admin access"
                gradient="from-amber-500 to-orange-500"
                colorClass="amber"
              />
              <StatCard 
                icon={ShieldCheck} 
                label="Managers" 
                value={stats.managers}
                subtext="Can manage resources"
                gradient="from-blue-500 to-indigo-500"
                colorClass="blue"
              />
              <StatCard 
                icon={Mail} 
                label="Pending Invites" 
                value={pendingInvites.length}
                subtext="Awaiting response"
                gradient="from-emerald-500 to-teal-500"
                colorClass="emerald"
              />
            </div>
          )}

        {loading ? (
          <ContentSkeleton rows={5} />
        ) : (
          <div className="space-y-8">
            {/* Join Requests Alert */}
            {isOwner && joinRequests.length > 0 && (
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

            {/* Search and Filter Bar - Modern Dark Theme */}
            <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-violet-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-purple-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-col sm:flex-row gap-3">
                {/* Search Input - Enhanced */}
                <div className="relative flex-1 group">
                  <div className="absolute inset-0 bg-gradient-to-r from-violet-500/20 via-purple-500/20 to-violet-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-violet-500/20 to-purple-500/20 flex items-center justify-center group-focus-within:from-violet-500/40 group-focus-within:to-purple-500/40 transition-all duration-300">
                      <Search className="w-4 h-4 text-violet-300 group-focus-within:text-violet-200 transition-colors" />
                    </div>
                    <Input
                      type="search"
                      placeholder="Search by name or email..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                    />
                    {searchQuery && (
                      <button
                        type="button"
                        onClick={() => setSearchQuery('')}
                        className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-white/10 hover:bg-red-500/30 flex items-center justify-center text-slate-400 hover:text-red-300 transition-all duration-200"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>
                
                {/* Role Filter - Enhanced */}
                <div className="relative group/filter">
                  <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-50 transition-all duration-300" />
                  <Select value={roleFilter} onValueChange={setRoleFilter}>
                    <SelectTrigger className="relative w-full sm:w-[180px] h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-400/20 transition-all duration-300">
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                          <Filter className="w-3.5 h-3.5 text-slate-300" />
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
                  </SelectContent>
                </Select>
                </div>
              </div>
              
              {/* Active Filter Pills */}
              {(searchQuery || roleFilter !== 'all') && (
                <div className="relative mt-4 flex flex-wrap items-center gap-2">
                  <span className="text-xs text-slate-400">Showing:</span>
                  {searchQuery && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-white/10 text-white text-xs font-medium border border-white/10">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery}&quot;
                      <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-violet-300 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {roleFilter !== 'all' && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-violet-500/20 text-violet-300 text-xs font-medium border border-violet-400/30">
                      <Filter className="w-3 h-3" />
                      {roleFilter === '0' ? 'Owners' : roleFilter === '2' ? 'Managers' : 'Members'}
                      <button onClick={() => setRoleFilter('all')} className="ml-0.5 hover:text-violet-100 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  <span className="text-xs text-slate-400">
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
                  {filteredMembers.map(member => {
                    const roleInfo = getRoleInfo(member.role);
                    const RoleIcon = roleInfo.icon;
                    
                    return (
                      <div 
                        key={member.userId}
                        className="group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:shadow-lg hover:border-violet-200 transition-all cursor-pointer overflow-hidden"
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
                      </div>
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
                  {pendingInvites.map(invite => (
                    <div 
                      key={invite.id}
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
                      <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium bg-blue-100 text-blue-700">
                        <Send className="w-3 h-3" />
                        Pending
                      </span>
                    </div>
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
            isOwner={isOwner}
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
