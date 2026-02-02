import { Link } from 'react-router-dom';
import { HelpCircle, Link2, Shield, Mail } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Help() {
  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-12 text-center">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">How It Works</h1>
            <p className="text-xl text-slate-600">Everything you need to know about using ACI</p>
          </div>

          <nav className="mb-12 bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-label="Table of contents">
            <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-3">On this page</h2>
            <ul className="space-y-2">
              <li><a href="#what-heading" className="text-orange-600 hover:text-orange-700 font-medium">What This Tool Does</a></li>
              <li><a href="#generate-heading" className="text-orange-600 hover:text-orange-700 font-medium">How to Generate Copy</a></li>
              <li><a href="#crm-heading" className="text-orange-600 hover:text-orange-700 font-medium">How CRM Connection Works</a></li>
              <li><a href="#privacy-help-heading" className="text-orange-600 hover:text-orange-700 font-medium">Data & Privacy</a></li>
              <li><a href="#support-heading" className="text-orange-600 hover:text-orange-700 font-medium">Need More Help?</a></li>
            </ul>
          </nav>

          {/* What This Tool Does */}
          <section className="mb-12" aria-labelledby="what-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <HelpCircle className="w-6 h-6 text-orange-600" />
              </div>
              <h2 className="text-2xl font-bold text-slate-900">What This Tool Does</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                ACI is a powerful tool that generates professional copy for your CRM in seconds. It uses advanced AI to create personalized emails, follow-ups, workflow messages, and CRM notes that match your brand voice.
              </p>
              <p className="text-slate-700">
                No more staring at a blank screen or worrying about what to write. Just select what you need, and we'll generate it for you—ready to send directly to your CRM.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="generate-heading">
            <h2 id="generate-heading" className="text-2xl font-bold text-slate-900 mb-6">How to Generate Copy</h2>
            <div className="space-y-6">
              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    1
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Choose what to write</h3>
                    <p className="text-slate-700">Select from sales emails, follow-ups, CRM notes, deal messages, or workflow content.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    2
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Set your goal</h3>
                    <p className="text-slate-700">Pick your objective (schedule meeting, follow up, close deal, etc.) and add any specific context.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    3
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Generate and refine</h3>
                    <p className="text-slate-700">Click generate, review the AI-created copy, and use adjustment buttons to make it shorter, friendlier, or more persuasive.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    4
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Send to CRM</h3>
                    <p className="text-slate-700">Choose which contact, deal, or workflow to send to, and your copy appears instantly in your CRM.</p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* CRM Connection */}
          <section className="mb-12">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <Link2 className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="crm-heading" className="text-2xl font-bold text-slate-900">How CRM Connection Works</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                We use secure OAuth integration to connect to your CRM. When you click "Connect," you'll be redirected to your CRM's authorization page where you can review and approve the permissions.
              </p>
              <p className="text-slate-700 mb-4">
                <strong>We request access to:</strong>
              </p>
              <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                <li>Read your contacts and deals (to personalize copy)</li>
                <li>Create emails and notes (to save generated content)</li>
                <li>Update workflow messages (for automation integration)</li>
              </ul>
              <p className="text-slate-700">
                You can disconnect at any time from your Settings page. We never store your CRM credentials.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="privacy-help-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Shield className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="privacy-help-heading" className="text-2xl font-bold text-slate-900">Data & Privacy</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                Your privacy and data security are our top priorities. Here's what you should know:
              </p>
              <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                <li>We only access the CRM data necessary to generate copy</li>
                <li>All data is encrypted in transit and at rest</li>
                <li>We don't sell or share your data with third parties</li>
                <li>You can delete your account and all associated data at any time</li>
                <li>We comply with GDPR, CCPA, and other privacy regulations</li>
              </ul>
              <Link to="/privacy" className="text-orange-600 hover:text-orange-700 font-medium focus-visible:underline">
                Read our full Privacy Policy →
              </Link>
            </div>
          </section>

          <section aria-labelledby="support-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Mail className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="support-heading" className="text-2xl font-bold text-slate-900">Need More Help?</h2>
            </div>
            <div className="bg-gradient-to-r from-orange-600 to-orange-500 rounded-2xl shadow-sm border border-orange-200 p-8 text-white">
              <p className="text-lg mb-4">Can't find what you're looking for? Our support team is here to help!</p>
              <a href="mailto:support@example.com" className="inline-block bg-white text-orange-600 hover:bg-slate-50 px-6 py-3 rounded-2xl font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-orange-500">
                Contact Support
              </a>
            </div>
          </section>
        </div>
      </main>
    </div>
  );
}
