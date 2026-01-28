import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LogOut, Trash2, RefreshCw, Save } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function Settings() {
  const [companyName, setCompanyName] = useState('Acme Corporation');
  const [brandTone, setBrandTone] = useState('professional');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const navigate = useNavigate();

  const handleSave = () => {
    // Save settings
    alert('Settings saved successfully!');
  };

  const handleDeleteAccount = () => {
    if (showDeleteConfirm) {
      // Delete account logic
      navigate('/');
    } else {
      setShowDeleteConfirm(true);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Settings</h1>
            <p className="text-gray-600">Manage your account and preferences</p>
          </div>

          <div className="space-y-6">
            {/* Brand Settings */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
              <h2 className="text-xl font-semibold text-gray-900 mb-6">Brand Settings</h2>

              <div className="space-y-6">
                {/* Company Name */}
                <div>
                  <label htmlFor="company" className="block text-sm font-medium text-gray-700 mb-2">
                    Company Name
                  </label>
                  <input
                    type="text"
                    id="company"
                    value={companyName}
                    onChange={(e) => setCompanyName(e.target.value)}
                    className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition-colors"
                  />
                </div>

                {/* Brand Tone */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    Brand Tone
                  </label>
                  <div className="space-y-3">
                    <label className="flex items-center p-4 border-2 border-gray-200 rounded-lg cursor-pointer hover:border-orange-300 transition-colors">
                      <input
                        type="radio"
                        name="tone"
                        value="professional"
                        checked={brandTone === 'professional'}
                        onChange={(e) => setBrandTone(e.target.value)}
                        className="w-4 h-4 text-orange-600 focus:ring-orange-500"
                      />
                      <div className="ml-3">
                        <p className="font-medium text-gray-900">Professional</p>
                        <p className="text-sm text-gray-600">Formal, business-focused communication</p>
                      </div>
                    </label>

                    <label className="flex items-center p-4 border-2 border-gray-200 rounded-lg cursor-pointer hover:border-orange-300 transition-colors">
                      <input
                        type="radio"
                        name="tone"
                        value="friendly"
                        checked={brandTone === 'friendly'}
                        onChange={(e) => setBrandTone(e.target.value)}
                        className="w-4 h-4 text-orange-600 focus:ring-orange-500"
                      />
                      <div className="ml-3">
                        <p className="font-medium text-gray-900">Friendly</p>
                        <p className="text-sm text-gray-600">Warm, approachable, and conversational</p>
                      </div>
                    </label>

                    <label className="flex items-center p-4 border-2 border-gray-200 rounded-lg cursor-pointer hover:border-orange-300 transition-colors">
                      <input
                        type="radio"
                        name="tone"
                        value="persuasive"
                        checked={brandTone === 'persuasive'}
                        onChange={(e) => setBrandTone(e.target.value)}
                        className="w-4 h-4 text-orange-600 focus:ring-orange-500"
                      />
                      <div className="ml-3">
                        <p className="font-medium text-gray-900">Persuasive</p>
                        <p className="text-sm text-gray-600">Compelling, action-oriented messaging</p>
                      </div>
                    </label>
                  </div>
                </div>

                <button
                  onClick={handleSave}
                  className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-3 px-6 rounded-lg transition-colors flex items-center justify-center gap-2"
                >
                  <Save className="w-5 h-5" />
                  Save Changes
                </button>
              </div>
            </div>

            {/* HubSpot Connection */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
              <h2 className="text-xl font-semibold text-gray-900 mb-4">HubSpot Connection</h2>
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium text-gray-900">company@example.com</p>
                  <div className="flex items-center gap-2 text-sm text-green-600 mt-1">
                    <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                    Connected
                  </div>
                </div>
                <button className="flex items-center gap-2 text-orange-600 hover:text-orange-700 font-medium transition-colors">
                  <RefreshCw className="w-5 h-5" />
                  Reconnect
                </button>
              </div>
            </div>

            {/* Account Actions */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
              <h2 className="text-xl font-semibold text-gray-900 mb-4">Account Actions</h2>
              <div className="space-y-3">
                <Link
                  to="/login"
                  className="w-full flex items-center justify-center gap-2 py-3 px-6 border-2 border-gray-300 rounded-lg hover:border-gray-400 transition-colors font-medium text-gray-700"
                >
                  <LogOut className="w-5 h-5" />
                  Logout
                </Link>

                <button
                  onClick={handleDeleteAccount}
                  className={`w-full flex items-center justify-center gap-2 py-3 px-6 rounded-lg transition-colors font-medium ${
                    showDeleteConfirm
                      ? 'bg-red-600 hover:bg-red-700 text-white'
                      : 'border-2 border-red-200 text-red-600 hover:border-red-300 hover:bg-red-50'
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
