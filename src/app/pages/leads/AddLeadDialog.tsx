import { useState, useEffect } from 'react';
import {
  User, Building2, Mail, Phone, Briefcase, Sparkles, ChevronRight, Check, Tag, Activity, UserPlus
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogTitle,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import type { AddLeadDialogProps, WizardStep } from './types';
import { WIZARD_STEPS, SOURCE_ICONS, STATUS_COLORS, LIFECYCLE_STAGES } from './config';

export function AddLeadDialog({
  open,
  onOpenChange,
  editingLead,
  form,
  setForm,
  companies,
  sourceOptions,
  statusOptions,
  onSubmit,
  saving,
}: AddLeadDialogProps) {
  const [activeStep, setActiveStep] = useState<WizardStep>('contact');
  const isContactComplete = form.name.trim() && form.email.trim();

  // Reset to first step when dialog opens
  useEffect(() => {
    if (open) {
      setActiveStep('contact');
    }
  }, [open]);

  // Get current step index
  const currentStepIndex = WIZARD_STEPS.findIndex(s => s.id === activeStep);
  const currentStepConfig = WIZARD_STEPS[currentStepIndex];

  // Navigation helpers
  const goToNextStep = () => {
    if (currentStepIndex < WIZARD_STEPS.length - 1) {
      const nextStep = WIZARD_STEPS[currentStepIndex + 1];
      if (nextStep) {
        setActiveStep(nextStep.id);
      }
    }
  };

  const goToPrevStep = () => {
    if (currentStepIndex > 0) {
      const prevStep = WIZARD_STEPS[currentStepIndex - 1];
      if (prevStep) {
        setActiveStep(prevStep.id);
      }
    }
  };

  const isFirstStep = currentStepIndex === 0;
  const isLastStep = currentStepIndex === WIZARD_STEPS.length - 1;

  // Check if step is completed
  const isStepComplete = (stepId: WizardStep): boolean => {
    switch (stepId) {
      case 'contact':
        return !!(form.name.trim() && form.email.trim());
      case 'company':
        return true; // Optional step, always "complete"
      case 'qualification':
        return !!form.status; // Has a default, so always complete
      case 'notes':
        return true; // Optional step
      default:
        return false;
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Gradient Header */}
        <div className="relative overflow-hidden shrink-0">
          {/* Animated background gradients */}
          <div className="absolute inset-0 bg-gradient-to-br from-orange-500 via-orange-400 to-amber-400" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
          <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
          <div className="absolute -bottom-10 -left-10 w-32 h-32 bg-amber-300/30 rounded-full blur-2xl" />
          
          {/* Header content */}
          <div className="relative px-6 py-5">
            <div className="flex items-center gap-4">
              <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                <UserPlus className="w-6 h-6 text-white" />
              </div>
              <div>
                <DialogTitle className="text-xl font-bold text-white tracking-tight">
                  {editingLead ? 'Edit Lead' : 'Add New Lead'}
                </DialogTitle>
                <p className="text-white/80 text-sm mt-0.5">
                  {editingLead ? 'Update lead information' : 'Capture a new potential customer'}
                </p>
              </div>
            </div>
            
            {/* Progress indicator - Horizontal stepper */}
            <div className="mt-5">
              {/* Progress bar background */}
              <div className="relative">
                <div className="absolute top-4 left-0 right-0 h-0.5 bg-white/20" />
                <div 
                  className="absolute top-4 left-0 h-0.5 bg-white transition-all duration-300"
                  style={{ width: `${(currentStepIndex / (WIZARD_STEPS.length - 1)) * 100}%` }}
                />
                
                {/* Step indicators */}
                <div className="relative flex justify-between">
                  {WIZARD_STEPS.map((step, index) => {
                    const isActive = step.id === activeStep;
                    const isPast = index < currentStepIndex;
                    const StepIcon = step.icon;
                    
                    return (
                      <button
                        key={step.id}
                        type="button"
                        onClick={() => {
                          // Only allow going back or to completed steps
                          const prevStep = index > 0 ? WIZARD_STEPS[index - 1] : undefined;
                          if (index <= currentStepIndex || (prevStep && isStepComplete(prevStep.id))) {
                            setActiveStep(step.id);
                          }
                        }}
                        className={`flex flex-col items-center gap-1.5 transition-all ${
                          (() => {
                            const prevStep = index > 0 ? WIZARD_STEPS[index - 1] : undefined;
                            return index <= currentStepIndex || (prevStep && isStepComplete(prevStep.id));
                          })()
                            ? 'cursor-pointer'
                            : 'cursor-not-allowed opacity-50'
                        }`}
                      >
                        <div className={`w-8 h-8 rounded-full flex items-center justify-center transition-all ${
                          isActive
                            ? 'bg-white text-orange-600 shadow-lg scale-110'
                            : isPast
                              ? 'bg-white/90 text-orange-600'
                              : 'bg-white/20 text-white'
                        }`}>
                          {isPast ? (
                            <Check className="w-4 h-4" />
                          ) : (
                            <StepIcon className="w-4 h-4" />
                          )}
                        </div>
                        <span className={`text-xs font-medium transition-all ${
                          isActive ? 'text-white' : 'text-white/70'
                        }`}>
                          {step.shortLabel}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Step description banner */}
        <div className="px-6 py-3 bg-gradient-to-r from-orange-50 to-amber-50/50 border-b border-orange-100 shrink-0">
          <div className="flex items-center gap-3">
            <div className="flex items-center justify-center w-8 h-8 rounded-lg bg-orange-100 text-orange-600">
              {currentStepConfig && <currentStepConfig.icon className="w-4 h-4" />}
            </div>
            <div>
              <p className="text-sm font-semibold text-slate-800">
                Step {currentStepIndex + 1}: {currentStepConfig?.label}
              </p>
              <p className="text-xs text-slate-600">{currentStepConfig?.description}</p>
            </div>
          </div>
        </div>

        {/* Form Content - Scrollable */}
        <form onSubmit={onSubmit} className="flex-1 overflow-y-auto">
          <div className="p-6">
            {/* Step 1: Contact Info Section */}
            <div className={`space-y-4 ${activeStep === 'contact' ? 'block' : 'hidden'}`}>
              {/* Step explanation */}
              <div className="bg-blue-50 rounded-xl p-4 border border-blue-100 mb-5">
                <p className="text-sm text-blue-800">
                  <strong>Why this matters:</strong> Contact information is essential for reaching out to your lead. 
                  A valid email is required as it's the primary way to communicate and track engagement.
                </p>
              </div>

              {/* Name Field */}
              <div className="group">
                <Label htmlFor="lead-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-orange-100 text-orange-600">
                    <User className="w-3.5 h-3.5" />
                  </div>
                  Full Name <span className="text-orange-500">*</span>
                </Label>
                <Input
                  id="lead-name"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  placeholder="Enter lead's full name"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100 transition-all"
                  required
                />
                <p className="text-xs text-slate-500 mt-1.5">The person's full name as they prefer to be addressed</p>
              </div>

              {/* Email Field */}
              <div className="group">
                <Label htmlFor="lead-email" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-100 text-blue-600">
                    <Mail className="w-3.5 h-3.5" />
                  </div>
                  Email Address <span className="text-orange-500">*</span>
                </Label>
                <Input
                  id="lead-email"
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                  placeholder="lead@company.com"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100 transition-all"
                  required
                />
                <p className="text-xs text-slate-500 mt-1.5">Primary email for communication and follow-ups</p>
              </div>

              {/* Phone Field */}
              <div className="group">
                <Label htmlFor="lead-phone" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                    <Phone className="w-3.5 h-3.5" />
                  </div>
                  Phone Number
                  <span className="text-slate-400 text-xs font-normal ml-1">(optional)</span>
                </Label>
                <Input
                  id="lead-phone"
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                  placeholder="+1 (555) 000-0000"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100 transition-all"
                />
                <p className="text-xs text-slate-500 mt-1.5">For direct calls or SMS follow-ups</p>
              </div>
            </div>

            {/* Step 2: Company & Source Section */}
            <div className={`space-y-4 ${activeStep === 'company' ? 'block' : 'hidden'}`}>
              {/* Step explanation */}
              <div className="bg-purple-50 rounded-xl p-4 border border-purple-100 mb-5">
                <p className="text-sm text-purple-800">
                  <strong>Why this matters:</strong> Understanding where leads come from helps you optimize marketing spend. 
                  Company information enables B2B relationship tracking and account-based selling.
                </p>
              </div>

              {/* Company Select */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-purple-100 text-purple-600">
                    <Building2 className="w-3.5 h-3.5" />
                  </div>
                  Company
                  <span className="text-slate-400 text-xs font-normal ml-1">(optional)</span>
                </Label>
                <Select
                  value={form.companyId || 'none'}
                  onValueChange={(v) => setForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100">
                    <SelectValue placeholder="No company" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-500">No company</span>
                    </SelectItem>
                    {companies.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        <span className="flex items-center gap-2">
                          <Building2 className="w-3.5 h-3.5 text-slate-400" />
                          {c.name}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-slate-500 mt-1.5">Link to an existing company or leave empty for individual leads</p>
              </div>

              {/* Source Select - Enhanced with visual cards */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-600">
                    <Sparkles className="w-3.5 h-3.5" />
                  </div>
                  Lead Source
                </Label>
                <p className="text-xs text-slate-500 mb-3">How did this lead find you? This helps measure marketing ROI.</p>
                <div className="grid grid-cols-3 gap-2">
                  {sourceOptions.slice(0, 6).map((s) => {
                    const isSelected = (form.leadSourceId || form.source) === s.id || (form.leadSourceId || form.source) === s.name;
                    return (
                      <button
                        key={s.id}
                        type="button"
                        onClick={() => setForm((f) => ({ ...f, leadSourceId: s.id, source: s.name }))}
                        className={`flex flex-col items-center gap-1.5 p-3 rounded-lg border-2 transition-all ${
                          isSelected
                            ? 'border-orange-400 bg-orange-50 shadow-sm'
                            : 'border-slate-100 bg-slate-50/50 hover:border-slate-200 hover:bg-slate-100/50'
                        }`}
                      >
                        <span className="text-lg">{SOURCE_ICONS[s.name.toLowerCase()] || '📌'}</span>
                        <span className={`text-xs font-medium capitalize ${isSelected ? 'text-orange-700' : 'text-slate-600'}`}>
                          {s.name}
                        </span>
                      </button>
                    );
                  })}
                </div>
                {sourceOptions.length > 6 && (
                  <Select
                    value={form.leadSourceId || form.source || sourceOptions[0]?.id}
                    onValueChange={(v) => {
                      const opt = sourceOptions.find((s) => s.id === v);
                      setForm((f) => ({ ...f, leadSourceId: v, source: opt?.name ?? v }));
                    }}
                  >
                    <SelectTrigger className="h-9 mt-2 bg-slate-50/50 border-slate-200 text-sm">
                      <SelectValue placeholder="More sources..." />
                    </SelectTrigger>
                    <SelectContent>
                      {sourceOptions.map((s) => (
                        <SelectItem key={s.id} value={s.id}>
                          <span className="flex items-center gap-2">
                            <span>{SOURCE_ICONS[s.name.toLowerCase()] || '📌'}</span>
                            {s.name}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            </div>

            {/* Step 3: Qualification Section */}
            <div className={`space-y-4 ${activeStep === 'qualification' ? 'block' : 'hidden'}`}>
              {/* Step explanation */}
              <div className="bg-cyan-50 rounded-xl p-4 border border-cyan-100 mb-5">
                <p className="text-sm text-cyan-800">
                  <strong>Why this matters:</strong> Lead qualification helps prioritize your sales efforts. 
                  Status tracking shows where each lead is in their journey, while scoring identifies high-potential opportunities.
                </p>
              </div>

              {/* Status Select - Enhanced Pipeline Style */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-1">
                  <div className="flex items-center justify-center w-6 h-6 rounded-lg bg-gradient-to-br from-cyan-500 to-blue-500 text-white shadow-sm">
                    <Activity className="w-3.5 h-3.5" />
                  </div>
                  Lead Status
                </Label>
                <p className="text-xs text-slate-500 mb-4">Select where this lead is in your pipeline</p>
                
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-2">
                  {statusOptions.map((s) => {
                    const isSelected = (form.leadStatusId || form.status) === s.id || (form.leadStatusId || form.status) === s.name;
                    const colors = STATUS_COLORS[s.name] || { bg: 'bg-slate-50', text: 'text-slate-600', border: 'border-slate-200', selectedBg: 'bg-slate-400', selectedBorder: 'border-slate-400', icon: '•' };
                    return (
                      <button
                        key={s.id}
                        type="button"
                        onClick={() => setForm((f) => ({ ...f, leadStatusId: s.id, status: s.name }))}
                        className={`relative flex flex-col items-center gap-1.5 px-3 py-3 rounded-xl border-2 transition-all duration-200 ${
                          isSelected
                            ? `${colors.bg} ${colors.border} ${colors.text} shadow-md ring-2 ring-offset-1 ${colors.border.replace('border-', 'ring-')}/30`
                            : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50 text-slate-600'
                        }`}
                      >
                        <span className={`flex items-center justify-center w-8 h-8 rounded-full text-sm transition-all ${
                          isSelected 
                            ? `${colors.selectedBg} text-white shadow-sm` 
                            : 'bg-slate-100 text-slate-400'
                        }`}>
                          {isSelected ? <Check className="w-4 h-4" /> : colors.icon}
                        </span>
                        <span className={`text-xs font-medium text-center leading-tight ${isSelected ? colors.text : 'text-slate-600'}`}>
                          {s.name}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Lifecycle Stage Select */}
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-indigo-100 text-indigo-600">
                    <Tag className="w-3.5 h-3.5" />
                  </div>
                  Lifecycle Stage
                  <span className="text-slate-400 text-xs font-normal ml-1">(optional)</span>
                </Label>
                <Select
                  value={form.lifecycleStage || 'none'}
                  onValueChange={(v) => setForm((f) => ({ ...f, lifecycleStage: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100">
                    <SelectValue placeholder="Select lifecycle stage" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">
                      <span className="text-slate-400">— None —</span>
                    </SelectItem>
                    {LIFECYCLE_STAGES.map((stage) => (
                      <SelectItem key={stage} value={stage}>
                        {stage}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-slate-500 mt-1.5">
                  <strong>MQL</strong> = Marketing Qualified, <strong>SQL</strong> = Sales Qualified, <strong>Hot</strong> = Ready to buy
                </p>
              </div>

              {/* Lead Score */}
              <div className="group">
                <Label htmlFor="lead-score" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-600">
                    <Sparkles className="w-3.5 h-3.5" />
                  </div>
                  Lead Score
                  <span className="text-slate-400 text-xs font-normal ml-1">(0-100, optional)</span>
                </Label>
                <Input
                  id="lead-score"
                  type="number"
                  min="0"
                  max="100"
                  value={form.leadScore}
                  onChange={(e) => setForm((f) => ({ ...f, leadScore: e.target.value }))}
                  placeholder="e.g. 75"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-orange-300 focus:ring-orange-100 transition-all"
                />
                <p className="text-xs text-slate-500 mt-1.5">
                  Higher scores (70+) indicate high-value leads that should be prioritized
                </p>
              </div>
            </div>

            {/* Step 4: Notes Section */}
            <div className={`space-y-4 ${activeStep === 'notes' ? 'block' : 'hidden'}`}>
              {/* Step explanation */}
              <div className="bg-blue-50 rounded-xl p-4 border border-blue-100 mb-5">
                <p className="text-sm text-blue-800">
                  <strong>Why this matters:</strong> Detailed notes provide context for future conversations. 
                  Record first impressions, specific needs mentioned, or any relevant background information.
                </p>
              </div>

              {/* Description */}
              <div className="group">
                <Label htmlFor="lead-description" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-100 text-blue-600">
                    <Briefcase className="w-3.5 h-3.5" />
                  </div>
                  Description / Notes
                  <span className="text-slate-400 text-xs font-normal ml-1">(optional)</span>
                </Label>
                <textarea
                  id="lead-description"
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Add any notes or context about this lead...

Examples:
• Met at trade show, interested in enterprise plan
• Referred by existing customer John Smith
• Downloaded whitepaper on automation"
                  rows={6}
                  className="w-full px-4 py-2.5 rounded-lg border border-slate-200 bg-slate-50/50 text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-300 transition-colors resize-none"
                />
              </div>

              {/* Summary card */}
              <div className="bg-gradient-to-r from-slate-50 to-orange-50/30 rounded-xl p-4 border border-slate-200">
                <p className="text-sm font-medium text-slate-700 mb-3">Lead Summary</p>
                <div className="grid grid-cols-2 gap-3 text-sm">
                  <div>
                    <span className="text-slate-500">Name:</span>
                    <span className="ml-2 font-medium text-slate-700">{form.name || '—'}</span>
                  </div>
                  <div>
                    <span className="text-slate-500">Email:</span>
                    <span className="ml-2 font-medium text-slate-700 truncate">{form.email || '—'}</span>
                  </div>
                  <div>
                    <span className="text-slate-500">Status:</span>
                    <span className="ml-2 font-medium text-slate-700">{form.status || 'New'}</span>
                  </div>
                  <div>
                    <span className="text-slate-500">Source:</span>
                    <span className="ml-2 font-medium text-slate-700">{form.source || 'Manual'}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Action Buttons - Fixed at bottom */}
          <div className="sticky bottom-0 px-6 py-4 bg-white border-t border-slate-100 shrink-0">
            <div className="flex items-center gap-3">
              {!isFirstStep && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={goToPrevStep}
                  className="h-11 px-6 border-slate-200 text-slate-600 hover:bg-slate-50"
                >
                  Back
                </Button>
              )}
              
              {isFirstStep && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => onOpenChange(false)}
                  className="h-11 px-6 border-slate-200 text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </Button>
              )}

              <div className="flex-1" />

              {!isLastStep ? (
                <Button
                  type="button"
                  onClick={goToNextStep}
                  disabled={activeStep === 'contact' && !isContactComplete}
                  className="h-11 px-6 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 text-white font-medium shadow-lg shadow-orange-200/50 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Continue
                  <ChevronRight className="w-4 h-4 ml-2" />
                </Button>
              ) : (
                <Button
                  type="submit"
                  disabled={saving || !form.name.trim() || !form.email.trim()}
                  className="h-11 px-6 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 text-white font-medium shadow-lg shadow-orange-200/50 transition-all disabled:opacity-50"
                >
                  {saving ? (
                    <span className="flex items-center gap-2">
                      <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                      </svg>
                      {editingLead ? 'Updating...' : 'Creating...'}
                    </span>
                  ) : (
                    <span className="flex items-center gap-2">
                      {editingLead ? 'Update Lead' : 'Create Lead'}
                      <Sparkles className="w-4 h-4" />
                    </span>
                  )}
                </Button>
              )}
            </div>
          </div>
        </form>

        {/* Quick tip footer */}
        <div className="px-6 py-3 bg-gradient-to-r from-slate-50 to-orange-50/30 border-t border-slate-100 shrink-0">
          <p className="text-xs text-slate-500 flex items-center gap-2">
            <span className="flex items-center justify-center w-4 h-4 rounded-full bg-orange-100 text-orange-500 text-[10px]">💡</span>
            {activeStep === 'contact' && 'Name and email are required to create a lead'}
            {activeStep === 'company' && 'Tracking lead sources helps optimize your marketing efforts'}
            {activeStep === 'qualification' && 'Qualified leads convert 3x better than unqualified ones'}
            {activeStep === 'notes' && 'Good notes help your team pick up conversations seamlessly'}
          </p>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default AddLeadDialog;
