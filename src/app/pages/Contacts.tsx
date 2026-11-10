import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { 
  Search, Plus, Pencil, Trash2, Building2, Mail, Phone, Briefcase,
  User, X, SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, Calendar, Activity as ActivityIcon,
  MessageSquare, Clock, Users, Sparkles, Paperclip, BarChart3, AlertCircle,
  ExternalLink, Archive, ArchiveRestore, Upload, Download
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getContactsPaged, getContactById, createContact, updateContact, deleteContact, getCompanies, getDealsPaged, messages } from '@/app/api';
import { showEditConflict } from '@/app/lib/editConflict';
import { whenChosen } from '@/app/lib/whenChosen';
import {
  archiveContact, unarchiveContact, exportContacts, getContactStats,
  type ContactActivity, type ContactListFilters, type ContactSortField, type ContactStats, type ContactStatus,
} from '@/app/api/contacts';
import { getTasks } from '@/app/api/tasks';
import type { Contact, Company, Deal, TaskItem } from '@/app/api/types';
import { recordHref } from '@/app/hooks/useDocumentTitle';
import { Input } from '@/app/components/ui/input';
import { Button } from '@/app/components/ui/button';
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
import { DuplicateMerge, CONTACT_DUPLICATES } from '@/app/components/duplicates';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { useMoney } from '@/app/hooks/useMoney';

const SORTS: ContactSortField[] = ['name', 'email', 'lastActivity', 'createdAt'];

/** "lastActivity-desc" from the address or the sort menu; anything else is name A-Z. */
function parseSort(value: string | null): [ContactSortField, 'asc' | 'desc'] {
  const [field, dir] = (value ?? '').split('-');
  return SORTS.includes(field as ContactSortField)
    ? [field as ContactSortField, dir === 'desc' ? 'desc' : 'asc']
    : ['name', 'asc'];
}

export default function Contacts() {
  const money = useMoney();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Pagination state from URL params
  const currentPage = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;
  const searchFromUrl = searchParams.get('search') || '';
  // The filters and the order are in the address too, so a filtered list survives a
  // reload and can be shared, and the server applies them to every contact.
  const statusParam = searchParams.get('status');
  const status: ContactStatus = statusParam === 'archived' || statusParam === 'all' ? statusParam : 'active';
  const filterCompany = searchParams.get('company') || 'all';
  const activityParam = searchParams.get('activity');
  const filterActivity: 'all' | ContactActivity = activityParam === 'recent' || activityParam === 'quiet' ? activityParam : 'all';
  const [sortField, sortDirection] = parseSort(searchParams.get('sort'));
  
  const [contacts, setContacts] = useState<Contact[]>([]);
  // The figures above the list, over the whole workspace (null until read, or if they could not be).
  const [stats, setStats] = useState<ContactStats | null>(null);
  const [exporting, setExporting] = useState(false);
  const [companies, setCompanies] = useState<Company[]>([]);
  // HP-9: Deals linked to contacts
  const [allDeals, setAllDeals] = useState<Deal[]>([]);
  // HP-12: Task visibility on contacts
  const [allTasks, setAllTasks] = useState<TaskItem[]>([]);
  // Only the first load blanks the list and holds the overlay; a search keystroke
  // or page change keeps the list on screen (see Leads.tsx).
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const loadedOnceRef = useRef(false);
  usePageLoading(loading);
  const [searchQuery, setSearchQuery] = useState(searchFromUrl);
  const [debouncedSearch, setDebouncedSearch] = useState(searchFromUrl);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingContact, setEditingContact] = useState<Contact | null>(null);
  const [form, setForm] = useState({ name: '', email: '', phone: '', jobTitle: '', companyId: '', description: '', doNotContact: false });
  const [saving, setSaving] = useState(false);
  const [deleteConfirmContact, setDeleteConfirmContact] = useState<Contact | null>(null);
  const [deleting, setDeleting] = useState(false);
  
  
  // Pagination state
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  
  const [showFilters, setShowFilters] = useState(false);

  /** Change one filter in the address, back on the first page: page 3 of another list means nothing. */
  const setListParam = (key: string, value: string | null) => {
    const params = new URLSearchParams(searchParams);
    if (value === null) params.delete(key);
    else params.set(key, value);
    params.set('page', '1');
    setSearchParams(params);
  };
  const setStatus = (next: ContactStatus) => setListParam('status', next === 'active' ? null : next);
  const setFilterCompany = (next: string) => setListParam('company', next === 'all' ? null : next);
  const setFilterActivity = (next: 'all' | ContactActivity) => setListParam('activity', next === 'all' ? null : next);
  const setSort = (field: ContactSortField, dir: 'asc' | 'desc') =>
    setListParam('sort', field === 'name' && dir === 'asc' ? null : `${field}-${dir}`);

  // What the server is asked for, the list and the export alike.
  const listFilters = useMemo<ContactListFilters>(() => ({
    search: debouncedSearch || undefined,
    status,
    companyId: ['all', 'some', 'none'].includes(filterCompany) ? undefined : filterCompany,
    hasCompany: filterCompany === 'some' ? true : filterCompany === 'none' ? false : undefined,
    activity: filterActivity === 'all' ? undefined : filterActivity,
    sortBy: sortField,
    sortDir: sortDirection,
  }), [debouncedSearch, status, filterCompany, filterActivity, sortField, sortDirection]);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Update URL when search changes
  useEffect(() => {
    const params = new URLSearchParams(searchParams);
    if (debouncedSearch) {
      params.set('search', debouncedSearch);
      params.set('page', '1'); // Reset to first page on search
    } else {
      params.delete('search');
    }
    setSearchParams(params, { replace: true });
  }, [debouncedSearch]);

  // Fetch contacts with pagination
  const fetchContacts = useCallback(async () => {
    if (loadedOnceRef.current) setRefreshing(true);
    else setLoading(true);
    try {
      const [pagedResult, statsData, companiesData, dealsResult, tasksResult] = await Promise.all([
        getContactsPaged({ page: currentPage, pageSize, ...listFilters }),
        // The figures help; the page still works without them.
        getContactStats().catch(() => null),
        getCompanies(),
        getDealsPaged({ page: 1, pageSize: 100 }), // server clamps to max 100
        getTasks(), // HP-12: Load all tasks for contact visibility
      ]);
      setContacts(pagedResult.items);
      setStats(statsData);
      setTotalCount(pagedResult.totalCount);
      setTotalPages(pagedResult.totalPages);
      setCompanies(companiesData);
      setAllDeals(dealsResult.items);
      setAllTasks(tasksResult);
      loadedOnceRef.current = true;
    } catch (error) {
      console.error('Failed to fetch contacts:', error);
      toast.error('Failed to load contacts');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [currentPage, pageSize, listFilters]);

  useEffect(() => {
    fetchContacts();
  }, [fetchContacts]);

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

  // Count active filters
  const activeFilterCount = [
    filterCompany !== 'all',
    filterActivity !== 'all',
  ].filter(Boolean).length;

  // Clear all filters
  const clearFilters = () => {
    const params = new URLSearchParams(searchParams);
    for (const key of ['company', 'activity', 'status', 'search']) params.delete(key);
    params.set('page', '1');
    setSearchParams(params);
    setSearchQuery('');
  };
  const filtered = activeFilterCount > 0 || status !== 'active' || !!debouncedSearch;

  // Every contact the list shows with these filters, written by the server: the page used
  // to export the twenty on screen.
  const handleExport = async () => {
    setExporting(true);
    try {
      const result = await exportContacts(listFilters);
      if (!result) toast.success('Contacts exported');
      else if (result.exported < result.total) toast.success(`Exported the first ${result.exported.toLocaleString()} of ${result.total.toLocaleString()} contacts`);
      else toast.success(`Exported ${result.exported.toLocaleString()} contact${result.exported === 1 ? '' : 's'}`);
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : 'The export failed');
    } finally {
      setExporting(false);
    }
  };

  const openCreate = () => {
    setEditingContact(null);
    setForm({ name: '', email: '', phone: '', jobTitle: '', companyId: '', description: '', doNotContact: false });
    setDialogOpen(true);
  };

  const openEdit = (contact: Contact) => {
    setEditingContact(contact);
    setForm({
      name: contact.name,
      email: contact.email,
      phone: contact.phone ?? '',
      jobTitle: contact.jobTitle ?? '',
      companyId: contact.companyId ?? '',
      description: contact.description ?? '',
      doNotContact: contact.doNotContact ?? false,
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim() || !form.email.trim()) {
      toast.error(messages.validation.nameAndEmailRequired);
      return;
    }
    setSaving(true);
    try {
      if (editingContact) {
        // An emptied field is sent as null, which takes it off: left out, as it was, a
        // contact who left a company stayed linked to it. The version makes the save
        // refused, rather than written over, when someone saved the contact since.
        const { contact: updated, error, conflict } = await updateContact(editingContact.id, {
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || null,
          jobTitle: form.jobTitle.trim() || null,
          companyId: form.companyId || null,
          description: form.description?.trim() || null,
          doNotContact: form.doNotContact,
          version: editingContact.version,
        });
        if (updated) {
          toast.success(messages.success.contactUpdated);
          setDialogOpen(false);
          fetchContacts(); // Refresh data
        } else if (conflict) {
          const contactId = editingContact.id;
          showEditConflict(error, () => {
            void getContactById(contactId)
              .then((fresh) => { if (fresh) openEdit(fresh); })
              .catch(() => toast.error(messages.errors.generic));
            fetchContacts();
          });
        } else {
          toast.error(error ?? messages.errors.generic);
        }
      } else {
        const { contact: created, error } = await createContact({
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          jobTitle: form.jobTitle.trim() || undefined,
          companyId: form.companyId || undefined,
          description: form.description?.trim() || undefined,
          // The switch is on this form; it used to be dropped when the contact was new.
          doNotContact: form.doNotContact,
        });
        if (created) {
          toast.success(messages.success.contactCreated);
          setDialogOpen(false);
          fetchContacts(); // Refresh data
        } else {
          toast.error(error ?? messages.errors.generic);
        }
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  // HP-5: Archive/Unarchive contact
  const handleArchiveToggle = async (contact: Contact) => {
    try {
      if (contact.isArchived) {
        const ok = await unarchiveContact(contact.id);
        if (ok) {
          toast.success('Contact unarchived');
          fetchContacts();
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const ok = await archiveContact(contact.id);
        if (ok) {
          toast.success('Contact archived');
          fetchContacts();
        } else {
          toast.error(messages.errors.generic);
        }
      }
    } catch (error) {
      // The server says why, e.g. that an active contact already uses this email.
      toast.error(error instanceof Error && error.message ? error.message : messages.errors.generic);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmContact) return;
    setDeleting(true);
    try {
      const ok = await deleteContact(deleteConfirmContact.id);
      if (ok) {
        toast.success(messages.success.contactDeleted);
        setDeleteConfirmContact(null);
        fetchContacts(); // Refresh data
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const companyName = (id: string) => companies.find((c) => c.id === id)?.name ?? '';

  // Get initials from name
  const getInitials = (name: string) => {
    return name
      .split(' ')
      .map((n) => n[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  };

  // HP-9: Get deals for a contact
  const getDealsForContact = (contactId: string) => allDeals.filter(d => d.contactId === contactId);
  // HP-12: Get tasks for a contact
  const getTasksForContact = (contactId: string) => allTasks.filter(t => t.contactId === contactId);

  // Get avatar gradient based on name
  const getAvatarGradient = (name: string) => {
    const gradients = [
      'from-blue-500 to-cyan-400',
      'from-purple-500 to-pink-400',
      'from-emerald-500 to-teal-400',
      'from-orange-500 to-amber-400',
      'from-rose-500 to-pink-400',
      'from-indigo-500 to-purple-400',
    ];
    const index = name.charCodeAt(0) % gradients.length;
    return gradients[index];
  };

  // The page's numbers, in its header. With Company, Active and Need Attention
  // open the filters with that filter set, as their cards did.
  // How this person likes contacts shown: cards, a list or a table, saved for them.
  const view = useViewPreference('contacts', { layouts: ['grid', 'list', 'table'] });
  const openContact = (c: Contact) => navigate(recordHref(`/contacts/${c.id}`, c.name, 'Contacts'));
  const shortDate = (iso?: string) => {
    if (!iso) return '—';
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  };
  const contactColumns: RecordColumn<Contact>[] = [
    {
      id: 'name',
      label: 'Name',
      required: true,
      cell: (c) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={c.name} size="sm" />
          <span className="truncate">{c.name}</span>
          {c.doNotContact && <span className="shrink-0 rounded-full bg-red-100 px-1.5 text-[10px] font-semibold text-red-700">DNC</span>}
          {c.isArchived && <span className="shrink-0 rounded-full bg-slate-100 px-1.5 text-[10px] font-semibold text-slate-600">Archived</span>}
        </span>
      ),
    },
    { id: 'email', label: 'Email', cell: (c) => c.email || '—' },
    { id: 'phone', label: 'Phone', cell: (c) => c.phone || '—' },
    { id: 'jobTitle', label: 'Job title', cell: (c) => c.jobTitle || '—' },
    { id: 'company', label: 'Company', cell: (c) => (c.companyId ? companyName(c.companyId) || '—' : '—') },
    { id: 'deals', label: 'Deals', numeric: true, cell: (c) => getDealsForContact(c.id).length || '—' },
    { id: 'tasks', label: 'Tasks', numeric: true, byDefault: false, cell: (c) => getTasksForContact(c.id).length || '—' },
    { id: 'lastActivity', label: 'Last activity', cell: (c) => shortDate(c.lastActivityAtUtc) },
    { id: 'added', label: 'Added', byDefault: false, cell: (c) => shortDate(c.createdAtUtc) },
  ];

  /** The same menu on a card, a row and a table line. */
  const contactMenu = (contact: Contact, triggerClassName = '') => (
    <WriteOnly>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className={`h-8 w-8 p-0 ${triggerClassName}`}>
            <span className="sr-only">Actions for {contact.name}</span>
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z" />
            </svg>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-48">
          <DropdownMenuItem onClick={() => openEdit(contact)}>
            <Pencil className="w-4 h-4 mr-2" />
            Edit Contact
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}>
            <Paperclip className="w-4 h-4 mr-2" />
            Attach copy
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => handleArchiveToggle(contact)}>
            {contact.isArchived ? (
              <><ArchiveRestore className="w-4 h-4 mr-2" /> Unarchive</>
            ) : (
              <><Archive className="w-4 h-4 mr-2" /> Archive</>
            )}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            onClick={() => setDeleteConfirmContact(contact)}
            className="text-red-600 focus:text-red-600"
          >
            <Trash2 className="w-4 h-4 mr-2" />
            Delete
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </WriteOnly>
  );

  const contactHeroStats: PageStat[] | undefined = stats ? [
    { label: 'Total Contacts', value: stats.total, icon: BarChart3, tone: 'emerald' },
    {
      label: 'With Company', value: stats.withCompany, icon: Building2, tone: 'violet', pressed: filterCompany === 'some',
      onClick: () => { setFilterCompany(filterCompany === 'some' ? 'all' : 'some'); setShowFilters(true); },
      actionLabel: filterCompany === 'some'
        ? `With a company, ${stats.withCompany}. Filter is on. Activate to show every contact.`
        : `With a company, ${stats.withCompany}. Activate to show only contacts with a company.`,
    },
    {
      label: 'Active (30d)', value: stats.activeLast30Days, icon: ActivityIcon, tone: 'teal', pressed: filterActivity === 'recent',
      onClick: () => { setFilterActivity('recent'); setShowFilters(true); },
      actionLabel: `Active in the last 30 days, ${stats.activeLast30Days}. Activate to show recently active contacts.`,
    },
    {
      label: 'Need Attention', value: stats.quietOver90Days, icon: Clock, tone: 'amber', hint: 'quiet 90d+', pressed: filterActivity === 'quiet',
      onClick: () => { setFilterActivity('quiet'); setShowFilters(true); },
      actionLabel: `Need attention, ${stats.quietOver90Days}. No activity in 90 days. Activate to show them.`,
    },
    { label: 'This Week', value: stats.addedLast7Days, icon: Calendar, tone: 'blue', hint: 'added, last 7 days' },
    { label: 'With Phone', value: stats.withPhone, icon: Phone, tone: 'cyan' },
    {
      label: 'Archived', value: stats.archived, icon: Archive, tone: 'slate', pressed: status === 'archived',
      onClick: () => setStatus(status === 'archived' ? 'active' : 'archived'),
      actionLabel: status === 'archived'
        ? `Archived, ${stats.archived}. Showing archived contacts. Activate to show active ones.`
        : `Archived, ${stats.archived}. Activate to show archived contacts, to restore one.`,
    },
  ] : undefined;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Users}
            tone="emerald"
            title="Contacts"
            subtitle={loading ? 'Loading…' : refreshing ? 'Updating…' : 'Manage your business relationships'}
            actions={
              <>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => { void handleExport(); }}
                  disabled={exporting || loading}
                  className={heroSecondary}
                  aria-label="Export the contacts this list shows"
                >
                  <Download className="h-4 w-4" /> {exporting ? 'Exporting…' : 'Export'}
                </Button>
                <WriteOnly>
                  <Button onClick={() => navigate('/contacts/import')} variant="outline" className={heroSecondary} aria-label="Import">
                    <Upload className="h-4 w-4" />
                    <span className="hidden sm:inline">Import</span>
                  </Button>
                  <Button onClick={openCreate} className={heroPrimary('emerald')}>
                    <Plus className="h-4 w-4" />
                    Add Contact
                  </Button>
                </WriteOnly>
              </>
            }
            stats={!loading ? contactHeroStats : undefined}
          >
            {/* Quick insight: contacts that have gone quiet. */}
            {!loading && stats && stats.quietOver90Days > 3 && filterActivity !== 'quiet' && (
              <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50/80 px-3 py-2">
                <div className="flex min-w-0 flex-1 items-start gap-2 sm:items-center">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600 sm:mt-0" aria-hidden />
                  <p className="min-w-0 text-sm text-amber-700">
                    <span className="font-semibold text-amber-800">Re-engagement Opportunity</span>
                    {' · '}
                    {stats.quietOver90Days} contacts haven't had activity in over 90 days.
                    <span className="hidden sm:inline"> Consider reaching out to maintain relationships.</span>
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => { setFilterActivity('quiet'); setShowFilters(true); }}
                  className="h-8 shrink-0 rounded-lg border-amber-300 bg-white/70 text-amber-700 hover:bg-amber-100"
                >
                  View Inactive
                </Button>
              </div>
            )}
          </PageHero>

        <DuplicateMerge spec={CONTACT_DUPLICATES} onMerged={() => { void fetchContacts(); }} />

        {/* Search, filter and sort */}
        <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-emerald-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-teal-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-emerald-500/20 via-teal-500/20 to-emerald-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-emerald-100 to-teal-100 flex items-center justify-center group-focus-within:from-emerald-200 group-focus-within:to-teal-200 transition-all duration-300">
                  <Search className="w-4 h-4 text-emerald-600 group-focus-within:text-emerald-700 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search by name, email, phone, or job title..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-orange-300 focus:ring-4 focus:ring-orange-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                  aria-label="Search contacts"
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery('')}
                    className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-slate-100 hover:bg-red-50 flex items-center justify-center text-slate-500 hover:text-red-500 transition-all duration-200"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            </div>

            {/* Filter & Sort Controls */}
            <div className="flex flex-wrap items-center gap-2">
              {/* Which contacts: archived ones can be found again here, and restored. */}
              <div role="group" aria-label="Show contacts" className="flex h-11 items-center rounded-xl border border-slate-200 bg-white p-1 shadow-sm">
                {(['active', 'archived', 'all'] as const).map((value) => (
                  <button
                    key={value}
                    type="button"
                    aria-pressed={status === value}
                    onClick={() => setStatus(value)}
                    className={`h-full rounded-lg px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-400 ${
                      status === value ? 'bg-emerald-600 text-white shadow-sm' : 'text-slate-600 hover:bg-emerald-50 hover:text-emerald-700'
                    }`}
                  >
                    {value === 'active' ? 'Active' : value === 'archived' ? 'Archived' : 'All'}
                  </button>
                ))}
              </div>
              {/* Filter Button - Enhanced */}
              <div className="relative group/btn">
                <div className={`absolute inset-0 rounded-xl blur-lg transition-all duration-300 ${
                  showFilters || activeFilterCount > 0 
                    ? 'bg-gradient-to-r from-emerald-500/40 to-teal-500/40 opacity-100' 
                    : 'bg-emerald-200/50 opacity-0 group-hover/btn:opacity-60'
                }`} />
                <Button
                  variant="outline"
                  onClick={() => setShowFilters(!showFilters)}
                  className={`relative h-11 px-4 rounded-xl border shadow-sm transition-all duration-300 ${
                    showFilters || activeFilterCount > 0
                      ? 'bg-gradient-to-r from-emerald-500 to-teal-500 border-emerald-400/50 text-white hover:from-emerald-400 hover:to-teal-400 hover:shadow-emerald-500/25'
                      : 'bg-white border-slate-200 text-slate-700 hover:bg-emerald-50 hover:border-emerald-300 hover:text-emerald-700'
                  }`}
                >
                  <div className={`w-6 h-6 rounded-lg flex items-center justify-center mr-2 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0 ? 'bg-white/20' : 'bg-emerald-100 text-emerald-700'
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
                    const [field, dir] = parseSort(v);
                    setSort(field, dir);
                  }}
                >
                  <SelectTrigger aria-label="Sort contacts by" className="relative h-11 w-[180px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-violet-50 hover:border-violet-300 focus:border-violet-300 focus:ring-2 focus:ring-violet-200/60 transition-all duration-300">
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
                      <ArrowUp className="w-3.5 h-3.5 text-slate-500" />
                      Name A-Z
                    </span>
                  </SelectItem>
                  <SelectItem value="name-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-500" />
                      Name Z-A
                    </span>
                  </SelectItem>
                  <SelectItem value="lastActivity-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-500" />
                      Recently Active
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-500" />
                      Newest First
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-500" />
                      Oldest First
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
              </div>

              <ViewSwitcher view={view} columns={columnChoices(contactColumns)} />
            </div>
          </div>

          {/* Expandable Filter Panel */}
          {showFilters && (
            <div className="mt-4 rounded-xl border border-emerald-100 bg-gradient-to-b from-emerald-50/70 via-white to-white p-4 shadow-sm animate-in slide-in-from-top-2 duration-200">
              <div className="flex flex-wrap gap-4">
                {/* Company Filter */}
                <div className="min-w-[180px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-500 uppercase tracking-wider mb-2">
                    <Building2 className="w-3.5 h-3.5" />
                    Company
                  </label>
                  <Select value={filterCompany} onValueChange={setFilterCompany}>
                    <SelectTrigger aria-label="Company" className="h-10 rounded-lg border-slate-200 bg-white text-slate-800 shadow-sm hover:border-emerald-300 transition-colors">
                      <SelectValue placeholder="All companies" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All contacts</SelectItem>
                      <SelectItem value="some">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-violet-400" />
                          Has a company
                        </span>
                      </SelectItem>
                      <SelectItem value="none">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-slate-300" />
                          No company
                        </span>
                      </SelectItem>
                      {companies.map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          <span className="flex items-center gap-2">
                            <Building2 className="w-3.5 h-3.5 text-slate-500" />
                            {c.name}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Activity Filter */}
                <div className="min-w-[160px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-500 uppercase tracking-wider mb-2">
                    <ActivityIcon className="w-3.5 h-3.5" />
                    Activity
                  </label>
                  <Select value={filterActivity} onValueChange={(v) => setFilterActivity(v as 'all' | ContactActivity)}>
                    <SelectTrigger aria-label="Activity" className="h-10 rounded-lg border-slate-200 bg-white text-slate-800 shadow-sm hover:border-emerald-300 transition-colors">
                      <SelectValue placeholder="All activity" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All contacts</SelectItem>
                      <SelectItem value="recent">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-emerald-500" />
                          Recently active (30d)
                        </span>
                      </SelectItem>
                      <SelectItem value="quiet">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-amber-500" />
                          Inactive (90+ days)
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
          {(activeFilterCount > 0 || searchQuery || status !== 'active') && (
            <div className="mt-4 flex flex-wrap items-center gap-2">
              <span className="text-xs text-slate-500">Showing:</span>
              {status !== 'active' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                  <Archive className="w-3 h-3" />
                  {status === 'archived' ? 'Archived' : 'Active and archived'}
                  <button type="button" onClick={() => setStatus('active')} aria-label="Show active contacts only" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-slate-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {searchQuery && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                  <Search className="w-3 h-3" />
                  &quot;{searchQuery}&quot;
                  <button type="button" onClick={() => setSearchQuery('')} aria-label="Clear search" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-emerald-100 hover:text-emerald-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-400">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterCompany !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-purple-50 text-purple-700 text-xs font-medium border border-purple-200">
                  <Building2 className="w-3 h-3" />
                  {filterCompany === 'none' ? 'No company' : filterCompany === 'some' ? 'Has a company' : companyName(filterCompany)}
                  <button type="button" onClick={() => setFilterCompany('all')} aria-label="Remove company filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-purple-100 hover:text-purple-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-purple-400">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterActivity !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-emerald-50 text-emerald-700 text-xs font-medium border border-emerald-200">
                  <ActivityIcon className="w-3 h-3" />
                  {filterActivity === 'recent' ? 'Recently Active' : 'Quiet 90+ days'}
                  <button type="button" onClick={() => setFilterActivity('all')} aria-label="Remove activity filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-emerald-100 hover:text-emerald-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-400">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-500">
                {totalCount.toLocaleString()} {totalCount === 1 ? 'contact' : 'contacts'}
              </span>
            </div>
          )}
        </div>

        {loading ? (
          <ContentSkeleton rows={6} />
        ) : contacts.length === 0 && !filtered && (stats?.total ?? 0) === 0 ? (
          /* Enhanced Empty State */
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-blue-500 via-cyan-500 to-blue-400 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <Users className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Build Your Contact Network</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Contacts are the people you do business with. Track relationships, activities, and communication history.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-3 gap-4 mb-6">
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mx-auto mb-2">
                      <User className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Organize</p>
                    <p className="text-xs text-slate-500 mt-0.5">Keep contact details in one place</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-purple-100 flex items-center justify-center mx-auto mb-2">
                      <Building2 className="w-5 h-5 text-purple-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Link</p>
                    <p className="text-xs text-slate-500 mt-0.5">Connect to companies & deals</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mx-auto mb-2">
                      <MessageSquare className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Engage</p>
                    <p className="text-xs text-slate-500 mt-0.5">Track communication history</p>
                  </div>
                </div>
                
                <div className="flex flex-col sm:flex-row gap-3 justify-center">
                  <WriteOnly>
                    <Button onClick={openCreate} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-blue-500 to-cyan-500 hover:from-blue-600 hover:to-cyan-600 shadow-lg shadow-blue-200/50">
                      <Plus className="w-4 h-4" />
                      Add Your First Contact
                    </Button>
                  </WriteOnly>
                </div>
              </div>
            </div>
          </div>
        ) : contacts.length === 0 ? (
          /* No results state */
          <div className="flex items-center gap-3 p-6 rounded-xl bg-white border border-slate-200">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center">
              <Search className="w-6 h-6 text-slate-500" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-slate-700">
                {status === 'archived' && activeFilterCount === 0 && !debouncedSearch ? 'No archived contacts' : 'No contacts match your filters'}
              </p>
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
              label="Contacts"
              items={contacts}
              view={view}
              getKey={(c) => c.id}
              onOpen={openContact}
              leading={(c) => <InitialsAvatar name={c.name} />}
              title={(c) => c.name}
              subtitle={(c) => [c.jobTitle, c.companyId ? companyName(c.companyId) : ''].filter(Boolean).join(' · ') || c.email}
              columns={contactColumns}
              actions={(c) => contactMenu(c)}
            />
          ) : view.layout === 'table' ? (
            <RecordTable
              label="Contacts"
              items={contacts}
              view={view}
              getKey={(c) => c.id}
              onOpen={openContact}
              columns={contactColumns}
              actions={(c) => contactMenu(c)}
            />
          ) : (
          /* Contact Cards Grid */
          <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
            {contacts.map((contact, index) => {
              const company = contact.companyId ? companyName(contact.companyId) : null;
              const initials = getInitials(contact.name);
              const gradient = getAvatarGradient(contact.name);
              
              // Check if inactive
              const ninetyDaysAgo = new Date();
              ninetyDaysAgo.setDate(ninetyDaysAgo.getDate() - 90);
              const isInactive = !contact.lastActivityAtUtc || new Date(contact.lastActivityAtUtc) < ninetyDaysAgo;
              
              return (
                <StaggerItem
                  key={contact.id}
                  index={index}
                  className={`group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-slate-300 overflow-hidden ${liftOnHover}`}
                >
                  {/* Inactive indicator */}
                  {isInactive && (
                    <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-amber-400 to-orange-400" />
                  )}
                  
                  <div className={view.density === 'compact' ? 'p-4' : 'p-5'}>
                    {/* Header with avatar and actions */}
                    <div className="flex items-start gap-4 mb-4">
                      <div className={`w-14 h-14 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-white font-bold text-lg shadow-lg group-hover:scale-105 transition-transform duration-300`}>
                        {initials || <User className="w-6 h-6" />}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <h3 
                          className="font-semibold text-slate-900 text-lg truncate group-hover:text-blue-600 transition-colors cursor-pointer"
                          onClick={() => navigate(recordHref(`/contacts/${contact.id}`, contact.name, 'Contacts'))}
                        >
                          {contact.name}
                        </h3>
                        {contact.jobTitle && (
                          <p className="text-sm text-slate-500 truncate flex items-center gap-1.5">
                            <Briefcase className="w-3.5 h-3.5 text-slate-500" />
                            {contact.jobTitle}
                          </p>
                        )}
                        {contact.doNotContact && (
                          <span className="inline-flex items-center gap-1 mt-1 px-2 py-0.5 text-xs font-medium rounded-full bg-red-100 text-red-700 border border-red-200">
                            <AlertCircle className="w-3 h-3" />
                            Do Not Contact
                          </span>
                        )}
                        {contact.isArchived && (
                          <span className="inline-flex items-center gap-1 mt-1 px-2 py-0.5 text-xs font-medium rounded-full bg-slate-100 text-slate-600 border border-slate-200">
                            <Archive className="w-3 h-3" />
                            Archived
                          </span>
                        )}
                        {company && (
                          <p 
                            className="text-sm text-slate-500 truncate flex items-center gap-1.5 mt-0.5 hover:text-violet-600 cursor-pointer transition-colors"
                            onClick={(e) => {
                              e.stopPropagation();
                              if (contact.companyId) navigate(`/companies/${contact.companyId}`);
                            }}
                          >
                            <Building2 className="w-3.5 h-3.5 text-slate-500" />
                            {company}
                          </p>
                        )}
                      </div>

                      {/* Actions dropdown */}
                      {contactMenu(contact, 'opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity')}
                    </div>

                    {/* Contact details */}
                    <div className="space-y-2 mb-4">
                      <a 
                        href={`mailto:${contact.email}`}
                        className="flex items-center gap-2 text-sm text-slate-600 hover:text-blue-600 transition-colors group/link"
                      >
                        <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center group-hover/link:bg-blue-100 transition-colors">
                          <Mail className="w-4 h-4 text-slate-500 group-hover/link:text-blue-600" />
                        </div>
                        <span className="truncate">{contact.email}</span>
                        <ExternalLink className="w-3 h-3 opacity-0 group-hover/link:opacity-100 transition-opacity" />
                      </a>
                      
                      {contact.phone && (
                        <a 
                          href={`tel:${contact.phone}`}
                          className="flex items-center gap-2 text-sm text-slate-600 hover:text-blue-600 transition-colors group/link"
                        >
                          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center group-hover/link:bg-blue-100 transition-colors">
                            <Phone className="w-4 h-4 text-slate-500 group-hover/link:text-blue-600" />
                          </div>
                          <span>{contact.phone}</span>
                          <ExternalLink className="w-3 h-3 opacity-0 group-hover/link:opacity-100 transition-opacity" />
                        </a>
                      )}
                    </div>

                    {/* HP-9: Linked Deals */}
                    {(() => {
                      const contactDeals = getDealsForContact(contact.id);
                      if (contactDeals.length === 0) return null;
                      return (
                        <div className="pt-3 border-t border-slate-100 mb-3">
                          <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                            <Briefcase className="w-3 h-3" />
                            Deals ({contactDeals.length})
                          </p>
                          <div className="space-y-1.5">
                            {contactDeals.slice(0, 3).map(deal => (
                              <button
                                key={deal.id}
                                type="button"
                                onClick={() => navigate(`/deals/${deal.id}`)}
                                className="w-full flex items-center justify-between px-2 py-1.5 rounded-lg hover:bg-slate-50 transition-colors text-left group/deal"
                              >
                                <span className="text-xs font-medium text-slate-700 truncate group-hover/deal:text-blue-600 transition-colors">{deal.name}</span>
                                <span className="text-xs text-emerald-700 font-semibold shrink-0 ml-2">{money.formatDeal(deal)}</span>
                              </button>
                            ))}
                            {contactDeals.length > 3 && (
                              <p className="text-xs text-slate-500 px-2">+{contactDeals.length - 3} more</p>
                            )}
                          </div>
                        </div>
                      );
                    })()}

                    {/* HP-12: Linked Tasks */}
                    {(() => {
                      const contactTasks = getTasksForContact(contact.id);
                      if (contactTasks.length === 0) return null;
                      return (
                        <div className="pt-3 border-t border-slate-100 mb-3">
                          <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1">
                            <Calendar className="w-3 h-3" />
                            Tasks ({contactTasks.length})
                          </p>
                          <div className="space-y-1.5">
                            {contactTasks.slice(0, 3).map(task => (
                              <div key={task.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg bg-slate-50">
                                <span className={`w-2 h-2 rounded-full shrink-0 ${task.status === 'completed' ? 'bg-emerald-400' : task.status === 'in_progress' ? 'bg-blue-400' : 'bg-slate-300'}`} />
                                <span className={`text-xs font-medium truncate ${task.status === 'completed' ? 'text-slate-500 line-through' : 'text-slate-700'}`}>{task.title}</span>
                              </div>
                            ))}
                            {contactTasks.length > 3 && (
                              <p className="text-xs text-slate-500 px-2">+{contactTasks.length - 3} more</p>
                            )}
                          </div>
                        </div>
                      );
                    })()}

                    {/* Footer with activity info */}
                    <div className="pt-3 border-t border-slate-100 flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        {contact.lastActivityAtUtc ? (
                          <>
                            <span className={`w-2 h-2 rounded-full ${isInactive ? 'bg-amber-400' : 'bg-emerald-400'}`} />
                            <span className="text-xs text-slate-500">
                              {(() => {
                                try {
                                  const d = new Date(contact.lastActivityAtUtc);
                                  if (Number.isNaN(d.getTime())) return 'Unknown';
                                  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
                                } catch {
                                  return 'Unknown';
                                }
                              })()}
                            </span>
                          </>
                        ) : (
                          <>
                            <span className="w-2 h-2 rounded-full bg-slate-300" />
                            <span className="text-xs text-slate-500">No activity</span>
                          </>
                        )}
                      </div>
                      
                      <Button 
                        variant="ghost" 
                        size="sm" 
                        onClick={() => navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}
                        className="h-8 text-xs text-blue-600 hover:text-blue-700 hover:bg-blue-50"
                      >
                        <Paperclip className="w-3.5 h-3.5 mr-1" />
                        Attach copy
                      </Button>
                    </div>
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

        {/* Enhanced Add/Edit Contact Dialog */}
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogContent className="sm:max-w-[500px] p-0 gap-0 overflow-hidden">
            {/* Gradient Header */}
            <div className="relative overflow-hidden">
              <div className="absolute inset-0 bg-gradient-to-br from-blue-500 via-cyan-500 to-blue-400" />
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
              <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
              
              <div className="relative px-6 py-5">
                <div className="flex items-center gap-4">
                  <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                    {editingContact ? <Pencil className="w-6 h-6 text-white" /> : <User className="w-6 h-6 text-white" />}
                  </div>
                  <div>
                    <DialogTitle className="text-xl font-bold text-white tracking-tight">
                      {editingContact ? 'Edit Contact' : 'Add New Contact'}
                    </DialogTitle>
                    <p className="text-white/80 text-sm mt-0.5">
                      {editingContact ? 'Update contact information' : 'Add a new person to your network'}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <form onSubmit={handleSubmit} className="p-6 space-y-4">
              {/* Name Field */}
              <div className="group">
                <Label htmlFor="contact-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-100 text-blue-600">
                    <User className="w-3.5 h-3.5" />
                  </div>
                  Full Name <span className="text-blue-500">*</span>
                </Label>
                <Input
                  id="contact-name"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  placeholder="Enter contact's full name"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100 transition-all"
                  required
                />
              </div>

              {/* Email Field */}
              <div className="group">
                <Label htmlFor="contact-email" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-cyan-100 text-cyan-600">
                    <Mail className="w-3.5 h-3.5" />
                  </div>
                  Email Address <span className="text-blue-500">*</span>
                </Label>
                <Input
                  id="contact-email"
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                  placeholder="contact@company.com"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100 transition-all"
                  required
                />
              </div>

              {/* Phone & Job Title Row */}
              <div className="grid grid-cols-2 gap-4">
                <div className="group">
                  <Label htmlFor="contact-phone" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-700">
                      <Phone className="w-3.5 h-3.5" />
                    </div>
                    Phone
                  </Label>
                  <Input
                    id="contact-phone"
                    value={form.phone}
                    onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                    placeholder="+1 (555) 000-0000"
                    className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100 transition-all"
                  />
                </div>

                <div className="group">
                  <Label htmlFor="contact-jobtitle" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-purple-100 text-purple-600">
                      <Briefcase className="w-3.5 h-3.5" />
                    </div>
                    Job Title
                  </Label>
                  <Input
                    id="contact-jobtitle"
                    value={form.jobTitle}
                    onChange={(e) => setForm((f) => ({ ...f, jobTitle: e.target.value }))}
                    placeholder="e.g. CEO, Manager"
                    className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100 transition-all"
                  />
                </div>
              </div>

              {/* Company Select */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-700">
                    <Building2 className="w-3.5 h-3.5" />
                  </div>
                  Company
                </Label>
                <Select
                  value={form.companyId || 'none'}
                  onValueChange={whenChosen((v) => setForm((f) => ({ ...f, companyId: v === 'none' ? '' : v })))}
                >
                  <SelectTrigger aria-label="Company" className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100">
                    <SelectValue placeholder="Select a company" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-500">— No company —</span>
                    </SelectItem>
                    {companies.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        <span className="flex items-center gap-2">
                          <Building2 className="w-3.5 h-3.5 text-slate-500" />
                          {c.name}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Do Not Contact toggle — HP-4 */}
              <div className="group flex items-center justify-between p-3 rounded-lg border border-slate-200 bg-slate-50/50">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 cursor-pointer">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-red-100 text-red-600">
                    <AlertCircle className="w-3.5 h-3.5" />
                  </div>
                  Do Not Contact
                </Label>
                <button aria-label="Do Not Contact"
                  type="button"
                  role="switch"
                  aria-checked={form.doNotContact}
                  onClick={() => setForm((f) => ({ ...f, doNotContact: !f.doNotContact }))}
                  className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 ${form.doNotContact ? 'bg-red-500' : 'bg-slate-200'}`}
                >
                  <span className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${form.doNotContact ? 'translate-x-5' : 'translate-x-0'}`} />
                </button>
              </div>

              {/* Description / Notes — HP-7 */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <Briefcase className="w-3.5 h-3.5" />
                  </div>
                  Notes / Description
                </Label>
                <textarea aria-label="Notes / Description"
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Add notes about this contact..."
                  rows={3}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-200 bg-slate-50/50 focus:bg-white focus:border-blue-300 focus:ring-2 focus:ring-blue-100 focus:outline-none resize-none transition-all"
                />
              </div>

              <DialogFooter className="pt-4 border-t border-slate-100">
                <Button type="button" variant="outline" onClick={() => setDialogOpen(false)} className="h-11 px-6">
                  Cancel
                </Button>
                <Button 
                  type="submit" 
                  disabled={saving} 
                  className="h-11 px-6 bg-gradient-to-r from-blue-500 to-cyan-500 hover:from-blue-600 hover:to-cyan-600 shadow-lg shadow-blue-200/50"
                >
                  {saving ? (
                    <span className="flex items-center gap-2">
                      <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                      </svg>
                      {editingContact ? 'Updating...' : 'Creating...'}
                    </span>
                  ) : (
                    <span className="flex items-center gap-2">
                      {editingContact ? 'Update Contact' : 'Create Contact'}
                      <Sparkles className="w-4 h-4" />
                    </span>
                  )}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        <AlertDialog open={!!deleteConfirmContact} onOpenChange={(open) => !open && setDeleteConfirmContact(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete contact?</AlertDialogTitle>
              <AlertDialogDescription>
                This will remove &quot;{deleteConfirmContact?.name}&quot;. This action cannot be undone.
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
