import { Link } from 'react-router-dom';
import { ArrowRight, Check, Zap, Sparkles, MessageSquare, Star, TrendingUp, Clock } from 'lucide-react';

export default function Homepage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 via-white to-gray-50">
      {/* Header */}
      <header className="w-full px-[var(--page-padding)] py-6 flex justify-between items-center sticky top-0 bg-white/80 backdrop-blur-md z-50 rounded-b-2xl shadow-sm">
        <Link to="/" className="flex items-center gap-2 group">
          <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg group-hover:scale-105 transition-transform">
            <Sparkles className="w-6 h-6 text-white" />
          </div>
          <span className="text-xl font-bold text-gray-900">HubSpot AI Writer</span>
        </Link>
        <Link to="/login" className="text-orange-600 hover:text-orange-700 font-semibold px-6 py-2 rounded-lg hover:bg-orange-50 transition-all">
          Sign In
        </Link>
      </header>

      {/* Hero Section */}
      <section className="w-full px-[var(--page-padding)] py-24 text-center relative overflow-hidden" aria-label="Introduction">
        {/* Animated background elements */}
        <div className="absolute inset-0 -z-10">
          <div className="absolute top-20 left-10 w-72 h-72 bg-orange-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob"></div>
          <div className="absolute top-40 right-10 w-72 h-72 bg-yellow-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-2000"></div>
          <div className="absolute -bottom-8 left-1/2 w-72 h-72 bg-pink-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-4000"></div>
        </div>
        
        {/* Trust Badge */}
        <div className="inline-flex items-center gap-2 bg-orange-100 text-orange-700 px-4 py-2 rounded-full text-sm font-medium mb-6 animate-fade-in">
          <Star className="w-4 h-4 fill-current" />
          Trusted by 1,000+ sales teams
        </div>
        
        <h1 className="text-5xl md:text-7xl font-bold text-gray-900 mb-6 leading-tight animate-fade-in-up">
          Create HubSpot CRM copy<br />
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 to-orange-500">in minutes with AI</span>
        </h1>
        <p className="text-xl md:text-2xl text-gray-600 mb-10 max-w-3xl mx-auto leading-relaxed animate-fade-in-up animation-delay-200">
          No prompts, no hassle. Just powerful AI-generated copy that goes straight into your HubSpot CRM.
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center animate-fade-in-up animation-delay-400">
          <Link
            to="/login"
            className="bg-gradient-to-r from-orange-600 to-orange-500 hover:from-orange-700 hover:to-orange-600 text-white px-10 py-5 rounded-xl font-semibold flex items-center justify-center gap-2 transition-all shadow-xl hover:shadow-2xl hover:scale-105 transform"
          >
            Connect HubSpot
            <ArrowRight className="w-5 h-5" />
          </Link>
          <Link
            to="/help"
            className="bg-white hover:bg-gray-50 text-gray-900 px-10 py-5 rounded-xl font-semibold border-2 border-gray-200 hover:border-orange-300 transition-all shadow-lg hover:shadow-xl"
          >
            See how it works
          </Link>
        </div>
        
        {/* Stats */}
        <div className="grid grid-cols-3 gap-8 mt-20 animate-fade-in-up animation-delay-600">
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">10k+</div>
            <div className="text-gray-600">Emails Generated</div>
          </div>
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">95%</div>
            <div className="text-gray-600">Time Saved</div>
          </div>
          <div className="text-center">
            <div className="text-4xl font-bold text-orange-600 mb-2">4.9★</div>
            <div className="text-gray-600">User Rating</div>
          </div>
        </div>
      </section>

      {/* How It Works */}
      <section className="w-full px-[var(--page-padding)] py-24">
        <div className="text-center mb-16">
          <h2 className="text-4xl md:text-5xl font-bold text-gray-900 mb-4">How It Works</h2>
          <p className="text-xl text-gray-600 max-w-2xl mx-auto">Get started in three simple steps</p>
        </div>
        <div className="grid md:grid-cols-3 gap-8">
          {[
            { step: '1', title: 'Connect HubSpot', desc: 'One-click integration with your HubSpot account', icon: '🔗' },
            { step: '2', title: 'Choose what to write', desc: 'Select from emails, notes, or workflow messages', icon: '✍️' },
            { step: '3', title: 'Send to HubSpot', desc: 'AI-generated copy appears directly in your CRM', icon: '🚀' }
          ].map((item, idx) => (
            <div key={idx} className="bg-white p-8 rounded-2xl shadow-lg border border-gray-100 hover:shadow-2xl transition-all hover:-translate-y-2 transform group">
              <div className="w-16 h-16 bg-gradient-to-br from-orange-600 to-orange-500 rounded-2xl flex items-center justify-center mb-6 shadow-lg group-hover:scale-110 transition-transform">
                <span className="text-3xl">{item.icon}</span>
              </div>
              <div className="flex items-center gap-3 mb-3">
                <span className="w-8 h-8 bg-orange-100 text-orange-600 rounded-full flex items-center justify-center font-bold">{item.step}</span>
                <h3 className="text-xl font-bold text-gray-900">{item.title}</h3>
              </div>
              <p className="text-gray-600 leading-relaxed">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* What You Can Create */}
      <section className="bg-gradient-to-b from-gray-50 to-white py-24">
        <div className="w-full px-[var(--page-padding)]">
          <div className="text-center mb-16">
            <h2 className="text-4xl md:text-5xl font-bold text-gray-900 mb-4">What You Can Create</h2>
            <p className="text-xl text-gray-600 max-w-2xl mx-auto">Everything you need to power your sales process</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[
              { icon: MessageSquare, title: 'Sales emails', desc: 'Personalized outreach that converts', color: 'from-blue-500 to-blue-600' },
              { icon: MessageSquare, title: 'Follow-ups', desc: 'Timely reminders that get responses', color: 'from-green-500 to-green-600' },
              { icon: Zap, title: 'Workflow messages', desc: 'Automated sequences that nurture', color: 'from-purple-500 to-purple-600' },
              { icon: MessageSquare, title: 'CRM notes', desc: 'Clear documentation for your team', color: 'from-pink-500 to-pink-600' },
              { icon: MessageSquare, title: 'Deal messages', desc: 'Updates that move deals forward', color: 'from-orange-500 to-orange-600' },
              { icon: TrendingUp, title: 'Performance reports', desc: 'Data-driven insights and summaries', color: 'from-indigo-500 to-indigo-600' },
            ].map((item, idx) => (
              <div key={idx} className="bg-white p-6 rounded-2xl border border-gray-200 hover:shadow-xl transition-all group hover:-translate-y-1">
                <div className={`w-14 h-14 bg-gradient-to-br ${item.color} rounded-xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform shadow-lg`}>
                  <item.icon className="w-7 h-7 text-white" />
                </div>
                <h3 className="font-bold text-lg mb-2 text-gray-900">{item.title}</h3>
                <p className="text-gray-600 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Why This Tool */}
      <section className="w-full px-[var(--page-padding)] py-24">
        <div className="text-center mb-16">
          <h2 className="text-4xl md:text-5xl font-bold text-gray-900 mb-4">Why Choose Us</h2>
          <p className="text-xl text-gray-600 max-w-2xl mx-auto">Built for busy sales teams who need results fast</p>
        </div>
        <div className="grid md:grid-cols-3 gap-8">
          {[
            { icon: Clock, title: 'Save time', desc: 'Generate professional copy in seconds, not hours', stat: '10x faster' },
            { icon: Check, title: 'No prompts', desc: 'Skip the prompt engineering. Just select and generate', stat: 'Zero setup' },
            { icon: Sparkles, title: 'HubSpot-ready', desc: 'Copy flows directly into your CRM workflows', stat: 'One-click sync' }
          ].map((item, idx) => (
            <div key={idx} className="text-center group">
              <div className="w-20 h-20 bg-gradient-to-br from-orange-100 to-orange-50 rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-lg group-hover:shadow-xl group-hover:scale-110 transition-all">
                <item.icon className="w-10 h-10 text-orange-600" />
              </div>
              <div className="inline-block bg-orange-100 text-orange-700 px-3 py-1 rounded-full text-sm font-semibold mb-3">{item.stat}</div>
              <h3 className="text-2xl font-bold mb-3 text-gray-900">{item.title}</h3>
              <p className="text-gray-600 leading-relaxed">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Final CTA */}
      <section className="bg-gradient-to-r from-orange-600 via-orange-500 to-orange-600 py-20 relative overflow-hidden" aria-label="Call to action">
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAiIGhlaWdodD0iNjAiIHZpZXdCb3g9IjAgMCA2MCA2MCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZyBmaWxsPSJub25lIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiPjxwYXRoIGQ9Ik0zNiAxOGMzLjMxNCAwIDYgMi42ODYgNiA2cy0yLjY4NiA2LTYgNi02LTIuNjg2LTYtNiAyLjY4Ni02IDYtNnptMCA0YzEuMTA1IDAgMiAuODk1IDIgMnMtLjg5NSAyLTIgMi0yLS44OTUtMi0yIC44OTUtMiAyLTJ6IiBmaWxsPSIjZmZmIiBmaWxsLW9wYWNpdHk9Ii4xIi8+PC9nPjwvc3ZnPg==')] opacity-10"></div>
        <div className="w-full px-[var(--page-padding)] text-center relative z-10">
          <h2 className="text-4xl md:text-5xl font-bold text-white mb-6">Start writing in minutes</h2>
          <p className="text-orange-100 mb-10 text-xl max-w-2xl mx-auto">Join 1,000+ teams already using AI to power their HubSpot CRM</p>
          <Link
            to="/login"
            className="bg-white hover:bg-gray-50 text-orange-600 px-12 py-5 rounded-xl font-bold text-lg inline-flex items-center gap-2 transition-all shadow-2xl hover:shadow-[0_35px_60px_-15px_rgba(0,0,0,0.3)] hover:scale-105 transform"
          >
            Get Started Free
            <ArrowRight className="w-6 h-6" />
          </Link>
          <p className="text-orange-100 mt-6 text-sm">No credit card required • Free forever plan</p>
        </div>
      </section>

      {/* Footer */}
      <footer className="bg-gray-900 text-gray-400 py-16" role="contentinfo">
        <div className="w-full px-[var(--page-padding)]">
          <div className="grid md:grid-cols-4 gap-8 mb-12">
            <div className="md:col-span-2">
              <div className="flex items-center gap-2 mb-4">
                <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg">
                  <Sparkles className="w-6 h-6 text-white" />
                </div>
                <span className="text-xl font-bold text-white">HubSpot AI Writer</span>
              </div>
              <p className="text-gray-400 max-w-md">Generate professional HubSpot CRM copy in seconds with AI. No prompts, no hassle.</p>
            </div>
            <div>
              <h4 className="font-semibold text-white mb-4">Product</h4>
              <div className="space-y-2">
                <Link to="/help" className="block hover:text-white transition-colors">How it works</Link>
                <Link to="/templates" className="block hover:text-white transition-colors">Templates</Link>
                <Link to="/dashboard" className="block hover:text-white transition-colors">Dashboard</Link>
              </div>
            </div>
            <div>
              <h4 className="font-semibold text-white mb-4">Legal</h4>
              <div className="space-y-2">
                <Link to="/privacy" className="block hover:text-white transition-colors">Privacy Policy</Link>
                <Link to="/terms" className="block hover:text-white transition-colors">Terms of Service</Link>
                <Link to="/help" className="block hover:text-white transition-colors">Help Center</Link>
              </div>
            </div>
          </div>
          <div className="border-t border-gray-800 pt-8 flex flex-col md:flex-row justify-between items-center gap-4">
            <p>&copy; 2026 HubSpot AI Writer. All rights reserved.</p>
            <div className="flex gap-6" role="navigation" aria-label="Social links">
              <a href="https://twitter.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors">Twitter</a>
              <a href="https://linkedin.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors">LinkedIn</a>
              <a href="https://github.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors">GitHub</a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}