import { Link, useNavigate } from 'react-router-dom';
import { Mail, ArrowLeft, Sparkles, Play } from 'lucide-react';
import { setDemoUser } from '@/app/lib/auth';

const DEMO_USER = { name: 'Demo User', email: 'demo@example.com' };

export default function Login() {
  const navigate = useNavigate();

  const handleDemoLogin = () => {
    setDemoUser(DEMO_USER);
    navigate('/dashboard', { replace: true });
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 to-white flex flex-col">
      {/* Header */}
      <header className="w-full px-[var(--page-padding)] py-6">
        <Link to="/" className="flex items-center gap-2 text-gray-600 hover:text-gray-900 transition-colors">
          <ArrowLeft className="w-5 h-5" />
          Back to home
        </Link>
      </header>

      {/* Login Form */}
      <div className="flex-1 flex items-center justify-center px-[var(--page-padding)] py-12">
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div className="w-16 h-16 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <Sparkles className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Welcome back</h1>
            <p className="text-gray-600">Sign in to start creating AI-powered copy</p>
          </div>

          <div className="bg-white rounded-2xl shadow-lg border border-gray-200 p-8">
            {/* Google Login */}
            <button className="w-full bg-white hover:bg-gray-50 border-2 border-gray-300 text-gray-700 font-semibold py-4 px-6 rounded-lg flex items-center justify-center gap-3 mb-4 transition-colors">
              <svg className="w-5 h-5" viewBox="0 0 24 24">
                <path
                  fill="#4285F4"
                  d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
                />
                <path
                  fill="#34A853"
                  d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                />
                <path
                  fill="#FBBC05"
                  d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
                />
                <path
                  fill="#EA4335"
                  d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
                />
              </svg>
              Login with Google
            </button>

            {/* Demo Login */}
            <button
              onClick={handleDemoLogin}
              className="w-full bg-gradient-to-r from-emerald-600 to-emerald-500 hover:from-emerald-700 hover:to-emerald-600 text-white font-semibold py-4 px-6 rounded-lg flex items-center justify-center gap-3 transition-all shadow-lg hover:shadow-xl mb-4"
            >
              <Play className="w-5 h-5" />
              Try demo (instant access)
            </button>

            {/* Divider */}
            <div className="flex items-center gap-4 my-6">
              <div className="flex-1 border-t border-gray-300"></div>
              <span className="text-gray-500 text-sm">or</span>
              <div className="flex-1 border-t border-gray-300"></div>
            </div>

            {/* Email Login */}
            <button className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-4 px-6 rounded-lg flex items-center justify-center gap-3 transition-colors">
              <Mail className="w-5 h-5" />
              Login with Email
            </button>

            {/* Info Text */}
            <p className="text-center text-sm text-gray-500 mt-6">
              Connect HubSpot in one click after signing in
            </p>

            {/* Links */}
            <div className="flex justify-center gap-4 mt-6 text-sm">
              <Link to="/privacy" className="text-orange-600 hover:text-orange-700">
                Privacy Policy
              </Link>
              <span className="text-gray-300">•</span>
              <Link to="/terms" className="text-orange-600 hover:text-orange-700">
                Terms of Service
              </Link>
            </div>
          </div>

          {/* Sign Up Link */}
          <p className="text-center text-gray-600 mt-6">
            Don't have an account?{' '}
            <Link to="/" className="text-orange-600 hover:text-orange-700 font-semibold underline-offset-2 hover:underline">
              Sign up free
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
