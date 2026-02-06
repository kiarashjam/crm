import { useState, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { 
  ArrowLeft, Building2, Mail, Plus, UserPlus, Check, Crown,
  Settings, Users, RefreshCw, Clock,
  Zap, ArrowRight, Copy, MoreHorizontal,
  Briefcase, Target, BarChart3, X, UserCheck
} from 'lucide-react';
import { toast } from 'sonner';
import {
  createOrganization,
  acceptInviteById,
  getOrgMembers,
  listPendingJoinRequestsForOrg,
  acceptJoinRequest,
  rejectJoinRequest,
  type Organization,
  type InviteDto,
  type JoinRequestDto,
} from '@/app/api/organizations';
import { setCurrentOrganizationId } from '@/app/lib/auth';
import { useOrg } from '@/app/contexts/OrgContext';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import DemoBanner from '@/app/components/DemoBanner';
import AppHeader from '@/app/components/AppHeader';
import { isUsingRealApi } from '@/app/api/apiClient';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogTitle,
} from '@/app/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';

// Organization Card Component
function OrganizationCard({
  org,
  isActive,
  onSwitch,
  onSettings,
  onNavigate,
  memberCount,
}: {
  org: Organization;
  isActive: boolean;
  onSwitch: () => void;
  onSettings: () => void;
  onNavigate: () => void;
  memberCount?: number;
}) {
  return (
    <div 
      className={`relative overflow-hidden bg-white rounded-2xl border-2 transition-all cursor-pointer ${
        isActive 
          ? 'border-orange-400 shadow-lg shadow-orange-100/50 ring-2 ring-orange-100' 
          : 'border-slate-200 hover:border-slate-300 hover:shadow-md'
      }`}
      onClick={onNavigate}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onNavigate();
        }
      }}
    >
      {/* Active indicator */}
      {isActive && (
        <div className="absolute top-0 left-0 right-0 h-1.5 bg-gradient-to-r from-orange-500 to-amber-500" />
      )}
      
      <div className="p-5">
        <div className="flex items-start gap-4">
          {/* Logo/Avatar */}
          <div className={`w-14 h-14 rounded-xl flex items-center justify-center shadow-lg ${
            isActive 
              ? 'bg-gradient-to-br from-orange-500 to-amber-500' 
              : 'bg-gradient-to-br from-slate-400 to-slate-500'
          }`}>
            <Building2 className="w-7 h-7 text-white" />
          </div>
          
          {/* Info */}
          <div className="flex-1 min-w-0">
            <div className="flex items-start justify-between gap-2">
              <div>
                <h3 className="font-bold text-lg text-slate-900 truncate">{org.name}</h3>
                <div className="flex items-center gap-2 mt-1">
                  <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${
                    org.isOwner 
                      ? 'bg-amber-100 text-amber-700' 
                      : 'bg-slate-100 text-slate-600'
                  }`}>
                    {org.isOwner ? (
                      <>
                        <Crown className="w-3 h-3" />
                        Owner
                      </>
                    ) : (
                      <>
                        <Users className="w-3 h-3" />
                        Member
                      </>
                    )}
                  </span>
                  {isActive && (
                    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700">
                      <Check className="w-3 h-3" />
                      Active
                    </span>
                  )}
                </div>
              </div>
              
              {/* Actions Menu */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button 
                    variant="ghost" 
                    size="sm" 
                    className="h-8 w-8 p-0"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <MoreHorizontal className="w-4 h-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  {!isActive && (
                    <DropdownMenuItem onClick={(e) => { e.stopPropagation(); onSwitch(); }}>
                      <ArrowRight className="w-4 h-4 mr-2" />
                      Switch to this org
                    </DropdownMenuItem>
                  )}
                  {org.isOwner && (
                    <DropdownMenuItem onClick={(e) => { e.stopPropagation(); onSettings(); }}>
                      <Settings className="w-4 h-4 mr-2" />
                      Settings
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuItem onClick={(e) => {
                    e.stopPropagation();
                    navigator.clipboard.writeText(org.id);
                    toast.success('Organization ID copied');
                  }}>
                    <Copy className="w-4 h-4 mr-2" />
                    Copy ID
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
            
            {/* Stats row */}
            <div className="flex items-center gap-4 mt-3 text-sm text-slate-500">
              {memberCount !== undefined && (
                <span className="flex items-center gap-1.5">
                  <Users className="w-4 h-4" />
                  {memberCount} member{memberCount !== 1 ? 's' : ''}
                </span>
              )}
            </div>
          </div>
        </div>
        
        {/* Action Button */}
        <div className="mt-4 pt-4 border-t border-slate-100">
          {isActive ? (
            <div className="flex items-center justify-between">
              <span className="text-sm text-emerald-600 font-medium flex items-center gap-1.5">
                <Check className="w-4 h-4" />
                Currently active
              </span>
              <Button 
                variant="outline" 
                size="sm" 
                onClick={(e) => { e.stopPropagation(); onSettings(); }}
                className="text-slate-600"
              >
                <Settings className="w-4 h-4 mr-1.5" />
                Settings
              </Button>
            </div>
          ) : (
            <Button 
              onClick={(e) => { e.stopPropagation(); onSwitch(); }}
              className="w-full bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-200/50"
            >
              Switch to this organization
              <ArrowRight className="w-4 h-4 ml-2" />
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

// Create Organization Dialog
function CreateOrgDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (org: Organization) => void;
}) {
  const [name, setName] = useState('');
  const [creating, setCreating] = useState(false);
  const { addDemoOrg } = useOrg();

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || creating) return;
    
    setCreating(true);
    try {
      if (addDemoOrg) {
        const org = addDemoOrg(name.trim());
        onCreated(org);
        toast.success('Organization created!');
        setName('');
        onOpenChange(false);
      } else {
        const org = await createOrganization(name.trim());
        if (org) {
          onCreated(org);
          toast.success('Organization created!');
          setName('');
          onOpenChange(false);
        } else {
          toast.error('Could not create organization.');
        }
      }
    } catch {
      toast.error('Could not create organization.');
    } finally {
      setCreating(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px] p-0 gap-0 overflow-hidden">
        {/* Gradient Header */}
        <div className="relative overflow-hidden">
          <div className="absolute inset-0 bg-gradient-to-br from-orange-500 via-orange-400 to-amber-400" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
          <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
          
          <div className="relative px-6 py-5">
            <div className="flex items-center gap-4">
              <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                <Building2 className="w-6 h-6 text-white" />
              </div>
              <div>
                <DialogTitle className="text-xl font-bold text-white tracking-tight">
                  Create Organization
                </DialogTitle>
                <p className="text-white/80 text-sm mt-0.5">
                  Start a new workspace for your team
                </p>
              </div>
            </div>
          </div>
        </div>

        <form onSubmit={handleCreate} className="p-6">
          <div className="space-y-4">
            {/* Info Card */}
            <div className="bg-orange-50 rounded-xl p-4 border border-orange-100">
              <p className="text-sm text-orange-800">
                <strong>What's an organization?</strong> An organization is a separate workspace 
                with its own team, leads, deals, and settings. You can create multiple organizations 
                for different businesses or teams.
              </p>
            </div>

            {/* Name Field */}
            <div>
              <Label htmlFor="org-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <Building2 className="w-4 h-4 text-orange-500" />
                Organization Name
              </Label>
              <Input
                id="org-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Acme Corp, My Startup, Sales Team"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300"
                required
              />
              <p className="text-xs text-slate-500 mt-1.5">
                Choose a name that represents your company or team
              </p>
            </div>

            {/* Features preview */}
            <div className="bg-slate-50 rounded-xl p-4">
              <p className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-3">
                What you'll get
              </p>
              <div className="grid grid-cols-2 gap-2">
                {[
                  { icon: Users, label: 'Team management' },
                  { icon: Target, label: 'Lead tracking' },
                  { icon: Briefcase, label: 'Deal pipeline' },
                  { icon: BarChart3, label: 'Reports & insights' },
                ].map(({ icon: Icon, label }) => (
                  <div key={label} className="flex items-center gap-2 text-sm text-slate-600">
                    <Icon className="w-4 h-4 text-orange-500" />
                    {label}
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3 pt-6">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              className="flex-1 h-11"
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={creating || !name.trim()}
              className="flex-[2] h-11 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600"
            >
              {creating ? (
                <span className="flex items-center gap-2">
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  Creating...
                </span>
              ) : (
                <span className="flex items-center gap-2">
                  <Plus className="w-4 h-4" />
                  Create Organization
                </span>
              )}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export default function Organizations() {
  const navigate = useNavigate();
  const location = useLocation();
  const { organizations, pendingInvites, refreshOrgs, loading, hasFetched, addDemoOrg: _addDemoOrg, currentOrgId } = useOrg();
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [memberCounts, setMemberCounts] = useState<Record<string, number>>({});
  const [pendingJoinRequests, setPendingJoinRequests] = useState<JoinRequestDto[]>([]);
  const [processingJoinRequestId, setProcessingJoinRequestId] = useState<string | null>(null);

  // Check if we came from the app (has header) or from login flow
  const isFromApp = location.state?.fromApp || (hasFetched && organizations.length > 0 && currentOrgId);

  // Load member counts and pending join requests for orgs the user owns
  useEffect(() => {
    if (!isUsingRealApi() || !hasFetched) return;
    
    organizations.forEach(async (org) => {
      try {
        const members = await getOrgMembers(org.id);
        setMemberCounts(prev => ({ ...prev, [org.id]: members.length }));
      } catch {
        // Ignore errors
      }
    });

    // Load pending join requests for organizations the user owns
    const loadJoinRequests = async () => {
      const ownedOrgs = organizations.filter(o => o.isOwner);
      const allRequests: JoinRequestDto[] = [];
      
      for (const org of ownedOrgs) {
        try {
          const requests = await listPendingJoinRequestsForOrg(org.id);
          allRequests.push(...requests);
        } catch {
          // Ignore errors
        }
      }
      
      setPendingJoinRequests(allRequests);
    };
    
    loadJoinRequests();
  }, [organizations, hasFetched]);

  const handleSwitchOrg = (org: Organization) => {
    setCurrentOrganizationId(org.id);
    toast.success(`Switched to ${org.name}`);
    navigate('/dashboard', { replace: true });
  };

  const handleOrgCreated = async (org: Organization) => {
    setCurrentOrganizationId(org.id);
    await refreshOrgs();
    navigate('/onboarding', { replace: true });
  };

  const handleAcceptInvite = async (invite: InviteDto) => {
    setAcceptingId(invite.id);
    try {
      const accepted = await acceptInviteById(invite.id);
      if (accepted) {
        await refreshOrgs();
        setCurrentOrganizationId(accepted.organizationId);
        toast.success(`Joined ${accepted.organizationName}!`);
        navigate('/dashboard', { replace: true });
      } else {
        toast.error('Could not accept invite.');
      }
    } catch {
      toast.error('Could not accept invite.');
    } finally {
      setAcceptingId(null);
    }
  };

  const handleAcceptJoinRequest = async (request: JoinRequestDto) => {
    setProcessingJoinRequestId(request.id);
    try {
      const result = await acceptJoinRequest(request.id);
      if (result) {
        toast.success(`${request.userName} has been added to ${request.organizationName}`);
        setPendingJoinRequests(prev => prev.filter(r => r.id !== request.id));
        // Refresh member counts
        const members = await getOrgMembers(request.organizationId);
        setMemberCounts(prev => ({ ...prev, [request.organizationId]: members.length }));
      } else {
        toast.error('Could not accept join request.');
      }
    } catch {
      toast.error('Could not accept join request.');
    } finally {
      setProcessingJoinRequestId(null);
    }
  };

  const handleRejectJoinRequest = async (request: JoinRequestDto) => {
    setProcessingJoinRequestId(request.id);
    try {
      const result = await rejectJoinRequest(request.id);
      if (result) {
        toast.success(`Join request from ${request.userName} has been rejected`);
        setPendingJoinRequests(prev => prev.filter(r => r.id !== request.id));
      } else {
        toast.error('Could not reject join request.');
      }
    } catch {
      toast.error('Could not reject join request.');
    } finally {
      setProcessingJoinRequestId(null);
    }
  };

  // Show loading state
  if (!hasFetched || loading) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-orange-50 to-slate-50 flex items-center justify-center px-4">
        <LoadingSpinner size="lg" label="Loading organizations…" />
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      {/* Use AppHeader if coming from within the app */}
      {isFromApp ? (
        <AppHeader />
      ) : (
        <header className="w-full px-[var(--page-padding)] py-4 border-b border-slate-200/60 bg-white/80" role="banner">
          <Link
            to="/dashboard"
            className="inline-flex items-center gap-2 text-slate-600 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg"
          >
            <ArrowLeft className="w-5 h-5" aria-hidden />
            Back to Dashboard
          </Link>
        </header>
      )}
      
      <DemoBanner />
      
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        {/* Header */}
        <div className="mb-8">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-2">
            <div className="flex items-center gap-4">
              <div className="w-14 h-14 bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl flex items-center justify-center shadow-lg shadow-orange-200/50">
                <Building2 className="w-7 h-7 text-white" />
              </div>
              <div>
                <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Organizations</h1>
                <p className="text-slate-600 mt-0.5">
                  Switch between workspaces or create a new one
                </p>
              </div>
            </div>
            <Button 
              onClick={() => setCreateDialogOpen(true)}
              className="gap-2 bg-orange-600 hover:bg-orange-700 shadow-lg shadow-orange-200/50"
            >
              <Plus className="w-4 h-4" />
              New organization
            </Button>
          </div>
        </div>

        {/* Stats Overview */}
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4 mb-8">
          <div className="bg-white rounded-xl border border-slate-200 p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-orange-100 flex items-center justify-center">
                <Building2 className="w-5 h-5 text-orange-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">{organizations.length}</p>
                <p className="text-xs text-slate-500">Organization{organizations.length !== 1 ? 's' : ''}</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-slate-200 p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center">
                <Crown className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {organizations.filter(o => o.isOwner).length}
                </p>
                <p className="text-xs text-slate-500">Owned by you</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-slate-200 p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center">
                <Users className="w-5 h-5 text-blue-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {organizations.filter(o => !o.isOwner).length}
                </p>
                <p className="text-xs text-slate-500">Member of</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-slate-200 p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center">
                <Mail className="w-5 h-5 text-emerald-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">{pendingInvites.length}</p>
                <p className="text-xs text-slate-500">Pending invite{pendingInvites.length !== 1 ? 's' : ''}</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-slate-200 p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-purple-100 flex items-center justify-center">
                <UserCheck className="w-5 h-5 text-purple-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">{pendingJoinRequests.length}</p>
                <p className="text-xs text-slate-500">Join request{pendingJoinRequests.length !== 1 ? 's' : ''}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Pending Invites Alert */}
        {pendingInvites.length > 0 && (
          <div className="mb-8 bg-gradient-to-r from-emerald-50 to-teal-50 rounded-2xl border border-emerald-200 p-5">
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center shrink-0">
                <Mail className="w-5 h-5 text-emerald-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-slate-900 mb-1">
                  You have {pendingInvites.length} pending invitation{pendingInvites.length !== 1 ? 's' : ''}
                </h3>
                <p className="text-sm text-slate-600 mb-4">
                  Accept to join these organizations
                </p>
                <div className="space-y-2">
                  {pendingInvites.map(invite => (
                    <div 
                      key={invite.id}
                      className="flex items-center justify-between gap-4 p-3 bg-white rounded-xl border border-emerald-100"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center">
                          <Building2 className="w-5 h-5 text-white" />
                        </div>
                        <div>
                          <p className="font-medium text-slate-900">{invite.organizationName}</p>
                          <p className="text-xs text-slate-500 flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            Expires {new Date(invite.expiresAtUtc).toLocaleDateString()}
                          </p>
                        </div>
                      </div>
                      <Button
                        size="sm"
                        onClick={() => handleAcceptInvite(invite)}
                        disabled={acceptingId === invite.id}
                        className="bg-emerald-600 hover:bg-emerald-700"
                      >
                        {acceptingId === invite.id ? (
                          <RefreshCw className="w-4 h-4 animate-spin" />
                        ) : (
                          <>
                            <UserPlus className="w-4 h-4 mr-1.5" />
                            Accept
                          </>
                        )}
                      </Button>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Pending Join Requests Alert - for organization owners */}
        {pendingJoinRequests.length > 0 && (
          <div className="mb-8 bg-gradient-to-r from-purple-50 to-violet-50 rounded-2xl border border-purple-200 p-5">
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-xl bg-purple-100 flex items-center justify-center shrink-0">
                <UserCheck className="w-5 h-5 text-purple-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-slate-900 mb-1">
                  {pendingJoinRequests.length} join request{pendingJoinRequests.length !== 1 ? 's' : ''} pending approval
                </h3>
                <p className="text-sm text-slate-600 mb-4">
                  Review and approve or reject requests to join your organizations
                </p>
                <div className="space-y-2">
                  {pendingJoinRequests.map(request => (
                    <div 
                      key={request.id}
                      className="flex items-center justify-between gap-4 p-3 bg-white rounded-xl border border-purple-100"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-purple-500 to-violet-500 flex items-center justify-center">
                          <UserPlus className="w-5 h-5 text-white" />
                        </div>
                        <div>
                          <p className="font-medium text-slate-900">{request.userName}</p>
                          <p className="text-xs text-slate-500">{request.userEmail}</p>
                          <p className="text-xs text-purple-600 flex items-center gap-1 mt-0.5">
                            <Building2 className="w-3 h-3" />
                            Wants to join {request.organizationName}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleRejectJoinRequest(request)}
                          disabled={processingJoinRequestId === request.id}
                          className="text-red-600 border-red-200 hover:bg-red-50"
                        >
                          {processingJoinRequestId === request.id ? (
                            <RefreshCw className="w-4 h-4 animate-spin" />
                          ) : (
                            <>
                              <X className="w-4 h-4 mr-1" />
                              Reject
                            </>
                          )}
                        </Button>
                        <Button
                          size="sm"
                          onClick={() => handleAcceptJoinRequest(request)}
                          disabled={processingJoinRequestId === request.id}
                          className="bg-purple-600 hover:bg-purple-700"
                        >
                          {processingJoinRequestId === request.id ? (
                            <RefreshCw className="w-4 h-4 animate-spin" />
                          ) : (
                            <>
                              <Check className="w-4 h-4 mr-1" />
                              Approve
                            </>
                          )}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Organizations List */}
        <section>
          <h2 className="text-lg font-semibold text-slate-900 flex items-center gap-2 mb-4">
            <Building2 className="w-5 h-5 text-orange-500" />
            Your Organizations
          </h2>

          {organizations.length === 0 ? (
            <div className="text-center py-12 bg-white rounded-2xl border border-slate-200">
              <div className="w-16 h-16 mx-auto rounded-2xl bg-orange-100 flex items-center justify-center mb-4">
                <Building2 className="w-8 h-8 text-orange-500" />
              </div>
              <h3 className="text-lg font-semibold text-slate-900 mb-2">No organizations yet</h3>
              <p className="text-slate-500 mb-6 max-w-sm mx-auto">
                Create your first organization to get started, or wait for an invitation to join one.
              </p>
              <Button 
                onClick={() => setCreateDialogOpen(true)}
                className="bg-orange-600 hover:bg-orange-700"
              >
                <Plus className="w-4 h-4 mr-2" />
                Create your first organization
              </Button>
            </div>
          ) : (
            <div className="grid sm:grid-cols-2 gap-4">
              {/* Sort: active org first, then by ownership */}
              {[...organizations]
                .sort((a, b) => {
                  if (a.id === currentOrgId) return -1;
                  if (b.id === currentOrgId) return 1;
                  if (a.isOwner && !b.isOwner) return -1;
                  if (!a.isOwner && b.isOwner) return 1;
                  return a.name.localeCompare(b.name);
                })
                .map(org => (
                  <OrganizationCard
                    key={org.id}
                    org={org}
                    isActive={org.id === currentOrgId}
                    onSwitch={() => handleSwitchOrg(org)}
                    onSettings={() => navigate('/settings')}
                    onNavigate={() => {
                      // If not active, switch first then navigate
                      if (org.id !== currentOrgId) {
                        setCurrentOrganizationId(org.id);
                        toast.success(`Switched to ${org.name}`);
                      }
                      navigate('/dashboard');
                    }}
                    memberCount={memberCounts[org.id]}
                  />
                ))}
            </div>
          )}
        </section>

        {/* Quick Tips */}
        <section className="mt-8 bg-gradient-to-r from-slate-50 to-orange-50/30 rounded-2xl p-6 border border-slate-200">
          <h3 className="text-sm font-semibold text-slate-800 mb-4 flex items-center gap-2">
            <Zap className="w-4 h-4 text-orange-500" />
            Quick Tips
          </h3>
          <div className="grid sm:grid-cols-3 gap-4">
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-lg bg-orange-100 flex items-center justify-center shrink-0">
                <Building2 className="w-4 h-4 text-orange-600" />
              </div>
              <div>
                <p className="font-medium text-slate-800 text-sm">Multiple workspaces</p>
                <p className="text-xs text-slate-500">Create separate organizations for different businesses or clients</p>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-lg bg-blue-100 flex items-center justify-center shrink-0">
                <Users className="w-4 h-4 text-blue-600" />
              </div>
              <div>
                <p className="font-medium text-slate-800 text-sm">Invite your team</p>
                <p className="text-xs text-slate-500">Go to the Team page to add members to your organization</p>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-lg bg-emerald-100 flex items-center justify-center shrink-0">
                <RefreshCw className="w-4 h-4 text-emerald-600" />
              </div>
              <div>
                <p className="font-medium text-slate-800 text-sm">Easy switching</p>
                <p className="text-xs text-slate-500">Switch between organizations anytime from this page</p>
              </div>
            </div>
          </div>
        </section>
      </main>

      {/* Create Organization Dialog */}
      <CreateOrgDialog
        open={createDialogOpen}
        onOpenChange={setCreateDialogOpen}
        onCreated={handleOrgCreated}
      />
    </div>
  );
}
