import { useMemo } from 'react';
import {
  RefreshCw,
  User,
  ArrowRight,
  Users,
  Building2,
  Handshake,
  Info,
  CheckCircle2,
  CircleDot,
  UserPlus,
  Check,
  ArrowRightCircle,
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
import type {
  ConvertLeadDialogProps,
  Company,
} from './types';

export interface ConvertLeadDialogPropsExtended extends ConvertLeadDialogProps {
  companies: Company[];
}

export function ConvertLeadDialog({
  lead,
  onClose,
  convertForm,
  setConvertForm,
  convertOptions,
  convertOptionsLoading,
  converting,
  onConvert,
  companies,
}: ConvertLeadDialogPropsExtended) {
  // Compute stage list and default stage
  const convertPipeline = convertForm.pipelineId
    ? convertOptions.pipelines.find((p) => p.id === convertForm.pipelineId)
    : convertOptions.pipelines[0];
  const convertStageList = useMemo(
    () => (convertPipeline?.dealStages ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder),
    [convertPipeline]
  );
  const defaultConvertStageId = convertStageList[0]?.id ?? 'Qualification';

  return (
    <Dialog open={!!lead} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[580px] p-0 gap-0 overflow-hidden">
        {/* Gradient Header */}
        <div className="relative overflow-hidden">
          {/* Animated background gradients */}
          <div className="absolute inset-0 bg-gradient-to-br from-emerald-600 via-emerald-500 to-teal-400" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
          <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
          <div className="absolute -bottom-10 -left-10 w-32 h-32 bg-teal-300/30 rounded-full blur-2xl" />

          {/* Header content */}
          <div className="relative px-6 py-5">
            <div className="flex items-center gap-4">
              <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                <RefreshCw className="w-6 h-6 text-white" />
              </div>
              <div>
                <DialogTitle className="text-xl font-bold text-white tracking-tight">
                  Convert Lead
                </DialogTitle>
                <p className="text-white/80 text-sm mt-0.5">
                  Qualify <span className="font-semibold">{lead?.name}</span> into a contact, company, or deal
                </p>
              </div>
            </div>

            {/* Conversion flow visualization */}
            <div className="mt-5 flex items-center justify-center gap-2 bg-white/10 backdrop-blur-sm rounded-xl px-4 py-3 border border-white/20">
              <div className="flex items-center gap-1.5 text-white/90">
                <div className="w-8 h-8 rounded-lg bg-white/20 flex items-center justify-center">
                  <User className="w-4 h-4" />
                </div>
                <span className="text-sm font-medium">Lead</span>
              </div>
              <ArrowRight className="w-5 h-5 text-white/60 mx-1" />
              <div className={`flex items-center gap-1.5 transition-all ${convertForm.createContact ? 'text-white' : 'text-white/40'}`}>
                <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all ${convertForm.createContact ? 'bg-white/30' : 'bg-white/10'}`}>
                  <Users className="w-4 h-4" />
                </div>
                <span className="text-sm font-medium">Contact</span>
              </div>
              {convertForm.createNewCompany && (
                <>
                  <span className="text-white/40 text-sm">+</span>
                  <div className="flex items-center gap-1.5 text-white">
                    <div className="w-8 h-8 rounded-lg bg-white/30 flex items-center justify-center">
                      <Building2 className="w-4 h-4" />
                    </div>
                    <span className="text-sm font-medium">Company</span>
                  </div>
                </>
              )}
              {convertForm.createDeal && (
                <>
                  <span className="text-white/40 text-sm">+</span>
                  <div className="flex items-center gap-1.5 text-white">
                    <div className="w-8 h-8 rounded-lg bg-white/30 flex items-center justify-center">
                      <Handshake className="w-4 h-4" />
                    </div>
                    <span className="text-sm font-medium">Deal</span>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Form Content */}
        {convertOptionsLoading ? (
          <div className="py-12 text-center">
            <div className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-emerald-100 mb-3">
              <RefreshCw className="w-6 h-6 text-emerald-600 animate-spin" />
            </div>
            <p className="text-slate-600 text-sm">Loading conversion options…</p>
          </div>
        ) : (
          <form id="convert-lead-form" onSubmit={onConvert} className="flex flex-col">
            {/* Scrollable content area */}
            <div className="p-6 space-y-5 max-h-[50vh] overflow-y-auto">
              {/* What is conversion? - Info card */}
              <div className="bg-gradient-to-r from-slate-50 to-emerald-50/30 rounded-xl p-4 border border-slate-100">
                <div className="flex gap-3">
                  <div className="shrink-0 w-8 h-8 rounded-lg bg-emerald-100 flex items-center justify-center">
                    <Info className="w-4 h-4 text-emerald-600" />
                  </div>
                  <div className="space-y-2 text-sm">
                    <p className="font-medium text-slate-700">What does conversion do?</p>
                    <p className="text-slate-600">
                      Conversion qualifies an <strong className="text-slate-700">unqualified lead</strong> into active records you can work with.
                    </p>
                    <ul className="space-y-1.5 text-slate-600">
                      <li className="flex items-start gap-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Contact</strong> — The qualified person you'll engage with</span>
                      </li>
                      <li className="flex items-start gap-2">
                        <CircleDot className="w-4 h-4 text-purple-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Company</strong> — Their organization, if applicable</span>
                      </li>
                      <li className="flex items-start gap-2">
                        <CircleDot className="w-4 h-4 text-amber-500 mt-0.5 shrink-0" />
                        <span><strong className="text-slate-700">Deal</strong> — A sales opportunity, if there's potential revenue</span>
                      </li>
                    </ul>
                  </div>
                </div>
              </div>

              {/* Contact Section - Primary */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-emerald-100 flex items-center justify-center">
                    <Users className="w-3.5 h-3.5 text-emerald-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Contact</span>
                  <span className="text-xs bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded-full font-medium">Typical</span>
                </div>

                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createContact: true, existingContactId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createContact
                      ? 'border-emerald-400 bg-emerald-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createContact ? 'bg-emerald-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <UserPlus className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createContact ? 'text-emerald-800' : 'text-slate-700'}`}>Create new contact</p>
                    <p className="text-xs text-slate-500 mt-0.5">From lead: {lead?.email}</p>
                  </div>
                  {convertForm.createContact && <Check className="w-5 h-5 text-emerald-600 shrink-0" />}
                </button>

                {convertOptions.contacts.length > 0 && (
                  <div className="relative">
                    <div className="absolute inset-0 flex items-center" aria-hidden>
                      <div className="w-full border-t border-slate-200" />
                    </div>
                    <div className="relative flex justify-center">
                      <span className="bg-white px-2 text-xs text-slate-400">or use existing</span>
                    </div>
                  </div>
                )}

                {!convertForm.createContact && convertOptions.contacts.length > 0 && (
                  <Select
                    value={convertForm.existingContactId || 'none'}
                    onValueChange={(v) => setConvertForm((f) => ({ ...f, existingContactId: v === 'none' ? undefined : v, createContact: false }))}
                  >
                    <SelectTrigger className="h-11 bg-slate-50/50 border-slate-200">
                      <SelectValue placeholder="Select existing contact" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">— Select a contact —</SelectItem>
                      {convertOptions.contacts.map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          <span className="flex items-center gap-2">
                            <Users className="w-3.5 h-3.5 text-slate-400" />
                            {c.name} {c.email ? `(${c.email})` : ''}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>

              {/* Company Section - Optional */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-purple-100 flex items-center justify-center">
                    <Building2 className="w-3.5 h-3.5 text-purple-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Company</span>
                  <span className="text-xs bg-slate-100 text-slate-500 px-2 py-0.5 rounded-full font-medium">Optional</span>
                </div>

                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createNewCompany: !f.createNewCompany, existingCompanyId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createNewCompany
                      ? 'border-purple-400 bg-purple-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createNewCompany ? 'bg-purple-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <Building2 className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createNewCompany ? 'text-purple-800' : 'text-slate-700'}`}>Create new company</p>
                    <p className="text-xs text-slate-500 mt-0.5">For B2B relationships and organization tracking</p>
                  </div>
                  {convertForm.createNewCompany && <Check className="w-5 h-5 text-purple-600 shrink-0" />}
                </button>

                {convertForm.createNewCompany && (
                  <div className="pl-4 border-l-2 border-purple-200">
                    <Label htmlFor="convert-company-name" className="text-sm text-slate-600">Company name</Label>
                    <Input
                      id="convert-company-name"
                      value={convertForm.newCompanyName}
                      onChange={(e) => setConvertForm((f) => ({ ...f, newCompanyName: e.target.value }))}
                      placeholder={lead?.companyId ? companies.find((c) => c.id === lead?.companyId)?.name : lead?.name}
                      className="mt-1.5 h-10 bg-white"
                    />
                  </div>
                )}

                {!convertForm.createNewCompany && companies.length > 0 && (
                  <>
                    <div className="relative">
                      <div className="absolute inset-0 flex items-center" aria-hidden>
                        <div className="w-full border-t border-slate-200" />
                      </div>
                      <div className="relative flex justify-center">
                        <span className="bg-white px-2 text-xs text-slate-400">or link to existing</span>
                      </div>
                    </div>
                    <Select
                      value={convertForm.existingCompanyId || 'none'}
                      onValueChange={(v) => setConvertForm((f) => ({ ...f, existingCompanyId: v === 'none' ? undefined : v }))}
                    >
                      <SelectTrigger className="h-10 bg-slate-50/50 border-slate-200">
                        <SelectValue placeholder="Select existing company" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">— Use lead's company if set —</SelectItem>
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
                  </>
                )}
              </div>

              {/* Deal Section - Optional */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-md bg-amber-100 flex items-center justify-center">
                    <Handshake className="w-3.5 h-3.5 text-amber-600" />
                  </div>
                  <span className="text-sm font-semibold text-slate-700">Deal / Opportunity</span>
                  <span className="text-xs bg-slate-100 text-slate-500 px-2 py-0.5 rounded-full font-medium">Optional</span>
                </div>

                <button
                  type="button"
                  onClick={() => setConvertForm((f) => ({ ...f, createDeal: !f.createDeal, existingDealId: undefined }))}
                  className={`w-full flex items-center gap-3 p-4 rounded-xl border-2 transition-all text-left ${
                    convertForm.createDeal
                      ? 'border-amber-400 bg-amber-50 shadow-sm'
                      : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-all ${
                    convertForm.createDeal ? 'bg-amber-500 text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    <Handshake className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`font-medium ${convertForm.createDeal ? 'text-amber-800' : 'text-slate-700'}`}>Create new deal</p>
                    <p className="text-xs text-slate-500 mt-0.5">Track this as a sales opportunity with potential revenue</p>
                  </div>
                  {convertForm.createDeal && <Check className="w-5 h-5 text-amber-600 shrink-0" />}
                </button>

                {convertForm.createDeal && (
                  <div className="pl-4 border-l-2 border-amber-200 space-y-3">
                    <div>
                      <Label htmlFor="convert-deal-name" className="text-sm text-slate-600">Deal name</Label>
                      <Input
                        id="convert-deal-name"
                        value={convertForm.dealName}
                        onChange={(e) => setConvertForm((f) => ({ ...f, dealName: e.target.value }))}
                        placeholder={lead?.name}
                        className="mt-1.5 h-10 bg-white"
                      />
                    </div>
                    <div>
                      <Label htmlFor="convert-deal-value" className="text-sm text-slate-600">Deal value</Label>
                      <Input
                        id="convert-deal-value"
                        value={convertForm.dealValue}
                        onChange={(e) => setConvertForm((f) => ({ ...f, dealValue: e.target.value }))}
                        placeholder="e.g. $10,000"
                        className="mt-1.5 h-10 bg-white"
                      />
                    </div>
                    {convertOptions.pipelines.length > 0 && (
                      <div>
                        <Label className="text-sm text-slate-600">Pipeline & Stage</Label>
                        <div className="flex gap-2 mt-1.5">
                          <Select
                            value={convertForm.pipelineId || convertOptions.pipelines[0]?.id}
                            onValueChange={(v) => {
                              const p = convertOptions.pipelines.find((x) => x.id === v);
                              const stages = (p?.dealStages ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder);
                              setConvertForm((f) => ({ ...f, pipelineId: v, dealStageId: stages[0]?.id }));
                            }}
                          >
                            <SelectTrigger className="flex-1 h-10 bg-white">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {convertOptions.pipelines.map((p) => (
                                <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <Select
                            value={convertForm.dealStageId || defaultConvertStageId}
                            onValueChange={(v) => setConvertForm((f) => ({ ...f, dealStageId: v }))}
                          >
                            <SelectTrigger className="flex-1 h-10 bg-white">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {convertStageList.map((s) => (
                                <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    )}
                    {convertStageList.length === 0 && (
                      <div>
                        <Label className="text-sm text-slate-600">Stage</Label>
                        <Select
                          value={convertForm.dealStage || 'Qualification'}
                          onValueChange={(v) => setConvertForm((f) => ({ ...f, dealStage: v }))}
                        >
                          <SelectTrigger className="mt-1.5 h-10 bg-white">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'].map((s) => (
                              <SelectItem key={s} value={s}>{s}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    )}
                  </div>
                )}

                {!convertForm.createDeal && convertOptions.deals.length > 0 && (
                  <>
                    <div className="relative">
                      <div className="absolute inset-0 flex items-center" aria-hidden>
                        <div className="w-full border-t border-slate-200" />
                      </div>
                      <div className="relative flex justify-center">
                        <span className="bg-white px-2 text-xs text-slate-400">or attach to existing</span>
                      </div>
                    </div>
                    <Select
                      value={convertForm.existingDealId || 'none'}
                      onValueChange={(v) => setConvertForm((f) => ({ ...f, existingDealId: v === 'none' ? undefined : v }))}
                    >
                      <SelectTrigger className="h-10 bg-slate-50/50 border-slate-200">
                        <SelectValue placeholder="Attach to existing deal" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">— None —</SelectItem>
                        {convertOptions.deals.map((d) => (
                          <SelectItem key={d.id} value={d.id}>
                            <span className="flex items-center gap-2">
                              <Handshake className="w-3.5 h-3.5 text-slate-400" />
                              {d.name} ({d.value})
                            </span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </>
                )}
              </div>
            </div>

            {/* Action Buttons - fixed at bottom */}
            <div className="flex items-center gap-3 px-6 pb-6 pt-4 border-t border-slate-100">
              <Button
                type="button"
                variant="outline"
                onClick={onClose}
                className="flex-1 h-11 border-slate-200 text-slate-600 hover:bg-slate-50"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={converting || (!convertForm.createContact && !convertForm.existingContactId)}
                className="flex-[2] h-11 bg-gradient-to-r from-emerald-600 to-teal-500 hover:from-emerald-700 hover:to-teal-600 text-white font-medium shadow-lg shadow-emerald-200/50 transition-all disabled:opacity-50"
              >
                {converting ? (
                  <span className="flex items-center gap-2">
                    <RefreshCw className="w-4 h-4 animate-spin" />
                    Converting…
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <ArrowRightCircle className="w-4 h-4" />
                    Convert Lead
                  </span>
                )}
              </Button>
            </div>
          </form>
        )}

        {/* Footer tip */}
        <div className="px-6 py-3 bg-gradient-to-r from-slate-50 to-emerald-50/30 border-t border-slate-100">
          <p className="text-xs text-slate-500 flex items-center gap-2">
            <span className="flex items-center justify-center w-4 h-4 rounded-full bg-emerald-100 text-emerald-500 text-[10px]">💡</span>
            The lead can be marked as converted. Organization ownership stays the same.
          </p>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default ConvertLeadDialog;
