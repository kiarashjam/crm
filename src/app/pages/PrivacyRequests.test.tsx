// Privacy requests, as the person who handles them uses the page: find someone (from one of
// their records or by email), choose which records are theirs, download everything held, or
// erase them after reading what it would do and typing ERASE; the retention periods and their
// run; and the settings, saved against the version read. Someone without privacy.manage is
// told so, and nothing is asked of the server.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type {
  ErasurePlan, ErasureResult, PersonFound, PersonSelection, PrivacySettings, RetentionPreview, RetentionRun,
} from '@/app/api/privacy';
import { ApiError } from '@/app/api/apiClient';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[] | 'owner', personal = false) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: personal ? null : 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: personal ? null : {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner',
      isOwner: permissions === 'owner', role: permissions === 'owner' ? 0 : 1,
      permissions: permissions === 'owner' ? undefined : permissions,
    },
  };
};

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({
  find: vi.fn(), exportPerson: vi.fn(), preview: vi.fn(), erase: vi.fn(), erasures: vi.fn(),
  settings: vi.fn(), save: vi.fn(), retention: vi.fn(), run: vi.fn(), runs: vi.fn(),
}));
vi.mock('@/app/api/privacy', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/privacy')>()),
  findPerson: (...a: unknown[]) => api.find(...a),
  exportPerson: (...a: unknown[]) => api.exportPerson(...a),
  previewErasure: (...a: unknown[]) => api.preview(...a),
  erasePerson: (...a: unknown[]) => api.erase(...a),
  getErasures: (...a: unknown[]) => api.erasures(...a),
  getPrivacySettings: (...a: unknown[]) => api.settings(...a),
  savePrivacySettings: (...a: unknown[]) => api.save(...a),
  getRetentionPreview: (...a: unknown[]) => api.retention(...a),
  runRetention: (...a: unknown[]) => api.run(...a),
  getRetentionRuns: (...a: unknown[]) => api.runs(...a),
}));

const { default: PrivacyRequests } = await import('./PrivacyRequests');

const FOUND: PersonFound = {
  addresses: ['anna@example.ch'],
  records: [
    { kind: 'lead', id: 'l1', name: 'Anna Keller', email: 'anna@example.ch', detail: 'New', foundBy: 'The record you started from' },
    { kind: 'member', id: 'm1', name: 'Anna Keller', email: 'anna@example.ch', detail: 'Gold, Active', foundBy: 'Same email address' },
    { kind: 'contact', id: 'c9', name: 'Anna Keller (old)', email: 'anna@example.ch', detail: null, foundBy: 'Same email address' },
  ],
};

const PLAN: ErasurePlan = {
  records: 2,
  lines: [
    { area: 'Leads', action: 'delete', count: 1, note: null },
    { area: 'Charges and payments', action: 'anonymise', count: 4, note: 'Amounts, dates and VAT stay, under the pseudonym.' },
    { area: 'Reservations still to come', action: 'cancel', count: 1, note: null },
  ],
  warnings: ['Anna Keller owes CHF 40.00. The debt stays in the books under the pseudonym, but nobody can bill or remind them any more.'],
  addressesSuppressed: ['anna@example.ch'],
  addressesShared: [],
};

const RESULT: ErasureResult = { number: 7, pseudonym: 'Erased member 7', erasedAtUtc: '2026-11-12T10:00:00Z', lines: PLAN.lines };

const SETTINGS: PrivacySettings = {
  controllerName: null, controllerAddress: null, controllerEmail: null, dpoContact: null,
  marketingConsent: 'optional',
  marketingConsentText: 'Yes, I would like to hear about news, events and offers by email. I can change my mind at any time.',
  marketingConsentTextIsDefault: true,
  leadRetentionMonths: null, memberRetentionYears: null, mailRetentionMonths: null, detailRetentionMonths: null,
  version: 'v1', updatedAtUtc: null, updatedByName: null,
};

const PREVIEW_OFF: RetentionPreview = {
  anythingOn: false,
  rules: [
    { rule: 'leads', period: null, unit: 'months', count: 0, what: 'Leads that never became a customer…' },
    { rule: 'members', period: null, unit: 'years', count: 0, what: 'Members who resigned…' },
    { rule: 'mail', period: null, unit: 'months', count: 0, what: 'Older sent emails lose their text…' },
    { rule: 'details', period: null, unit: 'months', count: 0, what: 'Older audit lines lose the name…' },
  ],
  kept: [],
};

let location = '';
function Where() {
  const l = useLocation();
  location = `${l.pathname}${l.search}`;
  return null;
}

const show = (url = '/privacy-requests') => render(withQueries(
  <MemoryRouter initialEntries={[url]}>
    <Routes><Route path="/privacy-requests" element={<><PrivacyRequests /><Where /></>} /></Routes>
  </MemoryRouter>,
));

beforeEach(() => {
  org.current = null;
  location = '';
  toast.success.mockReset();
  toast.error.mockReset();
  for (const fn of Object.values(api)) fn.mockReset();
  api.find.mockResolvedValue(FOUND);
  api.exportPerson.mockResolvedValue(undefined);
  api.preview.mockResolvedValue(PLAN);
  api.erase.mockResolvedValue(RESULT);
  api.erasures.mockResolvedValue([]);
  api.settings.mockResolvedValue(SETTINGS);
  api.save.mockImplementation(async () => ({ ...SETTINGS, version: 'v2' }));
  api.retention.mockResolvedValue(PREVIEW_OFF);
  api.runs.mockResolvedValue([]);
});

describe('who may handle privacy requests', () => {
  it('tells someone without the permission so, and asks the server nothing', async () => {
    as([PERMISSION.EditRecords, PERMISSION.DeleteRecords, PERMISSION.ExportRecords]);
    show('/privacy-requests?lead=l1');
    expect(await screen.findByText(/Only someone allowed to handle privacy requests/)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(api.find).not.toHaveBeenCalled();
  });

  it('says a personal workspace has no privacy requests', async () => {
    as('owner', true);
    show();
    expect(await screen.findByText(/handled in a club's or company's workspace/)).toBeInTheDocument();
    expect(api.settings).not.toHaveBeenCalled();
  });

  it('is open to a member given the permission alone', async () => {
    as([PERMISSION.ManagePrivacy]);
    show();
    expect(await screen.findByRole('tab', { name: 'Requests' })).toBeInTheDocument();
  });
});

describe('a request', () => {
  it('opened from a lead, finds their records with all of them ticked, and downloads only what stays ticked', async () => {
    as('owner');
    const user = userEvent.setup();
    show('/privacy-requests?lead=l1');

    expect(await screen.findByText('3 records found')).toBeInTheDocument();
    expect(api.find).toHaveBeenCalledWith({ leadId: 'l1' }, expect.anything());
    const boxes = screen.getAllByRole('checkbox');
    expect(boxes).toHaveLength(4); // three records and one address
    for (const box of boxes) expect(box).toBeChecked();

    // The old contact is someone else's: unticked, it is left out.
    await user.click(screen.getByRole('checkbox', { name: /Anna Keller \(old\)/ }));
    await user.click(screen.getByRole('button', { name: /Download everything held/ }));

    await waitFor(() => expect(api.exportPerson).toHaveBeenCalledTimes(1));
    const sent = api.exportPerson.mock.calls[0]![0] as PersonSelection;
    expect(sent).toEqual({ leadIds: ['l1'], contactIds: [], memberIds: ['m1'], applicationIds: [], addresses: ['anna@example.ch'] });
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/recorded in the audit log/));
  });

  it("keeps a ticked record's own address with it, and lets an address go only once no ticked record has it", async () => {
    as('owner');
    const user = userEvent.setup();
    show('/privacy-requests?lead=l1');

    // The server always takes a chosen record's address with it: unticking it alone would do nothing.
    const address = await screen.findByRole('checkbox', { name: /^anna@example\.ch/ });
    expect(address).toBeChecked();
    expect(address).toBeDisabled();
    expect(screen.getByText('(on a ticked record)')).toBeInTheDocument();

    for (const name of [/^Lead Anna Keller ·/, /^Member Anna Keller/, /Anna Keller \(old\)/])
      await user.click(screen.getByRole('checkbox', { name }));
    expect(address).toBeEnabled();
    expect(screen.getByRole('button', { name: /Download everything held/ })).toBeEnabled();
    await user.click(address);
    expect(address).not.toBeChecked();
    expect(screen.getByRole('button', { name: /Download everything held/ })).toBeDisabled();
  });

  it('finds by an email address typed in', async () => {
    as('owner');
    const user = userEvent.setup();
    show();
    await user.type(await screen.findByLabelText('Find a person'), 'anna@example.ch');
    await user.click(screen.getByRole('button', { name: 'Find' }));
    expect(await screen.findByText('3 records found')).toBeInTheDocument();
    expect(api.find).toHaveBeenCalledWith({ email: 'anna@example.ch' }, expect.anything());

    // Asked again, it is read again: what is found is what a download or an erasure acts on.
    await user.click(screen.getByRole('button', { name: 'Find' }));
    await waitFor(() => expect(api.find).toHaveBeenCalledTimes(2));
  });

  it('erases only after showing what it would do and the word ERASE typed, then forgets the record it started from', async () => {
    as('owner');
    const user = userEvent.setup();
    show('/privacy-requests?lead=l1');

    await user.click(await screen.findByRole('button', { name: /Erase this person/ }));
    const dialog = await screen.findByRole('dialog');
    // What it would do, read without changing anything.
    expect(await within(dialog).findByText('Charges and payments')).toBeInTheDocument();
    expect(within(dialog).getByText('Anonymised')).toBeInTheDocument();
    expect(within(dialog).getByText(/owes CHF 40\.00/)).toBeInTheDocument();
    expect(within(dialog).getByText(/suppression list as a one-way fingerprint/)).toBeInTheDocument();
    expect(api.preview).toHaveBeenCalledWith(
      { leadIds: ['l1'], contactIds: ['c9'], memberIds: ['m1'], applicationIds: [], addresses: ['anna@example.ch'] },
      expect.anything(),
    );

    const erase = within(dialog).getByRole('button', { name: 'Erase for good' });
    expect(erase).toBeDisabled();
    await user.type(within(dialog).getByLabelText(/Your reference/), 'Letter of 3 May');
    await user.type(within(dialog).getByLabelText(/Type ERASE to confirm/), 'erase');
    expect(erase).toBeDisabled();
    await user.clear(within(dialog).getByLabelText(/Type ERASE to confirm/));
    await user.type(within(dialog).getByLabelText(/Type ERASE to confirm/), 'ERASE');
    expect(erase).toBeEnabled();
    await user.click(erase);

    expect(await within(dialog).findByText(/Erased member 7/)).toBeInTheDocument();
    expect(api.erase).toHaveBeenCalledWith(
      { leadIds: ['l1'], contactIds: ['c9'], memberIds: ['m1'], applicationIds: [], addresses: ['anna@example.ch'] },
      'ERASE',
      'Letter of 3 May',
    );
    // The lead it started from is gone: the page no longer asks for it.
    await waitFor(() => expect(location).toBe('/privacy-requests'));
    await user.click(within(dialog).getByRole('button', { name: 'Done' }));
    expect(screen.getByRole('status')).toHaveTextContent('Erased as “Erased member 7” (erasure 7)');
    expect(screen.queryByText('3 records found')).toBeNull();
  });

  it("shows the server's refusal and changes nothing when the erasure is refused", async () => {
    as('owner');
    api.erase.mockRejectedValue(new ApiError('Someone changed one of these records while it was being erased. Look the person up again.', 409));
    const user = userEvent.setup();
    show('/privacy-requests?member=m1');

    await user.click(await screen.findByRole('button', { name: /Erase this person/ }));
    const dialog = await screen.findByRole('dialog');
    await user.type(await within(dialog).findByLabelText(/Type ERASE to confirm/), 'ERASE');
    await user.click(within(dialog).getByRole('button', { name: 'Erase for good' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Look the person up again');
    expect(location).toBe('/privacy-requests?member=m1');
  });
});

describe('retention', () => {
  it('offers no run while every period is off', async () => {
    as('owner');
    show('/privacy-requests?tab=retention');
    expect(await screen.findByText('Leads nobody worked on')).toBeInTheDocument();
    expect(screen.getAllByText('Off')).toHaveLength(4);
    expect(screen.getByRole('button', { name: /Run now/ })).toBeDisabled();
  });

  it('asks before a run, and runs on the second click', async () => {
    as('owner');
    api.retention.mockResolvedValue({
      ...PREVIEW_OFF,
      anythingOn: true,
      rules: PREVIEW_OFF.rules.map((r) => (r.rule === 'leads' ? { ...r, period: 18, count: 3 } : r)),
      kept: ['1 former member is kept although their period is over: money is still open on their account, or a subscription still runs.'],
    });
    const run: RetentionRun = {
      id: 'r1', trigger: 'manual', status: 'succeeded', startedAtUtc: '2026-11-12T10:00:00Z', finishedAtUtc: '2026-11-12T10:00:02Z',
      startedByName: 'Owner', leadsDeleted: 3, membersAnonymised: 0, messagesPurged: 0, detailsPurged: 0, failures: null,
    };
    api.run.mockResolvedValue(run);
    const user = userEvent.setup();
    show('/privacy-requests?tab=retention');

    expect(await screen.findByText('After 18 months')).toBeInTheDocument();
    expect(screen.getByText('3 to remove now')).toBeInTheDocument();
    expect(screen.getByText(/money is still open on their account/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Run now/ }));
    expect(api.run).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Run now' }));
    await waitFor(() => expect(api.run).toHaveBeenCalledTimes(1));
    expect(toast.success).toHaveBeenCalledWith('Retention run done');
  });
});

describe('the settings', () => {
  it('save who answers for the data, the consent and the periods against the version read, keeping the standard words', async () => {
    as('owner');
    const user = userEvent.setup();
    show('/privacy-requests?tab=settings');

    await user.type(await screen.findByLabelText('Name'), 'Pavillon 46 AG');
    await user.type(screen.getByLabelText('Email for privacy requests'), 'datenschutz@pavillon46.example');
    await user.click(screen.getByRole('radio', { name: /Required/ }));
    await user.type(screen.getByLabelText(/Delete leads nobody worked on after/), '18');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(api.save).toHaveBeenCalledTimes(1));
    expect(api.save).toHaveBeenCalledWith({
      controllerName: 'Pavillon 46 AG',
      controllerAddress: null,
      controllerEmail: 'datenschutz@pavillon46.example',
      dpoContact: null,
      marketingConsent: 'required',
      marketingConsentText: null,
      leadRetentionMonths: 18,
      memberRetentionYears: null,
      mailRetentionMonths: null,
      detailRetentionMonths: null,
    }, 'v1');
    expect(toast.success).toHaveBeenCalledWith('Privacy settings saved');
  });

  it('reads the settings again when someone else saved them first', async () => {
    as('owner');
    api.save.mockRejectedValue(new ApiError('Someone else changed the privacy settings since you opened them.', 409));
    const user = userEvent.setup();
    show('/privacy-requests?tab=settings');

    await user.click(await screen.findByRole('button', { name: 'Save' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/Someone else changed/)));
    await waitFor(() => expect(api.settings).toHaveBeenCalledTimes(2));
  });
});
