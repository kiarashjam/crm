import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  Sparkles,
  ChevronDown,
  ChevronUp,
  Lightbulb,
  User,
  X,
} from 'lucide-react';
import { getCurrentUser, isDemoMode } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { cn } from '@/app/components/ui/utils';
import {
  getTemplateById,
  getUserSettings,
  getCopyHistoryStats,
  getCopyHistory,
  getTemplates,
  getDashboardStats,
  getPipelineValueByStage,
  getPipelineValueByAssignee,
  getLeads,
  getContacts,
  getDeals,
} from '@/app/api';
import { 
  generateCopyWithRecipient, 
  generateCopyInLanguage,
  type RecipientContext, 
  type SupportedLanguage,
  SUPPORTED_LANGUAGES 
} from '@/app/api/copyGenerator';
import type { Lead, Contact, Deal } from '@/app/api/types';
import type { CopyHistoryItem } from '@/app/api/types';
import type { PipelineValueByAssignee } from '@/app/api/reporting';
import { toast } from 'sonner';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';

// Import extracted components and config
import { DashboardHero } from './dashboard/DashboardHero';
import { PipelineChart } from './dashboard/PipelineChart';
import { QuickNav } from './dashboard/QuickNav';
import { CopyStatsWidget } from './dashboard/CopyStatsWidget';
import { RecentActivity } from './dashboard/RecentActivity';
import { TeamPerformance } from './dashboard/TeamPerformance';
import { copyTypes, goals } from './dashboard/config';
import type { DashboardStats, CopyStats, PipelineStage } from './dashboard/types';

export default function Dashboard() {
  const location = useLocation();
  const templateId = (location.state as { templateId?: string } | null)?.templateId;
  const [selectedType, setSelectedType] = useState<typeof copyTypes[number]['id'] | ''>(() =>
    isDemoMode() ? 'sales-email' : ''
  );
  const [goal, setGoal] = useState<string>(goals[0]!);
  const [context, setContext] = useState('');
  const [length, setLength] = useState<'short' | 'medium' | 'long'>('medium');
  const [language, setLanguage] = useState<SupportedLanguage>('en');
  const [isGenerating, setIsGenerating] = useState(false);
  const [stats, setStats] = useState<CopyStats>({
    sentThisWeek: 0,
    totalSent: 0,
    templateCount: 8,
  });
  const [crmStats, setCrmStats] = useState<DashboardStats | null>(null);
  const [pipelineByStage, setPipelineByStage] = useState<PipelineStage[]>([]);
  const [pipelineByAssignee, setPipelineByAssignee] = useState<PipelineValueByAssignee[]>([]);
  const [recentActivity, setRecentActivity] = useState<CopyHistoryItem[]>([]);
  
  // Recipient selection states
  const [showRecipientPicker, setShowRecipientPicker] = useState(false);
  const [recipientType, setRecipientType] = useState<'lead' | 'contact' | 'deal' | null>(null);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [selectedRecipient, setSelectedRecipient] = useState<{
    type: 'lead' | 'contact' | 'deal';
    id: string;
    name: string;
    email?: string;
    company?: string;
    dealStage?: string;
    dealValue?: string;
  } | null>(null);
  const [recipientSearch, setRecipientSearch] = useState('');
  
  const navigate = useNavigate();
  const user = getCurrentUser();
  const displayName = user?.name ?? 'there';

  useEffect(() => {
    let cancelled = false;
    const guard = <T,>(fn: (x: T) => void) => (x: T) => { if (!cancelled) fn(x); };
    getCopyHistoryStats()
      .then(guard((s) => setStats((prev) => ({ ...prev, ...s }))))
      .catch(() => {});
    getTemplates()
      .then(guard((t) => setStats((prev) => ({ ...prev, templateCount: t.length }))))
      .catch(() => {});
    getCopyHistory()
      .then(guard((items) => setRecentActivity(items.slice(0, 5))))
      .catch(() => {});
    getDashboardStats()
      .then(guard(setCrmStats))
      .catch(() => {});
    getPipelineValueByStage()
      .then(guard(setPipelineByStage))
      .catch(() => setPipelineByStage([]));
    getPipelineValueByAssignee()
      .then(guard(setPipelineByAssignee))
      .catch(() => setPipelineByAssignee([]));
    // Load recipients for copy personalization
    getLeads().then(guard(setLeads)).catch(() => setLeads([]));
    getContacts().then(guard(setContacts)).catch(() => setContacts([]));
    getDeals().then(guard(setDeals)).catch(() => setDeals([]));
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!templateId) return;
    let cancelled = false;
    getTemplateById(templateId)
      .then((t) => {
        if (!cancelled && t) {
          setSelectedType(t.copyTypeId);
          setGoal(t.goal);
        }
      })
      .catch(() => {});
    return () => { cancelled = true; };
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
      
      // Build recipient context if a recipient is selected
      let recipient: RecipientContext | undefined;
      if (selectedRecipient) {
        recipient = {
          name: selectedRecipient.name,
          email: selectedRecipient.email,
          company: selectedRecipient.company,
          type: selectedRecipient.type,
          dealStage: selectedRecipient.dealStage,
          dealValue: selectedRecipient.dealValue,
        };
      }
      
      // Use multi-language generator if not English
      const result = language !== 'en' 
        ? await generateCopyInLanguage({
            copyTypeId: selectedType,
            goal: goal || goals[0]!,
            context: context.trim() || undefined,
            length,
            companyName: settings.companyName,
            brandTone: settings.brandTone,
            recipient,
            targetLanguage: language,
          })
        : await generateCopyWithRecipient({
            copyTypeId: selectedType,
            goal: goal || goals[0]!,
            context: context.trim() || undefined,
            length,
            companyName: settings.companyName,
            brandTone: settings.brandTone,
            recipient,
          });
      
      const copyTypeLabel = copyTypes.find((t) => t.id === selectedType)?.title ?? 'Copy';
      const langName = SUPPORTED_LANGUAGES.find(l => l.code === language)?.name ?? '';
      toast.success(messages.copy.generated + (language !== 'en' ? ` (${langName})` : ''));
      navigate('/generated', { 
        state: { 
          copy: result.body, 
          subject: result.subject,
          copyTypeLabel, 
          copyTypeId: selectedType,
        } 
      });
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 dark:bg-slate-900">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {/* Hero Section - Extracted Component */}
          <DashboardHero displayName={displayName} stats={crmStats} />

        {/* Main Content Grid */}
        <div className="space-y-8">
          <div className="grid lg:grid-cols-3 gap-6 lg:gap-8">
            {/* Left Column - 2 cols */}
            <div className="lg:col-span-2 space-y-6">
              
              {/* Pipeline by Stage - Extracted Component */}
              <PipelineChart stages={pipelineByStage} />

              {/* Intelligent Sales Writer */}
              <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                <div className="px-6 py-4 border-b border-slate-100 flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/25">
                    <Sparkles className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <h2 className="text-base font-bold text-slate-900">Intelligent Sales Writer</h2>
                    <p className="text-xs text-slate-500">Create personalized sales content in seconds</p>
                  </div>
                </div>
                <div className="p-6">
                  <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3">Choose copy type</label>
                  <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
                    {copyTypes.map((type) => {
                      const isSelected = selectedType === type.id;
                      return (
                        <button
                          key={type.id}
                          type="button"
                          onClick={() => setSelectedType(type.id)}
                          className={cn(
                            'relative flex flex-col items-center text-center p-4 rounded-xl border-2 transition-all duration-200 group overflow-hidden',
                            isSelected
                              ? 'border-orange-500 bg-orange-50'
                              : 'border-slate-200 bg-white hover:border-slate-300 hover:shadow-md'
                          )}
                        >
                          <div className={cn(
                            "w-12 h-12 rounded-xl flex items-center justify-center mb-3 transition-all",
                            isSelected 
                              ? `bg-gradient-to-br ${type.color} shadow-lg` 
                              : "bg-slate-100 group-hover:scale-110"
                          )}>
                            <type.icon className={cn("w-5 h-5", isSelected ? "text-white" : "text-slate-600")} />
                          </div>
                          <span className={cn("text-sm font-medium", isSelected ? "text-orange-700" : "text-slate-700")}>{type.title}</span>
                          <span className="text-xs text-slate-500 mt-0.5 hidden sm:block">{type.desc}</span>
                        </button>
                      );
                    })}
                  </div>

                  {selectedType ? (
                    <div className="mt-6 pt-5 border-t border-slate-100 space-y-4">
                      <div className="grid sm:grid-cols-2 gap-4">
                        <div>
                          <label htmlFor="goal" className="block text-sm font-medium text-slate-700 mb-1.5">Message goal</label>
                          <div className="relative">
                            <select
                              id="goal"
                              value={goal}
                              onChange={(e) => setGoal(e.target.value)}
                              className="w-full h-11 pl-4 pr-10 rounded-xl border border-slate-300 bg-white text-slate-900 text-sm focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-all"
                            >
                              {goals.map((g) => (
                                <option key={g} value={g}>{g}</option>
                              ))}
                            </select>
                            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 pointer-events-none" />
                          </div>
                        </div>
                        <div>
                          <label className="block text-sm font-medium text-slate-700 mb-1.5">Length</label>
                          <div className="flex gap-2">
                            {[
                              { id: 'short' as const, label: 'Short' },
                              { id: 'medium' as const, label: 'Medium' },
                              { id: 'long' as const, label: 'Long' },
                            ].map((opt) => (
                              <button
                                key={opt.id}
                                type="button"
                                onClick={() => setLength(opt.id)}
                                className={cn(
                                  'flex-1 py-2.5 px-3 rounded-xl border-2 text-sm font-medium transition-all',
                                  length === opt.id
                                    ? 'border-orange-500 bg-orange-50 text-orange-700'
                                    : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
                                )}
                              >
                                {opt.label}
                              </button>
                            ))}
                          </div>
                        </div>
                      </div>
                      
                      <div>
                        <label htmlFor="context" className="block text-sm font-medium text-slate-700 mb-1.5">Optional context</label>
                        <textarea
                          id="context"
                          value={context}
                          onChange={(e) => setContext(e.target.value)}
                          placeholder="Add details like previous conversations, pain points, or key talking points..."
                          rows={3}
                          className="w-full px-4 py-3 rounded-xl border border-slate-300 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none resize-none transition-all"
                        />
                        <p className="flex items-center gap-1.5 mt-1.5 text-xs text-slate-500">
                          <Lightbulb className="w-3.5 h-3.5 text-amber-500" />
                          More context = more relevant, on-brand copy
                        </p>
                      </div>
                      
                      {/* Language selector */}
                      <div>
                        <label htmlFor="language" className="block text-sm font-medium text-slate-700 mb-1.5">Output language</label>
                        <select
                          id="language"
                          value={language}
                          onChange={(e) => setLanguage(e.target.value as SupportedLanguage)}
                          className="w-full px-4 py-3 rounded-xl border border-slate-300 bg-white text-slate-900 text-sm focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-all cursor-pointer"
                        >
                          {SUPPORTED_LANGUAGES.map((lang) => (
                            <option key={lang.code} value={lang.code}>
                              {lang.name}
                            </option>
                          ))}
                        </select>
                        {language !== 'en' && (
                          <p className="flex items-center gap-1.5 mt-1.5 text-xs text-orange-600">
                            <Sparkles className="w-3.5 h-3.5" />
                            AI will generate copy in {SUPPORTED_LANGUAGES.find(l => l.code === language)?.name}
                          </p>
                        )}
                      </div>
                      
                      {/* Recipient selection */}
                      <div className="bg-slate-50 rounded-xl border border-slate-200 overflow-hidden">
                        <button
                          type="button"
                          onClick={() => setShowRecipientPicker(!showRecipientPicker)}
                          className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-100 transition-colors"
                        >
                          <div className="flex items-center gap-2">
                            <User className="w-4 h-4 text-slate-500" />
                            <span className="text-sm font-medium text-slate-700">
                              {selectedRecipient 
                                ? `Personalizing for: ${selectedRecipient.name}` 
                                : 'Personalize for a recipient (optional)'}
                            </span>
                          </div>
                          {showRecipientPicker ? (
                            <ChevronUp className="w-4 h-4 text-slate-400" />
                          ) : (
                            <ChevronDown className="w-4 h-4 text-slate-400" />
                          )}
                        </button>
                        
                        {showRecipientPicker && (
                          <div className="px-4 py-3 border-t border-slate-200 bg-white">
                            {selectedRecipient ? (
                              <div className="flex items-center justify-between p-3 bg-orange-50 border border-orange-200 rounded-xl">
                                <div>
                                  <p className="text-sm font-medium text-slate-900">{selectedRecipient.name}</p>
                                  <p className="text-xs text-slate-500">
                                    {selectedRecipient.type === 'lead' ? 'Lead' : selectedRecipient.type === 'contact' ? 'Contact' : 'Deal'}
                                    {selectedRecipient.email && ` • ${selectedRecipient.email}`}
                                  </p>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => setSelectedRecipient(null)}
                                  className="p-1.5 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
                                  aria-label="Remove recipient"
                                >
                                  <X className="w-4 h-4" />
                                </button>
                              </div>
                            ) : (
                              <>
                                <div className="flex gap-2 mb-3">
                                  {[
                                    { id: 'lead' as const, label: 'Leads', count: leads.length },
                                    { id: 'contact' as const, label: 'Contacts', count: contacts.length },
                                    { id: 'deal' as const, label: 'Deals', count: deals.length },
                                  ].map((type) => (
                                    <button
                                      key={type.id}
                                      type="button"
                                      onClick={() => {
                                        setRecipientType(type.id);
                                        setRecipientSearch('');
                                      }}
                                      className={cn(
                                        'flex-1 py-2 px-3 rounded-lg text-xs font-medium transition-colors',
                                        recipientType === type.id
                                          ? 'bg-orange-100 text-orange-700 border border-orange-300'
                                          : 'bg-slate-100 border border-slate-200 text-slate-600 hover:border-slate-300'
                                      )}
                                    >
                                      {type.label} ({type.count})
                                    </button>
                                  ))}
                                </div>
                                
                                {recipientType && (
                                  <>
                                    <input
                                      type="text"
                                      value={recipientSearch}
                                      onChange={(e) => setRecipientSearch(e.target.value)}
                                      placeholder={`Search ${recipientType}s...`}
                                      className="w-full px-3 py-2 mb-2 text-sm border border-slate-300 rounded-lg focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none"
                                    />
                                    <div className="max-h-40 overflow-y-auto space-y-1">
                                      {(recipientType === 'lead' ? leads : recipientType === 'contact' ? contacts : deals)
                                        .filter((item) => 
                                          item.name.toLowerCase().includes(recipientSearch.toLowerCase()) ||
                                          ('email' in item && item.email?.toLowerCase().includes(recipientSearch.toLowerCase()))
                                        )
                                        .slice(0, 10)
                                        .map((item) => (
                                          <button
                                            key={item.id}
                                            type="button"
                                            onClick={() => {
                                              const deal = recipientType === 'deal' ? item as Deal : null;
                                              const lead = recipientType === 'lead' ? item as Lead : null;
                                              const contact = recipientType === 'contact' ? item as Contact : null;
                                              setSelectedRecipient({
                                                type: recipientType,
                                                id: item.id,
                                                name: item.name,
                                                email: lead?.email || contact?.email,
                                                company: undefined,
                                                dealStage: deal?.stage,
                                                dealValue: deal?.value,
                                              });
                                              setShowRecipientPicker(false);
                                              setRecipientType(null);
                                            }}
                                            className="w-full flex items-center justify-between p-2.5 text-left text-sm rounded-lg hover:bg-slate-50 transition-colors"
                                          >
                                            <div>
                                              <p className="font-medium text-slate-900">{item.name}</p>
                                              {'email' in item && item.email && (
                                                <p className="text-xs text-slate-500">{item.email}</p>
                                              )}
                                              {recipientType === 'deal' && (
                                                <p className="text-xs text-slate-500">
                                                  {(item as Deal).stage} • {(item as Deal).value}
                                                </p>
                                              )}
                                            </div>
                                          </button>
                                        ))}
                                    </div>
                                  </>
                                )}
                              </>
                            )}
                          </div>
                        )}
                      </div>
                      
                      <button
                        type="button"
                        onClick={handleGenerate}
                        disabled={isGenerating}
                        className={cn(
                          'w-full h-12 px-6 rounded-xl font-semibold text-white flex items-center justify-center gap-2 transition-all',
                          'bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/25',
                          'disabled:from-slate-300 disabled:to-slate-400 disabled:shadow-none disabled:cursor-not-allowed'
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
                            Generate Copy
                          </>
                        )}
                      </button>
                    </div>
                  ) : (
                    <EmptyState
                      icon={Sparkles}
                      title="Choose a copy type to get started"
                      description="Select one of the formats above to generate sales-ready copy in seconds."
                      actionLabel="Browse templates"
                      actionHref="/templates"
                      className="mt-6"
                    />
                  )}
                </div>
              </section>

              {/* Team Performance - Extracted Component */}
              <TeamPerformance members={pipelineByAssignee} />
            </div>

            {/* Right Column - Sidebar with Extracted Components */}
            <div className="space-y-6">
              <QuickNav />
              <CopyStatsWidget stats={stats} />
              <RecentActivity items={recentActivity} />
            </div>
          </div>
        </div>
        </main>
      </PageTransition>
    </div>
  );
}
