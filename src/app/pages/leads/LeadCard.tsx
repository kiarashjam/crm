import { Mail, Phone, Building2, Pencil, Trash2, ArrowRightCircle, CheckCircle2 } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import type { Lead, Company } from './types';
import { STATUS_BADGE_COLORS } from './config';

interface LeadCardProps {
  lead: Lead;
  company?: Company;
  onView: (lead: Lead) => void;
  onEdit: (lead: Lead) => void;
  onDelete: (lead: Lead) => void;
  onConvert: (lead: Lead) => void;
}

export function LeadCard({ lead, company, onView, onEdit, onDelete, onConvert }: LeadCardProps) {
  const statusStyle = STATUS_BADGE_COLORS[lead.status] || STATUS_BADGE_COLORS.New;
  
  // Get initials for avatar
  const initials = lead.name
    .split(' ')
    .map(n => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  return (
    <div 
      onClick={() => onView(lead)}
      className="group relative bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 p-4 hover:shadow-lg hover:border-orange-200 dark:hover:border-orange-800 transition-all duration-200 cursor-pointer"
    >
      {/* Converted Badge */}
      {lead.isConverted && (
        <div className="absolute -top-2 -right-2 z-10">
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800 shadow-sm">
            <CheckCircle2 className="w-3 h-3" />
            Converted
          </span>
        </div>
      )}

      <div className="flex items-start gap-3">
        {/* Avatar */}
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-orange-400 to-amber-400 flex items-center justify-center text-white font-semibold text-sm shadow-md shrink-0">
          {initials}
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="font-semibold text-slate-800 dark:text-slate-100 truncate">{lead.name}</h3>
            <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-md ${statusStyle?.bg} ${statusStyle?.text} ${statusStyle?.border} border`}>
              <span className={`w-1.5 h-1.5 rounded-full ${statusStyle?.dot}`} />
              {lead.status}
            </span>
          </div>

          <div className="flex items-center gap-3 text-sm text-slate-500 dark:text-slate-400 mb-2">
            {lead.email && (
              <span className="flex items-center gap-1 truncate">
                <Mail className="w-3.5 h-3.5 shrink-0" />
                <span className="truncate">{lead.email}</span>
              </span>
            )}
            {lead.phone && (
              <span className="flex items-center gap-1">
                <Phone className="w-3.5 h-3.5 shrink-0" />
                {lead.phone}
              </span>
            )}
          </div>

          {company && (
            <span className="inline-flex items-center gap-1.5 text-xs text-slate-600 dark:text-slate-300 bg-slate-100 dark:bg-slate-700 px-2 py-1 rounded-md">
              <Building2 className="w-3 h-3" />
              {company.name}
            </span>
          )}

          {/* Lead Score */}
          {lead.leadScore !== undefined && lead.leadScore !== null && (
            <div className="mt-2">
              <div className="flex items-center gap-2">
                <div className="flex-1 h-1.5 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
                  <div 
                    className={`h-full rounded-full transition-all ${
                      lead.leadScore >= 70 ? 'bg-emerald-500' :
                      lead.leadScore >= 40 ? 'bg-amber-500' : 'bg-slate-400'
                    }`}
                    style={{ width: `${Math.min(100, lead.leadScore)}%` }}
                  />
                </div>
                <span className="text-xs font-medium text-slate-600 dark:text-slate-400">{lead.leadScore}</span>
              </div>
            </div>
          )}
        </div>

        {/* Actions - Show on hover */}
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => { e.stopPropagation(); onEdit(lead); }}
            className="h-8 w-8 p-0 text-slate-400 hover:text-slate-600"
          >
            <Pencil className="w-4 h-4" />
          </Button>
          {!lead.isConverted && (
            <Button
              variant="ghost"
              size="sm"
              onClick={(e) => { e.stopPropagation(); onConvert(lead); }}
              className="h-8 w-8 p-0 text-emerald-500 hover:text-emerald-600"
            >
              <ArrowRightCircle className="w-4 h-4" />
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => { e.stopPropagation(); onDelete(lead); }}
            className="h-8 w-8 p-0 text-slate-400 hover:text-red-500"
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

export default LeadCard;
