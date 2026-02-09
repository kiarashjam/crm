import { useState, useEffect, useMemo, useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { 
  Search, Plus, Pencil, Trash2, Building2, Mail, Phone, Briefcase,
  User, X, SlidersHorizontal, ArrowUpDown, ArrowUp, ArrowDown, Calendar, Activity as ActivityIcon,
  MessageSquare, Clock, Users, Sparkles, Send, BarChart3, AlertCircle,
  ExternalLink
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import DataPagination from '@/app/components/DataPagination';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getContactsPaged, createContact, updateContact, deleteContact, getCompanies, getDealsPaged, messages } from '@/app/api';
import { getTasks } from '@/app/api/tasks';
import type { Contact, Company, Deal, TaskItem } from '@/app/api/types';
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

export default function Contacts() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Pagination state from URL params
  const currentPage = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('pageSize')) || 20;
  const searchFromUrl = searchParams.get('search') || '';
  
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  // HP-9: Deals linked to contacts
  const [allDeals, setAllDeals] = useState<Deal[]>([]);
  // HP-12: Task visibility on contacts
  const [allTasks, setAllTasks] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);
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
  
  // Filter & Sort state (client-side for additional filters)
  const [filterCompany, setFilterCompany] = useState<string>('all');
  const [filterActivity, setFilterActivity] = useState<'all' | 'recent' | 'inactive'>('all');
  const [sortField, setSortField] = useState<'name' | 'email' | 'lastActivity' | 'createdAt'>('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [showFilters, setShowFilters] = useState(false);

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
    setLoading(true);
    try {
      const [pagedResult, companiesData, dealsResult, tasksResult] = await Promise.all([
        getContactsPaged({ page: currentPage, pageSize, search: debouncedSearch || undefined }),
        getCompanies(),
        getDealsPaged({ page: 1, pageSize: 100 }), // server clamps to max 100
        getTasks(), // HP-12: Load all tasks for contact visibility
      ]);
      setContacts(pagedResult.items);
      setTotalCount(pagedResult.totalCount);
      setTotalPages(pagedResult.totalPages);
      setCompanies(companiesData);
      setAllDeals(dealsResult.items);
      setAllTasks(tasksResult);
    } catch (error) {
      console.error('Failed to fetch contacts:', error);
      toast.error('Failed to load contacts');
    } finally {
      setLoading(false);
    }
  }, [currentPage, pageSize, debouncedSearch]);

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

  // Calculate contact statistics
  const contactStats = useMemo(() => {
    const total = contacts.length;
    const withCompany = contacts.filter(c => c.companyId).length;
    const withPhone = contacts.filter(c => c.phone).length;
    
    // Calculate recently active (within 30 days)
    const thirtyDaysAgo = new Date();
    thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
    const recentlyActive = contacts.filter(c => {
      if (!c.lastActivityAtUtc) return false;
      return new Date(c.lastActivityAtUtc) >= thirtyDaysAgo;
    }).length;
    
    // Calculate contacts added this week
    const startOfWeek = new Date();
    startOfWeek.setDate(startOfWeek.getDate() - startOfWeek.getDay());
    startOfWeek.setHours(0, 0, 0, 0);
    const thisWeek = contacts.filter(c => {
      if (!c.createdAtUtc) return false;
      return new Date(c.createdAtUtc) >= startOfWeek;
    }).length;
    
    // Inactive contacts (no activity in 90 days)
    const ninetyDaysAgo = new Date();
    ninetyDaysAgo.setDate(ninetyDaysAgo.getDate() - 90);
    const inactive = contacts.filter(c => {
      if (!c.lastActivityAtUtc) return true;
      return new Date(c.lastActivityAtUtc) < ninetyDaysAgo;
    }).length;
    
    return { total, withCompany, withPhone, recentlyActive, thisWeek, inactive };
  }, [contacts]);

  const filteredContacts = useMemo(() => {
    let result = [...contacts];
    
    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(
        (c) =>
          c.name.toLowerCase().includes(q) ||
          c.email.toLowerCase().includes(q) ||
          (c.phone && c.phone.includes(q)) ||
          (c.jobTitle && c.jobTitle.toLowerCase().includes(q))
      );
    }
    
    // Company filter
    if (filterCompany !== 'all') {
      if (filterCompany === 'none') {
        result = result.filter(c => !c.companyId);
      } else {
        result = result.filter(c => c.companyId === filterCompany);
      }
    }
    
    // Activity filter
    if (filterActivity !== 'all') {
      const thirtyDaysAgo = new Date();
      thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
      
      if (filterActivity === 'recent') {
        result = result.filter(c => {
          if (!c.lastActivityAtUtc) return false;
          return new Date(c.lastActivityAtUtc) >= thirtyDaysAgo;
        });
      } else if (filterActivity === 'inactive') {
        result = result.filter(c => {
          if (!c.lastActivityAtUtc) return true;
          return new Date(c.lastActivityAtUtc) < thirtyDaysAgo;
        });
      }
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
        case 'lastActivity':
          const aDate = a.lastActivityAtUtc ? new Date(a.lastActivityAtUtc).getTime() : 0;
          const bDate = b.lastActivityAtUtc ? new Date(b.lastActivityAtUtc).getTime() : 0;
          comparison = aDate - bDate;
          break;
        case 'createdAt':
          const aCreated = a.createdAtUtc ? new Date(a.createdAtUtc).getTime() : 0;
          const bCreated = b.createdAtUtc ? new Date(b.createdAtUtc).getTime() : 0;
          comparison = aCreated - bCreated;
          break;
      }
      return sortDirection === 'asc' ? comparison : -comparison;
    });
    
    return result;
  }, [contacts, searchQuery, filterCompany, filterActivity, sortField, sortDirection]);

  // Count active filters
  const activeFilterCount = [
    filterCompany !== 'all',
    filterActivity !== 'all',
  ].filter(Boolean).length;

  // Clear all filters
  const clearFilters = () => {
    setFilterCompany('all');
    setFilterActivity('all');
    setSearchQuery('');
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
        const { contact: updated, error } = await updateContact(editingContact.id, {
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          jobTitle: form.jobTitle.trim() || undefined,
          companyId: form.companyId || undefined,
        });
        if (updated) {
          toast.success(messages.success.contactUpdated);
          setDialogOpen(false);
          fetchContacts(); // Refresh data
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
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Contacts</h1>
                  <p className="text-slate-400 mt-1">
                    {loading ? 'Loading…' : 'Manage your business relationships'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                <Button onClick={openCreate} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white">
                  <Plus className="w-4 h-4" />
                  Add Contact
                </Button>
              </div>
            </div>
          </div>
        </div>

          {/* Stats Cards */}
          {!loading && contacts.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
              {/* Total Contacts */}
              <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{contactStats.total}</p>
                  <p className="text-xs font-medium text-slate-500 mt-1">Total Contacts</p>
                </div>
              </div>

              {/* With Company */}
              <div 
                className="group relative bg-white rounded-2xl border border-purple-100 p-5 shadow-sm hover:shadow-xl hover:shadow-purple-100 hover:border-purple-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterCompany('none'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-purple-50 to-purple-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-100 to-purple-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Building2 className="w-5 h-5 text-purple-600" />
                  </div>
                  <p className="text-3xl font-bold text-purple-600 tracking-tight">{contactStats.withCompany}</p>
                  <p className="text-xs font-medium text-purple-600/70 mt-1">With Company</p>
                </div>
              </div>

              {/* Recently Active */}
              <div 
                className="group relative bg-white rounded-2xl border border-emerald-100 p-5 shadow-sm hover:shadow-xl hover:shadow-emerald-100 hover:border-emerald-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterActivity('recent'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-emerald-50 to-emerald-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-100 to-emerald-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <ActivityIcon className="w-5 h-5 text-emerald-600" />
                  </div>
                  <p className="text-3xl font-bold text-emerald-600 tracking-tight">{contactStats.recentlyActive}</p>
                  <p className="text-xs font-medium text-emerald-600/70 mt-1">Active (30d)</p>
                </div>
              </div>

              {/* Inactive */}
              <div 
                className="group relative bg-white rounded-2xl border border-amber-100 p-5 shadow-sm hover:shadow-xl hover:shadow-amber-100 hover:border-amber-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => { setFilterActivity('inactive'); setShowFilters(true); }}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-amber-50 to-amber-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-100 to-amber-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Clock className="w-5 h-5 text-amber-600" />
                  </div>
                  <p className="text-3xl font-bold text-amber-600 tracking-tight">{contactStats.inactive}</p>
                  <p className="text-xs font-medium text-amber-600/70 mt-1">Need Attention</p>
                </div>
              </div>

              {/* New This Week */}
              <div className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Calendar className="w-5 h-5 text-blue-600" />
                  </div>
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{contactStats.thisWeek}</p>
                  <p className="text-xs font-medium text-blue-600/70 mt-1">This Week</p>
                </div>
              </div>

              {/* With Phone */}
              <div className="group relative bg-white rounded-2xl border border-cyan-100 p-5 shadow-sm hover:shadow-xl hover:shadow-cyan-100 hover:border-cyan-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-cyan-50 to-cyan-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-cyan-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Phone className="w-5 h-5 text-cyan-600" />
                  </div>
                  <p className="text-3xl font-bold text-cyan-600 tracking-tight">{contactStats.withPhone}</p>
                  <p className="text-xs font-medium text-cyan-600/70 mt-1">With Phone</p>
                </div>
              </div>
            </div>
          )}

          {/* Quick Insights Banner */}
          {!loading && contacts.length > 0 && contactStats.inactive > 3 && (
            <div className="mt-4 bg-gradient-to-r from-amber-50 to-orange-50 rounded-xl border border-amber-200 p-4">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-4 h-4 text-amber-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-amber-800">Re-engagement Opportunity</p>
                  <p className="mt-1 text-sm text-amber-700">
                    {contactStats.inactive} contacts haven't had activity in over 90 days. Consider reaching out to maintain relationships.
                  </p>
                </div>
                <Button 
                  variant="outline" 
                  size="sm" 
                  onClick={() => { setFilterActivity('inactive'); setShowFilters(true); }}
                  className="shrink-0 border-amber-300 text-amber-700 hover:bg-amber-100"
                >
                  View Inactive
                </Button>
              </div>
            </div>
          )}

        {/* Search, Filter & Sort Bar - Modern Dark Theme */}
        <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-blue-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-violet-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-blue-500/20 via-cyan-500/20 to-blue-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500/20 to-cyan-500/20 flex items-center justify-center group-focus-within:from-blue-500/40 group-focus-within:to-cyan-500/40 transition-all duration-300">
                  <Search className="w-4 h-4 text-blue-300 group-focus-within:text-blue-200 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search by name, email, phone, or job title..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-blue-400/50 focus:ring-2 focus:ring-blue-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                  aria-label="Search contacts"
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
                    ? 'bg-gradient-to-r from-blue-500/40 to-cyan-500/40 opacity-100' 
                    : 'bg-white/10 opacity-0 group-hover/btn:opacity-50'
                }`} />
                <Button
                  variant="outline"
                  onClick={() => setShowFilters(!showFilters)}
                  className={`relative h-11 px-4 rounded-xl border shadow-xl shadow-black/10 transition-all duration-300 ${
                    showFilters || activeFilterCount > 0
                      ? 'bg-gradient-to-r from-blue-500 to-cyan-500 border-blue-400/50 text-white hover:from-blue-400 hover:to-cyan-400 hover:shadow-blue-500/25'
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
                  <SelectTrigger className="relative h-11 w-[180px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-blue-400/50 focus:ring-2 focus:ring-blue-400/20 transition-all duration-300">
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
                  <SelectItem value="lastActivity-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Recently Active
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-desc">
                    <span className="flex items-center gap-2">
                      <ArrowDown className="w-3.5 h-3.5 text-slate-400" />
                      Newest First
                    </span>
                  </SelectItem>
                  <SelectItem value="createdAt-asc">
                    <span className="flex items-center gap-2">
                      <ArrowUp className="w-3.5 h-3.5 text-slate-400" />
                      Oldest First
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
                {/* Company Filter */}
                <div className="min-w-[180px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <Building2 className="w-3.5 h-3.5" />
                    Company
                  </label>
                  <Select value={filterCompany} onValueChange={setFilterCompany}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
                      <SelectValue placeholder="All companies" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All contacts</SelectItem>
                      <SelectItem value="none">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-slate-300" />
                          No company
                        </span>
                      </SelectItem>
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
                </div>

                {/* Activity Filter */}
                <div className="min-w-[160px]">
                  <label className="flex items-center gap-2 text-xs font-medium text-slate-300 uppercase tracking-wider mb-2">
                    <ActivityIcon className="w-3.5 h-3.5" />
                    Activity
                  </label>
                  <Select value={filterActivity} onValueChange={(v) => setFilterActivity(v as typeof filterActivity)}>
                    <SelectTrigger className="h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
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
                      <SelectItem value="inactive">
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
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-blue-300 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterCompany !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-purple-500/20 text-purple-300 text-xs font-medium border border-purple-400/30">
                  <Building2 className="w-3 h-3" />
                  {filterCompany === 'none' ? 'No company' : companyName(filterCompany)}
                  <button onClick={() => setFilterCompany('all')} className="ml-0.5 hover:text-purple-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterActivity !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-emerald-500/20 text-emerald-300 text-xs font-medium border border-emerald-400/30">
                  <ActivityIcon className="w-3 h-3" />
                  {filterActivity === 'recent' ? 'Recently Active' : 'Inactive'}
                  <button onClick={() => setFilterActivity('all')} className="ml-0.5 hover:text-emerald-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-400">
                {filteredContacts.length} of {contacts.length} contacts
              </span>
            </div>
          )}
        </div>

        {loading ? (
          <ContentSkeleton rows={6} />
        ) : contacts.length === 0 ? (
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
                  <Button onClick={openCreate} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-blue-500 to-cyan-500 hover:from-blue-600 hover:to-cyan-600 shadow-lg shadow-blue-200/50">
                    <Plus className="w-4 h-4" />
                    Add Your First Contact
                  </Button>
                </div>
              </div>
            </div>
          </div>
        ) : filteredContacts.length === 0 ? (
          /* No results state */
          <div className="flex items-center gap-3 p-6 rounded-xl bg-white border border-slate-200">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center">
              <Search className="w-6 h-6 text-slate-400" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-slate-700">No contacts match your filters</p>
              <p className="text-xs text-slate-500 mt-0.5">Try adjusting your search or filter criteria</p>
            </div>
            <Button variant="outline" size="sm" onClick={clearFilters}>
              Clear filters
            </Button>
          </div>
        ) : (
          <>
          {/* Contact Cards Grid */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {filteredContacts.map((contact) => {
              const company = contact.companyId ? companyName(contact.companyId) : null;
              const initials = getInitials(contact.name);
              const gradient = getAvatarGradient(contact.name);
              
              // Check if inactive
              const ninetyDaysAgo = new Date();
              ninetyDaysAgo.setDate(ninetyDaysAgo.getDate() - 90);
              const isInactive = !contact.lastActivityAtUtc || new Date(contact.lastActivityAtUtc) < ninetyDaysAgo;
              
              return (
                <div
                  key={contact.id}
                  className="group relative bg-white rounded-2xl border border-slate-200 shadow-sm hover:shadow-lg hover:border-slate-300 transition-all duration-300 overflow-hidden"
                >
                  {/* Inactive indicator */}
                  {isInactive && (
                    <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-amber-400 to-orange-400" />
                  )}
                  
                  <div className="p-5">
                    {/* Header with avatar and actions */}
                    <div className="flex items-start gap-4 mb-4">
                      <div className={`w-14 h-14 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-white font-bold text-lg shadow-lg group-hover:scale-105 transition-transform duration-300`}>
                        {initials || <User className="w-6 h-6" />}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <h3 
                          className="font-semibold text-slate-900 text-lg truncate group-hover:text-blue-600 transition-colors cursor-pointer"
                          onClick={() => navigate(`/contacts/${contact.id}`)}
                        >
                          {contact.name}
                        </h3>
                        {contact.jobTitle && (
                          <p className="text-sm text-slate-500 truncate flex items-center gap-1.5">
                            <Briefcase className="w-3.5 h-3.5 text-slate-400" />
                            {contact.jobTitle}
                          </p>
                        )}
                        {company && (
                          <p 
                            className="text-sm text-slate-500 truncate flex items-center gap-1.5 mt-0.5 hover:text-violet-600 cursor-pointer transition-colors"
                            onClick={(e) => {
                              e.stopPropagation();
                              if (contact.companyId) navigate(`/companies/${contact.companyId}`);
                            }}
                          >
                            <Building2 className="w-3.5 h-3.5 text-slate-400" />
                            {company}
                          </p>
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
                          <DropdownMenuItem onClick={() => openEdit(contact)}>
                            <Pencil className="w-4 h-4 mr-2" />
                            Edit Contact
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}>
                            <Send className="w-4 h-4 mr-2" />
                            Send Message
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
                                <span className="text-xs text-emerald-600 font-semibold shrink-0 ml-2">{deal.currency || '$'}{deal.value}</span>
                              </button>
                            ))}
                            {contactDeals.length > 3 && (
                              <p className="text-xs text-slate-400 px-2">+{contactDeals.length - 3} more</p>
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
                                <span className={`text-xs font-medium truncate ${task.status === 'completed' ? 'text-slate-400 line-through' : 'text-slate-700'}`}>{task.title}</span>
                              </div>
                            ))}
                            {contactTasks.length > 3 && (
                              <p className="text-xs text-slate-400 px-2">+{contactTasks.length - 3} more</p>
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
                            <span className="text-xs text-slate-400">No activity</span>
                          </>
                        )}
                      </div>
                      
                      <Button 
                        variant="ghost" 
                        size="sm" 
                        onClick={() => navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}
                        className="h-8 text-xs text-blue-600 hover:text-blue-700 hover:bg-blue-50"
                      >
                        <MessageSquare className="w-3.5 h-3.5 mr-1" />
                        Message
                      </Button>
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
                    <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
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
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-600">
                    <Building2 className="w-3.5 h-3.5" />
                  </div>
                  Company
                </Label>
                <Select
                  value={form.companyId || 'none'}
                  onValueChange={(v) => setForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-blue-300 focus:ring-blue-100">
                    <SelectValue placeholder="Select a company" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-400">— No company —</span>
                    </SelectItem>
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
