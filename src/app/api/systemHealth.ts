// System health: what has gone wrong in the workspace, in one read, for the people who manage
// its settings. Handoffs the portal has not taken, the outbox's late and failed messages,
// webhook deliveries, Stripe events and automation runs that failed, bounces and spam reports,
// the server's background jobs and the portal's last ping (GET /api/system-health).
//
// Server only: in demo mode there is nothing running to report on.

import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

export type HealthStatus = 'ok' | 'attention' | 'failing';

/** Something that can be tried again, with the endpoint that already does it. */
export interface HealthRetry {
  /** handoff: the deal's welcome sent to the portal again; automation: the run's failed step tried again. */
  kind: 'handoff' | 'automation';
  id: string;
}

export interface HandoffIssue {
  id: string;
  dealId: string;
  dealName?: string | null;
  person: string;
  /** late: still not taken by the portal an hour on; failed: it needs attention. */
  state: 'late' | 'failed';
  lastError?: string | null;
  attempts: number;
  sinceUtc: string;
  link: string;
  retry?: HealthRetry | null;
}

export interface OutboxIssue {
  id: string;
  kind: 'email' | 'webhook' | 'handoff';
  state: 'queued' | 'failed';
  /** The addresses of an email; the event of a webhook or a handoff. */
  to?: string | null;
  subject?: string | null;
  lastError?: string | null;
  attempts: number;
  createdAtUtc: string;
  nextAttemptAtUtc?: string | null;
  link?: string | null;
  retry?: HealthRetry | null;
}

export interface WebhookIssue {
  id: string;
  eventName: string;
  status: string;
  attempts: number;
  lastStatusCode?: number | null;
  lastError?: string | null;
  createdAtUtc: string;
}

export interface PaymentIssue {
  id: string;
  /** Stripe's evt_ id. */
  eventId: string;
  type: string;
  note?: string | null;
  attempts: number;
  receivedAtUtc: string;
}

export interface AutomationIssue {
  runId: string;
  ruleId: string;
  ruleName: string;
  recordName?: string | null;
  lastError?: string | null;
  attempts: number;
  failedAtUtc: string;
  link: string;
  retry?: HealthRetry | null;
}

export interface EmailIssue {
  id: string;
  kind: 'bounce' | 'spam_report';
  to?: string | null;
  subject?: string | null;
  detail?: string | null;
  atUtc: string;
  link?: string | null;
}

/** A background job: the server's, shared by every workspace. */
export interface JobHealth {
  name: string;
  intervalSeconds: number;
  lastSeenAtUtc: string;
  lastBeatAtUtc?: string | null;
  lastFailureAtUtc?: string | null;
  failing: boolean;
  silent: boolean;
  consecutiveFailures: number;
  lastError?: string | null;
}

export interface PortalHealth {
  /** Welcome & portal has an address and a secret. */
  configured: boolean;
  /** What the last ping found; null before the first. */
  ok?: boolean | null;
  atUtc?: string | null;
  statusCode?: number | null;
  error?: string | null;
  link: string;
}

export interface SystemHealth {
  checkedAtUtc: string;
  overall: {
    status: HealthStatus;
    /** How many areas have something in them. */
    issues: number;
    counts: Record<string, number>;
  };
  handoffs: { count: number; items: HandoffIssue[] };
  outbox: { count: number; queued: number; failed: number; items: OutboxIssue[] };
  webhooks: { count: number; items: WebhookIssue[]; link: string };
  payments: { count: number; items: PaymentIssue[]; link: string };
  automations: { count: number; items: AutomationIssue[] };
  email: { count: number; bounces: number; spamReports: number; newSuppressions: number; items: EmailIssue[]; link: string };
  jobs: JobHealth[];
  portal: PortalHealth;
}

/** The workspace's health now; null in demo mode, where nothing runs. */
export async function getSystemHealth(req?: RequestOptions): Promise<SystemHealth | null> {
  if (servesDemoData()) return null;
  return authFetchJson<SystemHealth>('/api/system-health', { signal: req?.signal });
}
