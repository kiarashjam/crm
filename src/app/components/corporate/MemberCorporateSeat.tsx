// On a member's page: the company whose corporate membership seats them, and whose account their
// charges go on when the company pays them. Shows nothing for a member who holds no seat.

import { Link } from 'react-router-dom';
import { Building2 } from 'lucide-react';
import { useMemberCorporateSeat } from '@/app/hooks/queries';
import { useModules } from '@/app/hooks/useModules';
import { useFormat } from '@/app/i18n';
import { Failed } from './parts';

export function MemberCorporateSeat({ memberId }: { memberId: string }) {
  const clubOn = useModules().club;
  const seat = useMemberCorporateSeat(memberId, clubOn);
  const f = useFormat();
  if (!clubOn || seat.isPending) return null;
  if (seat.isError) {
    return <Failed what="Their corporate seat" error={seat.error} onRetry={() => void seat.refetch()} retrying={seat.isFetching} />;
  }
  const s = seat.data;
  if (!s) return null;
  return (
    <p className="flex flex-wrap items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50/70 px-3 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-100">
      <Building2 className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span>
        Nominee of{' '}
        <Link to={`/companies/${s.companyId}`} className="font-semibold underline-offset-2 hover:underline">{s.companyName}</Link>
        {' '}on {s.name}
        {s.nomineeCharges === 'company' ? ': the company pays their charges' : ': they pay their own charges'}
        {` · renews ${f.date(s.renewsAtUtc)}`}
      </span>
    </p>
  );
}
