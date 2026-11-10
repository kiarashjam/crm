// Payments & bank: money that reaches the club from outside, and the books it goes to.
//
// - Online payments: what members paid through Stripe (pay links, saved cards, dues on
//   file), failures included with Stripe's reason, so staff see a declined card.
// - Bank files: the bank's camt files imported, QR-bill payments matched to members once,
//   and the transfers nobody's reference named, for staff to allocate or set aside.
// - Accounting export: the journal for a period, in the club's accounts, for Bexio or Abacus.
//
// Taking payments, importing files and exporting take money.manage; the server refuses the
// rest, and the page says so instead of offering it.

import { useSearchParams } from 'react-router-dom';
import { Landmark } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { DEMO_MESSAGE } from '@/app/api/payments';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { usePaymentStatus } from '@/app/hooks/queries';
import { OnlinePaymentsPanel } from '@/app/components/payments/OnlinePaymentsPanel';
import { BankFilesPanel } from '@/app/components/payments/BankFilesPanel';
import { JournalExportPanel } from '@/app/components/payments/JournalExportPanel';

const TABS = ['online', 'bank', 'export'] as const;
type Tab = (typeof TABS)[number];

export default function ClubPayments() {
  const org = useOrgOptional()?.currentOrg ?? null;
  const demo = servesDemoData();
  const canTake = !demo && orgCan(org, PERMISSION.ManageMoney);
  const status = usePaymentStatus().data;
  const [search, setSearch] = useSearchParams();
  const asked = search.get('tab');
  const tab: Tab = TABS.includes(asked as Tab) ? (asked as Tab) : 'online';
  const open = (value: string) => {
    const next = new URLSearchParams(search);
    if (value === 'online') next.delete('tab');
    else next.set('tab', value);
    setSearch(next, { replace: true });
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={Landmark}
            tone="emerald"
            title="Payments & bank"
            subtitle="Payments members made online, the bank's files with their transfers, and the accounting journal."
          />

          {demo ? (
            <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">{DEMO_MESSAGE}</p>
          ) : (
            <Tabs value={tab} onValueChange={open}>
              <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
                <TabsTrigger value="online">Online payments</TabsTrigger>
                <TabsTrigger value="bank">Bank files</TabsTrigger>
                <TabsTrigger value="export">Accounting export</TabsTrigger>
              </TabsList>
              <TabsContent value="online">
                <OnlinePaymentsPanel status={status} />
              </TabsContent>
              <TabsContent value="bank">
                <BankFilesPanel canTake={canTake} status={status} />
              </TabsContent>
              <TabsContent value="export">
                <JournalExportPanel canTake={canTake} />
              </TabsContent>
            </Tabs>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
