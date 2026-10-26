import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

const settings = { viewPreferencesJson: undefined as string | undefined };
const saveUserSettings = vi.fn(async (patch: { viewPreferencesJson?: string }) => ({ ...settings, ...patch }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({ ...settings })),
  saveUserSettings: (patch: { viewPreferencesJson?: string }) => saveUserSettings(patch),
}));

import { mergeViews, resetViewPreferencesForTests, serializeViews, useViewPreference, type PageView } from './viewPreferences';

const signIn = (id: string) => localStorage.setItem('aci_user', JSON.stringify({ id, name: id, email: `${id}@example.com` }));

describe('saved views', () => {
  beforeEach(() => {
    localStorage.clear();
    resetViewPreferencesForTests();
    settings.viewPreferencesJson = undefined;
    saveUserSettings.mockClear();
    signIn('ada');
  });
  afterEach(() => vi.useRealTimers());

  it('keeps the newer choice of each page between two copies', () => {
    const local: Record<string, PageView> = { leads: { layout: 'table', density: 'compact', at: 5 }, deals: { layout: 'list', density: 'comfortable', at: 9 } };
    const remote: Record<string, PageView> = { leads: { layout: 'grid', density: 'comfortable', at: 7 }, deals: { layout: 'board', density: 'comfortable', at: 1 }, tasks: { layout: 'list', density: 'compact', at: 3 } };
    const merged = mergeViews(local, remote);
    expect(merged.leads?.layout).toBe('grid');
    expect(merged.deals?.layout).toBe('list');
    expect(merged.tasks?.layout).toBe('list');
  });

  it('stays within what the server keeps, dropping the pages chosen longest ago', () => {
    const pages: Record<string, PageView> = {};
    for (let i = 0; i < 200; i++) pages[`page-${i}`] = { layout: 'table', density: 'compact', columns: { email: false, phone: true }, at: i };
    const json = serializeViews(pages);
    expect(json.length).toBeLessThanOrEqual(3800);
    const kept = JSON.parse(json).pages as Record<string, PageView>;
    expect(kept['page-199']).toBeDefined();
    expect(kept['page-0']).toBeUndefined();
  });

  it('opens a page the way the person left it, and saves the choice for them', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const { result } = renderHook(() => useViewPreference('leads', { layouts: ['grid', 'list', 'table'] }));
    expect(result.current.layout).toBe('grid');
    expect(result.current.customised).toBe(false);

    act(() => result.current.setLayout('table'));
    act(() => result.current.setDensity('compact'));
    act(() => result.current.setColumn('email', false));
    expect(result.current.layout).toBe('table');
    expect(result.current.density).toBe('compact');
    expect(result.current.shows('email')).toBe(false);
    expect(result.current.shows('phone', false)).toBe(false);

    await act(async () => { vi.advanceTimersByTime(1000); });
    expect(saveUserSettings).toHaveBeenCalledTimes(1);
    const saved = JSON.parse(saveUserSettings.mock.calls[0]![0].viewPreferencesJson!);
    expect(saved.pages.leads).toMatchObject({ layout: 'table', density: 'compact', columns: { email: false } });

    // A fresh page load reads this browser's copy straight away.
    resetViewPreferencesForTests();
    const again = renderHook(() => useViewPreference('leads', { layouts: ['grid', 'list', 'table'] }));
    expect(again.result.current.layout).toBe('table');
  });

  it('picks up a choice made in another browser', async () => {
    settings.viewPreferencesJson = JSON.stringify({ v: 1, pages: { deals: { layout: 'list', density: 'comfortable', at: Date.now() } } });
    const { result } = renderHook(() => useViewPreference('deals', { layouts: ['board', 'table', 'list', 'grid'] }));
    await waitFor(() => expect(result.current.layout).toBe('list'));
  });

  it('is each person’s own, and ignores a layout the page no longer offers', () => {
    localStorage.setItem('cadence.views.v1:ada', JSON.stringify({ v: 1, pages: { tasks: { layout: 'grid', density: 'compact' } } }));
    const ada = renderHook(() => useViewPreference('tasks', { layouts: ['board', 'list', 'table'] }));
    expect(ada.result.current.layout).toBe('board');
    expect(ada.result.current.density).toBe('compact');
    ada.unmount();

    resetViewPreferencesForTests();
    signIn('grace');
    const grace = renderHook(() => useViewPreference('tasks', { layouts: ['board', 'list', 'table'] }));
    expect(grace.result.current.density).toBe('comfortable');
  });

  it('goes back to how the page comes', () => {
    const { result } = renderHook(() => useViewPreference('contacts', { layouts: ['grid', 'list', 'table'] }));
    act(() => result.current.setLayout('list'));
    expect(result.current.customised).toBe(true);
    act(() => result.current.reset());
    expect(result.current.layout).toBe('grid');
    expect(result.current.customised).toBe(false);
  });
});
