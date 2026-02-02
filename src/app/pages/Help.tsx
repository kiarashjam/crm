import { Link } from 'react-router-dom';
import { HelpCircle, Link2, Shield, Mail, FileText, Target, Sparkles, Send } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Help() {
  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-12 text-center">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">How It Works</h1>
            <p className="text-xl text-slate-600">A quick guide to getting the most out of Cadence</p>
          </div>

          <nav className="mb-12 bg-white rounded-2xl border border-slate-200 shadow-sm p-6" aria-label="Table of contents">
            <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-3">On this page</h2>
            <ul className="space-y-2">
              <li><a href="#what-heading" className="text-orange-600 hover:text-orange-700 font-medium">What Cadence does</a></li>
              <li><a href="#generate-heading" className="text-orange-600 hover:text-orange-700 font-medium">Generating copy (4 steps)</a></li>
              <li><a href="#crm-heading" className="text-orange-600 hover:text-orange-700 font-medium">CRM connection</a></li>
              <li><a href="#privacy-help-heading" className="text-orange-600 hover:text-orange-700 font-medium">Data & privacy</a></li>
              <li><a href="#support-heading" className="text-orange-600 hover:text-orange-700 font-medium">Contact support</a></li>
            </ul>
          </nav>

          {/* What Cadence does */}
          <section className="mb-12" aria-labelledby="what-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <HelpCircle className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="what-heading" className="text-2xl font-bold text-slate-900">What Cadence Does</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                Cadence drafts sales copy for you—emails, follow-ups, CRM notes, deal messages—in your brand voice. You pick the type and goal; we generate the text. No blank screen, no hopping between apps.
              </p>
              <p className="text-slate-700">
                Everything stays inside Cadence until you send it. When you’re ready, you push the copy to the right contact or deal in your CRM. Fast, consistent, and on-brand.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="generate-heading">
            <div className="text-center mb-12">
              <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Process</span>
              <h2 id="generate-heading" className="text-3xl md:text-4xl font-bold text-slate-900 mb-4">Generating Copy (4 Steps)</h2>
              <p className="text-lg text-slate-600 max-w-2xl mx-auto">Pick what you need, set your goal, generate, and send. Everything stays in one flow.</p>
            </div>
            <div className="relative grid sm:grid-cols-2 lg:grid-cols-4 gap-6 md:gap-4">
              <div className="hidden lg:block absolute top-20 left-[10%] right-[10%] h-0.5 bg-gradient-to-r from-orange-200 via-orange-300 to-orange-200 rounded-full" aria-hidden="true" />
              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    1
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Pick what you’re writing</h3>
                    <p className="text-slate-700">Sales email, follow-up, CRM note, deal message, or workflow copy. You can also start from a template.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    2
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Set the goal</h3>
                    <p className="text-slate-700">Choose an outcome—e.g. schedule a meeting, follow up after a demo, close the deal—and add any extra context. We use your company name and brand tone from Settings.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    3
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Generate and tweak</h3>
                    <p className="text-slate-700">Hit generate, then review the draft. Use the controls to make it shorter, friendlier, or more persuasive—then copy it or send it on.</p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
                <div className="flex items-start gap-4">
                  <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                    4
                  </div>
                  <div>
                    <h3 className="font-semibold text-lg text-slate-900 mb-2">Send to your CRM</h3>
                    <p className="text-slate-700">Select the contact or deal, confirm, and the copy is sent. It’s stored in your copy history so you can reuse or reference it later.</p>
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
              <h2 id="crm-heading" className="text-2xl font-bold text-slate-900">CRM Connection</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                When you connect your CRM, you’re taken to your CRM’s own authorization screen. You review and approve what Cadence can do. We use standard OAuth—we never see or store your CRM password.
              </p>
              <p className="text-slate-700 mb-2 font-medium">We ask for permission to:</p>
              <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                <li>Read contacts and deals (so we can personalize and target copy)</li>
                <li>Create or update emails and notes (so we can send generated copy into the right record)</li>
                <li>Update workflow messages (for automation and sequences)</li>
              </ul>
              <p className="text-slate-700">
                You can disconnect anytime from Settings. Your CRM credentials are never stored by Cadence.
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
                We only use what’s needed to generate and send copy. Here’s the short version:
              </p>
              <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                <li>Access is limited to the data required for copy and sending</li>
                <li>Data is encrypted in transit and at rest</li>
                <li>We don’t sell or share your data</li>
                <li>You can delete your account and data whenever you want</li>
                <li>We follow GDPR, CCPA, and similar privacy rules</li>
              </ul>
              <Link to="/privacy" className="text-orange-600 hover:text-orange-700 font-medium focus-visible:underline">
                Full Privacy Policy →
              </Link>
            </div>
          </section>

          <section aria-labelledby="support-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Mail className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="support-heading" className="text-2xl font-bold text-slate-900">Contact Support</h2>
            </div>
            <div className="bg-gradient-to-r from-orange-600 to-orange-500 rounded-2xl shadow-sm border border-orange-200 p-8 text-white">
              <p className="text-lg mb-4">Something unclear or broken? We’re here to help.</p>
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
