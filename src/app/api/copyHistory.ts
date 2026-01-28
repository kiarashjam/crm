import type { CopyHistoryItem } from './types';

const STORAGE_KEY = 'crm_copy_history';

function getStored(): CopyHistoryItem[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    return JSON.parse(raw) as CopyHistoryItem[];
  } catch {
    return [];
  }
}

function setStored(items: CopyHistoryItem[]) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    // ignore
  }
}

/** Get copy history (newest first). */
export async function getCopyHistory(): Promise<CopyHistoryItem[]> {
  await delay(200);
  const items = getStored();
  return [...items].sort((a, b) => (b.createdAt > a.createdAt ? 1 : -1));
}

/** Add an item to history (e.g. after "Send to CRM"). */
export async function addToCopyHistory(item: Omit<CopyHistoryItem, 'id' | 'createdAt'>): Promise<CopyHistoryItem> {
  await delay(100);
  const list = getStored();
  const newItem: CopyHistoryItem = {
    ...item,
    id: `hist_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`,
    createdAt: new Date().toISOString(),
  };
  list.unshift(newItem);
  setStored(list);
  return newItem;
}

/** Get counts for dashboard (sent this week, total sent). */
export async function getCopyHistoryStats(): Promise<{ sentThisWeek: number; totalSent: number }> {
  const items = getStored();
  const now = new Date();
  const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
  const sentThisWeek = items.filter((i) => new Date(i.createdAt) >= weekAgo).length;
  return { sentThisWeek, totalSent: items.length };
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
