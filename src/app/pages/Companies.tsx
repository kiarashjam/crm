import { useState, useEffect, useMemo, useCallback } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { 
  Building2, Search, Plus, Pencil, Trash2, Globe, Briefcase, Users, X,
  SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, BarChart3, TrendingUp,
  ExternalLink, MapPin, Calendar, Link2, Factory, Sparkles, Building, UserCircle,
  AlertCircle, DollarSign, Target
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCompaniesPaged, getCompanyStats, createCompany, updateCompany, deleteCompany, getDealsPaged, messages } from '@/app/api';
import type { CompanyStatsItem } from '@/app/api/companies';
import type { Company, Deal } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
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
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';

// Common industries for filtering
const INDUSTRIES = [
  'Technology',
  'Healthcare',
  'Finance',
  'Manufacturing',
  'Retail',
  'Education',
  'Real Estate',
  'Consulting',
  'Marketing',
  'Other',
];

// Company sizes
const COMPANY_SIZES = [
  { value: '1-10', label: '1-10 employees', category: 'Startup' },
  { value: '11-50', label: '11-50 employees', category: 'Small' },
  { value: '51-200', label: '51-200 employees', category: 'Medium' },
  { value: '201-500', label: '201-500 employees', category: 'Large' },
  { value: '501-1000', label: '501-1000 employees', category: 'Enterprise' },
  { value: '1000+', label: '1000+ employees', category: 'Enterprise' },
];

export default function Companies() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Pagination state from URL
  const currentPage = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;
  const searchFromUrl = searchParams.get('search') || '';
  
  const [companies, setCompanies] = useState<Company[]>([]);
  const [companyStatsMap, setCompanyStatsMap] = useState<Map<string, CompanyStatsItem>>(new Map());
  const [deals, setDeals] = useState<Deal[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState(searchFromUrl);
  const [debouncedSearch, setDebouncedSearch] = useState(searchFromUrl);
  
  // Pagination state
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCompany, setEditingCompany] = useState<Company | null>(null);
  const [form, setForm] = useState({ name: '', domain: '', industry: '', size: '', description: '', website: '', location: '' });
  const [saving, setSaving] = useState(false);
  const [deleteConfirmCompany, setDeleteConfirmCompany] = useState<Company | null>(null);
  const [deleting, setDeleting] = useState(false);
  
  // Filter & Sort state
  const [filterIndustry, setFilterIndustry] = useState<string>('all');
  const [filterSize, setFilterSize] = useState<string>('all');
  const [sortField, setSortField] = useState<'name' | 'createdAt' | 'contacts' | 'deals'>('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [showFilters, setShowFilters] = useState(false);

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

  // Fetch companies with pagination
  const fetchCompanies = useCallback(async () => {
    setLoading(true);
    try {
      // HP-7: Use server-side stats instead of fetching ALL contacts/deals
      const [pagedResult, statsData, dealsResult] = await Promise.all([
        getCompaniesPaged({ page: currentPage, pageSize, search: debouncedSearch || undefined }),
        getCompanyStats(),
        getDealsPaged({ page: 1, pageSize: 500 }),
      ]);
      setCompanies(pagedResult.items);
      setTotalCount(pagedResult.totalCount);
      setTotalPages(pagedResult.totalPages);
      // Build lookup map from stats array
      const statsMap = new Map<string, CompanyStatsItem>();
      for (const s of statsData) {
        statsMap.set(s.companyId, s);
      }
      setCompanyStatsMap(statsMap);
      setDeals(dealsResult.items);
    } catch {
      toast.error(messages.errors.loadFailed);
    } finally {
      setLoading(false);
    }
  }, [currentPage, pageSize, debouncedSearch]);

  useEffect(() => {
    fetchCompanies();
  }, [fetchCompanies]);

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

  // Calculate company statistics (HP-7: uses server-side stats)
  const companyStats = useMemo(() => {
    // BUG #3 fix: Use totalCount from pagination (all companies) instead of companies.length (current page)
    const total = totalCount || companies.length;
    
    // Count by industry
    const byIndustry: Record<string, number> = {};
    companies.forEach(c => {
      const industry = c.industry || 'Other';
      byIndustry[industry] = (byIndustry[industry] || 0) + 1;
    });
    
    // Find top industry
    const topIndustry = Object.entries(byIndustry).sort((a, b) => b[1] - a[1])[0];
    
    // Companies with domain
    const withDomain = companies.filter(c => c.domain).length;
    
    // Companies added this week
    const oneWeekAgo = new Date();
    oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
    const thisWeek = companies.filter(c => c.createdAtUtc && new Date(c.createdAtUtc) >= oneWeekAgo).length;
    
    // HP-7: Use server-side stats — count from full stats map (all companies), not just current page
    const allStats = Array.from(companyStatsMap.values());
    const withContacts = allStats.filter(s => s.contactCount > 0).length;
    const withDeals = allStats.filter(s => s.dealCount > 0).length;
    const totalDealValue = allStats.reduce((sum, s) => sum + s.totalDealValue, 0);
    
    return { total, byIndustry, topIndustry, withDomain, thisWeek, withContacts, withDeals, totalDealValue };
  }, [companies, companyStatsMap, totalCount]);

  // HP-7: Get counts from server-side stats map
  const getContactCount = (companyId: string) => companyStatsMap.get(companyId)?.contactCount ?? 0;
  const getDealCount = (companyId: string) => companyStatsMap.get(companyId)?.dealCount ?? 0;
  const getDealValue = (companyId: string) => companyStatsMap.get(companyId)?.totalDealValue ?? 0;
  // HP-12: Get deals for a company (still from deals array for drill-down)
  const getDealsForCompany = (companyId: string) => deals.filter(d => d.companyId === companyId);

  const filteredCompanies = useMemo(() => {
    let result = [...companies];
    
    // HP-13: Removed duplicated client-side search filter.
    // Server already filters by debouncedSearch via getCompaniesPaged.
    
    // Industry filter
    if (filterIndustry !== 'all') {
      result = result.filter(c => (c.industry || 'Other') === filterIndustry);
    }
    
    // Size filter
    if (filterSize !== 'all') {
      result = result.filter(c => c.size === filterSize);
    }
    
    // Sorting
    result.sort((a, b) => {
      let comparison = 0;
      switch (sortField) {
        case 'name':
          comparison = a.name.localeCompare(b.name);
          break;
        case 'createdAt':
          comparison = new Date(a.createdAtUtc || 0).getTime() - new Date(b.createdAtUtc || 0).getTime();
          break;
        case 'contacts':
          comparison = getContactCount(a.id) - getContactCount(b.id);
          break;
        case 'deals':
          comparison = getDealCount(a.id) - getDealCount(b.id);
          break;
      }
      return sortDirection === 'asc' ? comparison : -comparison;
    });
    
    return result;
  }, [companies, searchQuery, filterIndustry, filterSize, sortField, sortDirection, companyStatsMap]);

  // Count active filters
  const activeFilterCount = [
    filterIndustry !== 'all',
    filterSize !== 'all',
  ].filter(Boolean).length;

  // Clear all filters
  const clearFilters = () => {
    setFilterIndustry('all');
    setFilterSize('all');
    setSearchQuery('');
  };

  const openCreate = () => {
    setEditingCompany(null);
    setForm({ name: '', domain: '', industry: '', size: '', description: '', website: '', location: '' });
    setDialogOpen(true);
  };

  const openEdit = (company: Company) => {
    setEditingCompany(company);
    setForm({
      name: company.name,
      domain: company.domain ?? '',
      industry: company.industry ?? '',
      size: company.size ?? '',
      description: company.description ?? '',
      website: company.website ?? '',
      location: company.location ?? '',
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const name = form.name.trim();
    if (!name) {
      toast.error(messages.validation.nameRequired);
      return;
    }
    setSaving(true);
    try {
      if (editingCompany) {
        // Send trimmed value (empty string clears the field via NormalizeNullable on backend).
        // For create, || undefined avoids sending empty strings; for update, empty string = "clear this field".
        const updated = await updateCompany(editingCompany.id, {
          name,
          domain: form.domain.trim(),
          industry: form.industry.trim(),
          size: form.size.trim(),
          description: form.description.trim(),
          website: form.website.trim(),
          location: form.location.trim(),
        });
        if (updated) {
          toast.success(messages.success.companyUpdated);
          setDialogOpen(false);
          fetchCompanies(); // Refresh data
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createCompany({
          name,
          domain: form.domain.trim() || undefined,
          industry: form.industry || undefined,
          size: form.size || undefined,
          description: form.description.trim() || undefined,
          website: form.website.trim() || undefined,
          location: form.location.trim() || undefined,
        });
        if (created) {
          toast.success(messages.success.companyCreated);
          setDialogOpen(false);
          fetchCompanies(); // Refresh data
        } else {
          toast.error(messages.errors.generic);
        }
      }
    } catch (error: unknown) {
      // HP-2: Handle duplicate company name error from backend (409 Conflict)
      const msg = error instanceof Error ? error.message : '';
      if (msg.includes('DuplicateName') || msg.includes('already exists')) {
        toast.error('A company with this name already exists.');
      } else {
        toast.error(messages.errors.generic);
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmCompany) return;
    setDeleting(true);
    try {
      const ok = await deleteCompany(deleteConfirmCompany.id);
      if (ok) {
        toast.success(messages.success.companyDeleted);
        setDeleteConfirmCompany(null);
        fetchCompanies(); // Refresh data
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  // Get company logo gradient based on name
  const getCompanyGradient = (name: string) => {
    const gradients = [
      'from-violet-500 to-purple-500',
      'from-blue-500 to-cyan-500',
      'from-emerald-500 to-teal-500',
      'from-orange-500 to-amber-500',
      'from-rose-500 to-pink-500',
      'from-indigo-500 to-blue-500',
    ];
    const index = name.charCodeAt(0) % gradients.length;
    return gradients[index];
  };

  // Get initials from company name
  const getInitials = (name: string) => {
    return name
      .split(' ')
      .map((n) => n[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  };

  // Format currency
  const formatCurrency = (value: number) => {
    if (value >= 1000000) return `$${(value / 1000000).toFixed(1)}M`;
    if (value >= 1000) return `$${(value / 1000).toFixed(0)}K`;
    return `$${value.toFixed(0)}`;
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
                  <Building2 className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Companies</h1>
                  <p className="text-slate-400 mt-1">
                    {loading ? 'Loading…' : 'Manage your business accounts'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                <Button onClick={openCreate} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white">
                  <Plus className="w-4 h-4" />
                  Add Company
                </Button>
              </div>
            </div>
          </div>
        </div>

          {/* Stats Cards */}
          {!loading && companies.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
              {/* Total Companies */}
              <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{companyStats.total}</p>
                  <p className="text-xs font-medium text-slate-500 mt-1">Total Companies</p>
                </div>
              </div>

              {/* With Contacts */}
              <div className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <UserCircle className="w-5 h-5 text-blue-600" />
                  </div>
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{companyStats.withContacts}</p>
                  <p className="text-xs font-medium text-blue-600/70 mt-1">With Contacts</p>
                </div>
              </div>

              {/* With Deals */}
              <div className="group relative bg-white rounded-2xl border border-emerald-100 p-5 shadow-sm hover:shadow-xl hover:shadow-emerald-100 hover:border-emerald-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-emerald-50 to-emerald-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-100 to-emerald-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Target className="w-5 h-5 text-emerald-600" />
                  </div>
                  <p className="text-3xl font-bold text-emerald-600 tracking-tight">{companyStats.withDeals}</p>
                  <p className="text-xs font-medium text-emerald-600/70 mt-1">With Deals</p>
                </div>
              </div>

              {/* Total Deal Value */}
              <div className="group relative bg-gradient-to-br from-amber-500 to-orange-600 rounded-2xl p-5 shadow-lg shadow-amber-500/20 hover:shadow-xl hover:shadow-amber-500/30 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3">
                    <DollarSign className="w-5 h-5 text-white" />
                  </div>
                  <p className="text-3xl font-bold text-white tracking-tight">{formatCurrency(companyStats.totalDealValue)}</p>
                  <p className="text-xs font-medium text-white/80 mt-1">Total Pipeline</p>
                </div>
              </div>

              {/* New This Week */}
              <div className="group relative bg-white rounded-2xl border border-purple-100 p-5 shadow-sm hover:shadow-xl hover:shadow-purple-100 hover:border-purple-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-purple-50 to-purple-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-100 to-purple-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Calendar className="w-5 h-5 text-purple-600" />
                  </div>
                  <p className="text-3xl font-bold text-purple-600 tracking-tight">{companyStats.thisWeek}</p>
                  <p className="text-xs font-medium text-purple-600/70 mt-1">This Week</p>
                </div>
              </div>

              {/* Top Industry */}
              <div 
                className="group relative bg-white rounded-2xl border border-cyan-100 p-5 shadow-sm hover:shadow-xl hover:shadow-cyan-100 hover:border-cyan-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { if (companyStats.topIndustry) { setFilterIndustry(companyStats.topIndustry[0]); setShowFilters(true); } }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-cyan-50 to-cyan-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-cyan-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Factory className="w-5 h-5 text-cyan-600" />
                  </div>
                  <p className="text-2xl font-bold text-cyan-600 tracking-tight truncate">{companyStats.topIndustry?.[0] || 'N/A'}</p>
                  <p className="text-xs font-medium text-cyan-600/70 mt-1">Top Industry ({companyStats.topIndustry?.[1] || 0})</p>
                </div>
              </div>
            </div>
          )}

          {/* Quick Insights Banner */}
          {!loading && companies.length > 0 && companyStats.withContacts < companyStats.total * 0.5 && (
            <div className="mt-4 bg-gradient-to-r from-violet-50 to-purple-50 rounded-xl border border-violet-200 p-4">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-violet-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-4 h-4 text-violet-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-violet-800">Opportunity to Expand</p>
                  <p className="mt-1 text-sm text-violet-700">
                    {companyStats.total - companyStats.withContacts} companies don't have any contacts yet. Add contacts to build stronger relationships.
                  </p>
                </div>
              </div>
            </div>
          )}

        {/* Search, Filter & Sort Bar - Modern Dark Theme */}
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
                  <Search className="w-4 h-4 text-violet-300 group-focus-within:text-violet-200 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search by name, domain, or industry..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                  aria-label="Search companies"
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
                    ? 'bg-gradient-to-r from-violet-500/40 to-purple-500/40 opacity-100' 
                    : 'bg-white/10 opacity-0 group-hover/btn:opacity-50'
                }`} />
                <Button
                  variant="outline"
                  onClick={() => setShowFilters(!showFilters)}
                  className={`relative h-11 px-4 rounded-xl border shadow-xl shadow-black/10 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0
                      ? 'bg-gradient-to-r from-violet-500 to-purple-500 border-violet-400/50 text-white hover:from-violet-400 hover:to-purple-400 hover:shadow-violet-500/25'
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
                  <SelectTrigger className="relative h-11 w-[180px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-400/20 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                        <ArrowUpDown className="w-3.5 h-3.5 text-slate-300" />
                      </div>
                      <SelectValue placeholder="Sort by..." />
                    </div>
                  </SelectTrigger>
                <SelectContent>
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
                  <SelectItem value="contacts-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Most Contacts
                    </span>
                  </SelectItem>
                  <SelectItem value="deals-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Most Deals
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Newest First
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
                {/* Industry Filter */}
                <div className="min-w-[180px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <Factory className="w-3.5 h-3.5" />
                    Industry
                  </label>
                  <Select value={filterIndustry} onValueChange={setFilterIndustry}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All industries" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All industries</SelectItem>
                      {INDUSTRIES.map((industry) => (
                        <SelectItem key={industry} value={industry}>
                          <span className="flex items-center gap-2">
                            <Factory className="w-3.5 h-3.5 text-slate-400" />
                            {industry}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Size Filter */}
                <div className="min-w-[180px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <Users className="w-3.5 h-3.5" />
                    Company Size
                  </label>
                  <Select value={filterSize} onValueChange={setFilterSize}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All sizes" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All sizes</SelectItem>
                      {COMPANY_SIZES.map((size) => (
                        <SelectItem key={size.value} value={size.value}>
                          {size.label}
                        </SelectItem>
                      ))}
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
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-violet-300 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterIndustry !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-cyan-500/20 text-cyan-300 text-xs font-medium border border-cyan-400/30">
                  <Factory className="w-3 h-3" />
                  {filterIndustry}
                  <button onClick={() => setFilterIndustry('all')} className="ml-0.5 hover:text-cyan-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterSize !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-purple-500/20 text-purple-300 text-xs font-medium border border-purple-400/30">
                  <Users className="w-3 h-3" />
                  {filterSize}
                  <button onClick={() => setFilterSize('all')} className="ml-0.5 hover:text-purple-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-400">
                {filteredCompanies.length} of {totalCount || companies.length} companies
              </span>
            </div>
          )}
        </div>

        {loading ? (
          <ContentSkeleton rows={6} />
        ) : companies.length === 0 ? (
          /* Enhanced Empty State */
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-violet-500 via-purple-500 to-violet-400 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <Building2 className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Build Your Account Base</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Companies are the organizations you do business with. Track accounts, link contacts, and manage deals.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-3 gap-4 mb-6">
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-violet-100 flex items-center justify-center mx-auto mb-2">
                      <Building className="w-5 h-5 text-violet-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Organize</p>
                    <p className="text-xs text-slate-500 mt-0.5">Track company details & industry</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mx-auto mb-2">
                      <Users className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Connect</p>
                    <p className="text-xs text-slate-500 mt-0.5">Link contacts & stakeholders</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mx-auto mb-2">
                      <TrendingUp className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Grow</p>
                    <p className="text-xs text-slate-500 mt-0.5">Track deals & revenue</p>
                  </div>
                </div>
                
                <div className="flex flex-col sm:flex-row gap-3 justify-center">
                  <Button onClick={openCreate} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-violet-500 to-purple-500 hover:from-violet-600 hover:to-purple-600 shadow-lg shadow-violet-200/50">
                    <Plus className="w-4 h-4" />
                    Add Your First Company
                  </Button>
                </div>
              </div>
            </div>
          </div>
        ) : filteredCompanies.length === 0 ? (
          /* No results state */
          <div className="flex items-center gap-3 p-6 rounded-xl bg-white border border-slate-200">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center">
              <Search className="w-6 h-6 text-slate-400" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-slate-700">No companies match your filters</p>
              <p className="text-xs text-slate-500 mt-0.5">Try adjusting your search or filter criteria</p>
            </div>
            <Button variant="outline" size="sm" onClick={clearFilters}>
              Clear filters
            </Button>
          </div>
        ) : (
          <>
          {/* Company Cards Grid */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {filteredCompanies.map((company) => {
              const initials = getInitials(company.name);
              const gradient = getCompanyGradient(company.name);
              const contactCount = getContactCount(company.id);
              const dealCount = getDealCount(company.id);
              const dealValue = getDealValue(company.id);
              
              return (
                <div
                  key={company.id}
                  className="group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:shadow-lg hover:border-slate-300 transition-all duration-300 overflow-hidden"
                >
                  <div className="p-5">
                    {/* Header with logo and actions */}
                    <div className="flex items-start gap-4 mb-4">
                      <div className={`w-14 h-14 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-white font-bold text-lg shadow-lg group-hover:scale-105 transition-transform duration-300`}>
                        {initials || <Building2 className="w-6 h-6" />}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <h3 
                          className="font-semibold text-slate-900 text-lg truncate group-hover:text-violet-600 transition-colors cursor-pointer"
                          onClick={() => navigate(`/companies/${company.id}`)}
                        >
                          {company.name}
                        </h3>
                        {company.industry && (
                          <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-md bg-slate-100 text-slate-600 mt-1">
                            <Factory className="w-3 h-3" />
                            {company.industry}
                          </span>
                        )}
                      </div>

                      {/* Actions dropdown */}
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity">
                            <span className="sr-only">Open menu</span>
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z" />
                            </svg>
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-48">
                          <DropdownMenuItem onClick={() => openEdit(company)}>
                            <Pencil className="w-4 h-4 mr-2" />
                            Edit Company
                          </DropdownMenuItem>
                          {company.domain && (
                            <DropdownMenuItem asChild>
                              <a href={`https://${company.domain}`} target="_blank" rel="noopener noreferrer">
                                <ExternalLink className="w-4 h-4 mr-2" />
                                Visit Website
                              </a>
                            </DropdownMenuItem>
                          )}
                          <DropdownMenuSeparator />
                          <DropdownMenuItem 
                            onClick={() => setDeleteConfirmCompany(company)}
                            className="text-red-600 focus:text-red-600"
                          >
                            <Trash2 className="w-4 h-4 mr-2" />
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>

                    {/* Company details */}
                    <div className="space-y-2 mb-4">
                      {company.domain && (
                        <a 
                          href={`https://${company.domain}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="flex items-center gap-2 text-sm text-slate-600 hover:text-violet-600 transition-colors group/link"
                        >
                          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center group-hover/link:bg-violet-100 transition-colors">
                            <Globe className="w-4 h-4 text-slate-500 group-hover/link:text-violet-600" />
                          </div>
                          <span className="truncate">{company.domain}</span>
                          <ExternalLink className="w-3 h-3 opacity-0 group-hover/link:opacity-100 transition-opacity" />
                        </a>
                      )}
                      
                      {company.size && (
                        <div className="flex items-center gap-2 text-sm text-slate-600">
                          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center">
                            <Users className="w-4 h-4 text-slate-500" />
                          </div>
                          <span>{company.size} employees</span>
                        </div>
                      )}

                      {company.location && (
                        <div className="flex items-center gap-2 text-sm text-slate-600">
                          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center">
                            <MapPin className="w-4 h-4 text-slate-500" />
                          </div>
                          <span className="truncate">{company.location}</span>
                        </div>
                      )}
                    </div>

                    {/* Stats row — HP-4: clickable drill-down to company detail */}
                    <div className="flex items-center gap-4 py-3 border-t border-slate-100">
                      <div
                        className="flex items-center gap-1.5 cursor-pointer hover:text-blue-600 transition-colors"
                        onClick={(e) => { e.stopPropagation(); navigate(`/companies/${company.id}`); }}
                        title="View contacts"
                      >
                        <UserCircle className="w-4 h-4 text-blue-500" />
                        <span className="text-sm font-medium text-slate-700">{contactCount}</span>
                        <span className="text-xs text-slate-400">contacts</span>
                      </div>
                      <div
                        className="flex items-center gap-1.5 cursor-pointer hover:text-emerald-600 transition-colors"
                        onClick={(e) => { e.stopPropagation(); navigate(`/companies/${company.id}`); }}
                        title="View deals"
                      >
                        <Target className="w-4 h-4 text-emerald-500" />
                        <span className="text-sm font-medium text-slate-700">{dealCount}</span>
                        <span className="text-xs text-slate-400">deals</span>
                      </div>
                      {dealValue > 0 && (
                        <div
                          className="flex items-center gap-1.5 ml-auto cursor-pointer hover:text-amber-700 transition-colors"
                          onClick={(e) => { e.stopPropagation(); navigate(`/companies/${company.id}`); }}
                          title="View deal value"
                        >
                          <DollarSign className="w-4 h-4 text-amber-500" />
                          <span className="text-sm font-bold text-amber-600">{formatCurrency(dealValue)}</span>
                        </div>
                      )}
                    </div>

                    {/* HP-12: Deals drill-down */}
                    {dealCount > 0 && (
                      <div className="pb-1">
                        <div className="space-y-1">
                          {getDealsForCompany(company.id).slice(0, 3).map(deal => (
                            <button
                              key={deal.id}
                              type="button"
                              onClick={() => navigate(`/deals/${deal.id}`)}
                              className="w-full flex items-center justify-between px-2 py-1.5 rounded-lg hover:bg-slate-50 transition-colors text-left group/deal"
                            >
                              <span className="text-xs font-medium text-slate-700 truncate group-hover/deal:text-emerald-600 transition-colors">{deal.name}</span>
                              <span className="text-xs text-emerald-600 font-semibold shrink-0 ml-2">{deal.currency || '$'}{deal.value}</span>
                            </button>
                          ))}
                          {dealCount > 3 && (
                            <button
                              type="button"
                              onClick={() => navigate('/deals')}
                              className="text-xs text-blue-600 hover:text-blue-700 px-2"
                            >
                              +{dealCount - 3} more deals
                            </button>
                          )}
                        </div>
                      </div>
                    )}
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

        {/* Enhanced Add/Edit Company Dialog */}
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogContent className="sm:max-w-[560px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
            {/* Gradient Header */}
            <div className="relative overflow-hidden shrink-0">
              <div className="absolute inset-0 bg-gradient-to-br from-violet-500 via-purple-500 to-violet-400" />
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
              <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
              
              <div className="relative px-6 py-5">
                <div className="flex items-center gap-4">
                  <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                    {editingCompany ? <Pencil className="w-6 h-6 text-white" /> : <Building2 className="w-6 h-6 text-white" />}
                  </div>
                  <div>
                    <DialogTitle className="text-xl font-bold text-white tracking-tight">
                      {editingCompany ? 'Edit Company' : 'Add New Company'}
                    </DialogTitle>
                    <p className="text-white/80 text-sm mt-0.5">
                      {editingCompany ? 'Update company information' : 'Add a new business account'}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-4">
              {/* Name Field */}
              <div className="group">
                <Label htmlFor="company-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-violet-100 text-violet-600">
                    <Building2 className="w-3.5 h-3.5" />
                  </div>
                  Company Name <span className="text-violet-500">*</span>
                </Label>
                <Input
                  id="company-name"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  placeholder="Enter company name"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100 transition-all"
                  required
                />
              </div>

              {/* Domain & Website Row */}
              <div className="grid grid-cols-2 gap-4">
                <div className="group">
                  <Label htmlFor="company-domain" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-100 text-blue-600">
                      <Globe className="w-3.5 h-3.5" />
                    </div>
                    Domain
                  </Label>
                  <Input
                    id="company-domain"
                    value={form.domain}
                    onChange={(e) => setForm((f) => ({ ...f, domain: e.target.value }))}
                    placeholder="e.g. acme.com"
                    className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100 transition-all"
                  />
                </div>

                <div className="group">
                  <Label htmlFor="company-website" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-cyan-100 text-cyan-600">
                      <Link2 className="w-3.5 h-3.5" />
                    </div>
                    Website
                  </Label>
                  <Input
                    id="company-website"
                    value={form.website}
                    onChange={(e) => setForm((f) => ({ ...f, website: e.target.value }))}
                    placeholder="https://..."
                    className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100 transition-all"
                  />
                </div>
              </div>

              {/* Industry & Size Row */}
              <div className="grid grid-cols-2 gap-4">
                <div className="group">
                  <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                      <Factory className="w-3.5 h-3.5" />
                    </div>
                    Industry
                  </Label>
                  <Select
                    value={form.industry || 'none'}
                    onValueChange={(v) => setForm((f) => ({ ...f, industry: v === 'none' ? '' : v }))}
                  >
                    <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100">
                      <SelectValue placeholder="Select industry" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">
                        <span className="text-slate-400">— Select —</span>
                      </SelectItem>
                      {INDUSTRIES.map((industry) => (
                        <SelectItem key={industry} value={industry}>
                          {industry}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="group">
                  <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-purple-100 text-purple-600">
                      <Users className="w-3.5 h-3.5" />
                    </div>
                    Company Size
                  </Label>
                  <Select
                    value={form.size || 'none'}
                    onValueChange={(v) => setForm((f) => ({ ...f, size: v === 'none' ? '' : v }))}
                  >
                    <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100">
                      <SelectValue placeholder="Select size" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">
                        <span className="text-slate-400">— Select —</span>
                      </SelectItem>
                      {COMPANY_SIZES.map((size) => (
                        <SelectItem key={size.value} value={size.value}>
                          {size.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {/* Location */}
              <div className="group">
                <Label htmlFor="company-location" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-600">
                    <MapPin className="w-3.5 h-3.5" />
                  </div>
                  Location
                </Label>
                <Input
                  id="company-location"
                  value={form.location}
                  onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))}
                  placeholder="e.g. San Francisco, CA"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-violet-300 focus:ring-violet-100 transition-all"
                />
              </div>

              {/* Description */}
              <div className="group">
                <Label htmlFor="company-description" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <Briefcase className="w-3.5 h-3.5" />
                  </div>
                  Description
                </Label>
                <textarea
                  id="company-description"
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Brief description of the company..."
                  rows={3}
                  className="w-full px-4 py-2.5 rounded-lg border border-slate-200 bg-slate-50/50 text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-violet-500/20 focus:border-violet-300 transition-colors resize-none"
                />
              </div>

              <DialogFooter className="pt-4 border-t border-slate-100">
                <Button type="button" variant="outline" onClick={() => setDialogOpen(false)} className="h-11 px-6">
                  Cancel
                </Button>
                <Button 
                  type="submit" 
                  disabled={saving} 
                  className="h-11 px-6 bg-gradient-to-r from-violet-500 to-purple-500 hover:from-violet-600 hover:to-purple-600 shadow-lg shadow-violet-200/50"
                >
                  {saving ? (
                    <span className="flex items-center gap-2">
                      <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                      </svg>
                      {editingCompany ? 'Updating...' : 'Creating...'}
                    </span>
                  ) : (
                    <span className="flex items-center gap-2">
                      {editingCompany ? 'Update Company' : 'Create Company'}
                      <Sparkles className="w-4 h-4" />
                    </span>
                  )}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        <AlertDialog open={!!deleteConfirmCompany} onOpenChange={(open) => !open && setDeleteConfirmCompany(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete company?</AlertDialogTitle>
              <AlertDialogDescription asChild>
                <div className="space-y-2">
                  <p>This will remove &quot;{deleteConfirmCompany?.name}&quot;. This action cannot be undone.</p>
                  {deleteConfirmCompany && (getContactCount(deleteConfirmCompany.id) > 0 || getDealCount(deleteConfirmCompany.id) > 0) && (
                    <div className="mt-2 p-3 rounded-lg bg-amber-50 border border-amber-200">
                      <p className="text-sm font-medium text-amber-800 flex items-center gap-1.5">
                        <AlertCircle className="w-4 h-4" />
                        Warning: This company has linked records
                      </p>
                      <ul className="mt-1.5 text-sm text-amber-700 list-disc list-inside">
                        {getContactCount(deleteConfirmCompany.id) > 0 && (
                          <li>{getContactCount(deleteConfirmCompany.id)} contact{getContactCount(deleteConfirmCompany.id) > 1 ? 's' : ''} will be unlinked</li>
                        )}
                        {getDealCount(deleteConfirmCompany.id) > 0 && (
                          <li>{getDealCount(deleteConfirmCompany.id)} deal{getDealCount(deleteConfirmCompany.id) > 1 ? 's' : ''} will be unlinked</li>
                        )}
                      </ul>
                    </div>
                  )}
                </div>
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
        </main>
      </PageTransition>
    </div>
  );
}
