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
import ExportCsvButton from '@/app/components/ExportCsvButton';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCompaniesPaged, getCompanyStats, createCompany, updateCompany, deleteCompany, getDealsPaged, messages } from '@/app/api';
import type { CompanyStatsItem } from '@/app/api/companies';
import type { Company, Deal } from '@/app/api/types';
import { recordHref } from '@/app/hooks/useDocumentTitle';
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
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary, type PageStat } from '@/app/components/PageHero';
import { DuplicateMerge, COMPANY_DUPLICATES } from '@/app/components/duplicates';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';

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
  usePageLoading(loading);
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
  const getContactCount = useCallback((companyId: string) => companyStatsMap.get(companyId)?.contactCount ?? 0, [companyStatsMap]);
  const getDealCount = useCallback((companyId: string) => companyStatsMap.get(companyId)?.dealCount ?? 0, [companyStatsMap]);
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
  }, [companies, searchQuery, filterIndustry, filterSize, sortField, sortDirection, getContactCount, getDealCount]);

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

  // The page's numbers, in its header. Top Industry opens the filters with
  // that industry set, as its card did.
  const topIndustryName = companyStats.topIndustry?.[0];
  const topIndustryCount = companyStats.topIndustry?.[1] || 0;
  // How this person likes companies shown: cards, a list or a table, saved for them.
  const view = useViewPreference('companies', { layouts: ['grid', 'list', 'table'] });
  const openCompany = (c: Company) => navigate(recordHref(`/companies/${c.id}`, c.name, 'Companies'));
  const companyColumns: RecordColumn<Company>[] = [
    {
      id: 'name',
      label: 'Company',
      required: true,
      cell: (c) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={c.name} size="sm" />
          <span className="truncate">{c.name}</span>
        </span>
      ),
    },
    { id: 'domain', label: 'Domain', cell: (c) => c.domain || c.website || '—' },
    { id: 'industry', label: 'Industry', cell: (c) => c.industry || '—' },
    { id: 'size', label: 'Size', byDefault: false, cell: (c) => c.size || '—' },
    { id: 'location', label: 'Location', byDefault: false, cell: (c) => c.location || '—' },
    { id: 'contacts', label: 'Contacts', numeric: true, cell: (c) => getContactCount(c.id) || '—' },
    { id: 'deals', label: 'Deals', numeric: true, cell: (c) => getDealCount(c.id) || '—' },
    { id: 'value', label: 'Pipeline', numeric: true, cell: (c) => (getDealValue(c.id) ? formatCurrency(getDealValue(c.id)) : '—') },
  ];

  /** The same menu on a card, a row and a table line. */
  const companyMenu = (company: Company, triggerClassName = '') => (
    <WriteOnly>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className={`h-8 w-8 p-0 ${triggerClassName}`}>
            <span className="sr-only">Actions for {company.name}</span>
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
    </WriteOnly>
  );

  const companyHeroStats: PageStat[] = [
    { label: 'Total Companies', value: companyStats.total, icon: BarChart3, tone: 'blue' },
    { label: 'With Contacts', value: companyStats.withContacts, icon: UserCircle, tone: 'sky' },
    { label: 'With Deals', value: companyStats.withDeals, icon: Target, tone: 'emerald' },
    { label: 'Total Pipeline', value: companyStats.totalDealValue, icon: DollarSign, tone: 'amber', format: formatCurrency },
    { label: 'This Week', value: companyStats.thisWeek, icon: Calendar, tone: 'violet', hint: 'added' },
    {
      label: 'Top Industry', value: topIndustryName || 'N/A', icon: Factory, tone: 'cyan', hint: `${topIndustryCount} ${topIndustryCount === 1 ? 'company' : 'companies'}`,
      onClick: topIndustryName ? () => { setFilterIndustry(topIndustryName); setShowFilters(true); } : undefined,
      actionLabel: topIndustryName
        ? `Top industry, ${topIndustryName}, ${topIndustryCount} companies. Activate to filter the list to ${topIndustryName}.`
        : undefined,
    },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Building2}
            tone="blue"
            title="Companies"
            subtitle={loading ? 'Loading…' : 'Manage your business accounts'}
            actions={
              <>
                <ExportCsvButton
                  rows={filteredCompanies}
                  filename="companies"
                  className={heroSecondary}
                  columns={[
                    { header: 'Name', value: (c) => c.name },
                    { header: 'Domain', value: (c) => c.domain },
                    { header: 'Industry', value: (c) => c.industry },
                    { header: 'Size', value: (c) => c.size },
                    { header: 'Website', value: (c) => c.website },
                    { header: 'Location', value: (c) => c.location },
                    { header: 'Created', value: (c) => c.createdAtUtc },
                  ]}
                />
                <WriteOnly>
                  <Button onClick={openCreate} className={heroPrimary('blue')}>
                    <Plus className="h-4 w-4" />
                    Add Company
                  </Button>
                </WriteOnly>
              </>
            }
            stats={!loading && companies.length > 0 ? companyHeroStats : undefined}
          >
            {/* Quick insight: accounts with nobody linked yet. */}
            {!loading && companies.length > 0 && companyStats.withContacts < companyStats.total * 0.5 && (
              <div className="flex items-start gap-2 rounded-xl border border-violet-200 bg-violet-50/80 px-3 py-2 sm:items-center">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-violet-600 sm:mt-0" aria-hidden />
                <p className="min-w-0 text-sm text-violet-700">
                  <span className="font-semibold text-violet-800">Opportunity to Expand</span>
                  {' · '}
                  {companyStats.total - companyStats.withContacts} companies don't have any contacts yet.
                  <span className="hidden sm:inline"> Add contacts to build stronger relationships.</span>
                </p>
              </div>
            )}
          </PageHero>

        <DuplicateMerge spec={COMPANY_DUPLICATES} onMerged={() => { void fetchCompanies(); }} />

        {/* Search, filter and sort */}
        <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-blue-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-indigo-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-blue-500/20 via-indigo-500/20 to-blue-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-blue-100 to-indigo-100 flex items-center justify-center group-focus-within:from-blue-200 group-focus-within:to-indigo-200 transition-all duration-300">
                  <Search className="w-4 h-4 text-blue-600 group-focus-within:text-blue-700 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search by name, domain, or industry..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-orange-300 focus:ring-4 focus:ring-orange-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                  aria-label="Search companies"
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

            {/* Filter & Sort Controls */}
            <div className="flex flex-wrap items-center gap-2">
              {/* Filter Button - Enhanced */}
              <div className="relative group/btn">
                <div className={`absolute inset-0 rounded-xl blur-lg transition-all duration-300 ${
                  showFilters || activeFilterCount > 0 
                    ? 'bg-gradient-to-r from-blue-500/40 to-indigo-500/40 opacity-100' 
                    : 'bg-blue-200/50 opacity-0 group-hover/btn:opacity-60'
                }`} />
                <Button
                  variant="outline"
                  onClick={() => setShowFilters(!showFilters)}
                  className={`relative h-11 px-4 rounded-xl border shadow-sm transition-all duration-300 ${
                    showFilters || activeFilterCount > 0
                      ? 'bg-gradient-to-r from-blue-500 to-indigo-500 border-blue-400/50 text-white hover:from-blue-400 hover:to-indigo-400 hover:shadow-blue-500/25'
                      : 'bg-white border-slate-200 text-slate-700 hover:bg-blue-50 hover:border-blue-300 hover:text-blue-700'
                  }`}
                >
                  <div className={`w-6 h-6 rounded-lg flex items-center justify-center mr-2 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0 ? 'bg-white/20' : 'bg-blue-100 text-blue-600'
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
                <div className="absolute inset-0 bg-violet-200/50 rounded-xl blur-lg opacity-0 group-hover/sort:opacity-60 transition-all duration-300" />
                <Select
                  value={`${sortField}-${sortDirection}`}
                  onValueChange={(v) => {
                    const [field, dir] = v.split('-') as [typeof sortField, typeof sortDirection];
                    setSortField(field);
                    setSortDirection(dir);
                  }}
                >
                  <SelectTrigger className="relative h-11 w-[180px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-violet-50 hover:border-violet-300 focus:border-violet-300 focus:ring-2 focus:ring-violet-200/60 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-violet-100 flex items-center justify-center">
                        <ArrowUpDown className="w-3.5 h-3.5 text-violet-600" />
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

              <ViewSwitcher view={view} columns={columnChoices(companyColumns)} />
            </div>
          </div>

          {/* Expandable Filter Panel */}
          {showFilters && (
            <div className="mt-4 rounded-xl border border-blue-100 bg-gradient-to-b from-blue-50/70 via-white to-white p-4 shadow-sm animate-in slide-in-from-top-2 duration-200">
              <div className="flex flex-wrap gap-4">
                {/* Industry Filter */}
                <div className="min-w-[180px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-500 uppercase tracking-wider mb-2">
                    <Factory className="w-3.5 h-3.5" />
                    Industry
                  </label>
                  <Select value={filterIndustry} onValueChange={setFilterIndustry}>
                    <SelectTrigger className="h-10 rounded-lg border-slate-200 bg-white text-slate-800 shadow-sm hover:border-blue-300 transition-colors">
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
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-500 uppercase tracking-wider mb-2">
                    <Users className="w-3.5 h-3.5" />
                    Company Size
                  </label>
                  <Select value={filterSize} onValueChange={setFilterSize}>
                    <SelectTrigger className="h-10 rounded-lg border-slate-200 bg-white text-slate-800 shadow-sm hover:border-blue-300 transition-colors">
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
                      className="h-10 text-slate-500 hover:text-red-600 hover:bg-red-50"
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
              <span className="text-xs text-slate-500">Showing:</span>
              {searchQuery && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                  <Search className="w-3 h-3" />
                  &quot;{searchQuery}&quot;
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-blue-600 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterIndustry !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-cyan-50 text-cyan-700 text-xs font-medium border border-cyan-200">
                  <Factory className="w-3 h-3" />
                  {filterIndustry}
                  <button onClick={() => setFilterIndustry('all')} className="ml-0.5 hover:text-cyan-900 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterSize !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-purple-50 text-purple-700 text-xs font-medium border border-purple-200">
                  <Users className="w-3 h-3" />
                  {filterSize}
                  <button onClick={() => setFilterSize('all')} className="ml-0.5 hover:text-purple-900 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-500">
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
                  <WriteOnly>
                    <Button onClick={openCreate} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-violet-500 to-purple-500 hover:from-violet-600 hover:to-purple-600 shadow-lg shadow-violet-200/50">
                      <Plus className="w-4 h-4" />
                      Add Your First Company
                    </Button>
                  </WriteOnly>
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
          <ViewTransition layout={view.layout}>
          {view.layout === 'list' ? (
            <RecordList
              label="Companies"
              items={filteredCompanies}
              view={view}
              getKey={(c) => c.id}
              onOpen={openCompany}
              leading={(c) => <InitialsAvatar name={c.name} />}
              title={(c) => c.name}
              subtitle={(c) => [c.industry, c.location].filter(Boolean).join(' · ') || c.domain || c.website}
              columns={companyColumns}
              actions={(c) => companyMenu(c)}
            />
          ) : view.layout === 'table' ? (
            <RecordTable
              label="Companies"
              items={filteredCompanies}
              view={view}
              getKey={(c) => c.id}
              onOpen={openCompany}
              columns={companyColumns}
              actions={(c) => companyMenu(c)}
            />
          ) : (
          /* Company Cards Grid */
          <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
            {filteredCompanies.map((company, index) => {
              const initials = getInitials(company.name);
              const gradient = getCompanyGradient(company.name);
              const contactCount = getContactCount(company.id);
              const dealCount = getDealCount(company.id);
              const dealValue = getDealValue(company.id);
              
              return (
                <StaggerItem
                  key={company.id}
                  index={index}
                  className={`group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-slate-300 overflow-hidden ${liftOnHover}`}
                >
                  <div className={view.density === 'compact' ? 'p-4' : 'p-5'}>
                    {/* Header with logo and actions */}
                    <div className="flex items-start gap-4 mb-4">
                      <div className={`w-14 h-14 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-white font-bold text-lg shadow-lg group-hover:scale-105 transition-transform duration-300`}>
                        {initials || <Building2 className="w-6 h-6" />}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <h3 
                          className="font-semibold text-slate-900 text-lg truncate group-hover:text-violet-600 transition-colors cursor-pointer"
                          onClick={() => navigate(recordHref(`/companies/${company.id}`, company.name, 'Companies'))}
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
                      {companyMenu(company, 'opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity')}
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
                </StaggerItem>
              );
            })}
          </div>
          )}
          </ViewTransition>
          
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
