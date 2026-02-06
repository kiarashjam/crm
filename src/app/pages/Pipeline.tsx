import { useState, useEffect, useMemo } from 'react';
import { DndProvider } from 'react-dnd';
import { HTML5Backend } from 'react-dnd-html5-backend';
import {
  Briefcase,
  Plus,
  Trash2,
  Pencil,
  Target,
  Handshake,
  Trophy,
  DollarSign,
  LayoutGrid,
  List,
  User,
  AlertCircle,
  Sparkles,
  Search,
  X,
  TrendingUp,
  BarChart3,
  Clock,
  Zap,
  Building2,
  Filter,
  CheckCircle2,
  Activity as ActivityIcon,
} from 'lucide-react';
import { toast } from 'sonner';
import { motion } from 'motion/react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Cell,
  Tooltip,
} from 'recharts';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { KanbanSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getDeals, updateDeal, createDeal, deleteDeal, getCompanies, getContacts, getPipelines, getOrgMembers, messages } from '@/app/api';
import { getActivitiesByDeal, createActivity } from '@/app/api/activities';
import type { Deal, Company, Contact, Pipeline, Activity } from '@/app/api/types';
import { getCurrentOrganizationId } from '@/app/lib/auth';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/app/components/ui/sheet';
import { Tabs, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { cn } from '@/app/components/ui/utils';

// Import extracted components and utilities
import { DroppableStageColumn } from './pipeline/DroppableStageColumn';
import { STAGE_COLORS_MAP } from './pipeline/config';
import { getStageList, groupDealsByStage, formatValueSum, getDaysUntilClose, UrgencyBadge } from './pipeline/utils';

export default function Pipeline() {
  const [deals, setDeals] = useState<Deal[]>([]);
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [selectedPipelineId, setSelectedPipelineId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<'kanban' | 'list'>('kanban');
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({
    name: '',
    value: '',
    currency: 'CHF',
    stageId: '',
    pipelineId: '',
    expectedCloseDate: '',
    companyId: '',
    contactId: '',
    assigneeId: '',
  });
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [saving, setSaving] = useState(false);
  const [editDeal, setEditDeal] = useState<Deal | null>(null);
  const [editForm, setEditForm] = useState({ name: '', value: '', currency: '', expectedCloseDate: '', companyId: '', pipelineId: '', stageId: '', assigneeId: '' });
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleteConfirmDeal, setDeleteConfirmDeal] = useState<Deal | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [detailDeal, setDetailDeal] = useState<Deal | null>(null);
  const [orgMembers, setOrgMembers] = useState<{ userId: string; name: string; email: string; role: number }[]>([]);
  
  // Deal activities state
  const [dealActivities, setDealActivities] = useState<Activity[]>([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [activityForm, setActivityForm] = useState({ type: 'Note', subject: '', body: '' });
  const [savingActivity, setSavingActivity] = useState(false);
  
  // Search and Filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [filterStage, setFilterStage] = useState<string>('all');
  const [_showFilters, _setShowFilters] = useState(false);

  const stageList = getStageList(pipelines, selectedPipelineId);
  
  // Calculate deal statistics
  const dealStats = useMemo(() => {
    const total = deals.length;
    const totalValue = deals.reduce((acc, d) => {
      const num = parseFloat(String(d.value).replace(/[^0-9.-]/g, '')) || 0;
      return acc + num;
    }, 0);
    
    const won = deals.filter(d => {
      const stage = d.dealStageId ? stageList.find(s => s.id === d.dealStageId)?.name : d.stage;
      return stage === 'Closed Won';
    });
    const lost = deals.filter(d => {
      const stage = d.dealStageId ? stageList.find(s => s.id === d.dealStageId)?.name : d.stage;
      return stage === 'Closed Lost';
    });
    const active = deals.filter(d => {
      const stage = d.dealStageId ? stageList.find(s => s.id === d.dealStageId)?.name : d.stage;
      return stage !== 'Closed Won' && stage !== 'Closed Lost';
    });
    
    const wonValue = won.reduce((acc, d) => acc + (parseFloat(String(d.value).replace(/[^0-9.-]/g, '')) || 0), 0);
    const activeValue = active.reduce((acc, d) => acc + (parseFloat(String(d.value).replace(/[^0-9.-]/g, '')) || 0), 0);
    
    const winRate = (won.length + lost.length) > 0 
      ? Math.round((won.length / (won.length + lost.length)) * 100) 
      : 0;
    
    // Overdue deals
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const overdue = active.filter(d => {
      if (!d.expectedCloseDateUtc) return false;
      const closeDate = new Date(d.expectedCloseDateUtc);
      closeDate.setHours(0, 0, 0, 0);
      return closeDate < today;
    }).length;
    
    // Closing this week
    const oneWeekFromNow = new Date();
    oneWeekFromNow.setDate(oneWeekFromNow.getDate() + 7);
    const closingThisWeek = active.filter(d => {
      if (!d.expectedCloseDateUtc) return false;
      const closeDate = new Date(d.expectedCloseDateUtc);
      return closeDate >= today && closeDate <= oneWeekFromNow;
    }).length;
    
    // Average deal size
    const avgDealSize = total > 0 ? totalValue / total : 0;
    
    return { 
      total, totalValue, won: won.length, lost: lost.length, active: active.length,
      wonValue, activeValue, winRate, overdue, closingThisWeek, avgDealSize
    };
  }, [deals, stageList]);

  // Filter deals
  const filteredDeals = useMemo(() => {
    let result = [...deals];
    
    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(d => 
        d.name.toLowerCase().includes(q) ||
        d.value.toLowerCase().includes(q) ||
        (contacts.find(c => c.id === d.contactId)?.name.toLowerCase().includes(q))
      );
    }
    
    // Stage filter
    if (filterStage !== 'all') {
      result = result.filter(d => {
        const stage = d.dealStageId ? stageList.find(s => s.id === d.dealStageId)?.name : d.stage;
        return stage === filterStage;
      });
    }
    
    return result;
  }, [deals, searchQuery, filterStage, contacts, stageList]);

  const dealsByStage = groupDealsByStage(filteredDeals, stageList);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getDeals(), getContacts(), getPipelines()])
      .then(([dealsData, contactsData, pipelinesData]) => {
        if (!cancelled) {
          setDeals(dealsData);
          setContacts(contactsData);
          setPipelines(pipelinesData ?? []);
          if (pipelinesData?.length && !selectedPipelineId) setSelectedPipelineId(pipelinesData?.[0]?.id ?? null);
        }
      })
      .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (createOpen || editDeal || detailDeal) {
      getCompanies().then(setCompanies).catch(() => {});
    }
  }, [createOpen, editDeal, detailDeal]);

  // Fetch activities when deal detail is opened
  useEffect(() => {
    if (detailDeal) {
      setActivitiesLoading(true);
      setDealActivities([]);
      getActivitiesByDeal(detailDeal.id)
        .then(setDealActivities)
        .catch(() => setDealActivities([]))
        .finally(() => setActivitiesLoading(false));
    }
  }, [detailDeal]);

  useEffect(() => {
    const orgId = getCurrentOrganizationId();
    if (orgId) getOrgMembers(orgId).then(setOrgMembers).catch(() => setOrgMembers([]));
    else setOrgMembers([]);
  }, []);

  useEffect(() => {
    if (editDeal) {
      setEditForm({
        name: editDeal.name,
        value: editDeal.value,
        currency: editDeal.currency ?? '',
        expectedCloseDate: editDeal.expectedCloseDateUtc ? editDeal.expectedCloseDateUtc.slice(0, 10) : '',
        companyId: editDeal.companyId || '',
        pipelineId: editDeal.pipelineId ?? '',
        stageId: editDeal.dealStageId ?? (editDeal.stage ?? ''),
        assigneeId: editDeal.assigneeId ?? '',
      });
    }
  }, [editDeal]);

  const funnelData = dealsByStage.map(({ stageName, deals: stageDeals }) => ({
    name: stageName,
    count: stageDeals.length,
    value: stageDeals.reduce((acc, d) => {
      const num = parseFloat(String(d.value).replace(/[^0-9.-]/g, '')) || 0;
      return acc + num;
    }, 0),
    fill: STAGE_COLORS_MAP[stageName]?.bar ?? '#94a3b8',
  }));

  const handleMoveStage = async (dealId: string, newStageId: string, newStageName: string) => {
    const deal = deals.find((d) => d.id === dealId);
    const currentStageId = deal?.dealStageId ?? deal?.stage ?? '';
    if (deal && currentStageId === newStageId) return;
    const isWon = newStageName === 'Closed Won' ? true : newStageName === 'Closed Lost' ? false : undefined;
    const payload: { stage: string; dealStageId?: string; isWon?: boolean } = { stage: newStageName };
    if (/^[0-9a-f-]{36}$/i.test(newStageId)) payload.dealStageId = newStageId;
    if (isWon !== undefined) payload.isWon = isWon;
    try {
      const updated = await updateDeal(dealId, payload);
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === dealId ? updated : d)));
        toast.success(messages.success.dealMoved);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmDeal) return;
    setDeleting(true);
    try {
      const ok = await deleteDeal(deleteConfirmDeal.id);
      if (ok) {
        setDeals((prev) => prev.filter((d) => d.id !== deleteConfirmDeal.id));
        toast.success(messages.success.dealDeleted);
        setDeleteConfirmDeal(null);
        if (detailDeal?.id === deleteConfirmDeal.id) setDetailDeal(null);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const editStageList = getStageList(pipelines, editForm.pipelineId || editDeal?.pipelineId || selectedPipelineId);

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editDeal) return;
    if (!editForm.name.trim() || !editForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    setSavingEdit(true);
    try {
      const expectedCloseDateUtc = editForm.expectedCloseDate
        ? new Date(editForm.expectedCloseDate).toISOString()
        : undefined;
      const stageName = editStageList.find((s) => s.id === editForm.stageId)?.name ?? editForm.stageId;
      const updated = await updateDeal(editDeal.id, {
        name: editForm.name.trim(),
        value: editForm.value.trim(),
        currency: editForm.currency || undefined,
        stage: stageName,
        pipelineId: editForm.pipelineId || undefined,
        dealStageId: /^[0-9a-f-]{36}$/i.test(editForm.stageId) ? editForm.stageId : undefined,
        expectedCloseDateUtc,
        companyId: editForm.companyId || undefined,
        assigneeId: editForm.assigneeId || undefined,
      });
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === editDeal.id ? updated : d)));
        toast.success(messages.success.dealUpdated);
        setEditDeal(null);
        if (detailDeal?.id === editDeal.id) setDetailDeal(updated);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSavingEdit(false);
    }
  };

  const setExpectedCloseInDays = (days: number) => {
    const d = new Date();
    d.setDate(d.getDate() + days);
    setCreateForm((f) => ({ ...f, expectedCloseDate: d.toISOString().slice(0, 10) }));
  };

  const createStageList = getStageList(pipelines, createForm.pipelineId || selectedPipelineId);
  const openCreateDeal = () => {
    const pipelineId = selectedPipelineId ?? pipelines[0]?.id ?? '';
    const firstStage = getStageList(pipelines, pipelineId)[0];
    setCreateForm({
      name: '',
      value: '',
      currency: '',
      stageId: firstStage?.id ?? 'Qualification',
      pipelineId,
      expectedCloseDate: '',
      companyId: '',
      contactId: '',
      assigneeId: '',
    });
    setCreateOpen(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.name.trim() || !createForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    setSaving(true);
    try {
      const expectedCloseDateUtc = createForm.expectedCloseDate
        ? new Date(createForm.expectedCloseDate).toISOString()
        : undefined;
      const stageName = createStageList.find((s) => s.id === createForm.stageId)?.name ?? createForm.stageId;
      const created = await createDeal({
        name: createForm.name.trim(),
        value: createForm.value.trim(),
        currency: createForm.currency.trim() || undefined,
        stage: stageName,
        pipelineId: createForm.pipelineId || undefined,
        dealStageId: /^[0-9a-f-]{36}$/i.test(createForm.stageId) ? createForm.stageId : undefined,
        expectedCloseDateUtc,
        companyId: createForm.companyId || undefined,
        contactId: createForm.contactId || undefined,
      });
      if (created) {
        setDeals((prev) => [created, ...prev]);
        toast.success(messages.success.dealCreated);
        setCreateOpen(false);
        const pipelineId = selectedPipelineId ?? pipelines[0]?.id ?? '';
        const firstStage = getStageList(pipelines, pipelineId)[0];
        setCreateForm({ name: '', value: '', currency: '', stageId: firstStage?.id ?? 'Qualification', pipelineId, expectedCloseDate: '', companyId: '', contactId: '', assigneeId: '' });
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  // Handle logging activity for a deal
  const handleLogActivity = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detailDeal || !activityForm.subject.trim()) {
      toast.error('Subject is required');
      return;
    }
    setSavingActivity(true);
    try {
      const activity = await createActivity({
        type: activityForm.type,
        subject: activityForm.subject.trim(),
        body: activityForm.body.trim() || undefined,
        dealId: detailDeal.id,
      });
      if (activity) {
        setDealActivities((prev) => [activity, ...prev]);
        setActivityForm({ type: 'Note', subject: '', body: '' });
        toast.success('Activity logged');
        // Update the deal's lastActivityAtUtc in local state
        setDeals((prev) =>
          prev.map((d) =>
            d.id === detailDeal.id
              ? { ...d, lastActivityAtUtc: activity.createdAt }
              : d
          )
        );
        setDetailDeal((prev) =>
          prev ? { ...prev, lastActivityAtUtc: activity.createdAt } : prev
        );
      } else {
        toast.error('Failed to log activity');
      }
    } catch {
      toast.error('Failed to log activity');
    } finally {
      setSavingActivity(false);
    }
  };

  const dealStageName = (d: Deal) => {
    if (d.dealStageId) {
      const s = stageList.find((x) => x.id === d.dealStageId);
      if (s) return s.name;
    }
    return d.stage || 'Qualification';
  };
  const activeDeals = deals.filter((d) => dealStageName(d) !== 'Closed Lost');
  const totalPipelineValue = formatValueSum(activeDeals.map((d) => d.value));

  const detailContact = detailDeal?.contactId ? contacts.find((c) => c.id === detailDeal.contactId) : undefined;
  const detailCompany = detailDeal?.companyId ? companies.find((c) => c.id === detailDeal.companyId) : undefined;

  return (
    <div className="min-h-screen flex flex-col overflow-hidden bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 min-h-0 flex flex-col w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {/* Hero Section with Dark Decorative Elements */}
          <motion.div 
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5 }}
          className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8 shrink-0"
        >
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
                  <Target className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Deals</h1>
                  <p className="text-slate-400 mt-1">
                    {loading ? 'Loading your pipeline…' : 'Track opportunities from qualification to close'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                {!loading && deals.length > 0 && (
                  <div className="hidden sm:flex items-center gap-3 mr-2">
                    <div className="flex items-center gap-2 px-3 py-1.5 rounded-full bg-white/10 border border-white/20">
                      <div className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
                      <span className="text-sm text-white font-medium">{dealStats.active} active</span>
                    </div>
                    <div className="flex items-center gap-2 px-3 py-1.5 rounded-full bg-white/10 border border-white/20">
                      <TrendingUp className="w-4 h-4 text-emerald-400" />
                      <span className="text-sm text-white font-medium">{totalPipelineValue}</span>
                    </div>
                  </div>
                )}
                <Button
                  onClick={openCreateDeal}
                  className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  New Deal
                </Button>
              </div>
            </div>
          </div>
        </motion.div>

        {/* Stats Grid - Modern Bento Box Style */}
        {!loading && deals.length > 0 && (
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.5, delay: 0.2 }}
              className="grid grid-cols-2 lg:grid-cols-12 gap-4 mb-8"
            >
              {/* Pipeline Value - Large Card */}
              <div className="col-span-2 lg:col-span-4 group relative overflow-hidden rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-600 p-6 shadow-lg shadow-emerald-500/20 hover:shadow-xl hover:shadow-emerald-500/30 transition-all duration-300">
                <div className="absolute top-0 right-0 w-24 h-24 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
                <div className="relative">
                  <div className="flex items-center justify-between">
                    <div className="w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center">
                      <DollarSign className="w-6 h-6 text-white" />
                    </div>
                    <div className="flex items-center gap-1 px-2 py-1 rounded-full bg-white/20 backdrop-blur-sm">
                      <TrendingUp className="w-3 h-3 text-emerald-200" />
                      <span className="text-xs text-white font-medium">Active</span>
                    </div>
                  </div>
                  <div className="mt-4">
                    <p className="text-3xl font-bold text-white tracking-tight">{formatValueSum([dealStats.activeValue.toString()])}</p>
                    <p className="text-emerald-100 text-sm mt-1">Total Pipeline Value</p>
                  </div>
                  <div className="mt-4 flex items-center gap-3">
                    <div className="flex-1 h-2 bg-white/20 rounded-full overflow-hidden">
                      <motion.div 
                        initial={{ width: 0 }}
                        animate={{ width: `${Math.min((dealStats.wonValue / (dealStats.activeValue + dealStats.wonValue || 1)) * 100, 100)}%` }}
                        transition={{ duration: 1, delay: 0.5 }}
                        className="h-full bg-white rounded-full"
                      />
                    </div>
                    <span className="text-xs text-emerald-100">{formatValueSum([dealStats.wonValue.toString()])} won</span>
                  </div>
                </div>
              </div>

              {/* Win Rate - Medium Card */}
              <div className="col-span-1 lg:col-span-2 group relative overflow-hidden rounded-2xl bg-white border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="flex items-center justify-between mb-3">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center group-hover:scale-110 transition-transform">
                      <TrendingUp className="w-5 h-5 text-blue-600" />
                    </div>
                    <span className={cn(
                      "text-xs font-semibold px-2 py-1 rounded-full",
                      dealStats.winRate >= 50 ? "bg-emerald-100 text-emerald-700" : "bg-amber-100 text-amber-700"
                    )}>
                      {dealStats.winRate >= 50 ? 'Great' : 'Needs work'}
                    </span>
                  </div>
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{dealStats.winRate}%</p>
                  <p className="text-xs font-medium text-blue-600/70 mt-1">Win Rate</p>
                  <div className="mt-3 relative">
                    <div className="w-full bg-blue-100 rounded-full h-1.5 overflow-hidden">
                      <motion.div 
                        initial={{ width: 0 }}
                        animate={{ width: `${dealStats.winRate}%` }}
                        transition={{ duration: 1, delay: 0.3 }}
                        className="bg-gradient-to-r from-blue-500 to-cyan-400 h-1.5 rounded-full"
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* Total Deals */}
              <div className="col-span-1 lg:col-span-2 group relative overflow-hidden rounded-2xl bg-white border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{dealStats.total}</p>
                  <p className="text-xs font-medium text-slate-500 mt-1">Total Deals</p>
                  <div className="mt-2 flex items-center gap-2 text-xs text-slate-500">
                    <span className="px-2 py-0.5 rounded-md bg-slate-100">{dealStats.active} open</span>
                    <span className="px-2 py-0.5 rounded-md bg-slate-100">{dealStats.won + dealStats.lost} closed</span>
                  </div>
                </div>
              </div>

              {/* Won Deals */}
              <div 
                className="col-span-1 lg:col-span-2 group relative overflow-hidden rounded-2xl bg-gradient-to-br from-amber-500 to-orange-600 p-5 shadow-lg shadow-amber-500/20 hover:shadow-xl hover:shadow-amber-500/30 transition-all duration-300 cursor-pointer"
                onClick={() => setFilterStage('Closed Won')}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3 group-hover:scale-110 group-hover:rotate-12 transition-all">
                    <Trophy className="w-5 h-5 text-white" />
                  </div>
                  <p className="text-3xl font-bold text-white tracking-tight">{dealStats.won}</p>
                  <p className="text-xs font-medium text-white/80 mt-1">Deals Won</p>
                  {dealStats.wonValue > 0 && (
                    <p className="mt-2 text-xs font-semibold text-white bg-white/20 px-2 py-1 rounded-lg inline-block">
                      {formatValueSum([dealStats.wonValue.toString()])}
                    </p>
                  )}
                </div>
              </div>

              {/* Overdue - Alert Card */}
              <div className={cn(
                "col-span-1 lg:col-span-2 group relative overflow-hidden rounded-2xl p-5 transition-all duration-300",
                dealStats.overdue > 0 
                  ? "bg-gradient-to-br from-red-500 to-rose-600 shadow-lg shadow-red-500/20 hover:shadow-xl hover:shadow-red-500/30"
                  : "bg-white border border-emerald-100 shadow-sm hover:shadow-xl hover:shadow-emerald-100 hover:border-emerald-200"
              )}>
                <div className={cn(
                  "absolute top-0 right-0 w-20 h-20 rounded-bl-[60px] -mr-2 -mt-2",
                  dealStats.overdue > 0 ? "bg-white/10" : "bg-gradient-to-br from-emerald-50 to-emerald-100 group-hover:scale-110 transition-transform"
                )} />
                <div className="relative">
                  <div className={cn(
                    "w-10 h-10 rounded-xl flex items-center justify-center mb-3 transition-transform group-hover:scale-110",
                    dealStats.overdue > 0 
                      ? "bg-white/20 backdrop-blur-sm" 
                      : "bg-gradient-to-br from-emerald-100 to-emerald-200"
                  )}>
                    {dealStats.overdue > 0 ? (
                      <AlertCircle className="w-5 h-5 text-white" />
                    ) : (
                      <CheckCircle2 className="w-5 h-5 text-emerald-600" />
                    )}
                  </div>
                  <p className={cn(
                    "text-3xl font-bold tracking-tight",
                    dealStats.overdue > 0 ? "text-white" : "text-emerald-600"
                  )}>{dealStats.overdue}</p>
                  <p className={cn(
                    "text-xs font-medium mt-1",
                    dealStats.overdue > 0 ? "text-white/80" : "text-emerald-600/70"
                  )}>{dealStats.overdue > 0 ? 'Overdue Deals' : 'All on track!'}</p>
                  {dealStats.overdue > 0 && (
                    <span className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-white bg-white/20 px-2 py-1 rounded-full">
                      <Clock className="w-3 h-3" />
                      Action needed
                    </span>
                  )}
                </div>
              </div>

            </motion.div>
          )}

          {/* Action Alert Banner */}
          {!loading && deals.length > 0 && dealStats.overdue > 0 && (
            <motion.div 
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.4 }}
              className="mt-4 relative overflow-hidden rounded-2xl bg-gradient-to-r from-red-500 via-rose-500 to-pink-500 p-[1px] shadow-lg shadow-red-200/50"
            >
              <div className="relative bg-gradient-to-r from-red-50 via-rose-50 to-pink-50 rounded-2xl p-4">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-red-500 to-rose-500 flex items-center justify-center shadow-lg shadow-red-300/50 shrink-0">
                    <AlertCircle className="w-6 h-6 text-white" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-bold text-red-800 flex items-center gap-2">
                      <Zap className="w-4 h-4" />
                      Immediate Attention Required
                    </p>
                    <p className="mt-0.5 text-sm text-red-700">
                      {dealStats.overdue} deal{dealStats.overdue > 1 ? 's are' : ' is'} past the expected close date. Review and update to keep your pipeline healthy.
                    </p>
                  </div>
                  <Button 
                    variant="outline" 
                    size="sm" 
                    className="shrink-0 border-red-300 text-red-700 hover:bg-red-100 hover:border-red-400"
                    onClick={() => setFilterStage('all')}
                  >
                    Review Now
                  </Button>
                </div>
              </div>
            </motion.div>
          )}

          {/* Search and Filter Bar - Modern Dark Theme */}
          {!loading && deals.length > 0 && (
            <motion.div 
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.3 }}
              className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mt-6 shadow-xl overflow-hidden"
            >
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-emerald-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-teal-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-col sm:flex-row gap-3">
                {/* Search Input - Compact Enhanced */}
                <div className="relative flex-1 group">
                  <div className="absolute inset-0 bg-gradient-to-r from-emerald-500/20 via-teal-500/20 to-emerald-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-emerald-500/20 to-teal-500/20 flex items-center justify-center group-focus-within:from-emerald-500/40 group-focus-within:to-teal-500/40 transition-all duration-300">
                      <Search className="w-4 h-4 text-emerald-300 group-focus-within:text-emerald-200 transition-colors" aria-hidden />
                    </div>
                    <input
                      type="search"
                      placeholder="Search deals by name, value, or contact..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 text-sm focus:outline-none focus:border-emerald-400/50 focus:ring-2 focus:ring-emerald-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300 placeholder:text-slate-400"
                      aria-label="Search deals"
                    />
                    {searchQuery && (
                      <button
                        type="button"
                        onClick={() => setSearchQuery('')}
                        className="absolute right-3 top-1/2 -translate-y-1/2 w-7 h-7 rounded-lg bg-white/10 hover:bg-red-500/30 flex items-center justify-center text-slate-400 hover:text-red-300 transition-all duration-200"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                {/* Stage Filter - Compact Enhanced */}
                <div className="relative group/filter">
                  <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-50 transition-all duration-300" />
                  <Select value={filterStage} onValueChange={setFilterStage}>
                    <SelectTrigger className="relative h-11 w-full sm:w-[200px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-emerald-400/50 focus:ring-2 focus:ring-emerald-400/20 transition-all duration-300">
                      <div className="flex items-center gap-2">
                        <div className="w-7 h-7 rounded-lg bg-white/10 flex items-center justify-center">
                          <Filter className="w-3.5 h-3.5 text-slate-300" />
                        </div>
                        <SelectValue placeholder="All stages" />
                      </div>
                    </SelectTrigger>
                  <SelectContent className="rounded-xl border border-slate-200 shadow-xl">
                    <SelectItem value="all" className="rounded-lg">All stages</SelectItem>
                    {stageList.map((stage) => (
                      <SelectItem key={stage.id} value={stage.name} className="rounded-lg">
                        <span className="flex items-center gap-2">
                          <span 
                            className="w-3 h-3 rounded-full shadow-sm"
                            style={{ backgroundColor: STAGE_COLORS_MAP[stage.name]?.bar ?? '#94a3b8' }}
                          />
                          {stage.name}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              </div>

              {/* Active Filter Pills */}
              {(searchQuery || filterStage !== 'all') && (
                <div className="relative mt-4 flex flex-wrap items-center gap-2">
                  <span className="text-xs text-slate-400 font-medium">Active filters:</span>
                  {searchQuery && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-white/10 text-white text-xs font-medium border border-white/10">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery}&quot;
                      <button onClick={() => setSearchQuery('')} className="ml-1 hover:text-emerald-300 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {filterStage !== 'all' && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-emerald-500/20 text-emerald-300 text-xs font-medium border border-emerald-400/30">
                      <span 
                        className="w-2 h-2 rounded-full bg-emerald-300"
                      />
                      {filterStage}
                      <button onClick={() => setFilterStage('all')} className="ml-1 hover:text-emerald-100 transition-colors">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  <span className="text-xs text-slate-400 ml-2">
                    {filteredDeals.length} of {deals.length} deals
                  </span>
                </div>
              )}
            </motion.div>
          )}

        {loading ? (
          <KanbanSkeleton columns={5} />
        ) : deals.length === 0 ? (
          /* Empty State - matching Leads page style */
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-emerald-500 via-teal-500 to-cyan-500 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <Target className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Build Your Sales Pipeline</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Track deals from qualification to close. Visualize your pipeline and forecast revenue.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-3 gap-4 mb-6">
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mx-auto mb-2">
                      <Target className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Qualify</p>
                    <p className="text-xs text-slate-500 mt-0.5">Identify promising opportunities</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center mx-auto mb-2">
                      <Handshake className="w-5 h-5 text-amber-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Negotiate</p>
                    <p className="text-xs text-slate-500 mt-0.5">Move deals through stages</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mx-auto mb-2">
                      <Trophy className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Close</p>
                    <p className="text-xs text-slate-500 mt-0.5">Win more deals, grow revenue</p>
                  </div>
                </div>
                
                <div className="flex flex-col sm:flex-row gap-3 justify-center">
                  <Button onClick={openCreateDeal} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50">
                    <Plus className="w-4 h-4" />
                    Add Your First Deal
                  </Button>
                </div>
              </div>
            </div>
          </div>
        ) : filteredDeals.length === 0 && (searchQuery || filterStage !== 'all') ? (
          /* No results state */
          <motion.div 
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="flex items-center gap-5 p-6 rounded-2xl bg-white border-2 border-dashed border-slate-200 shadow-lg shadow-slate-100"
          >
            <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center">
              <Search className="w-8 h-8 text-slate-400" />
            </div>
            <div className="flex-1">
              <p className="text-base font-semibold text-slate-700">No deals match your filters</p>
              <p className="text-sm text-slate-500 mt-1">Try adjusting your search or filter criteria to find what you're looking for</p>
            </div>
            <Button 
              variant="outline" 
              onClick={() => { setSearchQuery(''); setFilterStage('all'); }}
              className="shrink-0 h-11 px-6 rounded-xl border-2 hover:bg-slate-50"
            >
              Clear all filters
            </Button>
          </motion.div>
        ) : (
          <>
            {/* Pipeline Visualization - Modern Funnel */}
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.15, duration: 0.3 }}
              className="shrink-0 mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-lg shadow-slate-100"
            >
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center shadow-lg shadow-indigo-200/50">
                    <BarChart3 className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <h3 className="text-base font-semibold text-slate-800">Pipeline Distribution</h3>
                    <p className="text-xs text-slate-500">Deals by stage</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {funnelData.slice(0, 3).map((stage, i) => (
                    <div key={i} className="flex items-center gap-1.5 text-xs text-slate-500">
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: stage.fill }} />
                      <span className="hidden sm:inline">{stage.name}</span>
                    </div>
                  ))}
                </div>
              </div>
              <div className="h-[140px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={funnelData} layout="vertical" margin={{ top: 0, right: 30, left: 90, bottom: 0 }}>
                    <XAxis type="number" hide />
                    <YAxis 
                      type="category" 
                      dataKey="name" 
                      width={85} 
                      tick={{ fontSize: 12, fill: '#64748b', fontWeight: 500 }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <Tooltip
                      formatter={(value: number, _name: string) => [value, 'Deals']}
                      labelFormatter={(label) => label}
                      contentStyle={{ 
                        borderRadius: 12, 
                        border: 'none',
                        boxShadow: '0 10px 40px rgba(0,0,0,0.15)',
                        padding: '12px 16px'
                      }}
                      cursor={{ fill: 'rgba(0,0,0,0.05)', radius: 8 }}
                    />
                    <Bar dataKey="count" radius={[4, 8, 8, 4]} maxBarSize={28}>
                      {funnelData.map((entry, index) => (
                        <Cell key={index} fill={entry.fill} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </motion.div>

            {/* View Toggle - Floating Pills */}
            <div className="flex items-center justify-between mb-5 shrink-0">
              <Tabs value={viewMode} onValueChange={(v) => setViewMode(v as 'kanban' | 'list')}>
                <TabsList className="h-12 p-1.5 bg-slate-100/80 backdrop-blur-sm rounded-2xl border border-slate-200 shadow-lg shadow-slate-100">
                  <TabsTrigger 
                    value="kanban" 
                    className="gap-2 h-9 px-5 rounded-xl data-[state=active]:bg-white data-[state=active]:shadow-md data-[state=active]:text-emerald-700 font-medium transition-all"
                  >
                    <LayoutGrid className="w-4 h-4" />
                    Kanban
                  </TabsTrigger>
                  <TabsTrigger 
                    value="list" 
                    className="gap-2 h-9 px-5 rounded-xl data-[state=active]:bg-white data-[state=active]:shadow-md data-[state=active]:text-emerald-700 font-medium transition-all"
                  >
                    <List className="w-4 h-4" />
                    List View
                  </TabsTrigger>
                </TabsList>
              </Tabs>
              
              <div className="hidden sm:flex items-center gap-2 text-xs text-slate-500">
                <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100">
                  <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                  <span>{filteredDeals.length} deals</span>
                </div>
              </div>
            </div>

            {viewMode === 'kanban' ? (
              <DndProvider backend={HTML5Backend}>
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="flex-1 min-h-0 flex gap-5 overflow-x-auto overflow-y-hidden pb-6 -mx-2 px-2"
                >
                  {dealsByStage.map(({ stageId: colStageId, stageName: colStageName, deals: stageDeals }, index) => (
                    <motion.div
                      key={colStageId}
                      initial={{ opacity: 0, x: 20 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ delay: index * 0.1 }}
                    >
                      <DroppableStageColumn
                        stageId={colStageId}
                        stageName={colStageName}
                        deals={stageDeals}
                        contacts={contacts}
                        stageList={stageList}
                        onMoveStage={handleMoveStage}
                        onEdit={setEditDeal}
                        onDelete={setDeleteConfirmDeal}
                        onOpenDetail={setDetailDeal}
                      />
                    </motion.div>
                  ))}
                </motion.div>
              </DndProvider>
            ) : (
              <motion.div
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className="flex-1 min-h-0 overflow-auto"
              >
                <div className="rounded-2xl border-2 border-slate-200 bg-white overflow-hidden shadow-lg shadow-slate-100">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b-2 border-slate-200 bg-gradient-to-r from-slate-50 to-slate-100">
                        <th className="text-left py-4 px-6 font-bold text-slate-700 uppercase text-xs tracking-wider">Deal</th>
                        <th className="text-left py-4 px-6 font-bold text-slate-700 uppercase text-xs tracking-wider">Value</th>
                        <th className="text-left py-4 px-6 font-bold text-slate-700 uppercase text-xs tracking-wider">Stage</th>
                        <th className="text-left py-4 px-6 font-bold text-slate-700 uppercase text-xs tracking-wider">Contact</th>
                        <th className="text-left py-4 px-6 font-bold text-slate-700 uppercase text-xs tracking-wider">Close Date</th>
                        <th className="w-20" />
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {filteredDeals.map((deal, index) => {
                        const stageName = dealStageName(deal);
                        const config = STAGE_COLORS_MAP[stageName] ?? STAGE_COLORS_MAP.Qualification;
                        const contact = deal.contactId ? contacts.find((c) => c.id === deal.contactId) : undefined;
                        const daysToClose = getDaysUntilClose(deal.expectedCloseDateUtc);
                        return (
                          <motion.tr
                            key={deal.id}
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            transition={{ delay: index * 0.03 }}
                            className="group hover:bg-gradient-to-r hover:from-slate-50 hover:to-emerald-50/30 transition-all duration-200"
                          >
                            <td className="py-4 px-6">
                              <button
                                type="button"
                                onClick={() => setDetailDeal(deal)}
                                className="font-semibold text-slate-800 hover:text-emerald-600 text-left transition-colors group-hover:translate-x-1 transform duration-200"
                              >
                                {deal.name}
                              </button>
                            </td>
                            <td className="py-4 px-6">
                              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-gradient-to-r from-emerald-500/10 to-teal-500/10 border border-emerald-200 text-emerald-700 font-bold text-sm">
                                <DollarSign className="w-3.5 h-3.5" />
                                {deal.currency ? `${deal.currency} ${deal.value}` : deal.value}
                              </span>
                            </td>
                            <td className="py-4 px-6">
                              <span className={cn(
                                'inline-flex items-center gap-2 rounded-xl px-3 py-1.5 text-xs font-bold',
                                stageName === 'Closed Won' && 'bg-emerald-100 text-emerald-700',
                                stageName === 'Closed Lost' && 'bg-slate-100 text-slate-600',
                                stageName === 'Qualification' && 'bg-blue-100 text-blue-700',
                                stageName === 'Proposal' && 'bg-amber-100 text-amber-700',
                                stageName === 'Negotiation' && 'bg-violet-100 text-violet-700',
                                !['Closed Won', 'Closed Lost', 'Qualification', 'Proposal', 'Negotiation'].includes(stageName) && 'bg-slate-100 text-slate-600'
                              )}>
                                <span 
                                  className="w-2 h-2 rounded-full"
                                  style={{ backgroundColor: config?.bar }}
                                />
                                {stageName}
                              </span>
                            </td>
                            <td className="py-4 px-6">
                              {contact ? (
                                <div className="flex items-center gap-2">
                                  <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center">
                                    <User className="w-4 h-4 text-slate-500" />
                                  </div>
                                  <span className="text-slate-700 font-medium">{contact.name}</span>
                                </div>
                              ) : (
                                <span className="text-slate-400">—</span>
                              )}
                            </td>
                            <td className="py-4 px-6">
                              {deal.expectedCloseDateUtc ? (
                                <div className="flex flex-col gap-1">
                                  <span className="text-slate-700 font-medium">
                                    {new Date(deal.expectedCloseDateUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                                  </span>
                                  {daysToClose !== null && <UrgencyBadge days={daysToClose} />}
                                </div>
                              ) : (
                                <span className="text-slate-400">—</span>
                              )}
                            </td>
                            <td className="py-4 px-6">
                              <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                                <Button 
                                  variant="ghost" 
                                  size="icon" 
                                  className="h-9 w-9 rounded-xl text-slate-400 hover:text-emerald-600 hover:bg-emerald-50" 
                                  onClick={() => setEditDeal(deal)} 
                                  aria-label="Edit"
                                >
                                  <Pencil className="w-4 h-4" />
                                </Button>
                                <Button 
                                  variant="ghost" 
                                  size="icon" 
                                  className="h-9 w-9 rounded-xl text-slate-400 hover:text-red-600 hover:bg-red-50" 
                                  onClick={() => setDeleteConfirmDeal(deal)} 
                                  aria-label="Delete"
                                >
                                  <Trash2 className="w-4 h-4" />
                                </Button>
                              </div>
                            </td>
                          </motion.tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </motion.div>
            )}
          </>
        )}
        </main>
      </PageTransition>

      {/* Deal detail sheet */}
      <Sheet open={!!detailDeal} onOpenChange={(open) => !open && setDetailDeal(null)}>
        <SheetContent side="right" className="w-full sm:max-w-md">
          {detailDeal && (
            <>
              <SheetHeader>
                <SheetTitle className="text-xl">{detailDeal.name}</SheetTitle>
              </SheetHeader>
              <div className="flex flex-col gap-4 py-4 overflow-auto flex-1">
                {/* Deal Info Section */}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Value</p>
                    <p className="text-lg font-semibold text-emerald-700">{detailDeal.currency ? `${detailDeal.currency} ${detailDeal.value}` : detailDeal.value}</p>
                  </div>
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Stage</p>
                    <span className={cn(
                      'inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium',
                      STAGE_COLORS_MAP[dealStageName(detailDeal)]?.bg,
                      STAGE_COLORS_MAP[dealStageName(detailDeal)]?.accent
                    )}>
                      {dealStageName(detailDeal)}
                    </span>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  {detailContact && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Contact</p>
                      <p className="text-slate-900 text-sm">{detailContact.name}</p>
                      {detailContact.email && <p className="text-xs text-slate-500">{detailContact.email}</p>}
                    </div>
                  )}
                  {detailCompany && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Company</p>
                      <p className="text-slate-900 text-sm">{detailCompany.name}</p>
                    </div>
                  )}
                </div>
                {detailDeal.expectedCloseDateUtc && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Expected close</p>
                    <p className="text-slate-900 text-sm">
                      {new Date(detailDeal.expectedCloseDateUtc).toLocaleDateString(undefined, {
                        month: 'short',
                        day: 'numeric',
                        year: 'numeric',
                      })}
                    </p>
                    {getDaysUntilClose(detailDeal.expectedCloseDateUtc) !== null && (
                      <UrgencyBadge days={getDaysUntilClose(detailDeal.expectedCloseDateUtc)!} />
                    )}
                  </div>
                )}

                {/* Quick Actions */}
                <div className="flex gap-2 pt-2 border-t border-slate-200">
                  <Button
                    variant="outline"
                    size="sm"
                    className="flex-1 gap-2"
                    onClick={() => {
                      setEditDeal(detailDeal);
                      setDetailDeal(null);
                    }}
                  >
                    <Pencil className="w-3.5 h-3.5" />
                    Edit
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    className="text-red-600 hover:text-red-700 hover:bg-red-50"
                    onClick={() => {
                      setDeleteConfirmDeal(detailDeal);
                      setDetailDeal(null);
                    }}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </Button>
                </div>

                {/* Activities Section */}
                <div className="pt-4 border-t border-slate-200">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <ActivityIcon className="w-4 h-4 text-slate-500" />
                      <h3 className="text-sm font-semibold text-slate-700">Activity Log</h3>
                      <span className="text-xs bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded-full">
                        {dealActivities.length}
                      </span>
                    </div>
                  </div>

                  {/* Log Activity Form */}
                  <form onSubmit={handleLogActivity} className="mb-4 p-3 bg-slate-50 rounded-lg border border-slate-200">
                    <div className="flex gap-2 mb-2">
                      <Select
                        value={activityForm.type}
                        onValueChange={(v) => setActivityForm((f) => ({ ...f, type: v }))}
                      >
                        <SelectTrigger className="w-28 h-8 text-xs bg-white">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Note">Note</SelectItem>
                          <SelectItem value="Call">Call</SelectItem>
                          <SelectItem value="Email">Email</SelectItem>
                          <SelectItem value="Meeting">Meeting</SelectItem>
                          <SelectItem value="Task">Task</SelectItem>
                        </SelectContent>
                      </Select>
                      <Input
                        placeholder="Subject..."
                        value={activityForm.subject}
                        onChange={(e) => setActivityForm((f) => ({ ...f, subject: e.target.value }))}
                        className="flex-1 h-8 text-sm bg-white"
                      />
                    </div>
                    <textarea
                      placeholder="Details (optional)..."
                      value={activityForm.body}
                      onChange={(e) => setActivityForm((f) => ({ ...f, body: e.target.value }))}
                      className="w-full px-3 py-2 text-sm border border-slate-200 rounded-md bg-white resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                      rows={2}
                    />
                    <div className="flex justify-end mt-2">
                      <Button
                        type="submit"
                        size="sm"
                        disabled={savingActivity || !activityForm.subject.trim()}
                        className="h-7 text-xs bg-emerald-600 hover:bg-emerald-700 text-white"
                      >
                        {savingActivity ? (
                          <span className="flex items-center gap-1">
                            <Clock className="w-3 h-3 animate-spin" />
                            Saving...
                          </span>
                        ) : (
                          <span className="flex items-center gap-1">
                            <Plus className="w-3 h-3" />
                            Log Activity
                          </span>
                        )}
                      </Button>
                    </div>
                  </form>

                  {/* Activities Timeline */}
                  <div className="space-y-2 max-h-[300px] overflow-y-auto">
                    {activitiesLoading ? (
                      <div className="py-6 text-center">
                        <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                        <p className="text-xs text-slate-500">Loading activities...</p>
                      </div>
                    ) : dealActivities.length === 0 ? (
                      <div className="py-6 text-center">
                        <ActivityIcon className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                        <p className="text-sm text-slate-500">No activities yet</p>
                        <p className="text-xs text-slate-400 mt-1">Log your first activity above</p>
                      </div>
                    ) : (
                      dealActivities.map((activity) => (
                        <div
                          key={activity.id}
                          className="flex gap-3 p-3 bg-white rounded-lg border border-slate-100 hover:border-slate-200 transition-colors"
                        >
                          <div className={cn(
                            'w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                            activity.type === 'Call' && 'bg-blue-100 text-blue-600',
                            activity.type === 'Email' && 'bg-purple-100 text-purple-600',
                            activity.type === 'Meeting' && 'bg-amber-100 text-amber-600',
                            activity.type === 'Task' && 'bg-emerald-100 text-emerald-600',
                            activity.type === 'Note' && 'bg-slate-100 text-slate-600',
                            !['Call', 'Email', 'Meeting', 'Task', 'Note'].includes(activity.type) && 'bg-slate-100 text-slate-600'
                          )}>
                            {activity.type === 'Call' && <Zap className="w-4 h-4" />}
                            {activity.type === 'Email' && <Building2 className="w-4 h-4" />}
                            {activity.type === 'Meeting' && <User className="w-4 h-4" />}
                            {activity.type === 'Task' && <CheckCircle2 className="w-4 h-4" />}
                            {activity.type === 'Note' && <Pencil className="w-4 h-4" />}
                            {!['Call', 'Email', 'Meeting', 'Task', 'Note'].includes(activity.type) && <ActivityIcon className="w-4 h-4" />}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-start justify-between gap-2">
                              <div>
                                <p className="text-sm font-medium text-slate-800 truncate">{activity.subject || activity.type}</p>
                                <p className="text-xs text-slate-400">
                                  {activity.type} • {new Date(activity.createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                                </p>
                              </div>
                            </div>
                            {activity.body && (
                              <p className="text-xs text-slate-600 mt-1 line-clamp-2">{activity.body}</p>
                            )}
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-[520px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
          {/* Gradient Header */}
          <div className="relative overflow-hidden shrink-0">
            <div className="absolute inset-0 bg-gradient-to-br from-emerald-500 via-teal-500 to-emerald-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <Target className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Create New Deal
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Add a new sales opportunity to your pipeline
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleCreate} className="flex-1 overflow-y-auto p-6 space-y-4">
            <div className="group">
              <Label htmlFor="deal-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                  <Briefcase className="w-3.5 h-3.5" />
                </div>
                Deal Name <span className="text-emerald-500">*</span>
              </Label>
              <Input
                id="deal-name"
                value={createForm.name}
                onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                required
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="group">
                <Label htmlFor="deal-value" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-600">
                    <DollarSign className="w-3.5 h-3.5" />
                  </div>
                  Value <span className="text-emerald-500">*</span>
                </Label>
                <Input
                  id="deal-value"
                  value={createForm.value}
                  onChange={(e) => setCreateForm((f) => ({ ...f, value: e.target.value }))}
                  placeholder="e.g. $50,000"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                  required
                />
              </div>
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <span className="text-xs font-bold">$</span>
                  </div>
                  Currency
                </Label>
                <Select
                  value={createForm.currency}
                  onValueChange={(v) => setCreateForm((f) => ({ ...f, currency: v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all">
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="CHF">CHF - Swiss Franc</SelectItem>
                    <SelectItem value="EUR">EUR - Euro</SelectItem>
                    <SelectItem value="USD">USD - US Dollar</SelectItem>
                    <SelectItem value="GBP">GBP - British Pound</SelectItem>
                    <SelectItem value="CAD">CAD - Canadian Dollar</SelectItem>
                    <SelectItem value="AUD">AUD - Australian Dollar</SelectItem>
                    <SelectItem value="JPY">JPY - Japanese Yen</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            {pipelines.length > 0 && (
              <div>
                <Label>Pipeline</Label>
                <Select
                  value={createForm.pipelineId || 'none'}
                  onValueChange={(v) => {
                    const pid = v === 'none' ? '' : v;
                    const firstStage = getStageList(pipelines, pid)[0];
                    setCreateForm((f) => ({ ...f, pipelineId: pid, stageId: firstStage?.id ?? f.stageId }));
                  }}
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue placeholder="Select pipeline" />
                  </SelectTrigger>
                  <SelectContent>
                    {pipelines.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div>
              <Label>Stage</Label>
              <Select
                value={createForm.stageId || createStageList[0]?.id}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, stageId: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {createStageList.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="deal-close">Expected close date</Label>
              <div className="flex gap-2 mt-1">
                <Input
                  id="deal-close"
                  type="date"
                  value={createForm.expectedCloseDate}
                  onChange={(e) => setCreateForm((f) => ({ ...f, expectedCloseDate: e.target.value }))}
                />
                <div className="flex gap-1">
                  {[7, 14, 30].map((d) => (
                    <Button key={d} type="button" variant="outline" size="sm" onClick={() => setExpectedCloseInDays(d)}>
                      +{d}d
                    </Button>
                  ))}
                </div>
              </div>
            </div>
            <div>
              <Label>Company</Label>
              <Select
                value={createForm.companyId || 'none'}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {companies.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Contact</Label>
              <Select
                value={createForm.contactId || 'none'}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, contactId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {contacts.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {orgMembers.length > 0 && (
              <div>
                <Label>Assignee</Label>
                <Select
                  value={createForm.assigneeId || 'none'}
                  onValueChange={(v) => setCreateForm((f) => ({ ...f, assigneeId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {orgMembers.map((m) => (
                      <SelectItem key={m.userId} value={m.userId}>
                        {m.name} {m.email ? `(${m.email})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)} className="h-11 px-6">
                Cancel
              </Button>
              <Button 
                type="submit" 
                disabled={saving} 
                className="h-11 px-6 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50"
              >
                {saving ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Creating...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    Create Deal
                    <Sparkles className="w-4 h-4" />
                  </span>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editDeal} onOpenChange={(open) => !open && setEditDeal(null)}>
        <DialogContent className="sm:max-w-[520px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
          {/* Gradient Header */}
          <div className="relative overflow-hidden shrink-0">
            <div className="absolute inset-0 bg-gradient-to-br from-emerald-500 via-teal-500 to-emerald-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <Pencil className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Edit Deal
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Update deal information
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleSaveEdit} className="flex-1 overflow-y-auto p-6 space-y-4">
            <div className="group">
              <Label htmlFor="edit-deal-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                  <Briefcase className="w-3.5 h-3.5" />
                </div>
                Deal Name <span className="text-emerald-500">*</span>
              </Label>
              <Input
                id="edit-deal-name"
                value={editForm.name}
                onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                required
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="group">
                <Label htmlFor="edit-deal-value" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-600">
                    <DollarSign className="w-3.5 h-3.5" />
                  </div>
                  Value <span className="text-emerald-500">*</span>
                </Label>
                <Input
                  id="edit-deal-value"
                  value={editForm.value}
                  onChange={(e) => setEditForm((f) => ({ ...f, value: e.target.value }))}
                  placeholder="e.g. $50,000"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                  required
                />
              </div>
              <div className="group">
                <Label htmlFor="edit-deal-currency" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <span className="text-xs font-bold">$</span>
                  </div>
                  Currency
                </Label>
                <Select
                  value={editForm.currency || 'CHF'}
                  onValueChange={(v) => setEditForm((f) => ({ ...f, currency: v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all">
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="CHF">CHF - Swiss Franc</SelectItem>
                    <SelectItem value="EUR">EUR - Euro</SelectItem>
                    <SelectItem value="USD">USD - US Dollar</SelectItem>
                    <SelectItem value="GBP">GBP - British Pound</SelectItem>
                    <SelectItem value="CAD">CAD - Canadian Dollar</SelectItem>
                    <SelectItem value="AUD">AUD - Australian Dollar</SelectItem>
                    <SelectItem value="JPY">JPY - Japanese Yen</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            {pipelines.length > 0 && (
              <div>
                <Label>Pipeline</Label>
                <Select
                  value={editForm.pipelineId || 'none'}
                  onValueChange={(v) => {
                    const pid = v === 'none' ? '' : v;
                    const firstStage = getStageList(pipelines, pid)[0];
                    setEditForm((f) => ({ ...f, pipelineId: pid, stageId: firstStage?.id ?? f.stageId }));
                  }}
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {pipelines.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div>
              <Label>Stage</Label>
              <Select
                value={editForm.stageId || editStageList[0]?.id}
                onValueChange={(v) => setEditForm((f) => ({ ...f, stageId: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {editStageList.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="edit-deal-close">Expected close date</Label>
              <Input
                id="edit-deal-close"
                type="date"
                value={editForm.expectedCloseDate}
                onChange={(e) => setEditForm((f) => ({ ...f, expectedCloseDate: e.target.value }))}
                className="mt-1"
              />
            </div>
            <div>
              <Label>Company</Label>
              <Select
                value={editForm.companyId || 'none'}
                onValueChange={(v) => setEditForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {companies.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {orgMembers.length > 0 && (
              <div>
                <Label>Assignee</Label>
                <Select
                  value={editForm.assigneeId || 'none'}
                  onValueChange={(v) => setEditForm((f) => ({ ...f, assigneeId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {orgMembers.map((m) => (
                      <SelectItem key={m.userId} value={m.userId}>
                        {m.name} {m.email ? `(${m.email})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={() => setEditDeal(null)} className="h-11 px-6">
                Cancel
              </Button>
              <Button 
                type="submit" 
                disabled={savingEdit} 
                className="h-11 px-6 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50"
              >
                {savingEdit ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Saving...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    Save Changes
                    <CheckCircle2 className="w-4 h-4" />
                  </span>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deleteConfirmDeal} onOpenChange={(open) => !open && setDeleteConfirmDeal(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete deal</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600">
            Are you sure you want to delete <strong>{deleteConfirmDeal?.name}</strong>? This cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirmDeal(null)} disabled={deleting}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDeleteConfirm} disabled={deleting}>
              {deleting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
