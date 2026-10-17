import { toast } from 'sonner';
import { ApiError } from '@/app/api/apiClient';
import { createLead } from '@/app/api/leads';
import type { Lead } from '@/app/api/types';

type CreateLeadParams = Parameters<typeof createLead>[0];

/**
 * Create a lead. When an open lead already has this email, the server refuses
 * with a 409 that names it — which used to surface as "Something went wrong",
 * with no way past it. Now the toast says whose lead it is and offers to add this
 * one anyway; `onCreatedAnyway` runs if the user takes that.
 *
 * Resolves to the new lead, `'offered'` when the duplicate prompt is showing
 * instead, or null when the server returned nothing. Other failures throw.
 */
export async function createLeadOrOfferDuplicate(
  params: CreateLeadParams,
  onCreatedAnyway: (lead: Lead) => void | Promise<void>,
): Promise<Lead | null | 'offered'> {
  try {
    return await createLead(params);
  } catch (err) {
    if (!(err instanceof ApiError) || err.status !== 409) throw err;
    toast.warning(err.message || 'An open lead with this email already exists.', {
      duration: 12_000,
      action: {
        label: 'Create anyway',
        onClick: () => {
          createLead({ ...params, allowDuplicate: true })
            .then((lead) => (lead ? onCreatedAnyway(lead) : toast.error('Could not create the lead')))
            .catch((e: unknown) => toast.error(e instanceof Error && e.message ? e.message : 'Could not create the lead'));
        },
      },
    });
    return 'offered';
  }
}
