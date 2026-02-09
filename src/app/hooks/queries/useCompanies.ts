import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { getCompanies, getCompaniesPaged, createCompany, updateCompany, deleteCompany } from '@/app/api/companies';
import type { Company, PaginationParams, PagedResult } from '@/app/api/types';

/**
 * Hook to fetch companies with pagination and search (HP-8: primary data-fetching hook).
 * Uses keepPreviousData for smooth page transitions.
 */
export function useCompaniesPaged(params: PaginationParams = {}) {
  const { page = 1, pageSize = 20, search } = params;
  return useQuery({
    queryKey: queryKeys.companies.list({ page, pageSize, search }),
    queryFn: () => getCompaniesPaged({ page, pageSize, search }),
    placeholderData: keepPreviousData,
    staleTime: 30_000, // 30s — companies change infrequently
  });
}

/**
 * Hook to fetch all companies (non-paginated, for dropdowns/selects).
 */
export function useCompanies() {
  return useQuery({
    queryKey: queryKeys.companies.lists(),
    queryFn: getCompanies,
  });
}

/**
 * Hook to fetch a single company by ID.
 * Fetches from the paginated cache first, then falls back to fetching all.
 */
export function useCompanyById(id: string | undefined) {
  const { data: companies } = useCompanies();
  
  return useQuery({
    queryKey: queryKeys.companies.detail(id ?? ''),
    queryFn: () => companies?.find((c) => c.id === id) ?? null,
    enabled: !!id && !!companies,
  });
}

/**
 * Hook to create a new company with cache invalidation.
 */
export function useCreateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createCompany,
    onSuccess: (newCompany) => {
      if (newCompany) {
        // Invalidate all company queries to refetch fresh data
        queryClient.invalidateQueries({ queryKey: queryKeys.companies.all });
        toast.success('Company created successfully');
      }
    },
    onError: () => {
      toast.error('Failed to create company');
    },
  });
}

/**
 * Hook to update a company with cache invalidation.
 */
export function useUpdateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateCompany>[1]) =>
      updateCompany(id, params),
    onSuccess: (updatedCompany) => {
      if (updatedCompany) {
        // Invalidate all company queries to refetch fresh data
        queryClient.invalidateQueries({ queryKey: queryKeys.companies.all });
        toast.success('Company updated successfully');
      }
    },
    onError: () => {
      toast.error('Failed to update company');
    },
  });
}

/**
 * Hook to delete a company with cache invalidation.
 */
export function useDeleteCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteCompany,
    onSuccess: (success) => {
      if (success) {
        // Invalidate all company queries to refetch fresh data
        queryClient.invalidateQueries({ queryKey: queryKeys.companies.all });
        toast.success('Company deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete company');
    },
  });
}
