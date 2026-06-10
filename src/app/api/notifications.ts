// Notifications + reminders.
//
// A notification is a small, dismissible alert surfaced in the header bell.
// Against the real backend these are server-pushed (task reminders, @mentions,
// deal changes, …). In demo/mock mode there's no server clock, so we synthesize
// the most important ones — overdue / due-today tasks — from the local task
// store via `syncTaskReminders`, deduped by a stable `sourceKey`.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import type { TaskItem } from './types';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type NotificationType = 'task' | 'deal' | 'lead' | 'mention' | 'system';

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  message?: string;
  /** In-app route to open when the notification is clicked. */
  link?: string;
  read: boolean;
  createdAtUtc: string;
  /** Stable key used to avoid re-creating the same synthesized reminder. */
  sourceKey?: string;
}

function nowIso(): string {
  return new Date().toISOString();
}

function hoursAgo(h: number): string {
  return new Date(Date.now() - h * 3_600_000).toISOString();
}

const notificationStore = createMockStore<AppNotification>({
  storageKey: 'crm.mock.notifications.v1',
  seed: [
    {
      id: 'ntf-seed-1',
      type: 'mention',
      title: 'You were mentioned',
      message: 'Alex left a note on Acme Corp — “can you take this one?”',
      link: '/companies',
      read: false,
      createdAtUtc: hoursAgo(2),
    },
    {
      id: 'ntf-seed-2',
      type: 'deal',
      title: 'Deal moved to Negotiation',
      message: 'Website redesign — $24,000',
      link: '/deals',
      read: false,
      createdAtUtc: hoursAgo(6),
    },
    {
      id: 'ntf-seed-3',
      type: 'system',
      title: 'Welcome to Cadence',
      message: 'Reminders, @mentions and deal updates show up here.',
      link: undefined,
      read: true,
      createdAtUtc: hoursAgo(30),
    },
  ],
  idOf: (n) => n.id,
});

const byNewest = (a: AppNotification, b: AppNotification) =>
  Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc);

export async function getNotifications(): Promise<AppNotification[]> {
  return apiWithFallback(
    async () => { const res = await authFetchJson<AppNotification[]>('/api/notifications'); return Array.isArray(res) ? res : []; },
    async () => { await delay(120); return [...notificationStore.list()].sort(byNewest); },
  );
}

export async function getUnreadNotificationCount(): Promise<number> {
  return apiWithFallback(
    async () => { const res = await authFetchJson<{ count: number }>('/api/notifications/unread-count'); return res?.count ?? 0; },
    async () => { await delay(60); return notificationStore.list().filter((n) => !n.read).length; },
  );
}

export async function markNotificationRead(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/notifications/${id}/read`, { method: 'POST' }); if (!res.ok) throw new Error('failed'); return true; },
    async () => { await delay(60); return notificationStore.update(id, { read: true }) != null; },
  );
}

export async function markAllNotificationsRead(): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch('/api/notifications/read-all', { method: 'POST' }); if (!res.ok) throw new Error('failed'); return true; },
    async () => {
      await delay(80);
      for (const n of notificationStore.list()) {
        if (!n.read) notificationStore.update(n.id, { read: true });
      }
      return true;
    },
  );
}

export async function createNotification(
  input: Omit<AppNotification, 'id' | 'read' | 'createdAtUtc'> & { read?: boolean; createdAtUtc?: string },
): Promise<AppNotification | null> {
  return apiWithFallback(
    () => authFetchJson<AppNotification>('/api/notifications', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(40);
      return notificationStore.add({
        id: mockId('ntf'),
        read: input.read ?? false,
        createdAtUtc: input.createdAtUtc ?? nowIso(),
        ...input,
      });
    },
  );
}

/**
 * Demo-mode reminder engine: turn overdue / due-today tasks into notifications.
 * No-op against the real backend, which owns reminder delivery. Deduped by
 * `sourceKey` so the same task doesn't pile up notifications on every poll.
 */
export function syncTaskReminders(tasks: TaskItem[]): void {
  const existing = new Set(
    notificationStore.list().map((n) => n.sourceKey).filter(Boolean) as string[],
  );
  const now = Date.now();
  const DAY = 24 * 3_600_000;
  for (const t of tasks) {
    if (t.status === 'completed' || t.status === 'cancelled' || !t.dueDateUtc) continue;
    const due = Date.parse(t.dueDateUtc);
    if (Number.isNaN(due)) continue;
    const overdue = due < now;
    const dueToday = !overdue && due - now <= DAY;
    if (!overdue && !dueToday) continue;
    const sourceKey = `task-${overdue ? 'overdue' : 'due'}-${t.id}`;
    if (existing.has(sourceKey)) continue;
    const link = t.leadId
      ? `/leads/${t.leadId}`
      : t.dealId
        ? `/deals/${t.dealId}`
        : t.contactId
          ? `/contacts/${t.contactId}`
          : '/tasks';
    notificationStore.add({
      id: mockId('ntf'),
      type: 'task',
      title: overdue ? 'Task overdue' : 'Task due today',
      message: t.title,
      link,
      read: false,
      createdAtUtc: nowIso(),
      sourceKey,
    });
    existing.add(sourceKey);
  }
}
