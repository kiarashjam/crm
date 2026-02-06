import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { getCompanies, createCompany, updateCompany, deleteCompany } from '@/app/api/companies';
import type { Company } from '@/app/api/types';

/**
 * Hook to fetch all companies
 */
export function useCompanies() {
  return useQuery({
    queryKey: queryKeys.companies.lists(),
    queryFn: getCompanies,
  });
}

/**
 * Hook to fetch a single company by ID
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
 * Hook to create a new company
 */
export function useCreateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createCompany,
    onSuccess: (newCompany) => {
      if (newCompany) {
        queryClient.setQueryData<Company[]>(
          queryKeys.companies.lists(),
          (old) => (old ? [...old, newCompany] : [newCompany])
        );
        toast.success('Company created successfully');
      }
    },
    onError: () => {
      toast.error('Failed to create company');
    },
  });
}

/**
 * Hook to update a company
 */
export function useUpdateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateCompany>[1]) =>
      updateCompany(id, params),
    onSuccess: (updatedCompany) => {
      if (updatedCompany) {
        queryClient.setQueryData<Company[]>(
          queryKeys.companies.lists(),
          (old) =>
            old?.map((company) =>
              company.id === updatedCompany.id ? updatedCompany : company
            ) ?? []
        );
        toast.success('Company updated successfully');
      }
    },
    onError: () => {
      toast.error('Failed to update company');
    },
  });
}

/**
 * Hook to delete a company
 */
export function useDeleteCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteCompany,
    onSuccess: (success, deletedId) => {
      if (success) {
        queryClient.setQueryData<Company[]>(
          queryKeys.companies.lists(),
          (old) => old?.filter((company) => company.id !== deletedId) ?? []
        );
        toast.success('Company deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete company');
    },
  });
}
