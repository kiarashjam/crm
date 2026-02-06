import { authFetchJson } from './apiClient';

export interface WebhookInfo {
  webhookUrl: string;
  apiKey: string | null;
  apiKeyCreatedAt: string | null;
  hasApiKey: boolean;
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
