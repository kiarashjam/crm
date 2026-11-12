import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Sparkles } from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import DemoBanner from '@/app/components/DemoBanner';
import { BrandName } from '@/app/components/BrandName';
import { getPrivacyNotice, type PrivacyNotice, type PrivacyParty } from '@/app/api/privacy';

/**
 * The privacy notice, with its facts from the server.
 *
 * It used to name a placeholder company in San Francisco under California law, list no
 * processor although the AI writer sends records to OpenAI in the United States, promise a
 * session token "never exposed" while it sits in the browser's storage, and say nothing of
 * how long anything is kept. Now who answers for the data, which services it goes to, where
 * it is kept and for how long all come from the server: the operator's settings, and when the
 * page is opened from a club's form (?form=…), that club's own privacy settings.
 */
export default function Privacy() {
  const [params] = useSearchParams();
  const form = params.get('form');
  // Public data with no workspace: keyed by the form it was opened from, not by a workspace.
  const notice = useQuery({
    queryKey: ['public', 'privacy-notice', form ?? ''],
    queryFn: ({ signal }) => getPrivacyNotice(form, { signal }),
    staleTime: 5 * 60_000,
  });

  return (
    <div className="min-h-screen bg-gradient-calm">
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40 shadow-sm" role="banner">
        <div className="w-full px-[var(--page-padding)] py-4 flex justify-between items-center">
          <Link to="/" className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg min-h-[44px] min-w-[44px] items-center justify-center" aria-label="Back to home">
            <ArrowLeft className="w-5 h-5" aria-hidden />
            Back to Home
          </Link>
          <Link to="/" className="flex items-center gap-2" aria-label="CadenceE home">
            <div className="w-10 h-10 bg-orange-600 rounded-lg flex items-center justify-center" aria-hidden>
              <Sparkles className="w-6 h-6 text-white" />
            </div>
            <BrandName className="text-xl font-bold text-slate-900" />
          </Link>
        </div>
      </header>
      <DemoBanner />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 md:p-12">
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Privacy notice</h1>
          <p className="text-slate-600 mb-8">
            Under the Swiss Federal Act on Data Protection (nFADP) and, for people in the EU and EEA, the General Data
            Protection Regulation (GDPR).
          </p>

          {notice.isPending ? (
            <p className="text-slate-600" role="status">Loading the details…</p>
          ) : notice.isError ? (
            <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">
              <p>The details of this notice could not be loaded. Please check your connection.</p>
              <button type="button" onClick={() => notice.refetch()} className="mt-3 rounded-lg bg-red-700 px-3 py-1.5 text-white">
                Try again
              </button>
            </div>
          ) : (
            <NoticeBody notice={notice.data} />
          )}
        </div>
      </main>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="mb-8">
      <h2 className="text-xl font-bold text-slate-900 mb-3">{title}</h2>
      <div className="space-y-3 text-slate-700">{children}</div>
    </section>
  );
}

function Party({ party, missing }: { party: PrivacyParty; missing: string }) {
  if (!party.name) return <p className="italic text-slate-600">{missing}</p>;
  return (
    <address className="not-italic">
      <strong>{party.name}</strong>
      {party.address && <span className="block whitespace-pre-line">{party.address}</span>}
      {party.email && (
        <span className="block">
          Email: <a href={`mailto:${party.email}`} className="underline">{party.email}</a>
        </span>
      )}
      {party.dpoContact && <span className="block">Data protection contact: {party.dpoContact}</span>}
    </address>
  );
}

/** "after 6 months", or, with no period set, that nothing removes it on a schedule. */
function after(value: number | null | undefined, unit: 'month' | 'year'): string {
  return value ? `after ${value} ${unit}${value === 1 ? '' : 's'}` : 'no period is set, so it is kept until the club deletes it';
}

function NoticeBody({ notice }: { notice: PrivacyNotice | null }) {
  if (!notice) {
    return (
      <p className="text-slate-700">
        This is the demo. The live app fills this notice in from the club&apos;s own settings: who is responsible for your
        data, the services it goes to, and how long it is kept.
      </p>
    );
  }
  // The server names the workspace only when the page was opened from one of its forms.
  const fromForm = Boolean(notice.workspaceName);
  const club = notice.workspaceName ?? 'the club';
  const controllerEmail = notice.controller.email ?? notice.operator.email;
  return (
    <div>
      <Section title="Who is responsible for your data">
        {fromForm ? (
          <>
            <p>The data you give on {club}&apos;s forms, and what {club} records about you, is controlled by:</p>
            <Party party={notice.controller} missing={`${club} has not entered its details here yet. Ask the club directly.`} />
            <p>The app is run for {club} by:</p>
            <Party party={notice.operator} missing="The operator of this app has not entered its details yet." />
          </>
        ) : (
          <>
            <p>This app is run by:</p>
            <Party party={notice.operator} missing="The operator of this app has not entered its details yet." />
            <p>
              Each club or company that uses it is responsible for the data it records about its own members, leads and
              contacts, and its forms link to its own version of this notice.
            </p>
          </>
        )}
      </Section>

      <Section title="What is kept, and why">
        <ul className="list-disc pl-5 space-y-1">
          <li>
            <strong>What you tell us</strong>: your name, email, phone, company and what you ask about, to answer you
            (your request; pre-contractual measures).
          </li>
          <li>
            <strong>Membership and its money</strong>: your membership, visits, bookings, event places, charges,
            payments and statements, to run your membership (the contract) and keep the books (legal obligation).
          </li>
          <li>
            <strong>Email to you</strong>: what was sent, when, and whether it arrived or was opened. Some sales emails
            contain a small image that reports when they are opened.
          </li>
          <li>
            <strong>News and offers</strong>: only with your consent, which is recorded with where and when you gave
            it. You can withdraw it at any time with the link in every such email.
          </li>
          <li>
            <strong>Staff accounts</strong>: the name, email and sign-in details of the people who use the app.
          </li>
        </ul>
      </Section>

      <Section title="Who it goes to">
        <p>The data is processed for the club by these services, and no one else; it is never sold.</p>
        <ul className="space-y-2">
          {notice.processors.map((p) => (
            <li key={p.name} className="rounded-lg border border-slate-200 p-3">
              <strong>{p.name}</strong>: {p.purpose}
              <span className="block text-sm text-slate-600">
                Where: {p.location}.{p.transfer ? ` Covered by: ${p.transfer}.` : ''}
              </span>
            </li>
          ))}
        </ul>
        <p>
          Where a service is outside Switzerland and the EEA, the transfer is covered as stated beside it, as the nFADP
          (art. 16) and the GDPR (chapter V) require.
        </p>
      </Section>

      <Section title="Where it is kept">
        <p>Everything is kept with {notice.hosting}.</p>
        <p>
          When staff sign in, the app keeps a sign-in token in the browser&apos;s local storage, valid for{' '}
          {notice.sessionHours} hours, with the signed-in person&apos;s name and email, the workspace chosen in each tab,
          display preferences and a few working notes. The token is a bearer token: anyone who can use that browser while
          it is valid can act as that person, so staff should sign out on shared computers. Signing out removes it, and
          changing the password ends every session. The app sets no advertising or tracking cookies.
        </p>
      </Section>

      <Section title="How long it is kept">
        {notice.retention ? (
          <ul className="list-disc pl-5 space-y-1">
            <li>
              Enquiries that never became a customer are deleted, with everything on them, once nothing has been done on
              them for a while: {after(notice.retention.leadMonths, 'month')}.
            </li>
            <li>
              Members who resigned, lapsed or were cancelled are anonymised once nothing has happened on their account for
              a while: {after(notice.retention.memberYears, 'year')}. Their charges and payments stay in the books under a
              pseudonym.
            </li>
            <li>
              The text of sent emails is removed (who they went to, when, and whether they arrived stay):{' '}
              {after(notice.retention.mailMonths, 'month')}.
            </li>
            <li>
              Names in the audit trail, and details around requests (webhook payloads, till tickets, the subjects of
              replies, notifications), are removed: {after(notice.retention.detailMonths, 'month')}.
            </li>
          </ul>
        ) : (
          <p>Each club sets its own periods; its forms link to its own version of this notice.</p>
        )}
        <p>
          Accounting records (charges, payments, VAT) are kept for ten years, as Swiss law requires (Code of Obligations,
          art. 958f), even after the rest of your data has been erased; your name on them is replaced by a pseudonym.
        </p>
      </Section>

      <Section title="Your rights">
        <ul className="list-disc pl-5 space-y-1">
          <li>To know what is held about you and get a copy of it, in a portable form (one file with everything).</li>
          <li>To have it corrected.</li>
          <li>To have it erased: deleted where it can go, anonymised where the books must keep it.</li>
          <li>To object to its use, or restrict it, and to withdraw consent to news and offers at any time.</li>
        </ul>
        <p>
          To use any of these rights, write to{' '}
          {controllerEmail ? <a href={`mailto:${controllerEmail}`} className="underline">{controllerEmail}</a> : 'the club'}.
          The answer is due within 30 days (one month under the GDPR). Once someone is erased, their email address is kept only as a one-way
          fingerprint on the club&apos;s suppression list, so that it is not emailed or imported again by mistake.
        </p>
        <p>
          You can also complain to the Swiss Federal Data Protection and Information Commissioner (FDPIC), or, in the EU and
          EEA, to the data protection authority where you live.
        </p>
      </Section>

      {notice.marketingConsentText && (
        <Section title="The consent asked on the club's forms">
          <p>&ldquo;{notice.marketingConsentText}&rdquo;</p>
        </Section>
      )}
    </div>
  );
}
