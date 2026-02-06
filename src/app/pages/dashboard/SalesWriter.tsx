import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Sparkles,
  ChevronDown,
  ChevronUp,
  Lightbulb,
  User,
  X,
  Wand2,
  ArrowRight,
  Globe,
  Zap,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { copyTypes, goals } from './config';
import type { Lead, Contact, Deal } from '@/app/api/types';
import { SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/app/api/copyGenerator';

interface SalesWriterProps {
  selectedType: string;
  setSelectedType: (type: string) => void;
  goal: string;
  setGoal: (goal: string) => void;
  context: string;
  setContext: (context: string) => void;
  length: 'short' | 'medium' | 'long';
  setLength: (length: 'short' | 'medium' | 'long') => void;
  language: SupportedLanguage;
  setLanguage: (language: SupportedLanguage) => void;
  isGenerating: boolean;
  onGenerate: () => void;
  // Recipient props
  showRecipientPicker: boolean;
  setShowRecipientPicker: (show: boolean) => void;
  recipientType: 'lead' | 'contact' | 'deal' | null;
  setRecipientType: (type: 'lead' | 'contact' | 'deal' | null) => void;
  leads: Lead[];
  contacts: Contact[];
  deals: Deal[];
  selectedRecipient: {
    type: 'lead' | 'contact' | 'deal';
    id: string;
    name: string;
    email?: string;
    company?: string;
    dealStage?: string;
    dealValue?: string;
  } | null;
  setSelectedRecipient: (recipient: SalesWriterProps['selectedRecipient']) => void;
  recipientSearch: string;
  setRecipientSearch: (search: string) => void;
}

function CopyTypeCard({ 
  type, 
  isSelected, 
  onClick,
  index,
}: { 
  type: typeof copyTypes[number]; 
  isSelected: boolean; 
  onClick: () => void;
  index: number;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "group relative flex flex-col items-center text-center p-4 rounded-2xl transition-all duration-300",
        "border-2 overflow-hidden",
        isSelected
          ? "border-orange-400 bg-gradient-to-br from-orange-50 via-amber-50/50 to-orange-50 shadow-lg shadow-orange-500/10"
          : "border-slate-200/80 bg-white hover:border-slate-300 hover:shadow-xl hover:shadow-slate-200/50 hover:-translate-y-0.5"
      )}
      style={{ animationDelay: `${index * 60}ms` }}
    >
      {/* Selection indicator glow */}
      {isSelected && (
        <div className="absolute inset-0 bg-gradient-to-br from-orange-500/5 via-transparent to-amber-500/5" />
      )}
      
      {/* Hover gradient */}
      <div className={cn(
        "absolute inset-0 opacity-0 transition-opacity duration-300",
        !isSelected && "group-hover:opacity-100",
        `bg-gradient-to-br ${type.color}`
      )} style={{ opacity: 0 }} />
      
      {/* Icon */}
      <div className={cn(
        "relative w-14 h-14 rounded-2xl flex items-center justify-center mb-3 transition-all duration-300",
        isSelected 
          ? `bg-gradient-to-br ${type.color} shadow-lg shadow-orange-500/30 scale-105` 
          : "bg-gradient-to-br from-slate-100 to-slate-50 group-hover:scale-110 group-hover:shadow-md"
      )}>
        {/* Shine overlay */}
        <div className="absolute inset-0 rounded-2xl bg-gradient-to-tr from-white/40 via-white/10 to-transparent" />
        <type.icon className={cn(
          "relative w-6 h-6 transition-colors duration-300",
          isSelected ? "text-white" : "text-slate-500 group-hover:text-slate-700"
        )} />
      </div>
      
      {/* Label */}
      <span className={cn(
        "text-sm font-semibold transition-colors duration-300",
        isSelected ? "text-orange-700" : "text-slate-700 group-hover:text-slate-900"
      )}>
        {type.title}
      </span>
      
      {/* Description */}
      <span className={cn(
        "text-[11px] mt-1 transition-colors duration-300 hidden sm:block",
        isSelected ? "text-orange-600/70" : "text-slate-400 group-hover:text-slate-500"
      )}>
        {type.desc}
      </span>
      
      {/* Selected checkmark */}
      {isSelected && (
        <div className="absolute top-2 right-2 w-5 h-5 rounded-full bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-md">
          <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
      )}
    </button>
  );
}

export function SalesWriter({
  selectedType,
  setSelectedType,
  goal,
  setGoal,
  context,
  setContext,
  length,
  setLength,
  language,
  setLanguage,
  isGenerating,
  onGenerate,
  showRecipientPicker,
  setShowRecipientPicker,
  recipientType,
  setRecipientType,
  leads,
  contacts,
  deals,
  selectedRecipient,
  setSelectedRecipient,
  recipientSearch,
  setRecipientSearch,
}: SalesWriterProps) {
  return (
    <section className="relative overflow-hidden rounded-2xl border border-slate-200/60 bg-gradient-to-br from-white via-slate-50/30 to-white shadow-xl shadow-slate-200/40">
      {/* Animated background elements */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -right-40 w-80 h-80 bg-gradient-to-br from-orange-500/10 via-amber-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '4s' }} />
        <div className="absolute top-1/3 -left-32 w-64 h-64 bg-gradient-to-br from-violet-500/8 via-purple-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '5s', animationDelay: '1s' }} />
        <div className="absolute -bottom-24 right-1/4 w-56 h-56 bg-gradient-to-br from-cyan-500/8 via-blue-500/5 to-transparent rounded-full blur-3xl animate-pulse" style={{ animationDuration: '6s', animationDelay: '2s' }} />
        {/* Subtle pattern */}
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#8882_1px,transparent_1px),linear-gradient(to_bottom,#8882_1px,transparent_1px)] bg-[size:32px_32px] opacity-[0.02]" />
      </div>

      {/* Header */}
      <div className="relative px-6 py-5 border-b border-slate-100/80 bg-gradient-to-r from-white/80 via-slate-50/50 to-white/80 backdrop-blur-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="relative">
              <div className="absolute inset-0 bg-gradient-to-br from-orange-500 to-amber-500 rounded-2xl blur-lg opacity-50" />
              <div className="relative w-12 h-12 rounded-2xl bg-gradient-to-br from-orange-500 via-orange-400 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/30 ring-2 ring-white/50">
                <Sparkles className="w-6 h-6 text-white" />
              </div>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
                Intelligent Sales Writer
                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold bg-gradient-to-r from-orange-500 to-amber-500 text-white uppercase tracking-wider">
                  AI
                </span>
              </h2>
              <p className="text-sm text-slate-500">Create personalized sales content in seconds</p>
            </div>
          </div>
          <Link 
            to="/templates"
            className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-slate-500 hover:text-slate-700 bg-slate-100/80 hover:bg-slate-200/80 border border-slate-200/60 transition-all duration-200"
          >
            <Wand2 className="w-3.5 h-3.5" />
            Templates
          </Link>
        </div>
      </div>

      <div className="relative p-6">
        {/* Copy type selection */}
        <div className="mb-6">
          <div className="flex items-center justify-between mb-4">
            <label className="flex items-center gap-2 text-xs font-bold text-slate-500 uppercase tracking-wider">
              <Zap className="w-3.5 h-3.5 text-orange-500" />
              Choose copy type
            </label>
            <span className="text-[10px] text-slate-400">
              {selectedType ? '1 selected' : 'Select one'}
            </span>
          </div>
          
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
            {copyTypes.map((type, index) => (
              <CopyTypeCard
                key={type.id}
                type={type}
                isSelected={selectedType === type.id}
                onClick={() => setSelectedType(type.id)}
                index={index}
              />
            ))}
          </div>
        </div>

        {selectedType ? (
          <div className="space-y-5 pt-5 border-t border-slate-200/60">
            {/* Goal and Length row */}
            <div className="grid sm:grid-cols-2 gap-4">
              {/* Message Goal */}
              <div className="space-y-2">
                <label htmlFor="goal" className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                  Message goal
                </label>
                <div className="relative">
                  <select
                    id="goal"
                    value={goal}
                    onChange={(e) => setGoal(e.target.value)}
                    className="w-full h-12 pl-4 pr-10 rounded-xl border border-slate-200 bg-white text-slate-900 text-sm font-medium focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-all cursor-pointer hover:border-slate-300"
                  >
                    {goals.map((g) => (
                      <option key={g} value={g}>{g}</option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400 pointer-events-none" />
                </div>
              </div>

              {/* Length */}
              <div className="space-y-2">
                <label className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                  Length
                </label>
                <div className="flex gap-2">
                  {[
                    { id: 'short' as const, label: 'Short', desc: '~50 words' },
                    { id: 'medium' as const, label: 'Medium', desc: '~100 words' },
                    { id: 'long' as const, label: 'Long', desc: '~200 words' },
                  ].map((opt) => (
                    <button
                      key={opt.id}
                      type="button"
                      onClick={() => setLength(opt.id)}
                      className={cn(
                        "flex-1 py-3 px-3 rounded-xl border-2 text-sm font-semibold transition-all duration-200",
                        length === opt.id
                          ? "border-orange-400 bg-gradient-to-br from-orange-50 to-amber-50 text-orange-700 shadow-sm"
                          : "border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50"
                      )}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Context textarea */}
            <div className="space-y-2">
              <label htmlFor="context" className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                Optional context
                <span className="text-[10px] font-normal text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">Recommended</span>
              </label>
              <div className="relative">
                <textarea
                  id="context"
                  value={context}
                  onChange={(e) => setContext(e.target.value)}
                  placeholder="Add details like previous conversations, pain points, or key talking points..."
                  rows={3}
                  className="w-full px-4 py-3.5 rounded-xl border border-slate-200 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none resize-none transition-all hover:border-slate-300"
                />
              </div>
              <p className="flex items-center gap-2 text-xs text-slate-500">
                <Lightbulb className="w-4 h-4 text-amber-500" />
                More context = more relevant, on-brand copy
              </p>
            </div>

            {/* Language selector */}
            <div className="space-y-2">
              <label htmlFor="language" className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                <Globe className="w-4 h-4 text-slate-400" />
                Output language
              </label>
              <select
                id="language"
                value={language}
                onChange={(e) => setLanguage(e.target.value as SupportedLanguage)}
                className="w-full h-12 px-4 rounded-xl border border-slate-200 bg-white text-slate-900 text-sm font-medium focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-all cursor-pointer hover:border-slate-300"
              >
                {SUPPORTED_LANGUAGES.map((lang) => (
                  <option key={lang.code} value={lang.code}>
                    {lang.name}
                  </option>
                ))}
              </select>
              {language !== 'en' && (
                <p className="flex items-center gap-2 text-xs text-orange-600 font-medium">
                  <Sparkles className="w-4 h-4" />
                  AI will generate copy in {SUPPORTED_LANGUAGES.find(l => l.code === language)?.name}
                </p>
              )}
            </div>

            {/* Recipient selection */}
            <div className="rounded-xl border border-slate-200 bg-slate-50/50 overflow-hidden">
              <button
                type="button"
                onClick={() => setShowRecipientPicker(!showRecipientPicker)}
                className="w-full flex items-center justify-between px-4 py-3.5 text-left hover:bg-slate-100/80 transition-colors"
              >
                <div className="flex items-center gap-3">
                  <div className={cn(
                    "w-8 h-8 rounded-lg flex items-center justify-center transition-colors",
                    selectedRecipient ? "bg-orange-100 text-orange-600" : "bg-slate-200/80 text-slate-500"
                  )}>
                    <User className="w-4 h-4" />
                  </div>
                  <span className={cn(
                    "text-sm font-medium",
                    selectedRecipient ? "text-slate-900" : "text-slate-600"
                  )}>
                    {selectedRecipient 
                      ? `Personalizing for: ${selectedRecipient.name}` 
                      : 'Personalize for a recipient (optional)'}
                  </span>
                </div>
                <div className={cn(
                  "w-6 h-6 rounded-full flex items-center justify-center transition-colors",
                  showRecipientPicker ? "bg-slate-200" : "bg-transparent"
                )}>
                  {showRecipientPicker ? (
                    <ChevronUp className="w-4 h-4 text-slate-500" />
                  ) : (
                    <ChevronDown className="w-4 h-4 text-slate-400" />
                  )}
                </div>
              </button>
              
              {showRecipientPicker && (
                <div className="px-4 py-4 border-t border-slate-200 bg-white">
                  {selectedRecipient ? (
                    <div className="flex items-center justify-between p-3 bg-gradient-to-br from-orange-50 to-amber-50 border border-orange-200 rounded-xl">
                      <div>
                        <p className="text-sm font-semibold text-slate-900">{selectedRecipient.name}</p>
                        <p className="text-xs text-slate-500">
                          {selectedRecipient.type === 'lead' ? 'Lead' : selectedRecipient.type === 'contact' ? 'Contact' : 'Deal'}
                          {selectedRecipient.email && ` • ${selectedRecipient.email}`}
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setSelectedRecipient(null)}
                        className="p-2 text-slate-400 hover:text-slate-600 hover:bg-white rounded-lg transition-colors"
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
                              "flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold transition-all duration-200",
                              recipientType === type.id
                                ? "bg-gradient-to-br from-orange-500 to-amber-500 text-white shadow-md"
                                : "bg-slate-100 border border-slate-200 text-slate-600 hover:border-slate-300 hover:bg-slate-50"
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
                            className="w-full px-4 py-2.5 mb-3 text-sm border border-slate-200 rounded-xl focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-all"
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
                                  className="w-full flex items-center justify-between p-3 text-left text-sm rounded-xl hover:bg-slate-50 transition-colors border border-transparent hover:border-slate-200"
                                >
                                  <div>
                                    <p className="font-semibold text-slate-900">{item.name}</p>
                                    {'email' in item && item.email && (
                                      <p className="text-xs text-slate-500">{item.email}</p>
                                    )}
                                    {recipientType === 'deal' && (
                                      <p className="text-xs text-slate-500">
                                        {(item as Deal).stage} • {(item as Deal).value}
                                      </p>
                                    )}
                                  </div>
                                  <ArrowRight className="w-4 h-4 text-slate-300" />
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

            {/* Generate button */}
            <button
              type="button"
              onClick={onGenerate}
              disabled={isGenerating}
              className={cn(
                "group relative w-full h-14 px-6 rounded-2xl font-bold text-white flex items-center justify-center gap-3 transition-all duration-300 overflow-hidden",
                "bg-gradient-to-r from-orange-500 via-orange-400 to-amber-500",
                "hover:from-orange-600 hover:via-orange-500 hover:to-amber-600",
                "shadow-xl shadow-orange-500/25 hover:shadow-orange-500/40",
                "hover:scale-[1.02] active:scale-[0.98]",
                "disabled:from-slate-300 disabled:via-slate-400 disabled:to-slate-300 disabled:shadow-none disabled:cursor-not-allowed disabled:hover:scale-100"
              )}
            >
              {/* Shine effect */}
              <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent -translate-x-full group-hover:translate-x-full transition-transform duration-700" />
              
              {isGenerating ? (
                <>
                  <span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  <span className="text-base">Generating magic...</span>
                </>
              ) : (
                <>
                  <Sparkles className="w-5 h-5" />
                  <span className="text-base">Generate Copy</span>
                  <ArrowRight className="w-5 h-5 transition-transform group-hover:translate-x-1" />
                </>
              )}
            </button>
          </div>
        ) : (
          /* Empty state when no type selected */
          <div className="relative rounded-2xl border border-slate-200/60 bg-gradient-to-br from-slate-50/80 via-white to-slate-50/80 p-10 text-center overflow-hidden">
            {/* Background decoration */}
            <div className="absolute inset-0 overflow-hidden pointer-events-none">
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-gradient-to-br from-orange-500/5 to-amber-500/5 rounded-full blur-3xl" />
            </div>
            
            <div className="relative">
              <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-3xl bg-gradient-to-br from-orange-100 to-amber-50 ring-1 ring-orange-200/50 shadow-lg shadow-orange-500/10">
                <Sparkles className="h-10 w-10 text-orange-500" />
              </div>
              <h3 className="mt-6 text-xl font-bold text-slate-900">Choose a copy type to get started</h3>
              <p className="mt-2 text-sm text-slate-500 max-w-sm mx-auto leading-relaxed">
                Select one of the formats above to generate sales-ready copy in seconds.
              </p>
              <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
                <Link
                  to="/templates"
                  className="inline-flex items-center justify-center gap-2 rounded-xl px-6 py-3 text-sm font-bold text-white shadow-lg shadow-orange-500/25 transition-all duration-200 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 hover:shadow-orange-500/40 hover:scale-105"
                >
                  <Wand2 className="w-4 h-4" />
                  Browse templates
                </Link>
              </div>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

export default SalesWriter;
