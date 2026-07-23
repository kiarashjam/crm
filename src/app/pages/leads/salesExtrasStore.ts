// Per-lead sales-tracker fields — the columns from the P46 Sales Tracker
// workbook (outreach status, meeting scheduled, met, interested, contract
// sent/signed dates, deposit, notes). The backend's Lead schema does not
// carry these, so they're mirrored locally, one map keyed by lead id.
//
// This file is the single source of truth for the storage key so the Lead
// detail editor and the Reports dashboard read the same data.

import type {
  OutreachStatus,
  MeetingScheduled,
  YesNo,
  YesNoPending,
  ContractSent,
  ContractSigned,
} from '../salesTracker/types';

/** Everything the Excel CONTACTS sheet tracks that the backend Lead doesn't. */
export interface SalesExtras {
  outreachStatus: OutreachStatus;
  outreachDate: string;
  meetingScheduled: MeetingScheduled;
  meetingDate: string;
  met: YesNo;
  interestedAfterMtg: YesNoPending;
  contractSent: ContractSent;
  contractSentDate: string;
  contractSigned: ContractSigned;
  signatureDate: string;
  depositPaid: YesNo;
  lastContactDate: string;
  salesNotes: string;
}

export const EMPTY_SALES_EXTRAS: SalesExtras = {
  outreachStatus: '',
  outreachDate: '',
  meetingScheduled: '',
  meetingDate: '',
  met: '',
  interestedAfterMtg: '',
  contractSent: '',
  contractSentDate: '',
  contractSigned: '',
  signatureDate: '',
  depositPaid: '',
  lastContactDate: '',
  salesNotes: '',
};

const STORAGE_KEY = 'crm.leadSalesExtras.v1';
const WAITLIST_KEY = 'crm.leadSalesExtras.waitlistTotal.v1';
const SALES_EXTRAS_CHANGED = 'crm.leadSalesExtras.changed';

export function loadAllSalesExtras(): Record<string, SalesExtras> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, Partial<SalesExtras>>;
    if (!parsed || typeof parsed !== 'object') return {};
    const out: Record<string, SalesExtras> = {};
    for (const [id, ex] of Object.entries(parsed)) {
      out[id] = { ...EMPTY_SALES_EXTRAS, ...ex };
    }
    return out;
  } catch {
    return {};
  }
}

export function saveAllSalesExtras(map: Record<string, SalesExtras>): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
    window.dispatchEvent(new Event(SALES_EXTRAS_CHANGED));
  } catch {
    // ignore quota / private mode failures
  }
}

export function getSalesExtras(leadId: string): SalesExtras {
  return loadAllSalesExtras()[leadId] ?? { ...EMPTY_SALES_EXTRAS };
}

export function setSalesExtras(leadId: string, extras: SalesExtras): void {
  const map = loadAllSalesExtras();
  // Skip persisting entries with nothing set — keeps storage tidy for empty rows.
  const hasAny = Object.values(extras).some((v) => v && String(v).trim() !== '');
  if (hasAny) map[leadId] = extras;
  else delete map[leadId];
  saveAllSalesExtras(map);
}

export function loadWaitlistTotal(): number {
  try {
    const raw = localStorage.getItem(WAITLIST_KEY);
    if (!raw) return 0;
    const n = Number(raw);
    return Number.isFinite(n) && n >= 0 ? n : 0;
  } catch {
    return 0;
  }
}

export function saveWaitlistTotal(value: number): void {
  try {
    localStorage.setItem(WAITLIST_KEY, String(Math.max(0, Math.floor(value))));
    window.dispatchEvent(new Event(SALES_EXTRAS_CHANGED));
  } catch {
    // ignore
  }
}

export function onSalesExtrasChange(handler: () => void): () => void {
  const wrapped = () => handler();
  window.addEventListener(SALES_EXTRAS_CHANGED, wrapped);
  // Storage event fires for cross-tab changes.
  const storageHandler = (e: StorageEvent) => {
    if (e.key === STORAGE_KEY || e.key === WAITLIST_KEY) handler();
  };
  window.addEventListener('storage', storageHandler);
  return () => {
    window.removeEventListener(SALES_EXTRAS_CHANGED, wrapped);
    window.removeEventListener('storage', storageHandler);
  };
}

export { STORAGE_KEY as SALES_EXTRAS_STORAGE_KEY };
