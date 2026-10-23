import { useEffect, useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowUp, ChevronUp, Maximize2, Minimize2, RotateCcw, ShieldCheck, X } from 'lucide-react';
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
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { cn } from '@/app/components/ui/utils';
import { ActionChip, AgentBubble, AiOrb, EASE, SuggestionChips, ThinkingBubble, UserBubble } from './AgentChatParts';
import { followUpSuggestions, starterSuggestions } from './chatSuggestions';
import { CHAT_OPEN_KEY, takeChatFocusRequest, useStoredFlag } from './panelPrefs';

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
  /** Said in this page view, so it animates in. A turn read back from storage does not. */
  fresh?: boolean;
  /** A confirmation that a change was saved. */
  saved?: boolean;
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

/** How long the agent "thinks" before answering. Long enough to read as work, short enough not to wait on. */
const THINKING_MS = 650;

/**
 * The conversation on top of the rules, as a chat. A task or a note is written only from
 * a button, and only once. A draft only opens the sales writer.
 */
export function AgentDesk({ world, readOnly, accountKey, thinkingMs = THINKING_MS, initialOpen = false }: {
  world: AgentWorld;
  readOnly: boolean;
  accountKey: string;
  /** Pause before an answer appears. Zero answers at once (tests, and reduced motion). */
  thinkingMs?: number;
  /** Open or closed when this browser has no saved choice yet. Closed: the chat opens when asked for. */
  initialOpen?: boolean;
}) {
  const navigate = useNavigate();
  const reduce = useMotionPreference();
  const [question, setQuestion] = useState('');
  const [turns, setTurns] = useState<Turn[]>(() => readTurns(accountKey));
  const [busy, setBusy] = useState(false);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [thinking, setThinking] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [lastQuestion, setLastQuestion] = useState<string | null>(null);
  const thinkTimer = useRef<number | null>(null);
  const scroller = useRef<HTMLDivElement | null>(null);
  const field = useRef<HTMLTextAreaElement | null>(null);
  const [chatOpen, setChatOpen] = useStoredFlag(CHAT_OPEN_KEY, initialOpen);
  const focusOnOpen = useRef(takeChatFocusRequest());
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

  useEffect(() => () => {
    if (thinkTimer.current) window.clearTimeout(thinkTimer.current);
  }, []);

  // Keep the newest message in view as the conversation grows, and when the chat opens.
  useEffect(() => {
    const box = scroller.current;
    if (!box) return;
    if (typeof box.scrollTo === 'function') box.scrollTo({ top: box.scrollHeight, behavior: reduce ? 'auto' : 'smooth' });
    else box.scrollTop = box.scrollHeight;
  }, [turns.length, thinking, reduce, chatOpen]);

  // Opened by the person: the cursor goes where they will type, as soon as the box is there.
  const attachField = (element: HTMLTextAreaElement | null) => {
    field.current = element;
    if (element && focusOnOpen.current) {
      focusOnOpen.current = false;
      element.focus();
    }
  };

  function openChat() {
    focusOnOpen.current = true;
    setChatOpen(true);
  }

  // The box grows with what is typed, up to a few lines.
  useLayoutEffect(() => {
    const box = field.current;
    if (!box) return;
    box.style.height = 'auto';
    box.style.height = `${Math.min(box.scrollHeight, 132)}px`;
  }, [question]);

  function push(role: Turn['role'], text: string, actions: AgentAction[] = []) {
    setTurns((current) => [...current, { id: turnId(), role, text, actions, fresh: true }].slice(-12));
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

  /** Ask one question: it shows at once, and the answer follows after a short think. */
  function send(raw: string) {
    const text = raw.trim();
    if (!text || busy || thinking) return;
    setQuestion('');
    setNotice(null);
    setLastQuestion(text);
    push('you', text);
    const wait = reduce ? 0 : thinkingMs;
    if (wait <= 0) {
      ask(text);
      return;
    }
    setThinking(true);
    thinkTimer.current = window.setTimeout(() => {
      thinkTimer.current = null;
      setThinking(false);
      ask(text);
    }, wait);
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    send(question);
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    // Enter sends, Shift+Enter starts a new line, and a composing IME keeps its Enter.
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      send(question);
    }
  }

  function clearConversation() {
    if (thinkTimer.current) window.clearTimeout(thinkTimer.current);
    thinkTimer.current = null;
    setThinking(false);
    setTurns([]);
    setNotice(null);
    setLastQuestion(null);
    memory.current = emptyMemory();
    writeRaw(scopedKey(accountKey, FOCUS_BASE), JSON.stringify(memory.current));
    field.current?.focus();
  }

  function confirmSaved(action: AgentAction, text: string) {
    savedActions.current.add(action.id);
    setTurns((current) => [...current.map((turn) => ({
      ...turn,
      actions: turn.actions.filter((item) => item.id !== action.id),
    })), { id: turnId(), role: 'agent' as const, text, actions: [], fresh: true, saved: true }].slice(-12));
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
    setPendingId(action.id);
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
      setPendingId(null);
    }
  }

  const empty = turns.length === 0 && !thinking;
  const lastTurn = turns[turns.length - 1];
  const suggestions = empty
    ? starterSuggestions(world)
    : !thinking && !busy && lastTurn?.role === 'agent'
      ? followUpSuggestions(world, memory.current, lastQuestion)
      : [];
  const status = thinking ? 'Thinking…' : busy ? 'Saving…' : 'Ready';

  const messageCount = turns.length;
  return (
    <AnimatePresence mode="wait" initial={false}>
      {!chatOpen ? (
        <motion.button
          key="chat-closed"
          type="button"
          onClick={openChat}
          aria-expanded={false}
          aria-controls="agent-chat"
          initial={reduce ? false : { opacity: 0, y: 8, scale: 0.98 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: -6, scale: 0.98, transition: { duration: 0.15 } }}
          transition={{ duration: 0.3, ease: EASE }}
          whileHover={reduce ? undefined : { y: -2 }}
          whileTap={reduce ? undefined : { scale: 0.99 }}
          className="group relative mt-5 flex w-full items-center gap-3 overflow-hidden rounded-2xl border border-fuchsia-200/70 bg-gradient-to-r from-white via-fuchsia-50/50 to-orange-50/40 px-4 py-3 text-left shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 dark:border-fuchsia-900/50 dark:from-slate-900 dark:via-fuchsia-950/30 dark:to-slate-900"
        >
          {!reduce && (
            <motion.span
              aria-hidden="true"
              className="pointer-events-none absolute inset-y-0 -left-1/3 w-1/3 skew-x-[-20deg] bg-gradient-to-r from-transparent via-white/80 to-transparent dark:via-white/10"
              animate={{ x: ['0%', '420%'] }}
              transition={{ duration: 1.8, repeat: Infinity, repeatDelay: 5, ease: 'easeInOut' }}
            />
          )}
          <AiOrb size={34} reduce={reduce} />
          <span className="min-w-0 flex-1">
            <span className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
              Ask Cadence AI
              <span className="rounded-full bg-gradient-to-r from-orange-500 to-fuchsia-500 px-1.5 py-px text-[10px] font-bold uppercase tracking-wider text-white">AI</span>
            </span>
            <span className="block truncate text-xs text-slate-500 dark:text-slate-400">
              {messageCount > 0
                ? `Continue the conversation · ${messageCount} message${messageCount === 1 ? '' : 's'}`
                : 'Ask what to do next, or about a lead, deal, or contact'}
            </span>
          </span>
          <ChevronUp className="h-4 w-4 shrink-0 text-fuchsia-500 transition-transform motion-safe:group-hover:-translate-y-0.5" aria-hidden="true" />
        </motion.button>
      ) : (
    <motion.section
      key="chat-open"
      id="agent-chat"
      initial={reduce ? false : { opacity: 0, y: 10, scale: 0.99 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: 8, scale: 0.99, transition: { duration: 0.15 } }}
      transition={{ duration: 0.35, ease: EASE }}
      aria-labelledby="agent-chat-heading"
      className="relative mt-5 overflow-hidden rounded-2xl border border-fuchsia-200/70 bg-gradient-to-b from-white via-white to-fuchsia-50/40 shadow-sm dark:border-fuchsia-900/50 dark:from-slate-900 dark:via-slate-900 dark:to-fuchsia-950/20"
    >
      {/* A soft aurora behind the header, drifting slowly. */}
      {!reduce && (
        <>
          <motion.span
            aria-hidden="true"
            className="pointer-events-none absolute -left-10 -top-16 h-40 w-40 rounded-full bg-orange-300/25 blur-3xl"
            animate={{ x: [0, 30, 0], y: [0, 10, 0] }}
            transition={{ duration: 14, repeat: Infinity, ease: 'easeInOut' }}
          />
          <motion.span
            aria-hidden="true"
            className="pointer-events-none absolute -right-12 -top-10 h-44 w-44 rounded-full bg-fuchsia-300/25 blur-3xl"
            animate={{ x: [0, -24, 0], y: [0, 16, 0] }}
            transition={{ duration: 17, repeat: Infinity, ease: 'easeInOut' }}
          />
        </>
      )}

      <header className="relative flex items-center gap-3 border-b border-fuchsia-100/80 px-4 py-3 dark:border-fuchsia-900/40">
        <AiOrb size={36} thinking={thinking} reduce={reduce} />
        <div className="min-w-0 flex-1">
          <h3 id="agent-chat-heading" className="flex items-center gap-2 whitespace-nowrap text-sm font-semibold text-slate-900 dark:text-white">
            Ask Cadence AI
            <span className="rounded-full bg-gradient-to-r from-orange-500 to-fuchsia-500 px-1.5 py-px text-[10px] font-bold uppercase tracking-wider text-white">AI</span>
          </h3>
          <p className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
            <span className="relative flex h-2 w-2" aria-hidden="true">
              {(thinking || busy) && !reduce && <span className="absolute inline-flex h-full w-full rounded-full bg-fuchsia-400 opacity-75 motion-safe:animate-ping" />}
              <span className={cn('relative inline-flex h-2 w-2 rounded-full', thinking || busy ? 'bg-fuchsia-500' : 'bg-emerald-500')} />
            </span>
            <span>{status}</span>
            <span className="hidden sm:inline">· answers from this workspace&apos;s records</span>
          </p>
        </div>
        {turns.length > 0 && (
          <button
            type="button"
            onClick={clearConversation}
            disabled={busy}
            aria-label="New chat"
            className="inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 disabled:opacity-50 dark:hover:bg-slate-800 dark:hover:text-slate-100"
          >
            <RotateCcw className="h-3.5 w-3.5" aria-hidden="true" />
            <span className="hidden sm:inline">New chat</span>
          </button>
        )}
        {turns.length > 0 && (
          <button
            type="button"
            onClick={() => setExpanded((open) => !open)}
            aria-label={expanded ? 'Show a shorter conversation' : 'Show a taller conversation'}
            aria-pressed={expanded}
            className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 dark:hover:bg-slate-800"
          >
            {expanded ? <Minimize2 className="h-4 w-4" aria-hidden="true" /> : <Maximize2 className="h-4 w-4" aria-hidden="true" />}
          </button>
        )}
        <button
          type="button"
          onClick={() => setChatOpen(false)}
          aria-label="Close chat"
          aria-expanded
          aria-controls="agent-chat"
          title="Close chat. The conversation is kept."
          className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 dark:hover:bg-slate-800"
        >
          <X className="h-4 w-4" aria-hidden="true" />
        </button>
      </header>

      {/* The live region is always in the page, so a screen reader is already listening when the first answer arrives. */}
      <div
        ref={scroller}
        aria-live="polite"
        className={cn(
          'relative overflow-y-auto overscroll-contain px-4 transition-[max-height] duration-300',
          expanded ? 'max-h-[34rem]' : 'max-h-[22rem]',
          'py-4',
        )}
      >
        {empty ? (
          <motion.div
            initial={reduce ? false : { opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, ease: EASE }}
            className="flex items-start gap-2.5"
          >
            <AiOrb size={28} reduce={reduce} className="mt-0.5" />
            <div className="rounded-2xl rounded-tl-md border border-slate-200/80 bg-white px-4 py-3 text-sm text-slate-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
              <p className="font-medium text-slate-900 dark:text-white">Hi, I know this workspace&apos;s leads, deals, contacts, and tasks.</p>
              <p className="mt-1">Ask what to do next, who went quiet, or about a record by name. Nothing changes until you press a button.</p>
            </div>
          </motion.div>
        ) : (
          <ol className="space-y-4" aria-label="Conversation with the agent">
            {turns.map((turn) => {
              const actions = readOnly ? turn.actions.filter((action) => action.kind === 'open' || action.kind === 'draft') : turn.actions;
              return (
                <li key={turn.id}>
                  {turn.role === 'you' ? (
                    <UserBubble text={turn.text} fresh={!!turn.fresh} reduce={reduce} />
                  ) : (
                    <AgentBubble text={turn.text} saved={turn.saved} fresh={!!turn.fresh} reduce={reduce}>
                      {actions.length > 0 && actions.map((action, index) => (
                        <ActionChip
                          key={action.id}
                          action={action}
                          busy={busy}
                          pending={pendingId === action.id}
                          index={index}
                          reduce={reduce}
                          onRun={(chosen) => void run(chosen)}
                        />
                      ))}
                    </AgentBubble>
                  )}
                </li>
              );
            })}
          </ol>
        )}
        <AnimatePresence>
          {thinking && (
            <div className="mt-4">
              <ThinkingBubble reduce={reduce} />
            </div>
          )}
        </AnimatePresence>
      </div>

      <div className="relative border-t border-fuchsia-100/80 bg-white/70 px-4 pb-4 pt-3 backdrop-blur-sm dark:border-fuchsia-900/40 dark:bg-slate-900/70">
        <AnimatePresence initial={false}>
          {notice && (
            <motion.p
              key={notice}
              role="status"
              initial={reduce ? false : { opacity: 0, y: 6, height: 0 }}
              animate={{ opacity: 1, y: 0, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.25, ease: EASE }}
              className="mb-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300"
            >
              {notice}
            </motion.p>
          )}
        </AnimatePresence>
        <div className="mb-3">
          <SuggestionChips
            suggestions={suggestions}
            disabled={busy || thinking}
            reduce={reduce}
            onPick={send}
            label={empty ? 'Questions to start with' : 'Questions to ask next'}
          />
        </div>
        <form onSubmit={onSubmit} className="group/composer relative">
          <label className="sr-only" htmlFor="agent-question">Ask the agent</label>
          {/* A gradient ring that lights up while typing. */}
          <span
            aria-hidden="true"
            className="pointer-events-none absolute -inset-px rounded-2xl bg-gradient-to-r from-orange-400 via-fuchsia-500 to-violet-500 opacity-0 blur-[1px] transition-opacity duration-300 group-focus-within/composer:opacity-100"
          />
          <div className="relative flex items-end gap-2 rounded-2xl border border-slate-200 bg-white p-1.5 pl-3.5 shadow-sm dark:border-slate-600 dark:bg-slate-950">
            <textarea
              id="agent-question"
              ref={attachField}
              rows={1}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              onKeyDown={onKeyDown}
              placeholder="Ask Cadence AI anything…"
              className="max-h-[132px] min-h-[40px] flex-1 resize-none bg-transparent py-2.5 text-sm leading-5 text-slate-900 shadow-none outline-none ring-0 placeholder:text-slate-400 focus:outline-none focus:ring-0 focus-visible:outline-none dark:text-slate-100"
            />
            <motion.button
              type="submit"
              aria-label="Send"
              disabled={!question.trim() || busy || thinking}
              whileHover={reduce ? undefined : { scale: 1.06 }}
              whileTap={reduce ? undefined : { scale: 0.92 }}
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-fuchsia-500 text-white shadow-md shadow-fuchsia-500/25 transition-opacity focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 focus-visible:ring-offset-2 disabled:opacity-40"
            >
              <ArrowUp className="h-5 w-5" aria-hidden="true" />
            </motion.button>
          </div>
        </form>
        <p className="mt-2 hidden items-center gap-1.5 text-[11px] text-slate-400 sm:flex">
          <ShieldCheck className="h-3 w-3" aria-hidden="true" />
          Enter to send, Shift+Enter for a new line. A change runs only when you press its button, and only once.
        </p>
      </div>
    </motion.section>
      )}
    </AnimatePresence>
  );
}
