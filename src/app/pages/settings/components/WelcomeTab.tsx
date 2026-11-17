// Settings > Welcome & portal. Two things live here: the welcome handoff (Sales: a converted
// lead handed to your own portal with a welcome email) and the club's own member portal.
// With both modules on, each has its own view, chosen at the top and kept in the address
// (?part=portal), rather than one long page with two save models in it.

import type { ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import { DoorOpen, MessageSquareHeart, Send } from 'lucide-react';
import type { Organization } from '@/app/api/organizations';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { MemberPortalSettingsCard } from './MemberPortalSettingsCard';
import { WelcomePortalSection } from './WelcomePortalSection';

type Part = 'welcome' | 'portal';

export function WelcomeTab({ currentOrg, salesOn, clubOn }: { currentOrg: Organization | null; salesOn: boolean; clubOn: boolean }) {
  const [params, setParams] = useSearchParams();
  const both = salesOn && clubOn;
  const part: Part = both ? (params.get('part') === 'portal' ? 'portal' : 'welcome') : salesOn ? 'welcome' : 'portal';

  const choose = (next: string) =>
    setParams((current) => {
      const updated = new URLSearchParams(current);
      if (next === 'portal') updated.set('part', 'portal');
      else updated.delete('part');
      return updated;
    }, { replace: true });

  const header = (switcher?: ReactNode) => (
    <header className="flex flex-wrap items-center gap-4">
      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25">
        <MessageSquareHeart className="h-5 w-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <h2 className="text-xl font-semibold text-slate-900">Welcome &amp; portal</h2>
        <p className="text-sm text-slate-500">Welcome new members, and give them a place to sign in.</p>
      </div>
      {switcher}
    </header>
  );

  const portal = (
    <>
      <MemberPortalSettingsCard />
      {!salesOn && (
        <p className="rounded-xl bg-slate-50 p-3 text-sm text-slate-500">
          Handing a deal&apos;s contact to your own portal belongs to the Sales module, which this workspace has turned off. Turning it on again (Team) brings those settings back as they were.
        </p>
      )}
    </>
  );

  if (!both) {
    return (
      <div className="space-y-6 p-4 sm:p-6">
        {header()}
        {part === 'portal' ? <div className="space-y-5">{portal}</div> : <WelcomePortalSection currentOrg={currentOrg} />}
      </div>
    );
  }

  return (
    <Tabs value={part} onValueChange={choose} className="gap-6 p-4 sm:p-6">
      {header(
        <TabsList className="h-10 w-full sm:w-fit" aria-label="Welcome & portal">
          <TabsTrigger value="welcome" className="px-3"><Send aria-hidden /> Welcome handoff</TabsTrigger>
          <TabsTrigger value="portal" className="px-3"><DoorOpen aria-hidden /> Member portal</TabsTrigger>
        </TabsList>,
      )}
      <TabsContent value="welcome">
        <WelcomePortalSection currentOrg={currentOrg} />
      </TabsContent>
      <TabsContent value="portal" className="space-y-5">
        {portal}
      </TabsContent>
    </Tabs>
  );
}
