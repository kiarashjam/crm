// An edit refused because someone else saved the record after this page read it.
//
// Lead, deal, contact and company edit dialogs send the record's `version` with their
// save. When anyone has saved the record since (a colleague, the pipeline, a job), the
// server answers 409 with a code ending ".Changed.Conflict" instead of writing the
// dialog's older copy over their change. The person is told so, and offered the latest
// copy to make their change again on.

import { toast } from 'sonner';
import { ApiError } from '@/app/api/apiClient';

const FALLBACK =
  'Someone else changed this after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.';

/** Whether a save was refused because the record changed after this page read it. */
export function isEditConflict(err: unknown): err is ApiError {
  return err instanceof ApiError && err.status === 409 && !!err.code?.endsWith('Changed.Conflict');
}

/** Whether a problem+json error code says the record changed after this page read it. */
export function isEditConflictCode(code: string | undefined | null): boolean {
  return !!code?.endsWith('Changed.Conflict');
}

/**
 * Says the record changed under the person, with a Reload button that brings the
 * latest copy in. The server's sentence is used when it gave one.
 */
export function showEditConflict(message: string | undefined | null, reload: () => void): void {
  toast.error(message?.trim() ? message : FALLBACK, {
    duration: 12000,
    action: { label: 'Reload', onClick: reload },
  });
}
