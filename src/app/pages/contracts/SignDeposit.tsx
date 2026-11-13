// The deposit on the signing page: what the signer is asked to pay, and once they have signed,
// the ways to pay it — Stripe's page, or a bank transfer with the club's QR-bill — and where
// the payment is.
//
// The signer is not a user of the CRM, so the words are theirs (in the page's language), the
// amounts are written as the club writes them, and nothing says anything of anyone's account.

import { useState } from 'react';
import { CreditCard, FileDown, Loader2, Wallet, CheckCircle2, TriangleAlert } from 'lucide-react';
import {
  publicDepositQrBillUrl, startDepositCheckout, ContractError, type PublicDeposit,
} from '@/app/api/contracts';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { useFormat, useT } from '@/app/i18n';

/** Where the browser is sent for Stripe's page: one place, so a test can stand in for it. */
export const browser = { go: (url: string) => { window.location.assign(url); } };

/** "CHF 500.00", in the club's number format. */
export function depositMoney(deposit: Pick<PublicDeposit, 'amount' | 'currency' | 'locale'>, fallbackLocale: string): string {
  const options = { minimumFractionDigits: 2, maximumFractionDigits: 2 };
  let number: string;
  try {
    number = new Intl.NumberFormat(deposit.locale || fallbackLocale, options).format(deposit.amount);
  } catch {
    number = deposit.amount.toFixed(2);
  }
  return `${deposit.currency} ${number}`;
}

/** Said before signing: a deposit is asked, and paid once signed. */
export function DepositBeforeSigning({ deposit }: { deposit: PublicDeposit }) {
  const { t, numberLocale } = useT();
  return (
    <p className="mt-4 flex items-start gap-2 rounded-xl border border-indigo-200 bg-indigo-50/70 p-3 text-[13px] leading-relaxed text-indigo-900">
      <Wallet className="mt-0.5 h-4 w-4 shrink-0 text-indigo-600" aria-hidden />
      {t('sign.depositBefore', { amount: depositMoney(deposit, numberLocale) })}
    </p>
  );
}

/**
 * Once signed: the deposit and how to pay it, or that it is paid. `returned` is what Stripe
 * sent the signer back saying (?deposit=done or cancelled).
 */
export function DepositStep({ token, deposit, organizationName, returned }: {
  token: string;
  deposit: PublicDeposit;
  organizationName: string;
  returned?: 'done' | 'cancelled' | null;
}) {
  const { t, language, numberLocale } = useT();
  const format = useFormat();
  const [opening, setOpening] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const amount = depositMoney(deposit, numberLocale);
  const online = deposit.methods.length > 0;

  const pay = async () => {
    setOpening(true);
    setProblem(null);
    try {
      const { url } = await startDepositCheckout(token);
      browser.go(url);
    } catch (e) {
      setProblem(e instanceof ContractError && e.message ? e.message : t('sign.depositStartFailed'));
      setOpening(false);
    }
  };

  if (deposit.status === 'paid') {
    return (
      <section className="mt-4 rounded-2xl border border-emerald-200 bg-emerald-50 p-5" aria-label={t('sign.depositTitle')}>
        <p className="flex items-center gap-2 text-sm font-bold text-emerald-900">
          <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-hidden />
          {t('sign.depositPaid', { amount })}
        </p>
        {deposit.paidAtUtc && (
          <p className="mt-1 text-xs text-emerald-800/80">{t('sign.depositPaidOn', { date: format.date(deposit.paidAtUtc, 'long') })}</p>
        )}
      </section>
    );
  }

  if (deposit.status === 'refunded' || deposit.status === 'cancelled') {
    return (
      <section className="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-5 text-sm text-slate-700" aria-label={t('sign.depositTitle')}>
        {deposit.status === 'refunded' ? t('sign.depositRefunded') : t('sign.depositCancelled')}
      </section>
    );
  }

  return (
    <section className="mt-4 rounded-2xl border border-indigo-200 bg-white p-5 sm:p-6" aria-label={t('sign.depositTitle')}>
      <h2 className="flex items-center gap-2 text-sm font-bold text-slate-900">
        <Wallet className="h-4 w-4 text-indigo-600" aria-hidden />
        {t('sign.depositTitle')}
      </h2>
      <p className="mt-2 text-sm leading-relaxed text-slate-700">
        {t('sign.depositAsk', { organization: organizationName, amount })}
      </p>

      {returned === 'done' && deposit.status === 'awaiting' && (
        <p role="status" className="mt-3 rounded-lg bg-sky-50 p-2.5 text-[13px] text-sky-900">{t('sign.depositReturned')}</p>
      )}
      {returned === 'cancelled' && deposit.status === 'awaiting' && (
        <p className="mt-3 rounded-lg bg-amber-50 p-2.5 text-[13px] text-amber-900">{t('sign.depositReturnedCancelled')}</p>
      )}
      {deposit.status === 'processing' && (
        <p role="status" className="mt-3 rounded-lg bg-sky-50 p-2.5 text-[13px] text-sky-900">
          {t('sign.depositProcessing', { organization: organizationName })}
        </p>
      )}
      {deposit.failureMessage && deposit.status === 'awaiting' && (
        <p role="alert" className="mt-3 flex items-start gap-1.5 text-[13px] font-medium text-rose-700">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          {t('sign.depositFailed', { reason: deposit.failureMessage })}
        </p>
      )}

      {deposit.payable && (
        <>
          {online && (
            <Button
              type="button"
              onClick={() => void pay()}
              disabled={opening}
              className="mt-4 h-11 bg-gradient-to-r from-indigo-600 to-violet-600 px-6 font-semibold hover:from-indigo-500 hover:to-violet-500"
            >
              {opening
                ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> {t('sign.depositOpening')}</>
                : <><CreditCard className="mr-2 h-4 w-4" aria-hidden /> {t('sign.depositPayOnline', { amount })}</>}
            </Button>
          )}
          {problem && <p role="alert" className="mt-2 text-sm font-medium text-rose-700">{problem}</p>}

          {deposit.qrBill && (
            <div className={cn('text-[13px] text-slate-700', online ? 'mt-5 border-t border-slate-100 pt-4' : 'mt-4')}>
              <p>{online ? t('sign.depositByBank') : t('sign.depositByBankOnly', { organization: organizationName })}</p>
              <a
                href={publicDepositQrBillUrl(token, language)}
                target="_blank"
                rel="noopener noreferrer"
                className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300 hover:text-slate-900"
              >
                <FileDown className="h-3.5 w-3.5" aria-hidden /> {t('sign.depositQrBill')}
              </a>
              {(deposit.iban || deposit.reference) && (
                <dl className="mt-3 grid gap-x-6 gap-y-1 sm:grid-cols-2">
                  {deposit.iban && (<div><dt className="text-xs text-slate-500">{t('sign.depositAccount')}</dt><dd className="font-mono">{deposit.iban}</dd></div>)}
                  {deposit.creditor && (<div><dt className="text-xs text-slate-500">{t('sign.depositCreditor')}</dt><dd>{deposit.creditor}</dd></div>)}
                  {deposit.reference && (<div className="sm:col-span-2"><dt className="text-xs text-slate-500">{t('sign.depositReference')}</dt><dd className="font-mono">{deposit.reference}</dd></div>)}
                </dl>
              )}
            </div>
          )}

          {!online && !deposit.qrBill && (
            <p className="mt-3 text-[13px] text-slate-600">{t('sign.depositNoWay', { organization: organizationName })}</p>
          )}
        </>
      )}
    </section>
  );
}
