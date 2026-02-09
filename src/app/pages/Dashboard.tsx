import { useState, useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { Copy, Check, Send } from 'lucide-react';
import { getCurrentUser, isDemoMode } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import {
  getTemplateById,
  getUserSettings,
  getCopyHistoryStats,
  getTemplates,
  getDashboardStats,
  getPipelineValueByStage,
  getPipelineValueByAssignee,
  getLeads,
  getContacts,
  getDeals,
  getActivities,
  sendCopyToCrm,
} from '@/app/api';
import { 
  generateCopyWithRecipient, 
  generateCopyInLanguage,
  type RecipientContext, 
  type SupportedLanguage,
  SUPPORTED_LANGUAGES 
} from '@/app/api/copyGenerator';
import type { Lead, Contact, Deal, Activity } from '@/app/api/types';
import type { PipelineValueByAssignee } from '@/app/api/reporting';
import { toast } from 'sonner';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

// Import extracted components and config
import { DashboardHero } from './dashboard/DashboardHero';
import { PipelineChart } from './dashboard/PipelineChart';
import { QuickNav } from './dashboard/QuickNav';
import { CopyStatsWidget } from './dashboard/CopyStatsWidget';
import { RecentActivity } from './dashboard/RecentActivity';
import { TeamPerformance } from './dashboard/TeamPerformance';
import { SalesWriter } from './dashboard/SalesWriter';
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
  const [recentActivity, setRecentActivity] = useState<Activity[]>([]);
  
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
  
  // Generated copy state
  const [generatedCopy, setGeneratedCopy] = useState<{
    subject: string;
    body: string;
    copyTypeLabel: string;
    copyTypeId: string;
  } | null>(null);
  const [copied, setCopied] = useState(false);
  
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
    getActivities()
      .then(guard((activities) => setRecentActivity(activities.slice(0, 5))))
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
            brandName: settings.brandName,
            brandTone: settings.brandTone,
            recipient,
            targetLanguage: language,
          })
        : await generateCopyWithRecipient({
            copyTypeId: selectedType,
            goal: goal || goals[0]!,
            context: context.trim() || undefined,
            length,
            brandName: settings.brandName,
            brandTone: settings.brandTone,
            recipient,
          });
      
      const copyTypeLabel = copyTypes.find((t) => t.id === selectedType)?.title ?? 'Copy';
      const langName = SUPPORTED_LANGUAGES.find(l => l.code === language)?.name ?? '';
      toast.success(messages.copy.generated + (language !== 'en' ? ` (${langName})` : ''));
      setGeneratedCopy({
        subject: result.subject,
        body: result.body,
        copyTypeLabel,
        copyTypeId: selectedType,
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

              {/* Intelligent Sales Writer - Extracted Component */}
              <SalesWriter
                selectedType={selectedType}
                setSelectedType={setSelectedType}
                goal={goal}
                setGoal={setGoal}
                context={context}
                setContext={setContext}
                length={length}
                setLength={setLength}
                language={language}
                setLanguage={setLanguage}
                isGenerating={isGenerating}
                onGenerate={handleGenerate}
                showRecipientPicker={showRecipientPicker}
                setShowRecipientPicker={setShowRecipientPicker}
                recipientType={recipientType}
                setRecipientType={setRecipientType}
                leads={leads}
                contacts={contacts}
                deals={deals}
                selectedRecipient={selectedRecipient}
                setSelectedRecipient={setSelectedRecipient}
                recipientSearch={recipientSearch}
                setRecipientSearch={setRecipientSearch}
              />

              {/* Generated Copy Display */}
              {generatedCopy && (
                <div className="bg-white dark:bg-slate-800 rounded-xl shadow-sm border border-slate-200 dark:border-slate-700 p-6">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                      Generated {generatedCopy.copyTypeLabel}
                    </h3>
                    <button
                      onClick={() => setGeneratedCopy(null)}
                      className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
                      aria-label="Close"
                    >
                      ×
                    </button>
                  </div>
                  
                  {generatedCopy.subject && (
                    <div className="mb-4">
                      <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                        Subject
                      </label>
                      <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg text-slate-900 dark:text-slate-100">
                        {generatedCopy.subject}
                      </div>
                    </div>
                  )}
                  
                  <div className="mb-4">
                    <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                      Body
                    </label>
                    <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg text-slate-900 dark:text-slate-100 whitespace-pre-wrap">
                      {generatedCopy.body}
                    </div>
                  </div>
                  
                  <div className="flex gap-3">
                    <button
                      onClick={async () => {
                        const textToCopy = generatedCopy.subject 
                          ? `${generatedCopy.subject}\n\n${generatedCopy.body}`
                          : generatedCopy.body;
                        await navigator.clipboard.writeText(textToCopy);
                        setCopied(true);
                        toast.success('Copied to clipboard');
                        setTimeout(() => setCopied(false), 2000);
                      }}
                      className="flex items-center gap-2 px-4 py-2 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-900 dark:text-slate-100 rounded-lg transition-colors"
                    >
                      {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                      {copied ? 'Copied!' : 'Copy'}
                    </button>
                    
                    {selectedRecipient && (
                      <button
                        onClick={async () => {
                          try {
                            await sendCopyToCrm({
                              objectType: selectedRecipient.type,
                              recordId: selectedRecipient.id,
                              recordName: selectedRecipient.name,
                              copy: generatedCopy.body,
                              copyTypeLabel: generatedCopy.copyTypeLabel,
                            });
                            toast.success(`Copy sent to ${selectedRecipient.name}`);
                          } catch {
                            toast.error('Failed to send copy to CRM');
                          }
                        }}
                        className="flex items-center gap-2 px-4 py-2 bg-orange-600 hover:bg-orange-500 text-white rounded-lg transition-colors"
                      >
                        <Send className="w-4 h-4" />
                        Send to CRM
                      </button>
                    )}
                  </div>
                </div>
              )}

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
