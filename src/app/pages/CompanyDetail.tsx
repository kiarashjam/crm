import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  Pencil, Trash2, Building2, Clock, Users, Briefcase, Globe, MapPin,
  ExternalLink,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { RecordSkeleton } from '@/app/components/PageSkeleton';
import CustomFieldsCard from '@/app/components/CustomFieldsCard';
import AttachmentsCard from '@/app/components/AttachmentsCard';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCompanies, getContactsPaged, getDealsPaged, updateCompany, deleteCompany, messages } from '@/app/api';
import { authFetchJson, servesDemoData } from '@/app/api/apiClient';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import type { Company, Contact, Deal } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Badge } from '@/app/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { RecordAvatar, RecordCard, initialsOf } from '@/app/components/RecordCard';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { cn } from '@/app/components/ui/utils';
import { currencyOf, formatDealSum, formatMoney, parseAmount } from '@/app/lib/money';

type CompanyRaw = Company & Record<string, unknown>;

export default function CompanyDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [company, setCompany] = useState<Company | null>(null);
  useDocumentTitle(company?.name, 'Companies');
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
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
    // Demo mode has no server to ask; its companies live in the browser.
    const load = servesDemoData()
      ? getCompanies().then((list) => list.find((c) => c.id === id) ?? null)
      : authFetchJson<CompanyRaw>(`/api/companies/${id}`);
    load
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
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <RecordSkeleton />
        </main>
      </div>
    );
  }


  if (!company) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main className="flex-1 flex items-center justify-center">
          <div className="text-center">
            <Building2 className="w-12 h-12 text-slate-300 mx-auto mb-3" />
            <h2 className="text-lg font-semibold text-slate-700">Company not found</h2>
            <Link to="/companies" className="text-emerald-700 text-sm hover:underline mt-2 block">Back to Companies</Link>
          </div>
        </main>
      </div>
    );
  }

  const createdLabel = company.createdAtUtc
    ? new Date(company.createdAtUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
    : null;
  const websiteHref = company.website
    ? (company.website.startsWith('http') ? company.website : `https://${company.website}`)
    : undefined;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Building2}
            avatar={<RecordAvatar name={company.name} tone="blue" />}
            tone="blue"
            back={{ to: '/companies', label: 'Back to Companies' }}
            title={company.name}
            badges={company.industry && (
              <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
                <Briefcase className="h-3 w-3" aria-hidden /> {company.industry}
              </span>
            )}
            subtitle={
              <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
                {company.domain && (
                  <span className="inline-flex items-center gap-1.5"><Globe className="h-3.5 w-3.5" aria-hidden /> {company.domain}</span>
                )}
                {company.location && (
                  <span className="inline-flex items-center gap-1.5"><MapPin className="h-3.5 w-3.5" aria-hidden /> {company.location}</span>
                )}
                {company.size && <span>{company.size} people</span>}
                {company.website && (
                  <a
                    href={websiteHref}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1 font-medium text-blue-600 hover:underline"
                  >
                    <ExternalLink className="h-3.5 w-3.5" aria-hidden /> Website
                  </a>
                )}
              </span>
            }
            actions={
              <>
                <WriteOnly>
                  <Button variant="outline" onClick={() => setEditOpen(true)} className={heroSecondary}>
                    <Pencil className="h-4 w-4" /> Edit
                  </Button>
                </WriteOnly>
                <WriteOnly>
                  <Button
                    variant="outline"
                    onClick={() => setDeleteOpen(true)}
                    aria-label="Delete company"
                    className={`${heroSecondary} text-red-600 hover:border-red-200 hover:bg-red-50 hover:text-red-700`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </WriteOnly>
              </>
            }
            stats={[
              { label: 'Contacts', value: contacts.length, icon: Users, tone: 'emerald', hint: contacts[0] ? `incl. ${contacts[0].name}` : 'none yet' },
              { label: 'Deals', value: deals.length, icon: Briefcase, tone: 'violet', hint: deals.length > 0 ? `${formatDealSum(deals)} in all` : 'none yet' },
              { label: 'Industry', value: company.industry || '—', icon: Building2, tone: 'blue', hint: company.size ? `${company.size} people` : undefined },
              { label: 'Since', value: createdLabel ?? '—', icon: Clock, tone: 'amber', hint: company.updatedAtUtc ? `updated ${new Date(company.updatedAtUtc).toLocaleDateString()}` : undefined },
            ]}
          />

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="min-w-0 space-y-5">
              <RecordCard title="Contacts" icon={<Users className="h-3.5 w-3.5" />} tone="emerald" count={contacts.length} index={0}>
                {contactsLoading ? (
                  <div className="py-6 text-center">
                    <Spinner size={22} className="mx-auto mb-2" />
                    <p className="text-sm text-slate-500">Loading contacts...</p>
                  </div>
                ) : contacts.length === 0 ? (
                  <div className="py-6 text-center">
                    <Users className="mx-auto mb-2 h-10 w-10 text-slate-300" />
                    <p className="text-sm text-slate-500">No contacts linked to this company</p>
                  </div>
                ) : (
                  <ul className="grid gap-2 sm:grid-cols-2">
                    {contacts.map((contact, index) => (
                      <StaggerItem as="li" key={contact.id} index={index}>
                        <Link
                          to={`/contacts/${contact.id}`}
                          className={cn('group flex items-center gap-3 rounded-xl border border-slate-100 bg-slate-50 p-3 hover:border-emerald-200 hover:bg-white dark:border-slate-600 dark:bg-slate-700/50 dark:hover:bg-slate-700', liftOnHover)}
                        >
                          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-500 to-teal-500 text-xs font-bold text-white shadow-sm transition-transform group-hover:-rotate-6" aria-hidden>
                            {initialsOf(contact.name)}
                          </span>
                          <div className="min-w-0 flex-1">
                            <p className="truncate text-sm font-medium text-slate-800 group-hover:text-emerald-700 dark:text-slate-200">{contact.name}</p>
                            <p className="truncate text-xs text-slate-500">{contact.email}</p>
                            {contact.jobTitle && <p className="truncate text-xs text-slate-500">{contact.jobTitle}</p>}
                          </div>
                        </Link>
                      </StaggerItem>
                    ))}
                  </ul>
                )}
              </RecordCard>

              <RecordCard title="Deals" icon={<Briefcase className="h-3.5 w-3.5" />} tone="violet" count={deals.length} index={1}>
                {dealsLoading ? (
                  <div className="py-6 text-center">
                    <Spinner size={22} className="mx-auto mb-2" />
                    <p className="text-sm text-slate-500">Loading deals...</p>
                  </div>
                ) : deals.length === 0 ? (
                  <div className="py-6 text-center">
                    <Briefcase className="mx-auto mb-2 h-10 w-10 text-slate-300" />
                    <p className="text-sm text-slate-500">No deals linked to this company</p>
                  </div>
                ) : (
                  <ul className="space-y-2">
                    {deals.map((deal, index) => (
                      <StaggerItem as="li" key={deal.id} index={index}>
                        <Link
                          to={`/deals/${deal.id}`}
                          className={cn('group flex items-center justify-between gap-3 rounded-xl border border-slate-100 bg-slate-50 p-3 hover:border-violet-200 hover:bg-white dark:border-slate-600 dark:bg-slate-700/50 dark:hover:bg-slate-700', liftOnHover)}
                        >
                          <div className="flex min-w-0 flex-1 items-center gap-3">
                            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-500 text-white shadow-sm" aria-hidden>
                              <Briefcase className="h-4 w-4" />
                            </span>
                            <div className="min-w-0 flex-1">
                              <p className="truncate text-sm font-medium text-slate-800 group-hover:text-violet-700 dark:text-slate-200">{deal.name}</p>
                              {deal.dealStageName && (
                                <Badge variant="outline" className="mt-0.5 text-xs">{deal.dealStageName}</Badge>
                              )}
                            </div>
                          </div>
                          {deal.value && (
                            <span className="shrink-0 text-sm font-bold text-emerald-700">
                              {formatMoney(parseAmount(deal.value), currencyOf(deal))}
                            </span>
                          )}
                        </Link>
                      </StaggerItem>
                    ))}
                  </ul>
                )}
              </RecordCard>
            </div>

            <aside className="min-w-0 space-y-5">
              <RecordCard title="Overview" icon={<Building2 className="h-3.5 w-3.5" />} tone="blue" index={2}>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
                  {[
                    ['Name', company.name],
                    ['Domain', company.domain],
                    ['Industry', company.industry],
                    ['Size', company.size],
                    ['Location', company.location],
                  ].filter(([, value]) => value).map(([label, value]) => (
                    <div key={label} className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">{label}</dt>
                      <dd className="mt-0.5 truncate font-medium text-slate-800 dark:text-slate-200">{value}</dd>
                    </div>
                  ))}
                  {company.website && (
                    <div className="col-span-2 min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Website</dt>
                      <dd className="mt-0.5 truncate">
                        <a href={websiteHref} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 font-medium text-blue-600 hover:underline">
                          {company.website}
                          <ExternalLink className="h-3 w-3" />
                        </a>
                      </dd>
                    </div>
                  )}
                </dl>
                {company.description && (
                  <div className="mt-4 border-t border-slate-100 pt-3 dark:border-slate-700">
                    <p className="mb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500">Description</p>
                    <p className="whitespace-pre-wrap text-sm text-slate-700 dark:text-slate-300">{company.description}</p>
                  </div>
                )}
                <div className="mt-3 flex gap-4 text-xs text-slate-500">
                  {company.createdAtUtc && <span>Created {new Date(company.createdAtUtc).toLocaleDateString()}</span>}
                  {company.updatedAtUtc && <span>Updated {new Date(company.updatedAtUtc).toLocaleDateString()}</span>}
                </div>
              </RecordCard>

              {/* Custom fields (renders only when defined for companies) */}
              <CustomFieldsCard
                entityType="company"
                recordId={company.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />

              <AttachmentsCard
                entityType="company"
                recordId={company.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </aside>
          </div>
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
