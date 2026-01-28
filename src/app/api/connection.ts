import type { ConnectionStatus } from './types';

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

/** Get current CRM connection status. */
export async function getConnectionStatus(): Promise<ConnectionStatus> {
  await delay(100);
  return getStored();
}

/** Set connection status (e.g. after "Connect" on Connection page). */
export async function setConnectionStatus(status: ConnectionStatus): Promise<void> {
  await delay(200);
  setStored(status);
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
