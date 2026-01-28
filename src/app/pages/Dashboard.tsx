import { useState, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
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
  Link2,
  History,
  Settings,
  CheckCircle2,
  XCircle,
} from 'lucide-react';
import { getCurrentUser } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { cn } from '@/app/components/ui/utils';
import {
  getTemplateById,
  getUserSettings,
  generateCopy,
  getCopyHistoryStats,
  getCopyHistory,
  getTemplates,
  getConnectionStatus,
} from '@/app/api';
import type { CopyTypeId } from '@/app/api/types';
import type { CopyHistoryItem } from '@/app/api/types';
import { toast } from 'sonner';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

const copyTypes = [
  { id: 'sales-email' as CopyTypeId, icon: Mail, title: 'Sales Email', desc: 'Personalized outreach' },
  { id: 'follow-up' as CopyTypeId, icon: MessageSquare, title: 'Follow-up', desc: 'Keep conversations going' },
  { id: 'crm-note' as CopyTypeId, icon: FileText, title: 'CRM Note', desc: 'Document interactions' },
  { id: 'deal-message' as CopyTypeId, icon: Briefcase, title: 'Deal Message', desc: 'Update stakeholders' },
  { id: 'workflow-message' as CopyTypeId, icon: Workflow, title: 'Workflow', desc: 'Automated sequences' },
];

const goals = [
  'Schedule a meeting',
  'Follow up after demo',
  'Request feedback',
  'Share resources',
  'Check in on progress',
  'Close the deal',
];

export default function Dashboard() {
  const location = useLocation();
  const templateId = (location.state as { templateId?: string } | null)?.templateId;
  const [selectedType, setSelectedType] = useState<CopyTypeId | ''>('');
  const [goal, setGoal] = useState('');
  const [context, setContext] = useState('');
  const [length, setLength] = useState<'short' | 'medium' | 'long'>('medium');
  const [isGenerating, setIsGenerating] = useState(false);
  const [stats, setStats] = useState<{ sentThisWeek: number; totalSent: number; templateCount: number }>({
    sentThisWeek: 0,
    totalSent: 0,
    templateCount: 8,
  });
  const [connectionStatus, setConnectionStatus] = useState<{ connected: boolean; accountEmail?: string }>({ connected: false });
  const [recentActivity, setRecentActivity] = useState<CopyHistoryItem[]>([]);
  const navigate = useNavigate();
  const user = getCurrentUser();
  const displayName = user?.name ?? 'there';

  useEffect(() => {
    getCopyHistoryStats()
      .then((s) => setStats((prev) => ({ ...prev, ...s })))
      .catch(() => {});
    getTemplates()
      .then((t) => setStats((prev) => ({ ...prev, templateCount: t.length })))
      .catch(() => {});
    getConnectionStatus()
      .then((s) => setConnectionStatus({ connected: s.connected, accountEmail: s.accountEmail }))
      .catch(() => {});
    getCopyHistory()
      .then((items) => setRecentActivity(items.slice(0, 5)))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!templateId) return;
    getTemplateById(templateId)
      .then((t) => {
        if (t) {
          setSelectedType(t.copyTypeId);
          setGoal(t.goal);
        }
      })
      .catch(() => {});
  }, [templateId]);

  const regenerateContext = (location.state as { regenerateContext?: string } | null)?.regenerateContext;
  useEffect(() => {
    if (regenerateContext?.trim()) setContext(regenerateContext);
  }, [regenerateContext]);

  const handleGenerate = async () => {
    if (!selectedType) return;
    setIsGenerating(true);
    try {
      const settings = await getUserSettings();
      const copy = await generateCopy({
        copyTypeId: selectedType,
        goal: goal || goals[0],
        context: context.trim() || undefined,
        length,
        companyName: settings.companyName,
        brandTone: settings.brandTone,
      });
      const copyTypeLabel = copyTypes.find((t) => t.id === selectedType)?.title ?? 'Copy';
      toast.success(messages.copy.generated);
      navigate('/generated', { state: { copy, copyTypeLabel } });
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-6xl mx-auto px-[var(--page-padding)] py-8 lg:py-10" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-10">
            <p className="text-sm font-medium text-slate-500 uppercase tracking-wider mb-1">
              Dashboard
            </p>
            <h1 className="text-2xl sm:text-3xl font-semibold text-slate-900 tracking-tight mb-2">
              Welcome back, {displayName}
            </h1>
            <p className="text-slate-600 mb-6 max-w-xl">
              Create AI-powered copy and send it directly to your CRM. Select a type below to get started.
            </p>

            <div className="mb-8 flex flex-wrap items-center gap-4">
              <div
                className={cn(
                  'flex items-center gap-3 rounded-xl border px-4 py-3',
                  connectionStatus.connected
                    ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                    : 'bg-amber-50 border-amber-200 text-amber-800'
                )}
                aria-live="polite"
              >
                {connectionStatus.connected ? (
                  <>
                    <CheckCircle2 className="w-5 h-5 shrink-0 text-emerald-600" />
                    <div>
                      <p className="font-medium">CRM connected</p>
                      {connectionStatus.accountEmail && (
                        <p className="text-sm opacity-90">{connectionStatus.accountEmail}</p>
                      )}
                    </div>
                  </>
                ) : (
                  <>
                    <XCircle className="w-5 h-5 shrink-0 text-amber-600" />
                    <div>
                      <p className="font-medium">CRM not connected</p>
                      <p className="text-sm opacity-90">Connect to send copy to your CRM.</p>
                    </div>
                  </>
                )}
                <Link
                  to="/connect"
                  className="ml-2 inline-flex items-center gap-1.5 text-sm font-medium underline underline-offset-2 hover:no-underline focus-visible:rounded"
                >
                  <Link2 className="w-4 h-4" />
                  {connectionStatus.connected ? 'Manage' : 'Connect'}
                </Link>
              </div>
            </div>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              {[
                { value: String(stats.sentThisWeek), label: 'Generated this week', icon: FileOutput, color: 'bg-orange-50 text-orange-700 border-orange-100' },
                { value: String(stats.templateCount), label: 'Templates', icon: LayoutTemplate, color: 'bg-slate-100 text-slate-600 border-slate-200' },
                { value: String(stats.totalSent), label: 'Sent to CRM', icon: Send, color: 'bg-emerald-50 text-emerald-700 border-emerald-100' },
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

          <div className="flex items-center gap-4 my-10" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-orange-200/60 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-4 py-2 rounded-full bg-white border border-orange-200/80 shadow-sm text-sm font-medium text-orange-800 ring-1 ring-orange-100/50 transition-shadow hover:shadow-md">
              <Sparkles className="w-4 h-4 text-orange-500" />
              Create your content
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-orange-200/60 to-transparent rounded-full" />
          </div>

          <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-6 py-5 border-b border-slate-100">
              <h2 className="text-lg font-semibold text-slate-900">Create new copy</h2>
              <p className="text-sm text-slate-500 mt-0.5">Choose a content type and customize your message.</p>
            </div>

            <div className="p-6 sm:p-8">
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
                            ? 'border-orange-500 bg-orange-50/50 shadow-sm'
                            : 'border-slate-200 bg-slate-50/50 hover:border-slate-300 hover:bg-slate-50'
                        )}
                      >
                        <span
                          className={cn(
                            'flex items-center justify-center w-12 h-12 rounded-lg mb-3 transition-colors',
                            isSelected ? 'bg-orange-500 text-white' : 'bg-slate-200 text-slate-600'
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
                  <div>
                    <label htmlFor="goal" className="block text-sm font-medium text-slate-700 mb-2">
                      Message goal
                    </label>
                    <div className="relative">
                      <select
                        id="goal"
                        value={goal}
                        onChange={(e) => setGoal(e.target.value)}
                        className="w-full h-11 pl-4 pr-10 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-colors"
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
                      className="w-full px-4 py-3 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-colors resize-none"
                    />
                    <p className="flex items-center gap-1.5 mt-2 text-xs text-slate-500">
                      <Lightbulb className="w-3.5 h-3.5 shrink-0" />
                      More context improves the AI output.
                    </p>
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">Length</label>
                    <div className="flex gap-2">
                      {[
                        { id: 'short' as const, label: 'Short', sub: '2–3 sentences' },
                        { id: 'medium' as const, label: 'Medium', sub: '1 paragraph' },
                        { id: 'long' as const, label: 'Long', sub: '2+ paragraphs' },
                      ].map((opt) => (
                        <button
                          key={opt.id}
                          type="button"
                          onClick={() => setLength(opt.id)}
                          className={cn(
                            'flex-1 py-3 px-4 rounded-lg border text-left transition-all',
                            length === opt.id
                              ? 'border-orange-500 bg-orange-50 text-orange-800 shadow-sm'
                              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
                          )}
                        >
                          <span className="block text-sm font-medium">{opt.label}</span>
                          <span className="block text-xs text-slate-500 mt-0.5">{opt.sub}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="pt-2">
                    <button
                      type="button"
                      onClick={handleGenerate}
                      disabled={isGenerating}
                      className={cn(
                        'w-full sm:w-auto min-w-[200px] h-12 px-6 rounded-lg font-medium text-white flex items-center justify-center gap-2 transition-all',
                        'bg-orange-600 hover:bg-orange-500 active:bg-orange-700',
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
                  <div className="w-14 h-14 rounded-full bg-slate-100 flex items-center justify-center mb-4 text-slate-500">
                    <Sparkles className="w-7 h-7" aria-hidden />
                  </div>
                  <p className="text-sm font-medium text-slate-700">Select a content type</p>
                  <p className="text-sm text-slate-500 mt-1">Choose one of the options above to create your copy.</p>
                </div>
              )}
            </div>
          </section>

          <section className="mt-10 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden" aria-labelledby="recent-activity-heading">
            <div className="px-6 py-5 border-b border-slate-100">
              <h2 id="recent-activity-heading" className="text-lg font-semibold text-slate-900">Recent activity</h2>
              <p className="text-sm text-slate-500 mt-0.5">Latest copy sent to CRM.</p>
            </div>
            <div className="p-6">
              {recentActivity.length === 0 ? (
                <p className="text-sm text-slate-500 py-4">No activity yet. Generate copy and send it to your CRM to see it here.</p>
              ) : (
                <ul className="space-y-3">
                  {recentActivity.map((item) => (
                    <li key={item.id} className="flex flex-wrap items-center gap-2 text-sm border-b border-slate-100 pb-3 last:border-0 last:pb-0">
                      <span className="font-medium text-slate-900">{item.type}</span>
                      <span className="text-slate-500">→</span>
                      <span className="text-slate-700">{item.recipientName}</span>
                      <span className="text-slate-400 text-xs">
                        {new Date(item.createdAt).toLocaleDateString(undefined, { dateStyle: 'short' })}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <div className="mt-4">
                <Link
                  to="/history"
                  className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-orange-600 transition-colors focus-visible:rounded"
                >
                  View full history
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
            </div>
          </section>

          <div className="flex items-center gap-4 my-10" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-slate-300 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-4 py-2 rounded-full bg-slate-50 border border-slate-200 shadow-sm text-sm font-medium text-slate-600 transition-shadow hover:shadow-md hover:border-slate-300">
              <LayoutTemplate className="w-4 h-4 text-slate-500" />
              More options
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-slate-300 to-transparent rounded-full" />
          </div>

          <div className="mt-6 flex flex-wrap justify-center gap-6" role="navigation" aria-label="Quick links">
            <Link
              to="/templates"
              className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-orange-600 transition-colors focus-visible:rounded-lg"
            >
              <LayoutTemplate className="w-4 h-4" />
              Templates
            </Link>
            <Link
              to="/history"
              className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-orange-600 transition-colors focus-visible:rounded-lg"
            >
              <History className="w-4 h-4" />
              History
            </Link>
            <Link
              to="/settings"
              className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-orange-600 transition-colors focus-visible:rounded-lg"
            >
              <Settings className="w-4 h-4" />
              Settings
            </Link>
            <Link
              to="/connect"
              className="inline-flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-orange-600 transition-colors focus-visible:rounded-lg"
            >
              <Link2 className="w-4 h-4" />
              Connection
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
}
