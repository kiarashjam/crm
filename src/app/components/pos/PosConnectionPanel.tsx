// The POS connection, for someone who takes payments: connect the club's Lightspeed account
// (Lightspeed's own page asks the club to allow it, then sends the browser back), choose the
// location whose tickets come in, say what each of the POS's payment methods means, and turn
// the import on. The tokens never come back to the browser; the page sees only whether the
// account is connected.

import { useEffect, useState } from 'react';
import { Link2, Loader2, Plus, RefreshCw, Trash2, Unplug } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { LoadFailed } from '@/app/components/LoadFailed';
import { ApiError } from '@/app/api/apiClient';
import {
  connectPos, disconnectPos, importPosNow, POS_MEANS, POS_MEANS_LABEL, savePosSettings,
  type PosMeans, type PosSettings,
} from '@/app/api/pos';
import { TAB_VENUES, VENUE_LABELS } from '@/app/api/tabs';
import { usePosLocations, usePosSettings } from '@/app/hooks/queries';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function formatDateTime(iso?: string): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

/** A date input's value (local day) for an ISO time. */
function dayOf(iso?: string): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

interface Form {
  enabled: boolean;
  locationId: string;
  locationName: string;
  importFrom: string;
  defaultVenue: string;
  methods: { code: string; means: PosMeans }[];
}

const formOf = (s: PosSettings): Form => ({
  enabled: s.enabled,
  locationId: s.locationId ?? '',
  locationName: s.locationName ?? '',
  importFrom: dayOf(s.importFromUtc),
  defaultVenue: s.defaultVenue || 'Dining',
  methods: Object.entries(s.methodMap ?? {}).map(([code, means]) => ({ code, means })),
});

export function PosConnectionPanel({ canManage }: { canManage: boolean }) {
  const settingsQuery = usePosSettings(canManage);
  const settings = settingsQuery.data;
  const locationsQuery = usePosLocations(canManage && !!settings?.connected && !settings.tokenUnreadable);
  const [form, setForm] = useState<Form | null>(null);
  const [saving, setSaving] = useState(false);
  const [working, setWorking] = useState<null | 'connect' | 'disconnect' | 'import'>(null);

  // The form starts from what the server keeps, and again after each save it answers.
  useEffect(() => {
    if (settings) setForm(formOf(settings));
  }, [settings]);

  if (!canManage) {
    return (
      <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">
        Only someone who takes payments connects the POS and changes its settings.
      </p>
    );
  }
  if (settingsQuery.isError && !settings) {
    return <LoadFailed what="The POS settings" error={settingsQuery.error} onRetry={() => void settingsQuery.refetch()} retrying={settingsQuery.isFetching} />;
  }
  if (!settings || !form) {
    return <div className="flex items-center justify-center py-10 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /></div>;
  }

  const connect = async () => {
    setWorking('connect');
    try {
      window.location.assign(await connectPos());
    } catch (err) {
      toast.error(message(err, 'Could not start connecting Lightspeed'));
      setWorking(null);
    }
  };

  const disconnect = async () => {
    if (!window.confirm('Disconnect Lightspeed? No more tickets come in until it is connected again. Tickets already imported stay.')) return;
    setWorking('disconnect');
    try {
      await disconnectPos();
      toast.success('Lightspeed disconnected');
    } catch (err) {
      toast.error(message(err, 'Could not disconnect Lightspeed'));
    } finally {
      setWorking(null);
    }
  };

  const importNow = async () => {
    setWorking('import');
    try {
      const r = await importPosNow();
      toast.success(`${r.imported} new ticket${r.imported === 1 ? '' : 's'}: ${r.posted} posted, ${r.unmatched} need a member, ${r.failed} not posted`);
    } catch (err) {
      toast.error(message(err, 'Could not import from Lightspeed'));
    } finally {
      setWorking(null);
    }
  };

  const save = async () => {
    const methods = form.methods.filter((m) => m.code.trim());
    // The date goes back only when it was changed here. Sent with every save, it moved the moment
    // the import was turned on back to that day's midnight, billing what staff had rung up by
    // hand that morning, and after three months the server refused every save for its age.
    const importFromChanged = form.importFrom !== dayOf(settings.importFromUtc);
    setSaving(true);
    try {
      await savePosSettings({
        enabled: form.enabled,
        locationId: form.locationId.trim(),
        locationName: form.locationName.trim(),
        ...(importFromChanged
          ? { importFromUtc: form.importFrom ? new Date(`${form.importFrom}T00:00:00`).toISOString() : '' }
          : {}),
        defaultVenue: form.defaultVenue,
        methodMap: Object.fromEntries(methods.map((m) => [m.code.trim(), m.means])),
        version: settings.version,
      });
      toast.success('POS settings saved');
    } catch (err) {
      toast.error(message(err, 'Could not save the POS settings'));
      if (err instanceof ApiError && err.status === 409) void settingsQuery.refetch();
    } finally {
      setSaving(false);
    }
  };

  const set = (patch: Partial<Form>) => setForm({ ...form, ...patch });
  const locations = locationsQuery.data ?? [];

  return (
    <div className="max-w-3xl space-y-5">
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="mb-1 font-semibold text-slate-900">Lightspeed Restaurant (K-Series)</h3>
        {!settings.serverConfigured ? (
          <p className="text-sm text-slate-600">
            Lightspeed is not set up on this server yet: the owner adds Cadence&apos;s Lightspeed client id and secret to the server&apos;s
            settings, and registers <span className="font-mono text-xs">{settings.redirectUri}</span> with Lightspeed as its redirect address.
          </p>
        ) : settings.connected ? (
          <div className="space-y-2 text-sm text-slate-700">
            <p>
              Connected{settings.connectedAtUtc ? ` on ${formatDateTime(settings.connectedAtUtc)}` : ''}
              {settings.connectedByName ? ` by ${settings.connectedByName}` : ''}.
            </p>
            {settings.tokenUnreadable && (
              <p className="rounded-lg bg-amber-50 px-3 py-2 text-amber-900">The saved connection can no longer be read on this server. Connect again.</p>
            )}
            <p className="text-slate-500">
              {settings.lastImportAtUtc
                ? `Last import ${formatDateTime(settings.lastImportAtUtc)}: ${settings.lastImportCount} new ticket${settings.lastImportCount === 1 ? '' : 's'}.`
                : 'Nothing imported yet.'}
            </p>
            {settings.lastImportError && <p className="rounded-lg bg-rose-50 px-3 py-2 text-rose-800">The last import stopped: {settings.lastImportError}</p>}
            <div className="flex flex-wrap gap-2 pt-1">
              <Button type="button" variant="outline" onClick={() => void importNow()} disabled={working !== null || !settings.locationId}>
                {working === 'import' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-1 h-4 w-4" />} Import now
              </Button>
              {settings.tokenUnreadable && (
                <Button type="button" onClick={() => void connect()} disabled={working !== null}>
                  <Link2 className="mr-1 h-4 w-4" /> Connect again
                </Button>
              )}
              <Button type="button" variant="ghost" onClick={() => void disconnect()} disabled={working !== null}>
                <Unplug className="mr-1 h-4 w-4" /> Disconnect
              </Button>
            </div>
          </div>
        ) : (
          <div className="space-y-2 text-sm text-slate-600">
            <p>
              Connect the club&apos;s Lightspeed account. Lightspeed asks you to allow Cadence to read its sales, then brings you back
              here. Closed tickets then come in every few minutes and go on the accounts of the members whose code is on them.
            </p>
            <Button type="button" onClick={() => void connect()} disabled={working !== null}>
              {working === 'connect' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Link2 className="mr-1 h-4 w-4" />} Connect Lightspeed
            </Button>
          </div>
        )}
      </section>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="font-semibold text-slate-900">Import</h3>
        <div>
          <Label htmlFor="pos-location">Location</Label>
          {locations.length > 0 ? (
            <select
              id="pos-location"
              className="mt-1 block w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
              value={form.locationId}
              onChange={(e) => set({ locationId: e.target.value, locationName: locations.find((l) => l.id === e.target.value)?.name ?? '' })}
            >
              <option value="">Choose the location</option>
              {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          ) : (
            <Input
              id="pos-location"
              className="mt-1"
              value={form.locationId}
              onChange={(e) => set({ locationId: e.target.value })}
              placeholder="Lightspeed's business location id"
            />
          )}
          <p className="mt-1 text-xs text-slate-500">Whose closed tickets come in: one location of the account.</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <Label htmlFor="pos-from">Import tickets closed from</Label>
            <Input id="pos-from" type="date" className="mt-1" value={form.importFrom} onChange={(e) => set({ importFrom: e.target.value })} />
            <p className="mt-1 text-xs text-slate-500">
              Nothing closed before this is imported. Left empty, from when the import is turned on or first run. Changed, or with
              another location, tickets are read again from this date; those already in are not added twice.
            </p>
          </div>
          <div>
            <Label htmlFor="pos-venue">Counted as sold at</Label>
            <select
              id="pos-venue"
              className="mt-1 block w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
              value={form.defaultVenue}
              onChange={(e) => set({ defaultVenue: e.target.value })}
            >
              {TAB_VENUES.map((v) => <option key={v} value={v}>{VENUE_LABELS[v]}</option>)}
            </select>
            <p className="mt-1 text-xs text-slate-500">Sets the charge kind and the points&apos; reason for the POS&apos;s sales.</p>
          </div>
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.enabled} onChange={(e) => set({ enabled: e.target.checked })} disabled={!settings.connected} />
          Import closed tickets every few minutes
        </label>
      </section>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div>
          <h3 className="font-semibold text-slate-900">Payment methods</h3>
          <p className="text-sm text-slate-500">
            What each of the POS&apos;s payment methods means, by its code or name. One not listed is read from its name (Visa, Cash,
            TWINT, House account…); a ticket paid by a method that can&apos;t be told waits here until it is listed.
          </p>
        </div>
        {form.methods.map((m, i) => (
          <div key={i} className="flex flex-wrap items-center gap-2">
            <Input
              aria-label={`Payment method ${i + 1}`}
              className="w-48"
              value={m.code}
              onChange={(e) => set({ methods: form.methods.map((x, j) => (j === i ? { ...x, code: e.target.value } : x)) })}
              placeholder="ACCOUNT, Rechnung…"
            />
            <select
              aria-label={`What payment method ${i + 1} means`}
              className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
              value={m.means}
              onChange={(e) => set({ methods: form.methods.map((x, j) => (j === i ? { ...x, means: e.target.value as PosMeans } : x)) })}
            >
              {POS_MEANS.map((means) => <option key={means} value={means}>{POS_MEANS_LABEL[means]}</option>)}
            </select>
            <Button type="button" size="icon" variant="ghost" aria-label={`Remove payment method ${i + 1}`}
              onClick={() => set({ methods: form.methods.filter((_, j) => j !== i) })}>
              <Trash2 className="h-4 w-4 text-slate-500" />
            </Button>
          </div>
        ))}
        <Button type="button" variant="outline" size="sm" onClick={() => set({ methods: [...form.methods, { code: '', means: 'HouseAccount' }] })}>
          <Plus className="mr-1 h-3.5 w-3.5" /> Add a payment method
        </Button>
      </section>

      <div className="flex justify-end">
        <Button type="button" onClick={() => void save()} disabled={saving}>
          {saving ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null} Save POS settings
        </Button>
      </div>
    </div>
  );
}
