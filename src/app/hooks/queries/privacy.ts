// A person's data rights and the workspace's privacy settings, as cached queries keyed by
// workspace. The fourth part of each key is 'privacy', the kind of record api/dataChanged
// names for a privacy write: saving the settings, an erasure or a retention run refreshes
// all of them (an erasure and a run change the records too, and refresh those lists).

import { useQuery } from '@tanstack/react-query';
import {
  findPerson, getErasures, getPrivacySettings, getRetentionPreview, getRetentionRuns, previewErasure,
  type PersonLookup, type PersonSelection,
} from '@/app/api/privacy';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (s: WorkspaceScope, ...rest: unknown[]) => [...workspacePrefix(s), 'privacy', ...rest] as const;

const NOTHING_CHOSEN: PersonSelection = { leadIds: [], contactIds: [], memberIds: [], applicationIds: [], addresses: [] };

export const privacyKeys = {
  settings: (s: WorkspaceScope) => key(s, 'settings'),
  find: (s: WorkspaceScope, lookup: PersonLookup) => key(s, 'find', lookup),
  erasurePreview: (s: WorkspaceScope, selection: PersonSelection) => key(s, 'erasure-preview', selection),
  erasures: (s: WorkspaceScope) => key(s, 'erasures'),
  retentionPreview: (s: WorkspaceScope) => key(s, 'retention-preview'),
  retentionRuns: (s: WorkspaceScope) => key(s, 'retention-runs'),
} as const;

/** The workspace's privacy settings. Off for someone who may not handle privacy requests. */
export function usePrivacySettings(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: privacyKeys.settings(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPrivacySettings({ signal: s })),
    enabled,
  });
}

/**
 * The records that look like the person, from an email address or one of their records.
 * Off until something is asked. Never served from an older answer: what was found is what
 * an export or an erasure acts on, so it is read again each time it is asked.
 */
export function usePersonLookup(lookup: PersonLookup | null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: privacyKeys.find(ws, lookup ?? {}),
    queryFn: ({ signal }) => timed(signal, (s) => findPerson(lookup ?? {}, { signal: s })),
    enabled: lookup !== null,
    staleTime: 0,
    gcTime: 0,
    retry: false,
  });
}

/** What erasing these records would do, read without changing anything. Off while `selection` is null. */
export function useErasurePreview(selection: PersonSelection | null) {
  const ws = useWorkspaceScope();
  const asked = selection ?? NOTHING_CHOSEN;
  return useQuery({
    queryKey: privacyKeys.erasurePreview(ws, asked),
    queryFn: ({ signal }) => timed(signal, (s) => previewErasure(asked, { signal: s })),
    enabled: selection !== null,
    staleTime: 0,
    gcTime: 0,
    retry: false,
  });
}

/** The log of erasures, newest first. */
export function useErasures(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: privacyKeys.erasures(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getErasures({ signal: s })),
    enabled,
  });
}

/** What the retention settings would remove if they ran now. Counted afresh each time it is shown. */
export function useRetentionPreview(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: privacyKeys.retentionPreview(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getRetentionPreview({ signal: s })),
    enabled,
    staleTime: 0,
  });
}

/** The retention runs, newest first. */
export function useRetentionRuns(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: privacyKeys.retentionRuns(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getRetentionRuns({ signal: s })),
    enabled,
  });
}
