import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { 
  getTemplates, 
  getTemplateById,
  createTemplate, 
  updateTemplate, 
  deleteTemplate,
  ExtendedTemplate,
  CreateTemplateRequest,
  UpdateTemplateRequest 
} from '@/app/api/templates';

/**
 * Hook to fetch all templates
 */
export function useTemplates() {
  return useQuery({
    queryKey: queryKeys.templates.lists(),
    queryFn: getTemplates,
  });
}

/**
 * Hook to fetch a single template by ID
 */
export function useTemplateById(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.templates.detail(id ?? ''),
    queryFn: () => getTemplateById(id!),
    enabled: !!id,
  });
}

/**
 * Hook to create a new template
 */
export function useCreateTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (params: CreateTemplateRequest) => createTemplate(params),
    onSuccess: (newTemplate) => {
      queryClient.setQueryData<ExtendedTemplate[]>(
        queryKeys.templates.lists(),
        (old) => (old ? [...old, newTemplate] : [newTemplate])
      );
      toast.success('Template created successfully');
    },
    onError: () => {
      toast.error('Failed to create template');
    },
  });
}

/**
 * Hook to update a template
 */
export function useUpdateTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & UpdateTemplateRequest) =>
      updateTemplate(id, params),
    onSuccess: (updatedTemplate) => {
      queryClient.setQueryData<ExtendedTemplate[]>(
        queryKeys.templates.lists(),
        (old) =>
          old?.map((template) =>
            template.id === updatedTemplate.id ? updatedTemplate : template
          ) ?? []
      );
      // Also update the individual template cache
      queryClient.setQueryData(
        queryKeys.templates.detail(updatedTemplate.id),
        updatedTemplate
      );
      toast.success('Template updated successfully');
    },
    onError: () => {
      toast.error('Failed to update template');
    },
  });
}

/**
 * Hook to delete a template
 */
export function useDeleteTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteTemplate,
    onSuccess: (_result, deletedId) => {
      queryClient.setQueryData<ExtendedTemplate[]>(
        queryKeys.templates.lists(),
        (old) => old?.filter((template) => template.id !== deletedId) ?? []
      );
      toast.success('Template deleted successfully');
    },
    onError: () => {
      toast.error('Failed to delete template');
    },
  });
}
