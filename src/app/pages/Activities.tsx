import { useState, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Activity as ActivityIcon, Plus, Phone, Mail, MessageSquare, FileText, Trash2,
  Search, X, Calendar, Clock, User, Target, BarChart3, TrendingUp, Video,
  Presentation, CheckCircle, Users, Filter, Sparkles, Pencil, UserCheck,
  ChevronDown, CalendarDays, CalendarClock, Zap, AlertCircle, Download, FileText as FileTextIcon, FileSpreadsheet, Loader2
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getActivities, getActivitiesPaged, createActivity, updateActivity, deleteActivity, getContacts, getDeals, getLeads, getActivitiesByContact, getActivitiesByDeal, getActivitiesByLead, messages } from '@/app/api';
import type { Activity, Contact, Deal, Lead, PagedResult } from '@/app/api/types';
import { downloadCsv } from '@/app/lib/exportCsv';
import { activityCsvColumns, printActivitiesPdf, activityTypeLabel } from '@/app/lib/exportActivities';
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
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary, type PageStat } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

const ACTIVITY_TYPES = [
  { id: 'call', label: 'Call', icon: Phone, color: 'emerald', bgColor: 'bg-emerald-100', textColor: 'text-emerald-600', borderColor: 'border-emerald-200' },
  { id: 'meeting', label: 'Meeting', icon: Users, color: 'blue', bgColor: 'bg-blue-100', textColor: 'text-blue-600', borderColor: 'border-blue-200' },
  { id: 'email', label: 'Email', icon: Mail, color: 'amber', bgColor: 'bg-amber-100', textColor: 'text-amber-600', borderColor: 'border-amber-200' },
  { id: 'note', label: 'Note', icon: FileText, color: 'slate', bgColor: 'bg-slate-100', textColor: 'text-slate-600', borderColor: 'border-slate-200' },
  { id: 'video', label: 'Video Call', icon: Video, color: 'purple', bgColor: 'bg-purple-100', textColor: 'text-purple-600', borderColor: 'border-purple-200' },
  { id: 'demo', label: 'Demo', icon: Presentation, color: 'rose', bgColor: 'bg-rose-100', textColor: 'text-rose-600', borderColor: 'border-rose-200' },
  { id: 'task', label: 'Task Completed', icon: CheckCircle, color: 'cyan', bgColor: 'bg-cyan-100', textColor: 'text-cyan-600', borderColor: 'border-cyan-200' },
  { id: 'follow_up', label: 'Follow-up', icon: CalendarClock, color: 'orange', bgColor: 'bg-orange-100', textColor: 'text-orange-600', borderColor: 'border-orange-200' },
  { id: 'deadline', label: 'Deadline', icon: Clock, color: 'red', bgColor: 'bg-red-100', textColor: 'text-red-600', borderColor: 'border-red-200' },
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

type ActivityFilter = 'all' | 'contact' | 'deal' | 'lead';

export default function Activities() {
  const navigate = useNavigate();
  const [activities, setActivities] = useState<Activity[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [filter, setFilter] = useState<ActivityFilter>('all');
  const [filterContactId, setFilterContactId] = useState('');
  const [filterDealId, setFilterDealId] = useState('');
  const [filterLeadId, setFilterLeadId] = useState('');
  const [filterType, setFilterType] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingActivity, setEditingActivity] = useState<Activity | null>(null);
  const [form, setForm] = useState({
    type: 'note',
    subject: '',
    body: '',
    contactId: '',
    dealId: '',
    participants: '',
  });
  const [saving, setSaving] = useState(false);
  const [deleteConfirmActivity, setDeleteConfirmActivity] = useState<Activity | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [exporting, setExporting] = useState(false);
  
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
    } else if (filter === 'lead' && filterLeadId) {
      getActivitiesByLead(filterLeadId)
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
    } else if (filter === 'lead' && filterLeadId) {
      getActivitiesByLead(filterLeadId)
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
  }, [filter, filterContactId, filterDealId, filterLeadId, page, pageSize, debouncedSearch, filterType]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([getContacts(), getDeals(), getLeads()]).then(([c, d, l]) => {
      if (!cancelled) {
        setContacts(c);
        setDeals(d);
        setLeads(l);
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
      // HP-2: Handle edit vs create
      if (editingActivity) {
        const updated = await updateActivity(editingActivity.id, {
          type: form.type,
          subject: form.subject.trim() || undefined,
          body: form.body.trim() || undefined,
          participants: form.participants.trim() || undefined,
        });
        if (updated) {
          setActivities((prev) => prev.map((a) => a.id === updated.id ? updated : a));
          toast.success('Activity updated successfully');
          setDialogOpen(false);
          setEditingActivity(null);
          setForm({ type: 'note', subject: '', body: '', contactId: '', dealId: '', participants: '' });
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createActivity({
          type: form.type,
          subject: form.subject.trim() || undefined,
          body: form.body.trim() || undefined,
          contactId: form.contactId || undefined,
          dealId: form.dealId || undefined,
          leadId: (filter === 'lead' && filterLeadId) ? filterLeadId : undefined,
          participants: form.participants.trim() || undefined,
        });
        if (created) {
          setActivities((prev) => [created, ...prev]);
          toast.success(messages.success.activityLogged);
          setDialogOpen(false);
          setForm({ type: 'note', subject: '', body: '', contactId: '', dealId: '', participants: '' });
          if (filter !== 'all') loadData();
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

  // HP-2: Open edit dialog pre-filled with activity data
  const handleEditActivity = (activity: Activity) => {
    setEditingActivity(activity);
    setForm({
      type: activity.type,
      subject: activity.subject || '',
      body: activity.body || '',
      contactId: activity.contactId || '',
      dealId: activity.dealId || '',
      participants: activity.participants || '',
    });
    setDialogOpen(true);
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

  // Export the full activity history (all pages), honoring the current filters.
  const handleExport = async (format: 'pdf' | 'csv') => {
    setExporting(true);
    try {
      let rows = await getActivities();
      if (!Array.isArray(rows)) rows = [];
      if (filter === 'contact' && filterContactId) rows = rows.filter((a) => a.contactId === filterContactId);
      else if (filter === 'deal' && filterDealId) rows = rows.filter((a) => a.dealId === filterDealId);
      else if (filter === 'lead' && filterLeadId) rows = rows.filter((a) => a.leadId === filterLeadId);
      if (filterType !== 'all') rows = rows.filter((a) => a.type === filterType);
      const q = debouncedSearch.trim().toLowerCase();
      if (q) {
        rows = rows.filter((a) =>
          [a.subject, a.body, a.contactName, a.dealName, a.leadName]
            .some((v) => (v || '').toLowerCase().includes(q)));
      }
      rows.sort((x, y) => Date.parse(y.createdAt) - Date.parse(x.createdAt));
      if (rows.length === 0) { toast.error('No activities to export'); return; }

      if (format === 'csv') {
        downloadCsv('activity-history', rows, activityCsvColumns);
        toast.success(`Exported ${rows.length} ${rows.length === 1 ? 'activity' : 'activities'}`);
      } else {
        const subtitle = filterType !== 'all' ? `Type: ${activityTypeLabel(filterType)}` : 'All activity types';
        const ok = printActivitiesPdf(rows, { subtitle });
        if (!ok) toast.error('Allow pop-ups for this site to export as PDF');
      }
    } catch {
      toast.error('Export failed');
    } finally {
      setExporting(false);
    }
  };

  // Quick log activity with preset type
  const quickLogActivity = (type: string) => {
    setEditingActivity(null);
    setForm({ type, subject: '', body: '', contactId: '', dealId: '', participants: '' });
    setDialogOpen(true);
  };

  // Count active filters
  const activeFilterCount = [
    filterType !== 'all',
    filter !== 'all',
    filter === 'lead' && filterLeadId !== '',
  ].filter(Boolean).length;

  // The page's numbers, in its header. Calls, Meetings and Emails set the type
  // filter, as their cards did.
  const typeStat = (type: string, label: string, icon: PageStat['icon'], tone: PageStat['tone']): PageStat => {
    const count = activityStats.byType[type] || 0;
    return {
      label, value: count, icon, tone,
      onClick: () => setFilterType(type),
      actionLabel: `${label}, ${count}. Activate to show only ${label.toLowerCase()}.`,
    };
  };
  // How this person likes activities shown: the timeline, a table or cards, saved for them.
  const view = useViewPreference('activities', { layouts: ['list', 'table', 'grid'] });

  /** The contact, deal and lead an activity is about, each one a link. */
  const activityLinks = (activity: Activity, className = '') => {
    const contact = activity.contactId ? contacts.find((c) => c.id === activity.contactId) : null;
    const deal = activity.dealId ? deals.find((d) => d.id === activity.dealId) : null;
    const lead = activity.leadId ? leads.find((l) => l.id === activity.leadId) : null;
    if (!contact && !deal && !lead) return null;
    return (
      <div className={`flex flex-wrap items-center gap-2 ${className}`}>
        {contact && (
          <span
            className="inline-flex items-center gap-1.5 text-xs px-2 py-1 rounded-md bg-blue-50 text-blue-700 border border-blue-100 hover:bg-blue-100 cursor-pointer transition-colors"
            onClick={(e) => { e.stopPropagation(); navigate(`/contacts/${contact.id}`); }}
          >
            <User className="w-3 h-3" />
            {contact.name}
          </span>
        )}
        {deal && (
          <span
            className="inline-flex items-center gap-1.5 text-xs px-2 py-1 rounded-md bg-emerald-50 text-emerald-700 border border-emerald-100 hover:bg-emerald-100 cursor-pointer transition-colors"
            onClick={(e) => { e.stopPropagation(); navigate(`/deals/${deal.id}`); }}
          >
            <Target className="w-3 h-3" />
            {deal.name}
          </span>
        )}
        {lead && (
          <span
            className="inline-flex items-center gap-1.5 text-xs px-2 py-1 rounded-md bg-purple-50 text-purple-700 border border-purple-100 hover:bg-purple-100 cursor-pointer transition-colors"
            onClick={(e) => { e.stopPropagation(); navigate(`/leads/${lead.id}`); }}
          >
            <UserCheck className="w-3 h-3" />
            {lead.name}
          </span>
        )}
      </div>
    );
  };

  /** Edit and delete, the same on the timeline, a card and a table line. */
  const activityActions = (activity: Activity, onHover = false) => {
    const { label } = typeInfo(activity.type);
    const reveal = onHover ? 'opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity' : '';
    return (
      <div className="flex items-center gap-1">
        <WriteOnly>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => handleEditActivity(activity)}
            className={`h-8 w-8 p-0 ${reveal} text-slate-400 hover:text-indigo-600 hover:bg-indigo-50`}
            aria-label={`Edit activity ${activity.subject || label}`}
          >
            <Pencil className="w-4 h-4" />
          </Button>
        </WriteOnly>
        <WriteOnly>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setDeleteConfirmActivity(activity)}
            className={`h-8 w-8 p-0 ${reveal} text-slate-400 hover:text-red-600 hover:bg-red-50`}
            aria-label={`Delete activity ${activity.subject || label}`}
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        </WriteOnly>
      </div>
    );
  };

  /** One activity as a card: on the timeline, and in the cards layout. */
  const activityCard = (activity: Activity) => {
    const { icon: Icon, label, bgColor, textColor, borderColor } = typeInfo(activity.type);
    return (
      <div className={`bg-white rounded-xl border ${borderColor} shadow-sm overflow-hidden ${liftOnHover}`}>
        <div className={view.density === 'compact' ? 'p-3' : 'p-4'}>
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
                {activity.updatedAt && (
                  <span className="text-xs text-slate-400 italic">(edited)</span>
                )}
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

              {/* HP-6: Participants display */}
              {activity.participants && (
                <div className="flex items-center gap-1.5 mt-2">
                  <Users className="w-3.5 h-3.5 text-violet-500" />
                  <span className="text-xs text-violet-700 font-medium">
                    Participants: {activity.participants}
                  </span>
                </div>
              )}

              {activityLinks(activity, 'mt-3')}
            </div>

            {/* HP-2: Edit & Delete Buttons */}
            {activityActions(activity, true)}
          </div>
        </div>

        {/* Footer with timestamp and creator */}
        <div className="px-4 py-2 bg-slate-50/50 border-t border-slate-100">
          <p className="text-xs text-slate-400">
            {formatDate(activity.createdAt)}
            {activity.createdByName && <> &middot; by {activity.createdByName}</>}
          </p>
        </div>
      </div>
    );
  };

  const activityColumns: RecordColumn<Activity>[] = [
    {
      id: 'activity',
      label: 'Activity',
      required: true,
      cell: (a) => {
        const { icon: Icon, label, bgColor, textColor } = typeInfo(a.type);
        return (
          <span className="flex items-center gap-2.5">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg ${bgColor}`}>
              <Icon className={`h-3.5 w-3.5 ${textColor}`} aria-hidden />
            </span>
            <span className="min-w-0">
              <span className="block truncate">{a.subject || label}</span>
              {a.subject && <span className="block truncate text-xs font-normal text-slate-400">{label}</span>}
            </span>
          </span>
        );
      },
    },
    { id: 'linked', label: 'About', cell: (a) => activityLinks(a) ?? '—' },
    { id: 'note', label: 'Note', byDefault: false, className: 'max-w-xs', cell: (a) => (a.body ? <span className="line-clamp-1">{a.body}</span> : '—') },
    { id: 'participants', label: 'Participants', byDefault: false, cell: (a) => a.participants || '—' },
    { id: 'by', label: 'By', cell: (a) => a.createdByName || '—' },
    { id: 'when', label: 'When', cell: (a) => <span className="whitespace-nowrap" title={formatDate(a.createdAt)}>{formatRelativeDate(a.createdAt)}</span> },
  ];

  const activityHeroStats: PageStat[] = [
    { label: 'Total Activities', value: activityStats.total, icon: BarChart3, tone: 'sky' },
    { label: 'Today', value: activityStats.todayCount, icon: CalendarDays, tone: 'indigo' },
    { label: 'This Week', value: activityStats.thisWeek, icon: TrendingUp, tone: 'violet' },
    typeStat('call', 'Calls', Phone, 'emerald'),
    typeStat('meeting', 'Meetings', Users, 'blue'),
    typeStat('email', 'Emails', Mail, 'amber'),
  ];

  // Where each date group starts in the timeline, so its cards rise in one after another.
  const groupStarts: Record<string, number> = {};
  Object.entries(groupedActivities).reduce((start, [dateGroup, list]) => {
    groupStarts[dateGroup] = start;
    return start + list.length;
  }, 0);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={ActivityIcon}
            tone="sky"
            title="Activities"
            subtitle={loading ? 'Loading…' :
              filter === 'contact' && filterContactId
                ? `Timeline for ${contacts.find((c) => c.id === filterContactId)?.name ?? 'contact'}`
                : filter === 'deal' && filterDealId
                  ? `Timeline for ${deals.find((d) => d.id === filterDealId)?.name ?? 'deal'}`
                  : 'Track all your sales interactions'}
            actions={
              <>
                {/* Quick Log Dropdown */}
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="outline" className={heroSecondary}>
                      <Zap className="h-4 w-4" />
                      Quick Log
                      <ChevronDown className="h-4 w-4" />
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

                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="outline" disabled={exporting} className={`${heroSecondary} disabled:opacity-60`} aria-label="Export activity history">
                      {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
                      Export
                      <ChevronDown className="h-3.5 w-3.5 opacity-60" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="w-56">
                    <DropdownMenuItem onClick={() => handleExport('pdf')} className="gap-2">
                      <FileTextIcon className="w-4 h-4 text-rose-500" />
                      <span>Export as PDF</span>
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => handleExport('csv')} className="gap-2">
                      <FileSpreadsheet className="w-4 h-4 text-emerald-600" />
                      <span>Google Sheets (CSV)</span>
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>

                <WriteOnly>
                  <Button onClick={() => { setEditingActivity(null); setForm({ type: 'note', subject: '', body: '', contactId: '', dealId: '', participants: '' }); setDialogOpen(true); }} className={heroPrimary('sky')}>
                    <Plus className="h-4 w-4" />
                    Log Activity
                  </Button>
                </WriteOnly>
              </>
            }
            stats={!loading && activities.length > 0 ? activityHeroStats : undefined}
          >
            {/* Quick insight: nothing logged yet today. */}
            {!loading && activities.length > 0 && activityStats.todayCount === 0 && (
              <div className="flex items-center gap-2 rounded-xl border border-sky-200 bg-sky-50/80 px-3 py-2">
                <div className="flex min-w-0 flex-1 items-start gap-2 sm:items-center">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-sky-600 sm:mt-0" aria-hidden />
                  <p className="min-w-0 text-sm text-sky-700">
                    <span className="font-semibold text-sky-800">No activities logged today</span>
                    <span className="hidden sm:inline">
                      {' · '}
                      Consistent activity logging helps track your sales progress. Log a call, meeting, or note to keep your CRM up to date.
                    </span>
                  </p>
                </div>
                <WriteOnly>
                  <Button
                    onClick={() => setDialogOpen(true)}
                    size="sm"
                    className="h-8 shrink-0 rounded-lg bg-sky-600 text-white hover:bg-sky-700"
                  >
                    Log Now
                  </Button>
                </WriteOnly>
              </div>
            )}
          </PageHero>

        {/* Search and filters */}
        <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-sky-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-blue-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
          
          <div className="relative flex flex-col sm:flex-row sm:flex-wrap gap-3">
            {/* Search Input - Enhanced */}
            <div className="relative flex-1 group">
              <div className="absolute inset-0 bg-gradient-to-r from-sky-500/20 via-blue-500/20 to-sky-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
              <div className="relative">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-sky-100 to-blue-100 flex items-center justify-center group-focus-within:from-sky-200 group-focus-within:to-blue-200 transition-all duration-300">
                  <Search className="w-4 h-4 text-sky-600 group-focus-within:text-sky-700 transition-colors" aria-hidden />
                </div>
                <Input
                  type="search"
                  placeholder="Search activities..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-orange-300 focus:ring-4 focus:ring-orange-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                  aria-label="Search activities"
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

            {/* Type Filter - Enhanced */}
            <div className="relative group/filter">
              <div className="absolute inset-0 bg-sky-200/50 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-60 transition-all duration-300" />
              <Select value={filterType} onValueChange={setFilterType}>
                <SelectTrigger className="relative h-11 w-[160px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-sky-50 hover:border-sky-300 focus:border-sky-300 focus:ring-2 focus:ring-sky-200/60 transition-all duration-300">
                  <div className="flex items-center gap-2">
                    <div className="w-6 h-6 rounded-lg bg-sky-100 flex items-center justify-center">
                      <Filter className="w-3.5 h-3.5 text-sky-600" />
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

            <ViewSwitcher view={view} columns={columnChoices(activityColumns)} />
          </div>

          {/* View Filter Tabs - Enhanced */}
          <div className="relative mt-3 flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium text-slate-500">View:</span>
            <div className="relative">
                            <div className="relative flex flex-wrap gap-1 rounded-xl border border-slate-200 bg-slate-100/80 p-1">
                {(['all', 'contact', 'deal', 'lead'] as const).map((f) => (
                  <button
                    key={f}
                    onClick={() => {
                      setFilter(f);
                      if (f !== 'contact') setFilterContactId('');
                      if (f !== 'deal') setFilterDealId('');
                      if (f !== 'lead') setFilterLeadId('');
                    }}
                    className={`px-4 py-2 text-sm font-medium rounded-lg transition-all duration-300 ${
                      filter === f 
                        ? 'bg-gradient-to-r from-sky-500 to-blue-600 text-white shadow-md shadow-sky-500/25' 
                        : 'text-slate-600 hover:bg-white hover:text-slate-900'
                    }`}
                  >
                    {f === 'all' ? 'All Activities' : f === 'contact' ? 'By Contact' : f === 'deal' ? 'By Deal' : 'By Lead'}
                  </button>
                ))}
              </div>
            </div>
            
            {filter === 'contact' && (
              <div className="relative group/contact">
                <div className="absolute inset-0 bg-sky-200/50 rounded-xl blur-lg opacity-0 group-hover/contact:opacity-60 transition-all duration-300" />
                <Select value={filterContactId || 'none'} onValueChange={(v) => setFilterContactId(v === 'none' ? '' : v)}>
                  <SelectTrigger className="relative w-[200px] h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-sky-50 hover:border-sky-300 focus:border-sky-300 focus:ring-2 focus:ring-sky-200/60 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-sky-100 flex items-center justify-center">
                        <User className="w-3.5 h-3.5 text-sky-600" />
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
                <div className="absolute inset-0 bg-sky-200/50 rounded-xl blur-lg opacity-0 group-hover/deal:opacity-60 transition-all duration-300" />
                <Select value={filterDealId || 'none'} onValueChange={(v) => setFilterDealId(v === 'none' ? '' : v)}>
                  <SelectTrigger className="relative w-[200px] h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-sky-50 hover:border-sky-300 focus:border-sky-300 focus:ring-2 focus:ring-sky-200/60 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-sky-100 flex items-center justify-center">
                        <Target className="w-3.5 h-3.5 text-sky-600" />
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

            {/* HP-5: Lead filter selector */}
            {filter === 'lead' && (
              <div className="relative group/lead">
                <div className="absolute inset-0 bg-sky-200/50 rounded-xl blur-lg opacity-0 group-hover/lead:opacity-60 transition-all duration-300" />
                <Select value={filterLeadId || 'none'} onValueChange={(v) => setFilterLeadId(v === 'none' ? '' : v)}>
                  <SelectTrigger className="relative w-[200px] h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-sky-50 hover:border-sky-300 focus:border-sky-300 focus:ring-2 focus:ring-sky-200/60 transition-all duration-300">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-lg bg-sky-100 flex items-center justify-center">
                        <UserCheck className="w-3.5 h-3.5 text-sky-600" />
                      </div>
                      <SelectValue placeholder="Select lead" />
                    </div>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— Select lead —</SelectItem>
                    {leads.map((l) => (
                      <SelectItem key={l.id} value={l.id}>
                        {l.name}
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
              <span className="text-xs text-slate-500">Showing:</span>
              {searchQuery && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                  <Search className="w-3 h-3" />
                  &quot;{searchQuery}&quot;
                  <button onClick={() => setSearchQuery('')} className="ml-0.5 hover:text-sky-600 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              {filterType !== 'all' && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-sky-50 text-sky-700 text-xs font-medium border border-sky-200">
                  <ActivityIcon className="w-3 h-3" />
                  {typeInfo(filterType).label}
                  <button onClick={() => setFilterType('all')} className="ml-0.5 hover:text-sky-900 transition-colors">
                    <X className="w-3 h-3" />
                  </button>
                </span>
              )}
              <span className="text-xs text-slate-500">
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
                  <WriteOnly>
                    <Button onClick={() => setDialogOpen(true)} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-indigo-500 to-purple-500 hover:from-indigo-600 hover:to-purple-600 shadow-lg shadow-indigo-200/50">
                      <Plus className="w-4 h-4" />
                      Log Your First Activity
                    </Button>
                  </WriteOnly>
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
          <ViewTransition layout={view.layout}>
          {view.layout === 'table' ? (
            <RecordTable
              label="Activities"
              items={filteredActivities}
              view={view}
              getKey={(a) => a.id}
              columns={activityColumns}
              actions={(a) => activityActions(a)}
            />
          ) : view.layout === 'grid' ? (
            <div className={`grid sm:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3')}`}>
              {filteredActivities.map((activity, i) => (
                <StaggerItem key={activity.id} index={i} className="group">
                  {activityCard(activity)}
                </StaggerItem>
              ))}
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
                <div className={`relative pl-6 ${view.density === 'compact' ? 'space-y-2' : 'space-y-4'}`}>
                  {/* Timeline line */}
                  <div className="absolute left-[11px] top-6 bottom-6 w-0.5 bg-gradient-to-b from-slate-200 via-slate-200 to-transparent" />

                  {groupActivities.map((activity, i) => {
                    const { icon: Icon, bgColor, textColor } = typeInfo(activity.type);

                    return (
                      <StaggerItem key={activity.id} index={(groupStarts[dateGroup] ?? 0) + i} className="relative group">
                        {/* Timeline dot */}
                        <div className={`absolute -left-6 top-5 w-[22px] h-[22px] rounded-full ${bgColor} border-2 border-white shadow-sm flex items-center justify-center z-10`}>
                          <Icon className={`w-3 h-3 ${textColor}`} />
                        </div>

                        {activityCard(activity)}
                      </StaggerItem>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
          )}
          </ViewTransition>
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
      <Dialog open={dialogOpen} onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditingActivity(null); }}>
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
                    {editingActivity ? 'Edit Activity' : 'Log Activity'}
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    {editingActivity ? 'Update the activity details' : 'Record a call, meeting, email, or note'}
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
                {ACTIVITY_TYPES.map((type) => {
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

            {/* HP-6: Participants field — shown for call, meeting, video, demo */}
            {['call', 'meeting', 'video', 'demo'].includes(form.type) && (
              <div className="group">
                <Label htmlFor="activity-participants" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-violet-100 text-violet-600">
                    <Users className="w-3.5 h-3.5" />
                  </div>
                  Participants
                </Label>
                <Input
                  id="activity-participants"
                  value={form.participants}
                  onChange={(e) => setForm((f) => ({ ...f, participants: e.target.value }))}
                  placeholder="e.g. Jane Smith, Mike Johnson"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-indigo-300 focus:ring-indigo-100 transition-all"
                />
              </div>
            )}

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
              <Button type="button" variant="outline" onClick={() => { setDialogOpen(false); setEditingActivity(null); }} className="h-11 px-6">
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
                    {editingActivity ? 'Saving...' : 'Logging...'}
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    {editingActivity ? 'Save Changes' : 'Log Activity'}
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