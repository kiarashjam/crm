// Compact status pills for the Sales Tracker fields on a Lead card.
// Shows only the badges that are populated so the card stays tidy for
// leads not yet in the pipeline.

import { Calendar, FileCheck2, PenSquare, CircleDollarSign, AlertOctagon, Clock } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { getSalesExtras } from '../salesExtrasStore';
import { declineStage, daysContractOutstanding } from '../../salesTracker/computed';

const YES = new Set(['Yes']);

function Badge({
  children,
  tone,
  icon: Icon,
}: {
  children: React.ReactNode;
  tone: 'blue' | 'amber' | 'emerald' | 'rose' | 'slate' | 'violet';
  icon: React.ElementType;
}) {
  const tones: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-700 border-blue-200',
    amber: 'bg-amber-50 text-amber-700 border-amber-200',
    emerald: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    rose: 'bg-rose-50 text-rose-700 border-rose-200',
    slate: 'bg-slate-50 text-slate-600 border-slate-200',
    violet: 'bg-violet-50 text-violet-700 border-violet-200',
  };
  return (
    <span className={cn(
      'inline-flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider px-2 py-1 rounded-md border',
      tones[tone],
    )}>
      <Icon className="w-3 h-3" />
      {children}
    </span>
  );
}

export function SalesTrackerBadges({ leadId, className }: { leadId: string; className?: string }) {
  const extras = getSalesExtras(leadId);
  const stage = declineStage(extras);
  const outstanding = daysContractOutstanding(extras);

  const badges: React.ReactNode[] = [];

  if (extras.meetingScheduled === 'Yes' && extras.meetingDate) {
    badges.push(
      <Badge key="mtg" tone="blue" icon={Calendar}>
        Mtg {extras.meetingDate}
      </Badge>,
    );
  } else if (extras.meetingScheduled === 'Meeting to be scheduled') {
    badges.push(
      <Badge key="mtg-tbs" tone="slate" icon={Calendar}>Mtg to schedule</Badge>,
    );
  } else if (extras.meetingScheduled === 'Not interested in meeting') {
    badges.push(
      <Badge key="mtg-no" tone="rose" icon={AlertOctagon}>Not interested in mtg</Badge>,
    );
  }

  if (extras.contractSent === 'Yes') {
    badges.push(
      <Badge key="cs" tone="violet" icon={PenSquare}>
        Contract sent{extras.contractSentDate ? ` ${extras.contractSentDate}` : ''}
      </Badge>,
    );
  } else if (extras.contractSent === 'To be sent') {
    badges.push(
      <Badge key="cs-tbs" tone="amber" icon={PenSquare}>Contract to send</Badge>,
    );
  }

  if (YES.has(extras.contractSigned)) {
    badges.push(
      <Badge key="signed" tone="emerald" icon={FileCheck2}>
        Signed{extras.signatureDate ? ` ${extras.signatureDate}` : ''}
      </Badge>,
    );
  } else if (extras.contractSigned === 'Pending') {
    badges.push(
      <Badge key="pending" tone="amber" icon={FileCheck2}>Signature pending</Badge>,
    );
  }

  if (YES.has(extras.depositPaid)) {
    badges.push(
      <Badge key="deposit" tone="emerald" icon={CircleDollarSign}>Deposit paid</Badge>,
    );
  }

  if (outstanding != null && outstanding >= 14) {
    badges.push(
      <Badge key="outstanding" tone={outstanding >= 30 ? 'rose' : 'amber'} icon={Clock}>
        {outstanding}d outstanding
      </Badge>,
    );
  }

  if (stage === 'After Meeting') {
    badges.push(
      <Badge key="stage-m" tone="rose" icon={AlertOctagon}>Declined after mtg</Badge>,
    );
  } else if (stage === 'After Contract') {
    badges.push(
      <Badge key="stage-c" tone="rose" icon={AlertOctagon}>Declined after contract</Badge>,
    );
  }

  if (badges.length === 0) return null;

  return (
    <div className={cn('flex flex-wrap items-center gap-1.5', className)}>
      {badges}
    </div>
  );
}

export default SalesTrackerBadges;
