import { useEffect, useState } from 'react';
import { Link2, Copy, Loader2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { getWebhookInfo, updateOutboundWebhook, updateWebhookPassword, type WebhookInfo } from '@/app/api/webhook';
import { servesDemoData } from '@/app/api/apiClient';
import type { Organization } from '@/app/api/organizations';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { usePageLoading } from '@/app/components/LoadingOverlay';

const EXAMPLE_JSON = `{
  "name": "Jane Doe",
  "email": "jane@example.com",
  "phone": "+15555550100",
  "companyName": "Acme Inc",
  "source": "website",
  "webhookPassword": "the password you saved below"
}`;

export function IntegrationsSection({ currentOrg }: { currentOrg: Organization | null }) {
  const [info, setInfo] = useState<WebhookInfo | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [newPassword, setNewPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [outboundUrl, setOutboundUrl] = useState('');
  const [outboundSecret, setOutboundSecret] = useState('');
  const [savingOutbound, setSavingOutbound] = useState(false);

  useEffect(() => {
    setOutboundUrl(info?.outboundWebhookUrl ?? '');
    setOutboundSecret('');
  }, [info]);

  const saveOutbound = async (url: string) => {
    if (!currentOrg?.id) return;
    setSavingOutbound(true);
    try {
      await updateOutboundWebhook(currentOrg.id, url.trim(), outboundSecret.trim() || undefined);
      setInfo(await getWebhookInfo(currentOrg.id));
      toast.success(url.trim() ? 'Events will be posted to that address' : 'Event posting turned off');
    } catch (e) {
      // The server says why: not https, not a public address, or owner and managers only.
      toast.error(e instanceof Error ? e.message : 'Could not save the address');
    } finally {
      setSavingOutbound(false);
    }
  };

  useEffect(() => {
    if (servesDemoData() || !currentOrg?.id) {
      setInfo(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    getWebhookInfo(currentOrg.id)
      .then((data) => {
        if (!cancelled) setInfo(data);
      })
      .catch(() => {
        if (!cancelled) {
          setInfo(null);
          toast.error('Could not load webhook settings.');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [currentOrg?.id]);

  const copy = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success(`${label} copied`);
    } catch {
      toast.error('Could not copy');
    }
  };

  const handleSavePassword = async () => {
    if (!currentOrg?.id) return;
    setSaving(true);
    try {
      const password = newPassword.trim();
      await updateWebhookPassword(currentOrg.id, password);
      setNewPassword('');
      const fresh = await getWebhookInfo(currentOrg.id);
      setInfo(fresh);
      toast.success(password ? 'Webhook password updated' : 'Password webhook turned off');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not update password');
    } finally {
      setSaving(false);
    }
  };

  const handleResetPassword = async () => {
    if (!currentOrg?.id) return;
    setSaving(true);
    try {
      await updateWebhookPassword(currentOrg.id, '');
      setNewPassword('');
      const fresh = await getWebhookInfo(currentOrg.id);
      setInfo(fresh);
      toast.success('Password webhook turned off until you save a new password');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not reset password');
    } finally {
      setSaving(false);
    }
  };

  if (servesDemoData()) {
    return (
      <div className="p-6 space-y-4">
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Link2 className="w-5 h-5 text-orange-600" />
          Integrations
        </h2>
        <p className="text-slate-600 text-sm">
          Connect the real API (<code className="text-xs bg-slate-100 px-1 rounded">VITE_API_URL</code>) to configure
          inbound webhooks from this screen. To ask Claude about this organization, open{' '}
          <Link to="/settings?tab=claude" className="text-orange-700 font-medium hover:underline">Claude</Link>.
        </p>
      </div>
    );
  }

  if (!currentOrg?.id) {
    return (
      <div className="p-6 space-y-4">
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Link2 className="w-5 h-5 text-orange-600" />
          Integrations
        </h2>
        <p className="text-slate-600 text-sm">
          Select an organization to see webhook URLs and passwords. Claude setup is on{' '}
          <Link to="/settings?tab=claude" className="text-orange-700 font-medium hover:underline">Claude</Link>.
        </p>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="p-12 flex justify-center items-center text-slate-500 gap-2">
        <Loader2 className="w-6 h-6 animate-spin" />
        Loading…
      </div>
    );
  }

  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Link2 className="w-5 h-5 text-orange-600" />
          Integrations
        </h2>
        <p className="text-slate-600 text-sm mt-1">
          POST JSON leads from external tools on{' '}
          <Link to="/leads/webhook" className="text-orange-700 font-medium hover:underline">
            Lead webhook
          </Link>
          . To let Claude read this organization’s leads and tasks, follow{' '}
          <Link to="/settings?tab=claude" className="text-orange-700 font-medium hover:underline">
            Claude
          </Link>
          .
        </p>
      </div>

      <div>
        <h3 className="font-semibold text-slate-900 mb-2">JSON webhook URL</h3>
        <p className="text-sm text-slate-600 mb-3">
          Use this URL with header <code className="text-xs bg-slate-100 px-1 rounded">X-Webhook-Password</code> or a{' '}
          <code className="text-xs bg-slate-100 px-1 rounded">webhookPassword</code> field in the JSON body.
        </p>
        <div className="flex gap-2 flex-col sm:flex-row">
          <Input readOnly value={info?.passwordWebhookUrl ?? ''} className="font-mono text-sm" aria-label="JSON webhook URL" />
          <Button
            type="button"
            variant="outline"
            className="shrink-0"
            disabled={!info?.passwordWebhookUrl}
            onClick={() => info?.passwordWebhookUrl && copy(info.passwordWebhookUrl, 'Webhook URL')}
          >
            <Copy className="w-4 h-4 mr-2" />
            Copy URL
          </Button>
        </div>
      </div>

      <div>
        <h3 className="font-semibold text-slate-900 mb-2">Webhook password</h3>
        <p className="text-sm text-slate-600 mb-3">
          {info?.usesDefaultWebhookPassword
            ? 'No password is saved, so this address will refuse leads until you set one. The API-key address above still works.'
            : 'A password is saved. Send it with each lead, in the header or in the JSON.'}
        </p>
        <div className="space-y-3 max-w-md">
          <Input
            type="password"
            autoComplete="new-password"
            placeholder="New password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
          <div className="flex flex-wrap gap-2">
            <Button type="button" onClick={handleSavePassword} disabled={saving}>
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save password'}
            </Button>
            <Button type="button" variant="outline" onClick={handleResetPassword} disabled={saving}>
              Turn off password webhook
            </Button>
          </div>
        </div>
      </div>

      <div>
        <h3 className="font-semibold text-slate-900 mb-2">Send events to your systems</h3>
        <p className="text-sm text-slate-600 mb-3">
          Cadence posts each new lead, deal stage change and won deal to this address as JSON. It has
          to be a public <code className="bg-slate-100 text-slate-800 px-1 rounded">https</code> address. With a
          secret, each post carries <code className="bg-slate-100 text-slate-800 px-1 rounded">X-Cadence-Signature</code>,
          the hex HMAC-SHA256 of the body, so your side can check it came from here.
        </p>
        <div className="space-y-3 max-w-md">
          <Input
            type="url"
            inputMode="url"
            placeholder="https://hooks.example.com/cadence"
            aria-label="Address to post events to"
            value={outboundUrl}
            onChange={(e) => setOutboundUrl(e.target.value)}
          />
          <Input
            type="password"
            autoComplete="new-password"
            aria-label="Signing secret"
            placeholder={info?.hasOutboundWebhookSecret ? 'Signing secret saved; type a new one to change it' : 'Signing secret (optional)'}
            value={outboundSecret}
            onChange={(e) => setOutboundSecret(e.target.value)}
          />
          <div className="flex flex-wrap gap-2">
            <Button type="button" onClick={() => saveOutbound(outboundUrl)} disabled={savingOutbound || !outboundUrl.trim()}>
              {savingOutbound ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save address'}
            </Button>
            {info?.outboundWebhookUrl && (
              <Button type="button" variant="outline" onClick={() => saveOutbound('')} disabled={savingOutbound}>
                Stop posting events
              </Button>
            )}
          </div>
        </div>
      </div>

      <div>
        <h3 className="font-semibold text-slate-900 mb-2">Example JSON body</h3>
        <pre className="text-xs bg-slate-900 text-slate-100 p-4 rounded-xl overflow-x-auto whitespace-pre-wrap">{EXAMPLE_JSON}</pre>
        <p className="text-xs text-slate-500 mt-2">
          Replace <code className="bg-slate-100 text-slate-800 px-1 rounded">webhookPassword</code> with your saved password,
          or remove that field and send the password in the{' '}
          <code className="bg-slate-100 text-slate-800 px-1 rounded">X-Webhook-Password</code> header instead.
        </p>
      </div>
    </div>
  );
}
