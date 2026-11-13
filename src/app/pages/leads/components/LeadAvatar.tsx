// A lead's picture: a person's initials, or for an establishment the sign of what it is
// (a fork and knife for a restaurant, a cup for a café…), so the two read apart at a glance.

import { Store } from 'lucide-react';
import { InitialsAvatar } from '@/app/components/views';
import { cn } from '@/app/components/ui/utils';
import { establishmentTypeOption, isEstablishment } from '@/app/lib/leadKinds';
import type { Lead } from '@/app/api/types';

export function LeadAvatar({
  lead,
  size = 'md',
  className,
}: {
  lead: Pick<Lead, 'name' | 'kind' | 'establishmentType'>;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}) {
  if (!isEstablishment(lead)) {
    return <InitialsAvatar name={lead.name} size={size === 'sm' ? 'sm' : 'md'} className={cn(size === 'lg' && 'h-12 w-12 text-base', className)} />;
  }
  const Icon = establishmentTypeOption(lead.establishmentType)?.icon ?? Store;
  return (
    <span
      aria-hidden
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-500 text-white shadow-sm transition-transform duration-300 group-hover:scale-105 group-hover:-rotate-3',
        size === 'sm' ? 'h-7 w-7' : size === 'lg' ? 'h-12 w-12' : 'h-10 w-10',
        className,
      )}
    >
      <Icon className={size === 'sm' ? 'h-3.5 w-3.5' : size === 'lg' ? 'h-6 w-6' : 'h-5 w-5'} />
    </span>
  );
}
