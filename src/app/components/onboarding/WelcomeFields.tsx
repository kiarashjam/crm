import { AnimatePresence, motion } from 'motion/react';
import { CalendarClock, CalendarDays, Code2, Crown, HelpCircle, Languages, MessageSquareHeart, Settings2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import type { OnboardingSettings } from '@/app/api/onboarding';
import type { WelcomeDraft } from './welcome';

const EASE = [0.16, 1, 0.3, 1] as const;

/** Placeholder chips: clicking one adds it where the cursor is, or at the end. */
export function PlaceholderChips({ fields, tone, onPick }: { fields: string[]; tone: 'orange' | 'violet'; onPick: (token: string) => void }) {
  return (
    <div className="flex flex-wrap gap-1">
      {fields.map((f) => (
        <button
          key={f}
          type="button"
          onClick={() => onPick(`{{${f}}}`)}
          className={cn(
            'rounded-md border px-1.5 py-0.5 font-mono text-[11px] transition hover:-translate-y-px',
            tone === 'orange'
              ? 'border-orange-200 bg-orange-50 text-orange-700 hover:bg-orange-100'
              : 'border-violet-200 bg-violet-50 text-violet-700 hover:bg-violet-100',
          )}
        >
          {`{{${f}}}`}
        </button>
      ))}
    </div>
  );
}

/** Insert text at the cursor of the textarea with this id, or append it. */
export function insertAt(id: string, current: string, token: string): string {
  const el = typeof document !== 'undefined' ? (document.getElementById(id) as HTMLTextAreaElement | HTMLInputElement | null) : null;
  if (!el || el.selectionStart == null) return current + token;
  const start = el.selectionStart;
  const end = el.selectionEnd ?? start;
  const next = current.slice(0, start) + token + current.slice(end);
  requestAnimationFrame(() => {
    el.focus();
    el.setSelectionRange(start + token.length, start + token.length);
  });
  return next;
}

/**
 * The welcome a new customer gets when their lead becomes a deal: a message, when they
 * start, the language, and the email itself. Shared by the convert dialog and the deal.
 */
export function WelcomeFields({
  draft,
  onChange,
  settings,
  idPrefix = 'welcome',
  showToggle = true,
}: {
  draft: WelcomeDraft;
  onChange: (next: WelcomeDraft) => void;
  settings: OnboardingSettings | null;
  idPrefix?: string;
  /** In the convert dialog, sending is a choice; on a deal, it is what the dialog is for. */
  showToggle?: boolean;
}) {
  const set = (patch: Partial<WelcomeDraft>) => onChange({ ...draft, ...patch });
  const configured = Boolean(settings?.configured);
  const open = !showToggle || draft.send;
  const hasTemplate = Boolean(settings?.emailHtml);

  return (
    <div className="space-y-3">
      {showToggle && (
        <div className="flex items-center gap-2">
          <div className="flex h-6 w-6 items-center justify-center rounded-md bg-gradient-to-br from-orange-400 to-pink-500 text-white shadow-sm">
            <MessageSquareHeart className="h-3.5 w-3.5" />
          </div>
          <span className="text-sm font-semibold text-slate-700 dark:text-slate-200">Welcome &amp; portal</span>
          <span className="rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-700">New</span>
          {configured && (
            <label className="ml-auto flex cursor-pointer items-center gap-2 text-xs text-slate-500">
              Send welcome
              <Switch checked={draft.send} onCheckedChange={(v) => set({ send: v })} aria-label="Send the welcome with this conversion" />
            </label>
          )}
        </div>
      )}

      {!configured ? (
        <div className="flex items-start gap-3 rounded-xl border border-dashed border-orange-200 bg-orange-50/50 p-3 text-sm">
          <Settings2 className="mt-0.5 h-4 w-4 shrink-0 text-orange-500" />
          <p className="text-slate-600">
            Hand new customers straight to your member portal: it opens their account and sends your welcome email with a link to set their password.{' '}
            <Link to="/settings?tab=welcome" className="font-medium text-orange-700 hover:underline">Connect your portal</Link>
          </p>
        </div>
      ) : (
        <AnimatePresence initial={false}>
          {open && (
            <motion.div
              key="fields"
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.28, ease: EASE }}
              className="overflow-hidden"
            >
              <div className={cn('space-y-4', showToggle && 'rounded-xl border-2 border-orange-200/70 bg-gradient-to-br from-orange-50/60 via-white to-pink-50/40 p-4')}>
                <div>
                  <Label htmlFor={`${idPrefix}-message`} className="text-sm text-slate-600">Welcome message</Label>
                  <Textarea
                    id={`${idPrefix}-message`}
                    value={draft.welcomeMessage}
                    onChange={(e) => set({ welcomeMessage: e.target.value })}
                    placeholder="We're delighted to welcome you to the club…"
                    rows={3}
                    maxLength={4000}
                    className="mt-1.5 bg-white"
                  />
                  <p className="mt-1 text-xs text-slate-500">Goes into the email as {'{{welcomeMessage}}'}, as plain text.</p>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <Label htmlFor={`${idPrefix}-start`} className="flex items-center gap-1.5 text-sm text-slate-600">
                      <CalendarDays className="h-3.5 w-3.5" /> Membership starts
                    </Label>
                    <div className="mt-1.5 flex items-center gap-2">
                      {/* Typing a date decides it; "to be decided" takes the date off. */}
                      <Input
                        id={`${idPrefix}-start`}
                        type="date"
                        value={draft.undetermined ? '' : draft.startsOn}
                        onChange={(e) => set({ startsOn: e.target.value, undetermined: !e.target.value })}
                        className="h-10 bg-white"
                      />
                    </div>
                    <button
                      type="button"
                      onClick={() => set(draft.undetermined
                        ? { undetermined: false }
                        : { undetermined: true, startsOn: '' })}
                      aria-pressed={draft.undetermined}
                      className={cn(
                        'mt-2 inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition',
                        draft.undetermined
                          ? 'border-amber-300 bg-amber-100 text-amber-800 shadow-sm'
                          : 'border-slate-200 bg-white text-slate-500 hover:border-slate-300',
                      )}
                    >
                      {draft.undetermined ? <HelpCircle className="h-3.5 w-3.5" /> : <CalendarClock className="h-3.5 w-3.5" />}
                      {draft.undetermined ? 'Start to be decided' : 'Not decided yet?'}
                    </button>
                  </div>
                  <div className="space-y-3">
                    <div>
                      <Label className="flex items-center gap-1.5 text-sm text-slate-600"><Languages className="h-3.5 w-3.5" /> Email language</Label>
                      <div className="mt-1.5 inline-flex rounded-lg border border-slate-200 bg-white p-0.5" role="radiogroup" aria-label="Email language">
                        {(['fr', 'en'] as const).map((l) => (
                          <button
                            key={l}
                            type="button"
                            role="radio"
                            aria-checked={draft.language === l}
                            onClick={() => set({ language: l })}
                            className={cn(
                              'rounded-md px-3 py-1 text-sm font-medium transition',
                              draft.language === l ? 'bg-slate-900 text-white shadow' : 'text-slate-500 hover:text-slate-800',
                            )}
                          >
                            {l === 'fr' ? 'Français' : 'English'}
                          </button>
                        ))}
                      </div>
                    </div>
                    <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
                      <Switch checked={draft.addToClubMembers} onCheckedChange={(v) => set({ addToClubMembers: v })} aria-label="Add to club members" />
                      <Crown className="h-3.5 w-3.5 text-indigo-500" /> Add to club members
                    </label>
                  </div>
                </div>

                <div className="rounded-lg border border-slate-200 bg-white/80 p-3">
                  <div className="flex items-center gap-2">
                    <Code2 className="h-4 w-4 text-violet-500" />
                    <p className="flex-1 text-sm text-slate-700">
                      {draft.customEmail
                        ? 'This email only'
                        : hasTemplate
                          ? 'Your welcome template'
                          : "The portal's own welcome design"}
                    </p>
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-500">
                      Own email
                      <Switch
                        checked={draft.customEmail}
                        onCheckedChange={(v) => set({
                          customEmail: v,
                          emailSubject: v && !draft.emailSubject ? settings?.emailSubject ?? '' : draft.emailSubject,
                          emailHtml: v && !draft.emailHtml ? settings?.emailHtml ?? '' : draft.emailHtml,
                        })}
                        aria-label="Write this email's own HTML"
                      />
                    </label>
                  </div>
                  <AnimatePresence initial={false}>
                    {draft.customEmail && (
                      <motion.div
                        initial={{ opacity: 0, height: 0 }}
                        animate={{ opacity: 1, height: 'auto' }}
                        exit={{ opacity: 0, height: 0 }}
                        transition={{ duration: 0.25, ease: EASE }}
                        className="overflow-hidden"
                      >
                        <div className="mt-3 space-y-2">
                          <Input
                            id={`${idPrefix}-subject`}
                            value={draft.emailSubject}
                            onChange={(e) => set({ emailSubject: e.target.value })}
                            placeholder="Subject, e.g. Welcome to the club, {{firstName}}"
                            maxLength={300}
                            className="h-9 bg-white"
                            aria-label="Email subject"
                          />
                          <Textarea
                            id={`${idPrefix}-html`}
                            value={draft.emailHtml}
                            onChange={(e) => set({ emailHtml: e.target.value })}
                            placeholder="Paste your email's HTML here…"
                            rows={6}
                            spellCheck={false}
                            className="bg-slate-950 font-mono text-xs text-emerald-200 placeholder:text-slate-500"
                            aria-label="Email HTML"
                          />
                          <PlaceholderChips
                            fields={settings?.cadenceFields ?? []}
                            tone="orange"
                            onPick={(t) => set({ emailHtml: insertAt(`${idPrefix}-html`, draft.emailHtml, t) })}
                          />
                          <PlaceholderChips
                            fields={settings?.receiverFields ?? []}
                            tone="violet"
                            onPick={(t) => set({ emailHtml: insertAt(`${idPrefix}-html`, draft.emailHtml, t) })}
                          />
                          <p className="text-[11px] text-slate-500">
                            <span className="text-orange-600">Orange</span> ones CadenceE fills; <span className="text-violet-600">violet</span> ones the portal fills when it sends (the set-password link, the portal, the login).
                          </p>
                        </div>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      )}
    </div>
  );
}
