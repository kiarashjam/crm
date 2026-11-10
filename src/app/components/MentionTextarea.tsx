import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent, type TextareaHTMLAttributes } from 'react';
import { getOrgMembers } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { cn } from '@/app/components/ui/utils';

/** Someone a comment or a note can mention. */
export interface MentionPerson {
  userId: string;
  name: string;
  email?: string;
}

const MAX_SHOWN = 6;
const MAX_QUERY = 30;

// The workspace's people, read once per workspace and shared by every box on the page.
const peopleByOrg = new Map<string, Promise<MentionPerson[]>>();

/** The people of the workspace being worked in; none in a personal one, where there is nobody to mention. */
export function useWorkspacePeople(): MentionPerson[] {
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  const [people, setPeople] = useState<MentionPerson[]>([]);
  useEffect(() => {
    if (!orgId) {
      setPeople([]);
      return;
    }
    let live = true;
    let request = peopleByOrg.get(orgId);
    if (!request) {
      request = getOrgMembers(orgId)
        .then((members) => members.map((m) => ({ userId: m.userId, name: m.name, email: m.email })))
        .catch(() => {
          // Not cached: the next box to open asks again.
          peopleByOrg.delete(orgId);
          return [];
        });
      peopleByOrg.set(orgId, request);
    }
    void request.then((list) => { if (live) setPeople(list); });
    return () => { live = false; };
  }, [orgId]);
  return people;
}

/** Test hook: forget the people read so far. */
export function resetWorkspacePeopleForTests(): void {
  peopleByOrg.clear();
}

/**
 * The "@…" being typed right before the caret, if any. An @ straight after a letter is
 * an email address, as the server reads it, so it opens nothing.
 */
export function mentionQueryAt(text: string, caret: number): { start: number; query: string } | null {
  const before = text.slice(0, caret);
  const at = before.lastIndexOf('@');
  if (at < 0) return null;
  const previous = at > 0 ? before[at - 1] ?? '' : '';
  if (/[\p{L}\p{N}._+-]/u.test(previous)) return null;
  const query = before.slice(at + 1);
  if (query.length > MAX_QUERY || /[\n@]/.test(query) || /^\s/.test(query)) return null;
  return { start: at, query };
}

/** Who matches what has been typed after the @: by any word of the name, or the address. */
export function matchPeople(people: MentionPerson[], query: string): MentionPerson[] {
  const q = query.trim().toLowerCase();
  return people
    .filter((person) => {
      if (!q) return true;
      const name = person.name.trim().toLowerCase();
      return name.startsWith(q)
        || name.split(/\s+/).some((word) => word.startsWith(q))
        || (person.email ?? '').toLowerCase().startsWith(q);
    })
    .slice(0, MAX_SHOWN);
}

type Props = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange'> & {
  value: string;
  onChange: (value: string) => void;
  people: MentionPerson[];
  /** Classes for the box around the text area and its list, e.g. to let it grow in a row. */
  wrapperClassName?: string;
};

/**
 * A text box that offers the workspace's people after an "@" and writes the chosen one
 * as "@Full Name": plain text, which the server reads as a mention and tells them.
 */
export function MentionTextarea({ value, onChange, people, className, wrapperClassName, onKeyDown, ...rest }: Props) {
  const listId = useId();
  const box = useRef<HTMLTextAreaElement>(null);
  const [caret, setCaret] = useState<number | null>(null);
  const [active, setActive] = useState(0);
  const [dismissed, setDismissed] = useState<number | null>(null);

  // A dismissed @ stays shut only while it is still the one being typed after: a new
  // comment, or the caret moved away, opens the list again on the next @.
  useEffect(() => {
    if (dismissed !== null && caret !== null && mentionQueryAt(value, caret)?.start !== dismissed) setDismissed(null);
  }, [caret, value, dismissed]);

  const open = useMemo(() => {
    if (caret === null || people.length === 0) return null;
    const found = mentionQueryAt(value, caret);
    if (!found || found.start === dismissed) return null;
    // "@Anna Keller " and on is a mention already written, not one being looked up.
    const typed = found.query.toLowerCase();
    if (people.some((person) => typed.startsWith(`${person.name.trim().toLowerCase()} `))) return null;
    const matches = matchPeople(people, found.query);
    return matches.length > 0 ? { ...found, matches } : null;
  }, [caret, value, people, dismissed]);

  const choose = (person: MentionPerson) => {
    if (!open || caret === null) return;
    const inserted = `@${person.name.trim()} `;
    const next = value.slice(0, open.start) + inserted + value.slice(caret);
    const at = open.start + inserted.length;
    onChange(next);
    setCaret(at);
    setActive(0);
    // The name just written still matches what follows its @: that @ is done with.
    setDismissed(open.start);
    requestAnimationFrame(() => {
      box.current?.focus();
      box.current?.setSelectionRange(at, at);
    });
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (open) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        const step = e.key === 'ArrowDown' ? 1 : -1;
        setActive((i) => (i + step + open.matches.length) % open.matches.length);
        return;
      }
      if ((e.key === 'Enter' && !e.metaKey && !e.ctrlKey) || e.key === 'Tab') {
        e.preventDefault();
        const person = open.matches[Math.min(active, open.matches.length - 1)];
        if (person) choose(person);
        return;
      }
      if (e.key === 'Escape') {
        e.preventDefault();
        setDismissed(open.start);
        return;
      }
    }
    onKeyDown?.(e);
  };

  const track = (el: HTMLTextAreaElement) => setCaret(el.selectionStart ?? el.value.length);

  return (
    <div className={cn('relative', wrapperClassName)}>
      <textarea
        {...rest}
        ref={box}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
          track(e.target);
          setActive(0);
        }}
        onSelect={(e) => track(e.currentTarget)}
        onClick={(e) => track(e.currentTarget)}
        onBlur={() => setCaret(null)}
        onKeyDown={handleKeyDown}
        aria-autocomplete="list"
        aria-expanded={!!open}
        aria-controls={open ? listId : undefined}
        className={className}
      />
      {open && (
        <ul
          id={listId}
          role="listbox"
          aria-label="Mention a teammate"
          className="absolute left-0 right-0 z-30 mt-1 max-h-56 overflow-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-600 dark:bg-slate-800"
        >
          {open.matches.map((person, index) => (
            <li
              key={person.userId}
              role="option"
              aria-selected={index === active}
              // Chosen on mouse down, before the box loses focus and the list closes.
              onMouseDown={(e) => {
                e.preventDefault();
                choose(person);
              }}
              className={cn(
                'flex cursor-pointer items-baseline gap-2 px-3 py-1.5 text-sm',
                index === active ? 'bg-emerald-50 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-100' : 'text-slate-700 dark:text-slate-200',
              )}
            >
              <span className="font-medium">{person.name}</span>
              {person.email && <span className="truncate text-xs text-slate-500">{person.email}</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
