// The club's billing: what billing now would post, billing it, the runs so far, and a
// member's statement as a PDF, downloaded or emailed.
//
// The server bills: every period a running subscription has started, once per period, by
// the daily run (when the club turns automatic billing on) or when someone asks. "Run due
// cycles" used to be this browser billing each subscription in turn, one request apiece.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { runDueBillingCycles } from './subscriptions';
import { downloadFile } from '@/app/lib/exportCsv';

export interface BillingRunFailure {
  what: string;
  id?: string;
  name: string;
  error: string;
}

export interface BillingRun {
  id: string;
  /** schedule (the daily run) or manual (someone asked). */
  trigger: 'schedule' | 'manual';
  scheduledFor?: string;
  /** running, succeeded, partly_failed, failed, or stopped (marked running long after it began). */
  status: 'running' | 'succeeded' | 'partly_failed' | 'failed' | 'stopped';
  startedAtUtc: string;
  finishedAtUtc?: string;
  startedByName?: string;
  steps: string[];
  subscriptionsBilled: number;
  chargesPosted: number;
  amountBilled: number;
  currency: string;
  overdueCharges: number;
  subscriptionsPastDue: number;
  remindersQueued: number;
  membersLapsed: number;
  membersReinstated: number;
  statementsQueued: number;
  failures: BillingRunFailure[];
}

export interface BillingPreviewItem {
  subscriptionId: string;
  memberId: string;
  memberName: string;
  name: string;
  amount: number;
  currency: string;
  cadence: string;
  periods: string[];
}

export interface BillingPreview {
  subscriptions: number;
  periods: number;
  amountByCurrency: Record<string, number>;
  items: BillingPreviewItem[];
}

export interface StatementDelivery {
  id: string;
  memberId: string;
  periodStartUtc: string;
  periodEndUtc: string;
  status: 'queued' | 'skipped' | 'suppressed';
  reason?: string;
  amountDue: number;
  currency: string;
  outboundMessageId?: string;
}

/** The latest runs, newest first. Demo data has none: nothing runs on its own there. */
export async function getBillingRuns(req?: RequestOptions): Promise<BillingRun[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<BillingRun[]>('/api/billing/runs', { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => [],
  );
}

/** What billing now would post. */
export async function getBillingPreview(req?: RequestOptions): Promise<BillingPreview> {
  return apiWithFallback(
    () => authFetchJson<BillingPreview>('/api/billing/preview', { signal: req?.signal }),
    async () => ({ subscriptions: 0, periods: 0, amountByCurrency: {}, items: [] }),
  );
}

/** Bills now every period that has started; the run it made says what it billed. */
export async function runBillingNow(): Promise<BillingRun> {
  if (servesDemoData()) {
    const billed = await runDueBillingCycles();
    const at = new Date().toISOString();
    return {
      id: 'demo', trigger: 'manual', status: 'succeeded', startedAtUtc: at, finishedAtUtc: at, steps: ['billing'],
      subscriptionsBilled: billed, chargesPosted: billed, amountBilled: 0, currency: '', overdueCharges: 0, subscriptionsPastDue: 0,
      remindersQueued: 0, membersLapsed: 0, membersReinstated: 0, statementsQueued: 0, failures: [],
    };
  }
  return authFetchJson<BillingRun>('/api/billing/run', { method: 'POST' });
}

/** The file name the server gave, or a plain one. */
function fileNameOf(res: Response, fallback: string): string {
  const header = res.headers.get('Content-Disposition') ?? '';
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
  if (star) return decodeURIComponent(star);
  return /filename="?([^";]+)"?/i.exec(header)?.[1] ?? fallback;
}

/**
 * Downloads the member's statement for [fromUtc, toUtc) as the server's PDF. In demo
 * data, with no server to build it, the page's print view stands in.
 */
export async function downloadStatementPdf(memberId: string, fromUtc: string, toUtc: string): Promise<void> {
  if (servesDemoData()) {
    window.print();
    return;
  }
  const res = await authFetch(
    `/api/members/${memberId}/statement.pdf?from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}`);
  await ensureOk(res);
  downloadFile(await res.blob(), fileNameOf(res, 'statement.pdf'));
}

/** Emails the member their statement for [fromUtc, toUtc), the PDF attached. */
export async function emailStatement(memberId: string, fromUtc: string, toUtc: string): Promise<StatementDelivery> {
  if (servesDemoData()) throw new Error('Statements are emailed from the live app; demo data sends no email.');
  return authFetchJson<StatementDelivery>(`/api/members/${memberId}/statement/email`, {
    method: 'POST',
    body: JSON.stringify({ from: fromUtc, to: toUtc }),
  });
}
