// The club's payment settings: Stripe for paying online, the bank account the Swiss QR-bill
// on statements pays into, and the accounts the journal books to.
//
// Only someone who manages the club's settings reads or changes them. A Stripe secret is
// written here and never shown again: the server keeps it encrypted and says only whether
// one is set and how it ends. Every field but the secrets is saved as shown, so the panel
// always sends the whole form.

import { useState } from 'react';
import { AlertTriangle, BookOpen, CheckCircle2, Copy, CreditCard, Landmark, Loader2, Save, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { LoadFailed } from '@/app/components/LoadFailed';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { CHARGE_KINDS } from '@/app/api/charges';
import {
  DEMO_MESSAGE, savePaymentSettings, type AccountingAccounts, type PaymentSettings, type PaymentSettingsInput,
} from '@/app/api/payments';
import { usePaymentSettings, usePaymentStatus, useWorkspaceScope } from '@/app/hooks/queries';
import { METHOD_LABEL } from './paymentLabels';

interface Draft {
  stripeOfferTwint: boolean;
  stripeOfferSepaDebit: boolean;
  iban: string;
  qrIban: string;
  qrReferencePrefix: string;
  creditorName: string;
  creditorStreet: string;
  creditorBuildingNumber: string;
  creditorPostalCode: string;
  creditorTown: string;
  creditorCountry: string;
  accounts: AccountingAccounts;
}

function draftOf(s: PaymentSettings): Draft {
  return {
    stripeOfferTwint: s.stripeOfferTwint,
    stripeOfferSepaDebit: s.stripeOfferSepaDebit,
    iban: s.iban ?? '',
    qrIban: s.qrIban ?? '',
    qrReferencePrefix: s.qrReferencePrefix ?? '',
    creditorName: s.creditorName ?? '',
    creditorStreet: s.creditorStreet ?? '',
    creditorBuildingNumber: s.creditorBuildingNumber ?? '',
    creditorPostalCode: s.creditorPostalCode ?? '',
    creditorTown: s.creditorTown ?? '',
    creditorCountry: s.creditorCountry ?? 'CH',
    accounts: s.accounts,
  };
}

/** The whole form as the server saves it: an empty field clears it. */
function inputOf(draft: Draft): PaymentSettingsInput {
  return {
    stripeOfferTwint: draft.stripeOfferTwint,
    stripeOfferSepaDebit: draft.stripeOfferSepaDebit,
    iban: draft.iban.trim() || null,
    qrIban: draft.qrIban.trim() || null,
    qrReferencePrefix: draft.qrReferencePrefix.trim() || null,
    creditorName: draft.creditorName.trim() || null,
    creditorStreet: draft.creditorStreet.trim() || null,
    creditorBuildingNumber: draft.creditorBuildingNumber.trim() || null,
    creditorPostalCode: draft.creditorPostalCode.trim() || null,
    creditorTown: draft.creditorTown.trim() || null,
    creditorCountry: draft.creditorCountry.trim().toUpperCase() || null,
    accounts: draft.accounts,
  };
}

const ACCOUNT_FIELDS: { key: Exclude<keyof AccountingAccounts, 'revenueByKind' | 'vatCodes'>; label: string }[] = [
  { key: 'cash', label: 'Cash' },
  { key: 'bank', label: 'Bank (transfers, cheques)' },
  { key: 'cardClearing', label: 'Card and TWINT clearing (Stripe, terminals)' },
  { key: 'receivables', label: "Receivables (members' accounts)" },
  { key: 'customerPrepayments', label: "Members' credit (prepayments)" },
  { key: 'giftCardLiability', label: 'Gift cards not yet spent' },
  { key: 'vatOwed', label: 'VAT owed' },
  { key: 'openingBalances', label: 'Balances brought over' },
  { key: 'corrections', label: 'Corrections, owed more' },
  { key: 'creditCorrections', label: 'Corrections, credited' },
  { key: 'giftCardSales', label: 'Gift card sales paid into' },
  { key: 'giftCardRedemptions', label: 'Gift cards spent' },
  { key: 'giftCardBreakage', label: 'Gift cards expired' },
];

const VAT_RATES = ['8.1', '2.6', '3.8'];

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

function Card({ icon, title, children }: { icon: React.ReactNode; title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">{icon} {title}</h3>
      {children}
    </section>
  );
}

function StateLine({ ok, children }: { ok: boolean; children: React.ReactNode }) {
  return (
    <p className={`mb-4 inline-flex items-start gap-2 rounded-lg px-3 py-2 text-sm ${ok ? 'bg-emerald-50 text-emerald-800' : 'bg-amber-50 text-amber-900'}`}>
      {ok ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />}
      <span>{children}</span>
    </p>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div>
      <Label className="text-xs">{label}</Label>
      {children}
      {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
    </div>
  );
}

export function PaymentSettingsPanel() {
  const org = useOrgOptional()?.currentOrg ?? null;
  const demo = servesDemoData();
  const canManage = !demo && orgCan(org, PERMISSION.ManageSettings);
  const statusQuery = usePaymentStatus();
  const settingsQuery = usePaymentSettings(canManage);

  if (demo) {
    return <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">{DEMO_MESSAGE}</p>;
  }

  if (!canManage) {
    const status = statusQuery.data;
    return (
      <div className="max-w-3xl space-y-3 rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">
        <p>Only someone who manages the club&apos;s settings sees and changes the payment settings.</p>
        {status && (
          <ul className="list-inside list-disc space-y-1">
            <li>{status.onlinePayments ? `Members can pay online (${status.methods.map((m) => METHOD_LABEL[m] ?? m).join(', ')}).` : 'Online payments are not set up.'}</li>
            <li>{status.qrBills ? 'Statements carry a Swiss QR-bill.' : 'Statements carry no QR-bill: the bank details are not set up.'}</li>
          </ul>
        )}
      </div>
    );
  }

  if (settingsQuery.isError && !settingsQuery.data) {
    return (
      <LoadFailed
        what="The payment settings"
        error={settingsQuery.error}
        onRetry={() => void settingsQuery.refetch()}
        retrying={settingsQuery.isFetching}
      />
    );
  }
  if (!settingsQuery.data) {
    return (
      <div className="flex items-center justify-center py-12 text-slate-500">
        <Loader2 className="h-6 w-6 animate-spin" aria-label="Loading the payment settings" />
      </div>
    );
  }
  return <SettingsForm settings={settingsQuery.data} reload={async () => { await settingsQuery.refetch(); }} />;
}

function SettingsForm({ settings, reload }: { settings: PaymentSettings; reload: () => Promise<void> }) {
  // The form being edited, kept with the workspace and the version it started from: a
  // switch of workspace shows that workspace's settings, and a save sends the version, so
  // a change someone else made meanwhile is not overwritten.
  const ws = useWorkspaceScope();
  const scope = `${ws.orgId ?? ''}|${ws.userId ?? ''}`;
  const [edit, setEdit] = useState<{ scope: string; version: string; value: Draft } | null>(null);
  const editing = edit && edit.scope === scope ? edit : null;
  const draft = editing?.value ?? draftOf(settings);
  const set = (patch: Partial<Draft>) => setEdit({ scope, version: editing?.version ?? settings.version, value: { ...draft, ...patch } });
  const setAccounts = (patch: Partial<AccountingAccounts>) => set({ accounts: { ...draft.accounts, ...patch } });

  const [stripeKey, setStripeKey] = useState('');
  const [webhookSecret, setWebhookSecret] = useState('');
  const [saving, setSaving] = useState<null | 'all' | 'key' | 'secret'>(null);

  const save = async (which: 'all' | 'key' | 'secret', secrets: Pick<PaymentSettingsInput, 'stripeSecretKey' | 'stripeWebhookSecret'>) => {
    setSaving(which);
    try {
      await savePaymentSettings({ ...inputOf(draft), ...secrets, version: editing?.version ?? settings.version });
      setStripeKey('');
      setWebhookSecret('');
      toast.success(which === 'key' && secrets.stripeSecretKey === '' ? 'Stripe key removed' : 'Payment settings saved');
      // The form shows what was typed until the saved settings are read back, then those.
      await reload();
      setEdit(null);
    } catch (err) {
      // A value refused (an IBAN's digits, a key Stripe did not accept) says why. Changed by
      // someone else meanwhile: their version is read, and the change is to be made again.
      toast.error(message(err, 'Could not save the payment settings'));
      if (err instanceof ApiError && err.status === 409) {
        await reload();
        setEdit(null);
      }
    } finally {
      setSaving(null);
    }
  };

  const typedSecrets = {
    ...(stripeKey.trim() ? { stripeSecretKey: stripeKey.trim() } : {}),
    ...(webhookSecret.trim() ? { stripeWebhookSecret: webhookSecret.trim() } : {}),
  };
  const dirty = editing !== null || Object.keys(typedSecrets).length > 0;

  const copyWebhookUrl = async () => {
    try {
      await navigator.clipboard.writeText(settings.webhookUrl);
      toast.success('Webhook address copied');
    } catch {
      toast.error('Could not copy: select the address and copy it.');
    }
  };

  const chf = settings.currency === 'CHF';
  const eur = settings.currency === 'EUR';

  return (
    <div className="max-w-4xl space-y-6">
      <Card icon={<CreditCard className="h-4 w-4 text-indigo-600" />} title="Online payments (Stripe)">
        <p className="mb-3 text-sm text-slate-500">
          Members pay a statement or charges on Stripe&apos;s own page, from a link the club sends, and can save a card
          for their dues. Card details stay with Stripe: the club keeps only Stripe&apos;s ids and the card&apos;s last
          four digits.
        </p>
        {settings.stripeConfigured ? (
          <StateLine ok={settings.stripeWebhookSecretSet}>
            Connected{settings.stripeMode ? ` in ${settings.stripeMode} mode` : ''} with the key ending {settings.stripeKeyHint}
            {settings.stripeCheckedAtUtc ? `, checked ${formatDateTime(settings.stripeCheckedAtUtc)}` : ''}.
            {!settings.stripeWebhookSecretSet && ' Stripe\'s word that a payment went through is not heard until the webhook signing secret below is set: payments would stay "processing" and never reach the ledger.'}
          </StateLine>
        ) : (
          <StateLine ok={false}>Not set up: members cannot pay online, and no card can be saved.</StateLine>
        )}

        <div className="grid gap-4 md:grid-cols-2">
          <Field
            label="Secret key"
            hint={settings.stripeConfigured
              ? 'Leave empty to keep the key in use. A new key is checked with Stripe before it is kept.'
              : 'From Stripe, Developers, API keys: the secret key (sk_…) or a restricted key (rk_…) that may write Checkout Sessions, Customers, PaymentIntents, SetupIntents and Refunds.'}
          >
            <Input
              aria-label="Stripe secret key"
              type="password"
              autoComplete="off"
              placeholder={settings.stripeConfigured ? `Key ending ${settings.stripeKeyHint ?? ''}` : 'sk_live_… or rk_live_…'}
              value={stripeKey}
              onChange={(e) => setStripeKey(e.target.value)}
            />
          </Field>
          <Field
            label="Webhook signing secret"
            hint={settings.stripeWebhookSecretSet ? 'Set. Leave empty to keep it.' : 'From the webhook endpoint you add in Stripe (whsec_…).'}
          >
            <Input
              aria-label="Stripe webhook signing secret"
              type="password"
              autoComplete="off"
              placeholder={settings.stripeWebhookSecretSet ? 'Set' : 'whsec_…'}
              value={webhookSecret}
              onChange={(e) => setWebhookSecret(e.target.value)}
            />
          </Field>
        </div>

        <div className="mt-4 rounded-xl bg-slate-50 p-4 text-sm">
          <p className="mb-1 font-medium text-slate-800">Webhook endpoint to add in Stripe (Developers, Webhooks)</p>
          <div className="flex flex-wrap items-center gap-2">
            <code className="min-w-0 flex-1 break-all rounded bg-white px-2 py-1 text-xs ring-1 ring-slate-200" data-testid="webhook-url">
              {settings.webhookUrl}
            </code>
            <Button type="button" size="sm" variant="outline" onClick={copyWebhookUrl}>
              <Copy className="mr-1 h-3.5 w-3.5" /> Copy
            </Button>
          </div>
          <p className="mt-2 text-xs text-slate-500">Send these events:</p>
          <p className="mt-1 flex flex-wrap gap-1">
            {settings.webhookEvents.map((e) => (
              <code key={e} className="rounded bg-white px-1.5 py-0.5 text-[11px] ring-1 ring-slate-200">{e}</code>
            ))}
          </p>
        </div>

        <div className="mt-4 space-y-3">
          <label className="flex items-start gap-3 text-sm">
            <Switch
              aria-label="Offer TWINT"
              checked={draft.stripeOfferTwint}
              onCheckedChange={(v) => set({ stripeOfferTwint: v })}
              disabled={!chf}
            />
            <span>
              <span className="font-medium text-slate-800">Offer TWINT</span>
              <span className="block text-xs text-slate-500">
                {chf ? 'For payments in CHF, once TWINT is turned on in your Stripe account (Settings, Payment methods). Not for saving a card.' : 'Only for a club whose currency is CHF.'}
              </span>
            </span>
          </label>
          <label className="flex items-start gap-3 text-sm">
            <Switch
              aria-label="Offer SEPA direct debit"
              checked={draft.stripeOfferSepaDebit}
              onCheckedChange={(v) => set({ stripeOfferSepaDebit: v })}
              disabled={!eur}
            />
            <span>
              <span className="font-medium text-slate-800">Offer SEPA direct debit</span>
              <span className="block text-xs text-slate-500">
                {eur ? 'For payments in EUR, once SEPA Direct Debit is turned on in your Stripe account. A debit takes days to clear: it shows as processing until then.' : 'Only for a club whose currency is EUR.'}
              </span>
            </span>
          </label>
        </div>

        {(settings.stripeConfigured || settings.stripeWebhookSecretSet) && (
          <div className="mt-4 flex flex-wrap gap-2">
            {settings.stripeConfigured && (
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={saving !== null}
                onClick={() => {
                  if (!window.confirm('Remove the Stripe key? Members can no longer pay online, and saved cards are not charged, until a key is entered again.')) return;
                  void save('key', { stripeSecretKey: '' });
                }}
              >
                {saving === 'key' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Trash2 className="mr-1 h-3.5 w-3.5" />} Remove key
              </Button>
            )}
            {settings.stripeWebhookSecretSet && (
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={saving !== null}
                onClick={() => {
                  if (!window.confirm("Remove the webhook signing secret? Stripe's events are refused until one is entered again.")) return;
                  void save('secret', { stripeWebhookSecret: '' });
                }}
              >
                {saving === 'secret' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Trash2 className="mr-1 h-3.5 w-3.5" />} Remove signing secret
              </Button>
            )}
          </div>
        )}
      </Card>

      <Card icon={<Landmark className="h-4 w-4 text-emerald-600" />} title="Swiss QR-bill">
        <p className="mb-3 text-sm text-slate-500">
          With these set, every statement carries a QR-bill to pay at the bank, with a reference that says whose and which
          statement it is. A payment that comes back in the bank&apos;s camt file is matched by that reference.
        </p>
        <StateLine ok={settings.qrBillReady}>
          {settings.qrBillReady
            ? `Statements carry a QR-bill, with a ${settings.qrIban ? 'QR reference (QR-IBAN)' : 'creditor reference (RF…)'}.`
            : settings.qrBillProblem ?? 'Statements carry no QR-bill yet.'}
        </StateLine>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="IBAN" hint="The club's Swiss or Liechtenstein account. Statements quote a creditor reference (RF…) with it.">
            <Input aria-label="IBAN" placeholder="CH58 0079 1123 0008 8901 2" value={draft.iban} onChange={(e) => set({ iban: e.target.value })} />
          </Field>
          <Field label="QR-IBAN (optional)" hint="From your bank, for QR references. When set, statements use it with a QR reference.">
            <Input aria-label="QR-IBAN" placeholder="CH44 3199 9123 0008 8901 2" value={draft.qrIban} onChange={(e) => set({ qrIban: e.target.value })} />
          </Field>
          <Field label="QR reference prefix (optional)" hint="Up to 12 digits, only if your bank gave you a customer number to start QR references with.">
            <Input aria-label="QR reference prefix" inputMode="numeric" value={draft.qrReferencePrefix} onChange={(e) => set({ qrReferencePrefix: e.target.value })} />
          </Field>
        </div>
        <p className="mb-2 mt-5 text-sm font-medium text-slate-800">Creditor: the club, as the bank knows it</p>
        <div className="grid gap-4 md:grid-cols-6">
          <div className="md:col-span-6">
            <Field label="Name">
              <Input aria-label="Creditor name" value={draft.creditorName} onChange={(e) => set({ creditorName: e.target.value })} />
            </Field>
          </div>
          <div className="md:col-span-4">
            <Field label="Street">
              <Input aria-label="Creditor street" value={draft.creditorStreet} onChange={(e) => set({ creditorStreet: e.target.value })} />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field label="Building number">
              <Input aria-label="Creditor building number" value={draft.creditorBuildingNumber} onChange={(e) => set({ creditorBuildingNumber: e.target.value })} />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field label="Postal code">
              <Input aria-label="Creditor postal code" value={draft.creditorPostalCode} onChange={(e) => set({ creditorPostalCode: e.target.value })} />
            </Field>
          </div>
          <div className="md:col-span-3">
            <Field label="Town">
              <Input aria-label="Creditor town" value={draft.creditorTown} onChange={(e) => set({ creditorTown: e.target.value })} />
            </Field>
          </div>
          <div className="md:col-span-1">
            <Field label="Country">
              <Input aria-label="Creditor country" maxLength={2} value={draft.creditorCountry} onChange={(e) => set({ creditorCountry: e.target.value.toUpperCase() })} />
            </Field>
          </div>
        </div>
      </Card>

      <Card icon={<BookOpen className="h-4 w-4 text-amber-600" />} title="Accounts for the journal">
        <p className="mb-4 text-sm text-slate-500">
          The accounting export books every entry to these accounts. They start as the Swiss SME chart (KMU-Kontenrahmen);
          change any to your own chart&apos;s numbers. With your accounting program&apos;s VAT codes set, the export carries
          them for Bexio or Abacus to book the VAT.
        </p>
        <div className="grid gap-4 md:grid-cols-3">
          {ACCOUNT_FIELDS.map((f) => (
            <Field key={f.key} label={f.label}>
              <Input aria-label={`Account: ${f.label}`} value={draft.accounts[f.key]} onChange={(e) => setAccounts({ [f.key]: e.target.value })} />
            </Field>
          ))}
        </div>
        <p className="mb-2 mt-5 text-sm font-medium text-slate-800">Revenue, by kind of charge</p>
        <div className="grid gap-4 md:grid-cols-4">
          {CHARGE_KINDS.map((kind) => (
            <Field key={kind} label={kind}>
              <Input
                aria-label={`Revenue account: ${kind}`}
                value={draft.accounts.revenueByKind[kind] ?? ''}
                onChange={(e) => setAccounts({ revenueByKind: { ...draft.accounts.revenueByKind, [kind]: e.target.value } })}
              />
            </Field>
          ))}
        </div>
        <p className="mb-2 mt-5 text-sm font-medium text-slate-800">VAT codes in your accounting program</p>
        <div className="grid gap-4 md:grid-cols-3">
          {VAT_RATES.map((rate) => (
            <Field key={rate} label={`${rate} %`}>
              <Input
                aria-label={`VAT code for ${rate} %`}
                value={draft.accounts.vatCodes[rate] ?? ''}
                onChange={(e) => setAccounts({ vatCodes: { ...draft.accounts.vatCodes, [rate]: e.target.value } })}
              />
            </Field>
          ))}
        </div>
      </Card>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="button" onClick={() => void save('all', typedSecrets)} disabled={saving !== null || !dirty}>
          {saving === 'all' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Save className="mr-1 h-4 w-4" />} Save payment settings
        </Button>
        {editing && (
          <Button type="button" variant="outline" onClick={() => setEdit(null)} disabled={saving !== null}>
            Discard changes
          </Button>
        )}
        {settings.updatedAtUtc && (
          <span className="text-xs text-slate-500">
            Last saved {formatDateTime(settings.updatedAtUtc)}{settings.updatedByName ? ` by ${settings.updatedByName}` : ''}.
          </span>
        )}
      </div>
    </div>
  );
}
