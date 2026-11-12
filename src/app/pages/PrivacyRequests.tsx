// Privacy requests: a person asks what is held about them, or to be erased; and how long the
// workspace keeps personal data. Find them (by email, or from one of their records: ?lead=,
// ?contact=, ?member=), give them everything held as one download, or erase them; the log of
// erasures; the retention periods, what they would remove and their runs; and the settings
// (who answers for the data, consent on the public forms, the periods).
//
// It takes privacy.manage, which the owner and managers hold and a member can be given on
// the Team page: a download hands over everything about someone at once, and an erasure
// cannot be undone, so neither comes with editing records. The server refuses everyone
// else, and this page says so instead of offering what would be refused.

import { useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ShieldCheck } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { servesDemoData } from '@/app/api/apiClient';
import { PERMISSION } from '@/app/api/organizations';
import { DEMO_MESSAGE, type PersonLookup } from '@/app/api/privacy';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { mayIn } from '@/app/hooks/useMay';
import { PersonRequestPanel } from '@/app/components/privacy/PersonRequestPanel';
import { ErasureLogPanel } from '@/app/components/privacy/ErasureLogPanel';
import { RetentionPanel } from '@/app/components/privacy/RetentionPanel';
import { PrivacySettingsPanel } from '@/app/components/privacy/PrivacySettingsPanel';

const TABS = ['requests', 'erasures', 'retention', 'settings'] as const;
type Tab = (typeof TABS)[number];

/** Where a request starts: one of the person's records, or an address, from the URL. */
function startOf(search: URLSearchParams): PersonLookup | null {
  const lead = search.get('lead');
  const contact = search.get('contact');
  const member = search.get('member');
  const email = search.get('email');
  if (lead) return { leadId: lead };
  if (contact) return { contactId: contact };
  if (member) return { memberId: member };
  if (email) return { email };
  return null;
}

function Notice({ children }: { children: React.ReactNode }) {
  return <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">{children}</p>;
}

export default function PrivacyRequests() {
  const orgContext = useOrgOptional();
  const org = orgContext?.currentOrg ?? null;
  const demo = servesDemoData();
  const may = mayIn(orgContext, PERMISSION.ManagePrivacy);
  const [search, setSearch] = useSearchParams();
  const asked = search.get('tab');
  const tab: Tab = TABS.includes(asked as Tab) ? (asked as Tab) : 'requests';
  const start = useMemo(() => startOf(search), [search]);

  const open = (value: string) => {
    const next = new URLSearchParams(search);
    if (value === 'requests') next.delete('tab');
    else next.set('tab', value);
    setSearch(next, { replace: true });
  };

  // After an erasure the record the page was opened from is gone: forget it.
  const forgetStart = () => {
    const next = new URLSearchParams(search);
    for (const k of ['lead', 'contact', 'member', 'email']) next.delete(k);
    setSearch(next, { replace: true });
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={ShieldCheck}
            tone="teal"
            title="Privacy requests"
            subtitle="Everything held about a person as one download, their erasure, and how long the workspace keeps personal data."
          />

          {demo ? (
            <Notice>{DEMO_MESSAGE}</Notice>
          ) : !org ? (
            <Notice>Privacy requests are handled in a club&apos;s or company&apos;s workspace. Switch to it first.</Notice>
          ) : !may ? (
            <Notice>
              Only someone allowed to handle privacy requests can use this page: the owner, a manager, or a member given
              &ldquo;Handle privacy requests&rdquo; on the Team page.
            </Notice>
          ) : (
            <Tabs value={tab} onValueChange={open}>
              <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
                <TabsTrigger value="requests">Requests</TabsTrigger>
                <TabsTrigger value="erasures">Erasures</TabsTrigger>
                <TabsTrigger value="retention">Retention</TabsTrigger>
                <TabsTrigger value="settings">Settings</TabsTrigger>
              </TabsList>
              <TabsContent value="requests">
                <PersonRequestPanel start={start} onErased={forgetStart} />
              </TabsContent>
              <TabsContent value="erasures">
                <ErasureLogPanel />
              </TabsContent>
              <TabsContent value="retention">
                <RetentionPanel onOpenSettings={() => open('settings')} />
              </TabsContent>
              <TabsContent value="settings">
                <PrivacySettingsPanel />
              </TabsContent>
            </Tabs>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
