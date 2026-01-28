import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Sparkles } from 'lucide-react';

export default function Onboarding() {
  const [companyName, setCompanyName] = useState('');
  const [brandTone, setBrandTone] = useState('professional');
  const navigate = useNavigate();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // Save settings and navigate to dashboard
    navigate('/dashboard');
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 to-white flex flex-col items-center justify-center px-[var(--page-padding)] py-12">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Sparkles className="w-10 h-10 text-white" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Set your brand tone</h1>
          <p className="text-gray-600">This helps AI generate copy that matches your voice</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl shadow-lg border border-gray-200 p-8">
          {/* Company Name */}
          <div className="mb-6">
            <label htmlFor="company" className="block text-sm font-medium text-gray-700 mb-2">
              Company Name
            </label>
            <input
              type="text"
              id="company"
              value={companyName}
              onChange={(e) => setCompanyName(e.target.value)}
              placeholder="Enter your company name"
              className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition-colors"
              required
            />
          </div>

          {/* Brand Tone */}
          <div className="mb-8">
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

          {/* Submit Button */}
          <button
            type="submit"
            className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-4 px-6 rounded-lg transition-colors"
          >
            Save & Continue
          </button>

          <p className="text-center text-sm text-gray-500 mt-4">
            You can change these settings anytime
          </p>
        </form>
      </div>
    </div>
  );
}
