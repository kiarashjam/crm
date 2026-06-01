import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { UserPlus, Loader2, Mail, Phone, Building2, Briefcase, AlertCircle, Check } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { createContact, createActivity } from '@/app/api';
import type { Lead, Contact } from '@/app/api/types';

interface SaveLeadAsContactDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  lead: Lead;
  companyName?: string;
  /** Existing contacts, used to warn about a duplicate email. */
  existingContacts?: Contact[];
  /** Called after the contact is created (e.g. to refresh the timeline). */
  onCreated?: (contact: Contact) => void;
}

/**
 * Creates a Contact from a lead's details. Shows a review of exactly what will
 * be created (editable), warns on a duplicate email, and on confirm persists
 * the contact + logs an activity on the lead.
 */
export default function SaveLeadAsContactDialog({
  open, onOpenChange, lead, companyName, existingContacts = [], onCreated,
}: SaveLeadAsContactDialogProps) {
  const navigate = useNavigate();
  const [name, setName] = useState(lead.name);
  const [email, setEmail] = useState(lead.email);
  const [phone, setPhone] = useState(lead.phone ?? '');
  const [jobTitle, setJobTitle] = useState('');
  const [description, setDescription] = useState(lead.description ?? '');
  const [saving, setSaving] = useState(false);

  // Re-seed from the lead each time the dialog opens.
  useEffect(() => {
    if (open) {
      setName(lead.name);
      setEmail(lead.email);
      setPhone(lead.phone ?? '');
      setJobTitle('');
      setDescription(lead.description ?? '');
    }
  }, [open, lead]);

  const duplicate = email.trim()
    ? existingContacts.find((c) => c.email?.trim().toLowerCase() === email.trim().toLowerCase()) ?? null
    : null;

  const handleCreate = async () => {
    if (!name.trim() || !email.trim()) { toast.error('Name and email are required'); return; }
    setSaving(true);
    try {
      const { contact, error } = await createContact({
        name: name.trim(),
        email: email.trim(),
        phone: phone.trim() || undefined,
        jobTitle: jobTitle.trim() || undefined,
        companyId: lead.companyId || undefined,
        description: description.trim() || undefined,
      });
      if (error || !contact) { toast.error(error || 'Failed to create contact'); return; }

      // Record it on the lead's timeline (best-effort).
      createActivity({ type: 'system', subject: 'Saved as contact', body: contact.name, leadId: lead.id }).catch(() => {});

      toast.success('Contact created', {
        action: { label: 'View', onClick: () => navigate(`/contacts/${contact.id}`) },
      });
      onCreated?.(contact);
      onOpenChange(false);
    } catch {
      toast.error('Failed to create contact');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <UserPlus className="h-5 w-5 text-indigo-600" /> Save as contact
          </DialogTitle>
          <DialogDescription>
            Review the details and create a contact in your contact list from this lead.
          </DialogDescription>
        </DialogHeader>

        {duplicate && (
          <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5 text-sm text-amber-800" role="alert">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
            <span>
              A contact with this email already exists.{' '}
              <button type="button" onClick={() => navigate(`/contacts/${duplicate.id}`)} className="font-semibold underline underline-offset-2 hover:text-amber-900">
                View {duplicate.name}
              </button>
              . Creating will add a second entry.
            </span>
          </div>
        )}

        <div className="space-y-3">
          <div>
            <Label htmlFor="sc-name">Name</Label>
            <Input id="sc-name" value={name} onChange={(e) => setName(e.target.value)} className="mt-1" />
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="sc-email" className="flex items-center gap-1.5"><Mail className="h-3.5 w-3.5 text-slate-400" /> Email</Label>
              <Input id="sc-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} className="mt-1" />
            </div>
            <div>
              <Label htmlFor="sc-phone" className="flex items-center gap-1.5"><Phone className="h-3.5 w-3.5 text-slate-400" /> Phone</Label>
              <Input id="sc-phone" value={phone} onChange={(e) => setPhone(e.target.value)} className="mt-1" placeholder="Optional" />
            </div>
          </div>
          <div>
            <Label htmlFor="sc-title" className="flex items-center gap-1.5"><Briefcase className="h-3.5 w-3.5 text-slate-400" /> Job title</Label>
            <Input id="sc-title" value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} className="mt-1" placeholder="Optional" />
          </div>
          {companyName && (
            <p className="flex items-center gap-1.5 text-xs text-slate-500">
              <Building2 className="h-3.5 w-3.5 text-slate-400" />
              Will be linked to <span className="font-medium text-slate-700">{companyName}</span>
            </p>
          )}
          <div>
            <Label htmlFor="sc-notes">Notes</Label>
            <Textarea id="sc-notes" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} className="mt-1 resize-none" placeholder="Optional" />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={handleCreate} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            Create contact
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
