import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { getLeads, createLead, updateLead, deleteLead, searchLeads } from '@/app/api/leads';
import type { Lead } from '@/app/api/types';

/**
 * Hook to fetch all leads
 */
export function useLeads() {
  return useQuery({
    queryKey: queryKeys.leads.lists(),
    queryFn: getLeads,
  });
}

/**
 * Hook to search leads
 */
export function useSearchLeads(query: string) {
  return useQuery({
    queryKey: queryKeys.leads.list({ search: query }),
    queryFn: () => searchLeads(query),
    enabled: query.length > 0,
  });
}

/**
 * Hook to fetch a single lead by ID
 */
export function useLeadById(id: string | undefined) {
  const { data: leads } = useLeads();
  
  return useQuery({
    queryKey: queryKeys.leads.detail(id ?? ''),
    queryFn: () => leads?.find((l) => l.id === id) ?? null,
    enabled: !!id && !!leads,
  });
}

/**
 * Hook to create a new lead
 */
export function useCreateLead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createLead,
    onSuccess: (newLead) => {
      if (newLead) {
        // Update the leads list cache
        queryClient.setQueryData<Lead[]>(
          queryKeys.leads.lists(),
          (old) => (old ? [...old, newLead] : [newLead])
        );
        toast.success('Lead created successfully');
      }
    },
    onError: () => {
      toast.error('Failed to create lead');
    },
  });
}

/**
 * Hook to update a lead
 */
export function useUpdateLead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateLead>[1]) =>
      updateLead(id, params),
    onSuccess: (updatedLead) => {
      if (updatedLead) {
        // Update the leads list cache
        queryClient.setQueryData<Lead[]>(
          queryKeys.leads.lists(),
          (old) =>
            old?.map((lead) =>
              lead.id === updatedLead.id ? updatedLead : lead
            ) ?? []
        );
        toast.success('Lead updated successfully');
      }
    },
    onError: () => {
      toast.error('Failed to update lead');
    },
  });
}

/**
 * Hook to delete a lead
 */
export function useDeleteLead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteLead,
    onSuccess: (success, deletedId) => {
      if (success) {
        // Remove from the leads list cache
        queryClient.setQueryData<Lead[]>(
          queryKeys.leads.lists(),
          (old) => old?.filter((lead) => lead.id !== deletedId) ?? []
        );
        toast.success('Lead deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete lead');
    },
  });
}
