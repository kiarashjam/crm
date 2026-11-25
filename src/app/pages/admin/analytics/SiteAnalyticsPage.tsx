// Admin, Site analytics (/admin/analytics): who visits www.cadencee.ai and what they do.
//
// A platform page, for CadenceE's platform admins only. It asks the server who is one
// (GET /api/platform/me) before reading anything else, and shows everyone else a polite
// "not available". It belongs to no workspace, so its route sits outside the
// workspace-required wrapper: a platform admin with no workspace of their own can open it.

import AppHeader from '@/app/components/AppHeader';
import { LoadFailed } from '@/app/components/LoadFailed';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { usePlatformMe } from '@/app/hooks/queries/siteAnalytics';
import { servesDemoData } from '@/app/api/apiClient';
import { Bone } from './AnalyticsUI';
import { NotAvailable } from './NotAvailable';
import { SiteAnalyticsDashboard } from './SiteAnalyticsDashboard';

function Gate() {
  const me = usePlatformMe();
  if (servesDemoData()) return <NotAvailable />;
  if (me.isPending) {
    return (
      <div aria-busy="true" aria-label="Checking your access" className="space-y-4">
        <Bone className="h-11 w-72" />
        <Bone className="h-9 w-96 max-w-full" />
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
          {Array.from({ length: 6 }, (_, i) => <Bone key={i} className="h-32 rounded-2xl" />)}
        </div>
      </div>
    );
  }
  if (me.isError) {
    return <LoadFailed what="Your access to site analytics" error={me.error} onRetry={() => void me.refetch()} retrying={me.isFetching} />;
  }
  if (!me.data?.isPlatformAdmin) return <NotAvailable />;
  return <SiteAnalyticsDashboard />;
}

export default function SiteAnalyticsPage() {
  return (
    <div className="flex min-h-screen flex-col bg-gradient-subtle">
      <AppHeader />
      <main
        id={MAIN_CONTENT_ID}
        tabIndex={-1}
        className="w-full flex-1 px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
      >
        <Gate />
      </main>
    </div>
  );
}
