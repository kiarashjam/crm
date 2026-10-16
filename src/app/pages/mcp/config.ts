export type McpClient = 'cursor' | 'claude' | 'vscode';

/** Claude calls the CRM from the public internet, so a local or http address will not connect. */
export function isReachableByClaude(url: string): boolean {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:') return false;
    const host = parsed.hostname.toLowerCase();
    return host !== 'localhost' && host !== '127.0.0.1' && host !== '[::1]';
  } catch {
    return false;
  }
}

export function exampleQuestions(orgName: string): string[] {
  return [
    `List the recent leads in ${orgName}.`,
    `Which tasks are overdue in ${orgName}?`,
    `Give me a snapshot of leads and tasks in ${orgName}.`,
    `Search ${orgName} for contacts at Acme.`,
  ];
}

export function mcpServerName(orgName: string): string {
  const slug = orgName
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return `cadence-${slug || 'org'}`;
}

export function buildMcpConfig(
  client: McpClient,
  options: { serverName: string; url: string; apiKey: string },
): string {
  const headers = { Authorization: `Bearer ${options.apiKey}` };
  const entry =
    client === 'cursor'
      ? { url: options.url, headers }
      : { type: 'http', url: options.url, headers };

  const body =
    client === 'vscode'
      ? { servers: { [options.serverName]: entry } }
      : { mcpServers: { [options.serverName]: entry } };

  return JSON.stringify(body, null, 2);
}
