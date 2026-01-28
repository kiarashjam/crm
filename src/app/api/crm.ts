import type { SendToCrmParams } from './types';
import { addToCopyHistory } from './copyHistory';

/** Send copy to CRM (demo: records to history and returns success). */
export async function sendCopyToCrm(params: SendToCrmParams): Promise<{ success: boolean; message: string }> {
  await addToCopyHistory({
    type: params.copyTypeLabel,
    copy: params.copy,
    recipientName: params.recordName,
    recipientType: params.objectType,
    recipientId: params.recordId,
  });
  return {
    success: true,
    message: `Copy sent to ${params.recordName} (${params.objectType}).`,
  };
}
