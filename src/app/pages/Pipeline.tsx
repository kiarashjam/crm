import { useState, useEffect } from 'react';
import { Briefcase, ChevronDown, Plus, GripVertical, Trash2, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getDeals, updateDeal, createDeal, deleteDeal, getCompanies, getContacts } from '@/app/api';
import type { Deal, Company, Contact } from '@/app/api/types';
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
import { Card, CardContent, CardHeader } from '@/app/components/ui/card';

const STAGES = ['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'] as const;

function formatLastActivity(iso: string | undefined): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return '';
  }
}

function DealCard({
  deal,
  contactName,
  onMoveStage,
  onDelete,
  stages,
}: {
  deal: Deal;
  contactName: string | undefined;
  onMoveStage: (dealId: string, stage: string) => void;
  onDelete: () => void;
  stages: readonly string[];
}) {
  const lastActivity = formatLastActivity(deal.lastActivityAtUtc);
  return (
    <Card className="shadow-sm">
      <CardHeader className="p-3 pb-1">
        <p className="font-medium text-slate-900 text-sm truncate" title={deal.name}>
          {deal.name}
        </p>
        <p className="text-xs text-slate-600 font-medium">{deal.value}</p>
        {contactName && (
          <p className="text-xs text-slate-500 mt-0.5 truncate" title={contactName}>
            Contact: {contactName}
          </p>
        )}
        {lastActivity && (
          <p className="text-xs text-slate-400 mt-0.5">Last activity: {lastActivity}</p>
        )}
      </CardHeader>
      <CardContent className="p-3 pt-0 space-y-2">
        <Select
          value={deal.stage || 'Qualification'}
          onValueChange={(v) => onMoveStage(deal.id, v)}
        >
          <SelectTrigger className="h-8 text-xs">
            <ChevronDown className="w-3.5 h-3.5" />
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {stages.map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-full justify-start gap-1.5 text-red-600 hover:text-red-700 hover:bg-red-50 h-7 text-xs"
          onClick={onDelete}
        >
          <Trash2 className="w-3.5 h-3.5" />
          Delete
        </Button>
      </CardContent>
    </Card>
  );
}

export default function Pipeline() {
  const [deals, setDeals] = useState<Deal[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({
    name: '',
    value: '',
    stage: 'Qualification',
    expectedCloseDate: '',
    companyId: '',
    contactId: '',
  });
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [saving, setSaving] = useState(false);
  const [editDeal, setEditDeal] = useState<Deal | null>(null);
  const [editForm, setEditForm] = useState({ name: '', value: '', expectedCloseDate: '', companyId: '' });
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleteConfirmDeal, setDeleteConfirmDeal] = useState<Deal | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getDeals(), getContacts()])
      .then(([dealsData, contactsData]) => {
        if (!cancelled) {
          setDeals(dealsData);
          setContacts(contactsData);
        }
      })
      .catch(() => { if (!cancelled) toast.error('Failed to load deals'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (createOpen || editDeal) {
      getCompanies().then(setCompanies).catch(() => {});
    }
  }, [createOpen, editDeal]);

  const dealsByStage = STAGES.map((stage) => ({
    stage,
    deals: deals.filter((d) => (d.stage || 'Qualification') === stage),
  }));

  const handleMoveStage = async (dealId: string, newStage: string) => {
    const deal = deals.find((d) => d.id === dealId);
    if (deal && (deal.stage || 'Qualification') === newStage) return; // No-op if same stage (avoid redundant API call)
    const isWon = newStage === 'Closed Won' ? true : newStage === 'Closed Lost' ? false : undefined;
    const payload: { stage: string; isWon?: boolean } = { stage: newStage };
    if (isWon !== undefined) payload.isWon = isWon;
    const updated = await updateDeal(dealId, payload);
    if (updated) {
      setDeals((prev) => prev.map((d) => (d.id === dealId ? updated : d)));
      toast.success('Deal moved');
    } else {
      toast.error('Failed to update deal');
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmDeal) return;
    setDeleting(true);
    try {
      const ok = await deleteDeal(deleteConfirmDeal.id);
      if (ok) {
        setDeals((prev) => prev.filter((d) => d.id !== deleteConfirmDeal.id));
        toast.success('Deal deleted');
        setDeleteConfirmDeal(null);
      } else {
        toast.error('Failed to delete deal');
      }
    } catch {
      toast.error('Failed to delete deal');
    } finally {
      setDeleting(false);
    }
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editDeal) return;
    if (!editForm.name.trim() || !editForm.value.trim()) {
      toast.error('Name and value are required');
      return;
    }
    setSavingEdit(true);
    try {
      const expectedCloseDateUtc = editForm.expectedCloseDate
        ? new Date(editForm.expectedCloseDate).toISOString()
        : undefined;
      const updated = await updateDeal(editDeal.id, {
        name: editForm.name.trim(),
        value: editForm.value.trim(),
        expectedCloseDateUtc,
        companyId: editForm.companyId || undefined,
      });
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === editDeal.id ? updated : d)));
        toast.success('Deal updated');
        setEditDeal(null);
      } else {
        toast.error('Failed to update deal');
      }
    } catch {
      toast.error('Something went wrong');
    } finally {
      setSavingEdit(false);
    }
  };

  const setExpectedCloseInDays = (days: number) => {
    const d = new Date();
    d.setDate(d.getDate() + days);
    setCreateForm((f) => ({ ...f, expectedCloseDate: d.toISOString().slice(0, 10) }));
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.name.trim() || !createForm.value.trim()) {
      toast.error('Name and value are required');
      return;
    }
    setSaving(true);
    try {
      const expectedCloseDateUtc = createForm.expectedCloseDate
        ? new Date(createForm.expectedCloseDate).toISOString()
        : undefined;
      const created = await createDeal({
        name: createForm.name.trim(),
        value: createForm.value.trim(),
        stage: createForm.stage,
        expectedCloseDateUtc,
        companyId: createForm.companyId || undefined,
        contactId: createForm.contactId || undefined,
      });
      if (created) {
        setDeals((prev) => [created, ...prev]);
        toast.success('Deal created');
        setCreateOpen(false);
        setCreateForm({ name: '', value: '', stage: 'Qualification', expectedCloseDate: '', companyId: '', contactId: '' });
      } else {
        toast.error('Failed to create deal');
      }
    } catch {
      toast.error('Something went wrong');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="w-full max-w-7xl mx-auto px-[var(--page-padding)] py-8" tabIndex={-1}>
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Pipeline</h1>
            <p className="text-slate-600 mt-1">Deals by stage. Move deals between stages to track progress.</p>
          </div>
          <Button onClick={() => setCreateOpen(true)} className="gap-2 bg-orange-600 hover:bg-orange-500">
            <Plus className="w-4 h-4" />
            New deal
          </Button>
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : deals.length === 0 ? (
          <EmptyState
            icon={Briefcase}
            title="No deals yet"
            description="Create a deal or connect your CRM to see deals here."
            actionLabel="New deal"
            onAction={() => setCreateOpen(true)}
          />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4 overflow-x-auto pb-4">
            {dealsByStage.map(({ stage, deals: stageDeals }) => (
              <div
                key={stage}
                className="flex flex-col min-w-[260px] bg-slate-100/80 rounded-xl border border-slate-200 p-3"
              >
                <h2 className="font-semibold text-slate-800 mb-2 px-1 flex items-center gap-2">
                  <GripVertical className="w-4 h-4 text-slate-400" aria-hidden />
                  {stage}
                  <span className="ml-auto text-sm font-normal text-slate-500">
                    {stageDeals.length} · {formatValueSum(stageDeals.map((d) => d.value))}
                  </span>
                </h2>
                <div className="space-y-2 flex-1 overflow-y-auto max-h-[70vh]">
                  {stageDeals.map((deal) => (
                    <DealCard
                      key={deal.id}
                      deal={deal}
                      contactName={deal.contactId ? contacts.find((c) => c.id === deal.contactId)?.name : undefined}
                      onMoveStage={handleMoveStage}
                      onDelete={() => setDeleteConfirmDeal(deal)}
                      stages={STAGES}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </main>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>New deal</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleCreate} className="space-y-4">
            <div>
              <Label htmlFor="deal-name">Deal name *</Label>
              <Input
                id="deal-name"
                value={createForm.name}
                onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="deal-value">Value *</Label>
              <Input
                id="deal-value"
                value={createForm.value}
                onChange={(e) => setCreateForm((f) => ({ ...f, value: e.target.value }))}
                placeholder="e.g. $50,000"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label>Stage</Label>
              <Select
                value={createForm.stage}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, stage: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {STAGES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="deal-close">Expected close date</Label>
              <Input
                id="deal-close"
                type="date"
                value={createForm.expectedCloseDate}
                onChange={(e) => setCreateForm((f) => ({ ...f, expectedCloseDate: e.target.value }))}
                className="mt-1"
              />
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
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Creating...' : 'Create deal'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editDeal} onOpenChange={(open) => !open && setEditDeal(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Edit deal</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSaveEdit} className="space-y-4">
            <div>
              <Label htmlFor="edit-deal-name">Deal name *</Label>
              <Input
                id="edit-deal-name"
                value={editForm.name}
                onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="edit-deal-value">Value *</Label>
              <Input
                id="edit-deal-value"
                value={editForm.value}
                onChange={(e) => setEditForm((f) => ({ ...f, value: e.target.value }))}
                placeholder="e.g. $50,000"
                className="mt-1"
                required
              />
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
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditDeal(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={savingEdit} className="bg-orange-600 hover:bg-orange-500">
                {savingEdit ? 'Saving...' : 'Save'}
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
