import { Link } from 'react-router-dom';
import { HelpCircle, Shield, Mail } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Help() {
  return (
    <div className="min-h-screen flex flex-col bg-gradient-calm">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        {/* Enhanced Header Section with Dark Decorative Elements */}
        <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
          {/* Decorative blur elements */}
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
            <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
            <div className="absolute inset-0 opacity-5" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")` }} />
          </div>
          
          <div className="relative px-6 lg:px-8 py-8 lg:py-10">
            <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
              <div className="flex items-center gap-5">
                <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                  <HelpCircle className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">How It Works</h1>
                  <p className="text-slate-400 mt-1">A quick guide to getting the most out of Cadence</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="w-full">

          <nav className="mb-12 glass-strong rounded-2xl p-6" aria-label="Table of contents">
            <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-3">On this page</h2>
            <ul className="space-y-2">
              <li><a href="#what-heading" className="text-teal-600 hover:text-teal-700 font-medium">What Cadence does</a></li>
              <li><a href="#generate-heading" className="text-teal-600 hover:text-teal-700 font-medium">Generating copy (4 steps)</a></li>
              <li><a href="#crm-heading" className="text-teal-600 hover:text-teal-700 font-medium">Your data in Cadence</a></li>
              <li><a href="#privacy-help-heading" className="text-teal-600 hover:text-teal-700 font-medium">Data & privacy</a></li>
              <li><a href="#support-heading" className="text-teal-600 hover:text-teal-700 font-medium">Contact support</a></li>
            </ul>
          </nav>

          {/* What Cadence does */}
          <section className="mb-12" aria-labelledby="what-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-teal-100 rounded-lg flex items-center justify-center">
                <HelpCircle className="w-6 h-6 text-teal-600" />
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
            <h2 id="generate-heading" className="text-2xl font-bold text-slate-900 mb-6">Generating Copy (4 Steps)</h2>
            <div className="space-y-6">
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

          {/* Your data in Cadence */}
          <section className="mb-12">
            <h2 id="crm-heading" className="text-2xl font-bold text-slate-900 mb-6">Your Data in Cadence</h2>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                <strong>Cadence is the only CRM you need.</strong> All your data — contacts, deals, leads, copy history — stays in Cadence. No external CRM is required.
              </p>
              <p className="text-slate-700">
                When you use “Send to CRM” in the app, you’re saving copy to a contact or deal <strong>in Cadence</strong> — nothing is sent to an external system.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="privacy-help-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-teal-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Shield className="w-6 h-6 text-teal-600" />
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
              <Link to="/privacy" className="text-teal-600 hover:text-teal-700 font-medium focus-visible:underline">
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
            <div className="bg-gradient-to-r from-teal-600 to-teal-500 rounded-2xl shadow-lg shadow-teal-200/50 p-8 text-white">
              <p className="text-lg mb-4">Something unclear or broken? We’re here to help.</p>
              <a href="mailto:support@example.com" className="inline-block bg-white text-teal-600 hover:bg-slate-50 px-6 py-3 rounded-2xl font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-teal-500">
                Contact Support
              </a>
            </div>
          </section>
        </div>
      </main>
    </div>
  );
}
