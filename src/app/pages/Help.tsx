import { Link } from 'react-router-dom';
import { HelpCircle, Link2, Shield, Mail } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function Help() {
  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="mb-12 text-center">
            <h1 className="text-4xl font-bold text-gray-900 mb-4">How It Works</h1>
            <p className="text-xl text-gray-600">Everything you need to know about using HubSpot AI Writer</p>
          </div>

          {/* What This Tool Does */}
          <section className="mb-12">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <HelpCircle className="w-6 h-6 text-orange-600" />
              </div>
              <h2 className="text-2xl font-bold text-gray-900">What This Tool Does</h2>
            </div>
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8">
              <p className="text-gray-700 mb-4">
                HubSpot AI Writer is a powerful tool that generates professional copy for your HubSpot CRM in seconds. It uses advanced AI to create personalized emails, follow-ups, workflow messages, and CRM notes that match your brand voice.
              </p>
              <p className="text-gray-700">
                No more staring at a blank screen or worrying about what to write. Just select what you need, and we'll generate it for you—ready to send directly to your HubSpot account.
              </p>
            </div>
          </section>

          {/* How to Generate Copy */}
          <section className="mb-12">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">How to Generate Copy</h2>
            <div className="space-y-6">
              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    1
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-gray-900 mb-2">Choose what to write</h3>
                    <p className="text-gray-700">Select from sales emails, follow-ups, CRM notes, deal messages, or workflow content.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    2
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-gray-900 mb-2">Set your goal</h3>
                    <p className="text-gray-700">Pick your objective (schedule meeting, follow up, close deal, etc.) and add any specific context.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    3
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-gray-900 mb-2">Generate and refine</h3>
                    <p className="text-gray-700">Click generate, review the AI-created copy, and use adjustment buttons to make it shorter, friendlier, or more persuasive.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    4
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-gray-900 mb-2">Send to HubSpot</h3>
                    <p className="text-gray-700">Choose which contact, deal, or workflow to send to, and your copy appears instantly in your CRM.</p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* HubSpot Connection */}
          <section className="mb-12">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <Link2 className="w-6 h-6 text-orange-600" />
              </div>
              <h2 className="text-2xl font-bold text-gray-900">How HubSpot Connection Works</h2>
            </div>
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8">
              <p className="text-gray-700 mb-4">
                We use HubSpot's official OAuth integration to securely connect to your account. When you click "Connect HubSpot," you'll be redirected to HubSpot's authorization page where you can review and approve the permissions.
              </p>
              <p className="text-gray-700 mb-4">
                <strong>We request access to:</strong>
              </p>
              <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                <li>Read your contacts and deals (to personalize copy)</li>
                <li>Create emails and notes (to save generated content)</li>
                <li>Update workflow messages (for automation integration)</li>
              </ul>
              <p className="text-gray-700">
                You can disconnect at any time from your Settings page. We never store your HubSpot credentials.
              </p>
            </div>
          </section>

          {/* Data & Privacy */}
          <section className="mb-12">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <Shield className="w-6 h-6 text-orange-600" />
              </div>
              <h2 className="text-2xl font-bold text-gray-900">Data & Privacy</h2>
            </div>
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8">
              <p className="text-gray-700 mb-4">
                Your privacy and data security are our top priorities. Here's what you should know:
              </p>
              <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                <li>We only access the HubSpot data necessary to generate copy</li>
                <li>All data is encrypted in transit and at rest</li>
                <li>We don't sell or share your data with third parties</li>
                <li>You can delete your account and all associated data at any time</li>
                <li>We comply with GDPR, CCPA, and other privacy regulations</li>
              </ul>
              <Link to="/privacy" className="text-orange-600 hover:text-orange-700 font-medium">
                Read our full Privacy Policy →
              </Link>
            </div>
          </section>

          {/* Contact Support */}
          <section>
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <Mail className="w-6 h-6 text-orange-600" />
              </div>
              <h2 className="text-2xl font-bold text-gray-900">Need More Help?</h2>
            </div>
            <div className="bg-gradient-to-r from-orange-600 to-orange-700 rounded-xl shadow-lg p-8 text-white">
              <p className="text-lg mb-4">Can't find what you're looking for? Our support team is here to help!</p>
              <button className="bg-white text-orange-600 hover:bg-orange-50 px-6 py-3 rounded-lg font-semibold transition-colors">
                Contact Support
              </button>
            </div>
          </section>
        </div>
      </main>
    </div>
  );
}
