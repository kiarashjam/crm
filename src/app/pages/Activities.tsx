import { useState, useEffect } from 'react';
import { Activity as ActivityIcon, Plus, Phone, Mail, MessageSquare, FileText, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getActivities, createActivity, deleteActivity, getContacts, getDeals, getActivitiesByContact, getActivitiesByDeal, messages } from '@/app/api';
import type { Activity, Contact, Deal } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
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

const ACTIVITY_TYPES = [
  { id: 'call', label: 'Call', icon: Phone },
  { id: 'meeting', label: 'Meeting', icon: MessageSquare },
  { id: 'email', label: 'Email', icon: Mail },
  { id: 'note', label: 'Note', icon: FileText },
] as const;

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
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

  const loadData = () => {
    setLoading(true);
    if (filter === 'contact' && filterContactId) {
      getActivitiesByContact(filterContactId)
        .then(setActivities)
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    } else if (filter === 'deal' && filterDealId) {
      getActivitiesByDeal(filterDealId)
        .then(setActivities)
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    } else {
      getActivities()
        .then(setActivities)
        .catch(() => toast.error(messages.errors.loadFailed))
        .finally(() => setLoading(false));
    }
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    const promise =
      filter === 'contact' && filterContactId
        ? getActivitiesByContact(filterContactId)
        : filter === 'deal' && filterDealId
          ? getActivitiesByDeal(filterDealId)
          : getActivities();
    promise
      .then((data) => { if (!cancelled) setActivities(data); })
      .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [filter, filterContactId, filterDealId]);

  useEffect(() => {
    if (!(dialogOpen || filter !== 'all')) return;
    let cancelled = false;
    Promise.all([getContacts(), getDeals()]).then(([c, d]) => {
      if (!cancelled) {
        setContacts(c);
        setDeals(d);
      }
    });
    return () => { cancelled = true; };
  }, [dialogOpen, filter]);

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
        // Refetch when filtered so list stays correct (new activity shows only if it matches filter)
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

  const typeInfo = (type: string) => ACTIVITY_TYPES.find((t) => t.id === type) ?? { id: type, label: type, icon: FileText };

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

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Activities</h1>
            <p className="text-slate-600 mt-1">
              {filter === 'contact' && filterContactId
                ? `Activities for ${contacts.find((c) => c.id === filterContactId)?.name ?? 'contact'}`
                : filter === 'deal' && filterDealId
                  ? `Activities for ${deals.find((d) => d.id === filterDealId)?.name ?? 'deal'}`
                  : 'Calls, meetings, emails, and notes. Filter by contact or deal for timeline view.'}
            </p>
          </div>
          <Button onClick={() => setDialogOpen(true)} className="gap-2 bg-orange-600 hover:bg-orange-500">
            <Plus className="w-4 h-4" />
            Log activity
          </Button>
        </div>

        <div className="mb-4 flex flex-wrap items-center gap-3">
          <span className="text-sm font-medium text-slate-700">View:</span>
          <div className="flex gap-2">
            {(['all', 'contact', 'deal'] as const).map((f) => (
              <Button
                key={f}
                variant={filter === f ? 'default' : 'outline'}
                size="sm"
                onClick={() => {
                  setFilter(f);
                  if (f !== 'contact') setFilterContactId('');
                  if (f !== 'deal') setFilterDealId('');
                }}
                className={filter === f ? 'bg-orange-600 hover:bg-orange-500' : ''}
              >
                {f === 'all' ? 'All' : f === 'contact' ? 'By contact' : 'By deal'}
              </Button>
            ))}
          </div>
          {filter === 'contact' && (
            <Select value={filterContactId || 'none'} onValueChange={(v) => setFilterContactId(v === 'none' ? '' : v)}>
              <SelectTrigger className="w-[200px] h-9">
                <SelectValue placeholder="Select contact" />
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
          )}
          {filter === 'deal' && (
            <Select value={filterDealId || 'none'} onValueChange={(v) => setFilterDealId(v === 'none' ? '' : v)}>
              <SelectTrigger className="w-[200px] h-9">
                <SelectValue placeholder="Select deal" />
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
          )}
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : activities.length === 0 ? (
          <EmptyState
            icon={ActivityIcon}
            title="No activities yet"
            description="Log a call, meeting, email, or note to build your activity timeline."
            actionLabel="Log activity"
            onAction={() => setDialogOpen(true)}
          />
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <ul className="divide-y divide-slate-100">
              {activities.map((activity) => {
                const { icon: Icon, label } = typeInfo(activity.type);
                return (
                  <li key={activity.id} className="px-4 py-4 hover:bg-slate-50/50 transition-colors">
                    <div className="flex gap-3">
                      <div className="shrink-0 w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-600">
                        <Icon className="w-5 h-5" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="font-medium text-slate-900">{label}</p>
                        {activity.subject && (
                          <p className="text-sm text-slate-700 mt-0.5">{activity.subject}</p>
                        )}
                        {activity.body && (
                          <p className="text-sm text-slate-500 mt-1 line-clamp-2">{activity.body}</p>
                        )}
                        {(activity.contactId || activity.dealId) && (
                          <p className="text-xs text-slate-500 mt-1">
                            {[activity.contactId && contacts.find((c) => c.id === activity.contactId)?.name, activity.dealId && deals.find((d) => d.id === activity.dealId)?.name].filter(Boolean).join(' · ')}
                          </p>
                        )}
                        <p className="text-xs text-slate-400 mt-2">{formatDate(activity.createdAt)}</p>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
      </main>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Log activity</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label>Type</Label>
              <Select value={form.type} onValueChange={(v) => setForm((f) => ({ ...f, type: v }))}>
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACTIVITY_TYPES.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="activity-subject">Subject</Label>
              <Input
                id="activity-subject"
                value={form.subject}
                onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))}
                placeholder="e.g. Discovery call – discussed pricing"
                className="mt-1"
              />
            </div>
            <div>
              <Label htmlFor="activity-body">Details</Label>
              <Textarea
                id="activity-body"
                value={form.body}
                onChange={(e) => setForm((f) => ({ ...f, body: e.target.value }))}
                placeholder="Notes or summary"
                rows={3}
                className="mt-1"
              />
            </div>
            <div>
              <Label>Link to contact</Label>
              <Select
                value={form.contactId || 'none'}
                onValueChange={(v) => setForm((f) => ({ ...f, contactId: v === 'none' ? '' : v }))}
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
            <div>
              <Label>Link to deal</Label>
              <Select
                value={form.dealId || 'none'}
                onValueChange={(v) => setForm((f) => ({ ...f, dealId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {deals.map((d) => (
                    <SelectItem key={d.id} value={d.id}>
                      {d.name} · {d.value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Saving...' : 'Log activity'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deleteConfirmActivity} onOpenChange={(open) => !open && setDeleteConfirmActivity(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete activity</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600">
            Are you sure you want to delete this activity? This cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirmActivity(null)} disabled={deleting}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>
              {deleting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
