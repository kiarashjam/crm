import { Eye } from 'lucide-react';
import { useOrgOptional } from '@/app/contexts/OrgContext';

/**
 * Full-width banner shown on every page when the user's role in the current
 * organization is Viewer (read-only). Invisible for every other role.
 *
 * The backend refuses all writes for this role, so telling the user up front is
 * kinder than letting them discover it by clicking something that fails.
 */
export default function ReadOnlyBanner() {
  const org = useOrgOptional();
  if (!org?.isReadOnly) return null;

  return (
    <div
      className="w-full bg-violet-50 border-b border-violet-200/80 py-2 px-[var(--page-padding)]"
      role="status"
      aria-live="polite"
    >
      <div className="flex items-center justify-center gap-3 text-sm font-medium text-violet-800">
        <Eye className="h-4 w-4 shrink-0 text-violet-600" aria-hidden />
        <span className="max-w-3xl text-center">
          View-only access to <strong>{org.currentOrg?.name ?? 'this organization'}</strong> — you can see everything
          here, but not make changes.
        </span>
      </div>
    </div>
  );
}
