// The workspace's currency, locale and time zone, and formatters that use them.
//
// Every page that shows money or groups dates by day or month goes through here, so
// the board, a deal, the company totals and the reports cannot disagree about which
// currency a deal without one is in, or how an amount is written. The settings come
// with the workspace list the app already loads (OrgContext); until it answers, or
// outside a workspace, they are the defaults: CHF, de-CH, Europe/Zurich.

import { useMemo } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import {
  formatDealAmount, formatDealSum, formatMoney, formatTotals, regionalOf, sumByCurrency,
  type Cents, type CurrencySum, type Regional,
} from '@/app/lib/money';
import type { Deal } from '@/app/api/types';

/** What the workspace in use works in. */
export function useRegional(): Regional {
  const org = useOrgOptional()?.currentOrg ?? null;
  const currency = org?.currency;
  const locale = org?.locale;
  const timeZone = org?.timeZone;
  return useMemo(() => regionalOf({ currency, locale, timeZone }), [currency, locale, timeZone]);
}

export interface Money extends Regional {
  /** An amount, in `currency` or else the workspace's. */
  format: (amount: number, currency?: string, cents?: Cents) => string;
  /** A deal's amount in its own currency. */
  formatDeal: (deal: Pick<Deal, 'amount' | 'currency'>) => string;
  /** Deals totalled per currency, largest first. */
  totals: (deals: Pick<Deal, 'amount' | 'currency'>[]) => CurrencySum[];
  /** Totals per currency as one line, each in its own currency. */
  formatTotals: (totals: Pick<CurrencySum, 'currency' | 'value'>[], cents?: Cents) => string;
  /** Deals totalled per currency, as one line. */
  formatDeals: (deals: Pick<Deal, 'amount' | 'currency'>[]) => string;
}

export function useMoney(): Money {
  const regional = useRegional();
  return useMemo<Money>(() => ({
    ...regional,
    format: (amount, currency = regional.currency, cents = 'auto') => formatMoney(amount, currency, { locale: regional.locale, cents }),
    formatDeal: (deal) => formatDealAmount(deal, regional),
    totals: (deals) => sumByCurrency(deals, regional.currency),
    formatTotals: (totals, cents = 'auto') => formatTotals(totals, regional, cents),
    formatDeals: (deals) => formatDealSum(deals, regional),
  }), [regional]);
}
