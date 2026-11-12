import type { ReactNode } from 'react';
import { LoadFailed } from '@/app/components/LoadFailed';
import type { ClubReads } from '@/app/hooks/queries/club';

/**
 * A club page's content, or the failure of the reads it needs, with a way to try again.
 * A refresh that failed while an earlier copy is on screen says so above it.
 */
export function ClubReadState({ reads, what, children }: { reads: ClubReads; what: string; children: ReactNode }) {
  const retry = () => { void reads.reload(); };
  if (reads.failed) return <LoadFailed what={what} error={reads.error} onRetry={retry} retrying={reads.retrying} />;
  return (
    <>
      {reads.stale && <LoadFailed stale what={what} error={reads.error} onRetry={retry} retrying={reads.retrying} className="mb-4" />}
      {children}
    </>
  );
}
