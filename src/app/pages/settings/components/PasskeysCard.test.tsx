// Settings > Security: the person's passkeys. Adding one asks the browser for a new
// credential against the server's options and confirms it is the account's owner with
// the password; removing one asks first; a server without passkeys says so instead.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import type { Passkey } from '@/app/api/signIn';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { PasskeysCard } = await import('./PasskeysCard');

const BASE = 'http://api.test';
const b64 = (...bytes: number[]) => btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
const buf = (...bytes: number[]) => new Uint8Array(bytes).buffer;

const macbook: Passkey = { id: 'pk-1', name: 'MacBook', createdAtUtc: '2026-10-01T09:00:00Z', lastUsedAtUtc: '2026-11-10T08:30:00Z', synced: true };

let create: ReturnType<typeof vi.fn>;

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch' }));
  toast.success.mockReset();
  toast.error.mockReset();
  // A browser that can make passkeys: what navigator.credentials.create answers.
  create = vi.fn(async () => ({
    id: 'new-cred', rawId: buf(7, 7), type: 'public-key',
    response: { clientDataJSON: buf(1), attestationObject: buf(2) },
  }));
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
  Object.defineProperty(navigator, 'credentials', { configurable: true, value: { create, get: vi.fn() } });
  server.use(http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })));
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});

describe('the passkeys card', () => {
  it('lists the passkeys, and adds one on this device with the password', async () => {
    let listed: Passkey[] = [macbook];
    let optionsBody: unknown;
    let registered: { ceremonyId: string; name: string; credential: { rawId: string; response: Record<string, string> } } | undefined;
    server.use(
      http.get(`${BASE}/api/auth/passkeys`, () => HttpResponse.json(listed)),
      http.post(`${BASE}/api/auth/passkeys/registration/options`, async ({ request }) => {
        optionsBody = await request.json();
        return HttpResponse.json({
          ceremonyId: 'ceremony-1',
          options: {
            rp: { id: 'app.cadence.test', name: 'Cadence' },
            user: { id: b64(1, 2, 3), name: 'anna@pavillon46.ch', displayName: 'Anna' },
            challenge: b64(9, 9),
            pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
            excludeCredentials: [{ type: 'public-key', id: b64(5) }],
            authenticatorSelection: { requireResidentKey: true, userVerification: 'required' },
            attestation: 'none',
          },
        });
      }),
      http.post(`${BASE}/api/auth/passkeys/registration`, async ({ request }) => {
        registered = await request.json() as typeof registered;
        const added: Passkey = { id: 'pk-2', name: registered!.name, createdAtUtc: '2026-11-12T10:00:00Z', lastUsedAtUtc: null, synced: false };
        listed = [macbook, added];
        return HttpResponse.json(added);
      }),
    );
    const user = userEvent.setup();
    render(withQueries(<PasskeysCard />));

    const list = await screen.findByRole('list', { name: 'Your passkeys' });
    expect(within(list).getByText('MacBook')).toBeInTheDocument();
    expect(within(list).getByText('Synced')).toBeInTheDocument();

    await user.clear(screen.getByLabelText('Name'));
    await user.type(screen.getByLabelText('Name'), 'Office YubiKey');
    await user.type(screen.getByLabelText('Your password'), 'correct horse 1');
    await user.click(screen.getByRole('button', { name: 'Add a passkey' }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Passkey “Office YubiKey” added. You can sign in with it now.'));
    expect(optionsBody).toEqual({ password: 'correct horse 1' });
    // The browser was asked with the server's options, its bytes as bytes.
    const asked = create.mock.calls[0]![0] as { publicKey: PublicKeyCredentialCreationOptions };
    expect(new Uint8Array(asked.publicKey.challenge as ArrayBuffer)).toEqual(new Uint8Array([9, 9]));
    expect(new Uint8Array(asked.publicKey.excludeCredentials![0]!.id as ArrayBuffer)).toEqual(new Uint8Array([5]));
    expect(registered?.ceremonyId).toBe('ceremony-1');
    expect(registered?.credential.rawId).toBe(b64(7, 7));
    expect(registered?.credential.response.attestationObject).toBe(b64(2));
    expect(await within(list).findByText('Office YubiKey')).toBeInTheDocument();
    expect(screen.getByLabelText('Your password')).toHaveValue('');
  });

  it('says why when the server refuses the password, and adds nothing', async () => {
    server.use(
      http.get(`${BASE}/api/auth/passkeys`, () => HttpResponse.json([])),
      http.post(`${BASE}/api/auth/passkeys/registration/options`, () =>
        HttpResponse.json({ detail: 'That password is not right.', errorCode: 'Auth.WrongPassword' }, { status: 400 })),
    );
    const user = userEvent.setup();
    render(withQueries(<PasskeysCard />));

    expect(await screen.findByText('No passkeys yet.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Your password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Add a passkey' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('That password is not right.'));
    expect(create).not.toHaveBeenCalled();
  });

  it('removes a passkey only once confirmed', async () => {
    let removed: string | undefined;
    server.use(
      http.get(`${BASE}/api/auth/passkeys`, () => HttpResponse.json(removed ? [] : [macbook])),
      http.delete(`${BASE}/api/auth/passkeys/:id`, ({ params }) => {
        removed = String(params.id);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    render(withQueries(<PasskeysCard />));

    await user.click(await screen.findByRole('button', { name: 'Remove passkey MacBook' }));
    expect(removed).toBeUndefined();
    expect(screen.getByText('Remove it?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(removed).toBe('pk-1'));
    expect(await screen.findByText('No passkeys yet.')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Passkey “MacBook” removed.');
  });

  it('says passkeys are not set up when the server offers none, and offers no way to add one', async () => {
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: false })),
      http.get(`${BASE}/api/auth/passkeys`, () => HttpResponse.json([])),
    );
    render(withQueries(<PasskeysCard />));

    expect(await screen.findByText('Passkeys are not set up on this server.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add a passkey' })).not.toBeInTheDocument();
  });

  it('shows a failed read with a way to try again, never an empty list', async () => {
    let fail = true;
    server.use(http.get(`${BASE}/api/auth/passkeys`, () =>
      (fail ? HttpResponse.json({ detail: 'Server error' }, { status: 500 }) : HttpResponse.json([macbook]))));
    const user = userEvent.setup();
    render(withQueries(<PasskeysCard />));

    const retry = await screen.findByRole('button', { name: /try again/i });
    expect(screen.queryByText('No passkeys yet.')).not.toBeInTheDocument();
    fail = false;
    await user.click(retry);
    expect(await screen.findByText('MacBook')).toBeInTheDocument();
  });
});
