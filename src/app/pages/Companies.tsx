import { useState, useEffect } from 'react';
import { Building2, Search, Plus, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCompanies, createCompany, updateCompany, messages } from '@/app/api';
import type { Company } from '@/app/api/types';
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

export default function Companies() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCompany, setEditingCompany] = useState<Company | null>(null);
  const [formName, setFormName] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getCompanies()
      .then((data) => { if (!cancelled) setCompanies(data); })
      .catch(() => {})
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const filteredCompanies = searchQuery.trim()
    ? companies.filter((c) =>
        c.name.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : companies;

  const openCreate = () => {
    setEditingCompany(null);
    setFormName('');
    setDialogOpen(true);
  };

  const openEdit = (company: Company) => {
    setEditingCompany(company);
    setFormName(company.name);
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const name = formName.trim();
    if (!name) {
      toast.error(messages.validation.nameRequired);
      return;
    }
    setSaving(true);
    try {
      if (editingCompany) {
        const updated = await updateCompany(editingCompany.id, { name });
        if (updated) {
          setCompanies((prev) => prev.map((c) => (c.id === editingCompany.id ? updated : c)));
          toast.success(messages.success.companyUpdated);
          setDialogOpen(false);
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createCompany({ name });
        if (created) {
          setCompanies((prev) => [created, ...prev]);
          toast.success(messages.success.companyCreated);
          setDialogOpen(false);
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

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Companies</h1>
            <p className="text-slate-600 mt-1">
              {!loading && companies.length > 0
                ? `${companies.length} compan${companies.length === 1 ? 'y' : 'ies'}`
                : 'Manage companies. Used when creating leads and linking to deals.'}
            </p>
          </div>
          <Button onClick={openCreate} className="gap-2 bg-orange-600 hover:bg-orange-500">
            <Plus className="w-4 h-4" />
            Add company
          </Button>
        </div>

        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" aria-hidden />
            <Input
              type="search"
              placeholder="Search by name..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-10 rounded-lg border-slate-300"
              aria-label="Search companies"
            />
          </div>
          {searchQuery.trim() && filteredCompanies.length === 0 && !loading && (
            <p className="text-sm text-slate-500 mt-2">
              No results for &quot;{searchQuery.trim()}&quot;. Try a different search or add a company.
            </p>
          )}
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : filteredCompanies.length === 0 ? (
          <EmptyState
            icon={Building2}
            title={companies.length === 0 ? 'No companies yet' : 'No results found'}
            description={
              companies.length === 0
                ? 'Add a company to use when creating leads or linking deals.'
                : 'Try a different search.'
            }
            actionLabel="Add company"
            onAction={openCreate}
          />
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <ul className="divide-y divide-slate-100">
              {filteredCompanies.map((company) => (
                <li
                  key={company.id}
                  className="flex items-center justify-between gap-3 px-4 py-4 hover:bg-slate-50/50 transition-colors"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="shrink-0 w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-600">
                      <Building2 className="w-5 h-5" />
                    </div>
                    <div className="min-w-0">
                      <p className="font-medium text-slate-900 truncate" title={company.id}>{company.name}</p>
                    </div>
                  </div>
                  <Button variant="outline" size="sm" onClick={() => openEdit(company)} className="gap-1.5 shrink-0">
                    <Pencil className="w-4 h-4" />
                    Edit
                  </Button>
                </li>
              ))}
            </ul>
          </div>
        )}
      </main>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingCompany ? 'Edit company' : 'Add company'}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label htmlFor="company-name">Name *</Label>
              <Input
                id="company-name"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                placeholder="Company name"
                className="mt-1"
                required
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Saving...' : editingCompany ? 'Update' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
