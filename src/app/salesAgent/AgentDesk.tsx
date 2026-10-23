import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { createActivity } from '@/app/api/activities';
import { createContact } from '@/app/api/contacts';
import { mergeCompanies, mergeContacts } from '@/app/api/dedupe';
import { createDeal, updateDeal } from '@/app/api/deals';
import { assignLead, convertLead } from '@/app/api/leads';
import { enrollInSequence } from '@/app/api/sequences';
import { addTaskComment, createTask } from '@/app/api/tasks';
import { sendEmail } from '@/app/api/email';
import { getUserSettings, saveUserSettings } from '@/app/api/settings';
import { getCurrentOrganizationId, getCurrentUser } from '@/app/lib/auth';
import { askAgent, type AgentAction, type AgentReply, type AgentWorld } from './brain/answer';
import { emptyMemory, remember, rowFor, type AgentMemory } from './brain/reason';

const FOCUS_BASE = 'cadence.agent.focus';
const TURNS_BASE = 'cadence.agent.turns';

function scopedKey(accountKey: string, base: string): string {
  return `${base}.${accountKey}`;
}

function readRaw(key: string): string | null {
  try {
    return localStorage.getItem(key) ?? sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeRaw(key: string, value: string) {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* The conversation still works for this page view. */
  }
}

interface Turn {
  id: string;
  role: 'you' | 'agent';
  text: string;
  actions: AgentAction[];
}

let turnCount = 0;
function turnId(): string {
  turnCount += 1;
  return `turn-${Date.now().toString(36)}-${turnCount}`;
}

/**
 * Turns read back from storage. Only "open" and "draft" buttons come back: whether a
 * write was already pressed is not stored, and the record may have changed since.
 */
function restoredTurns(value: unknown): Turn[] {
  if (!Array.isArray(value)) return [];
  return value.slice(-12).flatMap((turn): Turn[] => {
    if (!turn || typeof turn !== 'object') return [];
    const row = turn as Partial<Turn>;
    if ((row.role !== 'you' && row.role !== 'agent') || typeof row.text !== 'string') return [];
    const actions = Array.isArray(row.actions) ? row.actions.filter((action) => action?.kind === 'open' || action?.kind === 'draft') : [];
    return [{ id: turnId(), role: row.role, text: row.text, actions }];
  });
}

function asMemory(parsed: Partial<AgentMemory> | null | undefined): AgentMemory {
  const text = (value: unknown) => (typeof value === 'string' && value.length > 0 ? value : null);
  return {
    ...emptyMemory(),
    focusKey: text(parsed?.focusKey),
    name: text(parsed?.name),
    leadId: text(parsed?.leadId),
    dealId: text(parsed?.dealId),
    contactId: text(parsed?.contactId),
    companyId: text(parsed?.companyId),
    companyName: text(parsed?.companyName),
  };
}

function readFocus(accountKey: string): AgentMemory {
  try {
    const raw = readRaw(scopedKey(accountKey, FOCUS_BASE));
    if (!raw) return emptyMemory();
    return asMemory(JSON.parse(raw) as Partial<AgentMemory>);
  } catch {
    return emptyMemory();
  }
}

function readTurns(accountKey: string): Turn[] {
  try {
    const raw = readRaw(scopedKey(accountKey, TURNS_BASE));
    return raw ? restoredTurns(JSON.parse(raw)) : [];
  } catch {
    return [];
  }
}

function orgOf(accountKey: string): string {
  return accountKey.slice(accountKey.indexOf(':') + 1);
}

function memoryPayload(accountKey: string, memory: AgentMemory, turns: Turn[]): string {
  const orgId = orgOf(accountKey);
  const stored = (rows: Turn[]) => rows.map(({ role, text, actions }) => ({ role, text, actions }));
  let kept = turns.slice(-12);
  let payload = JSON.stringify({ orgId, ...memory, turns: stored(kept) });
  while (kept.length > 0 && payload.length > 3900) {
    kept = kept.slice(1);
    payload = JSON.stringify({ orgId, ...memory, turns: stored(kept) });
  }
  return payload.length > 3900 ? JSON.stringify({ orgId, ...memory, turns: [] }) : payload;
}

function parseAccountMemory(raw: string | undefined, accountKey: string): { memory: AgentMemory; turns: Turn[] } | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Partial<AgentMemory> & { orgId?: unknown; turns?: unknown };
    if (parsed.orgId !== orgOf(accountKey)) return null;
    return { memory: asMemory(parsed), turns: restoredTurns(parsed.turns) };
  } catch {
    return null;
  }
}

// The panel remounts on every page, so the saved conversation is read from the server
// once per account in a page session, and written only when it actually changed.
const savedOnServer = new Map<string, Promise<string | undefined>>();
const lastWritten = new Map<string, string>();

function serverMemory(accountKey: string): Promise<string | undefined> {
  let pending = savedOnServer.get(accountKey);
  if (!pending) {
    pending = getUserSettings().then((settings) => settings.agentMemoryJson ?? undefined);
    pending.catch(() => savedOnServer.delete(accountKey));
    savedOnServer.set(accountKey, pending);
  }
  return pending;
}

/** Test hook: forget what was read from and written to the server. */
export function resetAgentDeskForTests(): void {
  savedOnServer.clear();
  lastWritten.clear();
  turnCount = 0;
}

/** Why a save failed, in the server's words when it gave any. */
function reason(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? `${fallback} ${error.message.trim()}` : fallback;
}

/**
 * The conversation on top of the rules. A task or a note is written only from a button.
 * A draft only opens the sales writer.
 */
export function AgentDesk({ world, readOnly, accountKey }: { world: AgentWorld; readOnly: boolean; accountKey: string }) {
  const navigate = useNavigate();
  const [question, setQuestion] = useState('');
  const [turns, setTurns] = useState<Turn[]>(() => readTurns(accountKey));
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [initialMemory] = useState(() => readFocus(accountKey));
  const memory = useRef<AgentMemory>(initialMemory);
  const accountReady = useRef(false);
  const savedActions = useRef(new Set<string>());
  const inflight = useRef(new Set<string>());

  useEffect(() => {
    let cancelled = false;
    serverMemory(accountKey)
      .then((raw) => {
        if (cancelled) return;
        const saved = parseAccountMemory(raw, accountKey);
        if (raw) lastWritten.set(accountKey, raw);
        if (!saved) return;
        if (!memory.current.focusKey && saved.memory.focusKey) memory.current = saved.memory;
        if (saved.turns.length > 0) {
          setTurns((current) => (current.length === 0 ? saved.turns : current));
        }
      })
      .catch(() => undefined)
      .finally(() => {
        if (!cancelled) accountReady.current = true;
      });
    return () => {
      cancelled = true;
    };
  }, [accountKey]);

  useEffect(() => {
    writeRaw(scopedKey(accountKey, TURNS_BASE), JSON.stringify(turns.map(({ role, text, actions }) => ({ role, text, actions }))));
    if (!accountReady.current) return undefined;
    const payload = memoryPayload(accountKey, memory.current, turns);
    if (lastWritten.get(accountKey) === payload) return undefined;
    const handle = window.setTimeout(() => {
      lastWritten.set(accountKey, payload);
      savedOnServer.set(accountKey, Promise.resolve(payload));
      saveUserSettings({ agentMemoryJson: payload }).catch(() => lastWritten.delete(accountKey));
    }, 400);
    return () => window.clearTimeout(handle);
  }, [turns, accountKey]);

  function push(role: Turn['role'], text: string, actions: AgentAction[] = []) {
    setTurns((current) => [...current, { id: turnId(), role, text, actions }].slice(-12));
  }

  function ask(text: string) {
    const reply: AgentReply = askAgent(text, world, new Date(), memory.current);
    memory.current = reply.memory
      ?? remember(rowFor(world, reply.focusKey), memory.current, { leads: world.leads, deals: world.deals, contacts: world.contacts });
    writeRaw(scopedKey(accountKey, FOCUS_BASE), JSON.stringify(memory.current));
    const actions = readOnly ? reply.actions.filter((action) => action.kind === 'open' || action.kind === 'draft') : reply.actions;
    if (readOnly && actions.length < reply.actions.length) {
      setNotice('This workspace is read-only, so nothing was saved.');
    }
    push('agent', reply.text, actions);
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    const text = question.trim();
    if (!text || busy) return;
    setQuestion('');
    setNotice(null);
    push('you', text);
    ask(text);
  }

  function confirmSaved(action: AgentAction, text: string) {
    savedActions.current.add(action.id);
    setTurns((current) => [...current.map((turn) => ({
      ...turn,
      actions: turn.actions.filter((item) => item.id !== action.id),
    })), { id: turnId(), role: 'agent' as const, text, actions: [] }].slice(-12));
  }

  /** The workspace and account the requests will go to, which another tab can change. */
  function sameWorkspace(): boolean {
    const current = `${getCurrentUser()?.id ?? 'session'}:${getCurrentOrganizationId() ?? 'no-org'}`;
    return current === accountKey;
  }

  /** Runs one confirmed write. Returns the line to show, or null when the button is missing what it needs. */
  async function save(action: AgentAction): Promise<{ saved: boolean; text: string } | null> {
    if (action.kind === 'task' && action.title) {
      const created = await createTask({
        title: action.title,
        dueDateUtc: action.dueDateUtc,
        leadId: action.leadId,
        dealId: action.dealId,
        contactId: action.contactId,
      });
      return created ? { saved: true, text: `Task saved: ${created.title}.` } : { saved: false, text: 'The task was not saved.' };
    }
    if (action.kind === 'note' && action.body) {
      const created = await createActivity({
        type: 'note',
        body: action.body,
        subject: 'Note from the agent',
        leadId: action.leadId,
        dealId: action.dealId,
        contactId: action.contactId,
      });
      return created ? { saved: true, text: 'Note saved on the record.' } : { saved: false, text: 'The note was not saved.' };
    }
    if (action.kind === 'comment' && action.taskId && action.body) {
      const created = await addTaskComment(action.taskId, action.body);
      return created ? { saved: true, text: 'Comment saved on the task.' } : { saved: false, text: 'The comment was not saved.' };
    }
    if (action.kind === 'send' && action.to && action.body) {
      const sent = await sendEmail({
        to: action.to,
        subject: action.title || 'Following up',
        body: action.body,
        leadId: action.leadId,
        dealId: action.dealId,
        contactId: action.contactId,
      });
      return sent ? { saved: true, text: `Sent to ${action.to}.` } : { saved: false, text: 'The message was not sent.' };
    }
    if (action.kind === 'assign' && action.leadId) {
      const user = getCurrentUser();
      if (!user) return { saved: false, text: 'The lead was not assigned. Sign in so the agent knows who you are.' };
      const saved = await assignLead(action.leadId, user.id);
      return saved ? { saved: true, text: `${saved.name} is assigned to you.` } : { saved: false, text: 'The lead was not assigned.' };
    }
    if (action.kind === 'convert' && action.leadId) {
      const saved = await convertLead(action.leadId, {
        createContact: true,
        createDeal: true,
        dealName: action.recipientName,
      });
      return saved ? { saved: true, text: `${action.recipientName ?? 'The lead'} was converted.` } : { saved: false, text: 'The lead was not converted.' };
    }
    if (action.kind === 'enroll' && action.sequenceId && action.leadId) {
      const saved = await enrollInSequence({
        sequenceId: action.sequenceId,
        targetType: 'lead',
        targetId: action.leadId,
        targetName: action.recipientName || 'Lead',
        targetEmail: action.to,
      });
      return saved ? { saved: true, text: `${action.recipientName ?? 'The lead'} is enrolled.` } : { saved: false, text: 'The enrollment was not saved.' };
    }
    if (action.kind === 'merge' && action.mergePrimaryId && action.mergeDuplicateIds?.length) {
      const saved = action.mergeKind === 'companies'
        ? await mergeCompanies(action.mergePrimaryId, action.mergeDuplicateIds)
        : await mergeContacts(action.mergePrimaryId, action.mergeDuplicateIds);
      return saved ? { saved: true, text: 'The duplicate group was merged.' } : { saved: false, text: 'The merge was not saved.' };
    }
    if (action.kind === 'create-contact' && action.contactName && action.contactEmail) {
      const result = await createContact({
        name: action.contactName,
        email: action.contactEmail,
        phone: action.contactPhone,
        companyId: action.companyId,
      });
      return result.contact
        ? { saved: true, text: `Contact saved: ${result.contact.name}, ${result.contact.email}.` }
        : { saved: false, text: result.error ?? 'The contact was not saved.' };
    }
    if (action.kind === 'create-deal' && action.title && action.dealValue) {
      const saved = await createDeal({
        name: action.title,
        value: action.dealValue,
        currency: action.currency,
        companyId: action.companyId,
        contactId: action.contactId,
      });
      return saved ? { saved: true, text: `Deal saved: ${saved.name}, amount ${saved.value}.` } : { saved: false, text: 'The deal was not saved.' };
    }
    if (action.kind === 'update-deal' && action.dealId && (action.dealValue || action.dueDateUtc)) {
      const saved = await updateDeal(action.dealId, {
        ...(action.dealValue ? { value: action.dealValue } : {}),
        ...(action.dueDateUtc ? { expectedCloseDateUtc: action.dueDateUtc } : {}),
      });
      if (!saved) return { saved: false, text: 'The deal was not updated.' };
      const amount = action.dealValue ? ` Amount ${saved.value}.` : '';
      const close = action.dueDateUtc ? ` Close date ${saved.expectedCloseDateUtc?.slice(0, 10) ?? action.dueDateUtc}.` : '';
      return { saved: true, text: `Deal saved: ${saved.name}.${amount}${close}` };
    }
    return null;
  }

  async function run(action: AgentAction) {
    setNotice(null);
    if (savedActions.current.has(action.id) || inflight.current.has(action.id)) {
      push('agent', 'That change was already saved or is still saving. It will not be sent again.', []);
      return;
    }
    if (action.kind === 'open' && action.href) {
      navigate(action.href);
      return;
    }
    if (action.kind === 'draft' && action.brief) {
      navigate('/dashboard', {
        state: {
          writerBrief: action.brief,
          writerRecipient: action.recipientName ? {
            type: action.recipientType ?? 'lead',
            id: action.leadId || action.dealId || action.contactId || '',
            name: action.recipientName,
          } : null,
        },
      });
      return;
    }
    if (readOnly) {
      setNotice('This workspace is read-only, so nothing was saved.');
      return;
    }
    // The records in this conversation belong to one workspace. Requests go to whichever
    // one is selected now, and another tab can switch it.
    if (!sameWorkspace()) {
      setNotice('The workspace or account changed in another tab. Nothing was saved. Reload this page to continue here.');
      return;
    }
    inflight.current.add(action.id);
    setBusy(true);
    try {
      const result = await save(action);
      if (!result) push('agent', 'That button is missing what the change needs, so nothing was saved. Ask again.', []);
      else if (result.saved) confirmSaved(action, result.text);
      else push('agent', result.text, []);
    } catch (error) {
      setNotice(reason(error, 'That could not be saved.'));
    } finally {
      inflight.current.delete(action.id);
      setBusy(false);
    }
  }

  return (
    <div className="mt-5 border-t border-slate-200 pt-4 dark:border-slate-700">
      <h3 className="text-sm font-semibold text-slate-900 dark:text-white">Ask</h3>
      <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
        Answers stay on this account when settings can be saved, and in this browser as well. A task, note, contact, deal, send, or other change runs only when you press its button, and only once.
      </p>
      {/* The live region is always in the page, so a screen reader is already listening when the first answer arrives. */}
      <div aria-live="polite">
        {turns.length > 0 && (
          <ol className="mt-3 space-y-3" aria-label="Conversation with the agent">
            {turns.map((turn) => {
              const actions = readOnly ? turn.actions.filter((action) => action.kind === 'open' || action.kind === 'draft') : turn.actions;
              return (
                <li key={turn.id} className="text-sm">
                  <p className="font-medium text-slate-500">{turn.role === 'you' ? 'You' : 'Agent'}</p>
                  <p className="mt-1 whitespace-pre-wrap text-slate-800 dark:text-slate-200">{turn.text}</p>
                  {actions.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {actions.map((action) => (
                        <button
                          key={action.id}
                          type="button"
                          disabled={busy}
                          onClick={() => run(action)}
                          className="inline-flex h-11 items-center rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 hover:border-[var(--brand-primary)] disabled:opacity-50 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
                        >
                          {action.label}
                        </button>
                      ))}
                    </div>
                  )}
                </li>
              );
            })}
          </ol>
        )}
      </div>
      {notice && <p className="mt-2 text-sm text-red-700" role="status">{notice}</p>}
      <form onSubmit={onSubmit} className="mt-3 flex flex-col gap-2 sm:flex-row">
        <label className="sr-only" htmlFor="agent-question">Ask the agent</label>
        <input
          id="agent-question"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="What should I do next?"
          className="h-11 min-w-0 flex-1 rounded-xl border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
        />
        <button
          type="submit"
          className="h-11 rounded-xl bg-[var(--brand-primary)] px-4 text-sm font-semibold text-white hover:bg-[var(--brand-primary-dark)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2"
        >
          Ask
        </button>
      </form>
    </div>
  );
}
