// An establishment's sidebar on the lead page: where it is, how to reach it and what it
// is, then the person to speak to there. Every field edits in place, as a person's do.

import { toast } from 'sonner';
import { Armchair, Building, ExternalLink, Globe, Hash, Mail, MapPin, Phone, Tag, UserRound } from 'lucide-react';
import type { Lead } from '@/app/api/types';
import { establishmentAddress, establishmentMapUrl, establishmentTypeLabel, websiteHref, websiteLabel } from '@/app/lib/leadKinds';
import { EstablishmentTypeSelect } from '../components/LeadKindFields';
import { InlineField } from './InlineField';

type Patch = (patch: Record<string, unknown>, log?: { subject: string; body?: string }) => Promise<boolean>;

const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Emptied, a field is sent as null, which takes it off. */
const orNull = (v: string) => v.trim() || null;

function emailSaver(patch: Patch, field: 'email' | 'contactPersonEmail', subject: string) {
  return (v: string) => {
    if (v.trim() && !EMAIL_SHAPE.test(v.trim())) {
      toast.error('That is not an email address.');
      return Promise.resolve(false);
    }
    // An establishment's own email may be empty, and is then "", as the server keeps it.
    return patch({ [field]: field === 'email' ? v.trim() : orNull(v) }, { subject });
  };
}

/** What the establishment is, where, and how to reach it. */
export function EstablishmentDetails({ lead, readOnly, patch }: { lead: Lead; readOnly: boolean; patch: Patch }) {
  const mapUrl = establishmentMapUrl(lead);
  const site = websiteHref(lead.website);
  const saveSeats = (v: string) => {
    if (v.trim() && !/^\d{1,5}$/.test(v.trim())) {
      toast.error('Seats is a whole number.');
      return Promise.resolve(false);
    }
    return patch({ seats: v.trim() ? Number(v.trim()) : null }, { subject: 'Seats updated' });
  };
  return (
    <div className="space-y-2.5">
      <InlineField label="Name" value={lead.name} onSave={(v) => patch({ name: v }, { subject: 'Name updated' })} readOnly={readOnly} icon={<Building className="w-3.5 h-3.5" />} />
      <div>
        <div className="mb-1 flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          <Tag className="w-3 h-3" aria-hidden />
          Type
        </div>
        {readOnly ? (
          <p className="text-sm text-slate-800">{lead.establishmentType ? establishmentTypeLabel(lead.establishmentType) : 'Not set'}</p>
        ) : (
          <EstablishmentTypeSelect
            id="lead-establishment-type"
            value={lead.establishmentType ?? ''}
            onChange={(v) => void patch({ establishmentType: v || null }, { subject: 'Type updated', body: establishmentTypeLabel(v) })}
          />
        )}
      </div>
      <div className="rounded-xl bg-violet-50/50 p-2.5 ring-1 ring-violet-100">
        <div className="mb-1.5 flex items-center justify-between gap-2">
          <span className="flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wider text-violet-700">
            <MapPin className="w-3 h-3" aria-hidden />
            Address
          </span>
          {mapUrl && (
            <a
              href={mapUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 rounded px-1 text-[11px] font-semibold text-violet-700 hover:text-violet-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400"
              aria-label={`Open ${establishmentAddress(lead)} in Google Maps`}
            >
              Map <ExternalLink className="w-3 h-3" aria-hidden />
            </a>
          )}
        </div>
        <div className="space-y-2">
          <InlineField label="Street" value={lead.addressLine ?? ''} onSave={(v) => patch({ addressLine: orNull(v) }, { subject: 'Address updated' })} readOnly={readOnly} />
          <div className="grid grid-cols-[5.5rem_minmax(0,1fr)] gap-2">
            <InlineField label="Postal code" value={lead.postalCode ?? ''} onSave={(v) => patch({ postalCode: orNull(v) }, { subject: 'Postal code updated' })} readOnly={readOnly} />
            <InlineField label="City" value={lead.city ?? ''} onSave={(v) => patch({ city: orNull(v) }, { subject: 'City updated' })} readOnly={readOnly} />
          </div>
          <InlineField label="Country" value={lead.country ?? ''} onSave={(v) => patch({ country: orNull(v) }, { subject: 'Country updated' })} readOnly={readOnly} />
        </div>
      </div>
      <InlineField label="Phone" type="tel" value={lead.phone ?? ''} onSave={(v) => patch({ phone: orNull(v) }, { subject: 'Phone updated' })} readOnly={readOnly} icon={<Phone className="w-3.5 h-3.5" />} />
      <InlineField label="Email" type="email" value={lead.email ?? ''} onSave={emailSaver(patch, 'email', 'Email updated')} readOnly={readOnly} icon={<Mail className="w-3.5 h-3.5" />} emptyHint="No email of its own" />
      <InlineField
        label="Website"
        type="url"
        value={lead.website ?? ''}
        onSave={(v) => patch({ website: orNull(v) }, { subject: 'Website updated' })}
        readOnly={readOnly}
        icon={<Globe className="w-3.5 h-3.5" />}
        renderValue={(v) => (site ? (
          <a href={site} target="_blank" rel="noopener noreferrer" onClick={(e) => e.stopPropagation()} className="text-violet-700 underline-offset-2 hover:underline">
            {websiteLabel(v)}
          </a>
        ) : v)}
      />
      <InlineField label="Seats" type="number" value={lead.seats != null ? String(lead.seats) : ''} onSave={saveSeats} readOnly={readOnly} icon={<Armchair className="w-3.5 h-3.5" />} />
    </div>
  );
}

/** The person to speak to at the establishment. */
export function ContactPersonDetails({ lead, readOnly, patch }: { lead: Lead; readOnly: boolean; patch: Patch }) {
  return (
    <div className="space-y-2.5">
      <InlineField label="Name" value={lead.contactPersonName ?? ''} onSave={(v) => patch({ contactPersonName: orNull(v) }, { subject: 'Contact person updated' })} readOnly={readOnly} icon={<UserRound className="w-3.5 h-3.5" />} emptyHint="Who do you speak to there?" />
      <InlineField label="Role" value={lead.contactPersonRole ?? ''} onSave={(v) => patch({ contactPersonRole: orNull(v) }, { subject: 'Contact person’s role updated' })} readOnly={readOnly} icon={<Hash className="w-3.5 h-3.5" />} placeholder="Owner, manager, chef…" />
      <InlineField label="Email" type="email" value={lead.contactPersonEmail ?? ''} onSave={emailSaver(patch, 'contactPersonEmail', 'Contact person’s email updated')} readOnly={readOnly} icon={<Mail className="w-3.5 h-3.5" />} />
      <InlineField label="Phone" type="tel" value={lead.contactPersonPhone ?? ''} onSave={(v) => patch({ contactPersonPhone: orNull(v) }, { subject: 'Contact person’s phone updated' })} readOnly={readOnly} icon={<Phone className="w-3.5 h-3.5" />} />
      <p className="text-[11px] text-slate-500">With a name and an email, they are kept as a contact, linked to the establishment.</p>
    </div>
  );
}
