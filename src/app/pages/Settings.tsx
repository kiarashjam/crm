import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LogOut, Trash2, RefreshCw, Save } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getUserSettings, saveUserSettings, getConnectionStatus } from '@/app/api';
import type { UserSettings } from '@/app/api/types';
import { messages } from '@/app/api/messages';

export default function Settings() {
  const [settings, setSettings] = useState<UserSettings | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [connected, setConnected] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    getUserSettings()
      .then(setSettings)
      .catch(() => setSettings({ companyName: 'Acme Corporation', brandTone: 'professional' }));
    getConnectionStatus()
      .then((s) => setConnected(s.connected))
      .catch(() => {});
  }, []);

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    try {
      await saveUserSettings(settings);
      toast.success(messages.settings.saved);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteAccount = () => {
    if (showDeleteConfirm) {
      navigate('/');
    } else {
      setShowDeleteConfirm(true);
    }
  };

  if (settings === null) {
    return (
      <div className="min-h-screen bg-slate-50">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="w-full px-[var(--page-padding)] py-12 flex justify-center items-center min-h-[50vh]" tabIndex={-1}>
          <LoadingSpinner size="lg" />
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-2xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Settings</h1>
            <p className="text-slate-600">Manage your account and preferences</p>
          </div>

          <div className="space-y-6">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
              <h2 className="text-xl font-semibold text-slate-900 mb-6">Brand Settings</h2>

              <div className="space-y-6">
                <div>
                  <label htmlFor="company" className="block text-sm font-medium text-slate-700 mb-2">
                    Company Name
                  </label>
                  <input
                    type="text"
                    id="company"
                    value={settings.companyName}
                    onChange={(e) => setSettings((s) => (s ? { ...s, companyName: e.target.value } : s))}
                    className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-3">
                    Brand Tone
                  </label>
                  <div className="space-y-3">
                    {[
                      { value: 'professional' as const, label: 'Professional', desc: 'Formal, business-focused communication' },
                      { value: 'friendly' as const, label: 'Friendly', desc: 'Warm, approachable, and conversational' },
                      { value: 'persuasive' as const, label: 'Persuasive', desc: 'Compelling, action-oriented messaging' },
                    ].map((opt) => (
                      <label
                        key={opt.value}
                        className="flex items-center p-4 border-2 border-slate-200 rounded-xl cursor-pointer hover:border-orange-200 transition-colors"
                      >
                        <input
                          type="radio"
                          name="tone"
                          value={opt.value}
                          checked={settings.brandTone === opt.value}
                          onChange={() => setSettings((s) => (s ? { ...s, brandTone: opt.value } : s))}
                          className="w-4 h-4 text-orange-600 focus:ring-orange-500"
                        />
                        <div className="ml-3">
                          <p className="font-medium text-slate-900">{opt.label}</p>
                          <p className="text-sm text-slate-600">{opt.desc}</p>
                        </div>
                      </label>
                    ))}
                  </div>
                </div>

                <button
                  type="button"
                  onClick={handleSave}
                  disabled={saving}
                  className="w-full h-11 bg-orange-600 hover:bg-orange-500 disabled:opacity-70 text-white font-semibold rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  <Save className="w-5 h-5" />
                  {saving ? 'Saving...' : 'Save Changes'}
                </button>
              </div>
            </div>

            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
              <h2 className="text-xl font-semibold text-slate-900 mb-4">CRM Connection</h2>
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium text-slate-900">{connected ? 'Connected' : 'Not connected'}</p>
                  <div className="flex items-center gap-2 text-sm mt-1 text-slate-600">
                    <div className={`w-2 h-2 rounded-full shrink-0 ${connected ? 'bg-emerald-500' : 'bg-slate-400'}`} aria-hidden />
                    {connected ? 'Active' : 'Connect to sync contacts and deals'}
                  </div>
                </div>
                <Link
                  to="/connect"
                  className="flex items-center gap-2 text-orange-600 hover:text-orange-700 font-medium transition-colors"
                >
                  <RefreshCw className="w-5 h-5" />
                  {connected ? 'Reconnect' : 'Connect'}
                </Link>
              </div>
            </div>

            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
              <h2 className="text-xl font-semibold text-slate-900 mb-4">Account Actions</h2>
              <div className="space-y-3">
                <Link
                  to="/login"
                  className="w-full flex items-center justify-center gap-2 h-11 py-3 px-6 border-2 border-slate-200 rounded-xl hover:border-slate-300 transition-colors font-medium text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  <LogOut className="w-5 h-5" />
                  Logout
                </Link>

                <button
                  type="button"
                  onClick={handleDeleteAccount}
                  className={`w-full flex items-center justify-center gap-2 h-11 py-3 px-6 rounded-xl transition-colors font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 ${
                    showDeleteConfirm
                      ? 'bg-red-600 hover:bg-red-700 text-white focus-visible:ring-red-500'
                      : 'border-2 border-red-200 text-red-600 hover:border-red-300 hover:bg-red-50 focus-visible:ring-red-500'
                  }`}
                >
                  <Trash2 className="w-5 h-5" />
                  {showDeleteConfirm ? 'Click again to confirm deletion' : 'Delete Account'}
                </button>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
