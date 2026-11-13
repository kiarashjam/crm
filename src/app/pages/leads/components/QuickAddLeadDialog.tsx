import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Loader2, Sparkles, Store, UserPlus, X } from 'lucide-react';
import { AnimatePresence, motion } from 'motion/react';
import { Dialog, DialogContent, DialogTitle } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { useLeadKinds } from '@/app/lib/leadKinds';
import type { LeadKind } from '@/app/api/types';
import type { LeadProfileFields } from '@/app/api/leads';
import {
  EMPTY_ESTABLISHMENT, EstablishmentFields, LeadKindChooser, establishmentPayload, establishmentProblem, type EstablishmentDraft,
} from './LeadKindFields';

interface StatusOption {
  id: string;
  name: string;
}

interface SourceOption {
  id: string;
  name: string;
}

interface CompanyOption {
  id: string;
  name: string;
}

/** What quick add hands on: a person's or an establishment's fields, and the shared ones. */
export type QuickLeadInput = LeadProfileFields & {
  kind: LeadKind;
  name: string;
  email: string;
  phone?: string;
  companyId?: string;
  source?: string;
  status?: string;
  description?: string;
};

interface QuickAddLeadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Persist the lead. Resolve once it's saved; resolve `false` to keep the dialog open. */
  onSubmit: (input: QuickLeadInput) => Promise<boolean | void>;
  /** Switch over to the full multi-step editor without losing what the user typed. */
  onOpenFullEditor: (prefill: QuickLeadInput) => void;
  statusOptions: StatusOption[];
  sourceOptions: SourceOption[];
  companies: CompanyOption[];
}

/**
 * Single-page replacement for the 4-step wizard. Required fields only by default
 * (name + email); the rest is collapsed behind a "More fields" expander. The
 * full multi-step editor is one click away for anyone who wants it.
 */
export function QuickAddLeadDialog({
  open,
  onOpenChange,
  onSubmit,
  onOpenFullEditor,
  statusOptions,
  sourceOptions,
  companies,
}: QuickAddLeadDialogProps) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [source, setSource] = useState('');
  const [status, setStatus] = useState('');
  const [description, setDescription] = useState('');
  const [showMore, setShowMore] = useState(false);
  const [saving, setSaving] = useState(false);
  const nameRef = useRef<HTMLInputElement | null>(null);
  // What the lead is: asked first only where the workspace has both people and establishments.
  const { choose, defaultKind } = useLeadKinds();
  const [kind, setKind] = useState<LeadKind>(defaultKind);
  const [place, setPlace] = useState<EstablishmentDraft>(EMPTY_ESTABLISHMENT);
  const establishment = kind === 'establishment';

  // Reset on open so each invocation is a fresh form.
  useEffect(() => {
    if (open) {
      setName('');
      setEmail('');
      setPhone('');
      setCompanyId('');
      setSource('');
      setStatus(statusOptions[0]?.name ?? '');
      setDescription('');
      setShowMore(false);
      setKind(defaultKind);
      setPlace(EMPTY_ESTABLISHMENT);
      // Focus the name field for fastest possible entry.
      setTimeout(() => nameRef.current?.focus(), 30);
    }
  }, [open, statusOptions, defaultKind]);

  // A person needs a name and an email; an establishment its name (see establishmentProblem).
  const problem = establishment ? establishmentProblem(place) : null;
  const valid = establishment ? problem === null : name.trim().length > 0 && email.trim().length > 0;

  /** Everything typed, whichever the lead is, for saving or for the full editor. */
  const collected = (): QuickLeadInput => (establishment
    ? {
      ...establishmentPayload(place),
      source: source || undefined,
      status: status || undefined,
      description: description.trim() || undefined,
    }
    : {
      kind: 'person',
      name: name.trim(),
      email: email.trim(),
      phone: phone.trim() || undefined,
      companyId: companyId || undefined,
      source: source || undefined,
      status: status || undefined,
      description: description.trim() || undefined,
    });

  const handleSubmit = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!valid || saving) return;
    setSaving(true);
    try {
      const saved = await onSubmit(collected());
      if (saved !== false) onOpenChange(false);
    } finally {
      setSaving(false);
    }
  };

  const switchToFullEditor = () => {
    onOpenChange(false);
    onOpenFullEditor(collected());
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent showCloseButton={false} className="sm:max-w-[520px] p-0 gap-0 overflow-hidden">
        <div className="relative overflow-hidden">
          <div className="absolute inset-0 bg-gradient-to-br from-orange-500 via-amber-500 to-rose-500" />
          <div className="absolute inset-0 bg-grid-white/10" />
          <div className="relative flex items-center justify-between p-5 text-white">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm">
                {establishment ? <Store className="h-5 w-5" /> : <UserPlus className="h-5 w-5" />}
              </div>
              <div>
                <DialogTitle className="text-base font-semibold">Quick add lead</DialogTitle>
                <p className="text-xs text-white/80 mt-0.5">
                  Just the essentials — you can edit more details later.
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => onOpenChange(false)}
              className="rounded-lg p-1.5 text-white/80 hover:bg-white/10 hover:text-white transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          {choose && <LeadKindChooser value={kind} onChange={setKind} compact />}

          <AnimatePresence mode="wait" initial={false}>
          {establishment ? (
            <motion.div
              key="establishment"
              initial={{ opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -6 }}
              transition={{ duration: 0.18 }}
              className="space-y-3"
            >
              <EstablishmentFields value={place} onChange={setPlace} idPrefix="quick-add-est" parts={['essentials']} nameRef={nameRef} />
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <Label htmlFor="quick-add-est-city" className="text-xs font-semibold text-slate-700">City</Label>
                  <Input id="quick-add-est-city" value={place.city} onChange={(e) => setPlace({ ...place, city: e.target.value })} placeholder="Lausanne" className="mt-1" />
                </div>
                <div>
                  <Label htmlFor="quick-add-est-phone" className="text-xs font-semibold text-slate-700">Phone</Label>
                  <Input id="quick-add-est-phone" type="tel" value={place.phone} onChange={(e) => setPlace({ ...place, phone: e.target.value })} placeholder="+41 21 000 00 00" className="mt-1" />
                </div>
                <div>
                  <Label htmlFor="quick-add-est-cp-name" className="text-xs font-semibold text-slate-700">Contact person</Label>
                  <Input id="quick-add-est-cp-name" value={place.contactPersonName} onChange={(e) => setPlace({ ...place, contactPersonName: e.target.value })} placeholder="Marco Rossi" className="mt-1" />
                </div>
                <div>
                  <Label htmlFor="quick-add-est-cp-email" className="text-xs font-semibold text-slate-700">Their email</Label>
                  <Input id="quick-add-est-cp-email" type="email" value={place.contactPersonEmail} onChange={(e) => setPlace({ ...place, contactPersonEmail: e.target.value })} placeholder="marco@trattoria.ch" className="mt-1" />
                </div>
              </div>
              {problem && place.name.trim() && <p role="alert" className="text-xs text-rose-600">{problem}</p>}
            </motion.div>
          ) : (
          <motion.div
            key="person"
            initial={choose ? { opacity: 0, y: 6 } : false}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.18 }}
            className="space-y-4"
          >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <Label htmlFor="quick-add-name" className="text-xs font-semibold text-slate-700">
                Name <span className="text-red-700">*</span>
              </Label>
              <Input
                id="quick-add-name"
                ref={nameRef}
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Jane Doe"
                required
                className="mt-1"
              />
            </div>
            <div>
              <Label htmlFor="quick-add-email" className="text-xs font-semibold text-slate-700">
                Email <span className="text-red-700">*</span>
              </Label>
              <Input
                id="quick-add-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="jane@acme.com"
                required
                className="mt-1"
              />
            </div>
          </div>

          <div>
            <Label htmlFor="quick-add-phone" className="text-xs font-semibold text-slate-700">
              Phone (optional)
            </Label>
            <Input
              id="quick-add-phone"
              type="tel"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="+1 555 123 4567"
              className="mt-1"
            />
          </div>
          </motion.div>
          )}
          </AnimatePresence>

          <button
            type="button"
            onClick={() => setShowMore((v) => !v)}
            className="flex items-center gap-1.5 text-sm font-medium text-indigo-600 hover:text-indigo-700"
          >
            <ChevronDown
              className={`h-4 w-4 transition-transform ${showMore ? 'rotate-180' : ''}`}
            />
            {showMore ? 'Hide' : 'Show'} more fields
          </button>

          {showMore && (
            <div className="space-y-3 rounded-xl border border-slate-200 bg-slate-50/50 p-3">
              {establishment && (
                <>
                  {/* The rest of the address: the city is asked above. */}
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-[minmax(0,1fr)_6rem]">
                    <div>
                      <Label htmlFor="quick-add-est-street" className="text-xs font-semibold text-slate-700">Street and number</Label>
                      <Input id="quick-add-est-street" value={place.addressLine} onChange={(e) => setPlace({ ...place, addressLine: e.target.value })} placeholder="Rue du Lac 4" autoComplete="street-address" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-postal" className="text-xs font-semibold text-slate-700">Postal code</Label>
                      <Input id="quick-add-est-postal" value={place.postalCode} onChange={(e) => setPlace({ ...place, postalCode: e.target.value })} placeholder="1003" autoComplete="postal-code" className="mt-1 bg-white" />
                    </div>
                  </div>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <Label htmlFor="quick-add-est-country" className="text-xs font-semibold text-slate-700">Country</Label>
                      <Input id="quick-add-est-country" value={place.country} onChange={(e) => setPlace({ ...place, country: e.target.value })} placeholder="Switzerland" autoComplete="country-name" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-seats" className="text-xs font-semibold text-slate-700">Seats</Label>
                      <Input id="quick-add-est-seats" inputMode="numeric" value={place.seats} onChange={(e) => setPlace({ ...place, seats: e.target.value.replace(/[^\d]/g, '').slice(0, 5) })} placeholder="60" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-email" className="text-xs font-semibold text-slate-700">Establishment email</Label>
                      <Input id="quick-add-est-email" type="email" value={place.email} onChange={(e) => setPlace({ ...place, email: e.target.value })} placeholder="info@trattoria.ch" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-website" className="text-xs font-semibold text-slate-700">Website</Label>
                      <Input id="quick-add-est-website" value={place.website} onChange={(e) => setPlace({ ...place, website: e.target.value })} placeholder="trattoria.ch" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-role" className="text-xs font-semibold text-slate-700">Contact person’s role</Label>
                      <Input id="quick-add-est-role" value={place.contactPersonRole} onChange={(e) => setPlace({ ...place, contactPersonRole: e.target.value })} placeholder="Owner, manager…" className="mt-1 bg-white" />
                    </div>
                    <div>
                      <Label htmlFor="quick-add-est-cp-phone" className="text-xs font-semibold text-slate-700">Contact person’s phone</Label>
                      <Input id="quick-add-est-cp-phone" type="tel" value={place.contactPersonPhone} onChange={(e) => setPlace({ ...place, contactPersonPhone: e.target.value })} placeholder="+41 79 000 00 00" className="mt-1 bg-white" />
                    </div>
                  </div>
                </>
              )}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <Label className="text-xs font-semibold text-slate-700">Status</Label>
                  <Select value={status} onValueChange={setStatus}>
                    <SelectTrigger aria-label="Status" className="mt-1 bg-white">
                      <SelectValue placeholder="Pick a status" />
                    </SelectTrigger>
                    <SelectContent>
                      {statusOptions.map((s) => (
                        <SelectItem key={s.id} value={s.name}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label className="text-xs font-semibold text-slate-700">Source</Label>
                  <Select value={source} onValueChange={setSource}>
                    <SelectTrigger aria-label="Source" className="mt-1 bg-white">
                      <SelectValue placeholder="How did they find you?" />
                    </SelectTrigger>
                    <SelectContent>
                      {sourceOptions.map((s) => (
                        <SelectItem key={s.id} value={s.name}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {!establishment && <div>
                <Label className="text-xs font-semibold text-slate-700">Company</Label>
                <Select value={companyId} onValueChange={setCompanyId}>
                  <SelectTrigger aria-label="Company" className="mt-1 bg-white">
                    <SelectValue placeholder="No company" />
                  </SelectTrigger>
                  <SelectContent>
                    {companies.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>}

              <div>
                <Label htmlFor="quick-add-desc" className="text-xs font-semibold text-slate-700">
                  Notes
                </Label>
                <textarea
                  id="quick-add-desc"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                  className="mt-1 w-full resize-none rounded-md border border-slate-300 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-orange-400 focus:outline-none focus:ring-2 focus:ring-orange-400/30"
                  placeholder="Anything worth remembering"
                />
              </div>
            </div>
          )}

          <div className="flex items-center justify-between gap-3 pt-2 border-t border-slate-100">
            <button
              type="button"
              onClick={switchToFullEditor}
              className="text-sm text-slate-500 hover:text-indigo-600 hover:underline"
            >
              Need more fields? Open full editor
            </button>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => onOpenChange(false)}
                disabled={saving}
              >
                Cancel
              </Button>
              <Button
                type="submit"
                size="sm"
                disabled={!valid || saving}
                className="gap-1.5 bg-gradient-to-r from-orange-500 to-amber-500 text-white hover:from-orange-600 hover:to-amber-600"
              >
                {saving ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Sparkles className="h-4 w-4" />
                )}
                Create lead
              </Button>
            </div>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
