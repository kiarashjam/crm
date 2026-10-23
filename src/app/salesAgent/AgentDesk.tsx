import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { createActivity } from '@/app/api/activities';
import { createContact } from '@/app/api/contacts';
import { mergeCompanies, mergeContacts } from '@/app/api/dedupe';
import { createDeal, updateDeal } from '@/app/api/deals';
import { assignLead, convertLead } from '@/app/api/leads';
import { resendInvite } from '@/app/api/organizations';
import { enrollInSequence } from '@/app/api/sequences';
import { addTaskComment, createTask } from '@/app/api/tasks';
import { sendEmail } from '@/app/api/email';
import { getUserSettings, saveUserSettings } from '@/app/api/settings';
import { getCurrentUser } from '@/app/lib/auth';
import { askAgent, type AgentAction, type AgentReply, type AgentWorld } from './brain/answer';
import { emptyMemory, remember, type AgentMemory } from './brain/reason';
import { invalidateWork } from './agentSession';

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
  role: 'you' | 'agent';
  text: string;
  actions: AgentAction[];
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
    if (!raw) return [];
    const parsed = JSON.parse(raw) as Turn[];
    return Array.isArray(parsed) ? parsed.slice(-12) : [];
  } catch {
    return [];
  }
}

function memoryPayload(accountKey: string, memory: AgentMemory, turns: Turn[]): string {
  const orgId = accountKey.split(':')[1] ?? '';
  let kept = turns.slice(-12);
  let payload = JSON.stringify({ orgId, ...memory, turns: kept });
  while (kept.length > 0 && payload.length > 3900) {
    kept = kept.slice(1);
    payload = JSON.stringify({ orgId, ...memory, turns: kept });
  }
  return payload.length > 3900 ? JSON.stringify({ orgId, ...memory, turns: [] }) : payload;
}

function parseAccountMemory(raw: string | undefined, accountKey: string): { memory: AgentMemory; turns: Turn[] } | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Partial<AgentMemory> & { orgId?: unknown; turns?: unknown };
    const orgId = accountKey.split(':')[1] ?? '';
    if (parsed.orgId !== orgId) return null;
    const turns = Array.isArray(parsed.turns) ? (parsed.turns as Turn[]).slice(-12) : [];
    return { memory: asMemory(parsed), turns };
  } catch {
    return null;
  }
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
  const memory = useRef<AgentMemory>(readFocus(accountKey));
  const accountReady = useRef(false);
  const savedActions = useRef(new Set<string>());
  const inflight = useRef(new Set<string>());

  useEffect(() => {
    memory.current = readFocus(accountKey);
    setTurns(readTurns(accountKey));
    accountReady.current = false;
  }, [accountKey]);

  useEffect(() => {
    let cancelled = false;
    getUserSettings()
      .then((settings) => {
        if (cancelled) return;
        const saved = parseAccountMemory(settings.agentMemoryJson, accountKey);
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
    try {
      writeRaw(scopedKey(accountKey, TURNS_BASE), JSON.stringify(turns));
    } catch {
      /* The conversation still works for this page view. */
    }
    if (!accountReady.current) return undefined;
    const payload = memoryPayload(accountKey, memory.current, turns);
    const handle = window.setTimeout(() => {
      saveUserSettings({ agentMemoryJson: payload }).catch(() => undefined);
    }, 400);
    return () => window.clearTimeout(handle);
  }, [turns, accountKey]);

  function push(role: Turn['role'], text: string, actions: AgentAction[] = []) {
    setTurns((current) => [...current, { role, text, actions }].slice(-12));
  }

  function ask(text: string) {
    const reply: AgentReply = askAgent(text, world, new Date(), memory.current);
    const focus = reply.focusKey ? world.items.find((item) => item.key === reply.focusKey) ?? null : null;
    memory.current = remember(focus, memory.current, { leads: world.leads, deals: world.deals, contacts: world.contacts });
    try {
      writeRaw(scopedKey(accountKey, FOCUS_BASE), JSON.stringify(memory.current));
    } catch {
      /* The next question on this page still sees the ref. */
    }
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
    invalidateWork(accountKey);
    setTurns((current) => [...current.map((turn) => ({
      ...turn,
      actions: turn.actions.filter((item) => item.id !== action.id),
    })), { role: 'agent' as const, text, actions: [] }].slice(-12));
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
    inflight.current.add(action.id);
    setBusy(true);
    try {
      if (action.kind === 'task') {
        const created = await createTask({
          title: action.title || 'Follow up',
          dueDateUtc: action.dueDateUtc,
          leadId: action.leadId,
          dealId: action.dealId,
          contactId: action.contactId,
        });
        if (created) confirmSaved(action, `Task saved: ${created.title}.`);
        else push('agent', 'The task was not saved.', []);
      }
      if (action.kind === 'note') {
        const created = await createActivity({
          type: 'note',
          body: action.body,
          subject: 'Note from the agent',
          leadId: action.leadId,
          dealId: action.dealId,
          contactId: action.contactId,
        });
        if (created) confirmSaved(action, 'Note saved on the record.');
        else push('agent', 'The note was not saved.', []);
      }
      if (action.kind === 'comment' && action.taskId) {
        const created = await addTaskComment(action.taskId, action.body || action.title || 'Follow up');
        if (created) confirmSaved(action, 'Comment saved on the task.');
        else push('agent', 'The comment was not saved.', []);
      }
      if (action.kind === 'resend' && action.inviteId) {
        const sent = await resendInvite(action.inviteId);
        if (sent) confirmSaved(action, `Invitation resent to ${sent.email}.`);
        else push('agent', 'The invitation was not resent.', []);
      }
      if (action.kind === 'send' && action.to) {
        const sent = await sendEmail({
          to: action.to,
          subject: action.title || 'Following up',
          body: action.body || '',
          leadId: action.leadId,
          dealId: action.dealId,
          contactId: action.contactId,
        });
        if (sent) confirmSaved(action, `Sent to ${action.to}.`);
        else push('agent', 'The message was not sent.', []);
      }
      if (action.kind === 'assign' && action.leadId) {
        const user = getCurrentUser();
        const saved = user ? await assignLead(action.leadId, user.id) : null;
        if (saved) confirmSaved(action, `${saved.name} is assigned to you.`);
        else push('agent', 'The lead was not assigned. Sign in so the agent knows who you are.', []);
      }
      if (action.kind === 'convert' && action.leadId) {
        const saved = await convertLead(action.leadId, {
          createContact: true,
          createDeal: true,
          dealName: action.recipientName,
        });
        if (saved) confirmSaved(action, `${action.recipientName ?? 'The lead'} was converted.`);
        else push('agent', 'The lead was not converted.', []);
      }
      if (action.kind === 'enroll' && action.sequenceId && action.leadId) {
        const saved = await enrollInSequence({
          sequenceId: action.sequenceId,
          targetType: 'lead',
          targetId: action.leadId,
          targetName: action.recipientName || 'Lead',
          targetEmail: action.to,
        });
        if (saved) confirmSaved(action, `${action.recipientName ?? 'The lead'} is enrolled.`);
        else push('agent', 'The enrollment was not saved.', []);
      }
      if (action.kind === 'merge' && action.mergePrimaryId && action.mergeDuplicateIds?.length) {
        const saved = action.mergeKind === 'companies'
          ? await mergeCompanies(action.mergePrimaryId, action.mergeDuplicateIds)
          : await mergeContacts(action.mergePrimaryId, action.mergeDuplicateIds);
        if (saved) confirmSaved(action, 'The duplicate group was merged.');
        else push('agent', 'The merge was not saved.', []);
      }
      if (action.kind === 'stage' && action.dealId && action.stageName) {
        const saved = await updateDeal(action.dealId, {
          stage: action.stageName,
          ...(action.stageId ? { dealStageId: action.stageId } : {}),
        });
        if (saved) confirmSaved(action, `${saved.name} is now in ${action.stageName}.`);
        else push('agent', 'The stage was not changed.', []);
      }
      if (action.kind === 'create-contact' && action.contactName && action.contactEmail) {
        const result = await createContact({
          name: action.contactName,
          email: action.contactEmail,
          phone: action.contactPhone,
          companyId: action.companyId,
        });
        if (result.contact) confirmSaved(action, `Contact saved: ${result.contact.name}, ${result.contact.email}.`);
        else push('agent', result.error ?? 'The contact was not saved.', []);
      }
      if (action.kind === 'create-deal' && action.title && action.dealValue) {
        const saved = await createDeal({
          name: action.title,
          value: action.dealValue,
          currency: action.currency,
          companyId: action.companyId,
          contactId: action.contactId,
        });
        if (saved) confirmSaved(action, `Deal saved: ${saved.name}, amount ${saved.value}.`);
        else push('agent', 'The deal was not saved.', []);
      }
      if (action.kind === 'update-deal' && action.dealId && (action.dealValue || action.dueDateUtc)) {
        const saved = await updateDeal(action.dealId, {
          ...(action.dealValue ? { value: action.dealValue } : {}),
          ...(action.dueDateUtc ? { expectedCloseDateUtc: action.dueDateUtc } : {}),
        });
        if (saved) {
          const amount = action.dealValue ? ` amount ${saved.value}` : '';
          const close = action.dueDateUtc ? ` close date ${saved.expectedCloseDateUtc?.slice(0, 10) ?? action.dueDateUtc}` : '';
          confirmSaved(action, `Deal saved: ${saved.name}.${amount}${close}.`);
        } else push('agent', 'The deal was not updated.', []);
      }
    } catch {
      setNotice('That could not be saved.');
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
      {turns.length > 0 && (
        <ol className="mt-3 space-y-3" aria-live="polite">
          {turns.map((turn, index) => (
            <li key={`${turn.role}-${index}`} className="text-sm">
              <p className="font-medium text-slate-500">{turn.role === 'you' ? 'You' : 'Agent'}</p>
              <p className="mt-1 whitespace-pre-wrap text-slate-800 dark:text-slate-200">{turn.text}</p>
              {turn.actions.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-2">
                  {turn.actions.map((action) => (
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
          ))}
        </ol>
      )}
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
