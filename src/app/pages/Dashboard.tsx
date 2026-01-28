import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Sparkles,
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Workflow,
  ChevronDown,
  FileOutput,
  Send,
  TrendingUp,
  LayoutTemplate,
  Lightbulb,
  ArrowRight,
} from 'lucide-react';
import { getDemoUser } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { cn } from '@/app/components/ui/utils';

export default function Dashboard() {
  const [selectedType, setSelectedType] = useState('');
  const [goal, setGoal] = useState('');
  const [context, setContext] = useState('');
  const [length, setLength] = useState<'short' | 'medium' | 'long'>('medium');
  const [isGenerating, setIsGenerating] = useState(false);
  const navigate = useNavigate();
  const user = getDemoUser();
  const displayName = user?.name ?? 'there';

  const copyTypes = [
    { id: 'sales-email', icon: Mail, title: 'Sales Email', desc: 'Personalized outreach' },
    { id: 'follow-up', icon: MessageSquare, title: 'Follow-up', desc: 'Keep conversations going' },
    { id: 'crm-note', icon: FileText, title: 'CRM Note', desc: 'Document interactions' },
    { id: 'deal-message', icon: Briefcase, title: 'Deal Message', desc: 'Update stakeholders' },
    { id: 'workflow-message', icon: Workflow, title: 'Workflow', desc: 'Automated sequences' },
  ] as const;

  const goals = [
    'Schedule a meeting',
    'Follow up after demo',
    'Request feedback',
    'Share resources',
    'Check in on progress',
    'Close the deal',
  ];

  const handleGenerate = () => {
    if (selectedType) {
      setIsGenerating(true);
      setTimeout(() => {
        setIsGenerating(false);
        navigate('/generated');
      }, 2000);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50/80">
      <AppHeader />

      <main className="w-full px-[var(--page-padding)] py-8 lg:py-10">
        <div className="w-full">
          {/* Welcome & stats */}
          <div className="mb-10">
            <p className="text-sm font-medium text-slate-500 uppercase tracking-wider mb-1">
              Dashboard
            </p>
            <h1 className="text-2xl sm:text-3xl font-semibold text-slate-900 tracking-tight mb-2">
              Welcome back, {displayName}
            </h1>
            <p className="text-slate-600 mb-8 max-w-xl">
              Create AI-powered copy and send it directly to HubSpot. Select a type below to get started.
            </p>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              {[
                { value: '24', label: 'Generated this week', icon: FileOutput, color: 'bg-amber-50 text-amber-700 border-amber-100' },
                { value: '8', label: 'Templates', icon: LayoutTemplate, color: 'bg-slate-100 text-slate-600 border-slate-200' },
                { value: '18', label: 'Sent to HubSpot', icon: Send, color: 'bg-emerald-50 text-emerald-700 border-emerald-100' },
                { value: '~2 min', label: 'Avg. time saved', icon: TrendingUp, color: 'bg-violet-50 text-violet-700 border-violet-100' },
              ].map((stat) => (
                <div
                  key={stat.label}
                  className="bg-white rounded-xl border border-slate-200/80 p-4 shadow-sm hover:shadow transition-shadow"
                >
                  <div className={cn('inline-flex p-2 rounded-lg border', stat.color)}>
                    <stat.icon className="w-5 h-5" />
                  </div>
                  <p className="mt-3 text-2xl font-semibold text-slate-900 tabular-nums">{stat.value}</p>
                  <p className="text-sm text-slate-500">{stat.label}</p>
                </div>
              ))}
            </div>
          </div>

          {/* Section divider: Welcome → Create */}
          <div className="flex items-center gap-4 my-10" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-amber-300/60 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-4 py-2 rounded-full bg-white border border-amber-200/80 shadow-sm text-sm font-medium text-amber-800/90 ring-1 ring-amber-100/50 transition-shadow hover:shadow-md">
              <Sparkles className="w-4 h-4 text-amber-500" />
              Create your content
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-amber-300/60 to-transparent rounded-full" />
          </div>

          {/* Create copy section */}
          <section className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
            <div className="px-6 py-5 border-b border-slate-100">
              <h2 className="text-lg font-semibold text-slate-900">Create new copy</h2>
              <p className="text-sm text-slate-500 mt-0.5">Choose a content type and customize your message.</p>
            </div>

            <div className="p-6 sm:p-8">
              {/* Copy type selection */}
              <div className="mb-8">
                <label className="block text-sm font-medium text-slate-700 mb-3">Content type</label>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                  {copyTypes.map((type) => {
                    const isSelected = selectedType === type.id;
                    return (
                      <button
                        key={type.id}
                        type="button"
                        onClick={() => setSelectedType(type.id)}
                        className={cn(
                          'flex flex-col items-center text-center p-4 rounded-xl border-2 transition-all duration-200',
                          isSelected
                            ? 'border-amber-500 bg-amber-50/50 shadow-sm'
                            : 'border-slate-200 bg-slate-50/50 hover:border-slate-300 hover:bg-slate-50'
                        )}
                      >
                        <span
                          className={cn(
                            'flex items-center justify-center w-12 h-12 rounded-lg mb-3 transition-colors',
                            isSelected ? 'bg-amber-500 text-white' : 'bg-slate-200 text-slate-600'
                          )}
                        >
                          <type.icon className="w-6 h-6" />
                        </span>
                        <span className="text-sm font-medium text-slate-900">{type.title}</span>
                        <span className="text-xs text-slate-500 mt-0.5">{type.desc}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {selectedType ? (
                <div className="space-y-6 pt-6 border-t border-slate-100">
                  {/* Goal */}
                  <div>
                    <label htmlFor="goal" className="block text-sm font-medium text-slate-700 mb-2">
                      Message goal
                    </label>
                    <div className="relative">
                      <select
                        id="goal"
                        value={goal}
                        onChange={(e) => setGoal(e.target.value)}
                        className="w-full h-11 pl-4 pr-10 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm focus:border-amber-500 focus:ring-2 focus:ring-amber-500/20 outline-none transition-colors"
                      >
                        <option value="">Select a goal...</option>
                        {goals.map((g) => (
                          <option key={g} value={g}>
                            {g}
                          </option>
                        ))}
                      </select>
                      <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400 pointer-events-none" />
                    </div>
                  </div>

                  {/* Context */}
                  <div>
                    <label htmlFor="context" className="block text-sm font-medium text-slate-700 mb-2">
                      Optional context
                    </label>
                    <textarea
                      id="context"
                      value={context}
                      onChange={(e) => setContext(e.target.value)}
                      placeholder="e.g. previous conversation, company info, pain points..."
                      rows={3}
                      className="w-full px-4 py-3 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-amber-500 focus:ring-2 focus:ring-amber-500/20 outline-none transition-colors resize-none"
                    />
                    <p className="flex items-center gap-1.5 mt-2 text-xs text-slate-500">
                      <Lightbulb className="w-3.5 h-3.5 shrink-0" />
                      More context improves the AI output.
                    </p>
                  </div>

                  {/* Length */}
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">Length</label>
                    <div className="flex gap-2">
                      {[
                        { id: 'short', label: 'Short', sub: '2–3 sentences' },
                        { id: 'medium', label: 'Medium', sub: '1 paragraph' },
                        { id: 'long', label: 'Long', sub: '2+ paragraphs' },
                      ].map((opt) => (
                        <button
                          key={opt.id}
                          type="button"
                          onClick={() => setLength(opt.id)}
                          className={cn(
                            'flex-1 py-3 px-4 rounded-lg border text-left transition-all',
                            length === opt.id
                              ? 'border-amber-500 bg-amber-50 text-amber-800 shadow-sm'
                              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
                          )}
                        >
                          <span className="block text-sm font-medium">{opt.label}</span>
                          <span className="block text-xs text-slate-500 mt-0.5">{opt.sub}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Generate CTA */}
                  <div className="pt-2">
                    <button
                      type="button"
                      onClick={handleGenerate}
                      disabled={isGenerating}
                      className={cn(
                        'w-full sm:w-auto min-w-[200px] h-12 px-6 rounded-lg font-medium text-white flex items-center justify-center gap-2 transition-all',
                        'bg-amber-600 hover:bg-amber-700 active:bg-amber-800',
                        'disabled:bg-slate-300 disabled:cursor-not-allowed disabled:opacity-90'
                      )}
                    >
                      {isGenerating ? (
                        <>
                          <span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                          Generating...
                        </>
                      ) : (
                        <>
                          <Sparkles className="w-5 h-5" />
                          Generate copy
                        </>
                      )}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center py-12 px-4 text-center border-2 border-dashed border-slate-200 rounded-xl bg-slate-50/50">
                  <div className="w-14 h-14 rounded-full bg-slate-200 flex items-center justify-center mb-4">
                    <Sparkles className="w-7 h-7 text-slate-500" />
                  </div>
                  <p className="text-sm font-medium text-slate-700">Select a content type</p>
                  <p className="text-sm text-slate-500 mt-1">Choose one of the options above to create your copy.</p>
                </div>
              )}
            </div>
          </section>

          {/* Section divider: Create → More */}
          <div className="flex items-center gap-4 my-10" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-slate-300 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-4 py-2 rounded-full bg-slate-50 border border-slate-200 shadow-sm text-sm font-medium text-slate-600 transition-shadow hover:shadow-md hover:border-slate-300">
              <LayoutTemplate className="w-4 h-4 text-slate-500" />
              More options
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-slate-300 to-transparent rounded-full" />
          </div>

          {/* Quick link */}
          <div className="mt-6 flex justify-center">
            <Link
              to="/templates"
              className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-amber-600 transition-colors"
            >
              Browse templates
              <ArrowRight className="w-4 h-4" />
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
