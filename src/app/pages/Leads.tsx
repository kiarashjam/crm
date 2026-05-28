import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Search, Plus, Pencil, Trash2, Building2, User, ArrowRightCircle, Link2,
  Mail, Phone, Sparkles, Check, Tag, UserPlus, Info, CircleDot,
  Upload, RefreshCw, Users, Handshake, ArrowRight, CheckCircle2,
  SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, X, Download,
  TrendingUp, Target, Clock, Zap, BarChart3, Calendar, AlertCircle, Activity as ActivityIcon,
  MessageSquarePlus,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getLeads,
  getLeadsPaged,
  getLeadStats,
  getLeadById,
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
  getActivitiesByLead,
  createActivity,
  messages,
  type ConvertLeadRequest,
  type LeadStats,
} from '@/app/api';
import type { Lead, Company, Contact, Deal, LeadStatus, LeadSource, Pipeline, Activity } from '@/app/api/types';
import { getOrgMembers, type OrgMemberDto } from '@/app/api/organizations';
import { getCurrentUser, type AuthUser } from '@/app/lib/auth';
import { useOrg } from '@/app/contexts/OrgContext';
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
import { StatusFilterMultiSelect } from './leads/components/StatusFilterMultiSelect';
import { StatusChangePopover } from './leads/components/StatusChangePopover';
import { QuickLogPopover } from './leads/components/QuickLogPopover';
import { BulkActionsBar } from './leads/components/BulkActionsBar';
import { QuickAddLeadDialog } from './leads/components/QuickAddLeadDialog';
import { Checkbox } from '@/app/components/ui/checkbox';
import { FALLBACK_STATUSES, FALLBACK_SOURCES, EMPTY_LEAD_FORM, ACTIVITY_TYPES } from './leads/config';
import { isValidGuid } from './leads/utils';
import type { LeadForm } from './leads/types';

/** Fallback row style when activity type is unknown (matches `note` in config). */
const DEFAULT_ACTIVITY_TYPE = ACTIVITY_TYPES[3]!;

// --- Lead card display constants & formatters (module-level so they are not
//     re-allocated for every card on every render). ---
type StatusStyle = { bg: string; text: string; border: string; dot: string };
const LEAD_STATUS_COLORS: Record<string, StatusStyle> = {
  New: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', dot: 'bg-blue-500' },
  Contacted: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', dot: 'bg-amber-500' },
  Qualified: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', dot: 'bg-emerald-500' },
  Lost: { bg: 'bg-slate-100', text: 'text-slate-500', border: 'border-slate-200', dot: 'bg-slate-400' },
};
const DEFAULT_STATUS_STYLE: StatusStyle = { bg: 'bg-slate-100', text: 'text-slate-600', border: 'border-slate-200', dot: 'bg-slate-400' };

const LEAD_AVATAR_GRADIENTS: Record<string, string> = {
  New: 'from-blue-500 to-cyan-400',
  Contacted: 'from-amber-500 to-orange-400',
  Qualified: 'from-emerald-500 to-teal-400',
  Lost: 'from-slate-400 to-slate-300',
};

const LEAD_SOURCE_ICONS: Record<string, string> = {
  website: '🌐',
  referral: '🤝',
  ads: '📢',
  events: '🎯',
  manual: '✏️',
  linkedin: '💼',
  cold_call: '📞',
  email_campaign: '📧',
};

const LEAD_SOURCE_BADGE_STYLES: Record<string, string> = {
  website: 'bg-gradient-to-r from-sky-50 via-blue-50 to-indigo-50 text-blue-900 border-blue-200/70 shadow-sm shadow-blue-500/10',
  referral: 'bg-gradient-to-r from-emerald-50 to-teal-50 text-emerald-900 border-emerald-200/70 shadow-sm',
  ads: 'bg-gradient-to-r from-amber-50 to-orange-50 text-amber-900 border-amber-200/70 shadow-sm',
  events: 'bg-gradient-to-r from-violet-50 to-fuchsia-50 text-violet-900 border-violet-200/60 shadow-sm',
  manual: 'bg-gradient-to-r from-slate-100 to-slate-50 text-slate-800 border-slate-200/80 shadow-sm',
  linkedin: 'bg-gradient-to-r from-sky-50 to-cyan-50 text-sky-900 border-sky-200/70 shadow-sm',
  cold_call: 'bg-gradient-to-r from-rose-50 to-orange-50 text-rose-900 border-rose-200/60 shadow-sm',
  email_campaign: 'bg-gradient-to-r from-indigo-50 to-violet-50 text-indigo-900 border-indigo-200/70 shadow-sm',
};
const DEFAULT_SOURCE_BADGE_CLASS = 'bg-gradient-to-r from-slate-50 to-slate-100 text-slate-700 border-slate-200/70 shadow-sm';

const safeDate = (dateStr?: string): Date | null => {
  if (!dateStr) return null;
  const d = new Date(dateStr);
  return Number.isNaN(d.getTime()) ? null : d;
};
const fmtAddedAt = (dateStr?: string) =>
  safeDate(dateStr)?.toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit',
  }) ?? null;
const fmtDateShort = (dateStr?: string) =>
  safeDate(dateStr)?.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) ?? null;
const fmtDateFull = (dateStr?: string) =>
  safeDate(dateStr)?.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) ?? null;
const fmtActivityWhen = (dateStr: string) =>
  safeDate(dateStr)?.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }) ?? '';

const LEAD_ASSIGNMENTS_STORAGE_KEY = 'crm.leadAssignments.v1';
const LEAD_REFERRALS_STORAGE_KEY = 'crm.leadReferrals.v1';
const LEAD_CREATED_AT_STORAGE_KEY = 'crm.leadCreatedAt.v1';

function loadLeadAssignments(): Record<string, string> {
  try {
    const raw = localStorage.getItem(LEAD_ASSIGNMENTS_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function saveLeadAssignments(assignments: Record<string, string>) {
  try {
    localStorage.setItem(LEAD_ASSIGNMENTS_STORAGE_KEY, JSON.stringify(assignments));
  } catch {
    // ignore storage failures
  }
}

function loadLeadReferrals(): Record<string, string> {
  try {
    const raw = localStorage.getItem(LEAD_REFERRALS_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function saveLeadReferrals(referrals: Record<string, string>) {
  try {
    localStorage.setItem(LEAD_REFERRALS_STORAGE_KEY, JSON.stringify(referrals));
  } catch {
    // ignore storage failures
  }
}

function loadLeadCreatedAtMap(): Record<string, string> {
  try {
    const raw = localStorage.getItem(LEAD_CREATED_AT_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function saveLeadCreatedAtMap(createdAtByLeadId: Record<string, string>) {
  try {
    localStorage.setItem(LEAD_CREATED_AT_STORAGE_KEY, JSON.stringify(createdAtByLeadId));
  } catch {
    // ignore storage failures
  }
}

export default function Leads() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { currentOrgId } = useOrg();

  const searchFromUrl = searchParams.get('search') || '';
  const currentPage = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;

  const [leads, setLeads] = useState<Lead[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [leadStats, setLeadStats] = useState<LeadStats | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [leadStatuses, setLeadStatuses] = useState<LeadStatus[]>([]);
  const [leadSources, setLeadSources] = useState<LeadSource[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState(searchFromUrl);
  const [debouncedSearch, setDebouncedSearch] = useState(searchFromUrl);
  
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
  const [orgMembers, setOrgMembers] = useState<OrgMemberDto[]>([]);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);

  // Multi-select for bulk actions
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [bulkBusy, setBulkBusy] = useState(false);

  // Search input ref so the `/` shortcut can focus it
  const searchInputRef = useRef<HTMLInputElement | null>(null);

  // Quick-add lead modal (single-page form; full wizard remains available)
  const [quickAddOpen, setQuickAddOpen] = useState(false);

  // Filter & Sort state
  // Status filter supports multiple selections (empty array = all statuses).
  const [filterStatuses, setFilterStatuses] = useState<string[]>([]);
  const [filterSource, setFilterSource] = useState<string>('all');
  const [filterConverted, setFilterConverted] = useState<'all' | 'converted' | 'active'>('active');
  const [filterAssignment, setFilterAssignment] = useState<'all' | 'me' | 'unassigned'>('all');
  const [sortField, setSortField] = useState<'name' | 'email' | 'status' | 'createdAt'>('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [showFilters, setShowFilters] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [leadAssignments, setLeadAssignments] = useState<Record<string, string>>(() => loadLeadAssignments());
  const [leadReferrals, setLeadReferrals] = useState<Record<string, string>>(() => loadLeadReferrals());
  const [leadCreatedAtMap, setLeadCreatedAtMap] = useState<Record<string, string>>(() => loadLeadCreatedAtMap());
  const [pageActivities, setPageActivities] = useState<Map<string, Activity[]>>(new Map());

  const statusOptions = leadStatuses.length > 0 ? leadStatuses : FALLBACK_STATUSES.map((name) => ({ id: name, name, organizationId: '', displayOrder: 0 }));
  const sourceOptions = leadSources.length > 0 ? leadSources : FALLBACK_SOURCES.map((name) => ({ id: name, name, organizationId: '', displayOrder: 0 }));

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Update URL when debounced search changes (resets to page 1)
  useEffect(() => {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev);
        if (debouncedSearch) {
          params.set('search', debouncedSearch);
        } else {
          params.delete('search');
        }
        params.set('page', '1');
        return params.toString() === prev.toString() ? prev : params;
      },
      { replace: true }
    );
  }, [debouncedSearch, setSearchParams]);

  // Reset to page 1 when server-side filters or sort change
  useEffect(() => {
    setSearchParams(
      (prev) => {
        if (prev.get('page') === '1') return prev;
        const params = new URLSearchParams(prev);
        params.set('page', '1');
        return params;
      },
      { replace: true }
    );
  }, [filterStatuses, filterSource, filterConverted, filterAssignment, sortField, sortDirection, setSearchParams]);

  // Browser back/forward or opening a shared link: align committed search with the URL.
  // Compare against a ref so this effect fires only on external URL changes — depending on
  // `debouncedSearch` would make it run before `setSearchParams` had a chance to commit, and
  // the stale `searchParams` closure would reset the user's typing.
  const debouncedSearchRef = useRef(debouncedSearch);
  useEffect(() => {
    debouncedSearchRef.current = debouncedSearch;
  }, [debouncedSearch]);
  useEffect(() => {
    const fromUrl = searchParams.get('search') || '';
    if (fromUrl === debouncedSearchRef.current) return;
    setDebouncedSearch(fromUrl);
    setSearchQuery(fromUrl);
  }, [searchParams]);

  const mergeLeadsWithLocalData = useCallback(
    (rawLeads: Lead[], contactsById: Map<string, Contact>) => {
      const assignments = loadLeadAssignments();
      const referrals = loadLeadReferrals();
      const createdAtByLeadId = loadLeadCreatedAtMap();
      let createdAtChanged = false;
      setLeadAssignments(assignments);
      setLeadReferrals(referrals);

      const merged = rawLeads.map((lead) => {
        const resolvedCreatedAt = lead.createdAtUtc || createdAtByLeadId[lead.id];
        if (!resolvedCreatedAt) {
          createdAtByLeadId[lead.id] = new Date().toISOString();
          createdAtChanged = true;
        }
        return {
          ...lead,
          createdAtUtc: resolvedCreatedAt || createdAtByLeadId[lead.id],
          assignedToId: assignments[lead.id] || lead.assignedToId,
          referredByContactId: referrals[lead.id] || lead.referredByContactId,
          referredByContactName:
            (referrals[lead.id] || lead.referredByContactId
              ? contactsById.get(referrals[lead.id] || lead.referredByContactId || '')?.name
              : undefined) || lead.referredByContactName,
        };
      });

      if (createdAtChanged) saveLeadCreatedAtMap(createdAtByLeadId);
      setLeadCreatedAtMap(createdAtByLeadId);
      return merged;
    },
    [],
  );

  const applyAssignmentFilter = useCallback(
    (list: Lead[]) => {
      if (filterAssignment === 'me' && currentUser?.id) {
        return list.filter((l) => l.assignedToId === currentUser.id);
      }
      if (filterAssignment === 'unassigned') {
        return list.filter((l) => !l.assignedToId);
      }
      return list;
    },
    [filterAssignment, currentUser?.id],
  );

  const applyClientLeadFilters = useCallback(
    (list: Lead[]) => {
      let result = [...list];
      if (debouncedSearch.trim()) {
        const q = debouncedSearch.toLowerCase();
        result = result.filter(
          (l) =>
            l.name.toLowerCase().includes(q) ||
            l.email.toLowerCase().includes(q) ||
            (l.phone && l.phone.includes(q)),
        );
      }
      if (filterStatuses.length > 0) result = result.filter((l) => filterStatuses.includes(l.status));
      if (filterSource !== 'all') result = result.filter((l) => l.source === filterSource);
      if (filterConverted === 'converted') result = result.filter((l) => l.isConverted);
      else if (filterConverted === 'active') result = result.filter((l) => !l.isConverted);

      const toSortableTimestamp = (value?: string) => {
        if (!value) return Number.NEGATIVE_INFINITY;
        const ts = Date.parse(value);
        return Number.isNaN(ts) ? Number.NEGATIVE_INFINITY : ts;
      };
      result.sort((a, b) => {
        let comparison = 0;
        switch (sortField) {
          case 'name':
            comparison = a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
            break;
          case 'email':
            comparison = a.email.localeCompare(b.email, undefined, { sensitivity: 'base' });
            break;
          case 'status':
            comparison = a.status.localeCompare(b.status, undefined, { sensitivity: 'base' });
            break;
          case 'createdAt':
            comparison = toSortableTimestamp(a.createdAtUtc) - toSortableTimestamp(b.createdAtUtc);
            break;
        }
        if (comparison === 0) {
          comparison = a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
        }
        return sortDirection === 'asc' ? comparison : -comparison;
      });
      return result;
    },
    [debouncedSearch, filterStatuses, filterSource, filterConverted, sortField, sortDirection],
  );

  const loadPageActivities = useCallback(async (leadIds: string[]) => {
    if (!leadIds.length) {
      setPageActivities(new Map());
      return;
    }
    try {
      const results = await Promise.all(
        leadIds.map((id) => getActivitiesByLead(id).catch((): Activity[] => [])),
      );
      const map = new Map<string, Activity[]>();
      leadIds.forEach((id, index) => {
        const list = results[index] ?? [];
        list.sort((x, y) => Date.parse(y.createdAt) - Date.parse(x.createdAt));
        map.set(id, list);
      });
      setPageActivities(map);
    } catch {
      setPageActivities(new Map());
    }
  }, []);

  const ensureCompaniesAndContacts = useCallback(async () => {
    if (companies.length > 0 && contacts.length > 0) return;
    try {
      const [companiesData, contactsData] = await Promise.all([getCompanies(), getContacts()]);
      setCompanies(companiesData ?? []);
      setContacts(contactsData ?? []);
    } catch {
      // dialogs can still open without full lists
    }
  }, [companies.length, contacts.length]);

  const fetchLeads = useCallback(async () => {
    setLoading(true);
    try {
      const [statuses, sources, stats] = await Promise.all([
        getLeadStatuses(),
        getLeadSources(),
        getLeadStats(),
      ]);
      setLeadStatuses(statuses ?? []);
      setLeadSources(sources ?? []);
      setLeadStats(stats);

      const contactsById = new Map(contacts.map((c) => [c.id, c]));
      // The paged API takes a single status. When the user picks more than one
      // status (or filters by assignment), filter client-side instead.
      const useClientMode = filterAssignment !== 'all' || filterStatuses.length > 1;

      if (useClientMode) {
        const allLeadsRaw = await getLeads();
        let merged = mergeLeadsWithLocalData(Array.isArray(allLeadsRaw) ? allLeadsRaw : [], contactsById);
        merged = applyClientLeadFilters(merged);
        merged = applyAssignmentFilter(merged);
        const count = merged.length;
        const pages = Math.ceil(count / pageSize) || 0;
        const start = (currentPage - 1) * pageSize;
        const pageItems = merged.slice(start, start + pageSize);
        setLeads(pageItems);
        setTotalCount(count);
        setTotalPages(pages);
        await loadPageActivities(pageItems.map((l) => l.id));
        if (pages > 0 && currentPage > pages) {
          const params = new URLSearchParams(searchParams);
          params.set('page', String(pages));
          setSearchParams(params, { replace: true });
        }
      } else {
        const paged = await getLeadsPaged({
          page: currentPage,
          pageSize,
          search: debouncedSearch || undefined,
          status: filterStatuses.length === 1 ? filterStatuses[0] : undefined,
          source: filterSource !== 'all' ? filterSource : undefined,
          converted: filterConverted,
          sortBy: sortField,
          sortDir: sortDirection,
        });
        const merged = mergeLeadsWithLocalData(paged.items, contactsById);
        setLeads(merged);
        setTotalCount(paged.totalCount);
        setTotalPages(paged.totalPages);
        await loadPageActivities(merged.map((l) => l.id));

        // If URL page is past the end (e.g. after deletes), jump to last page
        const pages = paged.totalPages;
        if (pages > 0 && currentPage > pages) {
          const params = new URLSearchParams(searchParams);
          params.set('page', String(pages));
          setSearchParams(params, { replace: true });
        }
      }
    } catch {
      toast.error(messages.errors.loadFailed);
    } finally {
      setLoading(false);
    }
  }, [
    contacts,
    currentPage,
    pageSize,
    debouncedSearch,
    filterStatuses,
    filterSource,
    filterConverted,
    filterAssignment,
    sortField,
    sortDirection,
    currentUser?.id,
    searchParams,
    setSearchParams,
    mergeLeadsWithLocalData,
    applyClientLeadFilters,
    applyAssignmentFilter,
    loadPageActivities,
  ]);

  useEffect(() => {
    fetchLeads();
  }, [fetchLeads]);

  const handlePageChange = (page: number) => {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(page));
    setSearchParams(params);
  };

  const handlePageSizeChange = (newPageSize: number) => {
    const params = new URLSearchParams(searchParams);
    params.set('pageSize', String(newPageSize));
    params.set('page', '1');
    setSearchParams(params);
  };

  // Get current user once for activity logging.
  useEffect(() => {
    setCurrentUser(getCurrentUser());
  }, []);

  // Reload org members whenever the active organization changes.
  useEffect(() => {
    if (!currentOrgId) {
      setOrgMembers([]);
      return;
    }
    getOrgMembers(currentOrgId)
      .then(setOrgMembers)
      .catch((err) => console.error('Failed to load org members:', err));
  }, [currentOrgId]);

  /** Leads for the current page (search/filter/sort applied server-side, except assignment filter). */
  const filteredLeads = leads;

  // Index org members by id once per render so each card's assignee lookup is O(1).
  const orgMembersById = useMemo(
    () => new Map(orgMembers.map((m) => [m.userId, m])),
    [orgMembers],
  );

  // Count active filters
  const activeFilterCount = [
    filterStatuses.length > 0,
    filterSource !== 'all',
    filterConverted !== 'all',
    filterAssignment !== 'all',
  ].filter(Boolean).length;

  // Clear all filters
  const clearFilters = () => {
    setFilterStatuses([]);
    setFilterSource('all');
    setFilterConverted('all');
    setFilterAssignment('all');
    setSearchQuery('');
  };

  const csvEscape = (value: string | number | boolean | null | undefined) => {
    if (value === null || value === undefined) return '';
    const str = String(value);
    if (/[",\n]/.test(str)) {
      return `"${str.replace(/"/g, '""')}"`;
    }
    return str;
  };

  const formatIsoDate = (value?: string) => {
    if (!value) return '';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : date.toISOString();
  };

  const handleExportLeads = async () => {
    setExporting(true);
    try {
      await ensureCompaniesAndContacts();
      const allLeads = await getLeads();
      if (!allLeads.length) {
        toast.info('No leads to export');
        return;
      }

      const headers = [
        'Id',
        'Name',
        'Email',
        'Phone',
        'Referred By',
        'Company',
        'Source',
        'Status',
        'Lead Score',
        'Lifecycle Stage',
        'Converted',
        'Created At',
        'Last Contacted At',
        'Converted At',
        'Description',
      ];

      const rows = allLeads.map((lead) => [
        lead.id,
        lead.name,
        lead.email,
        lead.phone ?? '',
        lead.referredByContactName ?? (lead.referredByContactId ? contacts.find((c) => c.id === lead.referredByContactId)?.name : '') ?? '',
        lead.companyId ? companyName(lead) : '',
        lead.source ?? '',
        lead.status,
        lead.leadScore ?? '',
        lead.lifecycleStage ?? '',
        lead.isConverted ? 'Yes' : 'No',
        formatIsoDate(lead.createdAtUtc),
        formatIsoDate(lead.lastContactedAt),
        formatIsoDate(lead.convertedAtUtc),
        lead.description ?? '',
      ]);

      const csvContent = [
        headers.map(csvEscape).join(','),
        ...rows.map((row) => row.map(csvEscape).join(',')),
      ].join('\n');

      const csvWithBom = `\uFEFF${csvContent}`;
      const blob = new Blob([csvWithBom], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const timestamp = new Date().toISOString().slice(0, 10);
      const link = document.createElement('a');
      link.href = url;
      link.download = `leads-${timestamp}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      toast.success(`Exported ${allLeads.length} leads`);
    } catch {
      toast.error('Failed to export leads');
    } finally {
      setExporting(false);
    }
  };

  // Primary entry point opens the streamlined Quick Add modal.
  const openCreate = () => {
    void ensureCompaniesAndContacts();
    setEditingLead(null);
    setForm(EMPTY_LEAD_FORM);
    setQuickAddOpen(true);
  };

  // Open the full multi-step editor, optionally prefilled with what the user
  // already typed in Quick Add.
  const openFullEditor = (prefill?: {
    name?: string;
    email?: string;
    phone?: string;
    companyId?: string;
    source?: string;
    status?: string;
    description?: string;
  }) => {
    void ensureCompaniesAndContacts();
    setEditingLead(null);
    setForm({
      ...EMPTY_LEAD_FORM,
      name: prefill?.name ?? '',
      email: prefill?.email ?? '',
      phone: prefill?.phone ?? '',
      companyId: prefill?.companyId ?? '',
      source: prefill?.source ?? '',
      status: prefill?.status ?? '',
      description: prefill?.description ?? '',
    });
    setDialogOpen(true);
  };

  // Persist a lead from the Quick Add modal using the same handlers as the wizard.
  const handleQuickAddSubmit = async (input: {
    name: string;
    email: string;
    phone?: string;
    companyId?: string;
    source?: string;
    status?: string;
    description?: string;
  }) => {
    const created = await createLead({
      name: input.name,
      email: input.email,
      phone: input.phone,
      companyId: input.companyId && isValidGuid(input.companyId) ? input.companyId : undefined,
      source: input.source,
      status: input.status,
      description: input.description,
    });
    if (!created) {
      toast.error('Failed to create lead');
      return;
    }
    toast.success('Lead added');
    await fetchLeads();
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
    void ensureCompaniesAndContacts();
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
    void ensureCompaniesAndContacts();
    setEditingLead(lead);
    const sourceOpt = sourceOptions.find((s) => s.id === lead.leadSourceId || s.name === lead.source);
    const statusOpt = statusOptions.find((s) => s.id === lead.leadStatusId || s.name === lead.status);
    setForm({
      name: lead.name,
      email: lead.email,
      phone: lead.phone ?? '',
        referredByContactId: lead.referredByContactId ?? '',
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

  // Lead detail now lives on its own page (/leads/:id). Clicking a card
  // navigates instead of opening a modal.
  const openDetail = (lead: Lead) => {
    navigate(`/leads/${lead.id}`);
  };

  // Migrate any legacy `?leadId=` deep-links to the new page, then handle
  // hand-offs from the detail page: `?convertLeadId=<id>` opens the convert
  // dialog; `?edit=<id>` opens the edit wizard. Each cleans up its own param.
  useEffect(() => {
    const legacyLeadId = searchParams.get('leadId');
    if (legacyLeadId) {
      navigate(`/leads/${legacyLeadId}`, { replace: true });
      return;
    }
    const convertLeadId = searchParams.get('convertLeadId');
    if (convertLeadId) {
      const target = leads.find((l) => l.id === convertLeadId);
      const consumeParam = () => {
        setSearchParams(
          (prev) => {
            const p = new URLSearchParams(prev);
            p.delete('convertLeadId');
            return p;
          },
          { replace: true },
        );
      };
      if (target) {
        setConvertDialogLead(target);
        consumeParam();
      } else {
        getLeadById(convertLeadId)
          .then((lead) => {
            if (lead) setConvertDialogLead(lead);
            consumeParam();
          })
          .catch(() => consumeParam());
      }
      return;
    }
    const editLeadId = searchParams.get('edit');
    if (editLeadId) {
      const target = leads.find((l) => l.id === editLeadId);
      const consumeParam = () => {
        setSearchParams(
          (prev) => {
            const p = new URLSearchParams(prev);
            p.delete('edit');
            return p;
          },
          { replace: true },
        );
      };
      if (target) {
        openEdit(target);
        consumeParam();
      } else {
        getLeadById(editLeadId)
          .then((lead) => {
            if (lead) openEdit(lead);
            consumeParam();
          })
          .catch(() => consumeParam());
      }
    }
    // openEdit is re-created each render but only sets state; the effect is
    // idempotent (one-shot per ?edit / ?convertLeadId param), so listing it as
    // a dep would only cause needless re-runs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [leads, searchParams, navigate, setSearchParams]);

  // Keyboard shortcuts: `/` focuses search, `n` opens "New lead".
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const isTyping = !!target && (
        target.tagName === 'INPUT' ||
        target.tagName === 'TEXTAREA' ||
        target.isContentEditable
      );
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      if (e.key === '/' && !isTyping) {
        e.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
      } else if (e.key.toLowerCase() === 'n' && !isTyping && !dialogOpen && !quickAddOpen) {
        e.preventDefault();
        setQuickAddOpen(true);
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [dialogOpen, quickAddOpen]);

  // Selection helpers
  const toggleSelected = (leadId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(leadId)) next.delete(leadId);
      else next.add(leadId);
      return next;
    });
  };
  const clearSelection = () => setSelectedIds(new Set());

  // Inline status change from the card — saves a round-trip through the detail modal.
  const handleInlineStatusChange = async (lead: Lead, newStatus: string) => {
    try {
      const updated = await updateLead(lead.id, { status: newStatus });
      if (updated) {
        handleLeadUpdate({ ...lead, ...updated, status: newStatus });
        toast.success(`Status set to ${newStatus}`);
      }
    } catch (err) {
      console.error('Failed to update lead status', err);
      toast.error('Failed to update status');
    }
  };

  // Inline quick-log activity from the card.
  const handleQuickLogActivity = async (
    lead: Lead,
    payload: { type: string; body: string },
  ) => {
    await createActivity({
      type: payload.type,
      body: payload.body,
      leadId: lead.id,
    });
    try {
      const list = await getActivitiesByLead(lead.id);
      setPageActivities((prev) => {
        const next = new Map(prev);
        next.set(
          lead.id,
          [...list].sort((x, y) => Date.parse(y.createdAt) - Date.parse(x.createdAt)),
        );
        return next;
      });
    } catch {
      /* refresh failure is non-fatal */
    }
  };

  // Bulk operations
  const runBulkStatusChange = async (newStatus: string) => {
    if (selectedIds.size === 0 || bulkBusy) return;
    setBulkBusy(true);
    const ids = Array.from(selectedIds);
    try {
      const results = await Promise.allSettled(
        ids.map((id) => updateLead(id, { status: newStatus })),
      );
      let failed = 0;
      results.forEach((r, idx) => {
        if (r.status === 'fulfilled' && r.value) {
          const existing = leads.find((l) => l.id === ids[idx]);
          if (existing) handleLeadUpdate({ ...existing, ...r.value, status: newStatus });
        } else {
          failed++;
        }
      });
      if (failed === 0) {
        toast.success(`Set ${ids.length} lead${ids.length === 1 ? '' : 's'} to ${newStatus}`);
      } else {
        toast.error(`${failed} of ${ids.length} updates failed`);
      }
      clearSelection();
      // The optimistic handleLeadUpdate calls already reflect the new status.
      // Only refetch when a status filter is active, since the changed leads
      // may no longer match the current filter (avoids a needless page +
      // per-lead activity reload in the common no-filter case).
      if (filterStatuses.length > 0 || filterConverted !== 'all') {
        await fetchLeads();
      }
    } finally {
      setBulkBusy(false);
    }
  };

  const runBulkDelete = async () => {
    if (selectedIds.size === 0 || bulkBusy) return;
    if (!window.confirm(`Delete ${selectedIds.size} lead${selectedIds.size === 1 ? '' : 's'}? This cannot be undone.`)) {
      return;
    }
    setBulkBusy(true);
    const ids = Array.from(selectedIds);
    try {
      const results = await Promise.allSettled(ids.map((id) => deleteLead(id)));
      const successIds = new Set<string>();
      let failed = 0;
      results.forEach((r, idx) => {
        if (r.status === 'fulfilled' && r.value) {
          successIds.add(ids[idx]!);
        } else {
          failed++;
        }
      });
      if (failed === 0) {
        toast.success(`Deleted ${ids.length} lead${ids.length === 1 ? '' : 's'}`);
      } else {
        toast.error(`${failed} of ${ids.length} deletes failed`);
      }
      if (successIds.size > 0) {
        await fetchLeads();
      }
      clearSelection();
    } finally {
      setBulkBusy(false);
    }
  };

  const handleLeadUpdate = (updatedLead: Lead) => {
    const existingLead = leads.find((l) => l.id === updatedLead.id);
    const resolvedReferredByContactId = updatedLead.referredByContactId ?? existingLead?.referredByContactId;
    const resolvedReferredByContactName = resolvedReferredByContactId
      ? (contacts.find((c) => c.id === resolvedReferredByContactId)?.name ?? existingLead?.referredByContactName)
      : undefined;
    const mergedLead: Lead = {
      ...(existingLead ?? updatedLead),
      ...updatedLead,
      createdAtUtc: updatedLead.createdAtUtc ?? existingLead?.createdAtUtc ?? leadCreatedAtMap[updatedLead.id] ?? new Date().toISOString(),
      assignedToId: updatedLead.assignedToId ?? existingLead?.assignedToId,
      referredByContactId: resolvedReferredByContactId,
      referredByContactName: resolvedReferredByContactName,
    };

    const updatedAssignments = { ...leadAssignments };
    if (mergedLead.assignedToId) {
      updatedAssignments[mergedLead.id] = mergedLead.assignedToId;
    } else {
      delete updatedAssignments[mergedLead.id];
    }
    setLeadAssignments(updatedAssignments);
    saveLeadAssignments(updatedAssignments);
    const updatedReferrals = { ...leadReferrals };
    if (mergedLead.referredByContactId) {
      updatedReferrals[mergedLead.id] = mergedLead.referredByContactId;
    } else {
      delete updatedReferrals[mergedLead.id];
    }
    setLeadReferrals(updatedReferrals);
    saveLeadReferrals(updatedReferrals);
    const updatedCreatedAtMap = {
      ...leadCreatedAtMap,
      [mergedLead.id]: mergedLead.createdAtUtc ?? new Date().toISOString(),
    };
    setLeadCreatedAtMap(updatedCreatedAtMap);
    saveLeadCreatedAtMap(updatedCreatedAtMap);
    setLeads((prev) => {
      const hasLead = prev.some((l) => l.id === mergedLead.id);
      if (!hasLead) return [mergedLead, ...prev];
      return prev.map((l) => (l.id === mergedLead.id ? mergedLead : l));
    });
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
          const mergedUpdatedLead: Lead = {
            ...updated,
            referredByContactId: form.referredByContactId || undefined,
            referredByContactName: form.referredByContactId ? contacts.find((c) => c.id === form.referredByContactId)?.name : undefined,
          };
          handleLeadUpdate(mergedUpdatedLead);
          toast.success(messages.success.leadUpdated);
          setDialogOpen(false);
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
          await fetchLeads();
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

  const companyName = (lead: Lead) =>
    lead.companyName ?? (lead.companyId ? companies.find((c) => c.id === lead.companyId)?.name : undefined) ?? '—';

  const stats = leadStats ?? {
    total: totalCount,
    converted: 0,
    active: totalCount,
    newLeads: 0,
    contacted: 0,
    qualified: 0,
    conversionRate: 0,
    thisWeek: 0,
    hotLeads: 0,
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
                <Button onClick={handleExportLeads} disabled={exporting} variant="outline" className="gap-2 h-10 rounded-xl border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30 disabled:opacity-60">
                  <Download className={`w-4 h-4 ${exporting ? 'animate-pulse' : ''}`} />
                  <span className="hidden sm:inline">{exporting ? 'Exporting...' : 'Export CSV'}</span>
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
          {!loading && totalCount > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
              {/* Total Leads */}
              <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{stats.total}</p>
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
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{stats.active}</p>
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
                  <p className="text-3xl font-bold text-amber-600 tracking-tight">{stats.hotLeads}</p>
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
                  <p className="text-3xl font-bold text-white tracking-tight">{stats.conversionRate}%</p>
                  <p className="text-xs font-medium text-white/80 mt-1">Converted</p>
                  <div className="mt-3">
                    <div className="h-1.5 bg-white/20 rounded-full overflow-hidden">
                      <div 
                        className="h-full bg-white rounded-full transition-all duration-500"
                        style={{ width: `${stats.conversionRate}%` }}
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
                  <p className="text-3xl font-bold text-purple-600 tracking-tight">{stats.thisWeek}</p>
                  <p className="text-xs font-medium text-purple-600/70 mt-1">This Week</p>
                </div>
              </div>

              {/* Qualified Leads */}
              <div 
                className="group relative bg-white rounded-2xl border border-cyan-100 p-5 shadow-sm hover:shadow-xl hover:shadow-cyan-100 hover:border-cyan-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterStatuses(['Qualified']); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-cyan-50 to-cyan-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-cyan-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <CheckCircle2 className="w-5 h-5 text-cyan-600" />
                  </div>
                  <p className="text-3xl font-bold text-cyan-600 tracking-tight">{stats.qualified}</p>
                  <p className="text-xs font-medium text-cyan-600/70 mt-1">Qualified</p>
                </div>
              </div>
            </div>
          )}

          {/* Quick Insights Banner */}
          {!loading && totalCount > 0 && (stats.hotLeads > 0 || stats.newLeads > 3) && (
            <div className="mt-4 bg-gradient-to-r from-amber-50 to-orange-50 rounded-xl border border-amber-200 p-4">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-4 h-4 text-amber-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-amber-800">Quick Insights</p>
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-amber-700">
                    {stats.hotLeads > 0 && (
                      <span className="flex items-center gap-1">
                        <Zap className="w-3.5 h-3.5" />
                        {stats.hotLeads} hot lead{stats.hotLeads > 1 ? 's' : ''} ready for outreach
                      </span>
                    )}
                    {stats.newLeads > 3 && (
                      <span className="flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" />
                        {stats.newLeads} new leads awaiting first contact
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
                  ref={searchInputRef}
                  type="search"
                  placeholder="Search by name, email, or phone…  (press / )"
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
                  <SelectTrigger className="relative h-11 w-[200px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
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
                      Date added · newest first
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Date added · oldest first
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
                  <SelectItem value="status-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Status Z-A
                    </span>
                  </SelectItem>
                  <SelectItem value="email-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Email A-Z
                    </span>
                  </SelectItem>
                  <SelectItem value="email-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Email Z-A
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
              </div>
            </div>
          </div>

          {/* Expandable Filter Panel */}
          {showFilters && (
            <div className="mt-4 rounded-2xl border border-white/10 bg-gradient-to-b from-white/[0.09] to-white/[0.04] backdrop-blur-md p-4 sm:p-5 shadow-lg shadow-black/20 animate-in slide-in-from-top-2 duration-200">
              {/* Panel header */}
              <div className="flex items-center justify-between gap-3 mb-4">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-xl bg-gradient-to-br from-orange-500/30 to-amber-500/30 ring-1 ring-white/10 flex items-center justify-center shrink-0">
                    <SlidersHorizontal className="w-4 h-4 text-orange-200" />
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-white leading-none">Refine results</h3>
                    <p className="text-[11px] text-slate-400 mt-1.5 leading-none">
                      {activeFilterCount > 0
                        ? `${activeFilterCount} filter${activeFilterCount === 1 ? '' : 's'} active`
                        : 'No filters applied'}
                    </p>
                  </div>
                </div>
                {activeFilterCount > 0 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={clearFilters}
                    className="h-8 px-3 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 shrink-0"
                  >
                    <X className="w-3.5 h-3.5 mr-1" />
                    Clear all
                  </Button>
                )}
              </div>

              {/* Filter grid — 1 col on mobile, 2 on tablet, 4 on desktop */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
                {/* Status Filter (multi-select) */}
                <div className="min-w-0">
                  <label className="flex items-center gap-2 text-[11px] font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
                    <Tag className="w-3.5 h-3.5 text-orange-300/80" />
                    Status
                  </label>
                  <StatusFilterMultiSelect
                    options={statusOptions.map((s) => s.name)}
                    selected={filterStatuses}
                    onChange={setFilterStatuses}
                  />
                </div>

                {/* Source Filter */}
                <div className="min-w-0">
                  <label className="flex items-center gap-2 text-[11px] font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
                    <Sparkles className="w-3.5 h-3.5 text-orange-300/80" />
                    Source
                  </label>
                  <Select value={filterSource} onValueChange={setFilterSource}>
                    <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
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
                <div className="min-w-0">
                  <label className="flex items-center gap-2 text-[11px] font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
                    <RefreshCw className="w-3.5 h-3.5 text-orange-300/80" />
                    Conversion
                  </label>
                  <Select value={filterConverted} onValueChange={(v) => setFilterConverted(v as typeof filterConverted)}>
                    <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
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

                {/* Assignment Filter */}
                <div className="min-w-0">
                  <label className="flex items-center gap-2 text-[11px] font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
                    <User className="w-3.5 h-3.5 text-orange-300/80" />
                    Assignment
                  </label>
                  <Select value={filterAssignment} onValueChange={(v) => setFilterAssignment(v as typeof filterAssignment)}>
                    <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All assignments" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All assignments</SelectItem>
                      <SelectItem value="me">Assigned to me</SelectItem>
                      <SelectItem value="unassigned">Unassigned</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
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
              {filterStatuses.map((status) => (
                <span key={status} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-blue-500/20 text-blue-300 text-xs font-medium border border-blue-400/30">
                  <Tag className="w-3 h-3" />
                  {status}
                  <button
                    onClick={() => setFilterStatuses((prev) => prev.filter((s) => s !== status))}
                    className="ml-0.5 hover:text-blue-100 transition-colors"
                    aria-label={`Remove ${status} filter`}
                  >
                    <X className="w-3 h-3" />
                  </button>
                </span>
              ))}
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
              {filterAssignment !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-indigo-500/20 text-indigo-300 text-xs font-medium border border-indigo-400/30">
                  <User className="w-3 h-3" />
                  {filterAssignment === 'me' ? 'Assigned to me' : 'Unassigned'}
                  <button onClick={() => setFilterAssignment('all')} className="ml-0.5 hover:text-indigo-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-400">
                {filteredLeads.length} of {totalCount} leads
              </span>
            </div>
          )}

          {/* No results message */}
          {filteredLeads.length === 0 && !loading && totalCount > 0 && (
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
          <ContentSkeleton rows={6} />
        ) : totalCount === 0 ? (
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
          {selectedIds.size > 0 && (
            <BulkActionsBar
              count={selectedIds.size}
              statuses={statusOptions.map((s) => s.name)}
              onClear={clearSelection}
              onBulkStatusChange={runBulkStatusChange}
              onBulkDelete={runBulkDelete}
            />
          )}
          <div className="space-y-3">
            {filteredLeads.map((lead) => {
              const statusStyle = LEAD_STATUS_COLORS[lead.status] || DEFAULT_STATUS_STYLE;

              const avatarGradient = lead.isConverted
                ? 'from-emerald-600 to-teal-500'
                : (LEAD_AVATAR_GRADIENTS[lead.status] || 'from-slate-500 to-slate-400');

              const sourceIcons = LEAD_SOURCE_ICONS;
              const sourceKey = (lead.source || '').toLowerCase();
              const sourceBadgeClass = LEAD_SOURCE_BADGE_STYLES[sourceKey] ?? DEFAULT_SOURCE_BADGE_CLASS;

              // Get initials
              const initials = lead.name
                .split(' ')
                .map((n) => n[0])
                .slice(0, 2)
                .join('')
                .toUpperCase();

              // Get assignee info (orgMembers indexed once per render via useMemo below)
              const assignee = lead.assignedToId ? orgMembersById.get(lead.assignedToId) ?? null : null;

              const formatAddedAt = fmtAddedAt;
              const formatDateShort = fmtDateShort;
              const formatDateFull = fmtDateFull;
              const formatActivityWhen = fmtActivityWhen;

              const leadInteractions = pageActivities.get(lead.id) ?? [];

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
                  className="group relative rounded-2xl border border-slate-200/70 bg-white shadow-[0_1px_0_rgba(15,23,42,0.04),0_10px_40px_-20px_rgba(15,23,42,0.12)] hover:shadow-[0_24px_60px_-20px_rgba(99,102,241,0.22),0_16px_40px_-24px_rgba(15,23,42,0.16)] hover:border-indigo-200/60 hover:-translate-y-0.5 transition-all duration-300 cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 overflow-hidden ring-1 ring-white/80"
                >
                  <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-orange-400/40 to-transparent opacity-60 group-hover:via-indigo-400/50 transition-all duration-500" />
                  <div className="absolute inset-0 bg-gradient-to-br from-indigo-50/0 via-white to-violet-50/0 group-hover:from-indigo-50/40 group-hover:via-amber-50/20 group-hover:to-fuchsia-50/30 transition-all duration-500" />

                  <div className={`absolute left-0 top-0 bottom-0 w-2 rounded-r-full shadow-[3px_0_14px_-3px_rgba(0,0,0,0.12)] ${
                    lead.isConverted ? 'bg-gradient-to-b from-emerald-500 via-teal-400 to-cyan-400' :
                    lead.status === 'New' ? 'bg-gradient-to-b from-blue-600 via-indigo-500 to-cyan-400' :
                    lead.status === 'Contacted' ? 'bg-gradient-to-b from-amber-500 via-orange-500 to-rose-400' :
                    lead.status === 'Qualified' ? 'bg-gradient-to-b from-emerald-500 via-teal-400 to-cyan-300' :
                    'bg-gradient-to-b from-slate-500 to-slate-300'
                  }`} />

                  {/* Bulk-select checkbox: visible on hover, or always when any are selected. */}
                  <div
                    className={`absolute top-3 left-3 z-20 transition-opacity ${
                      selectedIds.size > 0 || selectedIds.has(lead.id)
                        ? 'opacity-100'
                        : 'opacity-0 group-hover:opacity-100 focus-within:opacity-100'
                    }`}
                    onClick={(e) => e.stopPropagation()}
                  >
                    <Checkbox
                      checked={selectedIds.has(lead.id)}
                      onCheckedChange={() => toggleSelected(lead.id)}
                      aria-label={`Select ${lead.name}`}
                      className="bg-white shadow"
                    />
                  </div>

                  <div className="relative flex gap-5 p-5 pl-7">
                    {/* Left Section: Avatar & Score */}
                    <div className="shrink-0 flex flex-col items-center gap-3 rounded-2xl border border-slate-100/90 bg-gradient-to-b from-slate-50/95 via-white to-indigo-50/30 p-3 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)]">
                      {/* Avatar */}
                      <div className="relative">
                        <div className={`w-16 h-16 rounded-2xl bg-gradient-to-br ${avatarGradient} flex items-center justify-center text-white font-bold text-xl shadow-lg shadow-black/10 ring-4 ring-white/90 group-hover:scale-[1.04] group-hover:shadow-xl group-hover:shadow-indigo-500/20 transition-all duration-300`}>
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
                          <div className={`w-14 h-14 rounded-xl flex flex-col items-center justify-center shadow-inner ${
                            lead.leadScore >= 70 ? 'bg-gradient-to-br from-amber-100 via-orange-50 to-rose-50 border-2 border-amber-300/80' :
                            lead.leadScore >= 40 ? 'bg-gradient-to-br from-blue-100 via-cyan-50 to-sky-50 border-2 border-blue-300/70' :
                            'bg-gradient-to-br from-slate-100 to-slate-50 border-2 border-slate-200'
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
                            <h3 className="font-bold text-slate-900 text-lg truncate tracking-tight group-hover:text-indigo-700 transition-colors duration-300">
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
                              <span className="truncate">{companyName(lead)}</span>
                            </p>
                          )}
                        </div>
                        
                        {/* Status Badges */}
                        <div className="flex flex-wrap items-center gap-2 shrink-0">
                          {lead.source && (
                            <span className={`inline-flex items-center gap-1.5 text-xs px-2.5 py-1.5 rounded-lg border backdrop-blur-sm font-semibold ${sourceBadgeClass}`}>
                              <span className="text-sm drop-shadow-sm">{sourceIcons[sourceKey] || '📌'}</span>
                              <span className="capitalize">{(lead.source || '').replace(/_/g, ' ')}</span>
                            </span>
                          )}
                          <StatusChangePopover
                            currentStatus={lead.status}
                            statuses={statusOptions.map((s) => s.name)}
                            disabled={lead.isConverted}
                            onChange={(s) => handleInlineStatusChange(lead, s)}
                            className={`text-xs font-bold px-2.5 py-1.5 rounded-lg border shadow-sm ring-1 ring-slate-900/[0.04] ${statusStyle.bg} ${statusStyle.text} ${statusStyle.border}`}
                            prefix={<span className={`w-2 h-2 rounded-full ${statusStyle.dot} shadow-sm animate-pulse`} />}
                          />
                          {lead.isConverted && (
                            <span className="inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-1.5 rounded-lg bg-gradient-to-r from-emerald-500 via-teal-500 to-cyan-500 text-white shadow-lg shadow-emerald-300/40 ring-1 ring-white/30">
                              <CheckCircle2 className="w-3.5 h-3.5" />
                              Converted
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Contact Info Row */}
                      <div className="flex flex-wrap items-center gap-2.5">
                        {lead.email?.trim() ? (
                          <a 
                            href={`mailto:${lead.email.trim()}`}
                            onClick={(e) => e.stopPropagation()}
                            className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-xl bg-gradient-to-r from-blue-50 to-indigo-50 hover:from-blue-100 hover:to-indigo-100 text-blue-900 border border-blue-200/60 hover:border-blue-300 shadow-sm hover:shadow-md transition-all font-medium"
                          >
                            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/80 text-blue-600 shadow-sm ring-1 ring-blue-100">
                              <Mail className="w-4 h-4" />
                            </span>
                            <span className="truncate max-w-[180px]">{lead.email.trim()}</span>
                          </a>
                        ) : null}
                        {lead.phone && (
                          <a 
                            href={`tel:${lead.phone}`}
                            onClick={(e) => e.stopPropagation()}
                            className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-xl bg-gradient-to-r from-teal-50 to-cyan-50 hover:from-teal-100 hover:to-cyan-100 text-teal-900 border border-teal-200/60 hover:border-teal-300 shadow-sm hover:shadow-md transition-all font-medium"
                          >
                            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/80 text-teal-600 shadow-sm ring-1 ring-teal-100">
                              <Phone className="w-4 h-4" />
                            </span>
                            <span>{lead.phone}</span>
                          </a>
                        )}
                        {assignee && (
                          <span className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-xl bg-gradient-to-r from-violet-50 to-purple-50 text-violet-900 border border-violet-200/60 shadow-sm font-medium">
                            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-white text-[10px] font-bold shadow-md ring-2 ring-white">
                              {assignee.name.charAt(0).toUpperCase()}
                            </div>
                            <span className="font-medium">{assignee.name}</span>
                          </span>
                        )}
                        {lead.referredByContactName && (
                          <span className="inline-flex items-center gap-2 text-sm px-3 py-1.5 rounded-xl bg-gradient-to-r from-fuchsia-50 to-pink-50 text-fuchsia-900 border border-fuchsia-200/60 shadow-sm font-medium">
                            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/80 text-fuchsia-600 shadow-sm ring-1 ring-fuchsia-100">
                              <UserPlus className="w-4 h-4" />
                            </span>
                            <span>Referred by {lead.referredByContactName}</span>
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
                        <div className="relative overflow-hidden rounded-xl border border-slate-200/60 bg-gradient-to-r from-white via-slate-50/80 to-indigo-50/40 shadow-sm">
                          <div className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-orange-400 via-amber-400 to-rose-400" aria-hidden />
                          <p className="text-sm text-slate-700 line-clamp-2 pl-4 pr-4 py-3 font-medium leading-relaxed">
                            <span className="text-orange-400/90 font-serif text-lg leading-none mr-0.5">“</span>
                            {lead.description}
                            <span className="text-orange-400/90 font-serif text-lg leading-none">”</span>
                          </p>
                        </div>
                      )}

                      {/* Interactions (activities) — stopPropagation on click so using the list does not open the card */}
                      <div
                        className="rounded-2xl border border-indigo-200/50 bg-gradient-to-br from-white via-indigo-50/50 to-violet-50/60 p-3.5 shadow-[0_8px_30px_-18px_rgba(79,70,229,0.25)] ring-1 ring-indigo-500/[0.06]"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className="flex items-center justify-between gap-2 mb-2.5">
                          <span className="inline-flex items-center gap-2">
                            <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-violet-600 text-white shadow-md shadow-indigo-500/35">
                              <ActivityIcon className="w-4 h-4" />
                            </span>
                            <span className="text-[11px] font-bold uppercase tracking-wider text-indigo-950/80">Interactions</span>
                          </span>
                          <span className="rounded-full bg-white/90 px-2.5 py-1 text-[10px] font-bold text-indigo-700 border border-indigo-200/70 tabular-nums shadow-sm">
                            {leadInteractions.length} total
                          </span>
                        </div>
                        {leadInteractions.length === 0 ? (
                          <p className="text-xs text-indigo-600/70 pl-0.5">No interactions logged yet — open the lead to add notes and calls.</p>
                        ) : (
                          <ul className="max-h-52 overflow-y-auto space-y-2 pr-1">
                            {leadInteractions.map((activity) => {
                              const typeKey = (activity.type || '').toLowerCase();
                              const typeConfig =
                                ACTIVITY_TYPES.find((t) => t.id === typeKey) ?? DEFAULT_ACTIVITY_TYPE;
                              const TypeIcon = typeConfig.icon;
                              const isSystem = typeKey === 'system';
                              const whenLabel = formatActivityWhen(activity.createdAt);
                              const iconWrapClass = isSystem
                                ? 'w-7 h-7 rounded-lg bg-slate-100 text-slate-500 shrink-0 flex items-center justify-center'
                                : typeConfig.color === 'blue'
                                  ? 'w-7 h-7 rounded-lg bg-blue-100 text-blue-600 shrink-0 flex items-center justify-center'
                                  : typeConfig.color === 'purple'
                                    ? 'w-7 h-7 rounded-lg bg-purple-100 text-purple-600 shrink-0 flex items-center justify-center'
                                    : typeConfig.color === 'green'
                                      ? 'w-7 h-7 rounded-lg bg-emerald-100 text-emerald-600 shrink-0 flex items-center justify-center'
                                      : typeConfig.color === 'slate'
                                        ? 'w-7 h-7 rounded-lg bg-slate-100 text-slate-500 shrink-0 flex items-center justify-center'
                                        : 'w-7 h-7 rounded-lg bg-amber-100 text-amber-600 shrink-0 flex items-center justify-center';
                              return (
                                <li
                                  key={activity.id}
                                  className="flex gap-2.5 rounded-lg border border-slate-200/60 bg-white/90 px-2.5 py-2 text-left"
                                >
                                  <div className={iconWrapClass}>
                                    <TypeIcon className="w-3.5 h-3.5" />
                                  </div>
                                  <div className="min-w-0 flex-1">
                                    <div className="flex flex-wrap items-baseline gap-x-1.5 gap-y-0.5">
                                      <span
                                        className={`text-xs font-medium text-slate-800 ${isSystem ? 'italic text-slate-600' : ''}`}
                                      >
                                        {activity.subject || typeConfig.label}
                                      </span>
                                      {isSystem && (
                                        <span className="text-[10px] uppercase tracking-wide text-slate-400">
                                          system
                                        </span>
                                      )}
                                    </div>
                                    {activity.body && (
                                      <p className="mt-0.5 text-[11px] text-slate-600 line-clamp-3 whitespace-pre-wrap">
                                        {activity.body}
                                      </p>
                                    )}
                                    {whenLabel ? (
                                      <p className="mt-1 text-[10px] text-slate-400">{whenLabel}</p>
                                    ) : null}
                                  </div>
                                </li>
                              );
                            })}
                          </ul>
                        )}
                      </div>

                      {/* Meta Info Row */}
                      <div className="flex flex-wrap items-center gap-2 text-xs">
                        {lead.lastContactedAt && (
                          <div className="inline-flex items-center gap-2 rounded-full border border-amber-200/70 bg-gradient-to-r from-amber-50 to-orange-50 px-3 py-1.5 text-amber-900 shadow-sm">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-white text-amber-600 shadow-sm ring-1 ring-amber-100">
                              <Clock className="w-3.5 h-3.5" />
                            </span>
                            <span className="font-medium text-amber-950/80">Last contact <span className="font-bold text-amber-950">{formatDateShort(lead.lastContactedAt)}</span></span>
                          </div>
                        )}
                        <div className="inline-flex items-center gap-2 rounded-full border border-blue-200/70 bg-gradient-to-r from-blue-50 to-sky-50 px-3 py-1.5 text-blue-950 shadow-sm">
                          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-white text-blue-600 shadow-sm ring-1 ring-blue-100">
                            <Calendar className="w-3.5 h-3.5" />
                          </span>
                          <span className="font-medium text-blue-950/80">Added <span className="font-bold text-blue-950">{formatAddedAt(lead.createdAtUtc) ?? 'Unknown'}</span></span>
                        </div>
                        {lead.isConverted && lead.convertedAtUtc && (
                          <div className="inline-flex items-center gap-2 rounded-full border border-emerald-200/70 bg-gradient-to-r from-emerald-50 to-teal-50 px-3 py-1.5 text-emerald-950 shadow-sm">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-white text-emerald-600 shadow-sm ring-1 ring-emerald-100">
                              <Check className="w-3.5 h-3.5" />
                            </span>
                            <span className="font-medium text-emerald-950/80">Converted <span className="font-bold text-emerald-950">{formatDateFull(lead.convertedAtUtc)}</span></span>
                          </div>
                        )}
                      </div>

                      <div 
                        className="flex items-center justify-end gap-2 pt-3 mt-0.5 border-t border-slate-200/70 bg-gradient-to-r from-slate-50/90 via-white to-indigo-50/40 -mx-1 px-1 pb-0.5"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {!lead.isConverted && (
                          <Button 
                            variant="outline" 
                            size="sm" 
                            onClick={() => openConvert(lead)} 
                            className="gap-1.5 bg-gradient-to-r from-emerald-500 via-teal-500 to-cyan-500 hover:from-emerald-600 hover:via-teal-600 hover:to-cyan-600 text-white border-0 shadow-lg shadow-emerald-300/40 hover:shadow-xl hover:shadow-teal-400/30 hover:scale-[1.02] transition-all font-semibold rounded-lg" 
                            aria-label={`Convert ${lead.name}`}
                          >
                            <ArrowRightCircle className="w-4 h-4" />
                            Convert to Deal
                          </Button>
                        )}
                        <QuickLogPopover
                          onSubmit={(payload) => handleQuickLogActivity(lead, payload)}
                          trigger={
                            <Button
                              variant="outline"
                              size="sm"
                              className="gap-1.5 bg-white hover:bg-emerald-50 text-slate-700 hover:text-emerald-700 border-emerald-200/60 hover:border-emerald-300 shadow-sm hover:shadow-md transition-all font-medium rounded-lg"
                              aria-label={`Log activity for ${lead.name}`}
                            >
                              <MessageSquarePlus className="w-4 h-4" />
                              Log
                            </Button>
                          }
                        />
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={(e) => { e.stopPropagation(); openEdit(lead); }}
                          className="gap-1.5 bg-white hover:bg-indigo-50 text-slate-700 hover:text-indigo-800 border-indigo-200/60 hover:border-indigo-300 shadow-sm hover:shadow-md transition-all font-medium rounded-lg"
                          aria-label={`Edit ${lead.name}`}
                        >
                          <Pencil className="w-4 h-4" />
                          Edit
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setDeleteConfirmLead(lead)}
                          className="gap-1.5 bg-white text-red-600 border-red-200/80 hover:bg-gradient-to-r hover:from-red-50 hover:to-rose-50 hover:text-red-700 hover:border-red-300 shadow-sm hover:shadow transition-all font-medium rounded-lg"
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
      </PageTransition>

      <QuickAddLeadDialog
        open={quickAddOpen}
        onOpenChange={setQuickAddOpen}
        onSubmit={handleQuickAddSubmit}
        onOpenFullEditor={openFullEditor}
        statusOptions={statusOptions}
        sourceOptions={sourceOptions}
        companies={companies}
      />

      <AddLeadDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editingLead={editingLead}
        form={form}
        setForm={setForm}
        companies={companies}
        contacts={contacts}
        sourceOptions={sourceOptions}
        statusOptions={statusOptions}
        onSubmit={handleSubmit}
        saving={saving}
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
