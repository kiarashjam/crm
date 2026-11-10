// POS tickets: the closed tickets the connected POS (Lightspeed Restaurant K-Series) reported,
// and what came of each. Most need nobody: a ticket with a member's code on it is posted to
// their house account (owing, or paid at the POS) with its points, and one paid with nobody on
// it is not a member's. What waits here is what needs staff: a ticket put on the house account
// with no member's code on it, one naming a customer no code matches, and one that could not be
// posted, with why.
//
// Matching, retrying, setting aside and the connection take money.manage; the server refuses
// the rest, and the page says so instead of offering it.

import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PlugZap } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { POS_DEMO_MESSAGE } from '@/app/api/pos';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { usePosStatus } from '@/app/hooks/queries';
import { PosTicketsPanel } from '@/app/components/pos/PosTicketsPanel';
import { PosConnectionPanel } from '@/app/components/pos/PosConnectionPanel';

const TABS = ['attention', 'all', 'connection'] as const;
type Tab = (typeof TABS)[number];

export default function PosTickets() {
  const orgContext = useOrgOptional();
  const org = orgContext?.currentOrg ?? null;
  const demo = servesDemoData();
  const canAct = !demo && !orgContext?.isReadOnly && orgCan(org, PERMISSION.ManageMoney);
  const status = usePosStatus().data;
  const [search, setSearch] = useSearchParams();
  const asked = search.get('tab');
  const tab: Tab = TABS.includes(asked as Tab) ? (asked as Tab) : 'attention';
  const open = (value: string) => {
    const next = new URLSearchParams(search);
    if (value === 'attention') next.delete('tab');
    else next.set('tab', value);
    setSearch(next, { replace: true });
  };

  // Lightspeed sends the browser back here after connecting, saying how it went.
  const connected = search.get('connected');
  const failed = search.get('error');
  useEffect(() => {
    if (!connected && !failed) return;
    if (connected) toast.success('Lightspeed connected. Choose the location whose tickets come in, then turn the import on.');
    if (failed) toast.error(failed);
    const next = new URLSearchParams(search);
    next.delete('connected');
    next.delete('error');
    setSearch(next, { replace: true });
  }, [connected, failed, search, setSearch]);

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
            icon={PlugZap}
            tone="cyan"
            title="POS tickets"
            subtitle="Tickets the POS closed, posted to members' accounts by the codes on them; the ones that need a member wait here."
          />

          {demo ? (
            <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">{POS_DEMO_MESSAGE}</p>
          ) : (
            <>
              {status && !status.connected && tab !== 'connection' && (
                <p className="mb-4 max-w-3xl rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
                  No POS is connected, so no tickets come in. The house-charge screen rings up sales by hand
                  {canAct ? '; the Connection tab connects Lightspeed.' : '.'}
                </p>
              )}
              <Tabs value={tab} onValueChange={open}>
                <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
                  <TabsTrigger value="attention">
                    Needs attention{status && status.needsAttention > 0 ? ` (${status.needsAttention})` : ''}
                  </TabsTrigger>
                  <TabsTrigger value="all">All tickets</TabsTrigger>
                  <TabsTrigger value="connection">Connection</TabsTrigger>
                </TabsList>
                <TabsContent value="attention">
                  <PosTicketsPanel filter="attention" canAct={canAct} />
                </TabsContent>
                <TabsContent value="all">
                  <PosTicketsPanel filter="all" canAct={canAct} />
                </TabsContent>
                <TabsContent value="connection">
                  <PosConnectionPanel canManage={canAct} />
                </TabsContent>
              </Tabs>
            </>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
