// Who the workspace's email goes out from. Every workspace sends from one shared
// address, the only one the mail service accepts until a workspace's own domain has
// been verified with it. A workspace may save its own address here; it is used only
// once its domain is verified, and the card says which address is used today rather
// than claiming the saved one is.

import { useEffect, useState } from 'react';
import { Mail } from 'lucide-react';
import { toast } from 'sonner';
import { useOrg } from '@/app/contexts/OrgContext';
import {
  getEmailSending, getEmailSuppressions, liftEmailSuppression, orgCan, PERMISSION, updateEmailSending,
  type EmailSending, type EmailSuppression,
} from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';

const field = 'w-full h-11 px-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none disabled:bg-slate-50 disabled:text-slate-600';

/** Why an address is not emailed, in words. */
const WHY: Record<EmailSuppression['reason'], string> = {
  bounced: 'bounced',
  spam_report: 'marked this workspace’s email as spam',
  invalid: 'cannot be delivered to',
  unsubscribed: 'unsubscribed through the mail service',
};

export function EmailSendingCard() {
  const { currentOrg } = useOrg();
  const [sender, setSender] = useState<EmailSending | null>(null);
  const [suppressions, setSuppressions] = useState<EmailSuppression[]>([]);
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
    getEmailSuppressions(orgId)
      .then((found) => { if (!cancelled) setSuppressions(found); })
      .catch(() => { if (!cancelled) setSuppressions([]); });
    return () => { cancelled = true; };
  }, [orgId]);

  const lift = async (suppression: EmailSuppression) => {
    if (!orgId) return;
    try {
      await liftEmailSuppression(orgId, suppression.id);
      setSuppressions((list) => list.filter((s) => s.id !== suppression.id));
      toast.success(`${suppression.email} can be emailed again.`);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The address was not taken off the list.');
    }
  };

  // Demo workspaces send nothing; without an answer from the server there is nothing true to show.
  if (!currentOrg || servesDemoData() || !sender) return null;
  const canEdit = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
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
            Not used yet: {domain} has not been verified with the mail service for this workspace, so email still goes
            out from {sender.sharedAddress ?? 'the shared address'}. Whoever runs Cadence authenticates the domain with
            the mail service (a few DNS records on it) and then switches it on for this workspace, whose id is{' '}
            <span className="font-mono">{currentOrg.id}</span>.
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
        <p className="mt-4 text-xs text-slate-500">Changing this takes managing the workspace&apos;s settings, which the owner and managers have.</p>
      )}
      {suppressions.length > 0 && (
        <div className="mt-6">
          <h4 className="font-medium text-slate-900">Addresses this workspace does not email</h4>
          <p className="text-xs text-slate-500 mt-1 mb-2">
            The mail service reported these. Sequences writing to them stopped, and the composer refuses them.
            {canEdit && ' Taking one off lets this workspace email it again; if the mail service lists it too, remove it there as well, or it drops the email and reports it again.'}
          </p>
          <ul className="max-w-2xl divide-y divide-slate-100 border border-slate-200 rounded-xl text-sm" aria-label="Addresses this workspace does not email">
            {suppressions.map((s) => (
              <li key={s.id} className="px-3 py-2 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1">
                <span>
                  <span className="font-medium text-slate-800">{s.email}</span>{' '}
                  <span className="text-slate-600">{WHY[s.reason] ?? s.reason} on {new Date(s.createdAtUtc).toLocaleDateString()}</span>
                  {s.detail && <span className="block text-xs text-slate-500">{s.detail}</span>}
                </span>
                {canEdit && (
                  <button type="button" onClick={() => void lift(s)} className="text-xs font-medium text-orange-700 hover:text-orange-800">
                    Email it again
                  </button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
