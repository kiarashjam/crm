// A membership contract's terms on the CRM side: the tier it admits its counterparty at and
// the deposit they pay on signing, chosen on the draft, and where that deposit went once
// sent — paid or not, and whose account it is on.
//
// The deposit field is offered only to someone who may take payments (money.manage), as the
// server asks; and only with a tier, since the deposit goes on the account of the member the
// contract admits.

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, Crown, Wallet } from 'lucide-react';
import { getCatalog, tiersOnSale, type CatalogItem } from '@/app/api/catalog';
import type { ContractDeposit } from '@/app/api/contracts';
import { PERMISSION } from '@/app/api/organizations';
import { Input } from '@/app/components/ui/input';
import { useMay } from '@/app/hooks/useMay';
import { useModules } from '@/app/hooks/useModules';
import { useMoney } from '@/app/hooks/useMoney';
import { currentDateLocale } from '@/app/i18n/current';
import { cn } from '@/app/components/ui/utils';

/** The tier and deposit fields of a draft, for a workspace whose club is on. */
export function MembershipTermsFields({ tier, onTier, deposit, onDeposit, disabled }: {
  tier: string;
  onTier: (tier: string) => void;
  deposit: string;
  onDeposit: (amount: string) => void;
  disabled?: boolean;
}) {
  const clubOn = useModules().club;
  const may = useMay();
  const [tiers, setTiers] = useState<CatalogItem[]>([]);

  useEffect(() => {
    if (!clubOn) return;
    let live = true;
    getCatalog('Membership')
      .then((items) => { if (live) setTiers(tiersOnSale(items)); })
      .catch(() => { if (live) setTiers([]); });
    return () => { live = false; };
  }, [clubOn]);

  if (!clubOn) return null;
  const mayAskDeposit = may(PERMISSION.ManageMoney);
  return (
    <div className="mt-4 grid gap-3 rounded-xl border border-indigo-100 bg-indigo-50/40 p-3 sm:grid-cols-2">
      <label className="block">
        <span className="text-xs font-semibold text-slate-600">Membership tier</span>
        <select
          value={tier}
          onChange={(e) => onTier(e.target.value)}
          disabled={disabled}
          className="mt-1 h-9 w-full rounded-md border border-slate-200 bg-white px-2 text-sm"
        >
          <option value="">Not a membership contract</option>
          {tiers.map((t) => <option key={t.id} value={t.name}>{t.name}</option>)}
        </select>
        <span className="mt-1 block text-[11px] text-slate-500">
          Countersigning admits them at this tier, once.
        </span>
      </label>
      {mayAskDeposit && (
        <label className="block">
          <span className="text-xs font-semibold text-slate-600">Deposit on signing</span>
          <Input
            inputMode="decimal"
            value={deposit}
            onChange={(e) => onDeposit(e.target.value)}
            placeholder={tier ? 'e.g. 500' : 'Choose a tier first'}
            disabled={disabled || !tier}
            className="mt-1 h-9"
            aria-describedby="deposit-help"
          />
          <span id="deposit-help" className="mt-1 block text-[11px] text-slate-500">
            Paid on the signing page, by card or QR-bill, and put on their account once admitted.
          </span>
        </label>
      )}
    </div>
  );
}

/** The amount typed in the deposit field, or undefined for none; NaN for one that is not a number. */
export function depositAmountOf(text: string): number | undefined {
  const cleaned = text.replace(/[\s'’]/g, '').replace(',', '.');
  if (cleaned.length === 0) return undefined;
  const value = Number(cleaned);
  return Number.isFinite(value) ? value : Number.NaN;
}

const STATUS_TEXT: Record<ContractDeposit['status'], string> = {
  awaiting: 'Not paid yet',
  processing: 'Payment on its way',
  paid: 'Paid',
  refunded: 'Paid back',
  cancelled: 'No longer asked for',
};

const STATUS_TONE: Record<ContractDeposit['status'], string> = {
  awaiting: 'border-amber-200 bg-amber-50 text-amber-900',
  processing: 'border-sky-200 bg-sky-50 text-sky-900',
  paid: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  refunded: 'border-slate-200 bg-slate-50 text-slate-700',
  cancelled: 'border-slate-200 bg-slate-50 text-slate-600',
};

function methodOf(deposit: ContractDeposit): string | null {
  if (deposit.provider === 'bank') return 'by bank transfer';
  switch (deposit.methodType) {
    case 'twint': return 'with TWINT';
    case 'sepa_debit': return 'by SEPA direct debit';
    case 'card': return 'by card';
    default: return deposit.provider === 'stripe' ? 'online' : null;
  }
}

/**
 * Where a contract's deposit is: asked for, paid, on whose account. `withdrawn` is a contract
 * that was voided or declined: what was paid on it is the signer's to get back, and the server
 * never puts it on an account, so the line must not say it will.
 */
export function DepositLine({ deposit, withdrawn = false, className }: { deposit: ContractDeposit; withdrawn?: boolean; className?: string }) {
  const money = useMoney();
  const amount = money.format(deposit.amountPaid ?? deposit.amount, deposit.currency, 'always');
  const method = methodOf(deposit);
  const paidOn = deposit.paidAtUtc
    ? new Date(deposit.paidAtUtc).toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric' })
    : null;
  return (
    <div className={cn('rounded-xl border p-3 text-xs', STATUS_TONE[deposit.status], className)} data-testid="deposit-line">
      <p className="flex flex-wrap items-center gap-1.5 font-semibold">
        <Wallet className="h-3.5 w-3.5" aria-hidden />
        Deposit {amount} · {STATUS_TEXT[deposit.status]}
        {deposit.status === 'paid' && method && <span className="font-normal">{method}</span>}
        {deposit.status === 'paid' && paidOn && <span className="font-normal">on {paidOn}</span>}
      </p>
      {deposit.status === 'awaiting' && (
        <p className="mt-1">
          {deposit.failureMessage
            ? `The last payment did not go through: ${deposit.failureMessage}`
            : 'Paid on the signing page once they have signed.'}
          {deposit.reference ? ` QR reference ${deposit.reference}.` : ''}
        </p>
      )}
      {deposit.status === 'processing' && withdrawn && (
        <p className="mt-1">The contract was withdrawn: once the payment arrives, pay it back.</p>
      )}
      {deposit.status === 'paid' && (
        deposit.memberId && deposit.ledgerPaymentId ? (
          <p className="mt-1">
            On the account of{' '}
            <Link to={`/members/${deposit.memberId}`} className="inline-flex items-center gap-0.5 font-semibold underline-offset-2 hover:underline">
              {deposit.memberName ?? 'the member'} <ArrowUpRight className="h-3 w-3" aria-hidden />
            </Link>
            , paying their first charges.
          </p>
        ) : withdrawn ? (
          <p className="mt-1">
            The contract was withdrawn, so this goes on nobody&apos;s account: pay it back
            {deposit.provider === 'stripe' ? ' in Stripe' : ' by bank transfer'}.
          </p>
        ) : (
          <p className="mt-1">Kept until the club admits them, then put on their account.</p>
        )
      )}
      {deposit.status === 'refunded' && deposit.amountRefunded > 0 && (
        <p className="mt-1">{money.format(deposit.amountRefunded, deposit.currency, 'always')} paid back in Stripe.</p>
      )}
    </div>
  );
}

/** The tier a contract admits its counterparty at. */
export function TierPill({ tier }: { tier: string }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full border border-indigo-200 bg-indigo-50 px-2 py-0.5 text-[11px] font-semibold text-indigo-800">
      <Crown className="h-3 w-3" aria-hidden /> {tier} membership
    </span>
  );
}
