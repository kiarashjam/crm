import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CheckCircle, Sparkles } from 'lucide-react';
import { toast } from 'sonner';
import { getConnectionStatus, setConnectionStatus } from '@/app/api';
import { messages } from '@/app/api/messages';
import { getCurrentUser } from '@/app/lib/auth';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Connection() {
  const [isConnected, setIsConnected] = useState(false);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();
  const user = getCurrentUser();

  useEffect(() => {
    getConnectionStatus()
      .then((status) => {
        setIsConnected(status.connected);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, []);

  const handleConnect = async () => {
    setLoading(true);
    try {
      await setConnectionStatus({
        connected: true,
        accountEmail: user?.email ?? 'company@example.com',
      });
      setIsConnected(true);
      toast.success(messages.connection.connected);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setLoading(false);
    }
  };

  const handleContinue = () => {
    navigate('/onboarding');
  };

  if (loading && !isConnected) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-orange-50 to-white flex items-center justify-center px-4">
        <LoadingSpinner size="lg" label="Checking connection…" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 flex flex-col items-center justify-center px-[var(--page-padding)] py-12">
      <main id={MAIN_CONTENT_ID} className="w-full max-w-lg" tabIndex={-1}>
        {!isConnected ? (
          <div className="text-center">
            <div className="w-20 h-20 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-lg" aria-hidden>
              <Sparkles className="w-12 h-12 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">Connect your CRM</h1>
            <p className="text-slate-600 mb-8 max-w-md mx-auto">
              Connect your account to create and manage CRM copy. This allows us to:
            </p>

            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 mb-8 text-left">
              <ul className="space-y-4" role="list">
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-emerald-500 flex-shrink-0 mt-0.5" aria-hidden />
                  <div>
                    <p className="font-medium text-slate-900">Read your contacts and deals</p>
                    <p className="text-sm text-slate-600">To personalize AI-generated copy</p>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-emerald-500 flex-shrink-0 mt-0.5" aria-hidden />
                  <div>
                    <p className="font-medium text-slate-900">Create emails and notes</p>
                    <p className="text-sm text-slate-600">To save generated content to your CRM</p>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-emerald-500 flex-shrink-0 mt-0.5" aria-hidden />
                  <div>
                    <p className="font-medium text-slate-900">Update workflow messages</p>
                    <p className="text-sm text-slate-600">To integrate with your automations</p>
                  </div>
                </li>
              </ul>
            </div>

            <button
              type="button"
              onClick={handleConnect}
              disabled={loading}
              className="w-full h-12 bg-orange-600 hover:bg-orange-500 disabled:opacity-70 text-white font-semibold rounded-xl transition-colors mb-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
            >
              Connect
            </button>

            <Link to="/dashboard" className="block text-slate-500 hover:text-slate-700 text-sm focus-visible:rounded focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2">
              Skip for now
            </Link>
          </div>
        ) : (
          <div className="text-center">
<div className="w-20 h-20 bg-emerald-100 rounded-full flex items-center justify-center mx-auto mb-6" aria-hidden>
                <CheckCircle className="w-12 h-12 text-emerald-600" />
              </div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">Successfully Connected!</h1>
            <p className="text-slate-600 mb-8">
              Your account is now connected. You're ready to start generating AI-powered copy.
            </p>

            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 mb-8">
              <div className="text-left">
                <p className="text-sm text-slate-600 mb-2">Connected Account</p>
                <p className="font-semibold text-slate-900 text-lg mb-4">{user?.email ?? 'company@example.com'}</p>
                <div className="flex items-center gap-2 text-sm text-emerald-600">
                  <div className="w-2 h-2 bg-emerald-500 rounded-full shrink-0" aria-hidden />
                  Active Connection
                </div>
              </div>
            </div>

            <button
              type="button"
              onClick={handleContinue}
              className="w-full h-12 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
            >
              Continue
            </button>
          </div>
        )}
      </main>
    </div>
  );
}
