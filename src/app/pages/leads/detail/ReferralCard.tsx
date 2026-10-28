import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowUpRight, Check, Loader2, Pencil, UserPlus, Users, X } from 'lucide-react';
import { getLeadsReferredBy } from '@/app/api/leads';
import type { Contact, Lead } from '@/app/api/types';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { Button } from '@/app/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/app/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';

const EASE = [0.16, 1, 0.3, 1] as const;

function initialsOf(name: string): string {
  return name.split(/\s+/).map((part) => part[0]).filter(Boolean).slice(0, 2).join('').toUpperCase();
}

/**
 * Who referred this lead, saved on the lead for the whole team.
 *
 * The referrer links to their contact page, and says how many leads they have sent in
 * so far. A writer can add, change, or clear it from here: the picker lists active
 * contacts, and never the contact this same lead became.
 */
export function ReferralCard({
  lead,
  contacts,
  readOnly,
  onChange,
  className,
}: {
  lead: Lead;
  contacts: Contact[];
  readOnly: boolean;
  /** Save the referrer, or clear it with null. Resolves true when it was saved. */
  onChange: (contactId: string | null) => Promise<boolean>;
  className?: string;
}) {
  const reduce = useMotionPreference();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const [referredCount, setReferredCount] = useState<number | null>(null);

  const referrerId = lead.referredByContactId ?? null;
  const referrer = useMemo(() => (referrerId ? contacts.find((c) => c.id === referrerId) ?? null : null), [contacts, referrerId]);
  const referrerName = referrer?.name ?? lead.referredByContactName ?? null;
  const firstName = lead.name.split(/\s+/)[0] || lead.name;

  // How many leads this person has referred, this one included.
  useEffect(() => {
    if (!referrerId) {
      setReferredCount(null);
      return undefined;
    }
    let cancelled = false;
    getLeadsReferredBy(referrerId)
      .then((leads) => { if (!cancelled) setReferredCount(leads.length); })
      .catch(() => { if (!cancelled) setReferredCount(null); });
    return () => { cancelled = true; };
  }, [referrerId]);

  const choices = useMemo(
    () => contacts
      .filter((c) => !c.isArchived && c.convertedFromLeadId !== lead.id)
      .sort((a, b) => a.name.localeCompare(b.name)),
    [contacts, lead.id],
  );

  async function choose(contactId: string | null) {
    if (contactId === referrerId) {
      setOpen(false);
      return;
    }
    setSaving(contactId ?? 'clear');
    const ok = await onChange(contactId);
    setSaving(null);
    if (ok) setOpen(false);
  }

  const picker = (trigger: React.ReactNode) => (
    <Popover open={open} onOpenChange={(next) => { if (!saving) setOpen(next); }}>
      <PopoverTrigger asChild>{trigger}</PopoverTrigger>
      <PopoverContent aria-label={`Who referred ${firstName}`} align="end" className="w-[300px] p-0">
        <Command>
          <CommandInput placeholder="Search contacts by name, email, or company" />
          <CommandList>
            <CommandEmpty>No contact matches. Add the person under Contacts first.</CommandEmpty>
            <CommandGroup heading="Referred by">
              {choices.map((contact) => (
                <CommandItem
                  key={contact.id}
                  value={`${contact.name} ${contact.email} ${contact.companyName ?? ''}`}
                  onSelect={() => void choose(contact.id)}
                  disabled={!!saving}
                  className="gap-2.5"
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-violet-500 to-fuchsia-500 text-[10px] font-bold text-white">
                    {initialsOf(contact.name)}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-slate-800">{contact.name}</span>
                    <span className="block truncate text-xs text-slate-500">{contact.companyName || contact.email}</span>
                  </span>
                  {saving === contact.id
                    ? <Loader2 className="h-4 w-4 animate-spin text-violet-600" />
                    : contact.id === referrerId && <Check className="h-4 w-4 text-violet-600" />}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );

  return (
    <section
      aria-labelledby="referral-heading"
      className={cn('overflow-hidden rounded-2xl border border-violet-200/70 bg-gradient-to-br from-violet-50/70 via-white to-white shadow-sm', className)}
    >
      <div className="flex items-center justify-between border-b border-violet-100/80 px-4 py-2.5">
        <h3 id="referral-heading" className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-violet-700">
          <UserPlus className="h-3.5 w-3.5" aria-hidden="true" />
          Referred by
        </h3>
        {!readOnly && referrerName && picker(
          <button type="button" className="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px] font-medium text-violet-700 transition-colors hover:bg-violet-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400">
            <Pencil className="h-3 w-3" aria-hidden="true" />
            Change
          </button>,
        )}
      </div>

      <div className="p-4">
        <AnimatePresence mode="wait" initial={false}>
          {referrerName ? (
            <motion.div
              key={referrerId ?? referrerName}
              initial={reduce ? false : { opacity: 0, y: 8, scale: 0.98 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: -6, transition: { duration: 0.15 } }}
              transition={{ duration: 0.3, ease: EASE }}
              className="flex items-start gap-3"
            >
              <motion.span
                aria-hidden="true"
                initial={reduce ? false : { scale: 0.6, rotate: -8 }}
                animate={{ scale: 1, rotate: 0 }}
                transition={{ type: 'spring', stiffness: 380, damping: 18 }}
                className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-violet-500 to-fuchsia-500 text-sm font-bold text-white shadow-md shadow-violet-500/25 ring-2 ring-white"
              >
                {initialsOf(referrerName)}
              </motion.span>
              <div className="min-w-0 flex-1">
                {referrerId ? (
                  <Link
                    to={`/contacts/${referrerId}`}
                    className="group inline-flex max-w-full items-center gap-1 rounded-sm text-sm font-semibold text-slate-900 hover:text-violet-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400"
                  >
                    <span className="truncate">{referrerName}</span>
                    <ArrowUpRight className="h-3.5 w-3.5 shrink-0 transition-transform motion-safe:group-hover:-translate-y-0.5 motion-safe:group-hover:translate-x-0.5" aria-hidden="true" />
                    <span className="sr-only">, open their contact</span>
                  </Link>
                ) : (
                  <p className="truncate text-sm font-semibold text-slate-900">{referrerName}</p>
                )}
                {(referrer?.jobTitle || referrer?.companyName || referrer?.email) && (
                  <p className="mt-0.5 truncate text-xs text-slate-500">
                    {[referrer.jobTitle, referrer.companyName].filter(Boolean).join(' · ') || referrer.email}
                  </p>
                )}
                {referrer?.isArchived && <p className="mt-0.5 text-xs text-amber-700">This contact is archived.</p>}
                <AnimatePresence initial={false}>
                  {referredCount !== null && referredCount > 0 && (
                    <motion.p
                      initial={reduce ? false : { opacity: 0, height: 0 }}
                      animate={{ opacity: 1, height: 'auto' }}
                      exit={{ opacity: 0, height: 0 }}
                      transition={{ duration: 0.25, ease: EASE }}
                      className="mt-2 inline-flex items-center gap-1 rounded-full bg-violet-100 px-2 py-0.5 text-[11px] font-semibold text-violet-700"
                    >
                      <Users className="h-3 w-3" aria-hidden="true" />
                      {referredCount === 1 ? 'Their first referral' : `${referredCount} leads referred`}
                    </motion.p>
                  )}
                </AnimatePresence>
              </div>
              {!readOnly && (
                <button
                  type="button"
                  onClick={() => void choose(null)}
                  disabled={!!saving}
                  aria-label={`Remove ${referrerName} as the referrer`}
                  title="Remove referrer"
                  className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-slate-500 transition-colors hover:bg-red-50 hover:text-red-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-300 disabled:opacity-50"
                >
                  {saving === 'clear' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <X className="h-3.5 w-3.5" />}
                </button>
              )}
            </motion.div>
          ) : (
            <motion.div
              key="empty"
              initial={reduce ? false : { opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: -4, transition: { duration: 0.15 } }}
              transition={{ duration: 0.25, ease: EASE }}
            >
              <p className="text-sm text-slate-600">No referrer recorded.</p>
              {readOnly ? (
                <p className="mt-1 text-xs text-slate-500">A writer in this workspace can add who referred {firstName}.</p>
              ) : picker(
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className="mt-3 w-full gap-1.5 border-dashed border-violet-300 text-violet-700 hover:border-violet-400 hover:bg-violet-50"
                >
                  <UserPlus className="h-4 w-4" aria-hidden="true" />
                  Add who referred {firstName}
                </Button>,
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </section>
  );
}
