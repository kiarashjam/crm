import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Homepage from '@/app/pages/Homepage';
import Login from '@/app/pages/Login';
import HubSpotConnection from '@/app/pages/HubSpotConnection';
import Onboarding from '@/app/pages/Onboarding';
import Dashboard from '@/app/pages/Dashboard';
import GeneratedCopy from '@/app/pages/GeneratedCopy';
import SendToHubSpot from '@/app/pages/SendToHubSpot';
import Templates from '@/app/pages/Templates';
import History from '@/app/pages/History';
import Settings from '@/app/pages/Settings';
import Help from '@/app/pages/Help';
import Privacy from '@/app/pages/Privacy';
import Terms from '@/app/pages/Terms';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Homepage />} />
        <Route path="/login" element={<Login />} />
        <Route path="/connect-hubspot" element={<HubSpotConnection />} />
        <Route path="/onboarding" element={<Onboarding />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/generated" element={<GeneratedCopy />} />
        <Route path="/send" element={<SendToHubSpot />} />
        <Route path="/templates" element={<Templates />} />
        <Route path="/history" element={<History />} />
        <Route path="/settings" element={<Settings />} />
        <Route path="/help" element={<Help />} />
        <Route path="/privacy" element={<Privacy />} />
        <Route path="/terms" element={<Terms />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
