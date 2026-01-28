import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CheckCircle, Sparkles } from 'lucide-react';

export default function HubSpotConnection() {
  const [isConnected, setIsConnected] = useState(false);
  const navigate = useNavigate();

  const handleConnect = () => {
    // Simulate connection
    setIsConnected(true);
  };

  const handleContinue = () => {
    navigate('/onboarding');
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 to-white flex flex-col items-center justify-center px-[var(--page-padding)] py-12">
      <div className="w-full max-w-lg">
        {!isConnected ? (
          // Connection Screen
          <div className="text-center">
            <div className="w-20 h-20 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-6">
              <Sparkles className="w-12 h-12 text-white" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900 mb-4">Connect HubSpot</h1>
            <p className="text-gray-600 mb-8 max-w-md mx-auto">
              We need access to your HubSpot account to create and manage CRM copy. This allows us to:
            </p>

            <div className="bg-white rounded-xl shadow-lg border border-gray-200 p-8 mb-8 text-left">
              <ul className="space-y-4">
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-green-500 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="font-medium text-gray-900">Read your contacts and deals</p>
                    <p className="text-sm text-gray-600">To personalize AI-generated copy</p>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-green-500 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="font-medium text-gray-900">Create emails and notes</p>
                    <p className="text-sm text-gray-600">To save generated content to your CRM</p>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <CheckCircle className="w-6 h-6 text-green-500 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="font-medium text-gray-900">Update workflow messages</p>
                    <p className="text-sm text-gray-600">To integrate with your automations</p>
                  </div>
                </li>
              </ul>
            </div>

            <button
              onClick={handleConnect}
              className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-4 px-6 rounded-lg transition-colors mb-4"
            >
              Connect HubSpot
            </button>

            <Link to="/dashboard" className="block text-gray-500 hover:text-gray-700 text-sm">
              Skip for now
            </Link>
          </div>
        ) : (
          // Success Screen
          <div className="text-center">
            <div className="w-20 h-20 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6">
              <CheckCircle className="w-12 h-12 text-green-600" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900 mb-4">Successfully Connected!</h1>
            <p className="text-gray-600 mb-8">
              Your HubSpot account is now connected. You're ready to start generating AI-powered copy.
            </p>

            <div className="bg-white rounded-xl shadow-lg border border-gray-200 p-8 mb-8">
              <div className="text-left">
                <p className="text-sm text-gray-600 mb-2">Connected Account</p>
                <p className="font-semibold text-gray-900 text-lg mb-4">company@example.com</p>
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                  Active Connection
                </div>
              </div>
            </div>

            <button
              onClick={handleContinue}
              className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-4 px-6 rounded-lg transition-colors"
            >
              Continue
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
