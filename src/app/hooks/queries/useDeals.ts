import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { getDeals, createDeal, updateDeal, deleteDeal, searchDeals } from '@/app/api/deals';
import type { Deal } from '@/app/api/types';

/**
 * Hook to fetch all deals
 */
export function useDeals() {
  return useQuery({
    queryKey: queryKeys.deals.lists(),
    queryFn: getDeals,
  });
}

/**
 * Hook to search deals
 */
export function useSearchDeals(query: string) {
  return useQuery({
    queryKey: queryKeys.deals.list({ search: query }),
    queryFn: () => searchDeals(query),
    enabled: query.length > 0,
  });
}

/**
 * Hook to fetch a single deal by ID
 */
export function useDealById(id: string | undefined) {
  const { data: deals } = useDeals();
  
  return useQuery({
    queryKey: queryKeys.deals.detail(id ?? ''),
    queryFn: () => deals?.find((d) => d.id === id) ?? null,
    enabled: !!id && !!deals,
  });
}

/**
 * Hook to create a new deal
 */
export function useCreateDeal() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createDeal,
    onSuccess: (newDeal) => {
      if (newDeal) {
        queryClient.setQueryData<Deal[]>(
          queryKeys.deals.lists(),
          (old) => (old ? [...old, newDeal] : [newDeal])
        );
        toast.success('Deal created successfully');
      }
    },
    onError: () => {
      toast.error('Failed to create deal');
    },
  });
}

/**
 * Hook to update a deal
 */
export function useUpdateDeal() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateDeal>[1]) =>
      updateDeal(id, params),
    onSuccess: (updatedDeal) => {
      if (updatedDeal) {
        queryClient.setQueryData<Deal[]>(
          queryKeys.deals.lists(),
          (old) =>
            old?.map((deal) =>
              deal.id === updatedDeal.id ? updatedDeal : deal
            ) ?? []
        );
        toast.success('Deal updated successfully');
      }
    },
    onError: () => {
      toast.error('Failed to update deal');
    },
  });
}

/**
 * Hook to delete a deal
 */
export function useDeleteDeal() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteDeal,
    onSuccess: (success, deletedId) => {
      if (success) {
        queryClient.setQueryData<Deal[]>(
          queryKeys.deals.lists(),
          (old) => old?.filter((deal) => deal.id !== deletedId) ?? []
        );
        toast.success('Deal deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete deal');
    },
  });
}
