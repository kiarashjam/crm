import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import { toast } from 'sonner';
import { ArrowUpRight, Heart, Loader2, Plus, X } from 'lucide-react';
import { getContacts } from '@/app/api/contacts';
import type { Contact } from '@/app/api/types';
import {
  RELATIONSHIP_TYPES,
  addRelationship,
  byCoupleThenName,
  getRelationships,
  isCouple,
  relationshipLabel,
  removeRelationship,
  updateRelationship,
  type ContactRelationship,
  type RelationshipOwner,
  type RelationshipType,
} from '@/app/api/relationships';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { Button } from '@/app/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/app/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';

const EASE = [0.16, 1, 0.3, 1] as const;

function initialsOf(name: string): string {
  return name.split(/\s+/).map((part) => part[0]).filter(Boolean).slice(0, 2).join('').toUpperCase();
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/**
 * How this person is related to other contacts: their spouse or partner (who the
 * couples package is for), parents, children, siblings, and so on.
 *
 * Saved on the server, so the whole team sees the same thing, from either side: a
 * spouse added on Claire's lead shows on Marc's contact too. A writer adds a relative
 * from a searchable picker, changes the kind of relationship in place, or removes it.
 */
export default function RelationshipsCard({
  owner,
  personName,
  contacts: givenContacts,
  readOnly,
  revision = 0,
  onChanged,
  className,
}: {
  owner: RelationshipOwner;
  personName: string;
  /** Contacts to pick from. Loaded on first use when not given. */
  contacts?: Contact[];
  readOnly: boolean;
  /** Bumped by the page when its records change, so the list reads again. */
  revision?: number;
  /** Told after each saved change, in a sentence, e.g. for the page's timeline. */
  onChanged?: (summary: string) => void;
  className?: string;
}) {
  const reduce = useMotionPreference();
  const [items, setItems] = useState<ContactRelationship[] | null>(null);
  const [contactId, setContactId] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const [loadedContacts, setLoadedContacts] = useState<Contact[] | null>(null);
  const [open, setOpen] = useState(false);
  const [newType, setNewType] = useState<RelationshipType>('spouse');
  const [saving, setSaving] = useState<string | null>(null);

  const ownerKey = 'leadId' in owner ? `lead:${owner.leadId}` : `contact:${owner.contactId}`;
  const firstName = personName.split(/\s+/)[0] || personName;

  useEffect(() => {
    let cancelled = false;
    setFailed(false);
    getRelationships(owner)
      .then((list) => {
        if (cancelled) return;
        setItems(list.items);
        setContactId(list.contactId);
      })
      .catch(() => { if (!cancelled) { setItems([]); setFailed(true); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- owner is identified by ownerKey
  }, [ownerKey, revision]);

  useEffect(() => {
    if (!open || givenContacts || loadedContacts) return;
    getContacts().then(setLoadedContacts).catch(() => setLoadedContacts([]));
  }, [open, givenContacts, loadedContacts]);

  const hasCouple = !!items?.some((r) => isCouple(r.type));
  const contacts = givenContacts ?? loadedContacts;
  const choices = useMemo(() => {
    const related = new Set(items?.map((r) => r.relatedContactId) ?? []);
    return (contacts ?? [])
      .filter((c) => !c.isArchived && c.id !== contactId && !related.has(c.id)
        && !('leadId' in owner && c.convertedFromLeadId === owner.leadId))
      .sort((a, b) => a.name.localeCompare(b.name));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- owner is identified by ownerKey
  }, [contacts, items, contactId, ownerKey]);

  const openPicker = (next: boolean) => {
    if (saving) return;
    if (next) setNewType(hasCouple ? 'family' : 'spouse');
    setOpen(next);
  };

  const add = useCallback(async (related: Contact) => {
    setSaving(related.id);
    try {
      const saved = await addRelationship(owner, related.id, newType);
      setItems((prev) => [...(prev ?? []), saved].sort(byCoupleThenName));
      setOpen(false);
      const summary = `${related.name} added as ${firstName}'s ${relationshipLabel(newType).toLowerCase()}`;
      toast.success(`${summary}.`);
      onChanged?.(summary);
    } catch (err) {
      toast.error(errorText(err, 'The relationship was not saved.'));
    } finally {
      setSaving(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- owner is identified by ownerKey
  }, [ownerKey, newType, firstName, onChanged]);

  const changeType = useCallback(async (relationship: ContactRelationship, type: RelationshipType) => {
    if (type === relationship.type) return;
    setSaving(relationship.id);
    try {
      const saved = await updateRelationship(owner, relationship.id, relationship.relatedContactId, type);
      setItems((prev) => (prev ?? []).map((r) => (r.id === saved.id ? saved : r)).sort(byCoupleThenName));
      const summary = `${relationship.relatedContactName} is now ${firstName}'s ${relationshipLabel(type).toLowerCase()}`;
      toast.success(`${summary}.`);
      onChanged?.(`${summary} (was ${relationshipLabel(relationship.type).toLowerCase()})`);
    } catch (err) {
      toast.error(errorText(err, 'The relationship was not changed.'));
    } finally {
      setSaving(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- owner is identified by ownerKey
  }, [ownerKey, firstName, onChanged]);

  const remove = useCallback(async (relationship: ContactRelationship) => {
    setSaving(relationship.id);
    try {
      await removeRelationship(owner, relationship.id);
      setItems((prev) => (prev ?? []).filter((r) => r.id !== relationship.id));
      const summary = `${relationship.relatedContactName} removed as ${firstName}'s ${relationshipLabel(relationship.type).toLowerCase()}`;
      toast.success(`${summary}.`);
      onChanged?.(summary);
    } catch (err) {
      toast.error(errorText(err, 'The relationship was not removed.'));
    } finally {
      setSaving(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- owner is identified by ownerKey
  }, [ownerKey, firstName, onChanged]);

  const picker = (
    <Popover open={open} onOpenChange={openPicker}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="mt-3 w-full gap-1.5 border-dashed border-rose-300 text-rose-700 hover:border-rose-400 hover:bg-rose-50"
        >
          <Plus className="h-4 w-4" aria-hidden="true" />
          {hasCouple ? 'Add a relative' : `Add ${firstName}'s spouse or a relative`}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[320px] p-0">
        <div className="flex items-center gap-2 border-b px-3 py-2">
          <label htmlFor={`relationship-type-${ownerKey}`} className="shrink-0 text-xs font-medium text-slate-600">
            Their
          </label>
          <Select value={newType} onValueChange={(v) => setNewType(v as RelationshipType)}>
            <SelectTrigger id={`relationship-type-${ownerKey}`} className="h-8 flex-1 text-sm" aria-label="Relationship">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {RELATIONSHIP_TYPES.map((t) => (
                <SelectItem key={t.value} value={t.value} disabled={hasCouple && isCouple(t.value)}>
                  {t.label}{hasCouple && isCouple(t.value) ? ' (already recorded)' : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Command>
          <CommandInput placeholder="Search contacts by name or email" />
          <CommandList>
            {contacts === null ? (
              <div className="flex items-center justify-center gap-2 py-6 text-sm text-slate-500" role="status">
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                Loading contacts…
              </div>
            ) : (
              <>
                <CommandEmpty>No contact matches. Add the person under Contacts or Leads first.</CommandEmpty>
                <CommandGroup heading={`${firstName}'s ${relationshipLabel(newType).toLowerCase()} is…`}>
                  {choices.map((contact) => (
                    <CommandItem
                      key={contact.id}
                      value={`${contact.name} ${contact.email} ${contact.companyName ?? ''}`}
                      onSelect={() => void add(contact)}
                      disabled={!!saving}
                      className="gap-2.5"
                    >
                      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-rose-500 to-pink-500 text-[10px] font-bold text-white">
                        {initialsOf(contact.name)}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-slate-800">{contact.name}</span>
                        <span className="block truncate text-xs text-slate-500">{contact.email || contact.companyName}</span>
                      </span>
                      {saving === contact.id && <Loader2 className="h-4 w-4 animate-spin text-rose-600" />}
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );

  return (
    <section
      aria-labelledby={`relationships-heading-${ownerKey}`}
      className={cn('overflow-hidden rounded-2xl border border-rose-200/70 bg-gradient-to-br from-rose-50/70 via-white to-white shadow-sm dark:border-slate-700 dark:from-slate-800 dark:via-slate-800 dark:to-slate-800', className)}
    >
      <div className="flex items-center justify-between border-b border-rose-100/80 px-4 py-2.5 dark:border-slate-700">
        <h3 id={`relationships-heading-${ownerKey}`} className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-rose-700">
          <Heart className="h-3.5 w-3.5" aria-hidden="true" />
          Relationships
        </h3>
        {items && items.length > 0 && (
          <span className="rounded-full bg-rose-100 px-2 py-0.5 text-[11px] font-semibold text-rose-700">{items.length}</span>
        )}
      </div>

      <div className="p-4">
        {items === null ? (
          <div className="flex items-center justify-center gap-2 py-3 text-sm text-slate-500" role="status">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            Loading relationships…
          </div>
        ) : failed ? (
          <p className="text-sm text-slate-500">The relationships could not be loaded.</p>
        ) : contactId === null ? (
          <p className="text-sm text-slate-500">
            Give {firstName} a name and an email to record their spouse or family.
          </p>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-slate-600">No spouse or relatives recorded.</p>
            ) : (
              <ul className="space-y-2">
                <AnimatePresence initial={false}>
                  {items.map((relationship) => (
                    <motion.li
                      key={relationship.id}
                      layout={!reduce}
                      initial={reduce ? false : { opacity: 0, y: 8 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, x: -8, transition: { duration: 0.15 } }}
                      transition={{ duration: 0.25, ease: EASE }}
                      className="flex items-center gap-2.5 rounded-xl border border-slate-100 bg-white/80 p-2.5 dark:border-slate-600 dark:bg-slate-700/50"
                    >
                      <span
                        aria-hidden="true"
                        className={cn(
                          'flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white',
                          isCouple(relationship.type)
                            ? 'bg-gradient-to-br from-rose-500 to-pink-500 shadow-md shadow-rose-500/25'
                            : 'bg-gradient-to-br from-slate-400 to-slate-500',
                        )}
                      >
                        {initialsOf(relationship.relatedContactName)}
                      </span>
                      <div className="min-w-0 flex-1">
                        <Link
                          to={relationship.relatedLeadId ? `/leads/${relationship.relatedLeadId}` : `/contacts/${relationship.relatedContactId}`}
                          className="group inline-flex max-w-full items-center gap-1 rounded-sm text-sm font-semibold text-slate-900 hover:text-rose-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-400 dark:text-slate-100"
                        >
                          <span className="truncate">{relationship.relatedContactName}</span>
                          <ArrowUpRight className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                          <span className="sr-only">, open their {relationship.relatedLeadId ? 'lead' : 'contact'}</span>
                        </Link>
                        <div className="mt-0.5 flex flex-wrap items-center gap-1.5">
                          {readOnly ? (
                            <span className="text-xs text-slate-600">{relationshipLabel(relationship.type)}</span>
                          ) : (
                            <Select
                              value={relationship.type}
                              onValueChange={(v) => void changeType(relationship, v as RelationshipType)}
                              disabled={!!saving}
                            >
                              <SelectTrigger
                                className="h-6 w-auto gap-1 border-slate-200 px-2 text-xs"
                                aria-label={`How ${relationship.relatedContactName} is related to ${firstName}`}
                              >
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {RELATIONSHIP_TYPES.map((t) => (
                                  <SelectItem
                                    key={t.value}
                                    value={t.value}
                                    disabled={isCouple(t.value) && hasCouple && !isCouple(relationship.type)}
                                  >
                                    {t.label}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          )}
                          {isCouple(relationship.type) && (
                            <span className="rounded-full bg-rose-100 px-1.5 py-0.5 text-[10px] font-semibold text-rose-700">Couple</span>
                          )}
                          {relationship.relatedContactIsArchived && (
                            <span className="text-[11px] text-amber-700">Archived</span>
                          )}
                        </div>
                      </div>
                      {!readOnly && (
                        <button
                          type="button"
                          onClick={() => void remove(relationship)}
                          disabled={!!saving}
                          aria-label={`Remove ${relationship.relatedContactName} as ${firstName}'s ${relationshipLabel(relationship.type).toLowerCase()}`}
                          title="Remove relationship"
                          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-300 disabled:opacity-50"
                        >
                          {saving === relationship.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <X className="h-3.5 w-3.5" />}
                        </button>
                      )}
                    </motion.li>
                  ))}
                </AnimatePresence>
              </ul>
            )}
            {readOnly
              ? items.length === 0 && <p className="mt-1 text-xs text-slate-400">A writer in this workspace can add {firstName}'s spouse or family.</p>
              : picker}
          </>
        )}
      </div>
    </section>
  );
}
