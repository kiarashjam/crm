import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, UserCircle, Plus, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getContacts, createContact, updateContact, getCompanies, messages } from '@/app/api';
import type { Contact, Company } from '@/app/api/types';
import { Input } from '@/app/components/ui/input';
import { Button } from '@/app/components/ui/button';
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

export default function Contacts() {
  const navigate = useNavigate();
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingContact, setEditingContact] = useState<Contact | null>(null);
  const [form, setForm] = useState({ name: '', email: '', phone: '', companyId: '' });
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getContacts()
      .then((data) => { if (!cancelled) setContacts(data); })
      .catch(() => {})
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const filteredContacts = searchQuery.trim()
    ? contacts.filter(
        (c) =>
          c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          c.email.toLowerCase().includes(searchQuery.toLowerCase()) ||
          (c.phone && c.phone.includes(searchQuery))
      )
    : contacts;

  useEffect(() => {
    if (dialogOpen) getCompanies().then(setCompanies).catch(() => {});
  }, [dialogOpen]);

  const openCreate = () => {
    setEditingContact(null);
    setForm({ name: '', email: '', phone: '', companyId: '' });
    setDialogOpen(true);
  };

  const openEdit = (contact: Contact, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingContact(contact);
    setForm({
      name: contact.name,
      email: contact.email,
      phone: contact.phone ?? '',
      companyId: contact.companyId ?? '',
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
        const updated = await updateContact(editingContact.id, {
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
        });
        if (updated) {
          setContacts((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
          toast.success(messages.success.contactUpdated);
          setDialogOpen(false);
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createContact({
          name: form.name.trim(),
          email: form.email.trim(),
          phone: form.phone.trim() || undefined,
          companyId: form.companyId || undefined,
        });
        if (created) {
          setContacts((prev) => [created, ...prev]);
          toast.success(messages.success.contactCreated);
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
    <div className="min-h-screen bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-8" tabIndex={-1}>
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Contacts</h1>
            <p className="text-slate-600 mt-1">
            {!loading && contacts.length > 0
              ? `${filteredContacts.length === contacts.length ? contacts.length : `${filteredContacts.length} of ${contacts.length}`} contact${contacts.length === 1 ? '' : 's'}`
              : 'View your contacts. Used when sending copy to CRM and filtering activities.'}
            </p>
          </div>
          <Button onClick={openCreate} className="gap-2 bg-orange-600 hover:bg-orange-500 shrink-0">
            <Plus className="w-4 h-4" />
            Add contact
          </Button>
        </div>

        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" aria-hidden />
            <Input
              type="search"
              placeholder="Search by name, email, or phone..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-10 rounded-lg border-slate-300"
              aria-label="Search contacts"
            />
          </div>
          {searchQuery.trim() && filteredContacts.length === 0 && !loading && (
            <p className="text-sm text-slate-500 mt-2">
              No results for &quot;{searchQuery.trim()}&quot;. Try a different search or add a contact.
            </p>
          )}
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : filteredContacts.length === 0 ? (
          <EmptyState
            icon={UserCircle}
            title={contacts.length === 0 ? 'No contacts yet' : 'No results found'}
            description={
              contacts.length === 0
                ? 'Contacts appear when you sync your CRM or send copy to a contact.'
                : 'Try a different search.'
            }
          />
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <ul className="divide-y divide-slate-100">
              {filteredContacts.map((contact) => (
                <li
                  key={contact.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}
                  onKeyDown={(e) => e.key === 'Enter' && navigate('/send', { state: { contactId: contact.id, contactName: contact.name } })}
                  className="flex items-center gap-3 px-4 py-4 hover:bg-slate-50/50 transition-colors cursor-pointer"
                >
                  <div className="shrink-0 w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-600">
                    <UserCircle className="w-5 h-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-slate-900 truncate">{contact.name}</p>
                    <p className="text-sm text-slate-600 truncate">{contact.email}</p>
                    {contact.phone && (
                      <p className="text-xs text-slate-500 mt-0.5">{contact.phone}</p>
                    )}
                    {contact.lastActivityAtUtc && (() => {
                      try {
                        const d = new Date(contact.lastActivityAtUtc);
                        if (Number.isNaN(d.getTime())) return null;
                        return (
                          <p className="text-xs text-slate-400 mt-0.5">
                            Last activity: {d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                          </p>
                        );
                      } catch {
                        return null;
                      }
                    })()}
                  </div>
                  <Button variant="outline" size="sm" onClick={(e) => openEdit(contact, e)} className="gap-1.5 shrink-0" aria-label={`Edit ${contact.name}`}>
                    <Pencil className="w-4 h-4" />
                    Edit
                  </Button>
                </li>
              ))}
            </ul>
          </div>
        )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingContact ? 'Edit contact' : 'Add contact'}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label htmlFor="contact-name">Name *</Label>
              <Input
                id="contact-name"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="Full name"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="contact-email">Email *</Label>
              <Input
                id="contact-email"
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                placeholder="email@example.com"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="contact-phone">Phone</Label>
              <Input
                id="contact-phone"
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
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Saving...' : editingContact ? 'Update' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
      </main>
    </div>
  );
}
