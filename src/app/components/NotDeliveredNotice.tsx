import type { ReactNode } from 'react';
import { MailX } from 'lucide-react';

/**
 * Said on the screens that record messages to members, until Cadence can send them.
 *
 * "Send message" and a campaign's "Send now" only ever wrote a log entry marked
 * "Sent"; nothing was emailed, texted or pushed, and a scheduled campaign never ran.
 * Staff then told members "we emailed you" on the strength of that log.
 */
export default function NotDeliveredNotice({ children }: { children?: ReactNode }) {
  return (
    <div
      role="note"
      className="mb-6 flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900"
    >
      <MailX className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" aria-hidden />
      <div>
        <p className="font-medium">Cadence does not deliver email, SMS or push messages yet.</p>
        <p className="mt-0.5">
          {children ?? 'What you log here is a record for your team. Nobody receives it: send it from your own mail or phone.'}
        </p>
      </div>
    </div>
  );
}
