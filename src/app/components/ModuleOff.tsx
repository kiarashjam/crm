import type { ReactNode } from 'react';
import { Link, Navigate, Outlet } from 'react-router-dom';
import { PowerOff } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { MODULE_LABELS, orgCan, PERMISSION, type WorkspaceModule } from '@/app/api/organizations';
import { useModules } from '@/app/hooks/useModules';

/**
 * What a page of a module the workspace has turned off shows instead, reached by a link
 * or a bookmark: that the module is off, that nothing was deleted, and who can turn it
 * back on. The server refuses the module's requests too, so the page could only fail.
 */
export function ModuleOffPage({ module }: { module: WorkspaceModule }) {
  const org = useOrgOptional();
  const modules = useModules();
  const label = MODULE_LABELS[module];
  const canTurnOn = orgCan(org?.currentOrg ?? null, PERMISSION.ManageSettings);
  // Somewhere that is on: the other module's home page.
  const home = module === 'sales' ? (modules.club ? '/hospitality' : '/contacts') : (modules.sales ? '/dashboard' : '/contacts');

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} tabIndex={-1} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]">
        <div role="status" className="mx-auto mt-10 max-w-xl rounded-2xl border border-slate-200 bg-white p-10 text-center shadow-sm dark:border-slate-700 dark:bg-slate-800">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-100 text-slate-600 ring-1 ring-slate-200 dark:bg-slate-900 dark:text-slate-300 dark:ring-slate-700">
            <PowerOff className="h-7 w-7" aria-hidden="true" />
          </div>
          <h1 className="mt-5 text-xl font-semibold text-slate-900 dark:text-slate-100">The {label} module is turned off</h1>
          <p className="mt-2 text-sm leading-relaxed text-slate-600 dark:text-slate-400">
            {org?.currentOrg?.name ? `${org.currentOrg.name} does not use ${label} at the moment` : `This workspace does not use ${label} at the moment`},
            so its pages are hidden. Nothing in it was deleted: turning the module back on brings everything back as it was.
          </p>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
            {canTurnOn
              ? 'You can turn it on again in Settings, under Team.'
              : 'Someone who manages the workspace’s settings can turn it on again.'}
          </p>
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            {canTurnOn && (
              <Link
                to="/settings?tab=organization"
                className="inline-flex h-10 items-center rounded-xl bg-orange-700 px-5 text-sm font-semibold text-white hover:bg-orange-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
              >
                Open Settings
              </Link>
            )}
            <Link
              to={home}
              className="inline-flex h-10 items-center rounded-xl border border-slate-300 px-5 text-sm font-semibold text-slate-700 hover:border-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 dark:border-slate-600 dark:text-slate-100"
            >
              Go to what is on
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}

/** The pages of one module: shown while it is on, the turned-off page while it is not. */
export function ModuleOutlet({ module }: { module: WorkspaceModule }) {
  const modules = useModules();
  return modules[module] ? <Outlet /> : <ModuleOffPage module={module} />;
}

/**
 * The sales dashboard (its children), where sign-in and the workspace launcher land. A
 * workspace that has turned Sales off lands on the club's day instead, rather than on a
 * page saying Sales is off.
 */
export function SalesHome({ children }: { children: ReactNode }) {
  const modules = useModules();
  if (!modules.sales && modules.club) return <Navigate to="/hospitality" replace />;
  if (!modules.sales) return <ModuleOffPage module="sales" />;
  return <>{children}</>;
}
