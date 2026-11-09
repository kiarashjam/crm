// Against the server, duplicate contacts and companies come from the server's own search,
// and "not the same" is a decision for the whole workspace, kept on the server. It was kept
// in localStorage, so each colleague, on each device, was asked about the same pair again.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { Users } from 'lucide-react';
import type { DuplicateScan } from '@/app/api/dedupe';
import type { DuplicateKind } from './kinds';

const demo = vi.hoisted(() => ({ on: false }));
vi.mock('@/app/api/apiClient', async (importActual) => ({
  ...(await importActual<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => demo.on,
}));
const server = vi.hoisted(() => ({
  markNotSame: vi.fn(async () => undefined),
  clearNotSame: vi.fn(async () => undefined),
  getNotSamePairs: vi.fn(async (): Promise<string[]> => []),
}));
vi.mock('@/app/api/dedupe', async (importActual) => ({
  ...(await importActual<typeof import('@/app/api/dedupe')>()),
  markNotSame: server.markNotSame,
  clearNotSame: server.clearNotSame,
  getNotSamePairs: server.getNotSamePairs,
}));
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { useDuplicateGroups } = await import('./useDuplicateGroups');

interface Person { id: string; name: string; email: string; phone?: string }
const marc: Person = { id: 'a', name: 'Marc Dubois', email: 'marc@gmail.com', phone: '+41 79 123 45 67' };
const marcAtWork: Person = { id: 'b', name: 'Marc Dubois', email: 'm.dubois@firm.ch', phone: '079 123 45 67' };

function spec(over: Partial<DuplicateKind<Person>> = {}): DuplicateKind<Person> {
  return {
    kind: 'contacts',
    noun: { one: 'contact', many: 'contacts' },
    groupNoun: { one: 'person', many: 'people' },
    tone: 'emerald',
    icon: Users,
    load: async () => [marc, marcAtWork],
    group: (records) => (records.length > 1 ? [{ key: 'a|b', reason: 'Same phone · Same name', records }] : []),
    fields: [],
    title: (p) => p.name,
    subtitle: (p) => p.email,
    created: () => undefined,
    moves: [],
    fate: () => '',
    ...over,
  };
}

beforeEach(() => {
  demo.on = false;
  localStorage.clear();
  vi.clearAllMocks();
});
afterEach(() => vi.useRealTimers());

describe('useDuplicateGroups against the server', () => {
  it("uses the server's search, and decides \"not the same\" for the whole team", async () => {
    let scan: DuplicateScan<Person> = { groups: [{ key: 'a|b', reason: 'Same phone · Same name', records: [marc, marcAtWork] }], notSame: [] };
    const load = vi.fn(async () => [marc, marcAtWork]);
    const kind = spec({ load, scan: vi.fn(async () => scan) });
    const { result } = renderHook(() => useDuplicateGroups(kind, { delayMs: 0 }));

    await waitFor(() => expect(result.current.groups).toHaveLength(1));
    expect(load).not.toHaveBeenCalled();

    scan = { groups: [], notSame: [{ key: 'a|b', recordIds: ['a', 'b'] }] };
    act(() => result.current.markNotSame(['a|b']));

    expect(result.current.groups).toHaveLength(0);
    expect(server.markNotSame).toHaveBeenCalledWith('contacts', ['a', 'b']);
    await waitFor(() => expect(result.current.notSameCount).toBe(1));
    expect(localStorage.getItem('cadence.duplicates.notSame.contacts')).toBeNull();

    scan = { groups: [{ key: 'a|b', reason: 'Same phone · Same name', records: [marc, marcAtWork] }], notSame: [] };
    act(() => result.current.clearNotSame());
    expect(server.clearNotSame).toHaveBeenCalledWith('contacts', ['a', 'b']);
    await waitFor(() => expect(result.current.groups).toHaveLength(1));
  });

  it('keeps a group hidden when a read that began before the decision answers with it', async () => {
    const group = { key: 'a|b', reason: 'Same phone', records: [marc, marcAtWork] };
    let answerSlowRead!: (scan: DuplicateScan<Person>) => void;
    let answerReread!: (scan: DuplicateScan<Person>) => void;
    const scan = vi.fn()
      .mockResolvedValueOnce({ groups: [group], notSame: [] })
      .mockImplementationOnce(() => new Promise<DuplicateScan<Person>>((resolve) => { answerSlowRead = resolve; }))
      .mockImplementationOnce(() => new Promise<DuplicateScan<Person>>((resolve) => { answerReread = resolve; }));
    // One kind for every render, as the pages' kinds are. A new one each render read again
    // on its own, used up the answers above, and made this test fail now and then.
    const kind = spec({ scan });
    const { result } = renderHook(() => useDuplicateGroups(kind, { delayMs: 0 }));
    await waitFor(() => expect(result.current.groups).toHaveLength(1));

    act(() => { void result.current.refresh(); });
    act(() => result.current.markNotSame(['a|b']));
    await act(async () => { answerSlowRead({ groups: [group], notSame: [] }); });

    expect(result.current.groups).toHaveLength(0);
    await waitFor(() => expect(scan).toHaveBeenCalledTimes(3));
    await act(async () => { answerReread({ groups: [], notSame: [{ key: 'a|b', recordIds: ['a', 'b'] }] }); });
    await waitFor(() => expect(result.current.notSameCount).toBe(1));
    expect(result.current.groups).toHaveLength(0);
  });

  it('keeps a group hidden while its decision is still being saved, when an earlier one answers first', async () => {
    const first = { key: 'a|b', reason: 'Same phone', records: [marc, marcAtWork] };
    const second = { key: 'c|d', reason: 'Same name', records: [{ ...marc, id: 'c' }, { ...marcAtWork, id: 'd' }] };
    let saveFirst!: () => void;
    let saveSecond!: () => void;
    server.markNotSame
      .mockImplementationOnce(() => new Promise<undefined>((resolve) => { saveFirst = () => resolve(undefined); }))
      .mockImplementationOnce(() => new Promise<undefined>((resolve) => { saveSecond = () => resolve(undefined); }));
    let answerAfterFirst!: (scan: DuplicateScan<Person>) => void;
    const scan = vi.fn()
      .mockResolvedValueOnce({ groups: [first, second], notSame: [] })
      // Read once the first decision is saved, while the second is still on its way.
      .mockImplementationOnce(() => new Promise<DuplicateScan<Person>>((resolve) => { answerAfterFirst = resolve; }))
      .mockResolvedValue({ groups: [], notSame: [{ key: 'a|b', recordIds: ['a', 'b'] }, { key: 'c|d', recordIds: ['c', 'd'] }] });
    // One kind for every render, as the pages' kinds are: a new one would read again each time.
    const kind = spec({ scan });
    const { result } = renderHook(() => useDuplicateGroups(kind, { delayMs: 0 }));
    await waitFor(() => expect(result.current.groups).toHaveLength(2));

    // Two quick "not the same" clicks: the second is saved after the first.
    act(() => result.current.markNotSame(['a|b']));
    act(() => result.current.markNotSame(['c|d']));
    expect(result.current.groups).toHaveLength(0);
    await act(async () => { saveFirst(); });
    await waitFor(() => expect(scan).toHaveBeenCalledTimes(2));
    await act(async () => { answerAfterFirst({ groups: [second], notSame: [{ key: 'a|b', recordIds: ['a', 'b'] }] }); });
    // That read's answer is on screen (the first group is counted as decided)...
    await waitFor(() => expect(result.current.notSameCount).toBe(1));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    // ...and the second group, whose decision it could not know yet, stays hidden.
    expect(result.current.groups).toHaveLength(0);

    await act(async () => { saveSecond(); });
    await waitFor(() => expect(result.current.notSameCount).toBe(2));
    expect(result.current.groups).toHaveLength(0);
  });

  it('shows the group again, and says why, when the decision could not be saved', async () => {
    server.markNotSame.mockRejectedValueOnce(new Error('A Viewer cannot change records.'));
    const scan: DuplicateScan<Person> = { groups: [{ key: 'a|b', reason: 'Same phone', records: [marc, marcAtWork] }], notSame: [] };
    const { result } = renderHook(() => useDuplicateGroups(spec({ scan: async () => scan }), { delayMs: 0 }));
    await waitFor(() => expect(result.current.groups).toHaveLength(1));

    act(() => result.current.markNotSame(['a|b']));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('A Viewer cannot change records.'));
    await waitFor(() => expect(result.current.groups).toHaveLength(1));
  });

  it("hides a group found in the browser once the team said every pair in it is not the same", async () => {
    server.getNotSamePairs.mockResolvedValue(['a|b']);
    const { result } = renderHook(() => useDuplicateGroups(spec({ kind: 'leads' }), { delayMs: 0 }));

    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(server.getNotSamePairs).toHaveBeenCalledWith('leads');
    expect(result.current.groups).toHaveLength(0);
    expect(result.current.notSameCount).toBe(1);
  });
});

describe('useDuplicateGroups in demo mode', () => {
  it('keeps "not the same" in this browser, as all demo data is', async () => {
    demo.on = true;
    const kind = spec({ scan: vi.fn(async () => ({ groups: [], notSame: [] })) });
    const { result } = renderHook(() => useDuplicateGroups(kind, { delayMs: 0 }));
    await waitFor(() => expect(result.current.groups).toHaveLength(1));
    expect(kind.scan).not.toHaveBeenCalled();

    act(() => result.current.markNotSame(['a|b']));

    expect(result.current.groups).toHaveLength(0);
    expect(server.markNotSame).not.toHaveBeenCalled();
    expect(JSON.parse(localStorage.getItem('cadence.duplicates.notSame.contacts') ?? '[]')).toEqual(['a|b']);
  });
});
