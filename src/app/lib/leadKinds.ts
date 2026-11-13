// A lead is a person (someone with a name and an email, as every lead used to be) or an
// establishment: a business such as a restaurant, with its address and a person to speak
// to. Each workspace says which it works with (Organization.leadKinds): Pavillon 46's
// leads are people, Bonap's are restaurants. People only is the default, and a workspace
// on it sees nothing of establishments anywhere.

import type { ElementType } from 'react';
import { Building2, ChefHat, Coffee, Croissant, Hotel, Store, Truck, UtensilsCrossed, Wine } from 'lucide-react';
import type { Lead, LeadKind } from '@/app/api/types';
import { useOrgOptional } from '@/app/contexts/OrgContext';

export type LeadKindsMode = 'person' | 'establishment' | 'both';

export interface EstablishmentTypeOption {
  value: string;
  label: string;
  icon: ElementType;
}

/** The kinds of establishment, in the order they are offered. */
export const ESTABLISHMENT_TYPES: readonly EstablishmentTypeOption[] = [
  { value: 'restaurant', label: 'Restaurant', icon: UtensilsCrossed },
  { value: 'cafe', label: 'Café', icon: Coffee },
  { value: 'bar', label: 'Bar', icon: Wine },
  { value: 'hotel', label: 'Hotel', icon: Hotel },
  { value: 'bakery', label: 'Bakery', icon: Croissant },
  { value: 'catering', label: 'Catering', icon: ChefHat },
  { value: 'food_truck', label: 'Food truck', icon: Truck },
  { value: 'shop', label: 'Shop', icon: Store },
  { value: 'other', label: 'Other', icon: Building2 },
];

export function establishmentTypeOption(value?: string | null): EstablishmentTypeOption | undefined {
  return value ? ESTABLISHMENT_TYPES.find((t) => t.value === value) : undefined;
}

/** "Restaurant", or "Establishment" when no type was given. */
export function establishmentTypeLabel(value?: string | null): string {
  return establishmentTypeOption(value)?.label ?? 'Establishment';
}

export function leadKindOf(lead: Pick<Lead, 'kind'> | null | undefined): LeadKind {
  return lead?.kind === 'establishment' ? 'establishment' : 'person';
}

export function isEstablishment(lead: Pick<Lead, 'kind'> | null | undefined): boolean {
  return leadKindOf(lead) === 'establishment';
}

/** The workspace's mode, as the server sends it; anything else (an older server, demo, a personal workspace) is people only. */
export function leadKindsMode(value?: string | null): LeadKindsMode {
  return value === 'establishment' || value === 'both' ? value : 'person';
}

/** The kind a new lead starts as: an establishment only where the workspace's leads are all establishments. */
export function defaultLeadKind(mode: LeadKindsMode): LeadKind {
  return mode === 'establishment' ? 'establishment' : 'person';
}

/** What the current workspace's leads are, and what a new one starts as. */
export function useLeadKinds(): { mode: LeadKindsMode; defaultKind: LeadKind; choose: boolean; establishments: boolean } {
  const mode = leadKindsMode(useOrgOptional()?.currentOrg?.leadKinds);
  return {
    mode,
    defaultKind: defaultLeadKind(mode),
    // Only a workspace with both asks which a new lead is.
    choose: mode === 'both',
    // Whether establishments show anywhere: a people-only workspace sees none of it.
    establishments: mode !== 'person',
  };
}

type Place = Pick<Lead, 'addressLine' | 'postalCode' | 'city' | 'country'>;

/** "Rue du Lac 4, 1003 Lausanne, Switzerland": the parts given, in the order a letter has them. */
export function establishmentAddress(lead: Place): string {
  const town = [lead.postalCode, lead.city].map((p) => p?.trim()).filter(Boolean).join(' ');
  return [lead.addressLine?.trim(), town, lead.country?.trim()].filter(Boolean).join(', ');
}

/** A map search for the establishment's address, or nothing without one. */
export function establishmentMapUrl(lead: Place & Pick<Lead, 'name'>): string | undefined {
  const address = establishmentAddress(lead);
  if (!address) return undefined;
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${lead.name}, ${address}`)}`;
}

/** "Restaurant · Lausanne": what an establishment is and where, for a card's second line. */
export function establishmentSummary(lead: Pick<Lead, 'establishmentType' | 'city' | 'country'>): string {
  return [establishmentTypeLabel(lead.establishmentType), lead.city?.trim() || lead.country?.trim()].filter(Boolean).join(' · ');
}

/** The address to write to: the lead's own, else its contact person's. */
export function leadReachEmail(lead: Pick<Lead, 'email' | 'contactPersonEmail' | 'kind'>): string | undefined {
  const own = lead.email?.trim();
  if (own) return own;
  return isEstablishment(lead) ? lead.contactPersonEmail?.trim() || undefined : undefined;
}

/** The number to call: the lead's own, else its contact person's. */
export function leadReachPhone(lead: Pick<Lead, 'phone' | 'contactPersonPhone' | 'kind'>): string | undefined {
  const own = lead.phone?.trim();
  if (own) return own;
  return isEstablishment(lead) ? lead.contactPersonPhone?.trim() || undefined : undefined;
}

/** "https://" in front of a website typed without it, so a link opens it. */
export function websiteHref(website?: string | null): string | undefined {
  const w = website?.trim();
  if (!w) return undefined;
  return /^https?:\/\//i.test(w) ? w : `https://${w}`;
}

/** The website as people write it: no scheme, no trailing slash. */
export function websiteLabel(website?: string | null): string {
  return (website ?? '').trim().replace(/^https?:\/\//i, '').replace(/\/$/, '');
}

/** Who a conversion's contact and welcome are for: the person, or an establishment's contact person. */
export function convertRecipient(lead: Pick<Lead, 'name' | 'email' | 'kind' | 'contactPersonName' | 'contactPersonEmail'>): { name?: string; email?: string } {
  if (!isEstablishment(lead)) return { name: lead.name, email: lead.email };
  return { name: lead.contactPersonName?.trim() || undefined, email: lead.contactPersonEmail?.trim() || undefined };
}
