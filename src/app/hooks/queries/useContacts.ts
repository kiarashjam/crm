import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { 
  getContacts, 
  createContact, 
  updateContact, 
  deleteContact,
  archiveContact,
  unarchiveContact,
  searchContacts 
} from '@/app/api/contacts';
import type { Contact } from '@/app/api/types';

/**
 * Hook to fetch all contacts
 */
export function useContacts(includeArchived = false) {
  return useQuery({
    queryKey: queryKeys.contacts.list({ includeArchived }),
    queryFn: () => getContacts(includeArchived),
  });
}

/**
 * Hook to search contacts
 */
export function useSearchContacts(query: string, includeArchived = false) {
  return useQuery({
    queryKey: queryKeys.contacts.list({ search: query, includeArchived }),
    queryFn: () => searchContacts(query, includeArchived),
    enabled: query.length > 0,
  });
}

/**
 * Hook to fetch a single contact by ID
 */
export function useContactById(id: string | undefined) {
  const { data: contacts } = useContacts();
  
  return useQuery({
    queryKey: queryKeys.contacts.detail(id ?? ''),
    queryFn: () => contacts?.find((c) => c.id === id) ?? null,
    enabled: !!id && !!contacts,
  });
}

/**
 * Hook to create a new contact
 */
export function useCreateContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createContact,
    onSuccess: (result) => {
      if (result.contact) {
        queryClient.setQueryData<Contact[]>(
          queryKeys.contacts.lists(),
          (old) => (old ? [...old, result.contact!] : [result.contact!])
        );
        toast.success('Contact created successfully');
      } else if (result.error) {
        toast.error(result.error);
      }
    },
    onError: () => {
      toast.error('Failed to create contact');
    },
  });
}

/**
 * Hook to update a contact
 */
export function useUpdateContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateContact>[1]) =>
      updateContact(id, params),
    onSuccess: (result) => {
      if (result.contact) {
        queryClient.setQueryData<Contact[]>(
          queryKeys.contacts.lists(),
          (old) =>
            old?.map((contact) =>
              contact.id === result.contact!.id ? result.contact! : contact
            ) ?? []
        );
        toast.success('Contact updated successfully');
      } else if (result.error) {
        toast.error(result.error);
      }
    },
    onError: () => {
      toast.error('Failed to update contact');
    },
  });
}

/**
 * Hook to delete a contact
 */
export function useDeleteContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteContact,
    onSuccess: (success, deletedId) => {
      if (success) {
        queryClient.setQueryData<Contact[]>(
          queryKeys.contacts.lists(),
          (old) => old?.filter((contact) => contact.id !== deletedId) ?? []
        );
        toast.success('Contact deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete contact');
    },
  });
}

/**
 * Hook to archive a contact
 */
export function useArchiveContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: archiveContact,
    onSuccess: (success) => {
      if (success) {
        queryClient.invalidateQueries({ queryKey: queryKeys.contacts.all });
        toast.success('Contact archived successfully');
      }
    },
    onError: () => {
      toast.error('Failed to archive contact');
    },
  });
}

/**
 * Hook to unarchive a contact
 */
export function useUnarchiveContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: unarchiveContact,
    onSuccess: (success) => {
      if (success) {
        queryClient.invalidateQueries({ queryKey: queryKeys.contacts.all });
        toast.success('Contact unarchived successfully');
      }
    },
    onError: () => {
      toast.error('Failed to unarchive contact');
    },
  });
}
