// A lead's form as the API takes it, the same for every page that saves one: a person's
// fields for a person, an establishment's for an establishment.

import type { LeadKind } from '@/app/api/types';
import type { LeadProfileFields } from '@/app/api/leads';
import { establishmentProblem } from './components/LeadKindFields';
import type { LeadForm } from './types';
import type { QuickLeadInput } from './components/QuickAddLeadDialog';
import { EMPTY_LEAD_FORM } from './config';

/**
 * The kind and its own fields. On a create, an empty field is left out; on an update it is
 * sent as null, which takes it off, so a cleared address is cleared. A person's update
 * leaves an establishment's fields alone. An update names the kind only when it changes
 * (`was` is what the lead was), so a lead of a kind the workspace no longer takes can
 * still be edited.
 */
export function leadProfilePayload(form: LeadForm, mode: 'create' | 'update', was?: LeadKind): LeadProfileFields & { kind?: LeadKind } {
  const empty = mode === 'update' ? null : undefined;
  const t = (s: string) => s.trim() || empty;
  const kind = mode === 'update' && was === form.kind ? {} : { kind: form.kind };
  if (form.kind === 'establishment') {
    return {
      ...kind,
      establishmentType: t(form.establishmentType),
      addressLine: t(form.addressLine),
      postalCode: t(form.postalCode),
      city: t(form.city),
      country: t(form.country),
      website: t(form.website),
      seats: form.seats.trim() ? Number(form.seats.trim()) : empty,
      contactPersonName: t(form.contactPersonName),
      contactPersonRole: t(form.contactPersonRole),
      contactPersonEmail: t(form.contactPersonEmail),
      contactPersonPhone: t(form.contactPersonPhone),
    };
  }
  return {
    ...kind,
    // Sent only once typed: a person's job title and country were kept by the server but never editable here.
    ...(form.jobTitle.trim() || mode === 'update' ? { jobTitle: t(form.jobTitle) } : {}),
    ...(form.country.trim() || mode === 'update' ? { country: t(form.country) } : {}),
  };
}

/** Why the form cannot be saved, or null: a person needs a name and an email, an establishment its name. */
export function leadFormProblem(form: LeadForm): string | null {
  if (form.kind === 'establishment') return establishmentProblem(form);
  if (!form.name.trim() || !form.email.trim()) return 'A lead needs a name and an email.';
  return null;
}

/** What quick add holds, as the full editor's form, so switching over loses nothing. */
export function quickInputToForm(input: QuickLeadInput): LeadForm {
  const s = (v: string | number | null | undefined) => (v == null ? '' : String(v));
  return {
    ...EMPTY_LEAD_FORM,
    kind: input.kind,
    name: input.name,
    email: input.email,
    phone: s(input.phone),
    jobTitle: s(input.jobTitle),
    establishmentType: s(input.establishmentType),
    addressLine: s(input.addressLine),
    postalCode: s(input.postalCode),
    city: s(input.city),
    country: s(input.country),
    website: s(input.website),
    seats: s(input.seats),
    contactPersonName: s(input.contactPersonName),
    contactPersonRole: s(input.contactPersonRole),
    contactPersonEmail: s(input.contactPersonEmail),
    contactPersonPhone: s(input.contactPersonPhone),
    companyId: s(input.companyId),
    source: s(input.source),
    status: s(input.status),
    description: s(input.description),
  };
}
