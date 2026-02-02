import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LogOut, Trash2, RefreshCw, Save } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getUserSettings, saveUserSettings, getConnectionStatus, twoFactorSetup, twoFactorEnable, twoFactorDisable } from '@/app/api';
import type { UserSettings } from '@/app/api/types';
import { messages } from '@/app/api/messages';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/app/components/ui/input-otp';

export default function Settings() {
  const [settings, setSettings] = useState<UserSettings | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [connected, setConnected] = useState(false);
  const [twoFaEnabled, setTwoFaEnabled] = useState<boolean | null>(null);
  const [twoFaLoading, setTwoFaLoading] = useState(false);
  const [twoFaSecret, setTwoFaSecret] = useState<string | null>(null);
  const [twoFaUri, setTwoFaUri] = useState<string | null>(null);
  const [twoFaCode, setTwoFaCode] = useState('');
  const [twoFaDisablePassword, setTwoFaDisablePassword] = useState('');
  const [twoFaDisableCode, setTwoFaDisableCode] = useState('');
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deleteConfirmChecked, setDeleteConfirmChecked] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    getUserSettings()
      .then(setSettings)
      .catch(() => setSettings({ companyName: 'Acme Corporation', brandTone: 'professional' }));
    getConnectionStatus()
      .then((s) => setConnected(s.connected))
      .catch(() => {});

    // Best-effort: fetch 2FA status (setup endpoint returns Enabled + secret)
    twoFactorSetup()
      .then((r) => {
        setTwoFaEnabled(r.enabled);
        setTwoFaSecret(r.secret);
        setTwoFaUri(r.otpauthUri);
      })
      .catch(() => setTwoFaEnabled(false));
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

  const handleStart2fa = async () => {
    setTwoFaLoading(true);
    try {
      const r = await twoFactorSetup();
      setTwoFaEnabled(r.enabled);
      setTwoFaSecret(r.secret);
      setTwoFaUri(r.otpauthUri);
      toast.message('Scan the secret in your authenticator, then confirm the code.');
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setTwoFaLoading(false);
    }
  };

  const handleEnable2fa = async () => {
    if (twoFaCode.replace(/\D/g, '').length !== 6) return;
    setTwoFaLoading(true);
    try {
      await twoFactorEnable(twoFaCode);
      setTwoFaEnabled(true);
      setTwoFaCode('');
      toast.success('2FA enabled');
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      toast.error(msg);
    } finally {
      setTwoFaLoading(false);
    }
  };

  const handleDisable2fa = async () => {
    if (!twoFaDisablePassword || twoFaDisableCode.replace(/\D/g, '').length !== 6) return;
    setTwoFaLoading(true);
    try {
      await twoFactorDisable(twoFaDisablePassword, twoFaDisableCode);
      setTwoFaEnabled(false);
      setTwoFaSecret(null);
      setTwoFaUri(null);
      setTwoFaDisablePassword('');
      setTwoFaDisableCode('');
      toast.success('2FA disabled');
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      toast.error(msg);
    } finally {
      setTwoFaLoading(false);
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
            <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-labelledby="brand-heading">
              <h2 id="brand-heading" className="text-xl font-semibold text-slate-900 mb-6">Brand</h2>

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
            </section>

            <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-labelledby="crm-heading">
              <h2 id="crm-heading" className="text-xl font-semibold text-slate-900 mb-4">CRM connection</h2>
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
            </section>

            <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-labelledby="security-heading">
              <h2 id="security-heading" className="text-xl font-semibold text-slate-900 mb-4">Security</h2>

              <div className="flex items-center justify-between mb-4">
                <div>
                  <p className="font-medium text-slate-900">Two-factor authentication (2FA)</p>
                  <p className="text-sm text-slate-600">
                    {twoFaEnabled ? 'Enabled' : 'Optional (recommended)'}
                  </p>
                </div>
                <button
                  type="button"
                  disabled={twoFaLoading}
                  onClick={handleStart2fa}
                  className="h-10 px-4 rounded-lg border-2 border-slate-200 hover:bg-slate-50 font-medium text-slate-800 disabled:opacity-60"
                >
                  {twoFaEnabled ? 'View setup' : 'Enable'}
                </button>
              </div>

              {!twoFaEnabled && twoFaSecret && twoFaUri && (
                <div className="space-y-4">
                  <ol className="list-decimal list-inside space-y-1 text-sm text-slate-700 mb-4">
                    <li>Scan the QR code (or enter the secret) in your authenticator app.</li>
                    <li>Enter the 6-digit code from the app below.</li>
                    <li>Click Enable to turn on 2FA.</li>
                  </ol>
                  <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                    <p className="text-sm font-medium text-slate-900 mb-1">Secret (manual entry)</p>
                    <p className="font-mono text-sm text-slate-800 break-all">{twoFaSecret}</p>
                    <p className="text-xs text-slate-500 mt-2 break-all">
                      otpauth: <span className="font-mono">{twoFaUri}</span>
                    </p>
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">Enter 6-digit code to confirm</label>
                    <InputOTP maxLength={6} value={twoFaCode} onChange={setTwoFaCode}>
                      <InputOTPGroup>
                        {Array.from({ length: 6 }).map((_, i) => (
                          <InputOTPSlot key={i} index={i} />
                        ))}
                      </InputOTPGroup>
                    </InputOTP>
                  </div>

                  <button
                    type="button"
                    disabled={twoFaLoading || twoFaCode.replace(/\D/g, '').length !== 6}
                    onClick={handleEnable2fa}
                    className="w-full h-11 bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-colors"
                  >
                    {twoFaLoading ? 'Enabling…' : 'Confirm & enable 2FA'}
                  </button>
                </div>
              )}

              {twoFaEnabled && (
                <div className="mt-4 space-y-4">
                  <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-sm text-emerald-800">
                    2FA is enabled. You’ll be asked for a code when signing in.
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">Password (to disable)</label>
                    <input
                      type="password"
                      value={twoFaDisablePassword}
                      onChange={(e) => setTwoFaDisablePassword(e.target.value)}
                      className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                      autoComplete="current-password"
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">2FA code (to disable)</label>
                    <InputOTP maxLength={6} value={twoFaDisableCode} onChange={setTwoFaDisableCode}>
                      <InputOTPGroup>
                        {Array.from({ length: 6 }).map((_, i) => (
                          <InputOTPSlot key={i} index={i} />
                        ))}
                      </InputOTPGroup>
                    </InputOTP>
                  </div>

                  <button
                    type="button"
                    disabled={
                      twoFaLoading ||
                      !twoFaDisablePassword ||
                      twoFaDisableCode.replace(/\D/g, '').length !== 6
                    }
                    onClick={handleDisable2fa}
                    className="w-full h-11 border-2 border-red-200 text-red-600 hover:bg-red-50 font-semibold rounded-xl transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    {twoFaLoading ? 'Disabling…' : 'Disable 2FA'}
                  </button>
                </div>
              )}
            </section>

            <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-labelledby="account-heading">
              <h2 id="account-heading" className="text-xl font-semibold text-slate-900 mb-4">Account</h2>
              <div className="space-y-3">
                <Link
                  to="/login"
                  className="w-full flex items-center justify-center gap-2 h-11 py-3 px-6 border-2 border-slate-200 rounded-xl hover:border-slate-300 transition-colors font-medium text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  <LogOut className="w-5 h-5" />
                  Logout
                </Link>

                {showDeleteConfirm ? (
                <div className="space-y-3 p-4 border-2 border-red-200 rounded-xl bg-red-50/50">
                  <p className="text-sm text-slate-700">To permanently delete your account:</p>
                  <label className="flex items-start gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={deleteConfirmChecked}
                      onChange={(e) => setDeleteConfirmChecked(e.target.checked)}
                      className="mt-1 rounded border-slate-300 text-red-600 focus:ring-red-500"
                    />
                    <span className="text-sm text-slate-700">I understand my data will be removed and this cannot be undone.</span>
                  </label>
                  <input
                    type="text"
                    value={deleteConfirmText}
                    onChange={(e) => setDeleteConfirmText(e.target.value)}
                    placeholder="Type DELETE to confirm"
                    className="w-full h-11 px-4 border border-slate-300 rounded-xl"
                    aria-label="Type DELETE to confirm"
                  />
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => { setShowDeleteConfirm(false); setDeleteConfirmText(''); setDeleteConfirmChecked(false); }}
                      className="flex-1 h-11 border-2 border-slate-200 rounded-xl font-medium text-slate-700 hover:bg-slate-100"
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      onClick={handleDeleteAccount}
                      disabled={!canConfirmDelete}
                      className="flex-1 h-11 bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold rounded-xl"
                    >
                      Delete account
                    </button>
                  </div>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setShowDeleteConfirm(true)}
                  className="w-full flex items-center justify-center gap-2 h-11 py-3 px-6 border-2 border-red-200 text-red-600 hover:border-red-300 hover:bg-red-50 rounded-xl font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500 focus-visible:ring-offset-2"
                >
                  <Trash2 className="w-5 h-5" />
                  Delete Account
                </button>
              )}
                <Link to="/help" className="block text-center text-sm text-orange-600 hover:text-orange-700 font-medium mt-4">
                  Need help?
                </Link>
              </div>
            </section>
          </div>
        </div>
      </main>
    </div>
  );
}
