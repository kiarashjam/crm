import { useState, useEffect, useMemo, useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { 
  Search, Plus, Pencil, Trash2, Building2, User, ArrowRightCircle, Link2,
  Mail, Phone, Sparkles, Check, Tag, UserPlus, Info, CircleDot,
  Upload, RefreshCw, Users, Handshake, ArrowRight, CheckCircle2,
  SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, X,
  TrendingUp, Target, Clock, Zap, BarChart3, Calendar, AlertCircle
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getLeadsPaged,
  createLead,
  updateLead,
  deleteLead,
  getCompanies,
  getContacts,
  getDeals,
  getPipelines,
  getLeadStatuses,
  getLeadSources,
  convertLead,
  messages,
  type ConvertLeadRequest,
} from '@/app/api';
import type { Lead, Company, Contact, Deal, LeadStatus, LeadSource, Pipeline } from '@/app/api/types';
import { getOrgMembers, type OrgMemberDto } from '@/app/api/organizations';
import { getCurrentUser, type AuthUser } from '@/app/lib/auth';
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

// Import extracted components from leads folder
import { AddLeadDialog } from './leads/AddLeadDialog';
import { LeadDetailModal } from './leads/LeadDetailModal';
import { FALLBACK_STATUSES, FALLBACK_SOURCES, EMPTY_LEAD_FORM } from './leads/config';
import { isValidGuid } from './leads/utils';
import type { LeadForm } from './leads/types';

export default function Leads() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Pagination state from URL
  const currentPage = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;
  const searchFromUrl = searchParams.get('search') || '';
  
  const [leads, setLeads] = useState<Lead[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [leadStatuses, setLeadStatuses] = useState<LeadStatus[]>([]);
  const [leadSources, setLeadSources] = useState<LeadSource[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState(searchFromUrl);
  const [debouncedSearch, setDebouncedSearch] = useState(searchFromUrl);
  
  // Pagination state
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingLead, setEditingLead] = useState<Lead | null>(null);
  const [form, setForm] = useState<LeadForm>(EMPTY_LEAD_FORM);
  const [saving, setSaving] = useState(false);
  const [deleteConfirmLead, setDeleteConfirmLead] = useState<Lead | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [convertDialogLead, setConvertDialogLead] = useState<Lead | null>(null);
  const [convertForm, setConvertForm] = useState<ConvertLeadRequest>({
    createContact: true,
    createDeal: false,
    dealName: '',
    dealValue: '',
    dealStage: 'Qualification',
    pipelineId: undefined,
    dealStageId: undefined,
    createNewCompany: false,
    newCompanyName: '',
    existingCompanyId: undefined,
    existingContactId: undefined,
    existingDealId: undefined,
  });
  const [convertOptions, setConvertOptions] = useState<{ contacts: Contact[]; deals: Deal[]; pipelines: Pipeline[] }>({ contacts: [], deals: [], pipelines: [] });
  const [convertOptionsLoading, setConvertOptionsLoading] = useState(false);
  const [converting, setConverting] = useState(false);

  // Detail modal state
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [detailLead, setDetailLead] = useState<Lead | null>(null);
  const [orgMembers, setOrgMembers] = useState<OrgMemberDto[]>([]);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);

  // Filter & Sort state
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [filterSource, setFilterSource] = useState<string>('all');
  const [filterConverted, setFilterConverted] = useState<'all' | 'converted' | 'active'>('active');
  const [sortField, setSortField] = useState<'name' | 'email' | 'status' | 'createdAt'>('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [showFilters, setShowFilters] = useState(false);

  const statusOptions = leadStatuses.length > 0 ? leadStatuses : FALLBACK_STATUSES.map((name) => ({ id: name, name, organizationId: '', displayOrder: 0 }));
  const sourceOptions = leadSources.length > 0 ? leadSources : FALLBACK_SOURCES.map((name) => ({ id: name, name, organizationId: '', displayOrder: 0 }));

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Update URL when debounced search changes
  useEffect(() => {
    const params = new URLSearchParams(searchParams);
    if (debouncedSearch) {
      params.set('search', debouncedSearch);
      params.set('page', '1'); // Reset to first page on search
    } else {
      params.delete('search');
    }
    if (params.toString() !== searchParams.toString()) {
      setSearchParams(params);
    }
  }, [debouncedSearch]);

  // Fetch leads with pagination
  const fetchLeads = useCallback(async () => {
    setLoading(true);
    try {
      const [pagedResult, companiesData, statuses, sources] = await Promise.all([
        getLeadsPaged({ page: currentPage, pageSize, search: debouncedSearch || undefined }),
        getCompanies(),
        getLeadStatuses(),
        getLeadSources(),
      ]);
      setLeads(pagedResult.items);
      setTotalCount(pagedResult.totalCount);
      setTotalPages(pagedResult.totalPages);
      setCompanies(companiesData);
      setLeadStatuses(statuses ?? []);
      setLeadSources(sources ?? []);
    } catch {
      toast.error(messages.errors.loadFailed);
    } finally {
      setLoading(false);
    }
  }, [currentPage, pageSize, debouncedSearch]);

  useEffect(() => {
    fetchLeads();
  }, [fetchLeads]);

  // Pagination handlers
  const handlePageChange = (page: number) => {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(page));
    setSearchParams(params);
  };

  const handlePageSizeChange = (newPageSize: number) => {
    const params = new URLSearchParams(searchParams);
    params.set('pageSize', String(newPageSize));
    params.set('page', '1'); // Reset to first page
    setSearchParams(params);
  };

  // Load org members for assignment and get current user
  useEffect(() => {
    const loadOrgMembers = async () => {
      try {
        const { getCurrentOrganizationId } = await import('@/app/lib/auth');
        const orgId = getCurrentOrganizationId();
        if (orgId) {
          const members = await getOrgMembers(orgId);
          setOrgMembers(members);
        }
      } catch (err) {
        console.error('Failed to load org members:', err);
      }
    };
    loadOrgMembers();
    
    // Get current user for activity logging
    const user = getCurrentUser();
    setCurrentUser(user);
  }, []);

  const filteredLeads = useMemo(() => {
    let result = [...leads];
    
    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(
        (l) =>
          l.name.toLowerCase().includes(q) ||
          l.email.toLowerCase().includes(q) ||
          (l.phone && l.phone.includes(q))
      );
    }
    
    // Status filter
    if (filterStatus !== 'all') {
      result = result.filter((l) => l.status === filterStatus);
    }
    
    // Source filter
    if (filterSource !== 'all') {
      result = result.filter((l) => l.source === filterSource);
    }
    
    // Converted filter
    if (filterConverted === 'converted') {
      result = result.filter((l) => l.isConverted);
    } else if (filterConverted === 'active') {
      result = result.filter((l) => !l.isConverted);
    }
    
    // Sorting
    result.sort((a, b) => {
      let comparison = 0;
      switch (sortField) {
        case 'name':
          comparison = a.name.localeCompare(b.name);
          break;
        case 'email':
          comparison = a.email.localeCompare(b.email);
          break;
        case 'status':
          comparison = a.status.localeCompare(b.status);
          break;
        case 'createdAt':
          comparison = new Date(a.createdAtUtc || 0).getTime() - new Date(b.createdAtUtc || 0).getTime();
          break;
      }
      return sortDirection === 'asc' ? comparison : -comparison;
    });
    
    return result;
  }, [leads, searchQuery, filterStatus, filterSource, filterConverted, sortField, sortDirection]);

  // Count active filters
  const activeFilterCount = [
    filterStatus !== 'all',
    filterSource !== 'all',
    filterConverted !== 'all',
  ].filter(Boolean).length;

  // Clear all filters
  const clearFilters = () => {
    setFilterStatus('all');
    setFilterSource('all');
    setFilterConverted('all');
    setSearchQuery('');
  };

  const openCreate = () => {
    setEditingLead(null);
    setForm(EMPTY_LEAD_FORM);
    setDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmLead) return;
    setDeleting(true);
    try {
      const ok = await deleteLead(deleteConfirmLead.id);
      if (ok) {
        toast.success(messages.success.leadDeleted);
        setDeleteConfirmLead(null);
        fetchLeads(); // Refresh data
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const openConvert = (lead: Lead) => {
    setConvertDialogLead(lead);
    setConvertOptionsLoading(true);
    setConvertForm({
      createContact: true,
      createDeal: false,
      dealName: lead.name,
      dealValue: '',
      dealStage: 'Qualification',
      pipelineId: undefined,
      dealStageId: undefined,
      createNewCompany: false,
      newCompanyName: companies.find((c) => c.id === lead.companyId)?.name ?? '',
      existingCompanyId: undefined,
      existingContactId: undefined,
      existingDealId: undefined,
    });
    Promise.all([getContacts(), getDeals(), getPipelines()])
      .then(([contacts, deals, pipelines]) => {
        setConvertOptions({ contacts: contacts ?? [], deals: deals ?? [], pipelines: pipelines ?? [] });
      })
      .catch(() => {})
      .finally(() => setConvertOptionsLoading(false));
  };

  const convertPipeline = convertForm.pipelineId
    ? convertOptions.pipelines.find((p) => p.id === convertForm.pipelineId)
    : convertOptions.pipelines[0];
  const convertStageList = (convertPipeline?.dealStages ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder);
  const defaultConvertStageId = convertStageList[0]?.id ?? 'Qualification';

  const handleConvert = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!convertDialogLead) return;
    if (!convertForm.createContact && !convertForm.createDeal && !convertForm.existingDealId) {
      toast.error(messages.validation.selectContactOrDeal);
      return;
    }
    setConverting(true);
    try {
      const result = await convertLead(convertDialogLead.id, convertForm);
      if (result) {
        const parts: string[] = [];
        if (result.companyId) parts.push('company created');
        if (result.contactId) parts.push('contact created');
        if (result.dealId) parts.push('deal created');
        toast.success(parts.length ? `Lead converted: ${parts.join(', ')}` : messages.success.leadConverted);
        setConvertDialogLead(null);
        fetchLeads();
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setConverting(false);
    }
  };

  const openEdit = (lead: Lead) => {
    setEditingLead(lead);
    const sourceOpt = sourceOptions.find((s) => s.id === lead.leadSourceId || s.name === lead.source);
    const statusOpt = statusOptions.find((s) => s.id === lead.leadStatusId || s.name === lead.status);
    setForm({
      name: lead.name,
      email: lead.email,
      phone: lead.phone ?? '',
      companyId: lead.companyId ?? '',
      source: lead.source ?? (sourceOpt?.name ?? 'Manual'),
      status: lead.status,
      leadSourceId: lead.leadSourceId ?? (sourceOpt?.id ?? ''),
      leadStatusId: lead.leadStatusId ?? (statusOpt?.id ?? ''),
      leadScore: lead.leadScore?.toString() ?? '',
      description: lead.description ?? '',
      lifecycleStage: lead.lifecycleStage ?? '',
    });
    setDialogOpen(true);
  };

  const openDetail = (lead: Lead) => {
    setDetailLead(lead);
    setDetailModalOpen(true);
  };

  const handleLeadUpdate = (updatedLead: Lead) => {
    setLeads(prev => prev.map(l => l.id === updatedLead.id ? updatedLead : l));
    setDetailLead(updatedLead);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim() || !form.email.trim()) {
      toast.error(messages.validation.nameAndEmailRequired);
      return;
    }
    setSaving(true);
    try {
      // Only send leadSourceId/leadStatusId if they are valid GUIDs (not fallback string values)
      const validLeadSourceId = isValidGuid(form.leadSourceId) ? form.leadSourceId : undefined;
      const validLeadStatusId = isValidGuid(form.leadStatusId) ? form.leadStatusId : undefined;
      const leadScore = form.leadScore ? parseInt(form.leadScore, 10) : undefined;
      const validLeadScore = leadScore !== undefined && !isNaN(leadScore) && leadScore >= 0 && leadScore <= 100 ? leadScore : undefined;

      if (editingLead) {
        const updated = await updateLead(editingLead.id, {
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
          source: form.source || undefined,
          status: form.status,
          leadSourceId: validLeadSourceId,
          leadStatusId: validLeadStatusId,
          leadScore: validLeadScore,
          description: form.description.trim() || undefined,
          lifecycleStage: form.lifecycleStage || undefined,
        });
        if (updated) {
          toast.success(messages.success.leadUpdated);
          setDialogOpen(false);
          fetchLeads(); // Refresh data
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createLead({
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
          source: form.source || undefined,
          status: form.status,
          leadSourceId: validLeadSourceId,
          leadStatusId: validLeadStatusId,
          leadScore: validLeadScore,
          description: form.description.trim() || undefined,
          lifecycleStage: form.lifecycleStage || undefined,
        });
        if (created) {
          toast.success(messages.success.leadCreated);
          setDialogOpen(false);
          fetchLeads(); // Refresh data
        } else {
          toast.error(messages.errors.generic);
        }
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  const companyName = (id: string) => companies.find((c) => c.id === id)?.name ?? '—';

  // Calculate lead statistics
  const leadStats = useMemo(() => {
    const total = leads.length;
    const converted = leads.filter(l => l.isConverted).length;
    const active = total - converted;
    const newLeads = leads.filter(l => l.status === 'New').length;
    const contacted = leads.filter(l => l.status === 'Contacted' || l.status === 'Attempted Contact' || l.status === 'Connected').length;
    const qualified = leads.filter(l => l.status === 'Qualified').length;
    const conversionRate = total > 0 ? Math.round((converted / total) * 100) : 0;
    
    // Calculate leads added this week
    const oneWeekAgo = new Date();
    oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
    const thisWeek = leads.filter(l => {
      if (!l.createdAtUtc) return false;
      return new Date(l.createdAtUtc) >= oneWeekAgo;
    }).length;
    
    // Hot leads (high score)
    const hotLeads = leads.filter(l => (l.leadScore ?? 0) >= 70 && !l.isConverted).length;
    
    return { total, converted, active, newLeads, contacted, qualified, conversionRate, thisWeek, hotLeads };
  }, [leads]);

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
                  <Users className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Leads</h1>
                  <p className="text-slate-400 mt-1">
                    {loading ? 'Loading…' : 'Manage and qualify your sales leads'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                <Button onClick={() => navigate('/leads/webhook')} variant="outline" className="gap-2 h-10 rounded-xl border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30">
                  <Link2 className="w-4 h-4" />
                  <span className="hidden sm:inline">Webhook</span>
                </Button>
                <Button onClick={() => navigate('/leads/import')} variant="outline" className="gap-2 h-10 rounded-xl border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30">
                  <Upload className="w-4 h-4" />
                  <span className="hidden sm:inline">Import</span>
                </Button>
                <Button onClick={openCreate} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white">
                  <Plus className="w-4 h-4" />
                  Add Lead
                </Button>
              </div>
            </div>
          </div>
        </div>

          {/* Stats Cards */}
          {!loading && leads.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
              {/* Total Leads */}
              <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{leadStats.total}</p>
                  <p className="text-xs font-medium text-slate-500 mt-1">Total Leads</p>
                </div>
              </div>

              {/* Active Leads */}
              <div 
                className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterConverted('active'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Target className="w-5 h-5 text-blue-600" />
                  </div>
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{leadStats.active}</p>
                  <p className="text-xs font-medium text-blue-600/70 mt-1">Active</p>
                </div>
              </div>

              {/* Hot Leads */}
              <div className="group relative bg-white rounded-2xl border border-amber-100 p-5 shadow-sm hover:shadow-xl hover:shadow-amber-100 hover:border-amber-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-amber-50 to-amber-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-100 to-amber-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Zap className="w-5 h-5 text-amber-600" />
                  </div>
                  <p className="text-3xl font-bold text-amber-600 tracking-tight">{leadStats.hotLeads}</p>
                  <p className="text-xs font-medium text-amber-600/70 mt-1">Hot Leads</p>
                </div>
              </div>

              {/* Conversion Rate */}
              <div 
                className="group relative bg-gradient-to-br from-emerald-500 to-teal-600 rounded-2xl p-5 shadow-lg shadow-emerald-500/20 hover:shadow-xl hover:shadow-emerald-500/30 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterConverted('converted'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3">
                    <TrendingUp className="w-5 h-5 text-white" />
                  </div>
                  <p className="text-3xl font-bold text-white tracking-tight">{leadStats.conversionRate}%</p>
                  <p className="text-xs font-medium text-white/80 mt-1">Converted</p>
                  <div className="mt-3">
                    <div className="h-1.5 bg-white/20 rounded-full overflow-hidden">
                      <div 
                        className="h-full bg-white rounded-full transition-all duration-500"
                        style={{ width: `${leadStats.conversionRate}%` }}
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* New This Week */}
              <div className="group relative bg-white rounded-2xl border border-purple-100 p-5 shadow-sm hover:shadow-xl hover:shadow-purple-100 hover:border-purple-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-purple-50 to-purple-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-100 to-purple-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Calendar className="w-5 h-5 text-purple-600" />
                  </div>
                  <p className="text-3xl font-bold text-purple-600 tracking-tight">{leadStats.thisWeek}</p>
                  <p className="text-xs font-medium text-purple-600/70 mt-1">This Week</p>
                </div>
              </div>

              {/* Qualified Leads */}
              <div 
                className="group relative bg-white rounded-2xl border border-cyan-100 p-5 shadow-sm hover:shadow-xl hover:shadow-cyan-100 hover:border-cyan-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterStatus('Qualified'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-cyan-50 to-cyan-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-cyan-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <CheckCircle2 className="w-5 h-5 text-cyan-600" />
                  </div>
                  <p className="text-3xl font-bold text-cyan-600 tracking-tight">{leadStats.qualified}</p>
                  <p className="text-xs font-medium text-cyan-600/70 mt-1">Qualified</p>
                </div>
              </div>
            </div>
          )}

          {/* Quick Insights Banner */}
          {!loading && leads.length > 0 && (leadStats.hotLeads > 0 || leadStats.newLeads > 3) && (
            <div className="mt-4 bg-gradient-to-r from-amber-50 to-orange-50 rounded-xl border border-amber-200 p-4">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-4 h-4 text-amber-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-amber-800">Quick Insights</p>
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-amber-700">
                    {leadStats.hotLeads > 0 && (
                      <span className="flex items-center gap-1">
                        <Zap className="w-3.5 h-3.5" />
                        {leadStats.hotLeads} hot lead{leadStats.hotLeads > 1 ? 's' : ''} ready for outreach
                      </span>
                    )}
                    {leadStats.newLeads > 3 && (
                      <span className="flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" />
                        {leadStats.newLeads} new leads awaiting first contact
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

        {/* Search, Filter & Sort Bar - Modern Dark Theme */}
        <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-orange-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-violet-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-orange-500/20 via-amber-500/20 to-orange-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-orange-500/20 to-amber-500/20 flex items-center justify-center group-focus-within:from-orange-500/40 group-focus-within:to-amber-500/40 transition-all duration-300">
                  <Search className="w-4 h-4 text-orange-300 group-focus-within:text-orange-200 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search by name, email, or phone..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                  aria-label="Search leads"
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

            {/* Filter & Sort Controls */}
            <div className="flex gap-2">
              {/* Filter Button - Enhanced */}
              <div className="relative group/btn">
                <div className={`absolute inset-0 rounded-xl blur-lg transition-all duration-300 ${
                  showFilters || activeFilterCount > 0 
                    ? 'bg-gradient-to-r from-orange-500/40 to-amber-500/40 opacity-100' 
                    : 'bg-white/10 opacity-0 group-hover/btn:opacity-50'
                }`} />
                <Button
                  variant="outline"
                  onClick={() => setShowFilters(!showFilters)}
                  className={`relative h-11 px-4 rounded-xl border shadow-xl shadow-black/10 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0
                      ? 'bg-gradient-to-r from-orange-500 to-amber-500 border-orange-400/50 text-white hover:from-orange-400 hover:to-amber-400 hover:shadow-orange-500/25'
                      : 'bg-white/5 backdrop-blur-md border-white/10 text-white hover:bg-white/10 hover:border-white/20'
                  }`}
                >
                  <div className={`w-6 h-6 rounded-lg flex items-center justify-center mr-2 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0 ? 'bg-white/20' : 'bg-white/10'
                  }`}>
                    <SlidersHorizontal className="w-3.5 h-3.5" />
                  </div>
                  <span className="font-medium text-sm">Filters</span>
                  {activeFilterCount > 0 && (
                    <span className="ml-2 flex items-center justify-center w-5 h-5 rounded-md bg-white/25 text-white text-xs font-bold">
                      {activeFilterCount}
                    </span>
                  )}
                </Button>
              </div>

              {/* Sort Dropdown - Enhanced */}
              <div className="relative group/sort">
                <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/sort:opacity-50 transition-all duration-300" />
                <Select
                  value={`${sortField}-${sortDirection}`}
                  onValueChange={(v) => {
                    const [field, dir] = v.split('-') as [typeof sortField, typeof sortDirection];
                    setSortField(field);
                    setSortDirection(dir);
                  }}
                >
                  <SelectTrigger className="relative h-11 w-[180px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                        <ArrowUpDown className="w-3.5 h-3.5 text-slate-300" />
                      </div>
                      <SelectValue placeholder="Sort by..." />
                    </div>
                  </SelectTrigger>
                <SelectContent>
                  <SelectItem value="createdAt-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Newest first
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Oldest first
                    </span>
                  </SelectItem>
                  <SelectItem value="name-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Name A-Z
                    </span>
                  </SelectItem>
                  <SelectItem value="name-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Name Z-A
                    </span>
                  </SelectItem>
                  <SelectItem value="status-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Status A-Z
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
              </div>
            </div>
          </div>

          {/* Expandable Filter Panel */}
          {showFilters && (
            <div className="mt-4 bg-white/10 backdrop-blur-sm rounded-xl border border-white/10 p-4 animate-in slide-in-from-top-2 duration-200">
              <div className="flex flex-wrap gap-4">
                {/* Status Filter */}
                <div className="min-w-[160px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <Tag className="w-3.5 h-3.5" />
                    Status
                  </label>
                  <Select value={filterStatus} onValueChange={setFilterStatus}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All statuses" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-slate-300" />
                          All statuses
                        </span>
                      </SelectItem>
                      {statusOptions.map((s) => {
                        const colors: Record<string, string> = {
                          New: 'bg-blue-500',
                          Contacted: 'bg-amber-500',
                          Qualified: 'bg-emerald-500',
                          Lost: 'bg-slate-400',
                        };
                        return (
                          <SelectItem key={s.id} value={s.name}>
                            <span className="flex items-center gap-2">
                              <span className={`w-2 h-2 rounded-full ${colors[s.name] || 'bg-slate-400'}`} />
                              {s.name}
                            </span>
                          </SelectItem>
                        );
                      })}
                    </SelectContent>
                  </Select>
                </div>

                {/* Source Filter */}
                <div className="min-w-[160px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <Sparkles className="w-3.5 h-3.5" />
                    Source
                  </label>
                  <Select value={filterSource} onValueChange={setFilterSource}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All sources" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All sources</SelectItem>
                      {sourceOptions.map((s) => (
                        <SelectItem key={s.id} value={s.name}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Converted Filter */}
                <div className="min-w-[160px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <RefreshCw className="w-3.5 h-3.5" />
                    Conversion
                  </label>
                  <Select value={filterConverted} onValueChange={(v) => setFilterConverted(v as typeof filterConverted)}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All leads" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All leads</SelectItem>
                      <SelectItem value="active">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-blue-500" />
                          Active (not converted)
                        </span>
                      </SelectItem>
                      <SelectItem value="converted">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-emerald-500" />
                          Converted
                        </span>
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                {/* Clear Filters */}
                {activeFilterCount > 0 && (
                  <div className="flex items-end">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={clearFilters}
                      className="h-10 text-slate-300 hover:text-white hover:bg-white/10"
                    >
                      <X className="w-4 h-4 mr-1" />
                      Clear all
                    </Button>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Active Filter Pills */}
          {(activeFilterCount > 0 || searchQuery) && (
            <div className="mt-4 flex flex-wrap items-center gap-2">
              <span className="text-xs text-slate-400">Showing:</span>
              {searchQuery && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-white/10 text-white text-xs font-medium border border-white/10">
                  <Search className="w-3 h-3" />
                  &quot;{searchQuery}&quot;
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-orange-300 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterStatus !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-blue-500/20 text-blue-300 text-xs font-medium border border-blue-400/30">
                  <Tag className="w-3 h-3" />
                  {filterStatus}
                  <button onClick={() => setFilterStatus('all')} className="ml-0.5 hover:text-blue-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterSource !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-amber-500/20 text-amber-300 text-xs font-medium border border-amber-400/30">
                  <Sparkles className="w-3 h-3" />
                  {filterSource}
                  <button onClick={() => setFilterSource('all')} className="ml-0.5 hover:text-amber-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterConverted !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-emerald-500/20 text-emerald-300 text-xs font-medium border border-emerald-400/30">
                  <RefreshCw className="w-3 h-3" />
                  {filterConverted === 'converted' ? 'Converted' : 'Active'}
                  <button onClick={() => setFilterConverted('all')} className="ml-0.5 hover:text-emerald-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-400">
                {filteredLeads.length} of {leads.length} leads
              </span>
            </div>
          )}

          {/* No results message */}
          {filteredLeads.length === 0 && !loading && leads.length > 0 && (
            <div className="mt-4 flex items-center gap-3 p-4 rounded-xl bg-white/5 border border-white/10">
              <div className="w-10 h-10 rounded-full bg-white/10 flex items-center justify-center">
                <Search className="w-5 h-5 text-slate-400" />
              </div>
              <div>
                <p className="text-sm font-medium text-white">No leads match your filters</p>
                <p className="text-xs text-slate-400 mt-0.5">Try adjusting your search or filter criteria</p>
              </div>
              <Button variant="outline" size="sm" onClick={clearFilters} className="ml-auto border-white/20 text-white hover:bg-white/10">
                Clear filters
              </Button>
            </div>
          )}
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : leads.length === 0 ? (
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-orange-500 via-amber-500 to-orange-400 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <Users className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Start Building Your Pipeline</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Leads are potential customers who've shown interest. Track them here and convert them into contacts and deals.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-3 gap-4 mb-6">
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mx-auto mb-2">
                      <UserPlus className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Capture</p>
                    <p className="text-xs text-slate-500 mt-0.5">Add leads manually or via webhook</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center mx-auto mb-2">
                      <Target className="w-5 h-5 text-amber-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Qualify</p>
                    <p className="text-xs text-slate-500 mt-0.5">Score and track lead progress</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mx-auto mb-2">
                      <ArrowRightCircle className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Convert</p>
                    <p className="text-xs text-slate-500 mt-0.5">Turn leads into contacts & deals</p>
                  </div>
                </div>
                
                <div className="flex flex-col sm:flex-row gap-3 justify-center">
                  <Button onClick={openCreate} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-200/50">
                    <Plus className="w-4 h-4" />
                    Add Your First Lead
                  </Button>
                  <Button onClick={() => navigate('/leads/import')} variant="outline" className="gap-2 h-11 px-6 rounded-xl border-slate-200">
                    <Upload className="w-4 h-4" />
                    Import from CSV
                  </Button>
                </div>
              </div>
            </div>
          </div>
        ) : filteredLeads.length === 0 ? (
          null /* No results message is shown in the filter panel above */
        ) : (
          <>
          <div className="space-y-3">
            {filteredLeads.map((lead) => {
              // Status color mapping
              const statusColors: Record<string, { bg: string; text: string; border: string; dot: string }> = {
                New: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', dot: 'bg-blue-500' },
                Contacted: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', dot: 'bg-amber-500' },
                Qualified: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', dot: 'bg-emerald-500' },
                Lost: { bg: 'bg-slate-100', text: 'text-slate-500', border: 'border-slate-200', dot: 'bg-slate-400' },
              };
              const statusStyle = statusColors[lead.status] || { bg: 'bg-slate-100', text: 'text-slate-600', border: 'border-slate-200', dot: 'bg-slate-400' };

              // Avatar gradient based on status
              const avatarGradients: Record<string, string> = {
                New: 'from-blue-500 to-cyan-400',
                Contacted: 'from-amber-500 to-orange-400',
                Qualified: 'from-emerald-500 to-teal-400',
                Lost: 'from-slate-400 to-slate-300',
              };
              const avatarGradient = lead.isConverted 
                ? 'from-emerald-600 to-teal-500' 
                : (avatarGradients[lead.status] || 'from-slate-500 to-slate-400');

              // Source icons mapping
              const sourceIcons: Record<string, string> = {
                website: '🌐',
                referral: '🤝',
                ads: '📢',
                events: '🎯',
                manual: '✏️',
                linkedin: '💼',
                cold_call: '📞',
                email_campaign: '📧',
              };

              // Get initials
              const initials = lead.name
                .split(' ')
                .map((n) => n[0])
                .slice(0, 2)
                .join('')
                .toUpperCase();

              // Get assignee info
              const assignee = lead.assignedToId ? orgMembers.find(m => m.userId === lead.assignedToId) : null;
              
              // Format dates helper
              const formatDateShort = (dateStr?: string) => {
                if (!dateStr) return null;
                try {
                  const d = new Date(dateStr);
                  if (Number.isNaN(d.getTime())) return null;
                  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
                } catch { return null; }
              };

              const formatDateFull = (dateStr?: string) => {
                if (!dateStr) return null;
                try {
                  const d = new Date(dateStr);
                  if (Number.isNaN(d.getTime())) return null;
                  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
                } catch { return null; }
              };

              return (
                <div
                  key={lead.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => openDetail(lead)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      openDetail(lead);
                    }
                  }}
                  className="group relative bg-white rounded-2xl border border-slate-200/60 shadow-sm hover:shadow-xl hover:border-slate-300 hover:-translate-y-0.5 transition-all duration-300 cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 overflow-hidden"
                >
                  {/* Animated gradient background on hover */}
                  <div className="absolute inset-0 bg-gradient-to-br from-orange-50/0 via-amber-50/0 to-rose-50/0 group-hover:from-orange-50/40 group-hover:via-amber-50/30 group-hover:to-rose-50/40 transition-all duration-500" />
                  
                  {/* Left accent bar based on status */}
                  <div className={`absolute left-0 top-0 bottom-0 w-1.5 ${
                    lead.isConverted ? 'bg-gradient-to-b from-emerald-500 to-teal-400' :
                    lead.status === 'New' ? 'bg-gradient-to-b from-blue-500 to-cyan-400' :
                    lead.status === 'Contacted' ? 'bg-gradient-to-b from-amber-500 to-orange-400' :
                    lead.status === 'Qualified' ? 'bg-gradient-to-b from-emerald-500 to-teal-400' :
                    'bg-gradient-to-b from-slate-400 to-slate-300'
                  }`} />

                  <div className="relative flex gap-5 p-5 pl-6">
                    {/* Left Section: Avatar & Score */}
                    <div className="shrink-0 flex flex-col items-center gap-3">
                      {/* Avatar */}
                      <div className="relative">
                        <div className={`w-16 h-16 rounded-2xl bg-gradient-to-br ${avatarGradient} flex items-center justify-center text-white font-bold text-xl shadow-lg group-hover:scale-105 group-hover:shadow-xl transition-all duration-300`}>
                          {initials || <User className="w-7 h-7" />}
                        </div>
                        {/* Status indicator */}
                        <div className={`absolute -bottom-1 -right-1 w-6 h-6 rounded-full ${
                          lead.isConverted ? 'bg-gradient-to-br from-emerald-500 to-teal-500' : statusStyle.dot
                        } border-[3px] border-white shadow-md flex items-center justify-center`}>
                          {lead.isConverted ? (
                            <Check className="w-3.5 h-3.5 text-white" />
                          ) : (
                            <span className="w-2 h-2 rounded-full bg-white/90" />
                          )}
                        </div>
                      </div>
                      
                      {/* Lead Score Circle */}
                      {(lead.leadScore !== undefined && lead.leadScore !== null) && (
                        <div className="relative">
                          <div className={`w-14 h-14 rounded-xl flex flex-col items-center justify-center ${
                            lead.leadScore >= 70 ? 'bg-gradient-to-br from-amber-100 to-orange-100 border-2 border-amber-200' :
                            lead.leadScore >= 40 ? 'bg-gradient-to-br from-blue-100 to-cyan-100 border-2 border-blue-200' :
                            'bg-slate-100 border-2 border-slate-200'
                          }`}>
                            <span className={`text-lg font-bold ${
                              lead.leadScore >= 70 ? 'text-amber-600' :
                              lead.leadScore >= 40 ? 'text-blue-600' :
                              'text-slate-500'
                            }`}>
                              {lead.leadScore}
                            </span>
                            <span className="text-[9px] uppercase tracking-wider text-slate-400 font-medium">Score</span>
                          </div>
                          {lead.leadScore >= 70 && (
                            <div className="absolute -top-1 -right-1 w-5 h-5 rounded-full bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center shadow-md">
                              <Zap className="w-3 h-3 text-white" />
                            </div>
                          )}
                        </div>
                      )}
                    </div>

                    {/* Right Section: Main Content */}
                    <div className="min-w-0 flex-1 flex flex-col gap-3">
                      {/* Header: Name, Company & Status Badges */}
                      <div className="flex items-start justify-between gap-4">
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <h3 className="font-bold text-slate-900 text-lg truncate group-hover:text-orange-600 transition-colors">
                              {lead.name}
                            </h3>
                            {lead.lifecycleStage && (
                              <span className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${
                                lead.lifecycleStage === 'Hot' ? 'bg-gradient-to-r from-red-500 to-rose-500 text-white shadow-sm' :
                                lead.lifecycleStage === 'SQL' ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-sm' :
                                lead.lifecycleStage === 'MQL' ? 'bg-gradient-to-r from-blue-500 to-indigo-500 text-white shadow-sm' :
                                'bg-slate-200 text-slate-600'
                              }`}>
                                {lead.lifecycleStage === 'Hot' && <Zap className="w-2.5 h-2.5" />}
                                {lead.lifecycleStage}
                              </span>
                            )}
                          </div>
                          {lead.companyId && (
                            <p className="flex items-center gap-1.5 text-sm text-slate-500 mt-0.5">
                              <Building2 className="w-3.5 h-3.5 text-slate-400" />
                              <span className="truncate">{companyName(lead.companyId)}</span>
                            </p>
                          )}
                        </div>
                        
                        {/* Status Badges */}
                        <div className="flex flex-wrap items-center gap-2 shrink-0">
                          {lead.source && (
                            <span className="inline-flex items-center gap-1.5 text-xs px-2.5 py-1.5 rounded-lg bg-slate-100/80 text-slate-600 border border-slate-200/50 backdrop-blur-sm">
                              <span className="text-sm">{sourceIcons[lead.source.toLowerCase()] || '📌'}</span>
                              <span className="capitalize font-medium">{lead.source}</span>
                            </span>
                          )}
                          <span className={`inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1.5 rounded-lg border shadow-sm ${statusStyle.bg} ${statusStyle.text} ${statusStyle.border}`}>
                            <span className={`w-2 h-2 rounded-full ${statusStyle.dot} animate-pulse`} />
                            {lead.status}
                          </span>
                          {lead.isConverted && (
                            <span className="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1.5 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-md shadow-emerald-200/50">
                              <CheckCircle2 className="w-3.5 h-3.5" />
                              Converted
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Contact Info Row */}
                      <div className="flex flex-wrap items-center gap-3">
                        <a 
                          href={`mailto:${lead.email}`}
                          onClick={(e) => e.stopPropagation()}
                          className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg bg-slate-50 hover:bg-orange-50 text-slate-600 hover:text-orange-600 border border-slate-200/50 hover:border-orange-200 transition-all"
                        >
                          <Mail className="w-4 h-4" />
                          <span className="truncate max-w-[180px]">{lead.email}</span>
                        </a>
                        {lead.phone && (
                          <a 
                            href={`tel:${lead.phone}`}
                            onClick={(e) => e.stopPropagation()}
                            className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg bg-slate-50 hover:bg-orange-50 text-slate-600 hover:text-orange-600 border border-slate-200/50 hover:border-orange-200 transition-all"
                          >
                            <Phone className="w-4 h-4" />
                            <span>{lead.phone}</span>
                          </a>
                        )}
                        {assignee && (
                          <span className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg bg-indigo-50 text-indigo-600 border border-indigo-100">
                            <div className="w-5 h-5 rounded-full bg-gradient-to-br from-indigo-500 to-purple-500 flex items-center justify-center text-white text-[10px] font-bold">
                              {assignee.name.charAt(0).toUpperCase()}
                            </div>
                            <span className="font-medium">{assignee.name}</span>
                          </span>
                        )}
                      </div>

                      {/* Tags Row */}
                      {lead.tags && lead.tags.length > 0 && (
                        <div className="flex flex-wrap items-center gap-1.5">
                          {lead.tags.slice(0, 4).map((tag, idx) => (
                            <span 
                              key={idx}
                              className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded-full bg-gradient-to-r from-purple-50 to-pink-50 text-purple-700 border border-purple-100/80"
                            >
                              <Tag className="w-3 h-3" />
                              {tag}
                            </span>
                          ))}
                          {lead.tags.length > 4 && (
                            <span className="text-xs text-slate-400 font-medium">+{lead.tags.length - 4} more</span>
                          )}
                        </div>
                      )}

                      {/* Description Preview */}
                      {lead.description && (
                        <div className="relative">
                          <p className="text-sm text-slate-600 line-clamp-2 bg-gradient-to-r from-slate-50 to-slate-100/50 rounded-xl px-4 py-2.5 border border-slate-100 italic">
                            "{lead.description}"
                          </p>
                        </div>
                      )}

                      {/* Meta Info Row */}
                      <div className="flex flex-wrap items-center gap-4 text-xs">
                        {lead.lastContactedAt && (
                          <div className="flex items-center gap-1.5 text-slate-500">
                            <div className="w-6 h-6 rounded-lg bg-amber-100 flex items-center justify-center">
                              <Clock className="w-3.5 h-3.5 text-amber-600" />
                            </div>
                            <span>Last contact: <span className="font-semibold text-slate-700">{formatDateShort(lead.lastContactedAt)}</span></span>
                          </div>
                        )}
                        {lead.createdAtUtc && (
                          <div className="flex items-center gap-1.5 text-slate-500">
                            <div className="w-6 h-6 rounded-lg bg-blue-100 flex items-center justify-center">
                              <Calendar className="w-3.5 h-3.5 text-blue-600" />
                            </div>
                            <span>Created: <span className="font-semibold text-slate-700">{formatDateShort(lead.createdAtUtc)}</span></span>
                          </div>
                        )}
                        {lead.isConverted && lead.convertedAtUtc && (
                          <div className="flex items-center gap-1.5 text-emerald-600">
                            <div className="w-6 h-6 rounded-lg bg-emerald-100 flex items-center justify-center">
                              <Check className="w-3.5 h-3.5 text-emerald-600" />
                            </div>
                            <span>Converted: <span className="font-semibold">{formatDateFull(lead.convertedAtUtc)}</span></span>
                          </div>
                        )}
                      </div>

                      {/* Action Buttons - Always visible on right */}
                      <div 
                        className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100/80 opacity-0 group-hover:opacity-100 transition-all duration-300"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {!lead.isConverted && (
                          <Button 
                            variant="outline" 
                            size="sm" 
                            onClick={() => openConvert(lead)} 
                            className="gap-1.5 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 text-white border-0 shadow-lg shadow-emerald-200/50 hover:shadow-xl hover:shadow-emerald-300/50 hover:scale-105 transition-all" 
                            aria-label={`Convert ${lead.name}`}
                          >
                            <ArrowRightCircle className="w-4 h-4" />
                            Convert to Deal
                          </Button>
                        )}
                        <Button 
                          variant="outline" 
                          size="sm" 
                          onClick={() => openEdit(lead)} 
                          className="gap-1.5 bg-white hover:bg-slate-50 border-slate-200 hover:border-slate-300 shadow-sm hover:shadow transition-all" 
                          aria-label={`Edit ${lead.name}`}
                        >
                          <Pencil className="w-4 h-4" />
                          Edit
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setDeleteConfirmLead(lead)}
                          className="gap-1.5 bg-white text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700 hover:border-red-300 shadow-sm hover:shadow transition-all"
                          aria-label={`Delete ${lead.name}`}
                        >
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
          
          {/* Pagination */}
          <DataPagination
            currentPage={currentPage}
            totalPages={totalPages}
            totalCount={totalCount}
            pageSize={pageSize}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            className="mt-6"
          />
          </>
        )}
      </main>

      <AddLeadDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editingLead={editingLead}
        form={form}
        setForm={setForm}
        companies={companies}
        sourceOptions={sourceOptions}
        statusOptions={statusOptions}
        onSubmit={handleSubmit}
        saving={saving}
      />

      {/* Lead Detail Modal */}
      <LeadDetailModal
        open={detailModalOpen}
        onOpenChange={setDetailModalOpen}
        lead={detailLead}
        companies={companies}
        statusOptions={statusOptions}
        sourceOptions={sourceOptions}
        onEdit={openEdit}
        onConvert={(lead) => setConvertDialogLead(lead)}
        onDelete={(lead) => setDeleteConfirmLead(lead)}
        onUpdate={handleLeadUpdate}
        orgMembers={orgMembers}
        currentUser={currentUser}
      />

      {/* Enhanced Convert Lead Dialog */}
      <Dialog open={!!convertDialogLead} onOpenChange={(open) => !open && setConvertDialogLead(null)}>
        <DialogContent className="sm:max-w-[580px] p-0 gap-0 overflow-hidden">
          {/* Gradient Header */}
          <div className="relative overflow-hidden">
            {/* Animated background gradients */}
            <div className="absolute inset-0 bg-gradient-to-br from-emerald-600 via-emerald-500 to-teal-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            <div className="absolute -bottom-10 -left-10 w-32 h-32 bg-teal-300/30 rounded-full blur-2xl" />
            
            {/* Header content */}
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <RefreshCw className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Convert Lead
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Qualify <span className="font-semibold">{convertDialogLead?.name}</span> into a contact, company, or deal
                  </p>
                </div>
              </div>
              
              {/* Conversion flow visualization */}
              <div className="mt-5 flex items-center justify-center gap-2 bg-white/10 backdrop-blur-sm rounded-xl px-4 py-3 border border-white/20">
                <div className="flex items-center gap-1.5 text-white/90">
                  <div className="w-8 h-8 rounded-lg bg-white/20 flex items-center justify-center">
                    <User className="w-4 h-4" />
                  </div>
                  <span className="text-sm font-medium">Lead</span>
                </div>
                <ArrowRight className="w-5 h-5 text-white/60 mx-1" />
                <div className={`flex items-center gap-1.5 transition-all ${convertForm.createContact ? 'text-white' : 'text-white/40'}`}>
                  <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all ${convertForm.createContact ? 'bg-white/30' : 'bg-white/10'}`}>
                    <Users className="w-4 h-4" />
                  </div>
                  <span className="text-sm font-medium">Contact</span>
                </div>
                {convertForm.createNewCompany && (
                  <>
                    <span className="text-white/40 text-sm">+</span>
                    <div className="flex items-center gap-1.5 text-white">
                      <div className="w-8 h-8 rounded-lg bg-white/30 flex items-center justify-center">
                        <Building2 className="w-4 h-4" />
                      </div>
                      <span className="text-sm font-medium">Company</span>
                    </div>
                  </>
                )}
                {convertForm.createDeal && (
                  <>
                    <span className="text-white/40 text-sm">+</span>
                    <div className="flex items-center gap-1.5 text-white">
                      <div className="w-8 h-8 rounded-lg bg-white/30 flex items-center justify-center">
                        <Handshake className="w-4 h-4" />
                      </div>
                      <span className="text-sm font-medium">Deal</span>
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>

          {/* Form Content */}
          {convertOptionsLoading ? (
            <div className="py-12 text-center">
              <div className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-emerald-100 mb-3">
                <RefreshCw className="w-6 h-6 text-emerald-600 animate-spin" />
              </div>
              <p className="text-slate-600 text-sm">Loading conversion options…</p>
            </div>
          ) : (
            <form id="convert-lead-form" onSubmit={handleConvert} className="flex flex-col">
              {/* Scrollable content area */}
              <div className="p-6 space-y-5 max-h-[50vh] overflow-y-auto">
              {/* What is conversion? - Info card */}
              <div className="bg-gradient-to-r from-slate-50 to-emerald-50/30 rounded-xl p-4 border border-slate-100">
                <div className="flex gap-3">
                  <div className="shrink-0 w-8 h-8 rounded-lg bg-emerald-100 flex items-center justify-center">
                    <Info className="w-4 h-4 text-emerald-600" />
                  </div>
                  <div className="space-y-2 text-sm">
                    <p className="font-medium text-slate-700">What does conversion do?</p>
                    <p className="text-slate-600">
                      Conversion qualifies an <strong className="text-slate-700">unqualified lead</strong> into active records you can work with.
                    </p>
                    <ul className="space-y-1.5 text-slate-600">
                      <li className="flex items-start gap-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Contact</strong> — The qualified person you'll engage with</span>
                      </li>
                      <li className="flex items-start gap-2">
                        <CircleDot className="w-4 h-4 text-purple-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Company</strong> — Their organization, if applicable</span>
                      </li>
                      <li className="flex items-start gap-2">
                        <CircleDot className="w-4 h-4 text-amber-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Deal</strong> — A sales opportunity, if there's potential revenue</span>
                      </li>
                    </ul>
                  </div>
                </div>
              </div>

              {/* Contact Section - Primary */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-emerald-100 flex items-center justify-center">
                    <Users className="w-3.5 h-3.5 text-emerald-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Contact</span>
                  <span className="text-xs bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded-full font-medium">Typical</span>
                </div>
                
                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createContact: true, existingContactId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createContact
                      ? 'border-emerald-400 bg-emerald-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createContact ? 'bg-emerald-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <UserPlus className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createContact ? 'text-emerald-800' : 'text-slate-700'}`}>Create new contact</p>
                    <p className="text-xs text-slate-500 mt-0.5">From lead: {convertDialogLead?.email}</p>
                  </div>
                  {convertForm.createContact && <Check className="w-5 h-5 text-emerald-600 shrink-0" />}
                </button>

                {convertOptions.contacts.length > 0 && (
                  <div className="relative">
                    <div className="absolute inset-0 flex items-center" aria-hidden>
                      <div className="w-full border-t border-slate-200" />
                    </div>
                    <div className="relative flex justify-center">
                      <span className="bg-white px-2 text-xs text-slate-400">or use existing</span>
                    </div>
                  </div>
                )}

                {!convertForm.createContact && convertOptions.contacts.length > 0 && (
                  <Select
                    value={convertForm.existingContactId || 'none'}
                    onValueChange={(v) => setConvertForm((f) => ({ ...f, existingContactId: v === 'none' ? undefined : v, createContact: false }))}
                  >
                    <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200">
                      <SelectValue placeholder="Select existing contact" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">— Select a contact —</SelectItem>
                      {convertOptions.contacts.map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          <span className="flex items-center gap-2">
                            <Users className="w-3.5 h-3.5 text-slate-400" />
                            {c.name} {c.email ? `(${c.email})` : ''}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>

              {/* Company Section - Optional */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-purple-100 flex items-center justify-center">
                    <Building2 className="w-3.5 h-3.5 text-purple-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Company</span>
                  <span className="text-xs bg-slate-100 text-slate-500 px-2 py-0.5 rounded-full font-medium">Optional</span>
                </div>
                
                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createNewCompany: !f.createNewCompany, existingCompanyId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createNewCompany
                      ? 'border-purple-400 bg-purple-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createNewCompany ? 'bg-purple-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <Building2 className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createNewCompany ? 'text-purple-800' : 'text-slate-700'}`}>Create new company</p>
                    <p className="text-xs text-slate-500 mt-0.5">For B2B relationships and organization tracking</p>
                  </div>
                  {convertForm.createNewCompany && <Check className="w-5 h-5 text-purple-600 shrink-0" />}
                </button>

                {convertForm.createNewCompany && (
                  <div className="pl-4 border-l-2 border-purple-200">
                    <Label htmlFor="convert-company-name" className="text-sm text-slate-600">Company name</Label>
                    <Input
                      id="convert-company-name"
                      value={convertForm.newCompanyName}
                      onChange={(e) => setConvertForm((f) => ({ ...f, newCompanyName: e.target.value }))}
                      placeholder={convertDialogLead?.companyId ? companies.find((c) => c.id === convertDialogLead?.companyId)?.name : convertDialogLead?.name}
                      className="mt-1.5 h-10 bg-white"
                    />
                  </div>
                )}

                {!convertForm.createNewCompany && companies.length > 0 && (
                  <>
                    <div className="relative">
                      <div className="absolute inset-0 flex items-center" aria-hidden>
                        <div className="w-full border-t border-slate-200" />
                      </div>
                      <div className="relative flex justify-center">
                        <span className="bg-white px-2 text-xs text-slate-400">or link to existing</span>
                      </div>
                    </div>
                    <Select
                      value={convertForm.existingCompanyId || 'none'}
                      onValueChange={(v) => setConvertForm((f) => ({ ...f, existingCompanyId: v === 'none' ? undefined : v }))}
                    >
                      <SelectTrigger className="h-10 bg-slate-50/50 border-slate-200">
                        <SelectValue placeholder="Select existing company" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">— Use lead's company if set —</SelectItem>
                        {companies.map((c) => (
                          <SelectItem key={c.id} value={c.id}>
                            <span className="flex items-center gap-2">
                              <Building2 className="w-3.5 h-3.5 text-slate-400" />
                              {c.name}
                            </span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </>
                )}
              </div>

              {/* Deal Section - Optional */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-amber-100 flex items-center justify-center">
                    <Handshake className="w-3.5 h-3.5 text-amber-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Deal / Opportunity</span>
                  <span className="text-xs bg-slate-100 text-slate-500 px-2 py-0.5 rounded-full font-medium">Optional</span>
                </div>
                
                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createDeal: !f.createDeal, existingDealId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createDeal
                      ? 'border-amber-400 bg-amber-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createDeal ? 'bg-amber-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <Handshake className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createDeal ? 'text-amber-800' : 'text-slate-700'}`}>Create new deal</p>
                    <p className="text-xs text-slate-500 mt-0.5">Track this as a sales opportunity with potential revenue</p>
                  </div>
                  {convertForm.createDeal && <Check className="w-5 h-5 text-amber-600 shrink-0" />}
                </button>

                {convertForm.createDeal && (
                  <div className="pl-4 border-l-2 border-amber-200 space-y-3">
                    <div>
                      <Label htmlFor="convert-deal-name" className="text-sm text-slate-600">Deal name</Label>
                      <Input
                        id="convert-deal-name"
                        value={convertForm.dealName}
                        onChange={(e) => setConvertForm((f) => ({ ...f, dealName: e.target.value }))}
                        placeholder={convertDialogLead?.name}
                        className="mt-1.5 h-10 bg-white"
                      />
                    </div>
                    <div>
                      <Label htmlFor="convert-deal-value" className="text-sm text-slate-600">Deal value</Label>
                      <Input
                        id="convert-deal-value"
                        value={convertForm.dealValue}
                        onChange={(e) => setConvertForm((f) => ({ ...f, dealValue: e.target.value }))}
                        placeholder="e.g. $10,000"
                        className="mt-1.5 h-10 bg-white"
                      />
                    </div>
                    {convertOptions.pipelines.length > 0 && (
                      <div>
                        <Label className="text-sm text-slate-600">Pipeline & Stage</Label>
                        <div className="flex gap-2 mt-1.5">
                          <Select
                            value={convertForm.pipelineId || convertOptions.pipelines[0]?.id}
                            onValueChange={(v) => {
                              const p = convertOptions.pipelines.find((x) => x.id === v);
                              const stages = (p?.dealStages ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder);
                              setConvertForm((f) => ({ ...f, pipelineId: v, dealStageId: stages[0]?.id }));
                            }}
                          >
                            <SelectTrigger className="flex-1 h-10 bg-white">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {convertOptions.pipelines.map((p) => (
                                <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <Select
                            value={convertForm.dealStageId || defaultConvertStageId}
                            onValueChange={(v) => setConvertForm((f) => ({ ...f, dealStageId: v }))}
                          >
                            <SelectTrigger className="flex-1 h-10 bg-white">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {convertStageList.map((s) => (
                                <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    )}
                    {convertStageList.length === 0 && (
                      <div>
                        <Label className="text-sm text-slate-600">Stage</Label>
                        <Select
                          value={convertForm.dealStage || 'Qualification'}
                          onValueChange={(v) => setConvertForm((f) => ({ ...f, dealStage: v }))}
                        >
                          <SelectTrigger className="mt-1.5 h-10 bg-white">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'].map((s) => (
                              <SelectItem key={s} value={s}>{s}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    )}
                  </div>
                )}

                {!convertForm.createDeal && convertOptions.deals.length > 0 && (
                  <>
                    <div className="relative">
                      <div className="absolute inset-0 flex items-center" aria-hidden>
                        <div className="w-full border-t border-slate-200" />
                      </div>
                      <div className="relative flex justify-center">
                        <span className="bg-white px-2 text-xs text-slate-400">or attach to existing</span>
                      </div>
                    </div>
                    <Select
                      value={convertForm.existingDealId || 'none'}
                      onValueChange={(v) => setConvertForm((f) => ({ ...f, existingDealId: v === 'none' ? undefined : v }))}
                    >
                      <SelectTrigger className="h-10 bg-slate-50/50 border-slate-200">
                        <SelectValue placeholder="Attach to existing deal" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">— None —</SelectItem>
                        {convertOptions.deals.map((d) => (
                          <SelectItem key={d.id} value={d.id}>
                            <span className="flex items-center gap-2">
                              <Handshake className="w-3.5 h-3.5 text-slate-400" />
                              {d.name} ({d.value})
                            </span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </>
                )}
              </div>

              </div>
              {/* Action Buttons - fixed at bottom */}
              <div className="flex items-center gap-3 px-6 pb-6 pt-4 border-t border-slate-100">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setConvertDialogLead(null)}
                  className="flex-1 h-11 border-slate-200 text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  disabled={converting || (!convertForm.createContact && !convertForm.existingContactId)}
                  className="flex-[2] h-11 bg-gradient-to-r from-emerald-600 to-teal-500 hover:from-emerald-700 hover:to-teal-600 text-white font-medium shadow-lg shadow-emerald-200/50 transition-all disabled:opacity-50"
                >
                  {converting ? (
                    <span className="flex items-center gap-2">
                      <RefreshCw className="w-4 h-4 animate-spin" />
                      Converting…
                    </span>
                  ) : (
                    <span className="flex items-center gap-2">
                      <ArrowRightCircle className="w-4 h-4" />
                      Convert Lead
                    </span>
                  )}
                </Button>
              </div>
            </form>
          )}

          {/* Footer tip */}
          <div className="px-6 py-3 bg-gradient-to-r from-slate-50 to-emerald-50/30 border-t border-slate-100">
            <p className="text-xs text-slate-500 flex items-center gap-2">
              <span className="flex items-center justify-center w-4 h-4 rounded-full bg-emerald-100 text-emerald-500 text-[10px]">💡</span>
              The lead can be marked as converted. Organization ownership stays the same.
            </p>
          </div>
        </DialogContent>
      </Dialog>

      <AlertDialog open={!!deleteConfirmLead} onOpenChange={(open) => !open && setDeleteConfirmLead(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete lead?</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove &quot;{deleteConfirmLead?.name}&quot;. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDeleteConfirm} disabled={deleting} className="bg-red-600 hover:bg-red-700">
              {deleting ? 'Deleting...' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
