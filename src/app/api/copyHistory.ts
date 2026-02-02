import type { CopyHistoryItem } from './types';
import { mockCopyHistory } from './mockData';
import { isUsingRealApi, authFetchJson } from './apiClient';

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

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function mapItem(d: { id: string; type: string; copy: string; recipientName: string; recipientType: string; recipientId: string; createdAt: string }): CopyHistoryItem {
  return {
    id: d.id,
    type: d.type,
    copy: d.copy,
    recipientName: d.recipientName,
    recipientType: d.recipientType as CopyHistoryItem['recipientType'],
    recipientId: d.recipientId,
    createdAt: d.createdAt,
  };
}

/** Get copy history (newest first). */
export async function getCopyHistory(): Promise<CopyHistoryItem[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; type: string; copy: string; recipientName: string; recipientType: string; recipientId: string; createdAt: string }[]>('/api/copyhistory');
    return Array.isArray(list) ? list.map(mapItem) : [];
  }
  await delay(200);
  const items = getStored();
  const list = items.length > 0 ? items : [...mockCopyHistory];
  return [...list].sort((a, b) => (b.createdAt > a.createdAt ? 1 : -1));
}

/** Add an item to history (e.g. after "Send to CRM"). */
export async function addToCopyHistory(item: Omit<CopyHistoryItem, 'id' | 'createdAt'>): Promise<CopyHistoryItem> {
  if (isUsingRealApi()) {
    await authFetchJson<{ success: boolean; message: string }>('/api/copy/send', {
      method: 'POST',
      body: JSON.stringify({
        objectType: item.recipientType,
        recordId: item.recipientId,
        recordName: item.recipientName,
        copy: item.copy,
        copyTypeLabel: item.type,
      }),
    });
    const list = await getCopyHistory();
    const found = list.find((c) => c.recipientId === item.recipientId && c.copy === item.copy);
    return found ?? { ...item, id: '', createdAt: new Date().toISOString() };
  }
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
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ sentThisWeek: number; totalSent: number }>('/api/copyhistory/stats');
    return res ?? { sentThisWeek: 0, totalSent: 0 };
  }
  const items = getStored();
  const list = items.length > 0 ? items : mockCopyHistory;
  const now = new Date();
  const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
  const sentThisWeek = list.filter((i) => new Date(i.createdAt) >= weekAgo).length;
  return { sentThisWeek, totalSent: list.length };
}
