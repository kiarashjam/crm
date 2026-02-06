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
      className={`group relative overflow-hidden rounded-3xl transition-all duration-300 cursor-pointer ${
        isActive 
          ? 'bg-gradient-to-br from-orange-500 via-orange-500 to-amber-500 p-[2px] shadow-2xl shadow-orange-500/25' 
          : 'bg-gradient-to-br from-slate-200 to-slate-300 p-[1px] hover:from-slate-300 hover:to-slate-400 hover:shadow-xl'
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
      <div className="relative bg-white rounded-[22px] overflow-hidden">
        {/* Background Pattern for Active */}
        {isActive && (
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-48 h-48 bg-orange-500/5 rounded-full" />
            <div className="absolute -bottom-20 -left-20 w-40 h-40 bg-amber-500/5 rounded-full" />
          </div>
        )}

        {/* Header Section */}
        <div className={`relative px-6 pt-6 pb-5 ${isActive ? 'bg-gradient-to-r from-orange-50/80 to-amber-50/50' : 'bg-slate-50/50'}`}>
          <div className="flex items-start gap-5">
            {/* Logo/Avatar */}
            <div className={`relative w-16 h-16 rounded-2xl flex items-center justify-center shadow-xl ${
              isActive 
                ? 'bg-gradient-to-br from-orange-500 to-amber-500 shadow-orange-500/30' 
                : 'bg-gradient-to-br from-slate-500 to-slate-600 shadow-slate-500/20 group-hover:from-slate-600 group-hover:to-slate-700'
            }`}>
              <Building2 className="w-8 h-8 text-white" />
              {isActive && (
                <div className="absolute -top-1 -right-1 w-5 h-5 bg-emerald-500 rounded-full flex items-center justify-center border-2 border-white shadow-lg">
                  <Check className="w-3 h-3 text-white" />
                </div>
              )}
            </div>
            
            {/* Info */}
            <div className="flex-1 min-w-0">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-bold text-xl text-slate-900 truncate">{org.name}</h3>
                  <div className="flex items-center flex-wrap gap-2 mt-2">
                    <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ${
                      org.isOwner 
                        ? 'bg-gradient-to-r from-amber-100 to-yellow-100 text-amber-700 border border-amber-200' 
                        : 'bg-slate-100 text-slate-600 border border-slate-200'
                    }`}>
                      {org.isOwner ? (
                        <>
                          <Crown className="w-3.5 h-3.5" />
                          Owner
                        </>
                      ) : (
                        <>
                          <Users className="w-3.5 h-3.5" />
                          Member
                        </>
                      )}
                    </span>
                    {isActive && (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-gradient-to-r from-emerald-100 to-green-100 text-emerald-700 border border-emerald-200">
                        <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
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
                      className="h-9 w-9 p-0 rounded-xl hover:bg-slate-100"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <MoreHorizontal className="w-5 h-5" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="w-48">
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
            </div>
          </div>
        </div>

        {/* Stats Section */}
        <div className="px-6 py-4 border-t border-slate-100">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-6">
              {memberCount !== undefined && (
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center">
                    <Users className="w-4.5 h-4.5 text-blue-600" />
                  </div>
                  <div>
                    <p className="text-lg font-bold text-slate-900">{memberCount}</p>
                    <p className="text-xs text-slate-500">Member{memberCount !== 1 ? 's' : ''}</p>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
        
        {/* Action Footer */}
        <div className="px-6 pb-6">
          {isActive ? (
            <div className="flex items-center gap-3">
              <div className="flex-1 flex items-center gap-3 px-4 py-3 bg-emerald-50 rounded-2xl border border-emerald-100">
                <div className="w-8 h-8 rounded-xl bg-emerald-100 flex items-center justify-center">
                  <Check className="w-4 h-4 text-emerald-600" />
                </div>
                <span className="text-sm font-semibold text-emerald-700">Currently Active Workspace</span>
              </div>
              <Button 
                variant="outline" 
                size="sm" 
                onClick={(e) => { e.stopPropagation(); onSettings(); }}
                className="h-11 px-4 rounded-xl border-2 border-slate-200 hover:border-slate-300 hover:bg-slate-50"
              >
                <Settings className="w-4 h-4" />
              </Button>
            </div>
          ) : (
            <Button 
              onClick={(e) => { e.stopPropagation(); onSwitch(); }}
              className="w-full h-12 rounded-2xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/20 text-base font-semibold"
            >
              Switch to this workspace
              <ArrowRight className="w-5 h-5 ml-2" />
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
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4 mb-10">
          <div className="group relative overflow-hidden bg-gradient-to-br from-white to-orange-50/30 rounded-2xl border border-orange-100 p-5 hover:shadow-lg hover:shadow-orange-100/50 transition-all duration-300">
            <div className="absolute top-0 right-0 w-16 h-16 bg-orange-500/5 rounded-full -mr-6 -mt-6" />
            <div className="relative">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/20 mb-3">
                <Building2 className="w-6 h-6 text-white" />
              </div>
              <p className="text-3xl font-bold text-slate-900">{organizations.length}</p>
              <p className="text-sm text-slate-500 font-medium">Total Workspaces</p>
            </div>
          </div>
          <div className="group relative overflow-hidden bg-gradient-to-br from-white to-amber-50/30 rounded-2xl border border-amber-100 p-5 hover:shadow-lg hover:shadow-amber-100/50 transition-all duration-300">
            <div className="absolute top-0 right-0 w-16 h-16 bg-amber-500/5 rounded-full -mr-6 -mt-6" />
            <div className="relative">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-amber-500 to-yellow-500 flex items-center justify-center shadow-lg shadow-amber-500/20 mb-3">
                <Crown className="w-6 h-6 text-white" />
              </div>
              <p className="text-3xl font-bold text-slate-900">
                {organizations.filter(o => o.isOwner).length}
              </p>
              <p className="text-sm text-slate-500 font-medium">Owned by You</p>
            </div>
          </div>
          <div className="group relative overflow-hidden bg-gradient-to-br from-white to-blue-50/30 rounded-2xl border border-blue-100 p-5 hover:shadow-lg hover:shadow-blue-100/50 transition-all duration-300">
            <div className="absolute top-0 right-0 w-16 h-16 bg-blue-500/5 rounded-full -mr-6 -mt-6" />
            <div className="relative">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-blue-500 to-indigo-500 flex items-center justify-center shadow-lg shadow-blue-500/20 mb-3">
                <Users className="w-6 h-6 text-white" />
              </div>
              <p className="text-3xl font-bold text-slate-900">
                {organizations.filter(o => !o.isOwner).length}
              </p>
              <p className="text-sm text-slate-500 font-medium">Member Of</p>
            </div>
          </div>
          <div className="group relative overflow-hidden bg-gradient-to-br from-white to-emerald-50/30 rounded-2xl border border-emerald-100 p-5 hover:shadow-lg hover:shadow-emerald-100/50 transition-all duration-300">
            <div className="absolute top-0 right-0 w-16 h-16 bg-emerald-500/5 rounded-full -mr-6 -mt-6" />
            <div className="relative">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center shadow-lg shadow-emerald-500/20 mb-3">
                <Mail className="w-6 h-6 text-white" />
              </div>
              <p className="text-3xl font-bold text-slate-900">{pendingInvites.length}</p>
              <p className="text-sm text-slate-500 font-medium">Pending Invites</p>
            </div>
          </div>
          <div className="group relative overflow-hidden bg-gradient-to-br from-white to-purple-50/30 rounded-2xl border border-purple-100 p-5 hover:shadow-lg hover:shadow-purple-100/50 transition-all duration-300">
            <div className="absolute top-0 right-0 w-16 h-16 bg-purple-500/5 rounded-full -mr-6 -mt-6" />
            <div className="relative">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-purple-500 to-violet-500 flex items-center justify-center shadow-lg shadow-purple-500/20 mb-3">
                <UserCheck className="w-6 h-6 text-white" />
              </div>
              <p className="text-3xl font-bold text-slate-900">{pendingJoinRequests.length}</p>
              <p className="text-sm text-slate-500 font-medium">Join Requests</p>
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
        <section className="mb-10">
          {/* Section Header */}
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/20">
                <Building2 className="w-6 h-6 text-white" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-slate-900">Your Workspaces</h2>
                <p className="text-sm text-slate-500">Select a workspace to manage</p>
              </div>
            </div>
            <span className="text-sm font-medium text-slate-400 bg-slate-100 px-4 py-2 rounded-full">
              {organizations.length} workspace{organizations.length !== 1 ? 's' : ''}
            </span>
          </div>

          {organizations.length === 0 ? (
            <div className="text-center py-16 bg-gradient-to-br from-white to-slate-50 rounded-3xl border-2 border-dashed border-slate-200">
              <div className="w-20 h-20 mx-auto rounded-3xl bg-gradient-to-br from-orange-100 to-amber-100 flex items-center justify-center mb-6 shadow-lg shadow-orange-100">
                <Building2 className="w-10 h-10 text-orange-500" />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-2">No workspaces yet</h3>
              <p className="text-slate-500 mb-8 max-w-md mx-auto">
                Create your first workspace to start managing your team, leads, deals, and more.
              </p>
              <Button 
                onClick={() => setCreateDialogOpen(true)}
                className="h-12 px-8 text-base bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/25 rounded-2xl"
              >
                <Plus className="w-5 h-5 mr-2" />
                Create your first workspace
              </Button>
            </div>
          ) : (
            <div className="grid lg:grid-cols-2 gap-6">
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
        <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-3xl p-8">
          {/* Background decorations */}
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-64 h-64 bg-orange-500/10 rounded-full blur-3xl" />
            <div className="absolute -bottom-20 -left-20 w-48 h-48 bg-blue-500/10 rounded-full blur-3xl" />
          </div>
          
          <div className="relative">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/30">
                <Zap className="w-5 h-5 text-white" />
              </div>
              <div>
                <h3 className="text-lg font-bold text-white">Pro Tips</h3>
                <p className="text-sm text-slate-400">Get the most out of your workspaces</p>
              </div>
            </div>
            
            <div className="grid sm:grid-cols-3 gap-6">
              <div className="bg-white/5 backdrop-blur-sm rounded-2xl p-5 border border-white/10 hover:bg-white/10 transition-colors">
                <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-orange-500/20 to-amber-500/20 flex items-center justify-center mb-4 border border-orange-500/20">
                  <Building2 className="w-5 h-5 text-orange-400" />
                </div>
                <p className="font-semibold text-white mb-1">Multiple Workspaces</p>
                <p className="text-sm text-slate-400 leading-relaxed">Create separate workspaces for different businesses, teams, or clients</p>
              </div>
              <div className="bg-white/5 backdrop-blur-sm rounded-2xl p-5 border border-white/10 hover:bg-white/10 transition-colors">
                <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-blue-500/20 to-indigo-500/20 flex items-center justify-center mb-4 border border-blue-500/20">
                  <Users className="w-5 h-5 text-blue-400" />
                </div>
                <p className="font-semibold text-white mb-1">Invite Your Team</p>
                <p className="text-sm text-slate-400 leading-relaxed">Go to the Team page to add members and collaborate together</p>
              </div>
              <div className="bg-white/5 backdrop-blur-sm rounded-2xl p-5 border border-white/10 hover:bg-white/10 transition-colors">
                <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-emerald-500/20 to-teal-500/20 flex items-center justify-center mb-4 border border-emerald-500/20">
                  <RefreshCw className="w-5 h-5 text-emerald-400" />
                </div>
                <p className="font-semibold text-white mb-1">Easy Switching</p>
                <p className="text-sm text-slate-400 leading-relaxed">Switch between workspaces anytime from this page or the header</p>
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
