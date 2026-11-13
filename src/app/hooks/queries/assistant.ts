// The assistant's settings and the person's morning brief, as cached queries keyed by
// workspace. The fourth part of each key is 'agent', the kind api/dataChanged names for a
// write under /api/agent: saving the assistant's settings refreshes both.

import { useQuery } from '@tanstack/react-query';
import { getAssistantSettings, getMorningBrief } from '@/app/api/assistant';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (s: WorkspaceScope, ...rest: unknown[]) => [...workspacePrefix(s), 'agent', ...rest] as const;

export const assistantKeys = {
  settings: (s: WorkspaceScope) => key(s, 'settings'),
  brief: (s: WorkspaceScope) => key(s, 'brief'),
} as const;

/**
 * The workspace's settings for the assistant: whether a language model is set up (the chat
 * asks it, or answers from its built-in rules), the month's usage and the limits. Off outside
 * a workspace, where there is no assistant to ask.
 */
export function useAssistantSettings(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: assistantKeys.settings(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getAssistantSettings({ signal: s })),
    enabled: enabled && ws.orgId !== null,
    // Whether a model is set up changes only with the server's configuration.
    staleTime: 5 * 60_000,
    retry: false,
  });
}

/** Whether the chat may ask the language model: one is set up, as far as the server has said. */
export function useModelReady(enabled = true): boolean {
  const settings = useAssistantSettings(enabled);
  return settings.data?.configured === true;
}

/** The person's morning brief: the day now, and the one sent this morning. Read afresh each time it is shown. */
export function useMorningBrief() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: assistantKeys.brief(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getMorningBrief({ signal: s })),
    enabled: ws.orgId !== null,
    staleTime: 0,
  });
}
