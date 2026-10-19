import { authFetch, authFetchJson, ensureOk, servesDemoData } from './apiClient';

export interface WebhookInfo {
  webhookUrl: string;
  apiKey: string | null;
  apiKeyCreatedAt: string | null;
  hasApiKey: boolean;
  usesDefaultWebhookPassword: boolean;
  passwordWebhookUrl: string;
  /** Where Cadence posts lead and deal events, if anywhere. */
  outboundWebhookUrl?: string | null;
  /** Whether events are signed. The secret itself is never sent back. */
  hasOutboundWebhookSecret?: boolean;
}

/** A read-only placeholder returned in demo mode so the webhook page renders
 * cleanly instead of crashing. UI consumers should treat `hasApiKey: false`
 * and the `demo://…` URLs as "not configured". */
const DEMO_WEBHOOK_INFO: WebhookInfo = {
  webhookUrl: 'demo://webhook/leads',
  apiKey: null,
  apiKeyCreatedAt: null,
  hasApiKey: false,
  usesDefaultWebhookPassword: true,
  passwordWebhookUrl: 'demo://webhook/leads-password',
};

export async function getWebhookInfo(orgId: string): Promise<WebhookInfo> {
  if (servesDemoData()) return DEMO_WEBHOOK_INFO;
  return authFetchJson<WebhookInfo>(`/api/webhook/organizations/${orgId}`);
}

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

/**
 * Where Cadence posts lead.created, deal.stage_changed and deal.won, as JSON. An empty
 * URL turns it off. A secret signs each body (X-Cadence-Signature, hex HMAC-SHA256);
 * leave it undefined to keep the saved one. Owner or manager only; the address must be
 * public https, and the server's reason comes back as the error's message.
 */
export async function updateOutboundWebhook(orgId: string, url: string, secret?: string): Promise<void> {
  if (servesDemoData()) return; // no-op in demo
  const res = await authFetch(`/api/webhook/organizations/${orgId}/outbound`, {
    method: 'PUT',
    body: JSON.stringify({ url, secret }),
  });
  await ensureOk(res);
}
