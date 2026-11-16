import { useEffect, useRef } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Check, Handshake, MailCheck, MessageSquareHeart, Settings2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { cn } from '@/app/components/ui/utils';
import type { OnboardingSettings } from '@/app/api/onboarding';
import { WelcomeFields } from './WelcomeFields';
import type { WelcomeDraft } from './welcome';

const EASE = [0.16, 1, 0.3, 1] as const;

/**
 * The welcome, as one of the convert dialog's choices beside the contact, company and deal.
 * It sat out of sight until a deal was picked, so people did not know a conversion could
 * welcome anyone. A welcome goes with a deal (their membership) to the deal's contact:
 * choosing it adds a new deal when none is picked, and says why it cannot go when there is
 * nobody to send it to.
 */
export function ConvertWelcomeChoice({
  draft,
  onChange,
  settings,
  on,
  blocked,
  dealName,
  onTurnOn,
}: {
  draft: WelcomeDraft;
  onChange: (next: WelcomeDraft) => void;
  settings: OnboardingSettings | null;
  /** It goes with this conversion. */
  on: boolean;
  /** Why it cannot go, when it cannot. */
  blocked?: string | null;
  /** The deal it goes with: the new one's name, or the existing one picked. */
  dealName?: string;
  /** Turn it on, adding a deal when none is picked. */
  onTurnOn: () => void;
}) {
  const configured = Boolean(settings?.configured);
  const fieldsRef = useRef<HTMLDivElement>(null);
  const wasOn = useRef(on);

  // Bring the message and start date into view when it is turned on: they open below the
  // fold of the dialog's scrolling list.
  useEffect(() => {
    if (on && !wasOn.current) {
      const id = window.setTimeout(() => fieldsRef.current?.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' }), 300);
      wasOn.current = on;
      return () => window.clearTimeout(id);
    }
    wasOn.current = on;
    return undefined;
  }, [on]);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <div className="flex h-6 w-6 items-center justify-center rounded-md bg-gradient-to-br from-orange-400 to-pink-500 text-white shadow-sm">
          <MessageSquareHeart className="h-3.5 w-3.5" />
        </div>
        <span className="text-sm font-semibold text-slate-700">Welcome email</span>
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-500">Optional</span>
      </div>

      {!configured ? (
        <div className="flex items-start gap-3 rounded-xl border border-dashed border-orange-200 bg-orange-50/50 p-3 text-sm">
          <Settings2 className="mt-0.5 h-4 w-4 shrink-0 text-orange-500" />
          <p className="text-slate-600">
            Welcome them to your member portal as you convert: it opens their account and sends your welcome email with a link to set their password.{' '}
            <Link to="/settings?tab=welcome" className="font-medium text-orange-700 hover:underline">Connect your portal</Link>
          </p>
        </div>
      ) : (
        <>
          <button
            type="button"
            aria-pressed={on}
            disabled={Boolean(blocked) && !on}
            onClick={() => (on ? onChange({ ...draft, send: false }) : onTurnOn())}
            className={cn(
              'flex w-full items-center gap-3 rounded-xl border-2 p-4 text-left transition-all',
              on
                ? 'border-orange-400 bg-gradient-to-br from-orange-50 to-pink-50 shadow-sm'
                : 'border-slate-100 bg-white hover:border-slate-200 hover:bg-slate-50/50',
              blocked && !on && 'cursor-not-allowed opacity-60 hover:border-slate-100 hover:bg-white',
            )}
          >
            <div className={cn(
              'flex h-10 w-10 shrink-0 items-center justify-center rounded-lg transition-all',
              on ? 'bg-gradient-to-br from-orange-400 to-pink-500 text-white' : 'bg-slate-100 text-slate-500',
            )}
            >
              <MailCheck className="h-5 w-5" />
            </div>
            <div className="min-w-0 flex-1">
              <p className={cn('font-medium', on ? 'text-orange-800' : 'text-slate-700')}>Send welcome email</p>
              <p className="mt-0.5 text-xs text-slate-500">
                {blocked && !on
                  ? blocked
                  : 'Opens their portal account and emails your welcome, with a link to set their password'}
              </p>
            </div>
            {on && <Check className="h-5 w-5 shrink-0 text-orange-600" />}
          </button>

          <AnimatePresence initial={false}>
            {on && (
              <motion.div
                key="welcome-fields"
                ref={fieldsRef}
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                exit={{ opacity: 0, height: 0 }}
                transition={{ duration: 0.28, ease: EASE }}
                className="overflow-hidden"
              >
                <div className="space-y-3 border-l-2 border-orange-200 pl-4">
                  {dealName && (
                    <p className="flex items-center gap-1.5 text-xs text-slate-500">
                      <Handshake className="h-3.5 w-3.5 shrink-0 text-amber-500" />
                      <span>Goes with the deal <strong className="font-medium text-slate-700">{dealName}</strong>, their membership.</span>
                    </p>
                  )}
                  <WelcomeFields draft={draft} onChange={onChange} settings={settings} idPrefix="convert-welcome" showToggle={false} />
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </>
      )}
    </div>
  );
}
