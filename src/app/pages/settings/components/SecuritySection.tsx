import { Shield, Lock, Check } from 'lucide-react';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/app/components/ui/input-otp';

export interface SecuritySectionProps {
  twoFaEnabled: boolean | null;
  twoFaLoading: boolean;
  twoFaSecret: string | null;
  twoFaUri: string | null;
  twoFaCode: string;
  setTwoFaCode: (val: string) => void;
  twoFaDisablePassword: string;
  setTwoFaDisablePassword: (val: string) => void;
  twoFaDisableCode: string;
  setTwoFaDisableCode: (val: string) => void;
  handleStart2fa: () => void;
  handleEnable2fa: () => void;
  handleDisable2fa: () => void;
}

export function SecuritySection({
  twoFaEnabled,
  twoFaLoading,
  twoFaSecret,
  twoFaUri,
  twoFaCode,
  setTwoFaCode,
  twoFaDisablePassword,
  setTwoFaDisablePassword,
  twoFaDisableCode,
  setTwoFaDisableCode,
  handleStart2fa,
  handleEnable2fa,
  handleDisable2fa,
}: SecuritySectionProps) {
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Shield className="w-5 h-5 text-orange-600" />
          Security
        </h2>
        <p className="text-slate-600 text-sm mt-1">Protect your account with additional security</p>
      </div>

      {/* 2FA Section */}
      <div className="p-6 border border-slate-200 rounded-xl">
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <Lock className="w-5 h-5 text-slate-600" />
              <h3 className="font-semibold text-slate-900">Two-Factor Authentication (2FA)</h3>
            </div>
            <p className="text-sm text-slate-600 mt-1">
              Add an extra layer of security using an authenticator app
            </p>
          </div>
          {twoFaEnabled !== null && (
            <span className={`px-3 py-1 rounded-full text-xs font-medium ${
              twoFaEnabled ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-600'
            }`}>
              {twoFaEnabled ? 'Enabled' : 'Disabled'}
            </span>
          )}
        </div>

        {!twoFaEnabled && !twoFaSecret && (
          <button
            type="button"
            disabled={twoFaLoading}
            onClick={handleStart2fa}
            className="h-11 px-6 bg-orange-600 hover:bg-orange-700 disabled:opacity-60 text-white font-medium rounded-xl transition-colors"
          >
            {twoFaLoading ? 'Loading...' : 'Set up 2FA'}
          </button>
        )}

        {!twoFaEnabled && twoFaSecret && twoFaUri && (
          <div className="space-y-4">
            <ol className="list-decimal list-inside space-y-2 text-sm text-slate-700">
              <li>Install an authenticator app (Google Authenticator, Authy, etc.)</li>
              <li>Scan the QR code or enter the secret manually</li>
              <li>Enter the 6-digit code from the app below</li>
            </ol>

            <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
              <p className="text-sm font-medium text-slate-900 mb-1">Manual Entry Secret</p>
              <p className="font-mono text-sm text-slate-800 break-all select-all">{twoFaSecret}</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-2">Enter verification code</label>
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
              className="w-full h-11 bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-400 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-colors"
            >
              {twoFaLoading ? 'Verifying...' : 'Enable 2FA'}
            </button>
          </div>
        )}

        {twoFaEnabled && (
          <div className="space-y-4">
            <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
              <p className="text-sm text-emerald-800 flex items-center gap-2">
                <Check className="w-4 h-4" />
                2FA is enabled. You'll need to enter a code when signing in.
              </p>
            </div>

            <div className="pt-4 border-t border-slate-200">
              <p className="text-sm font-medium text-slate-700 mb-3">To disable 2FA, enter your password and a verification code:</p>
              
              <div className="space-y-3">
                <input
                  type="password"
                  value={twoFaDisablePassword}
                  onChange={(e) => setTwoFaDisablePassword(e.target.value)}
                  placeholder="Current password"
                  className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
                  autoComplete="current-password"
                />

                <InputOTP maxLength={6} value={twoFaDisableCode} onChange={setTwoFaDisableCode}>
                  <InputOTPGroup>
                    {Array.from({ length: 6 }).map((_, i) => (
                      <InputOTPSlot key={i} index={i} />
                    ))}
                  </InputOTPGroup>
                </InputOTP>

                <button
                  type="button"
                  disabled={twoFaLoading || !twoFaDisablePassword || twoFaDisableCode.replace(/\D/g, '').length !== 6}
                  onClick={handleDisable2fa}
                  className="w-full h-11 border-2 border-red-200 text-red-600 hover:bg-red-50 font-semibold rounded-xl transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  {twoFaLoading ? 'Disabling...' : 'Disable 2FA'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
