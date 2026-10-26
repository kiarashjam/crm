import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Users } from 'lucide-react';
import type { DuplicateGroup, MergeField } from '@/app/api/dedupe';
import type { DuplicateKind } from './kinds';

const mergeDuplicates = vi.fn();
vi.mock('@/app/api/dedupe', async (importActual) => ({
  ...(await importActual<typeof import('@/app/api/dedupe')>()),
  mergeDuplicates: (...args: unknown[]) => mergeDuplicates(...args),
}));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

import { MergeDuplicatesDialog } from './MergeDuplicatesDialog';
import { DuplicateMerge } from './DuplicateMerge';

interface Person { id: string; name: string; email: string; phone?: string; createdAtUtc?: string }

const FIELDS: MergeField<Person>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Email', keys: ['email'], rule: 'keep' },
  { label: 'Phone', keys: ['phone'], rule: 'fill' },
];

const people: Person[] = [
  { id: 'a', name: 'Ada Lovelace', email: 'ada@example.com', createdAtUtc: '2026-01-01T00:00:00Z' },
  { id: 'b', name: 'Ada Lovelace', email: 'ada@example.com', phone: '+41 79 123 45 67', createdAtUtc: '2025-01-01T00:00:00Z' },
];

const spec = (load: () => Promise<Person[]> = async () => people): DuplicateKind<Person> => ({
  kind: 'contacts',
  noun: { one: 'contact', many: 'contacts' },
  groupNoun: { one: 'person', many: 'people' },
  tone: 'emerald',
  icon: Users,
  load,
  group: (records) => (records.length > 1 ? [{ key: 'a|b', reason: 'Same email', records }] : []),
  fields: FIELDS,
  title: (p) => p.name,
  subtitle: (p) => p.email,
  created: (p) => p.createdAtUtc,
  moves: ['deals', 'tasks'],
  fate: () => 'The other one is archived.',
});

const groups: DuplicateGroup<Person>[] = [{ key: 'a|b', reason: 'Same email', records: people }];

function renderDialog(overrides: Partial<Parameters<typeof MergeDuplicatesDialog<Person>>[0]> = {}) {
  const props = {
    spec: spec(),
    groups,
    open: true,
    onOpenChange: vi.fn(),
    onMerged: vi.fn(),
    onNotSame: vi.fn(),
    ...overrides,
  };
  render(<MemoryRouter><MergeDuplicatesDialog {...props} /></MemoryRouter>);
  return props;
}

describe('MergeDuplicatesDialog', () => {
  beforeEach(() => {
    mergeDuplicates.mockReset();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('suggests the more complete record, previews the result, and merges the other into it', async () => {
    mergeDuplicates.mockResolvedValue({ merged: 1 });
    const props = renderDialog();

    const kept = screen.getByRole('radio', { checked: true });
    expect(kept).toHaveTextContent('Added');
    expect(screen.getByText('The same in both:')).toBeInTheDocument();
    expect(screen.getAllByText('+41 79 123 45 67').length).toBeGreaterThan(0);
    expect(screen.getByText(/moves to/)).toHaveTextContent('deals and tasks');

    fireEvent.click(screen.getByTestId('confirm-merge'));
    await waitFor(() => expect(mergeDuplicates).toHaveBeenCalledWith('contacts', 'b', ['a']));
    expect(props.onMerged).toHaveBeenCalled();
    expect(await screen.findByText('Merged into Ada Lovelace')).toBeInTheDocument();
  });

  it('keeps the record the person picks', async () => {
    mergeDuplicates.mockResolvedValue({ merged: 1 });
    renderDialog();
    const [first] = screen.getAllByRole('radio');
    fireEvent.click(first!);
    expect(first).toHaveAttribute('aria-checked', 'true');
    fireEvent.click(screen.getByTestId('confirm-merge'));
    await waitFor(() => expect(mergeDuplicates).toHaveBeenCalledWith('contacts', 'a', ['b']));
  });

  it('shows the server’s refusal and stays on the group', async () => {
    mergeDuplicates.mockResolvedValue({ merged: 0, error: 'Only a manager can do that.' });
    const props = renderDialog();
    fireEvent.click(screen.getByTestId('confirm-merge'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Only a manager can do that.');
    expect(props.onMerged).not.toHaveBeenCalled();
    expect(screen.getByTestId('confirm-merge')).not.toBeDisabled();
  });

  it('puts a group away for good when it is not the same', async () => {
    const props = renderDialog();
    fireEvent.click(screen.getByRole('button', { name: /Not the same/ }));
    expect(props.onNotSame).toHaveBeenCalledWith('a|b');
    expect(mergeDuplicates).not.toHaveBeenCalled();
    expect(await screen.findByText('Nothing was merged')).toBeInTheDocument();
  });
});

describe('DuplicateMerge', () => {
  beforeEach(() => {
    mergeDuplicates.mockReset();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('says how many duplicates there are and opens the merge from the banner', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    render(<MemoryRouter><DuplicateMerge spec={spec()} /></MemoryRouter>);
    await act(async () => { vi.advanceTimersByTime(800); });
    vi.useRealTimers();
    expect(await screen.findByText('1 duplicate contact found')).toBeInTheDocument();
    expect(screen.getByText(/1 person was entered more than once/)).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('review-duplicates'));
    expect(await screen.findByRole('dialog')).toHaveTextContent('Merge duplicate contacts');
  });

  it('can be put away until something new turns up', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    render(<MemoryRouter><DuplicateMerge spec={spec()} /></MemoryRouter>);
    await act(async () => { vi.advanceTimersByTime(800); });
    vi.useRealTimers();
    fireEvent.click(await screen.findByRole('button', { name: 'Hide for now' }));
    await waitFor(() => expect(screen.queryByTestId('review-duplicates')).not.toBeInTheDocument());
  });

  it('shows nothing when the list is clean, or cannot be read', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    render(<MemoryRouter><DuplicateMerge spec={spec(async () => [people[0]!])} /></MemoryRouter>);
    render(<MemoryRouter><DuplicateMerge spec={spec(async () => { throw new Error('offline'); })} /></MemoryRouter>);
    await act(async () => { vi.advanceTimersByTime(800); });
    vi.useRealTimers();
    expect(screen.queryByTestId('review-duplicates')).not.toBeInTheDocument();
  });
});
