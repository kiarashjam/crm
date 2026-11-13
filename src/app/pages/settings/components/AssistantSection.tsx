// Settings › Assistant: whether a language model answers the chat and writes the morning
// brief, this month's calls to it against the workspace's limit, and the two things a
// manager may change (settings.manage): the monthly limit, under the server's ceiling, and
// whether the workspace's people get a morning brief. Everyone else reads them.

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Bot, CheckCircle2, Info, Loader2, Sun } from 'lucide-react';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { saveAssistantSettings, type AssistantSettings, type AssistantSettingsUpdate } from '@/app/api/assistant';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { assistantKeys, useAssistantSettings } from '@/app/hooks/queries/assistant';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useFormat } from '@/app/i18n';

export function AssistantSection() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const f = useFormat();
  const canManage = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
  const settings = useAssistantSettings();
  const [limit, setLimit] = useState('');

  useEffect(() => {
    if (settings.data) setLimit(String(settings.data.monthlyLimit));
  }, [settings.data]);

  const save = useMutation({
    mutationFn: (change: AssistantSettingsUpdate) => saveAssistantSettings({ ...change, version: settings.data?.version }),
    onSuccess: (saved: AssistantSettings) => {
      client.setQueryData(assistantKeys.settings(scope), saved);
      toast.success('Saved.');
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error('Someone else changed these settings meanwhile. They are shown as they are now: make your change again.');
        void settings.refetch();
        return;
      }
      toast.error(err instanceof Error ? err.message : 'The setting could not be saved.');
    },
  });

  const header = (
    <div>
      <h2 className="flex items-center gap-2 text-xl font-semibold text-slate-900">
        <Bot className="h-5 w-5 text-orange-600" aria-hidden="true" />
        Assistant
      </h2>
      <p className="mt-1 text-sm text-slate-600">The chat, the morning brief, and what calls to a language model may cost this workspace each month.</p>
    </div>
  );

  if (!currentOrg) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="text-sm text-slate-600">The assistant works in a workspace. Open one to see its settings.</p>
      </div>
    );
  }
  if (servesDemoData()) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="text-sm text-slate-600">Demo data has no language model behind it: the chat answers from Cadence&apos;s built-in rules.</p>
      </div>
    );
  }
  if (settings.isError && !settings.data) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <LoadFailed what="The assistant's settings" error={settings.error} onRetry={() => void settings.refetch()} retrying={settings.isFetching} />
      </div>
    );
  }
  const s = settings.data;
  if (!s) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
      </div>
    );
  }

  const asked = Number(limit);
  const limitValid = limit.trim() !== '' && Number.isInteger(asked) && asked >= 0 && asked <= s.maxLimit;
  const usedShare = s.usage.limit > 0 ? Math.min(100, Math.round((s.usage.used / s.usage.limit) * 100)) : 100;

  return (
    <div className="space-y-8 p-6">
      {header}

      <section aria-labelledby="assistant-model" className="rounded-xl border border-slate-200 p-4">
        <h3 id="assistant-model" className="font-semibold text-slate-900">Language model</h3>
        {s.configured ? (
          <p className="mt-1 flex items-start gap-2 text-sm text-slate-700">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-hidden="true" />
            <span>
              Set up: {s.provider}{s.model ? ` (${s.model})` : ''} answers the chat, reading the records as each person may see them, and writes a few sentences on top of the morning brief. It only proposes changes; each runs when the person confirms it.
            </span>
          </p>
        ) : (
          <p className="mt-1 flex items-start gap-2 text-sm text-slate-700">
            <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden="true" />
            <span>
              Not set up. The chat answers from Cadence&apos;s built-in rules and the morning brief goes out as a plain list. Whoever runs this server turns it on with an OpenAI API key (OpenAI:ApiKey).
            </span>
          </p>
        )}
      </section>

      <section aria-labelledby="assistant-usage" className="space-y-3">
        <h3 id="assistant-usage" className="font-semibold text-slate-900">This month</h3>
        <p className="text-sm text-slate-700">
          {s.usage.limit === 0
            ? 'The AI is off for this workspace: its monthly limit is 0.'
            // A calendar day from the server (yyyy-MM-dd), read at noon so no time zone moves it to the day before.
            : `${s.usage.used} of ${s.usage.limit} calls to the language model used; the count starts again on ${f.date(`${s.usage.resetsOn}T12:00:00`, 'long')}.`}
          {s.usage.refused > 0 && ` ${s.usage.refused} refused because the month's calls were used up.`}
        </p>
        {s.usage.limit > 0 && (
          <div className="h-2 w-full max-w-md overflow-hidden rounded-full bg-slate-100" role="meter" aria-label="Calls used this month" aria-valuemin={0} aria-valuemax={s.usage.limit} aria-valuenow={s.usage.used}>
            <div className={`h-full rounded-full ${usedShare >= 90 ? 'bg-rose-500' : 'bg-orange-500'}`} style={{ width: `${usedShare}%` }} />
          </div>
        )}
        <p className="text-xs text-slate-500">
          Each step of an answer, each morning brief written by the model and each draft from the AI writer counts as a call. Morning briefs are written by the model only while at least half of the month&apos;s calls are left; after that they go as plain lists, so the rest stays for questions.
        </p>
      </section>

      <section aria-labelledby="assistant-limit" className="space-y-3">
        <h3 id="assistant-limit" className="font-semibold text-slate-900">Monthly limit</h3>
        <p className="text-sm text-slate-600">
          {s.customLimit === null
            ? `This workspace uses the server's default: ${s.defaultLimit} calls a month.`
            : `This workspace's own limit: ${s.customLimit} calls a month (the server's default is ${s.defaultLimit}).`}
          {' '}The server allows up to {s.maxLimit}.
        </p>
        {canManage ? (
          <form
            className="flex flex-wrap items-end gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              if (limitValid) save.mutate({ monthlyLimit: asked });
            }}
          >
            <div className="space-y-1">
              <Label htmlFor="assistant-monthly-limit">Calls a month</Label>
              <Input
                id="assistant-monthly-limit"
                type="number"
                min={0}
                max={s.maxLimit}
                step={1}
                value={limit}
                onChange={(event) => setLimit(event.target.value)}
                className="w-36"
                aria-invalid={!limitValid}
              />
            </div>
            <Button type="submit" disabled={!limitValid || save.isPending || (asked === s.monthlyLimit && s.customLimit !== null)}>
              {save.isPending ? 'Saving…' : 'Save limit'}
            </Button>
            {s.customLimit !== null && (
              <Button type="button" variant="outline" disabled={save.isPending} onClick={() => save.mutate({ useDefaultLimit: true })}>
                Use the default ({s.defaultLimit})
              </Button>
            )}
            {!limitValid && <p className="w-full text-xs text-rose-600">A whole number from 0 to {s.maxLimit}. 0 turns the AI off for this workspace.</p>}
          </form>
        ) : (
          <p className="text-xs text-slate-500">Only someone who manages the workspace&apos;s settings can change it.</p>
        )}
      </section>

      <section aria-labelledby="assistant-briefs" className="space-y-3 border-t border-slate-200 pt-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h3 id="assistant-briefs" className="flex items-center gap-2 font-semibold text-slate-900">
              <Sun className="h-4 w-4 text-amber-500" aria-hidden="true" />
              Morning briefs
            </h3>
            <p className="mt-1 text-sm text-slate-600">
              From 7:00 on the workspace&apos;s calendar, each person gets a notification with their tasks due, their deals gone quiet and, as their role allows, members past due, today&apos;s bookings and events, and renewals coming up. Each person can turn theirs off, or have it by email too, in their notification settings.
            </p>
          </div>
          <Switch
            checked={s.morningBriefs}
            disabled={!canManage || save.isPending}
            onCheckedChange={(on) => save.mutate({ morningBriefs: on })}
            aria-label="Morning briefs for this workspace"
          />
        </div>
      </section>
    </div>
  );
}
