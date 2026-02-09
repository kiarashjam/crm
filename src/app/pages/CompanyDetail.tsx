import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeft, Pencil, Trash2, Building2, Clock, Users, Briefcase, Globe, MapPin,
  ExternalLink,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getContactsPaged, getDealsPaged, updateCompany, deleteCompany, messages } from '@/app/api';
import { authFetchJson } from '@/app/api/apiClient';
import type { Company, Contact, Deal } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Badge } from '@/app/components/ui/badge';
import { Card } from '@/app/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import { getCurrencySymbol } from './pipeline/DealCard';

type CompanyRaw = Company & Record<string, unknown>;

export default function CompanyDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [contactsLoading, setContactsLoading] = useState(false);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [dealsLoading, setDealsLoading] = useState(false);

  // Edit state
  const [editOpen, setEditOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editForm, setEditForm] = useState({
    name: '',
    domain: '',
    industry: '',
    size: '',
    description: '',
    website: '',
    location: '',
  });

  // Delete state
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // HIGH-3: Fetch company by ID — direct endpoint only, no wasteful fallback
  useEffect(() => {
    if (!id) return;
    setLoading(true);
    authFetchJson<CompanyRaw>(`/api/companies/${id}`)
      .then((c) => {
        if (c) {
          setCompany(c as Company);
        }
      })
      .catch(() => {
        toast.error('Failed to load company');
      })
      .finally(() => {
        setLoading(false);
      });
  }, [id]);

  // HIGH-1: Load contacts linked to this company — server-side filtered by companyId
  useEffect(() => {
    if (!id) return;
    setContactsLoading(true);
    getContactsPaged({ page: 1, pageSize: 100, companyId: id })
      .then((result) => {
        setContacts(result.items);
      })
      .catch(() => setContacts([]))
      .finally(() => setContactsLoading(false));
  }, [id]);

  // HIGH-1: Load deals linked to this company — server-side filtered by companyId
  useEffect(() => {
    if (!id) return;
    setDealsLoading(true);
    getDealsPaged({ page: 1, pageSize: 100, companyId: id })
      .then((result) => {
        setDeals(result.items);
      })
      .catch(() => setDeals([]))
      .finally(() => setDealsLoading(false));
  }, [id]);

  // Initialize edit form when company loads
  useEffect(() => {
    if (company) {
      setEditForm({
        name: company.name || '',
        domain: company.domain || '',
        industry: company.industry || '',
        size: company.size || '',
        description: company.description || '',
        website: company.website || '',
        location: company.location || '',
      });
    }
  }, [company]);

  const handleEdit = async () => {
    if (!company) return;
    setEditing(true);
    try {
      // Send trimmed value — empty string clears the field via NormalizeNullable on backend.
      const updated = await updateCompany(company.id, {
        name: editForm.name.trim() || undefined, // Name is required — keep || undefined as safety net
        domain: editForm.domain.trim(),
        industry: editForm.industry.trim(),
        size: editForm.size.trim(),
        description: editForm.description.trim(),
        website: editForm.website.trim(),
        location: editForm.location.trim(),
      });
      if (updated) {
        setCompany(updated);
        toast.success('Company updated');
        setEditOpen(false);
      }
    } catch {
      toast.error(messages.errors?.generic || 'Failed to update company');
    } finally {
      setEditing(false);
    }
  };

  const handleDelete = async () => {
    if (!company) return;
    setDeleting(true);
    try {
      const ok = await deleteCompany(company.id);
      if (ok) {
        toast.success('Company deleted');
        navigate('/companies');
      }
    } catch {
      toast.error(messages.errors?.generic || 'Failed to delete company');
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
        <AppHeader />
        <main className="flex-1 flex items-center justify-center">
          <div className="text-center">
            <Clock className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-3" />
            <p className="text-slate-500">Loading company...</p>
          </div>
        </main>
      </div>
    );
  }

  if (!company) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30">
        <AppHeader />
        <main className="flex-1 flex items-center justify-center">
          <div className="text-center">
            <Building2 className="w-12 h-12 text-slate-300 mx-auto mb-3" />
            <h2 className="text-lg font-semibold text-slate-700">Company not found</h2>
            <Link to="/companies" className="text-emerald-600 text-sm hover:underline mt-2 block">Back to Companies</Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full max-w-5xl mx-auto px-6 py-8" tabIndex={-1}>
          {/* Back + Actions */}
          <div className="flex items-center justify-between mb-6">
            <Button variant="ghost" onClick={() => navigate('/companies')} className="gap-2 text-slate-600">
              <ArrowLeft className="w-4 h-4" /> Back to Companies
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => setEditOpen(true)} className="gap-1">
                <Pencil className="w-3.5 h-3.5" /> Edit
              </Button>
              <Button variant="outline" size="sm" className="text-red-600 hover:bg-red-50" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="w-3.5 h-3.5" />
              </Button>
            </div>
          </div>

          {/* Company Header */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <div className="flex items-start justify-between">
              <div>
                <h1 className="text-2xl font-bold text-slate-900 dark:text-white">{company.name}</h1>
                <div className="flex items-center gap-4 mt-3 flex-wrap">
                  {company.domain && (
                    <Badge variant="outline" className="gap-1">
                      <Globe className="w-3 h-3" />
                      {company.domain}
                    </Badge>
                  )}
                  {company.industry && (
                    <Badge variant="outline" className="gap-1">
                      <Briefcase className="w-3 h-3" />
                      {company.industry}
                    </Badge>
                  )}
                  {company.size && (
                    <Badge variant="outline">{company.size}</Badge>
                  )}
                  {company.location && (
                    <Badge variant="outline" className="gap-1">
                      <MapPin className="w-3 h-3" />
                      {company.location}
                    </Badge>
                  )}
                  {company.website && (
                    <a
                      href={company.website.startsWith('http') ? company.website : `https://${company.website}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex items-center gap-1 text-sm text-emerald-600 hover:underline"
                    >
                      <ExternalLink className="w-3 h-3" />
                      Website
                    </a>
                  )}
                </div>
              </div>
            </div>

            {/* Description */}
            {company.description && (
              <div className="mt-6 pt-6 border-t border-slate-100 dark:border-slate-700">
                <p className="text-xs text-slate-500 uppercase tracking-wider mb-2">Description</p>
                <p className="text-sm text-slate-700 dark:text-slate-300 whitespace-pre-wrap">{company.description}</p>
              </div>
            )}

            {/* Timestamps */}
            <div className="mt-4 flex gap-4 text-xs text-slate-400">
              {company.createdAtUtc && <span>Created {new Date(company.createdAtUtc).toLocaleDateString()}</span>}
              {company.updatedAtUtc && <span>Updated {new Date(company.updatedAtUtc).toLocaleDateString()}</span>}
            </div>
          </div>

          {/* Overview Section */}
          <Card className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <h2 className="text-lg font-semibold text-slate-800 dark:text-white mb-4 flex items-center gap-2">
              <Building2 className="w-5 h-5 text-slate-500" />
              Overview
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Name</p>
                <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{company.name}</p>
              </div>
              {company.domain && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Domain</p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{company.domain}</p>
                </div>
              )}
              {company.industry && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Industry</p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{company.industry}</p>
                </div>
              )}
              {company.size && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Size</p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{company.size}</p>
                </div>
              )}
              {company.location && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Location</p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{company.location}</p>
                </div>
              )}
              {company.website && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1">Website</p>
                  <a
                    href={company.website.startsWith('http') ? company.website : `https://${company.website}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-sm font-medium text-emerald-600 hover:underline inline-flex items-center gap-1"
                  >
                    {company.website}
                    <ExternalLink className="w-3 h-3" />
                  </a>
                </div>
              )}
            </div>
          </Card>

          {/* Contacts Section */}
          <Card className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <div className="flex items-center gap-2 mb-4">
              <Users className="w-5 h-5 text-slate-500" />
              <h2 className="text-lg font-semibold text-slate-800 dark:text-white">Contacts</h2>
              <Badge variant="secondary" className="ml-2">{contacts.length}</Badge>
            </div>

            {contactsLoading ? (
              <div className="py-6 text-center">
                <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-sm text-slate-500">Loading contacts...</p>
              </div>
            ) : contacts.length === 0 ? (
              <div className="py-6 text-center">
                <Users className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-sm text-slate-500">No contacts linked to this company</p>
              </div>
            ) : (
              <div className="space-y-2">
                {contacts.map((contact) => (
                  <Link
                    key={contact.id}
                    to={`/contacts/${contact.id}`}
                    className="flex items-center gap-3 p-3 bg-slate-50 dark:bg-slate-700/50 rounded-xl border border-slate-100 dark:border-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 transition-all"
                  >
                    <div className="w-9 h-9 rounded-lg bg-emerald-100 text-emerald-600 flex items-center justify-center shrink-0">
                      <Users className="w-4 h-4" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{contact.name}</p>
                      <p className="text-xs text-slate-500">{contact.email}</p>
                      {contact.jobTitle && <p className="text-xs text-slate-400">{contact.jobTitle}</p>}
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </Card>

          {/* Deals Section */}
          <Card className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-lg p-8">
            <div className="flex items-center gap-2 mb-4">
              <Briefcase className="w-5 h-5 text-slate-500" />
              <h2 className="text-lg font-semibold text-slate-800 dark:text-white">Deals</h2>
              <Badge variant="secondary" className="ml-2">{deals.length}</Badge>
            </div>

            {dealsLoading ? (
              <div className="py-6 text-center">
                <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-sm text-slate-500">Loading deals...</p>
              </div>
            ) : deals.length === 0 ? (
              <div className="py-6 text-center">
                <Briefcase className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-sm text-slate-500">No deals linked to this company</p>
              </div>
            ) : (
              <div className="space-y-2">
                {deals.map((deal) => (
                  <Link
                    key={deal.id}
                    to={`/deals/${deal.id}`}
                    className="flex items-center justify-between gap-3 p-3 bg-slate-50 dark:bg-slate-700/50 rounded-xl border border-slate-100 dark:border-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 transition-all"
                  >
                    <div className="flex items-center gap-3 flex-1 min-w-0">
                      <div className="w-9 h-9 rounded-lg bg-blue-100 text-blue-600 flex items-center justify-center shrink-0">
                        <Briefcase className="w-4 h-4" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{deal.name}</p>
                        <div className="flex items-center gap-2 mt-0.5">
                          {deal.dealStageName && (
                            <Badge variant="outline" className="text-xs">{deal.dealStageName}</Badge>
                          )}
                          {deal.value && (
                            <span className="text-xs font-medium text-slate-600 dark:text-slate-400">
                              {getCurrencySymbol(deal.currency || 'USD')}{deal.value}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </Card>
        </main>
      </PageTransition>

      {/* Edit Dialog */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Edit Company</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Name *</label>
              <Input
                value={editForm.name}
                onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                className="mt-1"
                placeholder="Company name"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Domain</label>
              <Input
                value={editForm.domain}
                onChange={(e) => setEditForm((f) => ({ ...f, domain: e.target.value }))}
                className="mt-1"
                placeholder="example.com"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Industry</label>
              <Input
                value={editForm.industry}
                onChange={(e) => setEditForm((f) => ({ ...f, industry: e.target.value }))}
                className="mt-1"
                placeholder="Technology"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Size</label>
              <Input
                value={editForm.size}
                onChange={(e) => setEditForm((f) => ({ ...f, size: e.target.value }))}
                className="mt-1"
                placeholder="1-10, 11-50, etc."
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Location</label>
              <Input
                value={editForm.location}
                onChange={(e) => setEditForm((f) => ({ ...f, location: e.target.value }))}
                className="mt-1"
                placeholder="City, Country"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Website</label>
              <Input
                value={editForm.website}
                onChange={(e) => setEditForm((f) => ({ ...f, website: e.target.value }))}
                className="mt-1"
                placeholder="https://example.com"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Description</label>
              <textarea
                value={editForm.description}
                onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))}
                className="mt-1 w-full px-3 py-2 text-sm border border-slate-200 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20"
                rows={3}
                placeholder="Company description..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)} disabled={editing}>Cancel</Button>
            <Button onClick={handleEdit} disabled={editing || !editForm.name.trim()} className="bg-emerald-600 hover:bg-emerald-700">
              {editing ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Dialog — HP-1: shows linked entity counts as warning */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader><DialogTitle>Delete company</DialogTitle></DialogHeader>
          <div className="space-y-3">
            <p className="text-slate-600 dark:text-slate-300">
              Are you sure you want to delete <strong>{company?.name}</strong>? This action cannot be undone.
            </p>
            {(contacts.length > 0 || deals.length > 0) && (
              <div className="p-3 rounded-lg bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-700">
                <p className="text-sm font-medium text-amber-800 dark:text-amber-300 flex items-center gap-1.5">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" /></svg>
                  Warning: This company has linked records
                </p>
                <ul className="mt-1.5 text-sm text-amber-700 dark:text-amber-400 list-disc list-inside">
                  {contacts.length > 0 && (
                    <li>{contacts.length} contact{contacts.length > 1 ? 's' : ''} will be unlinked</li>
                  )}
                  {deals.length > 0 && (
                    <li>{deals.length} deal{deals.length > 1 ? 's' : ''} will be unlinked</li>
                  )}
                </ul>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deleting}>Cancel</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>{deleting ? 'Deleting...' : 'Delete'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
