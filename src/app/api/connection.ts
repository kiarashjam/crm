import type { ConnectionStatus } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

const STORAGE_KEY = 'crm_connection';

function getStored(): ConnectionStatus {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { connected: false };
    return JSON.parse(raw) as ConnectionStatus;
  } catch {
    return { connected: false };
  }
}

function setStored(status: ConnectionStatus) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(status));
  } catch {
    // ignore
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Get current CRM connection status. */
export async function getConnectionStatus(): Promise<ConnectionStatus> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ connected: boolean; accountEmail?: string | null }>('/api/connection/status');
    return { connected: res?.connected ?? false, accountEmail: res?.accountEmail ?? undefined };
  }
  await delay(100);
  return getStored();
}

/** Set connection status (e.g. after "Connect" on Connection page). */
export async function setConnectionStatus(status: ConnectionStatus): Promise<void> {
  if (isUsingRealApi()) {
    await authFetchJson('/api/connection/status', {
      method: 'PUT',
      body: JSON.stringify({ connected: status.connected, accountEmail: status.accountEmail ?? null }),
    });
    return;
  }
  await delay(200);
  setStored(status);
}
