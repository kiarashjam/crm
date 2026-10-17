import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRightCircle, Eye, Mail, Pencil, Trash2, UserPlus } from 'lucide-react';
import { Button } from '@/app/components/ui/button';

interface LeadDetailToolbarProps {
  name: string;
  backUrl: string;
  readOnly: boolean;
  hasEmail: boolean;
  converted: boolean;
  onBack: () => void;
  onEmail: () => void;
  onEdit: () => void;
  onSaveContact: () => void;
  onConvert: () => void;
  onDelete: () => void;
}

const quiet =
  'h-11 rounded-xl border-slate-200 bg-white px-3 text-slate-700 shadow-none hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800';

export function LeadDetailToolbar({
  name,
  backUrl,
  readOnly,
  hasEmail,
  converted,
  onBack,
  onEmail,
  onEdit,
  onSaveContact,
  onConvert,
  onDelete,
}: LeadDetailToolbarProps) {
  return (
    <div className="sticky top-0 z-30 border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-950">
      <div className="flex w-full flex-col gap-2 px-[var(--page-padding)] py-2.5 sm:flex-row sm:items-center sm:justify-between">
        <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={onBack}
            className="h-11 shrink-0 gap-2 rounded-xl border-slate-200 bg-white px-3 text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
            aria-label="Back to leads"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Back</span>
          </Button>
          <ol className="flex min-w-0 items-center gap-2 text-sm">
            <li className="hidden sm:list-item">
              <Link to={backUrl} className="text-slate-500 transition-colors hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-100">
                Leads
              </Link>
            </li>
            <li aria-hidden className="hidden text-slate-300 sm:list-item dark:text-slate-600">/</li>
            <li className="min-w-0 truncate font-medium text-slate-900 dark:text-slate-100" aria-current="page">
              {name}
            </li>
          </ol>
        </nav>

        {readOnly ? (
          <span className="inline-flex h-11 items-center gap-2 self-start rounded-xl border border-violet-200 bg-violet-50 px-3 text-sm font-medium text-violet-800 sm:self-auto">
            <Eye className="h-4 w-4" />
            View only
          </span>
        ) : (
          <div className="flex flex-wrap items-center gap-2" role="toolbar" aria-label="Lead actions">
            {hasEmail && (
              <Button type="button" variant="outline" onClick={onEmail} className={quiet} aria-label="Email">
                <Mail className="h-4 w-4" />
                <span className="hidden md:inline">Email</span>
              </Button>
            )}
            <Button type="button" variant="outline" onClick={onEdit} className={quiet} aria-label="Edit">
              <Pencil className="h-4 w-4" />
              <span className="hidden md:inline">Edit</span>
            </Button>
            <Button type="button" variant="outline" onClick={onSaveContact} className={quiet} aria-label="Save as contact">
              <UserPlus className="h-4 w-4" />
              <span className="hidden lg:inline">Save as contact</span>
            </Button>
            {!converted && (
              <Button
                type="button"
                onClick={onConvert}
                className="h-11 gap-2 rounded-xl bg-[var(--brand-primary)] px-4 text-white shadow-sm hover:bg-[var(--brand-primary-dark)]"
                aria-label="Convert"
              >
                <ArrowRightCircle className="h-4 w-4" />
                <span>Convert</span>
              </Button>
            )}
            <span aria-hidden className="mx-0.5 hidden h-6 w-px bg-slate-200 sm:block dark:bg-slate-700" />
            <Button
              type="button"
              variant="outline"
              onClick={onDelete}
              className="h-11 gap-2 rounded-xl border-red-200 bg-white px-3 text-red-700 hover:border-red-300 hover:bg-red-50 dark:border-red-900 dark:bg-slate-900 dark:text-red-300 dark:hover:bg-red-950"
              aria-label="Delete"
            >
              <Trash2 className="h-4 w-4" />
              <span className="hidden md:inline">Delete</span>
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
