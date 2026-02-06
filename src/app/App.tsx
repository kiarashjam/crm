import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Outlet, useLocation } from 'react-router-dom';
import { Toaster } from 'sonner';
import SkipLink from '@/app/components/SkipLink';
import RequireAuth from '@/app/components/RequireAuth';
import { ErrorBoundary } from '@/app/components/ErrorBoundary';
import { PageLoader } from '@/app/components/PageLoader';
import { QueryProvider } from '@/app/providers';
import { OrgProvider, useOrgOptional } from '@/app/contexts/OrgContext';
import { ThemeProvider } from '@/app/contexts/ThemeContext';
import { isUsingRealApi } from '@/app/api/apiClient';

// Eager-loaded pages (critical path)
import Homepage from '@/app/pages/Homepage';
import Login from '@/app/pages/Login';

// Lazy-loaded pages (code splitting)
const Onboarding = lazy(() => import('@/app/pages/Onboarding'));
const Organizations = lazy(() => import('@/app/pages/Organizations'));
const Dashboard = lazy(() => import('@/app/pages/Dashboard'));
const GeneratedCopy = lazy(() => import('@/app/pages/GeneratedCopy'));
const SendToCrm = lazy(() => import('@/app/pages/SendToCrm'));
const Templates = lazy(() => import('@/app/pages/Templates'));
const History = lazy(() => import('@/app/pages/History'));
const Settings = lazy(() => import('@/app/pages/Settings'));
const Help = lazy(() => import('@/app/pages/Help'));
const Privacy = lazy(() => import('@/app/pages/Privacy'));
const Terms = lazy(() => import('@/app/pages/Terms'));
const Leads = lazy(() => import('@/app/pages/Leads'));
const LeadWebhook = lazy(() => import('@/app/pages/LeadWebhook'));
const LeadImport = lazy(() => import('@/app/pages/LeadImport'));
const Pipeline = lazy(() => import('@/app/pages/Pipeline'));
const Tasks = lazy(() => import('@/app/pages/Tasks'));
const Activities = lazy(() => import('@/app/pages/Activities'));
const Companies = lazy(() => import('@/app/pages/Companies'));
const Contacts = lazy(() => import('@/app/pages/Contacts'));
const Team = lazy(() => import('@/app/pages/Team'));
const EmailSequences = lazy(() => import('@/app/pages/EmailSequences').then(m => ({ default: m.EmailSequences })));
const ABTests = lazy(() => import('@/app/pages/ABTests').then(m => ({ default: m.ABTests })));
const CopyAnalytics = lazy(() => import('@/app/pages/CopyAnalytics').then(m => ({ default: m.CopyAnalytics })));

function ProtectedLayout() {
  return (
    <RequireAuth>
      <Suspense fallback={<PageLoader />}>
        <Outlet />
      </Suspense>
    </RequireAuth>
  );
}

function RequireOrgLayout() {
  const location = useLocation();
  const org = useOrgOptional();
  const isOrgPage = location.pathname === '/organizations';
  if (!isUsingRealApi()) return <Outlet />;
  if (!org) return <Outlet />;
  if (org.hasFetched && !org.currentOrgId && !isOrgPage) {
    return <Navigate to="/organizations" replace state={{ from: location.pathname }} />;
  }
  return <Outlet />;
}

export default function App() {
  return (
    <ErrorBoundary>
      <ThemeProvider>
        <QueryProvider>
          <BrowserRouter>
            <OrgProvider>
              <SkipLink />
              <Toaster position="top-center" richColors closeButton />
              <Routes>
                {/* Public routes - eager loaded */}
                <Route path="/" element={<Homepage />} />
                <Route path="/login" element={<Login />} />
                
                {/* Public routes - lazy loaded */}
                <Route path="/help" element={
                  <Suspense fallback={<PageLoader />}>
                    <Help />
                  </Suspense>
                } />
                <Route path="/privacy" element={
                  <Suspense fallback={<PageLoader />}>
                    <Privacy />
                  </Suspense>
                } />
                <Route path="/terms" element={
                  <Suspense fallback={<PageLoader />}>
                    <Terms />
                  </Suspense>
                } />
                
                {/* Protected routes */}
                <Route element={<ProtectedLayout />}>
                  <Route path="/organizations" element={<Organizations />} />
                  <Route path="/onboarding" element={<Onboarding />} />
                  <Route element={<RequireOrgLayout />}>
                    <Route path="/dashboard" element={<Dashboard />} />
                    <Route path="/generated" element={<GeneratedCopy />} />
                    <Route path="/send" element={<SendToCrm />} />
                    <Route path="/templates" element={<Templates />} />
                    <Route path="/history" element={<History />} />
                    <Route path="/leads" element={<Leads />} />
                    <Route path="/leads/webhook" element={<LeadWebhook />} />
                    <Route path="/leads/import" element={<LeadImport />} />
                    <Route path="/deals" element={<Pipeline />} />
                    <Route path="/tasks" element={<Tasks />} />
                    <Route path="/activities" element={<Activities />} />
                    <Route path="/contacts" element={<Contacts />} />
                    <Route path="/companies" element={<Companies />} />
                    <Route path="/team" element={<Team />} />
                    <Route path="/sequences" element={<EmailSequences />} />
                    <Route path="/ab-tests" element={<ABTests />} />
                    <Route path="/analytics" element={<CopyAnalytics />} />
                    <Route path="/settings" element={<Settings />} />
                  </Route>
                </Route>
                
                {/* Catch-all redirect */}
                <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
            </OrgProvider>
          </BrowserRouter>
        </QueryProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}
