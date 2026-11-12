// The workspace's own values a rule can pick from: its lead statuses and sources, its
// stages and pipelines, its people, its tiers, spaces and events, and its sequences.
//
// Each list is read under the same key the rest of the app reads it by, so the cache is
// shared, and only when a field of the rule being edited needs it. A list that cannot be
// read (a module turned off, a failed read) leaves its field to be typed in instead.

import { useQuery } from '@tanstack/react-query';
import { getLeadSources, getLeadStatuses, getOrgMembers, getPipelines } from '@/app/api';
import { servesDemoData } from '@/app/api/apiClient';
import { getCatalog } from '@/app/api/catalog';
import { getEvents } from '@/app/api/events';
import { getResources } from '@/app/api/reservations';
import { getSequences } from '@/app/api/sequences';
import type { AutomationChoice, AutomationFieldDef, AutomationTriggerDef } from '@/app/api/automations';
import { queryKeys, timed, useWorkspaceScope } from '@/app/hooks/queries';

export type OptionLists = Partial<Record<string, AutomationChoice[]>>;

const LIST_STALE_MS = 5 * 60 * 1000;

/**
 * The option lists a rule on this trigger reads: those its choice fields offer, the
 * workspace's people (whom a task goes to, whom to tell or assign), and its sequences
 * where the trigger can enroll in one.
 */
export function optionsNeeded(trigger: AutomationTriggerDef | undefined): Set<string> {
  const needed = new Set((trigger?.fields ?? []).map((f) => f.options).filter((o): o is string => !!o));
  needed.add('user');
  if (trigger?.actions.includes('add_to_sequence')) needed.add('sequence');
  return needed;
}

export function useConditionOptions(needed: Set<string>): OptionLists {
  const ws = useWorkspaceScope();
  const statuses = useQuery({
    queryKey: queryKeys.leadLookups.statuses(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadStatuses({ signal: s })),
    staleTime: LIST_STALE_MS,
    enabled: needed.has('leadStatus'),
  });
  const sources = useQuery({
    queryKey: queryKeys.leadLookups.sources(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadSources({ signal: s })),
    staleTime: LIST_STALE_MS,
    enabled: needed.has('leadSource'),
  });
  const pipelines = useQuery({
    queryKey: queryKeys.pipelines.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPipelines({ signal: s })),
    enabled: needed.has('dealStage') || needed.has('pipeline'),
  });
  const people = useQuery({
    queryKey: queryKeys.team.members(ws),
    // Demo mode has its own people, with no workspace of its own.
    queryFn: ({ signal }) => timed(signal, (s) => getOrgMembers(ws.orgId ?? '', { signal: s })),
    enabled: (!!ws.orgId || servesDemoData()) && needed.has('user'),
  });
  const tiers = useQuery({
    queryKey: queryKeys.club.catalog(ws, 'Membership'),
    queryFn: ({ signal }) => timed(signal, (s) => getCatalog('Membership', { signal: s })),
    enabled: needed.has('tier'),
  });
  const resources = useQuery({
    queryKey: queryKeys.club.resources(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getResources({ signal: s })),
    enabled: needed.has('resource'),
  });
  const events = useQuery({
    queryKey: queryKeys.club.events(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getEvents({ signal: s })),
    enabled: needed.has('event'),
  });
  const sequences = useQuery({
    queryKey: queryKeys.sequences.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getSequences({ signal: s })),
    enabled: needed.has('sequence'),
  });

  const lists: OptionLists = {};
  if (statuses.data?.length) lists.leadStatus = statuses.data.map((s) => ({ value: s.name, label: s.name }));
  if (sources.data?.length) lists.leadSource = sources.data.map((s) => ({ value: s.name, label: s.name }));
  const allPipelines = pipelines.data ?? [];
  if (allPipelines.length) {
    lists.pipeline = allPipelines.map((p) => ({ value: p.id, label: p.name }));
    const stages = allPipelines.flatMap((p) => (p.dealStages ?? []).map((s) => ({
      value: s.id, label: allPipelines.length > 1 ? `${s.name} (${p.name})` : s.name,
    })));
    if (stages.length) lists.dealStage = stages;
  }
  if (people.data?.length) lists.user = people.data.map((p) => ({ value: p.userId, label: p.name || p.email }));
  if (tiers.data?.length) lists.tier = tiers.data.map((t) => ({ value: t.name, label: t.name }));
  if (resources.data?.length) lists.resource = resources.data.map((r) => ({ value: r.id, label: r.name }));
  if (events.data?.length) lists.event = events.data.map((e) => ({ value: e.id, label: e.name }));
  if (sequences.data?.length) lists.sequence = sequences.data.map((q) => ({ value: q.id, label: q.name }));
  return lists;
}

/** The choices a field offers: its fixed ones, else the workspace's; none means it is typed in. */
export function choicesOf(field: AutomationFieldDef | undefined, lists: OptionLists): AutomationChoice[] | null {
  if (!field) return null;
  if (field.choices?.length) return field.choices;
  const list = field.options ? lists[field.options] : undefined;
  if (list?.length) return list;
  return null;
}
