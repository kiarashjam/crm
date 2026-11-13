// The morning brief: the person's day in this workspace, built on the server from the records
// as they may see them, and the brief that went out to them this morning, as it was written.
// The notification a brief sends links here.
//
// It belongs to both modules (tasks are everyone's; the club's parts show as the person's role
// allows), so it sits outside the module routes.

import { Link } from 'react-router-dom';
import { ArrowUpRight, Mail, Sparkles, Sun } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { LoadFailed } from '@/app/components/LoadFailed';
import { PageTransition } from '@/app/components/PageTransition';
import PageHero from '@/app/components/PageHero';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { servesDemoData } from '@/app/api/apiClient';
import type { MorningBriefSection } from '@/app/api/assistant';
import { useMorningBrief } from '@/app/hooks/queries/assistant';
import { useFormat } from '@/app/i18n';

function Section({ section }: { section: MorningBriefSection }) {
  return (
    <section aria-labelledby={`brief-${section.key}`} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900">
      <h2 id={`brief-${section.key}`} className="flex items-baseline justify-between gap-3 font-semibold text-slate-900 dark:text-white">
        <span>{section.title}</span>
        <span className="text-sm font-medium tabular-nums text-slate-500">{section.count}</span>
      </h2>
      <ul className="mt-3 space-y-2 text-sm text-slate-700 dark:text-slate-200">
        {section.lines.map((line, index) => (
          <li key={`${index}-${line.text}`} className="flex items-start justify-between gap-3">
            <span className="min-w-0 break-words">{line.text}</span>
            {line.link && (
              <Link to={line.link} className="inline-flex shrink-0 items-center gap-0.5 text-xs font-semibold text-orange-700 hover:underline dark:text-orange-300">
                Open <ArrowUpRight className="h-3 w-3" aria-hidden="true" />
              </Link>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

export default function MorningBrief() {
  const brief = useMorningBrief();
  const f = useFormat();
  const view = brief.data;

  let body;
  if (servesDemoData()) {
    body = <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">The morning brief is built by the live app from your records; demo data has none.</p>;
  } else if (brief.isError && !view) {
    body = <LoadFailed what="Your morning brief" error={brief.error} onRetry={() => void brief.refetch()} retrying={brief.isFetching} />;
  } else if (!view) {
    body = <p className="text-sm text-slate-500" aria-busy="true">Working out your day…</p>;
  } else {
    body = (
      <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="space-y-4">
          <p className="text-base font-medium text-slate-900 dark:text-white">{view.now.headline}</p>
          {view.now.sections.length === 0 ? (
            <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">Nothing is waiting for you today in {view.now.workspaceName}.</p>
          ) : (
            view.now.sections.map((section) => <Section key={section.key} section={section} />)
          )}
        </div>
        <aside aria-labelledby="brief-sent" className="h-fit rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900">
          <h2 id="brief-sent" className="font-semibold text-slate-900 dark:text-white">This morning&apos;s brief</h2>
          {view.thisMorning ? (
            <>
              <p className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500">
                <span>Sent {f.dateTime(view.thisMorning.sentAtUtc)}</span>
                {view.thisMorning.writtenByModel && (
                  <span className="inline-flex items-center gap-1"><Sparkles className="h-3 w-3" aria-hidden="true" /> summary written by the AI</span>
                )}
                {view.thisMorning.emailed && (
                  <span className="inline-flex items-center gap-1"><Mail className="h-3 w-3" aria-hidden="true" /> also by email</span>
                )}
              </p>
              <p className="mt-3 whitespace-pre-wrap break-words text-sm text-slate-700 dark:text-slate-200">{view.thisMorning.text}</p>
            </>
          ) : (
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
              None went out today yet. A brief is sent from 7:00 on the workspace&apos;s calendar, when there is something to say; you can turn yours off, or have it by email too, under Settings › Notifications.
            </p>
          )}
        </aside>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Sun}
            tone="orange"
            title="Morning brief"
            subtitle={view ? `Your day in ${view.now.workspaceName}, as it stands now.` : 'Your day in this workspace, as it stands now.'}
            noAgentSlot
          />
          {body}
        </main>
      </PageTransition>
    </div>
  );
}
