// What a person fills in to welcome someone, before it becomes a request: the message,
// when they start (or that it is still to be decided), the language, and optionally an
// email of this deal's own instead of the workspace's template.

import type { OnboardingLanguage, OnboardingPreviewRequest, OnboardingSettings, StartOnboardingRequest } from '@/app/api/onboarding';
import { currentDateLocale } from '@/app/i18n/current';

export interface WelcomeDraft {
  /** Hand them to the portal with the conversion. */
  send: boolean;
  welcomeMessage: string;
  /** yyyy-mm-dd, or empty. */
  startsOn: string;
  /** The start is still to be decided. */
  undetermined: boolean;
  language: OnboardingLanguage;
  addToClubMembers: boolean;
  /** Use an email of this handoff's own rather than the workspace's template. */
  customEmail: boolean;
  emailSubject: string;
  emailHtml: string;
}

export function draftFrom(settings: OnboardingSettings | null): WelcomeDraft {
  return {
    send: Boolean(settings?.configured && settings.enabled),
    welcomeMessage: '',
    startsOn: '',
    undetermined: true,
    language: settings?.defaultLanguage ?? 'fr',
    addToClubMembers: settings?.addToClubMembers ?? true,
    customEmail: false,
    emailSubject: '',
    emailHtml: '',
  };
}

/** A date, or still to be decided: never neither. */
function start(draft: WelcomeDraft): { startsOnUtc: string | null; startUndetermined: boolean } {
  const date = draft.undetermined ? '' : draft.startsOn.trim();
  return { startsOnUtc: date || null, startUndetermined: !date };
}

function email(draft: WelcomeDraft): { emailSubject?: string; emailHtml?: string } {
  if (!draft.customEmail) return {};
  return {
    emailSubject: draft.emailSubject.trim() || undefined,
    emailHtml: draft.emailHtml.trim() || undefined,
  };
}

export function toStartRequest(draft: WelcomeDraft, leadId?: string): StartOnboardingRequest {
  return {
    leadId,
    welcomeMessage: draft.welcomeMessage.trim() || undefined,
    ...start(draft),
    language: draft.language,
    addToClubMembers: draft.addToClubMembers,
    ...email(draft),
  };
}

export function toPreviewRequest(
  draft: WelcomeDraft,
  person: { dealId?: string; name?: string; email?: string; company?: string; dealName?: string },
): OnboardingPreviewRequest {
  return {
    ...person,
    welcomeMessage: draft.welcomeMessage.trim() || undefined,
    ...start(draft),
    language: draft.language,
    ...email(draft),
  };
}

/** The start as the team reads it. */
export function startLabel(startsOnUtc?: string | null, undetermined?: boolean): string {
  if (!startsOnUtc || undetermined) return 'To be decided';
  const d = new Date(startsOnUtc.length === 10 ? `${startsOnUtc}T00:00:00` : startsOnUtc);
  return Number.isNaN(d.getTime()) ? 'To be decided' : d.toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'long', year: 'numeric' });
}
