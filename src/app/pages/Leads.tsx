import { useState, useEffect, useMemo } from 'react';
import { Search, Plus, Pencil, Trash2, Building2, User } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getLeads,
  createLead,
  updateLead,
  deleteLead,
  getCompanies,
  convertLead,
  type ConvertLeadRequest,
} from '@/app/api';
import type { Lead, Company } from '@/app/api/types';
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

const LEAD_STATUSES = ['New', 'Contacted', 'Qualified', 'Lost'] as const;
const LEAD_SOURCES = ['website', 'referral', 'ads', 'events', 'manual'] as const;

type LeadForm = {
  name: string;
  email: string;
  phone: string;
  companyId: string;
  source: string;
  status: string;
};

const emptyForm: LeadForm = {
  name: '',
  email: '',
  phone: '',
  companyId: '',
  source: 'manual',
  status: 'New',
};

export default function Leads() {
  const [leads, setLeads] = useState<Lead[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingLead, setEditingLead] = useState<Lead | null>(null);
  const [form, setForm] = useState<LeadForm>(emptyForm);
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
  });
  const [converting, setConverting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getLeads(), getCompanies()])
      .then(([l, c]) => {
        if (!cancelled) {
          setLeads(l);
          setCompanies(c);
        }
      })
      .catch(() => { if (!cancelled) toast.error('Failed to load leads'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const filteredLeads = useMemo(() => {
    if (!searchQuery.trim()) return leads;
    const q = searchQuery.toLowerCase();
    return leads.filter(
      (l) =>
        l.name.toLowerCase().includes(q) ||
        l.email.toLowerCase().includes(q) ||
        (l.phone && l.phone.includes(q))
    );
  }, [leads, searchQuery]);

  const openCreate = () => {
    setEditingLead(null);
    setForm(emptyForm);
    setDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmLead) return;
    setDeleting(true);
    try {
      const ok = await deleteLead(deleteConfirmLead.id);
      if (ok) {
        setLeads((prev) => prev.filter((l) => l.id !== deleteConfirmLead.id));
        toast.success('Lead deleted');
        setDeleteConfirmLead(null);
      } else {
        toast.error('Failed to delete lead');
      }
    } catch {
      toast.error('Failed to delete lead');
    } finally {
      setDeleting(false);
    }
  };

  const openConvert = (lead: Lead) => {
    setConvertDialogLead(lead);
    setConvertForm({
      createContact: true,
      createDeal: false,
      dealName: lead.name,
      dealValue: '',
      dealStage: 'Qualification',
    });
  };

  const handleConvert = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!convertDialogLead) return;
    if (!convertForm.createContact && !convertForm.createDeal) {
      toast.error('Select at least one: Create contact or Create deal');
      return;
    }
    setConverting(true);
    try {
      const result = await convertLead(convertDialogLead.id, convertForm);
      if (result) {
        const parts: string[] = [];
        if (result.contactId) parts.push('contact created');
        if (result.dealId) parts.push('deal created');
        toast.success(parts.length ? `Lead converted: ${parts.join(', ')}` : 'Lead converted');
        setConvertDialogLead(null);
        getLeads().then(setLeads).catch(() => {});
      } else {
        toast.error('Failed to convert lead');
      }
    } catch {
      toast.error('Failed to convert lead');
    } finally {
      setConverting(false);
    }
  };

  const openEdit = (lead: Lead) => {
    setEditingLead(lead);
    setForm({
      name: lead.name,
      email: lead.email,
      phone: lead.phone ?? '',
      companyId: lead.companyId ?? '',
      source: lead.source ?? 'manual',
      status: lead.status,
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim() || !form.email.trim()) {
      toast.error('Name and email are required');
      return;
    }
    setSaving(true);
    try {
      if (editingLead) {
        const updated = await updateLead(editingLead.id, {
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
          source: form.source || undefined,
          status: form.status,
        });
        if (updated) {
          setLeads((prev) => prev.map((l) => (l.id === updated.id ? updated : l)));
          toast.success('Lead updated');
          setDialogOpen(false);
        } else {
          toast.error('Failed to update lead');
        }
      } else {
        const created = await createLead({
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
          source: form.source || undefined,
          status: form.status,
        });
        if (created) {
          setLeads((prev) => [created, ...prev]);
          toast.success('Lead created');
          setDialogOpen(false);
        } else {
          toast.error('Failed to create lead');
        }
      }
    } catch {
      toast.error('Something went wrong');
    } finally {
      setSaving(false);
    }
  };

  const companyName = (id: string) => companies.find((c) => c.id === id)?.name ?? '—';

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-8" tabIndex={-1}>
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Leads</h1>
            <p className="text-slate-600 mt-1">Manage and track your leads.</p>
          </div>
          <Button onClick={openCreate} className="gap-2 bg-orange-600 hover:bg-orange-500">
            <Plus className="w-4 h-4" />
            Add lead
          </Button>
        </div>

        <div className="mb-4 relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
          <Input
            type="search"
            placeholder="Search by name, email, or phone..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-10 rounded-lg border-slate-300"
          />
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : filteredLeads.length === 0 ? (
          <EmptyState
            icon={User}
            title={leads.length === 0 ? 'No leads yet' : 'No results found'}
            description={
              leads.length === 0
                ? 'Add your first lead to start tracking opportunities.'
                : 'Try a different search.'
            }
            actionLabel="Add lead"
            onAction={openCreate}
          />
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <ul className="divide-y divide-slate-100">
              {filteredLeads.map((lead) => (
                <li
                  key={lead.id}
                  className="flex flex-wrap items-center justify-between gap-3 px-4 py-4 hover:bg-slate-50/50 transition-colors"
                >
                  <button
                    type="button"
                    onClick={() => openEdit(lead)}
                    className="flex-1 min-w-0 text-left cursor-pointer"
                  >
                    <p className="font-medium text-slate-900 truncate">{lead.name}</p>
                    <p className="text-sm text-slate-500 truncate">{lead.email}</p>
                    <div className="flex flex-wrap items-center gap-2 mt-1">
                      {lead.companyId && (
                        <span className="inline-flex items-center gap-1 text-xs text-slate-500">
                          <Building2 className="w-3.5 h-3.5" />
                          {companyName(lead.companyId)}
                        </span>
                      )}
                      <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">
                        {lead.status}
                      </span>
                      {lead.source && (
                        <span className="text-xs text-slate-400">{lead.source}</span>
                      )}
                    </div>
                  </button>
                  <div className="flex items-center gap-2 flex-wrap">
                    <Button variant="outline" size="sm" onClick={() => openConvert(lead)} className="gap-1.5">
                      <ArrowRightCircle className="w-4 h-4" />
                      Convert
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => openEdit(lead)} className="gap-1.5">
                      <Pencil className="w-4 h-4" />
                      Edit
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setDeleteConfirmLead(lead)}
                      className="gap-1.5 text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700"
                      aria-label={`Delete ${lead.name}`}
                    >
                      <Trash2 className="w-4 h-4" />
                      Delete
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </main>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingLead ? 'Edit lead' : 'Add lead'}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label htmlFor="lead-name">Name *</Label>
              <Input
                id="lead-name"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="Full name"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="lead-email">Email *</Label>
              <Input
                id="lead-email"
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                placeholder="email@example.com"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="lead-phone">Phone</Label>
              <Input
                id="lead-phone"
                value={form.phone}
                onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                placeholder="Optional"
                className="mt-1"
              />
            </div>
            <div>
              <Label>Company</Label>
              <Select
                value={form.companyId || 'none'}
                onValueChange={(v) => setForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Select company" />
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
              <Label>Source</Label>
              <Select
                value={form.source}
                onValueChange={(v) => setForm((f) => ({ ...f, source: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {LEAD_SOURCES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Status</Label>
              <Select
                value={form.status}
                onValueChange={(v) => setForm((f) => ({ ...f, status: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {LEAD_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
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
                {saving ? 'Saving...' : editingLead ? 'Update' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!convertDialogLead} onOpenChange={(open) => !open && setConvertDialogLead(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Convert lead</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600 text-sm">
            Convert <strong>{convertDialogLead?.name}</strong> to a contact and/or deal. Contact will use the lead&apos;s name, email, phone, and company.
          </p>
          <form onSubmit={handleConvert} className="space-y-4">
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="convert-contact"
                checked={convertForm.createContact}
                onChange={(e) => setConvertForm((f) => ({ ...f, createContact: e.target.checked }))}
                className="rounded border-slate-300"
              />
              <Label htmlFor="convert-contact" className="font-normal cursor-pointer">Create contact</Label>
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="convert-deal"
                checked={convertForm.createDeal}
                onChange={(e) => setConvertForm((f) => ({ ...f, createDeal: e.target.checked }))}
                className="rounded border-slate-300"
              />
              <Label htmlFor="convert-deal" className="font-normal cursor-pointer">Create deal</Label>
            </div>
            {convertForm.createDeal && (
              <>
                <div>
                  <Label htmlFor="convert-deal-name">Deal name</Label>
                  <Input
                    id="convert-deal-name"
                    value={convertForm.dealName}
                    onChange={(e) => setConvertForm((f) => ({ ...f, dealName: e.target.value }))}
                    placeholder="Defaults to lead name"
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label htmlFor="convert-deal-value">Deal value</Label>
                  <Input
                    id="convert-deal-value"
                    value={convertForm.dealValue}
                    onChange={(e) => setConvertForm((f) => ({ ...f, dealValue: e.target.value }))}
                    placeholder="e.g. $10,000"
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label>Stage</Label>
                  <Select
                    value={convertForm.dealStage || 'Qualification'}
                    onValueChange={(v) => setConvertForm((f) => ({ ...f, dealStage: v }))}
                  >
                    <SelectTrigger className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'].map((s) => (
                        <SelectItem key={s} value={s}>
                          {s}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </>
            )}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setConvertDialogLead(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={converting} className="bg-orange-600 hover:bg-orange-500">
                {converting ? 'Converting...' : 'Convert'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deleteConfirmLead} onOpenChange={(open) => !open && setDeleteConfirmLead(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete lead</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600">
            Are you sure you want to delete <strong>{deleteConfirmLead?.name}</strong>? This cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirmLead(null)} disabled={deleting}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleDeleteConfirm}
              disabled={deleting}
            >
              {deleting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
