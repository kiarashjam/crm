import { authFetch, authFetchJson } from './apiClient';

export interface WebhookInfo {
  webhookUrl: string;
  apiKey: string | null;
  apiKeyCreatedAt: string | null;
  hasApiKey: boolean;
  usesDefaultWebhookPassword: boolean;
  passwordWebhookUrl: string;
}

export async function getWebhookInfo(orgId: string): Promise<WebhookInfo> {
  return authFetchJson<WebhookInfo>(`/api/webhook/organizations/${orgId}`);
}

export async function generateWebhookApiKey(orgId: string): Promise<string> {
  const result = await authFetchJson<{ apiKey: string }>(`/api/webhook/organizations/${orgId}/generate-key`, {
    method: 'POST',
  });
  return result.apiKey;
}

/** Sets the JSON webhook password, or clears it (empty string) to use the app default. */
export async function updateWebhookPassword(orgId: string, password: string | null): Promise<void> {
  const res = await authFetch(`/api/webhook/organizations/${orgId}/webhook-password`, {
    method: 'PUT',
    body: JSON.stringify({ password: password ?? '' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
}
