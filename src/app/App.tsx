import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'sonner';
import SkipLink from '@/app/components/SkipLink';
import Homepage from '@/app/pages/Homepage';
import Login from '@/app/pages/Login';
import Connection from '@/app/pages/Connection';
import Onboarding from '@/app/pages/Onboarding';
import Dashboard from '@/app/pages/Dashboard';
import GeneratedCopy from '@/app/pages/GeneratedCopy';
import SendToCrm from '@/app/pages/SendToCrm';
import Templates from '@/app/pages/Templates';
import History from '@/app/pages/History';
import Settings from '@/app/pages/Settings';
import Help from '@/app/pages/Help';
import Privacy from '@/app/pages/Privacy';
import Terms from '@/app/pages/Terms';
import Leads from '@/app/pages/Leads';
import Pipeline from '@/app/pages/Pipeline';
import Tasks from '@/app/pages/Tasks';
import Activities from '@/app/pages/Activities';
import Companies from '@/app/pages/Companies';
import Contacts from '@/app/pages/Contacts';

export default function App() {
  return (
    <BrowserRouter>
      <SkipLink />
      <Toaster position="top-center" richColors closeButton />
      <Routes>
        <Route path="/" element={<Homepage />} />
        <Route path="/login" element={<Login />} />
        <Route path="/connect" element={<Connection />} />
        <Route path="/onboarding" element={<Onboarding />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/generated" element={<GeneratedCopy />} />
        <Route path="/send" element={<SendToCrm />} />
        <Route path="/templates" element={<Templates />} />
        <Route path="/history" element={<History />} />
        <Route path="/leads" element={<Leads />} />
        <Route path="/deals" element={<Pipeline />} />
        <Route path="/tasks" element={<Tasks />} />
        <Route path="/activities" element={<Activities />} />
        <Route path="/contacts" element={<Contacts />} />
        <Route path="/companies" element={<Companies />} />
        <Route path="/settings" element={<Settings />} />
        <Route path="/help" element={<Help />} />
        <Route path="/privacy" element={<Privacy />} />
        <Route path="/terms" element={<Terms />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
