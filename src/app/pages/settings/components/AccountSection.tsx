import { Link } from 'react-router-dom';
import { Settings2, DollarSign, Clock, Download, RefreshCw, LogOut, Trash2 } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest, CurrencyOption } from '@/app/api/types';
import { useSignOut } from '@/app/lib/useSignOut';

export interface AccountSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
  currencies: CurrencyOption[];
  handleReset: () => void;
}

export function AccountSection({
  settings,
  updateSettings,
  currencies,
  handleReset,
}: AccountSectionProps) {
  const signOut = useSignOut();
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Settings2 className="w-5 h-5 text-orange-600" />
          Account & Data
        </h2>
        <p className="text-slate-600 text-sm mt-1">Manage your account settings and data</p>
      </div>

      {/* Defaults */}
      <div>
        <h3 className="font-semibold text-slate-900 mb-4">Default Settings</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="defaultCurrency" className="block text-sm font-medium text-slate-700 mb-2">
              <DollarSign className="w-4 h-4 inline mr-1" />
              Default Currency
            </label>
            <select
              id="defaultCurrency"
              value={settings.defaultCurrency || 'USD'}
              onChange={(e) => updateSettings({ defaultCurrency: e.target.value })}
              className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
            >
              {currencies.map((c) => (
                <option key={c.code} value={c.code}>{c.symbol} {c.code} - {c.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="followUpDays" className="block text-sm font-medium text-slate-700 mb-2">
              <Clock className="w-4 h-4 inline mr-1" />
              Default Follow-up Days
            </label>
            <input
              type="number"
              id="followUpDays"
              min={1}
              max={30}
              value={settings.defaultFollowUpDays}
              onChange={(e) => updateSettings({ defaultFollowUpDays: parseInt(e.target.value, 10) || 3 })}
              className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
            />
          </div>
        </div>
      </div>

      {/* Privacy */}
      <div className="pt-6 border-t border-slate-200">
        <h3 className="font-semibold text-slate-900 mb-4">Privacy</h3>
        <div className="space-y-3">
          <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
            <div>
              <p className="font-medium text-slate-900">Show Activity Status</p>
              <p className="text-sm text-slate-500">Let others see when you're active</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={settings.showActivityStatus}
              onClick={() => updateSettings({ showActivityStatus: !settings.showActivityStatus })}
              className={`relative w-12 h-7 rounded-full transition-colors ${
                settings.showActivityStatus ? 'bg-orange-600' : 'bg-slate-300'
              }`}
            >
              <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${
                settings.showActivityStatus ? 'translate-x-5' : ''
              }`} />
            </button>
          </label>

          <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
            <div>
              <p className="font-medium text-slate-900">Usage Analytics</p>
              <p className="text-sm text-slate-500">Help us improve by sharing anonymous usage data</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={settings.allowAnalytics}
              onClick={() => updateSettings({ allowAnalytics: !settings.allowAnalytics })}
              className={`relative w-12 h-7 rounded-full transition-colors ${
                settings.allowAnalytics ? 'bg-orange-600' : 'bg-slate-300'
              }`}
            >
              <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${
                settings.allowAnalytics ? 'translate-x-5' : ''
              }`} />
            </button>
          </label>
        </div>
      </div>

      {/* Data Management */}
      <div className="pt-6 border-t border-slate-200">
        <h3 className="font-semibold text-slate-900 mb-4">Data Management</h3>
        {/* There was an "Export Data" button here that did nothing. The lists can
            export; this says where, rather than offering a button that pretends. */}
        <p className="mb-4 flex items-start gap-2 text-sm text-slate-600">
          <Download className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          To take a copy of your records, use Export CSV on the Leads, Contacts and other lists.
        </p>
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={handleReset}
            className="h-11 px-5 border-2 border-slate-200 rounded-xl text-slate-700 font-medium hover:bg-slate-50 flex items-center gap-2 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Reset Settings
          </button>
        </div>
      </div>

      {/* Account Actions */}
      <div className="pt-6 border-t border-slate-200 space-y-4">
        <h3 className="font-semibold text-slate-900">Account Actions</h3>
        
        <button
          type="button"
          onClick={signOut}
          className="w-full flex items-center justify-center gap-2 h-11 border-2 border-slate-200 rounded-xl hover:border-slate-300 transition-colors font-medium text-slate-700"
        >
          <LogOut className="w-5 h-5" />
          Logout
        </button>

        {/* The Delete Account button asked for "DELETE", promised permanent deletion,
            and only went to the home page: nothing was deleted and the session stayed.
            Deleting an account has to decide what happens to the workspaces it owns and
            the records it made in shared ones, so until the server does that, this says
            plainly how to ask for it instead. */}
        <div className="rounded-xl border-2 border-slate-200 p-4">
          <p className="flex items-center gap-2 font-medium text-slate-900">
            <Trash2 className="h-5 w-5 text-slate-500" aria-hidden />
            Deleting your account
          </p>
          <p className="mt-2 text-sm text-slate-600">
            Cadence cannot delete an account from this page yet. To have your account and personal data
            deleted, ask the person who administers Cadence for your organization. To leave a workspace
            instead, ask its owner to remove you on the Team page.
          </p>
        </div>

        <Link to="/help" className="block text-center text-sm text-orange-700 hover:text-orange-800 font-medium pt-2">
          Need help?
        </Link>
      </div>
    </div>
  );
}
