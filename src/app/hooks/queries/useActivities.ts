import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { 
  getActivities, 
  createActivity, 
  deleteActivity,
  getActivitiesByContact,
  getActivitiesByDeal,
  getActivitiesByLead 
} from '@/app/api/activities';
import type { Activity } from '@/app/api/types';

/**
 * Hook to fetch all activities
 */
export function useActivities() {
  return useQuery({
    queryKey: queryKeys.activities.lists(),
    queryFn: getActivities,
  });
}

/**
 * Hook to fetch activities by contact
 */
export function useActivitiesByContact(contactId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.activities.list({ contactId }),
    queryFn: () => getActivitiesByContact(contactId!),
    enabled: !!contactId,
  });
}

/**
 * Hook to fetch activities by deal
 */
export function useActivitiesByDeal(dealId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.activities.list({ dealId }),
    queryFn: () => getActivitiesByDeal(dealId!),
    enabled: !!dealId,
  });
}

/**
 * Hook to fetch activities by lead
 */
export function useActivitiesByLead(leadId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.activities.list({ leadId }),
    queryFn: () => getActivitiesByLead(leadId!),
    enabled: !!leadId,
  });
}

/**
 * Hook to fetch a single activity by ID
 */
export function useActivityById(id: string | undefined) {
  const { data: activities } = useActivities();
  
  return useQuery({
    queryKey: queryKeys.activities.detail(id ?? ''),
    queryFn: () => activities?.find((a) => a.id === id) ?? null,
    enabled: !!id && !!activities,
  });
}

/**
 * Hook to create a new activity
 */
export function useCreateActivity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createActivity,
    onSuccess: (newActivity) => {
      if (newActivity) {
        queryClient.setQueryData<Activity[]>(
          queryKeys.activities.lists(),
          (old) => (old ? [newActivity, ...old] : [newActivity])
        );
        // Also invalidate related queries
        queryClient.invalidateQueries({ queryKey: queryKeys.activities.all });
        toast.success('Activity logged successfully');
      }
    },
    onError: () => {
      toast.error('Failed to log activity');
    },
  });
}

/**
 * Hook to delete an activity
 */
export function useDeleteActivity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteActivity,
    onSuccess: (success, deletedId) => {
      if (success) {
        queryClient.setQueryData<Activity[]>(
          queryKeys.activities.lists(),
          (old) => old?.filter((activity) => activity.id !== deletedId) ?? []
        );
        toast.success('Activity deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete activity');
    },
  });
}
