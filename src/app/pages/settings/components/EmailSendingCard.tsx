// Who the workspace's email goes out from. Every workspace sends from one shared
// address, the only one the mail service accepts until a workspace's own domain has
// been verified with it. A workspace may save its own address here; it is used only
// once its domain is verified, and the card says which address is used today rather
// than claiming the saved one is.

import { useEffect, useState } from 'react';
import { Mail } from 'lucide-react';
import { toast } from 'sonner';
import { useOrg } from '@/app/contexts/OrgContext';
import { getEmailSending, isOrgAdmin, updateEmailSending, type EmailSending } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';

const field = 'w-full h-11 px-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none disabled:bg-slate-50 disabled:text-slate-600';

export function EmailSendingCard() {
  const { currentOrg } = useOrg();
  const [sender, setSender] = useState<EmailSending | null>(null);
  const [address, setAddress] = useState('');
  const [saving, setSaving] = useState(false);
  const orgId = currentOrg?.id;

  useEffect(() => {
    if (!orgId || servesDemoData()) return;
    let cancelled = false;
    getEmailSending(orgId)
      .then((found) => {
        if (cancelled || !found) return;
        setSender(found);
        setAddress(found.sendingAddress ?? '');
      })
      .catch(() => { if (!cancelled) setSender(null); });
    return () => { cancelled = true; };
  }, [orgId]);

  // Demo workspaces send nothing; without an answer from the server there is nothing true to show.
  if (!currentOrg || servesDemoData() || !sender) return null;
  const canEdit = isOrgAdmin(currentOrg);
  const from = sender.verified && sender.sendingAddress ? sender.sendingAddress : sender.sharedAddress;
  const domain = sender.sendingAddress?.split('@')[1];
  const changed = address.trim() !== (sender.sendingAddress ?? '');

  const save = async () => {
    setSaving(true);
    try {
      const saved = await updateEmailSending(currentOrg.id, address.trim());
      setSender(saved);
      setAddress(saved.sendingAddress ?? '');
      toast.success(!saved.sendingAddress ? 'Sending address removed.' : saved.verified
        ? `Email now goes out from ${saved.sendingAddress}.`
        : `Saved. Email still goes out from ${saved.sharedAddress ?? 'the shared address'} until ${saved.sendingAddress.split('@')[1]} is verified.`);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The address was not saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="email-sending" className="pt-6 border-t border-slate-200">
      <h3 id="email-sending" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <Mail className="w-4 h-4" aria-hidden />
        Email sender
      </h3>
      <p className="text-sm text-slate-600 mb-3">
        Email from this workspace (the composer, the assistant, sequences and automations) goes out under its
        name. A reply reaches the person who wrote it
        {sender.repliesCaptured ? ', and comes back onto the record’s timeline' : ''}.
      </p>
      {from && (
        <p className="text-sm text-slate-700 mb-4">
          It goes out as <span className="font-medium">{currentOrg.name} &lt;{from}&gt;</span>.
        </p>
      )}
      <label className="block text-sm font-medium text-slate-700 max-w-md">
        The workspace’s own sending address
        <input
          type="email"
          className={`${field} mt-1.5`}
          placeholder="hello@yourclub.ch"
          value={address}
          disabled={!canEdit || saving}
          onChange={(e) => setAddress(e.target.value)}
        />
      </label>
      {sender.sendingAddress && (
        sender.verified ? (
          <p className="mt-2 text-xs text-emerald-700" role="status">
            {domain} is verified with the mail service, so email goes out from {sender.sendingAddress}.
          </p>
        ) : (
          <p className="mt-2 text-xs text-amber-700" role="status">
            Not used yet: {domain} has not been verified with the mail service, so email still goes out from{' '}
            {sender.sharedAddress ?? 'the shared address'}. Whoever runs Cadence authenticates the domain with the mail
            service (a few DNS records on it) and then switches it on.
          </p>
        )
      )}
      {canEdit ? (
        <button
          type="button"
          onClick={save}
          disabled={!changed || saving}
          className="mt-4 h-10 px-5 bg-orange-600 hover:bg-orange-700 disabled:opacity-50 text-white font-medium rounded-xl transition-colors"
        >
          {saving ? 'Saving…' : 'Save'}
        </button>
      ) : (
        <p className="mt-4 text-xs text-slate-500">Only the owner or a manager can change this.</p>
      )}
    </section>
  );
}
