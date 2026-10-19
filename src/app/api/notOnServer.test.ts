// The list of frontend-only features, checked against the backend's controllers.
import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NOT_ON_SERVER, apiBase, isNotOnServer } from './notOnServer';

const root = resolve(__dirname, '../../..');

/** Every `api/<first segment>` a backend controller answers. */
function servedBases(): Set<string> {
  const dir = join(root, 'backend/src/ACI.WebApi/Controllers');
  const bases = new Set<string>();
  for (const file of readdirSync(dir).filter((f) => f.endsWith('.cs'))) {
    const source = readFileSync(join(dir, file), 'utf8');
    const controller = file.replace(/Controller\.cs$/, '');
    for (const [, route] of source.matchAll(/\[Route\("([^"]+)"\)\]/g)) {
      const base = apiBase(`/${route!.replace('[controller]', controller)}`);
      if (base) bases.add(base);
    }
  }
  return bases;
}

/** Every `/api/<first segment>` the app's own code calls. */
function calledBases(): Map<string, string> {
  const found = new Map<string, string>();
  const walk = (dir: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, entry.name);
      if (entry.isDirectory()) walk(path);
      else if (/\.(ts|tsx)$/.test(entry.name) && !/\.test\./.test(entry.name)) {
        for (const [, base] of readFileSync(path, 'utf8').matchAll(/['`]\/api\/([a-z0-9-]+)/gi)) {
          if (!found.has(base!.toLowerCase())) found.set(base!.toLowerCase(), path.slice(root.length + 1));
        }
      }
    }
  };
  walk(join(root, 'src/app'));
  return found;
}

describe('features the backend does not serve', () => {
  it('none of them has a controller — when one gains one, take it off the list', () => {
    const served = servedBases();
    expect(served.has('leads')).toBe(true); // the scan works
    expect(NOT_ON_SERVER.filter((base) => served.has(base))).toEqual([]);
  });

  it('every API the app calls is either served or on the list', () => {
    const served = servedBases();
    const unanswered = [...calledBases()].filter(([base]) => !served.has(base) && !isNotOnServer(`/api/${base}`));
    expect(unanswered.map(([base, file]) => `${base} (${file})`)).toEqual([]);
  });
});

describe('authFetch', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sends requests for features the server now serves', async () => {
    vi.stubEnv('VITE_API_URL', 'https://api.example.test');
    const fetch = vi.fn().mockResolvedValue(new Response('[]', { status: 200 }));
    vi.stubGlobal('fetch', fetch);
    const { authFetch } = await import('./apiClient');

    expect(isNotOnServer('/api/members')).toBe(false);
    expect(isNotOnServer('/api/events')).toBe(false);
    const res = await authFetch('/api/members');
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(res.status).toBe(200);
    vi.unstubAllEnvs();
  });

  it('still sends a write, so the server can refuse a Viewer', async () => {
    vi.stubEnv('VITE_API_URL', 'https://api.example.test');
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 403 }));
    vi.stubGlobal('fetch', fetch);
    const { authFetch } = await import('./apiClient');
    const res = await authFetch('/api/members', { method: 'POST', body: '{}' });
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(res.status).toBe(403);
    vi.unstubAllEnvs();
  });
});

describe('apiBase', () => {
  it('reads the first segment after /api/, from a path or a full URL', () => {
    expect(apiBase('/api/custom-fields/values?entityType=lead')).toBe('custom-fields');
    expect(apiBase('https://api.cadencee.ai/api/attachments?x=1')).toBe('attachments');
    expect(apiBase('/health')).toBeNull();
  });
});
