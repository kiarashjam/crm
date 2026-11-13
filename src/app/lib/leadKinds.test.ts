// A lead is a person or an establishment, and the workspace says which it works with.
// People only is the default: anything the server does not say (an older server, a
// personal workspace) must read as people, so Pavillon 46 never sees an establishment.
import { describe, expect, it } from 'vitest';
import {
  convertRecipient, defaultLeadKind, establishmentAddress, establishmentMapUrl, establishmentSummary, establishmentTypeLabel,
  isEstablishment, leadKindsMode, leadReachEmail, leadReachPhone, websiteHref, websiteLabel,
} from './leadKinds';

const trattoria = {
  name: 'Trattoria Bella',
  kind: 'establishment' as const,
  email: '',
  establishmentType: 'restaurant',
  addressLine: 'Rue du Lac 4',
  postalCode: '1003',
  city: 'Lausanne',
  country: 'Switzerland',
  contactPersonName: 'Marco Rossi',
  contactPersonEmail: 'marco@trattoria.ch',
  contactPersonPhone: '+41 79 000 00 00',
};

describe('what a workspace’s leads are', () => {
  it('reads anything but establishment or both as people', () => {
    expect(leadKindsMode(undefined)).toBe('person');
    expect(leadKindsMode(null)).toBe('person');
    expect(leadKindsMode('')).toBe('person');
    expect(leadKindsMode('organisations')).toBe('person');
    expect(leadKindsMode('establishment')).toBe('establishment');
    expect(leadKindsMode('both')).toBe('both');
  });

  it('starts a new lead as an establishment only where every lead is one', () => {
    expect(defaultLeadKind('person')).toBe('person');
    expect(defaultLeadKind('both')).toBe('person');
    expect(defaultLeadKind('establishment')).toBe('establishment');
  });

  it('treats a lead with no kind as a person', () => {
    expect(isEstablishment({})).toBe(false);
    expect(isEstablishment({ kind: 'person' })).toBe(false);
    expect(isEstablishment({ kind: 'establishment' })).toBe(true);
  });
});

describe('an establishment, as the screens say it', () => {
  it('writes its address as a letter has it, from the parts given', () => {
    expect(establishmentAddress(trattoria)).toBe('Rue du Lac 4, 1003 Lausanne, Switzerland');
    expect(establishmentAddress({ city: 'Genève' })).toBe('Genève');
    expect(establishmentAddress({})).toBe('');
  });

  it('links its address to a map, and nothing without one', () => {
    expect(establishmentMapUrl(trattoria)).toBe(
      'https://www.google.com/maps/search/?api=1&query=Trattoria%20Bella%2C%20Rue%20du%20Lac%204%2C%201003%20Lausanne%2C%20Switzerland',
    );
    expect(establishmentMapUrl({ name: 'Nowhere' })).toBeUndefined();
  });

  it('says what it is and where', () => {
    expect(establishmentSummary(trattoria)).toBe('Restaurant · Lausanne');
    expect(establishmentSummary({ establishmentType: 'cafe' })).toBe('Café');
    expect(establishmentTypeLabel(undefined)).toBe('Establishment');
  });

  it('is reached at its own email and number, else its contact person’s', () => {
    expect(leadReachEmail(trattoria)).toBe('marco@trattoria.ch');
    expect(leadReachEmail({ ...trattoria, email: 'info@trattoria.ch' })).toBe('info@trattoria.ch');
    expect(leadReachPhone(trattoria)).toBe('+41 79 000 00 00');
    expect(leadReachPhone({ ...trattoria, phone: '+41 21 000 00 00' })).toBe('+41 21 000 00 00');
    // A person has no contact person to fall back on.
    expect(leadReachEmail({ kind: 'person', email: '', contactPersonEmail: 'x@example.ch' })).toBeUndefined();
  });

  it('converts into a contact for its contact person', () => {
    expect(convertRecipient(trattoria)).toEqual({ name: 'Marco Rossi', email: 'marco@trattoria.ch' });
    expect(convertRecipient({ name: 'Ada', email: 'ada@example.ch' })).toEqual({ name: 'Ada', email: 'ada@example.ch' });
  });

  it('opens a website typed without its scheme, and shows it as people write it', () => {
    expect(websiteHref('trattoria.ch')).toBe('https://trattoria.ch');
    expect(websiteHref('http://trattoria.ch')).toBe('http://trattoria.ch');
    expect(websiteHref('  ')).toBeUndefined();
    expect(websiteLabel('https://trattoria.ch/')).toBe('trattoria.ch');
  });
});
