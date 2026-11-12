import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Outlet, useLocation } from 'react-router-dom';
import { Toaster } from 'sonner';
import SkipLink from '@/app/components/SkipLink';
import RequireAuth from '@/app/components/RequireAuth';
import { WorkspaceOutlet } from '@/app/components/WorkspaceOutlet';
import { ErrorBoundary } from '@/app/components/ErrorBoundary';
import { PageLoader } from '@/app/components/PageLoader';
import PageHelp from '@/app/components/PageHelp';
import { QueryProvider, WorkspaceCacheGuard } from '@/app/providers';
import { OrgProvider, useOrgOptional } from '@/app/contexts/OrgContext';
import { ThemeProvider } from '@/app/contexts/ThemeContext';
import { servesDemoData } from '@/app/api/apiClient';
import { PageLoadingProvider } from '@/app/components/LoadingOverlay';
import { RouteProgress } from '@/app/components/motion/RouteProgress';
import { ModuleOutlet, SalesHome } from '@/app/components/ModuleOff';
import { I18nProvider } from '@/app/i18n';

// Eager-loaded pages (critical path)
import Homepage from '@/app/pages/Homepage';
import Login from '@/app/pages/Login';
import ForgotPassword from '@/app/pages/ForgotPassword';
import ResetPassword from '@/app/pages/ResetPassword';

// Lazy-loaded pages (code splitting)
const Organizations = lazy(() => import('@/app/pages/Organizations'));
const AcceptInvite = lazy(() => import('@/app/pages/AcceptInvite'));
const VerifyEmail = lazy(() => import('@/app/pages/VerifyEmail'));
const Dashboard = lazy(() => import('@/app/pages/Dashboard'));
const SendToCrm = lazy(() => import('@/app/pages/SendToCrm'));
const Templates = lazy(() => import('@/app/pages/Templates'));
const History = lazy(() => import('@/app/pages/History'));
const Settings = lazy(() => import('@/app/pages/Settings'));
const Help = lazy(() => import('@/app/pages/Help'));
const Privacy = lazy(() => import('@/app/pages/Privacy'));
const Terms = lazy(() => import('@/app/pages/Terms'));
const SignContract = lazy(() => import('@/app/pages/SignContract'));
const Leads = lazy(() => import('@/app/pages/Leads'));
const LeadDetailPage = lazy(() => import('@/app/pages/LeadDetailPage'));
const LeadWebhook = lazy(() => import('@/app/pages/LeadWebhook'));
const LeadImport = lazy(() => import('@/app/pages/LeadImport'));
const ContactImport = lazy(() => import('@/app/pages/ContactImport'));
const CompanyImport = lazy(() => import('@/app/pages/CompanyImport'));
const LeadRouting = lazy(() => import('@/app/pages/LeadRouting'));
const PublicCapture = lazy(() => import('@/app/pages/PublicCapture'));
const PublicApply = lazy(() => import('@/app/pages/PublicApply'));
const Unsubscribe = lazy(() => import('@/app/pages/Unsubscribe'));
const PayPage = lazy(() => import('@/app/pages/PayPage'));
const Pipeline = lazy(() => import('@/app/pages/Pipeline'));
const DealDetail = lazy(() => import('@/app/pages/DealDetail'));
const Tasks = lazy(() => import('@/app/pages/Tasks'));
const TaskDetail = lazy(() => import('@/app/pages/TaskDetail'));
const Activities = lazy(() => import('@/app/pages/Activities'));
const Sequences = lazy(() => import('@/app/pages/Sequences'));
const Reports = lazy(() => import('@/app/pages/Reports'));
const Automations = lazy(() => import('@/app/pages/Automations'));
const Duplicates = lazy(() => import('@/app/pages/Duplicates'));
const AuditLog = lazy(() => import('@/app/pages/AuditLog'));
const PrivacyRequests = lazy(() => import('@/app/pages/PrivacyRequests'));
const Companies = lazy(() => import('@/app/pages/Companies'));
const CompanyDetail = lazy(() => import('@/app/pages/CompanyDetail'));
const Contacts = lazy(() => import('@/app/pages/Contacts'));
const ContactDetail = lazy(() => import('@/app/pages/ContactDetail'));
const Team = lazy(() => import('@/app/pages/Team'));
const Members = lazy(() => import('@/app/pages/Members'));
const MemberDetail = lazy(() => import('@/app/pages/MemberDetail'));
const Events = lazy(() => import('@/app/pages/Events'));
const EventDetail = lazy(() => import('@/app/pages/EventDetail'));
const Reservations = lazy(() => import('@/app/pages/Reservations'));
const Loyalty = lazy(() => import('@/app/pages/Loyalty'));
const Applications = lazy(() => import('@/app/pages/Applications'));
const Charges = lazy(() => import('@/app/pages/Charges'));
const Visits = lazy(() => import('@/app/pages/Visits'));
const GiftCards = lazy(() => import('@/app/pages/GiftCards'));
const Communications = lazy(() => import('@/app/pages/Communications'));
const MemberPortal = lazy(() => import('@/app/pages/MemberPortal'));
const Households = lazy(() => import('@/app/pages/Households'));
const Subscriptions = lazy(() => import('@/app/pages/Subscriptions'));
const Campaigns = lazy(() => import('@/app/pages/Campaigns'));
const Donations = lazy(() => import('@/app/pages/Donations'));
const Calendar = lazy(() => import('@/app/pages/Calendar'));
const Statements = lazy(() => import('@/app/pages/Statements'));
const ClubPayments = lazy(() => import('@/app/pages/ClubPayments'));
const HospitalityReports = lazy(() => import('@/app/pages/HospitalityReports'));
const TabsPage = lazy(() => import('@/app/pages/Tabs'));
const PosTerminal = lazy(() => import('@/app/pages/PosTerminal'));
const PosTickets = lazy(() => import('@/app/pages/PosTickets'));
const HospitalitySettings = lazy(() => import('@/app/pages/HospitalitySettings'));
const HospitalityDashboard = lazy(() => import('@/app/pages/HospitalityDashboard'));
const MemberImport = lazy(() => import('@/app/pages/MemberImport'));

function ProtectedLayout() {
  return (
    <RequireAuth>
      <Suspense fallback={<PageLoader />}>
        <Outlet />
      </Suspense>
      <PageHelp />
    </RequireAuth>
  );
}

function RequireOrgLayout() {
  const location = useLocation();
  const org = useOrgOptional();
  const isOrgPage = location.pathname === '/organizations';
  if (servesDemoData()) return <WorkspaceOutlet />;
  if (!org) return <WorkspaceOutlet />;
  if (org.hasFetched && !org.currentOrgId && !isOrgPage) {
    return <Navigate to="/organizations" replace state={{ from: location.pathname }} />;
  }
  return <WorkspaceOutlet />;
}

export default function App() {
  return (
    <ErrorBoundary>
      <ThemeProvider>
        <QueryProvider>
          <BrowserRouter>
            <PageLoadingProvider>
              <RouteProgress />
            <OrgProvider>
              {/* Inside the workspaces: someone who has not chosen a language reads their workspace's. */}
              <I18nProvider>
              <WorkspaceCacheGuard />
              <SkipLink />
              <Toaster position="top-center" richColors closeButton />
              <Routes>
                {/* Public routes - eager loaded */}
                <Route path="/" element={<Homepage />} />
                <Route path="/login" element={<Login />} />
                <Route path="/forgot-password" element={<ForgotPassword />} />
                <Route path="/reset-password" element={<ResetPassword />} />
                <Route path="/verify-email" element={
                  <Suspense fallback={<PageLoader />}>
                    <VerifyEmail />
                  </Suspense>
                } />
                
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
                {/* Public contract signing. Outside ProtectedLayout on purpose:
                    the counterparty has no account, and requiring one to sign a
                    contract somebody sent them is a dead end. The token in the URL
                    is the whole authorisation. */}
                <Route path="/sign/:token" element={
                  <Suspense fallback={<PageLoader />}>
                    <SignContract />
                  </Suspense>
                } />
                <Route path="/capture/:token" element={
                  <Suspense fallback={<PageLoader />}>
                    <PublicCapture />
                  </Suspense>
                } />
                {/* The club's membership application: a stranger applies without an account. */}
                <Route path="/apply/:token" element={
                  <Suspense fallback={<PageLoader />}>
                    <PublicApply />
                  </Suspense>
                } />
                {/* The unsubscribe link in a workspace's email. Public for the same
                    reason as signing: the person following it has no account. */}
                <Route path="/unsubscribe/:token" element={
                  <Suspense fallback={<PageLoader />}>
                    <Unsubscribe />
                  </Suspense>
                } />
                {/* A club's pay link: the member paying has no account here either. */}
                <Route path="/pay/:token" element={
                  <Suspense fallback={<PageLoader />}>
                    <PayPage />
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
                  {/* Outside RequireOrgLayout: the invitee may have no workspace yet. */}
                  <Route path="/invites/accept" element={<AcceptInvite />} />
                  <Route path="/onboarding" element={<Navigate to="/settings" replace />} />
                  <Route element={<RequireOrgLayout />}>
                    <Route path="/dashboard" element={<SalesHome><Dashboard /></SalesHome>} />
                    {/* The Sales module's pages: with it turned off they say so (ModuleOutlet). */}
                    <Route element={<ModuleOutlet module="sales" />}>
                      <Route path="/send" element={<SendToCrm />} />
                      <Route path="/history" element={<History />} />
                      <Route path="/leads" element={<Leads />} />
                      <Route path="/leads/webhook" element={<LeadWebhook />} />
                      <Route path="/leads/import" element={<LeadImport />} />
                      <Route path="/leads/routing" element={<LeadRouting />} />
                      <Route path="/leads/:id" element={<LeadDetailPage />} />
                      <Route path="/deals" element={<Pipeline />} />
                      <Route path="/deals/:id" element={<DealDetail />} />
                      <Route path="/sequences" element={<Sequences />} />
                      <Route path="/reports" element={<Reports />} />
                    </Route>
                    {/* What both modules share. */}
                    <Route path="/templates" element={<Templates />} />
                    <Route path="/tasks" element={<Tasks />} />
                    <Route path="/tasks/:id" element={<TaskDetail />} />
                    <Route path="/activities" element={<Activities />} />
                    <Route path="/automations" element={<Automations />} />
                    <Route path="/duplicates" element={<Duplicates />} />
                    <Route path="/audit" element={<AuditLog />} />
                    <Route path="/privacy-requests" element={<PrivacyRequests />} />
                    <Route path="/contacts" element={<Contacts />} />
                    <Route path="/contacts/import" element={<ContactImport />} />
                    <Route path="/contacts/:id" element={<ContactDetail />} />
                    <Route path="/companies" element={<Companies />} />
                    <Route path="/companies/import" element={<CompanyImport />} />
                    <Route path="/companies/:id" element={<CompanyDetail />} />
                    <Route path="/team" element={<Team />} />
                    <Route path="/calendar" element={<Calendar />} />
                    {/* The Club module's pages. */}
                    <Route element={<ModuleOutlet module="club" />}>
                      <Route path="/members" element={<Members />} />
                      <Route path="/members/import" element={<MemberImport />} />
                      <Route path="/members/:id" element={<MemberDetail />} />
                      <Route path="/events" element={<Events />} />
                      <Route path="/events/:id" element={<EventDetail />} />
                      <Route path="/reservations" element={<Reservations />} />
                      <Route path="/loyalty" element={<Loyalty />} />
                      <Route path="/applications" element={<Applications />} />
                      <Route path="/charges" element={<Charges />} />
                      <Route path="/visits" element={<Visits />} />
                      <Route path="/gift-cards" element={<GiftCards />} />
                      <Route path="/communications" element={<Communications />} />
                      <Route path="/portal" element={<MemberPortal />} />
                      <Route path="/households" element={<Households />} />
                      <Route path="/subscriptions" element={<Subscriptions />} />
                      <Route path="/campaigns" element={<Campaigns />} />
                      <Route path="/donations" element={<Donations />} />
                      <Route path="/statements" element={<Statements />} />
                      <Route path="/club-payments" element={<ClubPayments />} />
                      <Route path="/club-reports" element={<HospitalityReports />} />
                      <Route path="/tabs" element={<TabsPage />} />
                      <Route path="/pos" element={<PosTerminal />} />
                      <Route path="/pos-tickets" element={<PosTickets />} />
                      <Route path="/hospitality-settings" element={<HospitalitySettings />} />
                      <Route path="/hospitality" element={<HospitalityDashboard />} />
                    </Route>
                    <Route path="/settings" element={<Settings />} />
                    <Route path="/mcp" element={<Navigate to="/settings?tab=claude" replace />} />
                  </Route>
                </Route>
                
                {/* Catch-all redirect */}
                <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
              </I18nProvider>
            </OrgProvider>
            </PageLoadingProvider>
          </BrowserRouter>
        </QueryProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}
