import { Link } from 'react-router-dom';
import {
  ArrowRight,
  Check,
  Sparkles,
  Star,
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Workflow,
  LayoutTemplate,
  History,
  Send,
  Link2,
  Settings,
  Clock,
} from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Homepage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 via-white to-slate-50">
      <header className="w-full px-[var(--page-padding)] py-6 flex justify-between items-center sticky top-0 bg-white/80 backdrop-blur-md z-50 rounded-b-2xl shadow-sm border-b border-slate-200/50" role="banner">
        <Link to="/" className="flex items-center gap-2 group focus-visible:rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2" aria-label="ACI home">
          <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg group-hover:scale-105 transition-transform" aria-hidden>
            <Sparkles className="w-6 h-6 text-white" />
          </div>
          <span className="text-xl font-bold text-slate-900">ACI</span>
        </Link>
        <Link to="/login" className="text-orange-600 hover:text-orange-700 font-semibold px-6 py-2 rounded-lg hover:bg-orange-50 transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2">
          Sign In
        </Link>
      </header>

      <main id={MAIN_CONTENT_ID} className="w-full" tabIndex={-1}>
      <section className="w-full px-[var(--page-padding)] py-24 text-center relative overflow-hidden" aria-labelledby="hero-heading">
        {/* Animated background elements */}
        <div className="absolute inset-0 -z-10">
          <div className="absolute top-20 left-10 w-72 h-72 bg-orange-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob"></div>
          <div className="absolute top-40 right-10 w-72 h-72 bg-yellow-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-2000"></div>
          <div className="absolute -bottom-8 left-1/2 w-72 h-72 bg-pink-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-4000"></div>
        </div>
        
        {/* Trust Badge */}
        <div className="inline-flex items-center gap-2 bg-orange-100 text-orange-700 px-4 py-2 rounded-full text-sm font-medium mb-6 animate-fade-in">
          <Star className="w-4 h-4 fill-current" />
          AI copy for your CRM
        </div>
        
        <h1 id="hero-heading" className="text-5xl md:text-7xl font-bold text-slate-900 mb-6 leading-tight animate-fade-in-up">
          Write sales copy<br />
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 to-orange-500">in seconds, not hours</span>
        </h1>
        <p className="text-xl md:text-2xl text-slate-600 mb-10 max-w-3xl mx-auto leading-relaxed animate-fade-in-up animation-delay-200">
          Generate emails, follow-ups, CRM notes, and deal messages with AI—then send them straight to your CRM.
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center animate-fade-in-up animation-delay-400">
          <Link
            to="/login"
            className="bg-gradient-to-r from-orange-600 to-orange-500 hover:from-orange-700 hover:to-orange-600 text-white px-10 py-5 rounded-xl font-semibold flex items-center justify-center gap-2 transition-all shadow-xl hover:shadow-2xl hover:scale-105 transform focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
          >
            Get started
            <ArrowRight className="w-5 h-5" aria-hidden />
          </Link>
          <Link
            to="/help"
            className="bg-white hover:bg-slate-50 text-slate-900 px-10 py-5 rounded-xl font-semibold border-2 border-slate-200 hover:border-orange-300 transition-all shadow-lg hover:shadow-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
          >
            See how it works
          </Link>
        </div>
        
        {/* Stats */}
        <div className="grid grid-cols-3 gap-8 mt-20 animate-fade-in-up animation-delay-600">
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">5</div>
            <div className="text-slate-600">Copy types</div>
          </div>
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">Templates</div>
            <div className="text-slate-600">Ready-to-use</div>
          </div>
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">1-click</div>
            <div className="text-slate-600">Send to CRM</div>
          </div>
        </div>
      </section>

      <section className="w-full px-[var(--page-padding)] py-24" aria-labelledby="how-it-works-heading">
        <div className="text-center mb-16">
          <h2 id="how-it-works-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">How It Works</h2>
          <p className="text-xl text-slate-600 max-w-2xl mx-auto">Connect, generate, and send—in three steps</p>
        </div>
        <div className="grid md:grid-cols-3 gap-8">
          {[
            { step: '1', title: 'Connect your CRM', desc: 'Link your CRM once. We keep your connection secure so you can send copy with one click.', icon: '🔗' },
            { step: '2', title: 'Generate copy', desc: 'Pick a type (sales email, follow-up, CRM note, deal message) or a template. Add your goal and context—AI does the rest.', icon: '✨' },
            { step: '3', title: 'Send to CRM', desc: 'Review the copy, then send it to a contact or deal in your CRM. History keeps everything in one place.', icon: '🚀' }
          ].map((item, idx) => (
            <div key={idx} className="bg-white p-8 rounded-2xl shadow-sm border border-slate-200 hover:shadow-lg transition-all hover:-translate-y-1 transform group">
              <div className="w-16 h-16 bg-gradient-to-br from-orange-600 to-orange-500 rounded-2xl flex items-center justify-center mb-6 shadow-md group-hover:scale-105 transition-transform" aria-hidden>
                <span className="text-3xl">{item.icon}</span>
              </div>
              <div className="flex items-center gap-3 mb-3">
                <span className="w-8 h-8 bg-orange-100 text-orange-600 rounded-full flex items-center justify-center font-bold text-sm" aria-hidden>{item.step}</span>
                <h3 className="text-xl font-bold text-slate-900">{item.title}</h3>
              </div>
              <p className="text-slate-600 leading-relaxed">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="bg-gradient-to-b from-slate-50 to-white py-24" aria-labelledby="features-heading">
        <div className="w-full px-[var(--page-padding)] max-w-6xl mx-auto">
          <div className="text-center mb-16">
            <h2 id="features-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">What You Can Do</h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">AI copy types, templates, history, and one-click send to your CRM</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[
              { icon: Mail, title: 'Sales email', desc: 'Personalized outreach that matches your brand', color: 'from-blue-500 to-blue-600' },
              { icon: MessageSquare, title: 'Follow-up', desc: 'Keep conversations going with the right tone', color: 'from-green-500 to-green-600' },
              { icon: FileText, title: 'CRM note', desc: 'Document interactions and next steps', color: 'from-purple-500 to-purple-600' },
              { icon: Briefcase, title: 'Deal message', desc: 'Update stakeholders and move deals forward', color: 'from-pink-500 to-pink-600' },
              { icon: Workflow, title: 'Workflow message', desc: 'Copy for automated sequences', color: 'from-orange-500 to-orange-600' },
              { icon: LayoutTemplate, title: 'Templates', desc: 'Pre-built goals and types for faster creation', color: 'from-indigo-500 to-indigo-600' },
              { icon: History, title: 'History', desc: 'All generated and sent copy in one place', color: 'from-slate-600 to-slate-700' },
              { icon: Send, title: 'Send to CRM', desc: 'One click to attach copy to contacts or deals', color: 'from-emerald-500 to-emerald-600' },
              { icon: Link2, title: 'Connection', desc: 'Connect your CRM and manage the link securely', color: 'from-amber-500 to-amber-600' },
              { icon: Settings, title: 'Settings', desc: 'Company name and brand tone for consistent copy', color: 'from-slate-500 to-slate-600' },
            ].map((item, idx) => (
              <div key={idx} className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm hover:shadow-md transition-all group hover:-translate-y-0.5">
                <div className={`w-14 h-14 bg-gradient-to-br ${item.color} rounded-xl flex items-center justify-center mb-4 group-hover:scale-105 transition-transform shadow-md`} aria-hidden>
                  <item.icon className="w-7 h-7 text-white" />
                </div>
                <h3 className="font-bold text-lg mb-2 text-slate-900">{item.title}</h3>
                <p className="text-slate-600 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="w-full px-[var(--page-padding)] py-24 max-w-6xl mx-auto" aria-labelledby="why-heading">
        <div className="text-center mb-16">
          <h2 id="why-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">Why Choose ACI</h2>
          <p className="text-xl text-slate-600 max-w-2xl mx-auto">Built for sales teams who write a lot of copy</p>
        </div>
        <div className="grid md:grid-cols-3 gap-8">
          {[
            { icon: Clock, title: 'Save time', desc: 'Generate emails, follow-ups, and CRM notes in seconds instead of minutes.', stat: 'Less writing' },
            { icon: Check, title: 'Simple setup', desc: 'Connect your CRM, pick a type or template, and generate. No complex setup.', stat: 'Zero hassle' },
            { icon: Sparkles, title: 'Your brand voice', desc: 'Set your company name and tone so every piece of copy matches your style.', stat: 'Sales-first' }
          ].map((item, idx) => (
            <div key={idx} className="text-center group">
              <div className="w-20 h-20 bg-gradient-to-br from-orange-100 to-orange-50 rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-md group-hover:scale-105 transition-all" aria-hidden>
                <item.icon className="w-10 h-10 text-orange-600" />
              </div>
              <div className="inline-block bg-orange-100 text-orange-700 px-3 py-1 rounded-full text-sm font-semibold mb-3">{item.stat}</div>
              <h3 className="text-2xl font-bold mb-3 text-slate-900">{item.title}</h3>
              <p className="text-slate-600 leading-relaxed">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="bg-gradient-to-r from-orange-600 via-orange-500 to-orange-600 py-20 relative overflow-hidden" aria-labelledby="cta-heading">
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAiIGhlaWdodD0iNjAiIHZpZXdCb3g9IjAgMCA2MCA2MCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZyBmaWxsPSJub25lIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiPjxwYXRoIGQ9Ik0zNiAxOGMzLjMxNCAwIDYgMi42ODYgNiA2cy0yLjY4NiA2LTYgNi02LTIuNjg2LTYtNiAyLjY4Ni02IDYtNnptMCA0YzEuMTA1IDAgMiAuODk1IDIgMnMtLjg5NSAyLTIgMi0yLS44OTUtMi0yIC44OTUtMiAyLTJ6IiBmaWxsPSIjZmZmIiBmaWxsLW9wYWNpdHk9Ii4xIi8+PC9nPjwvc3ZnPg==')] opacity-10" aria-hidden />
        <div className="w-full px-[var(--page-padding)] text-center relative z-10 max-w-4xl mx-auto">
          <h2 id="cta-heading" className="text-4xl md:text-5xl font-bold text-white mb-6">Start writing in minutes</h2>
          <p className="text-orange-100 mb-10 text-xl max-w-2xl mx-auto">Connect your CRM, generate copy, and send—all in one place</p>
          <Link
            to="/login"
            className="bg-white hover:bg-slate-50 text-orange-600 px-12 py-5 rounded-xl font-bold text-lg inline-flex items-center gap-2 transition-all shadow-2xl hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-orange-500"
          >
            Get Started Free
            <ArrowRight className="w-6 h-6" aria-hidden />
          </Link>
          <p className="text-orange-100 mt-6 text-sm">No credit card required • Free forever plan</p>
        </div>
      </section>
      </main>

      <footer className="bg-slate-900 text-slate-400 py-16" role="contentinfo">
        <div className="w-full px-[var(--page-padding)] max-w-6xl mx-auto">
          <div className="grid md:grid-cols-4 gap-8 mb-12">
            <div className="md:col-span-2">
              <div className="flex items-center gap-2 mb-4">
                <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg" aria-hidden>
                  <Sparkles className="w-6 h-6 text-white" />
                </div>
                <span className="text-xl font-bold text-white">ACI</span>
              </div>
              <p className="text-slate-400 max-w-md">AI-powered copy for your CRM. Generate sales emails, follow-ups, and notes—then send straight to your CRM.</p>
            </div>
            <nav aria-label="Product links">
              <h3 className="font-semibold text-white mb-4">Product</h3>
              <ul className="space-y-2">
                <li><Link to="/help" className="hover:text-white transition-colors focus-visible:underline">How it works</Link></li>
                <li><Link to="/templates" className="hover:text-white transition-colors focus-visible:underline">Templates</Link></li>
                <li><Link to="/dashboard" className="hover:text-white transition-colors focus-visible:underline">Dashboard</Link></li>
                <li><Link to="/history" className="hover:text-white transition-colors focus-visible:underline">History</Link></li>
                <li><Link to="/settings" className="hover:text-white transition-colors focus-visible:underline">Settings</Link></li>
                <li><Link to="/connect" className="hover:text-white transition-colors focus-visible:underline">Connect CRM</Link></li>
              </ul>
            </nav>
            <nav aria-label="Legal links">
              <h3 className="font-semibold text-white mb-4">Legal</h3>
              <ul className="space-y-2">
                <li><Link to="/privacy" className="hover:text-white transition-colors focus-visible:underline">Privacy Policy</Link></li>
                <li><Link to="/terms" className="hover:text-white transition-colors focus-visible:underline">Terms of Service</Link></li>
                <li><Link to="/help" className="hover:text-white transition-colors focus-visible:underline">Help Center</Link></li>
              </ul>
            </nav>
          </div>
          <div className="border-t border-slate-800 pt-8 flex flex-col md:flex-row justify-between items-center gap-4">
            <p>&copy; 2026 ACI. All rights reserved.</p>
            <nav className="flex gap-6" aria-label="Social links">
              <a href="https://twitter.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="Twitter (opens in new window)">Twitter</a>
              <a href="https://linkedin.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="LinkedIn (opens in new window)">LinkedIn</a>
              <a href="https://github.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="GitHub (opens in new window)">GitHub</a>
            </nav>
          </div>
        </div>
      </footer>
    </div>
  );
}