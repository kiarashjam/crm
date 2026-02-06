import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Save, Users, Kanban,
  User, Palette, Bell, Shield,
  ChevronRight,
  Settings2, Sparkles,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getUserSettings, saveUserSettings, resetUserSettings,
  getTimezones, getCurrencies, getLanguages,
  twoFactorSetup, twoFactorEnable, twoFactorDisable,
} from '@/app/api';
import type { UserSettings, UpdateUserSettingsRequest, TimezoneOption, CurrencyOption, LanguageOption } from '@/app/api/types';
import { messages } from '@/app/api/messages';
import { ProfileSection } from '@/app/pages/settings/components/ProfileSection';
import { NotificationsSection } from '@/app/pages/settings/components/NotificationsSection';
import { AccountSection } from '@/app/pages/settings/components/AccountSection';
import { AppearanceSection } from '@/app/pages/settings/components/AppearanceSection';
import { PipelinesSection } from '@/app/pages/settings/components/PipelinesSection';
import { OrganizationSection } from '@/app/pages/settings/components/OrganizationSection';
import { SecuritySection } from '@/app/pages/settings/components/SecuritySection';
import { useOrg } from '@/app/contexts/OrgContext';
import { useTheme } from '@/app/contexts/ThemeContext';
import {
  createInvite,
  listPendingInvitesForOrg,
  listPendingJoinRequestsForOrg,
  acceptJoinRequest,
  rejectJoinRequest,
  getOrgMembers,
  updateMemberRole,
  removeMember,
  type InviteDto,
  type JoinRequestDto,
  type OrgMemberDto,
} from '@/app/api/organizations';
import { isUsingRealApi } from '@/app/api/apiClient';
import { getCurrentUser } from '@/app/lib/auth';
import {
  getPipelines,
  createPipeline,
  updatePipeline,
  deletePipeline,
  getDealStagesByPipeline,
  createDealStage,
  updateDealStage,
  deleteDealStage,
} from '@/app/api';
import type { Pipeline, DealStage } from '@/app/api/types';
import { BrandSection } from './settings/components/BrandSection';

type SettingsTab = 'profile' | 'brand' | 'appearance' | 'notifications' | 'organization' | 'pipelines' | 'security' | 'account';

const TAB_CONFIG: { id: SettingsTab; label: string; icon: React.ElementType; description: string }[] = [
  { id: 'profile', label: 'Profile', icon: User, description: 'Personal information and preferences' },
  { id: 'brand', label: 'Brand & AI', icon: Sparkles, description: 'Brand voice and AI copy settings' },
  { id: 'appearance', label: 'Appearance', icon: Palette, description: 'Theme, layout, and display options' },
  { id: 'notifications', label: 'Notifications', icon: Bell, description: 'Email and in-app notifications' },
  { id: 'organization', label: 'Team', icon: Users, description: 'Organization and team members' },
  { id: 'pipelines', label: 'Pipelines', icon: Kanban, description: 'Sales pipeline stages' },
  { id: 'security', label: 'Security', icon: Shield, description: 'Two-factor authentication' },
  { id: 'account', label: 'Account', icon: Settings2, description: 'Account management and data' },
];

export default function Settings() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { organizations, currentOrg, setCurrentOrg, refreshOrgs: _refreshOrgs } = useOrg();
  const themeContext = useTheme();

  // Tab state
  const initialTab = (searchParams.get('tab') as SettingsTab) || 'profile';
  const [activeTab, setActiveTab] = useState<SettingsTab>(initialTab);

  // Settings state
  const [settings, setSettings] = useState<UserSettings | null>(null);
  const [saving, setSaving] = useState(false);
  const [hasChanges, setHasChanges] = useState(false);

  // Options data
  const [timezones, setTimezones] = useState<TimezoneOption[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyOption[]>([]);
  const [languages, setLanguages] = useState<LanguageOption[]>([]);

  // 2FA state
  const [twoFaEnabled, setTwoFaEnabled] = useState<boolean | null>(null);
  const [twoFaLoading, setTwoFaLoading] = useState(false);
  const [twoFaSecret, setTwoFaSecret] = useState<string | null>(null);
  const [twoFaUri, setTwoFaUri] = useState<string | null>(null);
  const [twoFaCode, setTwoFaCode] = useState('');
  const [twoFaDisablePassword, setTwoFaDisablePassword] = useState('');
  const [twoFaDisableCode, setTwoFaDisableCode] = useState('');

  // Account deletion state
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deleteConfirmChecked, setDeleteConfirmChecked] = useState(false);
  const canConfirmDelete = deleteConfirmChecked && deleteConfirmText.trim().toUpperCase() === 'DELETE';

  // Organization state
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviting, setInviting] = useState(false);
  const [ownerInvites, setOwnerInvites] = useState<InviteDto[]>([]);
  const [ownerJoinRequests, setOwnerJoinRequests] = useState<JoinRequestDto[]>([]);
  const [actingJrId, setActingJrId] = useState<string | null>(null);
  const [orgMembers, setOrgMembers] = useState<OrgMemberDto[]>([]);
  const [actingMemberId, setActingMemberId] = useState<string | null>(null);
  const [removeMemberConfirm, setRemoveMemberConfirm] = useState<OrgMemberDto | null>(null);
  const currentUser = getCurrentUser();

  // Pipelines state
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [expandedPipelineId, setExpandedPipelineId] = useState<string | null>(null);
  const [pipelineStages, setPipelineStages] = useState<Record<string, DealStage[]>>({});
  const [newPipelineName, setNewPipelineName] = useState('');
  const [creatingPipeline, setCreatingPipeline] = useState(false);
  const [editingPipelineId, setEditingPipelineId] = useState<string | null>(null);
  const [editingPipelineName, setEditingPipelineName] = useState('');
  const [deletingPipelineId, setDeletingPipelineId] = useState<string | null>(null);
  const [newStagePipelineId, setNewStagePipelineId] = useState<string | null>(null);
  const [newStageName, setNewStageName] = useState('');
  const [creatingStage, setCreatingStage] = useState(false);
  const [editingStageId, setEditingStageId] = useState<string | null>(null);
  const [editingStageName, setEditingStageName] = useState('');
  const [deletingStageId, setDeletingStageId] = useState<string | null>(null);

  // Load settings and options
  useEffect(() => {
    getUserSettings()
      .then(setSettings)
      .catch(() => setSettings({
        companyName: 'My Company',
        brandTone: 'professional',
        theme: 'light',
        dataDensity: 'comfortable',
        sidebarCollapsed: false,
        showWelcomeBanner: true,
        timezone: 'UTC',
        language: 'en',
        emailNotificationsEnabled: true,
        emailOnNewLead: true,
        emailOnDealUpdate: true,
        emailOnTaskDue: true,
        emailOnTeamMention: true,
        emailDigestFrequency: 'daily',
        inAppNotificationsEnabled: true,
        inAppSoundEnabled: true,
        browserNotificationsEnabled: false,
        defaultFollowUpDays: 3,
        defaultCurrency: 'USD',
        showActivityStatus: true,
        allowAnalytics: true,
      }));

    getTimezones().then(setTimezones).catch(() => {});
    getCurrencies().then(setCurrencies).catch(() => {});
    getLanguages().then(setLanguages).catch(() => {});

    twoFactorSetup()
      .then((r) => {
        setTwoFaEnabled(r.enabled);
        setTwoFaSecret(r.secret);
        setTwoFaUri(r.otpauthUri);
      })
      .catch(() => setTwoFaEnabled(false));
  }, []);

  // Sync API settings to ThemeContext when loaded (one-time sync, not reactive)
  useEffect(() => {
    if (settings) {
      themeContext.updateAppearance({
        themeMode: settings.theme, // Map API 'theme' to 'themeMode'
        dataDensity: settings.dataDensity,
        sidebarCollapsed: settings.sidebarCollapsed,
        showWelcomeBanner: settings.showWelcomeBanner,
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Only sync when API settings change, not when themeContext changes
  }, [settings?.theme, settings?.dataDensity, settings?.sidebarCollapsed, settings?.showWelcomeBanner]);

  // Load org data
  useEffect(() => {
    if (!isUsingRealApi() || !currentOrg?.isOwner || !currentOrg?.id) return;
    listPendingInvitesForOrg(currentOrg.id).then(setOwnerInvites).catch(() => setOwnerInvites([]));
    listPendingJoinRequestsForOrg(currentOrg.id).then(setOwnerJoinRequests).catch(() => setOwnerJoinRequests([]));
    getOrgMembers(currentOrg.id).then(setOrgMembers).catch(() => setOrgMembers([]));
  }, [currentOrg?.id, currentOrg?.isOwner]);

  // Load pipelines
  useEffect(() => {
    if (!isUsingRealApi() || !currentOrg?.isOwner || !currentOrg?.id) return;
    getPipelines().then(setPipelines).catch(() => setPipelines([]));
  }, [currentOrg?.id, currentOrg?.isOwner]);

  // Load pipeline stages when expanded
  useEffect(() => {
    if (!expandedPipelineId) return;
    getDealStagesByPipeline(expandedPipelineId).then((list) =>
      setPipelineStages((prev) => ({ ...prev, [expandedPipelineId]: list }))
    ).catch(() => setPipelineStages((prev) => ({ ...prev, [expandedPipelineId]: [] })));
  }, [expandedPipelineId]);

  // Update URL when tab changes
  const handleTabChange = (tab: SettingsTab) => {
    setActiveTab(tab);
    setSearchParams({ tab });
  };

  // Update settings with change tracking
  const updateSettings = useCallback((updates: UpdateUserSettingsRequest) => {
    setSettings((prev) => prev ? { ...prev, ...updates } as UserSettings : prev);
    setHasChanges(true);
  }, []);

  // Save settings
  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    try {
      await saveUserSettings(settings);
      toast.success(messages.settings.saved);
      setHasChanges(false);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  // Reset settings
  const handleReset = async () => {
    setSaving(true);
    try {
      const newSettings = await resetUserSettings();
      setSettings(newSettings);
      toast.success('Settings reset to defaults');
      setHasChanges(false);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  // Organization handlers
  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentOrg?.id || !inviteEmail.trim() || inviting) return;
    setInviting(true);
    try {
      const invite = await createInvite(currentOrg.id, inviteEmail.trim());
      if (invite) {
        toast.success(`Invite sent to ${inviteEmail.trim()}`);
        setInviteEmail('');
        listPendingInvitesForOrg(currentOrg.id).then(setOwnerInvites).catch(() => {});
      } else {
        toast.error('Could not send invite (e.g. already a member).');
      }
    } catch {
      toast.error('Could not send invite.');
    } finally {
      setInviting(false);
    }
  };

  const handleAcceptJr = async (jr: JoinRequestDto) => {
    setActingJrId(jr.id);
    try {
      await acceptJoinRequest(jr.id);
      toast.success(`Accepted ${jr.userName}`);
      if (currentOrg?.id) listPendingJoinRequestsForOrg(currentOrg.id).then(setOwnerJoinRequests).catch(() => {});
    } catch {
      toast.error('Could not accept request.');
    } finally {
      setActingJrId(null);
    }
  };

  const handleRejectJr = async (jr: JoinRequestDto) => {
    setActingJrId(jr.id);
    try {
      await rejectJoinRequest(jr.id);
      toast.success(`Rejected ${jr.userName}`);
      if (currentOrg?.id) listPendingJoinRequestsForOrg(currentOrg.id).then(setOwnerJoinRequests).catch(() => {});
    } catch {
      toast.error('Could not reject request.');
    } finally {
      setActingJrId(null);
    }
  };

  const handleMemberRoleChange = async (member: OrgMemberDto, newRole: number) => {
    if (!currentOrg?.id || member.role === 0) return;
    setActingMemberId(member.userId);
    try {
      const ok = await updateMemberRole(currentOrg.id, member.userId, newRole);
      if (ok) {
        toast.success(`Role updated for ${member.name}`);
        getOrgMembers(currentOrg.id).then(setOrgMembers).catch(() => {});
      } else {
        toast.error('Could not update role.');
      }
    } catch {
      toast.error('Could not update role.');
    } finally {
      setActingMemberId(null);
    }
  };

  const handleRemoveMember = async (member: OrgMemberDto) => {
    if (!currentOrg?.id || member.role === 0) return;
    setActingMemberId(member.userId);
    try {
      const ok = await removeMember(currentOrg.id, member.userId);
      if (ok) {
        toast.success(`${member.name} removed from organization`);
        setRemoveMemberConfirm(null);
        getOrgMembers(currentOrg.id).then(setOrgMembers).catch(() => {});
      } else {
        toast.error('Could not remove member.');
      }
    } catch {
      toast.error('Could not remove member.');
    } finally {
      setActingMemberId(null);
    }
  };

  // Pipeline handlers
  const refreshPipelines = () => {
    getPipelines().then(setPipelines).catch(() => setPipelines([]));
  };

  const handleCreatePipeline = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPipelineName.trim() || creatingPipeline) return;
    setCreatingPipeline(true);
    try {
      const p = await createPipeline({ name: newPipelineName.trim(), displayOrder: pipelines.length });
      if (p) {
        toast.success('Pipeline created');
        setNewPipelineName('');
        refreshPipelines();
      } else {
        toast.error('Could not create pipeline.');
      }
    } catch {
      toast.error('Could not create pipeline.');
    } finally {
      setCreatingPipeline(false);
    }
  };

  const handleUpdatePipeline = async (id: string) => {
    if (!editingPipelineName.trim()) return;
    try {
      const p = await updatePipeline(id, { name: editingPipelineName.trim() });
      if (p) {
        toast.success('Pipeline updated');
        setEditingPipelineId(null);
        setEditingPipelineName('');
        refreshPipelines();
      } else {
        toast.error('Could not update pipeline.');
      }
    } catch {
      toast.error('Could not update pipeline.');
    }
  };

  const handleDeletePipeline = async (id: string) => {
    setDeletingPipelineId(id);
    try {
      const ok = await deletePipeline(id);
      if (ok) {
        toast.success('Pipeline deleted');
        setExpandedPipelineId((prev) => (prev === id ? null : prev));
        setDeletingPipelineId(null);
        refreshPipelines();
      } else {
        toast.error('Could not delete pipeline.');
        setDeletingPipelineId(null);
      }
    } catch {
      toast.error('Could not delete pipeline.');
      setDeletingPipelineId(null);
    }
  };

  const handleCreateStage = async (e: React.FormEvent, pipelineId: string) => {
    e.preventDefault();
    if (!newStageName.trim() || creatingStage) return;
    setCreatingStage(true);
    try {
      const stages = pipelineStages[pipelineId] ?? [];
      const s = await createDealStage({
        pipelineId,
        name: newStageName.trim(),
        displayOrder: stages.length,
        isWon: false,
        isLost: false,
      });
      if (s) {
        toast.success('Stage added');
        setNewStagePipelineId(null);
        setNewStageName('');
        getDealStagesByPipeline(pipelineId).then((list) =>
          setPipelineStages((prev) => ({ ...prev, [pipelineId]: list }))
        ).catch(() => {});
      } else {
        toast.error('Could not add stage.');
      }
    } catch {
      toast.error('Could not add stage.');
    } finally {
      setCreatingStage(false);
    }
  };

  const handleUpdateStage = async (stageId: string, pipelineId: string) => {
    if (!editingStageName.trim()) return;
    try {
      const s = await updateDealStage(stageId, { name: editingStageName.trim() });
      if (s) {
        toast.success('Stage updated');
        setEditingStageId(null);
        setEditingStageName('');
        getDealStagesByPipeline(pipelineId).then((list) =>
          setPipelineStages((prev) => ({ ...prev, [pipelineId]: list }))
        ).catch(() => {});
      } else {
        toast.error('Could not update stage.');
      }
    } catch {
      toast.error('Could not update stage.');
    }
  };

  const handleDeleteStage = async (stageId: string, pipelineId: string) => {
    setDeletingStageId(stageId);
    try {
      const ok = await deleteDealStage(stageId);
      if (ok) {
        toast.success('Stage deleted');
        setDeletingStageId(null);
        getDealStagesByPipeline(pipelineId).then((list) =>
          setPipelineStages((prev) => ({ ...prev, [pipelineId]: list }))
        ).catch(() => {});
      } else {
        toast.error('Could not delete stage.');
        setDeletingStageId(null);
      }
    } catch {
      toast.error('Could not delete stage.');
      setDeletingStageId(null);
    }
  };

  // 2FA handlers
  const handleStart2fa = async () => {
    setTwoFaLoading(true);
    try {
      const r = await twoFactorSetup();
      setTwoFaEnabled(r.enabled);
      setTwoFaSecret(r.secret);
      setTwoFaUri(r.otpauthUri);
      toast.message(messages.twoFa.scanSecretConfirm);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setTwoFaLoading(false);
    }
  };

  const handleEnable2fa = async () => {
    if (twoFaCode.replace(/\D/g, '').length !== 6) return;
    setTwoFaLoading(true);
    try {
      await twoFactorEnable(twoFaCode);
      setTwoFaEnabled(true);
      setTwoFaCode('');
      toast.success(messages.twoFa.enabled);
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      toast.error(msg);
    } finally {
      setTwoFaLoading(false);
    }
  };

  const handleDisable2fa = async () => {
    if (!twoFaDisablePassword || twoFaDisableCode.replace(/\D/g, '').length !== 6) return;
    setTwoFaLoading(true);
    try {
      await twoFactorDisable(twoFaDisablePassword, twoFaDisableCode);
      setTwoFaEnabled(false);
      setTwoFaSecret(null);
      setTwoFaUri(null);
      setTwoFaDisablePassword('');
      setTwoFaDisableCode('');
      toast.success(messages.twoFa.disabled);
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      toast.error(msg);
    } finally {
      setTwoFaLoading(false);
    }
  };

  const handleDeleteAccount = () => {
    if (showDeleteConfirm) {
      navigate('/');
    } else {
      setShowDeleteConfirm(true);
    }
  };

  // Loading state
  if (settings === null) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)] flex justify-center items-center" tabIndex={-1}>
          <LoadingSpinner size="lg" />
        </main>
      </div>
    );
  }

  // Render section content
  const renderTabContent = () => {
    switch (activeTab) {
      case 'profile':
        return <ProfileSection settings={settings} updateSettings={updateSettings} timezones={timezones} languages={languages} />;
      case 'brand':
        return <BrandSection settings={settings} updateSettings={updateSettings} />;
      case 'appearance':
        return <AppearanceSection settings={settings} updateSettings={updateSettings} />;
      case 'notifications':
        return <NotificationsSection settings={settings} updateSettings={updateSettings} />;
      case 'organization':
        return (
          <OrganizationSection
            organizations={organizations}
            currentOrg={currentOrg}
            setCurrentOrg={setCurrentOrg}
            inviteEmail={inviteEmail}
            setInviteEmail={setInviteEmail}
            handleInvite={handleInvite}
            inviting={inviting}
            ownerInvites={ownerInvites}
            ownerJoinRequests={ownerJoinRequests}
            handleAcceptJr={handleAcceptJr}
            handleRejectJr={handleRejectJr}
            actingJrId={actingJrId}
            orgMembers={orgMembers}
            handleMemberRoleChange={handleMemberRoleChange}
            actingMemberId={actingMemberId}
            removeMemberConfirm={removeMemberConfirm}
            setRemoveMemberConfirm={setRemoveMemberConfirm}
            handleRemoveMember={handleRemoveMember}
            currentUser={currentUser}
          />
        );
      case 'pipelines':
        return (
          <PipelinesSection
            pipelines={pipelines}
            pipelineStages={pipelineStages}
            expandedPipelineId={expandedPipelineId}
            setExpandedPipelineId={setExpandedPipelineId}
            newPipelineName={newPipelineName}
            setNewPipelineName={setNewPipelineName}
            handleCreatePipeline={handleCreatePipeline}
            creatingPipeline={creatingPipeline}
            editingPipelineId={editingPipelineId}
            setEditingPipelineId={setEditingPipelineId}
            editingPipelineName={editingPipelineName}
            setEditingPipelineName={setEditingPipelineName}
            handleUpdatePipeline={handleUpdatePipeline}
            handleDeletePipeline={handleDeletePipeline}
            deletingPipelineId={deletingPipelineId}
            newStagePipelineId={newStagePipelineId}
            setNewStagePipelineId={setNewStagePipelineId}
            newStageName={newStageName}
            setNewStageName={setNewStageName}
            handleCreateStage={handleCreateStage}
            creatingStage={creatingStage}
            editingStageId={editingStageId}
            setEditingStageId={setEditingStageId}
            editingStageName={editingStageName}
            setEditingStageName={setEditingStageName}
            handleUpdateStage={handleUpdateStage}
            handleDeleteStage={handleDeleteStage}
            deletingStageId={deletingStageId}
            currentOrg={currentOrg}
          />
        );
      case 'security':
        return (
          <SecuritySection
            twoFaEnabled={twoFaEnabled}
            twoFaLoading={twoFaLoading}
            twoFaSecret={twoFaSecret}
            twoFaUri={twoFaUri}
            twoFaCode={twoFaCode}
            setTwoFaCode={setTwoFaCode}
            twoFaDisablePassword={twoFaDisablePassword}
            setTwoFaDisablePassword={setTwoFaDisablePassword}
            twoFaDisableCode={twoFaDisableCode}
            setTwoFaDisableCode={setTwoFaDisableCode}
            handleStart2fa={handleStart2fa}
            handleEnable2fa={handleEnable2fa}
            handleDisable2fa={handleDisable2fa}
          />
        );
      case 'account':
        return (
          <AccountSection
            settings={settings}
            updateSettings={updateSettings}
            currencies={currencies}
            showDeleteConfirm={showDeleteConfirm}
            setShowDeleteConfirm={setShowDeleteConfirm}
            deleteConfirmText={deleteConfirmText}
            setDeleteConfirmText={setDeleteConfirmText}
            deleteConfirmChecked={deleteConfirmChecked}
            setDeleteConfirmChecked={setDeleteConfirmChecked}
            canConfirmDelete={canConfirmDelete}
            handleDeleteAccount={handleDeleteAccount}
            handleReset={handleReset}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

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
                    <Settings2 className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Settings</h1>
                    <p className="text-slate-400 mt-1">Manage your account, preferences, and team settings</p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Main layout */}
          <div className="flex flex-col lg:flex-row gap-8">
            {/* Sidebar navigation */}
            <nav className="lg:w-64 shrink-0">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                <div className="p-2">
                  {TAB_CONFIG.map((tab) => {
                    const Icon = tab.icon;
                    const isActive = activeTab === tab.id;
                    return (
                      <button
                        key={tab.id}
                        onClick={() => handleTabChange(tab.id)}
                        className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl text-left transition-all ${
                          isActive
                            ? 'bg-orange-50 text-orange-700 font-medium'
                            : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
                        }`}
                      >
                        <Icon className={`w-5 h-5 shrink-0 ${isActive ? 'text-orange-600' : 'text-slate-400'}`} />
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium truncate">{tab.label}</p>
                          <p className={`text-xs truncate ${isActive ? 'text-orange-600/70' : 'text-slate-400'}`}>
                            {tab.description}
                          </p>
                        </div>
                        <ChevronRight className={`w-4 h-4 shrink-0 ${isActive ? 'text-orange-400' : 'text-slate-300'}`} />
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Save button (sticky on desktop) */}
              <div className="hidden lg:block mt-4 sticky top-4">
                <button
                  type="button"
                  onClick={handleSave}
                  disabled={saving || !hasChanges}
                  className="w-full h-11 bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-400 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-colors flex items-center justify-center gap-2"
                >
                  <Save className="w-5 h-5" />
                  {saving ? 'Saving...' : hasChanges ? 'Save Changes' : 'No Changes'}
                </button>
              </div>
            </nav>

            {/* Content area */}
            <div className="flex-1 min-w-0">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm">
                {renderTabContent()}
              </div>

              {/* Mobile save button */}
              <div className="lg:hidden mt-4">
                <button
                  type="button"
                  onClick={handleSave}
                  disabled={saving || !hasChanges}
                  className="w-full h-12 bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-400 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-colors flex items-center justify-center gap-2"
                >
                  <Save className="w-5 h-5" />
                  {saving ? 'Saving...' : hasChanges ? 'Save Changes' : 'No Changes'}
                </button>
              </div>
            </div>
          </div>
      </main>

      {/* Remove member modal */}
      {removeMemberConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40" role="dialog" aria-modal="true">
          <div className="bg-white rounded-xl shadow-lg max-w-md w-full p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-2">Remove member?</h3>
            <p className="text-slate-600 text-sm mb-4">
              {removeMemberConfirm.name} ({removeMemberConfirm.email}) will lose access. This cannot be undone.
            </p>
            <div className="flex gap-2 justify-end">
              <button
                type="button"
                onClick={() => setRemoveMemberConfirm(null)}
                className="h-10 px-4 rounded-lg border border-slate-300 text-slate-700 font-medium hover:bg-slate-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => handleRemoveMember(removeMemberConfirm)}
                disabled={actingMemberId === removeMemberConfirm.userId}
                className="h-10 px-4 rounded-lg bg-red-600 text-white font-medium hover:bg-red-700 disabled:opacity-50"
              >
                {actingMemberId === removeMemberConfirm.userId ? 'Removing...' : 'Remove'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}




