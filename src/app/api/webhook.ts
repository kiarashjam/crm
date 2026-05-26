import { authFetch, authFetchJson, isUsingRealApi } from './apiClient';

export interface WebhookInfo {
  webhookUrl: string;
  apiKey: string | null;
  apiKeyCreatedAt: string | null;
  hasApiKey: boolean;
  usesDefaultWebhookPassword: boolean;
  passwordWebhookUrl: string;
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
  if (!isUsingRealApi()) return DEMO_WEBHOOK_INFO;
  return authFetchJson<WebhookInfo>(`/api/webhook/organizations/${orgId}`);
}

export async function generateWebhookApiKey(orgId: string): Promise<string> {
  if (!isUsingRealApi()) {
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
  if (!isUsingRealApi()) return; // no-op in demo
  const res = await authFetch(`/api/webhook/organizations/${orgId}/webhook-password`, {
    method: 'PUT',
    body: JSON.stringify({ password: password ?? '' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
}
