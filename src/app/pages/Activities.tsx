import { useState, useEffect, useMemo } from 'react';
import { 
  Activity as ActivityIcon, Plus, Phone, Mail, MessageSquare, FileText, Trash2,
  Search, X, Calendar, Clock, User, Target, BarChart3, TrendingUp, Video,
  Presentation, CheckCircle, Users, Filter, Sparkles,
  ChevronDown, CalendarDays, Zap, AlertCircle
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getActivitiesPaged, createActivity, deleteActivity, getContacts, getDeals, getActivitiesByContact, getActivitiesByDeal, messages } from '@/app/api';
import type { Activity, Contact, Deal, PagedResult } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
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
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';

const ACTIVITY_TYPES = [
  { id: 'call', label: 'Call', icon: Phone, color: 'emerald', bgColor: 'bg-emerald-100', textColor: 'text-emerald-600', borderColor: 'border-emerald-200' },
  { id: 'meeting', label: 'Meeting', icon: Users, color: 'blue', bgColor: 'bg-blue-100', textColor: 'text-blue-600', borderColor: 'border-blue-200' },
  { id: 'email', label: 'Email', icon: Mail, color: 'amber', bgColor: 'bg-amber-100', textColor: 'text-amber-600', borderColor: 'border-amber-200' },
  { id: 'note', label: 'Note', icon: FileText, color: 'slate', bgColor: 'bg-slate-100', textColor: 'text-slate-600', borderColor: 'border-slate-200' },
  { id: 'video', label: 'Video Call', icon: Video, color: 'purple', bgColor: 'bg-purple-100', textColor: 'text-purple-600', borderColor: 'border-purple-200' },
  { id: 'demo', label: 'Demo', icon: Presentation, color: 'rose', bgColor: 'bg-rose-100', textColor: 'text-rose-600', borderColor: 'border-rose-200' },
  { id: 'task', label: 'Task Completed', icon: CheckCircle, color: 'cyan', bgColor: 'bg-cyan-100', textColor: 'text-cyan-600', borderColor: 'border-cyan-200' },
] as const;

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
}

function formatRelativeDate(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function getDateGroup(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const yesterday = new Date(today.getTime() - 86400000);
  const thisWeekStart = new Date(today.getTime() - today.getDay() * 86400000);
  const lastWeekStart = new Date(thisWeekStart.getTime() - 7 * 86400000);
  
  if (d >= today) return 'Today';
  if (d >= yesterday) return 'Yesterday';
  if (d >= thisWeekStart) return 'This Week';
  if (d >= lastWeekStart) return 'Last Week';
  return d.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
}

type ActivityFilter = 'all' | 'contact' | 'deal';

export default function Activities() {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<ActivityFilter>('all');
  const [filterContactId, setFilterContactId] = useState('');
  const [filterDealId, setFilterDealId] = useState('');
  const [filterType, setFilterType] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    type: 'note',
    subject: '',
    body: '',
    contactId: '',
    dealId: '',
  });
  const [saving, setSaving] = useState(false);
  const [deleteConfirmActivity, setDeleteConfirmActivity] = useState<Activity | null>(null);
  const [deleting, setDeleting] = useState(false);
  
  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [pagedResult, setPagedResult] = useState<PagedResult<Activity> | null>(null);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
      setPage(1); // Reset to first page on search
    }, 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Reset page when filter type changes
  useEffect(() => {
    setPage(1);
  }, [filterType]);

  const loadData = () => {
    setLoading(true);
    if (filter === 'contact' && filterContactId) {
      getActivitiesByContact(filterContactId)
        .then(data => {
          setActivities(data);
          setPagedResult(null);
        })
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    } else if (filter === 'deal' && filterDealId) {
      getActivitiesByDeal(filterDealId)
        .then(data => {
          setActivities(data);
          setPagedResult(null);
        })
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    } else {
      getActivitiesPaged({ 
        page, 
        pageSize, 
        search: debouncedSearch || undefined,
        type: filterType !== 'all' ? filterType : undefined
      })
        .then(result => {
          setActivities(result.items);
          setPagedResult(result);
        })
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    }
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    
    if (filter === 'contact' && filterContactId) {
      getActivitiesByContact(filterContactId)
        .then((data) => { 
          if (!cancelled) {
            setActivities(data);
            setPagedResult(null);
          }
        })
        .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
        .finally(() => { if (!cancelled) setLoading(false); });
    } else if (filter === 'deal' && filterDealId) {
      getActivitiesByDeal(filterDealId)
        .then((data) => { 
          if (!cancelled) {
            setActivities(data);
            setPagedResult(null);
          }
        })
        .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
        .finally(() => { if (!cancelled) setLoading(false); });
    } else {
      getActivitiesPaged({ 
        page, 
        pageSize, 
        search: debouncedSearch || undefined,
        type: filterType !== 'all' ? filterType : undefined
      })
        .then((result) => { 
          if (!cancelled) {
            setActivities(result.items);
            setPagedResult(result);
          }
        })
        .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
        .finally(() => { if (!cancelled) setLoading(false); });
    }
    
    return () => { cancelled = true; };
  }, [filter, filterContactId, filterDealId, page, pageSize, debouncedSearch, filterType]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([getContacts(), getDeals()]).then(([c, d]) => {
      if (!cancelled) {
        setContacts(c);
        setDeals(d);
      }
    });
    return () => { cancelled = true; };
  }, []);

  // Calculate activity statistics
  const activityStats = useMemo(() => {
    const total = activities.length;
    
    // Count by type
    const byType: Record<string, number> = {};
    activities.forEach(a => {
      byType[a.type] = (byType[a.type] || 0) + 1;
    });
    
    // Activities today
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const todayCount = activities.filter(a => new Date(a.createdAt) >= today).length;
    
    // Activities this week
    const oneWeekAgo = new Date();
    oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
    const thisWeek = activities.filter(a => new Date(a.createdAt) >= oneWeekAgo).length;
    
    // With contacts
    const withContacts = activities.filter(a => a.contactId).length;
    
    // With deals
    const withDeals = activities.filter(a => a.dealId).length;
    
    // Most common type
    const topType = Object.entries(byType).sort((a, b) => b[1] - a[1])[0];
    
    return { total, byType, todayCount, thisWeek, withContacts, withDeals, topType };
  }, [activities]);

  // For contact/deal filter views, apply client-side filtering
  // For 'all' view, filtering is done server-side
  const filteredActivities = useMemo(() => {
    // If using paginated view (filter === 'all'), activities are already filtered server-side
    if (filter === 'all') {
      return activities;
    }
    
    // For contact/deal views, apply client-side filtering
    let result = [...activities];
    
    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(a => 
        (a.subject && a.subject.toLowerCase().includes(q)) ||
        (a.body && a.body.toLowerCase().includes(q)) ||
        a.type.toLowerCase().includes(q)
      );
    }
    
    // Type filter
    if (filterType !== 'all') {
      result = result.filter(a => a.type === filterType);
    }
    
    return result;
  }, [activities, searchQuery, filterType, filter]);

  // Group activities by date
  const groupedActivities = useMemo(() => {
    const groups: Record<string, Activity[]> = {};
    filteredActivities.forEach(activity => {
      const group = getDateGroup(activity.createdAt);
      if (!groups[group]) groups[group] = [];
      groups[group].push(activity);
    });
    return groups;
  }, [filteredActivities]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.subject.trim() && !form.body.trim()) {
      toast.error(messages.validation.subjectOrBodyRequired);
      return;
    }
    setSaving(true);
    try {
      const created = await createActivity({
        type: form.type,
        subject: form.subject.trim() || undefined,
        body: form.body.trim() || undefined,
        contactId: form.contactId || undefined,
        dealId: form.dealId || undefined,
      });
      if (created) {
        setActivities((prev) => [created, ...prev]);
        toast.success(messages.success.activityLogged);
        setDialogOpen(false);
        setForm({ type: 'note', subject: '', body: '', contactId: '', dealId: '' });
        if (filter !== 'all') loadData();
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  const typeInfo = (type: string) => ACTIVITY_TYPES.find((t) => t.id === type) ?? { 
    id: type, label: type, icon: FileText, color: 'slate', 
    bgColor: 'bg-slate-100', textColor: 'text-slate-600', borderColor: 'border-slate-200' 
  };

  const handleDelete = async () => {
    if (!deleteConfirmActivity) return;
    setDeleting(true);
    try {
      const ok = await deleteActivity(deleteConfirmActivity.id);
      if (ok) {
        setActivities((prev) => prev.filter((a) => a.id !== deleteConfirmActivity.id));
        toast.success(messages.success.activityDeleted);
        setDeleteConfirmActivity(null);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  // Quick log activity with preset type
  const quickLogActivity = (type: string) => {
    setForm({ type, subject: '', body: '', contactId: '', dealId: '' });
    setDialogOpen(true);
  };

  // Count active filters
  const activeFilterCount = [
    filterType !== 'all',
    filter !== 'all',
  ].filter(Boolean).length;

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
                  <ActivityIcon className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Activities</h1>
                  <p className="text-slate-400 mt-1">
                    {loading ? 'Loading…' : 
                      filter === 'contact' && filterContactId
                        ? `Timeline for ${contacts.find((c) => c.id === filterContactId)?.name ?? 'contact'}`
                        : filter === 'deal' && filterDealId
                          ? `Timeline for ${deals.find((d) => d.id === filterDealId)?.name ?? 'deal'}`
                          : 'Track all your sales interactions'}
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                {/* Quick Log Dropdown */}
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="outline" className="gap-2 h-10 rounded-xl border-white/20 bg-white/10 text-white hover:bg-white/20 hover:border-white/30">
                      <Zap className="w-4 h-4" />
                      Quick Log
                      <ChevronDown className="w-4 h-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="w-48">
                    {ACTIVITY_TYPES.slice(0, 4).map((type) => (
                      <DropdownMenuItem key={type.id} onClick={() => quickLogActivity(type.id)}>
                        <type.icon className={`w-4 h-4 mr-2 ${type.textColor}`} />
                        Log {type.label}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuContent>
                </DropdownMenu>
                
                <Button onClick={() => setDialogOpen(true)} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold text-white">
                  <Plus className="w-4 h-4" />
                  Log Activity
                </Button>
              </div>
            </div>
          </div>
        </div>

          {/* Stats Cards */}
          {!loading && activities.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
              {/* Total Activities */}
              <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <BarChart3 className="w-5 h-5 text-slate-600" />
                  </div>
                  <p className="text-3xl font-bold text-slate-900 tracking-tight">{activityStats.total}</p>
                  <p className="text-xs font-medium text-slate-500 mt-1">Total Activities</p>
                </div>
              </div>

              {/* Today */}
              <div className="group relative bg-white rounded-2xl border border-indigo-100 p-5 shadow-sm hover:shadow-xl hover:shadow-indigo-100 hover:border-indigo-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-indigo-50 to-indigo-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-100 to-indigo-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <CalendarDays className="w-5 h-5 text-indigo-600" />
                  </div>
                  <p className="text-3xl font-bold text-indigo-600 tracking-tight">{activityStats.todayCount}</p>
                  <p className="text-xs font-medium text-indigo-600/70 mt-1">Today</p>
                </div>
              </div>

              {/* This Week */}
              <div className="group relative bg-white rounded-2xl border border-purple-100 p-5 shadow-sm hover:shadow-xl hover:shadow-purple-100 hover:border-purple-200 transition-all duration-300 overflow-hidden">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-purple-50 to-purple-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-100 to-purple-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <TrendingUp className="w-5 h-5 text-purple-600" />
                  </div>
                  <p className="text-3xl font-bold text-purple-600 tracking-tight">{activityStats.thisWeek}</p>
                  <p className="text-xs font-medium text-purple-600/70 mt-1">This Week</p>
                </div>
              </div>

              {/* Calls */}
              <div 
                className="group relative bg-white rounded-2xl border border-emerald-100 p-5 shadow-sm hover:shadow-xl hover:shadow-emerald-100 hover:border-emerald-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => setFilterType('call')}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-emerald-50 to-emerald-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-100 to-emerald-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Phone className="w-5 h-5 text-emerald-600" />
                  </div>
                  <p className="text-3xl font-bold text-emerald-600 tracking-tight">{activityStats.byType['call'] || 0}</p>
                  <p className="text-xs font-medium text-emerald-600/70 mt-1">Calls</p>
                </div>
              </div>

              {/* Meetings */}
              <div 
                className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => setFilterType('meeting')}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Users className="w-5 h-5 text-blue-600" />
                  </div>
                  <p className="text-3xl font-bold text-blue-600 tracking-tight">{activityStats.byType['meeting'] || 0}</p>
                  <p className="text-xs font-medium text-blue-600/70 mt-1">Meetings</p>
                </div>
              </div>

              {/* Emails */}
              <div 
                className="group relative bg-white rounded-2xl border border-amber-100 p-5 shadow-sm hover:shadow-xl hover:shadow-amber-100 hover:border-amber-200 transition-all duration-300 overflow-hidden cursor-pointer"
                onClick={() => setFilterType('email')}
              >
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-amber-50 to-amber-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                <div className="relative">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-100 to-amber-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                    <Mail className="w-5 h-5 text-amber-600" />
                  </div>
                  <p className="text-3xl font-bold text-amber-600 tracking-tight">{activityStats.byType['email'] || 0}</p>
                  <p className="text-xs font-medium text-amber-600/70 mt-1">Emails</p>
                </div>
              </div>
            </div>
          )}

          {/* Quick Insights */}
          {!loading && activities.length > 0 && activityStats.todayCount === 0 && (
            <div className="mt-4 bg-gradient-to-r from-indigo-50 to-purple-50 rounded-xl border border-indigo-200 p-4">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-lg bg-indigo-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-4 h-4 text-indigo-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-indigo-800">No activities logged today</p>
                  <p className="mt-1 text-sm text-indigo-700">
                    Consistent activity logging helps track your sales progress. Log a call, meeting, or note to keep your CRM up to date.
                  </p>
                </div>
                <Button 
                  onClick={() => setDialogOpen(true)}
                  size="sm"
                  className="shrink-0 bg-indigo-600 hover:bg-indigo-700"
                >
                  Log Now
                </Button>
              </div>
            </div>
          )}

        {/* Search and Filters - Modern Dark Theme */}
        <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-indigo-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-purple-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-indigo-500/20 via-purple-500/20 to-indigo-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-indigo-500/20 to-purple-500/20 flex items-center justify-center group-focus-within:from-indigo-500/40 group-focus-within:to-purple-500/40 transition-all duration-300">
                  <Search className="w-4 h-4 text-indigo-300 group-focus-within:text-indigo-200 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search activities..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-indigo-400/50 focus:ring-2 focus:ring-indigo-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                  aria-label="Search activities"
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

            {/* Type Filter - Enhanced */}
            <div className="relative group/filter">
              <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-50 transition-all duration-300" />
              <Select value={filterType} onValueChange={setFilterType}>
                <SelectTrigger className="relative h-11 w-[160px] rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-indigo-400/50 focus:ring-2 focus:ring-indigo-400/20 transition-all duration-300">
                  <div className="flex items-center gap-2">
                    <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                      <Filter className="w-3.5 h-3.5 text-slate-300" />
                    </div>
                    <SelectValue placeholder="All types" />
                  </div>
                </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {ACTIVITY_TYPES.map((type) => (
                  <SelectItem key={type.id} value={type.id}>
                    <span className="flex items-center gap-2">
                      <type.icon className={`w-4 h-4 ${type.textColor}`} />
                      {type.label}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            </div>
          </div>

          {/* View Filter Tabs - Enhanced */}
          <div className="relative mt-3 flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium text-slate-300">View:</span>
            <div className="relative">
              <div className="absolute inset-0 bg-white/5 rounded-xl blur-lg" />
              <div className="relative flex gap-1 bg-white/5 backdrop-blur-md rounded-xl p-1 border border-white/10 shadow-xl shadow-black/10">
                {(['all', 'contact', 'deal'] as const).map((f) => (
                  <button
                    key={f}
                    onClick={() => {
                      setFilter(f);
                      if (f !== 'contact') setFilterContactId('');
                      if (f !== 'deal') setFilterDealId('');
                    }}
                    className={`px-4 py-2 text-sm font-medium rounded-lg transition-all duration-300 ${
                      filter === f 
                        ? 'bg-gradient-to-r from-indigo-500 to-purple-500 text-white shadow-lg shadow-indigo-500/30' 
                        : 'text-slate-300 hover:bg-white/10 hover:text-white'
                    }`}
                  >
                    {f === 'all' ? 'All Activities' : f === 'contact' ? 'By Contact' : 'By Deal'}
                  </button>
                ))}
              </div>
            </div>
            
            {filter === 'contact' && (
              <div className="relative group/contact">
                <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/contact:opacity-50 transition-all duration-300" />
                <Select value={filterContactId || 'none'} onValueChange={(v) => setFilterContactId(v === 'none' ? '' : v)}>
                  <SelectTrigger className="relative w-[200px] h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl hover:bg-white/10 hover:border-white/20 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                        <User className="w-3.5 h-3.5 text-slate-300" />
                      </div>
                      <SelectValue placeholder="Select contact" />
                    </div>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— Select contact —</SelectItem>
                    {contacts.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            
            {filter === 'deal' && (
              <div className="relative group/deal">
                <div className="absolute inset-0 bg-white/10 rounded-xl blur-lg opacity-0 group-hover/deal:opacity-50 transition-all duration-300" />
                <Select value={filterDealId || 'none'} onValueChange={(v) => setFilterDealId(v === 'none' ? '' : v)}>
                  <SelectTrigger className="relative w-[200px] h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl hover:bg-white/10 hover:border-white/20 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-white/10 flex items-center justify-center">
                        <Target className="w-3.5 h-3.5 text-slate-300" />
                      </div>
                      <SelectValue placeholder="Select deal" />
                    </div>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— Select deal —</SelectItem>
                    {deals.map((d) => (
                      <SelectItem key={d.id} value={d.id}>
                        {d.name} · {d.value}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          {/* Active Filter Pills */}
          {(activeFilterCount > 0 || searchQuery) && (
            <div className="relative mt-4 flex flex-wrap items-center gap-2">
              <span className="text-xs text-slate-400">Showing:</span>
              {searchQuery && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-white/10 text-white text-xs font-medium border border-white/10">
                  <Search className="w-3 h-3" />
                  &quot;{searchQuery}&quot;
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-indigo-300 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterType !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-indigo-500/20 text-indigo-300 text-xs font-medium border border-indigo-400/30">
                  <ActivityIcon className="w-3 h-3" />
                  {typeInfo(filterType).label}
                  <button onClick={() => setFilterType('all')} className="ml-0.5 hover:text-indigo-100 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-400">
                {pagedResult 
                  ? `${filteredActivities.length} of ${pagedResult.totalCount} activities`
                  : `${filteredActivities.length} activities`
                }
              </span>
            </div>
          )}
        </div>

        {loading ? (
          <ContentSkeleton rows={6} />
        ) : activities.length === 0 ? (
          /* Enhanced Empty State */
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-indigo-500 via-purple-500 to-indigo-400 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <ActivityIcon className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Start Your Activity Timeline</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Log calls, meetings, emails, and notes to build a complete history of your customer interactions.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-4 gap-4 mb-6">
                  {ACTIVITY_TYPES.slice(0, 4).map((type) => (
                    <button
                      key={type.id}
                      onClick={() => quickLogActivity(type.id)}
                      className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100 hover:border-slate-200 hover:bg-slate-100 transition-all group"
                    >
                      <div className={`w-10 h-10 rounded-lg ${type.bgColor} flex items-center justify-center mx-auto mb-2 group-hover:scale-110 transition-transform`}>
                        <type.icon className={`w-5 h-5 ${type.textColor}`} />
                      </div>
                      <p className="text-sm font-medium text-slate-800">{type.label}</p>
                    </button>
                  ))}
                </div>
                
                <div className="flex justify-center">
                  <Button onClick={() => setDialogOpen(true)} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-indigo-500 to-purple-500 hover:from-indigo-600 hover:to-purple-600 shadow-lg shadow-indigo-200/50">
                    <Plus className="w-4 h-4" />
                    Log Your First Activity
                  </Button>
                </div>
              </div>
            </div>
          </div>
        ) : filteredActivities.length === 0 ? (
          /* No results state */
          <div className="flex items-center gap-3 p-6 rounded-xl bg-white border border-slate-200">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center">
              <Search className="w-6 h-6 text-slate-400" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-slate-700">No activities match your filters</p>
              <p className="text-xs text-slate-500 mt-0.5">Try adjusting your search or filter criteria</p>
            </div>
            <Button variant="outline" size="sm" onClick={() => { setSearchQuery(''); setFilterType('all'); }}>
              Clear filters
            </Button>
          </div>
        ) : (
          /* Timeline View */
          <div className="space-y-6">
            {Object.entries(groupedActivities).map(([dateGroup, groupActivities]) => (
              <div key={dateGroup}>
                {/* Date Group Header */}
                <div className="flex items-center gap-3 mb-4">
                  <div className="flex items-center gap-2 px-3 py-1.5 rounded-full bg-slate-100 border border-slate-200">
                    <Calendar className="w-4 h-4 text-slate-500" />
                    <span className="text-sm font-semibold text-slate-700">{dateGroup}</span>
                  </div>
                  <div className="flex-1 h-px bg-slate-200" />
                  <span className="text-xs text-slate-400">{groupActivities.length} activities</span>
                </div>

                {/* Activities List */}
                <div className="relative pl-6 space-y-4">
                  {/* Timeline line */}
                  <div className="absolute left-[11px] top-6 bottom-6 w-0.5 bg-gradient-to-b from-slate-200 via-slate-200 to-transparent" />

                  {groupActivities.map((activity, _) => {
                    const { icon: Icon, label, bgColor, textColor, borderColor } = typeInfo(activity.type);
                    const contact = activity.contactId ? contacts.find((c) => c.id === activity.contactId) : null;
                    const deal = activity.dealId ? deals.find((d) => d.id === activity.dealId) : null;

                    return (
                      <div key={activity.id} className="relative group">
                        {/* Timeline dot */}
                        <div className={`absolute -left-6 top-5 w-[22px] h-[22px] rounded-full ${bgColor} border-2 border-white shadow-sm flex items-center justify-center z-10`}>
                          <Icon className={`w-3 h-3 ${textColor}`} />
                        </div>

                        {/* Activity Card */}
                        <div className={`bg-white rounded-xl border ${borderColor} shadow-sm hover:shadow-md transition-all overflow-hidden`}>
                          <div className="p-4">
                            <div className="flex items-start justify-between gap-3">
                              <div className="flex-1 min-w-0">
                                {/* Header */}
                                <div className="flex items-center gap-2 mb-1">
                                  <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-md ${bgColor} ${textColor}`}>
                                    <Icon className="w-3 h-3" />
                                    {label}
                                  </span>
                                  <span className="text-xs text-slate-400 flex items-center gap-1">
                                    <Clock className="w-3 h-3" />
                                    {formatRelativeDate(activity.createdAt)}
                                  </span>
                                </div>

                                {/* Subject */}
                                {activity.subject && (
                                  <h3 className="font-semibold text-slate-900 text-base">
                                    {activity.subject}
                                  </h3>
                                )}

                                {/* Body */}
                                {activity.body && (
                                  <p className="text-sm text-slate-600 mt-1 line-clamp-2">
                                    {activity.body}
                                  </p>
                                )}

                                {/* Links */}
                                {(contact || deal) && (
                                  <div className="flex flex-wrap items-center gap-2 mt-3">
                                    {contact && (
                                      <span className="inline-flex items-center gap-1.5 text-xs px-2 py-1 rounded-md bg-blue-50 text-blue-700 border border-blue-100">
                                        <User className="w-3 h-3" />
                                        {contact.name}
                                      </span>
                                    )}
                                    {deal && (
                                      <span className="inline-flex items-center gap-1.5 text-xs px-2 py-1 rounded-md bg-emerald-50 text-emerald-700 border border-emerald-100">
                                        <Target className="w-3 h-3" />
                                        {deal.name}
                                      </span>
                                    )}
                                  </div>
                                )}
                              </div>

                              {/* Delete Button */}
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setDeleteConfirmActivity(activity)}
                                className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity text-slate-400 hover:text-red-600 hover:bg-red-50"
                                aria-label={`Delete activity ${activity.subject || label}`}
                              >
                                <Trash2 className="w-4 h-4" />
                              </Button>
                            </div>
                          </div>

                          {/* Footer with timestamp */}
                          <div className="px-4 py-2 bg-slate-50/50 border-t border-slate-100">
                            <p className="text-xs text-slate-400">
                              {formatDate(activity.createdAt)}
                            </p>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Pagination Controls */}
        {filter === 'all' && pagedResult && pagedResult.totalPages > 1 && (
          <div className="mt-6 flex items-center justify-between bg-white rounded-xl border border-slate-200 p-4">
            <div className="text-sm text-slate-500">
              Showing {(page - 1) * pageSize + 1} to {Math.min(page * pageSize, pagedResult.totalCount)} of {pagedResult.totalCount} activities
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={!pagedResult.hasPreviousPage || loading}
              >
                Previous
              </Button>
              <div className="flex items-center gap-1">
                {Array.from({ length: Math.min(5, pagedResult.totalPages) }, (_, i) => {
                  let pageNum: number;
                  if (pagedResult.totalPages <= 5) {
                    pageNum = i + 1;
                  } else if (page <= 3) {
                    pageNum = i + 1;
                  } else if (page >= pagedResult.totalPages - 2) {
                    pageNum = pagedResult.totalPages - 4 + i;
                  } else {
                    pageNum = page - 2 + i;
                  }
                  return (
                    <Button
                      key={pageNum}
                      variant={page === pageNum ? 'default' : 'outline'}
                      size="sm"
                      onClick={() => setPage(pageNum)}
                      disabled={loading}
                      className="w-9"
                    >
                      {pageNum}
                    </Button>
                  );
                })}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.min(pagedResult.totalPages, p + 1))}
                disabled={!pagedResult.hasNextPage || loading}
              >
                Next
              </Button>
            </div>
          </div>
        )}
        </main>
      </PageTransition>

      {/* Enhanced Log Activity Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-[520px] p-0 gap-0 overflow-hidden">
          {/* Gradient Header */}
          <div className="relative overflow-hidden">
            <div className="absolute inset-0 bg-gradient-to-br from-indigo-500 via-purple-500 to-indigo-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <ActivityIcon className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Log Activity
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Record a call, meeting, email, or note
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="p-6 space-y-4">
            {/* Activity Type Selection */}
            <div>
              <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-3">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-indigo-100 text-indigo-600">
                  <ActivityIcon className="w-3.5 h-3.5" />
                </div>
                Activity Type
              </Label>
              <div className="grid grid-cols-4 gap-2">
                {ACTIVITY_TYPES.slice(0, 4).map((type) => {
                  const isSelected = form.type === type.id;
                  return (
                    <button
                      key={type.id}
                      type="button"
                      onClick={() => setForm((f) => ({ ...f, type: type.id }))}
                      className={`flex flex-col items-center gap-2 p-3 rounded-xl border-2 transition-all ${
                        isSelected
                          ? `${type.borderColor} ${type.bgColor} shadow-sm`
                          : 'border-slate-100 bg-slate-50/50 hover:border-slate-200 hover:bg-slate-100/50'
                      }`}
                    >
                      <div className={`w-8 h-8 rounded-lg ${type.bgColor} flex items-center justify-center`}>
                        <type.icon className={`w-4 h-4 ${type.textColor}`} />
                      </div>
                      <span className={`text-xs font-medium ${isSelected ? type.textColor : 'text-slate-600'}`}>
                        {type.label}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Subject Field */}
            <div className="group">
              <Label htmlFor="activity-subject" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-purple-100 text-purple-600">
                  <FileText className="w-3.5 h-3.5" />
                </div>
                Subject
              </Label>
              <Input
                id="activity-subject"
                value={form.subject}
                onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
                placeholder="e.g. Discovery call – discussed pricing"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-indigo-300 focus:ring-indigo-100 transition-all"
              />
            </div>

            {/* Details Field */}
            <div className="group">
              <Label htmlFor="activity-body" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-100 text-blue-600">
                  <MessageSquare className="w-3.5 h-3.5" />
                </div>
                Details
              </Label>
              <Textarea
                id="activity-body"
                value={form.body}
                onChange={(e) => setForm((f) => ({ ...f, body: e.target.value }))}
                placeholder="Notes or summary of the interaction..."
                rows={3}
                className="bg-slate-50/50 border-slate-200 focus:bg-white focus:border-indigo-300 focus:ring-indigo-100 transition-all resize-none"
              />
            </div>

            {/* Link to Contact & Deal */}
            <div className="grid grid-cols-2 gap-4">
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-cyan-100 text-cyan-600">
                    <User className="w-3.5 h-3.5" />
                  </div>
                  Contact
                </Label>
                <Select
                  value={form.contactId || 'none'}
                  onValueChange={(v) => setForm((f) => ({ ...f, contactId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-indigo-300 focus:ring-indigo-100">
                    <SelectValue placeholder="Link to contact" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-400">— None —</span>
                    </SelectItem>
                    {contacts.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                    <Target className="w-3.5 h-3.5" />
                  </div>
                  Deal
                </Label>
                <Select
                  value={form.dealId || 'none'}
                  onValueChange={(v) => setForm((f) => ({ ...f, dealId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-indigo-300 focus:ring-indigo-100">
                    <SelectValue placeholder="Link to deal" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-400">— None —</span>
                    </SelectItem>
                    {deals.map((d) => (
                      <SelectItem key={d.id} value={d.id}>
                        {d.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)} className="h-11 px-6">
                Cancel
              </Button>
              <Button 
                type="submit" 
                disabled={saving} 
                className="h-11 px-6 bg-gradient-to-r from-indigo-500 to-purple-500 hover:from-indigo-600 hover:to-purple-600 shadow-lg shadow-indigo-200/50"
              >
                {saving ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Logging...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    Log Activity
                    <Sparkles className="w-4 h-4" />
                  </span>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <AlertDialog open={!!deleteConfirmActivity} onOpenChange={(open) => !open && setDeleteConfirmActivity(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete activity?</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove this activity. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} disabled={deleting} className="bg-red-600 hover:bg-red-700">
              {deleting ? 'Deleting...' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
