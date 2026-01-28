import { Link } from 'react-router-dom'

const howItWorks = [
  { step: 1, title: 'Connect', desc: 'Link your CRM account in one click.' },
  { step: 2, title: 'Choose what to write', desc: 'Pick the type of copy you need.' },
  { step: 3, title: 'Send to CRM', desc: 'Push finished copy straight into your CRM.' },
]

const whatYouCanCreate = [
  'Sales emails',
  'Follow-ups',
  'Workflow messages',
  'CRM notes',
  'Deal messages',
]

const whyThisTool = [
  { title: 'Save time', desc: 'Minutes instead of hours.' },
  { title: 'No prompts', desc: 'Just pick a type and go.' },
  { title: 'CRM-ready', desc: 'Built for your CRM.' },
]

export function Homepage() {
  return (
    <div>
      <header className="sticky top-0 z-50 bg-white/90 backdrop-blur-md border-b border-stone-200">
        <div className="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
          <span className="font-display font-bold text-xl text-stone-800">ACI</span>
          <div className="flex items-center gap-4">
            <Link to="/help" className="text-stone-600 hover:text-orange-600 font-medium text-sm">
              How it works
            </Link>
            <Link
              to="/login"
              className="px-4 py-2 rounded-lg bg-orange-500 text-white font-medium hover:bg-orange-600 text-sm"
            >
              Sign in
            </Link>
          </div>
        </div>
      </header>

      <section className="relative py-20 px-4 overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50" />
        <div className="absolute top-20 right-10 w-72 h-72 bg-orange-200/40 rounded-full blur-3xl animate-blob" />
        <div className="absolute bottom-20 left-10 w-96 h-96 bg-amber-200/30 rounded-full blur-3xl animate-blob" style={{ animationDelay: '2s' }} />
        <div className="relative max-w-4xl mx-auto text-center">
          <p className="text-orange-600 font-semibold text-sm mb-4 animate-fade-in">
            Trusted by 1,000+ sales teams
          </p>
          <h1 className="font-display font-bold text-4xl sm:text-5xl md:text-6xl text-stone-900 leading-tight mb-6 animate-fade-in">
            Create CRM copy in minutes with AI
          </h1>
          <p className="text-lg text-stone-600 max-w-2xl mx-auto mb-8 animate-fade-in">
            Write sales emails, follow-ups, workflow messages, and CRM notes without prompts. Connect once and send copy straight into your CRM.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center animate-fade-in">
            <Link
              to="/login"
              className="inline-flex justify-center px-6 py-3 rounded-xl bg-orange-500 text-white font-semibold hover:bg-orange-600 shadow-lg shadow-orange-500/25"
            >
              Connect
            </Link>
            <Link
              to="/help"
              className="inline-flex justify-center px-6 py-3 rounded-xl border-2 border-stone-300 text-stone-700 font-semibold hover:border-orange-500 hover:text-orange-600"
            >
              See how it works
            </Link>
          </div>
        </div>
      </section>

      <section className="py-16 px-4 bg-white">
        <div className="max-w-6xl mx-auto">
          <h2 className="font-display font-bold text-2xl text-center text-stone-900 mb-12">
            How it works
          </h2>
          <div className="grid md:grid-cols-3 gap-8">
            {howItWorks.map((item) => (
              <div key={item.step} className="text-center p-6 rounded-2xl bg-stone-50 border border-stone-100">
                <span className="inline-flex w-10 h-10 rounded-full bg-orange-500 text-white font-bold items-center justify-center mb-4">
                  {item.step}
                </span>
                <h3 className="font-display font-semibold text-lg text-stone-900 mb-2">{item.title}</h3>
                <p className="text-stone-600 text-sm">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="py-16 px-4 bg-stone-50">
        <div className="max-w-6xl mx-auto">
          <h2 className="font-display font-bold text-2xl text-center text-stone-900 mb-12">
            What you can create
          </h2>
          <div className="flex flex-wrap justify-center gap-4">
            {whatYouCanCreate.map((item) => (
              <span
                key={item}
                className="px-5 py-3 rounded-xl bg-white border border-stone-200 text-stone-700 font-medium shadow-sm hover:border-orange-200 hover:shadow-md transition-all"
              >
                {item}
              </span>
            ))}
          </div>
        </div>
      </section>

      <section className="py-16 px-4 bg-white">
        <div className="max-w-6xl mx-auto">
          <h2 className="font-display font-bold text-2xl text-center text-stone-900 mb-12">
            Why this tool
          </h2>
          <div className="grid md:grid-cols-3 gap-8">
            {whyThisTool.map((item) => (
              <div key={item.title} className="text-center p-6 rounded-2xl border border-stone-200 hover:border-orange-200 hover:shadow-lg transition-all">
                <h3 className="font-display font-semibold text-lg text-stone-900 mb-2">{item.title}</h3>
                <p className="text-stone-600 text-sm">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="py-20 px-4 bg-orange-500">
        <div className="max-w-3xl mx-auto text-center">
          <h2 className="font-display font-bold text-3xl text-white mb-4">
            Start writing in minutes
          </h2>
          <p className="text-orange-100 text-lg mb-8">
            Connect your CRM, set your brand tone once, and generate copy whenever you need it.
          </p>
          <Link
            to="/login"
            className="inline-flex justify-center px-6 py-3 rounded-xl bg-white text-orange-600 font-semibold hover:bg-orange-50 shadow-lg"
          >
            Connect
          </Link>
        </div>
      </section>

      <footer className="py-12 px-4 bg-stone-900 text-stone-400">
        <div className="max-w-6xl mx-auto flex flex-col sm:flex-row justify-between items-center gap-4">
          <span className="font-display font-bold text-white">ACI</span>
          <div className="flex gap-6">
            <Link to="/privacy" className="hover:text-white">Privacy Policy</Link>
            <Link to="/terms" className="hover:text-white">Terms of Service</Link>
          </div>
        </div>
      </footer>
    </div>
  )
}
