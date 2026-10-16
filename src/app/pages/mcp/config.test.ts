import { describe, expect, it } from 'vitest';
import { buildMcpConfig, exampleQuestions, isReachableByClaude, mcpServerName } from './config';

describe('mcpServerName', () => {
  it('turns an organization name into a stable server id', () => {
    expect(mcpServerName('Pavilion 46')).toBe('cadence-pavilion-46');
    expect(mcpServerName('  Acme / North  ')).toBe('cadence-acme-north');
    expect(mcpServerName('---')).toBe('cadence-org');
  });
});

describe('isReachableByClaude', () => {
  it('accepts a public https address', () => {
    expect(isReachableByClaude('https://api.example.com/mcp')).toBe(true);
  });

  it('rejects addresses Claude cannot call', () => {
    expect(isReachableByClaude('http://localhost:5160/mcp')).toBe(false);
    expect(isReachableByClaude('https://127.0.0.1/mcp')).toBe(false);
    expect(isReachableByClaude('http://api.example.com/mcp')).toBe(false);
  });
});

describe('exampleQuestions', () => {
  it('names the organization in every question', () => {
    expect(exampleQuestions('Northwind').every((q) => q.includes('Northwind'))).toBe(true);
  });
});

describe('buildMcpConfig', () => {
  const options = {
    serverName: 'cadence-pavilion-46',
    url: 'https://api.example.com/mcp',
    apiKey: 'aci_test',
  };

  it('writes a Cursor config that carries the organization key', () => {
    const parsed = JSON.parse(buildMcpConfig('cursor', options));
    expect(parsed.mcpServers['cadence-pavilion-46'].url).toBe(options.url);
    expect(parsed.mcpServers['cadence-pavilion-46'].headers.Authorization).toBe('Bearer aci_test');
  });

  it('uses an HTTP connector for Claude, with the organization key in the header', () => {
    const parsed = JSON.parse(buildMcpConfig('claude', options));
    expect(parsed.mcpServers['cadence-pavilion-46'].type).toBe('http');
    expect(parsed.mcpServers['cadence-pavilion-46'].url).toBe(options.url);
    expect(parsed.mcpServers['cadence-pavilion-46'].headers.Authorization).toBe('Bearer aci_test');
  });

  it('uses the VS Code server shape', () => {
    const parsed = JSON.parse(buildMcpConfig('vscode', options));
    expect(parsed.servers['cadence-pavilion-46'].type).toBe('http');
    expect(parsed.servers['cadence-pavilion-46'].url).toBe(options.url);
  });
});
