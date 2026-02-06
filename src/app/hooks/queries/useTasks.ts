import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from './queryKeys';
import { 
  getTasks, 
  getTaskStats, 
  createTask, 
  updateTask, 
  deleteTask,
  updateTaskStatus,
  GetTasksOptions 
} from '@/app/api/tasks';
import type { TaskItem, TaskStatusType } from '@/app/api/types';

/**
 * Hook to fetch all tasks with optional filters
 */
export function useTasks(options?: GetTasksOptions) {
  return useQuery({
    queryKey: queryKeys.tasks.list(options as Record<string, unknown> | undefined),
    queryFn: () => getTasks(options),
  });
}

/**
 * Hook to fetch task statistics
 */
export function useTaskStats() {
  return useQuery({
    queryKey: [...queryKeys.tasks.all, 'stats'],
    queryFn: getTaskStats,
  });
}

/**
 * Hook to fetch a single task by ID
 */
export function useTaskById(id: string | undefined) {
  const { data: tasks } = useTasks();
  
  return useQuery({
    queryKey: queryKeys.tasks.detail(id ?? ''),
    queryFn: () => tasks?.find((t) => t.id === id) ?? null,
    enabled: !!id && !!tasks,
  });
}

/**
 * Hook to create a new task
 */
export function useCreateTask() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createTask,
    onSuccess: (newTask) => {
      if (newTask) {
        // Invalidate all task queries to refresh the list
        queryClient.invalidateQueries({ queryKey: queryKeys.tasks.all });
        toast.success('Task created successfully');
      }
    },
    onError: () => {
      toast.error('Failed to create task');
    },
  });
}

/**
 * Hook to update a task
 */
export function useUpdateTask() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...params }: { id: string } & Parameters<typeof updateTask>[1]) =>
      updateTask(id, params),
    onSuccess: (updatedTask) => {
      if (updatedTask) {
        queryClient.invalidateQueries({ queryKey: queryKeys.tasks.all });
        toast.success('Task updated successfully');
      }
    },
    onError: () => {
      toast.error('Failed to update task');
    },
  });
}

/**
 * Hook to update task status
 */
export function useUpdateTaskStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: TaskStatusType }) =>
      updateTaskStatus(id, status),
    onMutate: async ({ id, status }) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: queryKeys.tasks.all });

      // Snapshot the previous value
      const previousTasks = queryClient.getQueryData<TaskItem[]>(queryKeys.tasks.lists());

      // Optimistically update
      if (previousTasks) {
        queryClient.setQueryData<TaskItem[]>(
          queryKeys.tasks.lists(),
          previousTasks.map((task) =>
            task.id === id ? { ...task, status, completed: status === 'completed' } : task
          )
        );
      }

      return { previousTasks };
    },
    onError: (_err, _variables, context) => {
      // Roll back on error
      if (context?.previousTasks) {
        queryClient.setQueryData(queryKeys.tasks.lists(), context.previousTasks);
      }
      toast.error('Failed to update task status');
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tasks.all });
    },
  });
}

/**
 * Hook to delete a task
 */
export function useDeleteTask() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteTask,
    onSuccess: (success, deletedId) => {
      if (success) {
        queryClient.setQueryData<TaskItem[]>(
          queryKeys.tasks.lists(),
          (old) => old?.filter((task) => task.id !== deletedId) ?? []
        );
        toast.success('Task deleted successfully');
      }
    },
    onError: () => {
      toast.error('Failed to delete task');
    },
  });
}
