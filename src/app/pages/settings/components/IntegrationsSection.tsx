import { useEffect, useState } from 'react';
import { Link2, Copy, Loader2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { getWebhookInfo, updateWebhookPassword, type WebhookInfo } from '@/app/api/webhook';
import { isUsingRealApi } from '@/app/api/apiClient';
import type { Organization } from '@/app/api/organizations';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';

const EXAMPLE_JSON = `{
  "name": "Jane Doe",
  "email": "jane@example.com",
  "phone": "+15555550100",
  "companyName": "Acme Inc",
  "source": "website",
  "webhookPassword": "1234"
}`;

export function IntegrationsSection({ currentOrg }: { currentOrg: Organization | null }) {
  const [info, setInfo] = useState<WebhookInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [newPassword, setNewPassword] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isUsingRealApi() || !currentOrg?.id) {
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
      await updateWebhookPassword(currentOrg.id, newPassword.trim() || '');
      setNewPassword('');
      const fresh = await getWebhookInfo(currentOrg.id);
      setInfo(fresh);
      toast.success(newPassword.trim() ? 'Webhook password updated' : 'Reset to default password');
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
      toast.success('Using default webhook password again');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not reset password');
    } finally {
      setSaving(false);
    }
  };

  if (!isUsingRealApi()) {
    return (
      <div className="p-6 space-y-4">
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Link2 className="w-5 h-5 text-orange-600" />
          Integrations
        </h2>
        <p className="text-slate-600 text-sm">
          Connect the real API (<code className="text-xs bg-slate-100 px-1 rounded">VITE_API_URL</code>) to configure
          inbound webhooks from this screen.
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
        <p className="text-slate-600 text-sm">Select an organization to see webhook URLs and passwords.</p>
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
          POST JSON leads from external tools. Full API-key setup and code samples are on{' '}
          <Link to="/leads/webhook" className="text-orange-600 font-medium hover:underline">
            Lead webhook
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
            ? 'No custom password is saved. External calls should use the default password 1234 until you set one below.'
            : 'A custom password is saved. Use it instead of the default.'}
        </p>
        <div className="space-y-3 max-w-md">
          <Input
            type="password"
            autoComplete="new-password"
            placeholder="New password (leave empty to reset to default)"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
          <div className="flex flex-wrap gap-2">
            <Button type="button" onClick={handleSavePassword} disabled={saving}>
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save password'}
            </Button>
            <Button type="button" variant="outline" onClick={handleResetPassword} disabled={saving}>
              Reset to default (1234)
            </Button>
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
