import { authFetch, authFetchJson, ensureOk, servesDemoData } from './apiClient';

export interface WebhookInfo {
  webhookUrl: string;
  apiKeyCreatedAt: string | null;
  /** Whether the workspace has a lead webhook key. The key itself is shown once, when made. */
  hasApiKey: boolean;
  usesDefaultWebhookPassword: boolean;
  passwordWebhookUrl: string;
  /** Where Cadence posts lead and deal events, if anywhere. */
  outboundWebhookUrl?: string | null;
  /** Whether events are signed. The secret itself is never sent back. */
  hasOutboundWebhookSecret?: boolean;
  /** Whether the workspace has an MCP key. The key itself is shown once, when made. */
  hasMcpApiKey?: boolean;
  mcpApiKeyCreatedAt?: string | null;
}

/** A read-only placeholder returned in demo mode so the webhook page renders
 * cleanly instead of crashing. UI consumers should treat `hasApiKey: false`
 * and the `demo://…` URLs as "not configured". */
const DEMO_WEBHOOK_INFO: WebhookInfo = {
  webhookUrl: 'demo://webhook/leads',
  apiKeyCreatedAt: null,
  hasApiKey: false,
  usesDefaultWebhookPassword: true,
  passwordWebhookUrl: 'demo://webhook/leads-password',
};

export async function getWebhookInfo(orgId: string): Promise<WebhookInfo> {
  if (servesDemoData()) return DEMO_WEBHOOK_INFO;
  return authFetchJson<WebhookInfo>(`/api/webhook/organizations/${orgId}`);
}

/**
 * A new lead webhook key, replacing any earlier one. Returned once: the server keeps
 * only its hash, so this is the only time anyone can copy it.
 */
export async function generateWebhookApiKey(orgId: string): Promise<string> {
  if (servesDemoData()) {
    // Demo mode: return a clearly-fake key so the UI shows a value but ops know
    // it isn't real.
    return 'demo-key-not-functional';
  }
  const result = await authFetchJson<{ apiKey: string }>(`/api/webhook/organizations/${orgId}/generate-key`, {
    method: 'POST',
  });
  return result.apiKey;
}

/**
 * A new key for AI assistants (MCP), replacing any earlier one. Returned once: the
 * server keeps only its hash. It is separate from the lead webhook key, which website
 * forms carry and which no longer opens the CRM to an assistant.
 */
export async function generateMcpApiKey(orgId: string): Promise<string> {
  if (servesDemoData()) return 'demo-key-not-functional';
  const result = await authFetchJson<{ apiKey: string }>(`/api/webhook/organizations/${orgId}/generate-mcp-key`, {
    method: 'POST',
  });
  return result.apiKey;
}

/** Sets the JSON webhook password, or clears it (empty string) to use the app default. */
export async function updateWebhookPassword(orgId: string, password: string | null): Promise<void> {
  if (servesDemoData()) return; // no-op in demo
  const res = await authFetch(`/api/webhook/organizations/${orgId}/webhook-password`, {
    method: 'PUT',
    body: JSON.stringify({ password: password ?? '' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
}

/** One event posted, or still to be posted, to the workspace's address. */
export interface WebhookDelivery {
  id: string;
  eventName: string;
  /** queued (it is tried again at nextAttemptAtUtc), sent, failed (given up on) or cancelled. */
  status: 'queued' | 'sent' | 'failed' | 'cancelled';
  attempts: number;
  /** What the receiver last answered, when it answered. */
  lastStatusCode: number | null;
  lastError: string | null;
  createdAtUtc: string;
  sentAtUtc: string | null;
  nextAttemptAtUtc: string | null;
}

/** The latest events and what the receiver answered. Owner or manager only; none in demo mode. */
export async function getWebhookDeliveries(orgId: string): Promise<WebhookDelivery[]> {
  if (servesDemoData()) return [];
  return authFetchJson<WebhookDelivery[]>(`/api/webhook/organizations/${orgId}/deliveries`);
}

/**
 * Where Cadence posts lead.created, deal.stage_changed and deal.won, as JSON. An empty
 * URL turns it off. A secret signs each body (X-Cadence-Signature, hex HMAC-SHA256);
 * leave it undefined to keep the saved one. Owner or manager only; the address must be
 * public https, and the server's reason comes back as the error's message. Events are
 * queued and posted by the server, which tries again for about two days when the
 * receiver does not take one.
 */
export async function updateOutboundWebhook(orgId: string, url: string, secret?: string): Promise<void> {
  if (servesDemoData()) return; // no-op in demo
  const res = await authFetch(`/api/webhook/organizations/${orgId}/outbound`, {
    method: 'PUT',
    body: JSON.stringify({ url, secret }),
  });
  await ensureOk(res);
}
