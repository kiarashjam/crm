// The full editor saves a person as it always did and an establishment with its own
// fields. An emptied field on an edit is sent as null (the server's merge-patch takes it
// off); a person's edit leaves an establishment's fields alone; and the kind is named on
// an edit only when it changes, so a lead of a kind the workspace no longer takes can
// still be saved.
import { describe, expect, it } from 'vitest';
import { EMPTY_LEAD_FORM } from './config';
import { leadFormProblem, leadProfilePayload, quickInputToForm } from './leadFormPayload';
import type { LeadForm } from './types';

const place: LeadForm = {
  ...EMPTY_LEAD_FORM,
  kind: 'establishment',
  name: 'Trattoria Bella',
  establishmentType: 'restaurant',
  city: ' Lausanne ',
  seats: '60',
  contactPersonName: 'Marco Rossi',
  contactPersonEmail: 'marco@trattoria.ch',
};

describe('leadFormProblem', () => {
  it('asks a person for a name and an email', () => {
    expect(leadFormProblem({ ...EMPTY_LEAD_FORM, name: 'Ada' })).toMatch(/name and an email/);
    expect(leadFormProblem({ ...EMPTY_LEAD_FORM, name: 'Ada', email: 'ada@example.ch' })).toBeNull();
  });

  it('asks an establishment for its name only, and checks what was typed', () => {
    expect(leadFormProblem({ ...EMPTY_LEAD_FORM, kind: 'establishment' })).toBe('Give the establishment a name.');
    expect(leadFormProblem({ ...EMPTY_LEAD_FORM, kind: 'establishment', name: 'Trattoria Bella' })).toBeNull();
    expect(leadFormProblem({ ...place, contactPersonEmail: 'marco at trattoria' })).toMatch(/contact person’s email/);
    expect(leadFormProblem({ ...place, email: 'info@' })).toMatch(/establishment’s email/);
  });
});

describe('leadProfilePayload', () => {
  it('sends an establishment’s fields on a create, leaving out the empty ones', () => {
    expect(leadProfilePayload(place, 'create')).toEqual({
      kind: 'establishment',
      establishmentType: 'restaurant',
      addressLine: undefined,
      postalCode: undefined,
      city: 'Lausanne',
      country: undefined,
      website: undefined,
      seats: 60,
      contactPersonName: 'Marco Rossi',
      contactPersonRole: undefined,
      contactPersonEmail: 'marco@trattoria.ch',
      contactPersonPhone: undefined,
    });
  });

  it('clears an emptied field on an edit, and names the kind only when it changes', () => {
    const edit = leadProfilePayload({ ...place, city: '', seats: '' }, 'update', 'establishment');
    expect(edit).not.toHaveProperty('kind');
    expect(edit.city).toBeNull();
    expect(edit.seats).toBeNull();
    expect(leadProfilePayload(place, 'update', 'person').kind).toBe('establishment');
  });

  it('sends nothing of an establishment for a person', () => {
    const create = leadProfilePayload({ ...EMPTY_LEAD_FORM, name: 'Ada', email: 'ada@example.ch' }, 'create');
    expect(create).toEqual({ kind: 'person' });
    const edit = leadProfilePayload({ ...EMPTY_LEAD_FORM, name: 'Ada', email: 'ada@example.ch', jobTitle: 'Engineer' }, 'update', 'person');
    expect(edit).toEqual({ jobTitle: 'Engineer', country: null });
  });
});

describe('quickInputToForm', () => {
  it('keeps everything quick add held when switching to the full editor', () => {
    const form = quickInputToForm({ kind: 'establishment', name: 'Trattoria Bella', email: '', city: 'Lausanne', seats: 60, contactPersonName: 'Marco Rossi' });
    expect(form).toMatchObject({ kind: 'establishment', name: 'Trattoria Bella', city: 'Lausanne', seats: '60', contactPersonName: 'Marco Rossi' });
  });
});
