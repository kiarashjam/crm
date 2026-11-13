// The pieces a lead's form is made of when leads can be establishments: the choice of
// what a lead is, and an establishment's questions (what it is, where, and who to speak
// to). Shared by quick add and the full editor, so both ask the same way.

import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Check, Store, UserRound } from 'lucide-react';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import { ESTABLISHMENT_TYPES } from '@/app/lib/leadKinds';
import type { LeadKind } from '@/app/api/types';

const KINDS: { value: LeadKind; label: string; text: string; icon: typeof UserRound; gradient: string }[] = [
  { value: 'person', label: 'Person', text: 'Someone with a name and an email', icon: UserRound, gradient: 'from-orange-500 to-pink-500' },
  { value: 'establishment', label: 'Establishment', text: 'A business: a restaurant, café, hotel…', icon: Store, gradient: 'from-violet-500 to-fuchsia-500' },
];

/** Person or establishment: the first question, where a workspace has both. */
export function LeadKindChooser({ value, onChange, compact }: { value: LeadKind; onChange: (kind: LeadKind) => void; compact?: boolean }) {
  return (
    <div role="radiogroup" aria-label="What is this lead?" className="grid grid-cols-2 gap-3">
      {KINDS.map((k, i) => {
        const selected = value === k.value;
        const Icon = k.icon;
        return (
          <motion.button
            key={k.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(k.value)}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.06, type: 'spring', stiffness: 320, damping: 24 }}
            whileHover={{ y: -2 }}
            whileTap={{ scale: 0.98 }}
            className={cn(
              'relative flex items-center gap-3 rounded-2xl border-2 text-left transition-colors',
              compact ? 'p-3' : 'p-4',
              selected ? 'border-orange-400 bg-gradient-to-br from-orange-50 via-white to-pink-50 shadow-md shadow-orange-100' : 'border-slate-200 bg-white hover:border-slate-300',
            )}
          >
            <span className={cn(
              'flex shrink-0 items-center justify-center rounded-xl text-white shadow-sm transition',
              compact ? 'h-9 w-9' : 'h-11 w-11',
              selected ? `bg-gradient-to-br ${k.gradient}` : 'bg-slate-200 text-slate-500',
            )}>
              <Icon className={compact ? 'h-4 w-4' : 'h-5 w-5'} aria-hidden />
            </span>
            <span className="min-w-0">
              <span className="block text-sm font-semibold text-slate-900">{k.label}</span>
              <span className="block text-xs text-slate-500">{k.text}</span>
            </span>
            <AnimatePresence>
              {selected && (
                <motion.span
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                  exit={{ scale: 0 }}
                  className="absolute right-2.5 top-2.5 flex h-5 w-5 items-center justify-center rounded-full bg-orange-500 text-white"
                >
                  <Check className="h-3 w-3" aria-hidden />
                </motion.span>
              )}
            </AnimatePresence>
          </motion.button>
        );
      })}
    </div>
  );
}

/** What an establishment's form holds, all as typed. */
export interface EstablishmentDraft {
  name: string;
  establishmentType: string;
  addressLine: string;
  postalCode: string;
  city: string;
  country: string;
  phone: string;
  email: string;
  website: string;
  seats: string;
  contactPersonName: string;
  contactPersonRole: string;
  contactPersonEmail: string;
  contactPersonPhone: string;
}

export const EMPTY_ESTABLISHMENT: EstablishmentDraft = {
  name: '', establishmentType: '', addressLine: '', postalCode: '', city: '', country: '', phone: '', email: '',
  website: '', seats: '', contactPersonName: '', contactPersonRole: '', contactPersonEmail: '', contactPersonPhone: '',
};

const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Why the establishment cannot be saved as typed, or null. */
export function establishmentProblem(d: Pick<EstablishmentDraft, 'name' | 'email' | 'contactPersonEmail' | 'seats'>): string | null {
  if (!d.name.trim()) return 'Give the establishment a name.';
  if (d.email.trim() && !EMAIL_SHAPE.test(d.email.trim())) return 'The establishment’s email is not an email address.';
  if (d.contactPersonEmail.trim() && !EMAIL_SHAPE.test(d.contactPersonEmail.trim())) return 'The contact person’s email is not an email address.';
  if (d.seats.trim() && !/^\d{1,5}$/.test(d.seats.trim())) return 'Seats is a whole number.';
  return null;
}

/** An establishment draft as the API takes it: empty fields left out, seats a number. */
export function establishmentPayload(d: EstablishmentDraft) {
  const t = (s: string) => s.trim() || undefined;
  return {
    kind: 'establishment' as const,
    name: d.name.trim(),
    email: d.email.trim(),
    phone: t(d.phone),
    establishmentType: t(d.establishmentType),
    addressLine: t(d.addressLine),
    postalCode: t(d.postalCode),
    city: t(d.city),
    country: t(d.country),
    website: t(d.website),
    seats: d.seats.trim() ? Number(d.seats.trim()) : undefined,
    contactPersonName: t(d.contactPersonName),
    contactPersonRole: t(d.contactPersonRole),
    contactPersonEmail: t(d.contactPersonEmail),
    contactPersonPhone: t(d.contactPersonPhone),
  };
}

function Field({ id, label, children, className }: { id: string; label: ReactNode; children: ReactNode; className?: string }) {
  return (
    <div className={className}>
      <Label htmlFor={id} className="text-xs font-semibold text-slate-700">{label}</Label>
      <div className="mt-1">{children}</div>
    </div>
  );
}

/** The establishment type, with its icon. */
export function EstablishmentTypeSelect({ id, value, onChange }: { id: string; value: string; onChange: (value: string) => void }) {
  return (
    <Select value={value || 'none'} onValueChange={(v) => onChange(v === 'none' ? '' : v)}>
      <SelectTrigger id={id} aria-label="Type of establishment" className="bg-white">
        <SelectValue placeholder="What kind?" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="none">— Not said —</SelectItem>
        {ESTABLISHMENT_TYPES.map((t) => (
          <SelectItem key={t.value} value={t.value}>
            <span className="flex items-center gap-2">
              <t.icon className="h-3.5 w-3.5 text-slate-500" aria-hidden />
              {t.label}
            </span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

type Part = 'essentials' | 'place' | 'reach' | 'contact';

/**
 * An establishment's questions, in parts, so quick add can show the essentials and the
 * full editor everything: what and where it is, how to reach it, and who to speak to.
 */
export function EstablishmentFields<T extends EstablishmentDraft>({
  value,
  onChange,
  idPrefix,
  parts = ['essentials', 'place', 'reach', 'contact'],
  nameRef,
}: {
  /** The draft, or a larger form holding its fields (the full editor's). */
  value: T;
  onChange: (next: T) => void;
  idPrefix: string;
  parts?: Part[];
  nameRef?: React.Ref<HTMLInputElement>;
}) {
  const set = (patch: Partial<EstablishmentDraft>) => onChange({ ...value, ...patch });
  const id = (f: string) => `${idPrefix}-${f}`;
  return (
    <div className="space-y-3">
      {parts.includes('essentials') && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field id={id('name')} label={<>Establishment name <span className="text-red-700">*</span></>}>
            <Input id={id('name')} ref={nameRef} value={value.name} onChange={(e) => set({ name: e.target.value })} placeholder="Trattoria Bella" required />
          </Field>
          <Field id={id('type')} label="Type">
            <EstablishmentTypeSelect id={id('type')} value={value.establishmentType} onChange={(v) => set({ establishmentType: v })} />
          </Field>
        </div>
      )}
      {parts.includes('place') && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-6">
          <Field id={id('street')} label="Street and number" className="sm:col-span-6">
            <Input id={id('street')} value={value.addressLine} onChange={(e) => set({ addressLine: e.target.value })} placeholder="Rue du Lac 4" autoComplete="street-address" />
          </Field>
          <Field id={id('postal')} label="Postal code" className="sm:col-span-2">
            <Input id={id('postal')} value={value.postalCode} onChange={(e) => set({ postalCode: e.target.value })} placeholder="1003" autoComplete="postal-code" />
          </Field>
          <Field id={id('city')} label="City" className="sm:col-span-2">
            <Input id={id('city')} value={value.city} onChange={(e) => set({ city: e.target.value })} placeholder="Lausanne" autoComplete="address-level2" />
          </Field>
          <Field id={id('country')} label="Country" className="sm:col-span-2">
            <Input id={id('country')} value={value.country} onChange={(e) => set({ country: e.target.value })} placeholder="Switzerland" autoComplete="country-name" />
          </Field>
        </div>
      )}
      {parts.includes('reach') && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field id={id('phone')} label="Phone">
            <Input id={id('phone')} type="tel" value={value.phone} onChange={(e) => set({ phone: e.target.value })} placeholder="+41 21 000 00 00" />
          </Field>
          <Field id={id('email')} label="Email (optional)">
            <Input id={id('email')} type="email" value={value.email} onChange={(e) => set({ email: e.target.value })} placeholder="info@trattoria.ch" />
          </Field>
          <Field id={id('website')} label="Website">
            <Input id={id('website')} value={value.website} onChange={(e) => set({ website: e.target.value })} placeholder="trattoria.ch" />
          </Field>
          <Field id={id('seats')} label="Seats">
            <Input id={id('seats')} inputMode="numeric" value={value.seats} onChange={(e) => set({ seats: e.target.value.replace(/[^\d]/g, '').slice(0, 5) })} placeholder="60" />
          </Field>
        </div>
      )}
      {parts.includes('contact') && (
        <fieldset className="rounded-xl border border-violet-100 bg-violet-50/40 p-3">
          <legend className="px-1 text-xs font-semibold text-violet-700">Contact person</legend>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field id={id('cp-name')} label="Name">
              <Input id={id('cp-name')} value={value.contactPersonName} onChange={(e) => set({ contactPersonName: e.target.value })} placeholder="Marco Rossi" />
            </Field>
            <Field id={id('cp-role')} label="Role">
              <Input id={id('cp-role')} value={value.contactPersonRole} onChange={(e) => set({ contactPersonRole: e.target.value })} placeholder="Owner, manager, chef…" />
            </Field>
            <Field id={id('cp-email')} label="Email">
              <Input id={id('cp-email')} type="email" value={value.contactPersonEmail} onChange={(e) => set({ contactPersonEmail: e.target.value })} placeholder="marco@trattoria.ch" />
            </Field>
            <Field id={id('cp-phone')} label="Phone">
              <Input id={id('cp-phone')} type="tel" value={value.contactPersonPhone} onChange={(e) => set({ contactPersonPhone: e.target.value })} placeholder="+41 79 000 00 00" />
            </Field>
          </div>
          <p className="mt-2 text-[11px] text-violet-700/80">With a name and an email, they become a contact, linked to the establishment.</p>
        </fieldset>
      )}
    </div>
  );
}
