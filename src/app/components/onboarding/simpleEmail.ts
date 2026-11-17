// The welcome email written in plain words rather than HTML: a heading, the message, the
// button to set a password, a sign-off and a colour. The HTML is made from them, and what
// was written is kept in a comment at its top, so the simple editor opens it again as it
// was. HTML without that comment is someone's own, and opens in the HTML editor.

import type { OnboardingLanguage } from '@/app/api/onboarding';

export interface SimpleEmail {
  heading: string;
  message: string;
  button: string;
  signoff: string;
  color: string;
}

/** What a field is called on its button, for those the CRM or the portal fills. */
export const FIELD_LABELS: Record<string, string> = {
  firstName: 'First name',
  lastName: 'Last name',
  name: 'Full name',
  email: 'Email',
  company: 'Company',
  dealName: 'Deal',
  welcomeMessage: 'Welcome message',
  startDate: 'Start date',
  senderName: 'Sender name',
  workspace: 'Club name',
  setPasswordUrl: 'Set-password link',
  portalUrl: 'Portal link',
  loginEmail: 'Login email',
  referralCode: 'Referral code',
  // The lead newsletter's: each lead's own booking link and code (RDV-XXXX).
  bookingUrl: 'Booking link',
  bookingCode: 'Booking code',
};

export const SIMPLE_COLORS = ['#0f172a', '#ea580c', '#db2777', '#7c3aed', '#1d4ed8', '#0f766e'] as const;

const MARK = '<!-- cadence:simple ';

export function simpleDefaults(language: OnboardingLanguage): SimpleEmail {
  return language === 'fr'
    ? {
        heading: 'Bienvenue, {{firstName}}',
        message: '{{welcomeMessage}}\n\nVotre adhésion commence le **{{startDate}}**.\n\nVotre identifiant : **{{loginEmail}}**',
        button: 'Choisir mon mot de passe',
        signoff: 'À très bientôt,\nL’équipe {{workspace}}',
        color: SIMPLE_COLORS[0],
      }
    : {
        heading: 'Welcome, {{firstName}}',
        message: '{{welcomeMessage}}\n\nYour membership starts on **{{startDate}}**.\n\nYour login: **{{loginEmail}}**',
        button: 'Choose my password',
        signoff: 'See you soon,\nThe {{workspace}} team',
        color: SIMPLE_COLORS[0],
      };
}

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Text as HTML: escaped, **bold** in bold, and a line break where there is one. Fields stay as written. */
const inline = (text: string) =>
  escape(text.trim()).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/\r?\n/g, '<br>');

const safeColor = (color: string) => (/^#[0-9a-f]{6}$/i.test(color) ? color : SIMPLE_COLORS[0]);

/** The email's HTML, made from what was written, with it kept at the top for the simple editor. */
export function buildSimpleHtml(email: SimpleEmail): string {
  const color = safeColor(email.color);
  const paragraphs = email.message
    .split(/\r?\n\s*\r?\n/)
    .map((p) => p.trim())
    .filter(Boolean)
    .map((p) => `      <p style="margin:0 0 16px;font:15px/1.65 Helvetica,Arial,sans-serif;color:#44403c">${inline(p)}</p>`)
    .join('\n');
  const kept = encodeURIComponent(JSON.stringify({ ...email, color }));
  return [
    `${MARK}${kept} -->`,
    '<div style="background:#f6f4f1;padding:32px 12px">',
    '  <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #ece7e1;border-radius:16px;overflow:hidden">',
    `    <div style="height:6px;background:${color}"></div>`,
    '    <div style="padding:36px 32px 28px">',
    `      <p style="margin:0 0 8px;font:600 12px/1.4 Helvetica,Arial,sans-serif;letter-spacing:.12em;text-transform:uppercase;color:${color}">{{workspace}}</p>`,
    `      <h1 style="margin:0 0 20px;font:400 28px/1.25 Georgia,'Times New Roman',serif;color:#1c1917">${inline(email.heading)}</h1>`,
    paragraphs,
    email.button.trim()
      ? `      <p style="margin:28px 0 4px"><a href="{{setPasswordUrl}}" style="display:inline-block;padding:13px 26px;border-radius:999px;background:${color};color:#ffffff;text-decoration:none;font:600 14px Helvetica,Arial,sans-serif">${inline(email.button)}</a></p>`
      : '',
    '    </div>',
    email.signoff.trim()
      ? `    <div style="padding:18px 32px;border-top:1px solid #f0ebe5;font:13px/1.6 Helvetica,Arial,sans-serif;color:#78716c">${inline(email.signoff)}</div>`
      : '',
    '  </div>',
    '</div>',
  ].filter(Boolean).join('\n');
}

/** What the simple editor wrote, when this HTML is its: null for HTML of someone's own. */
export function readSimple(html: string | null | undefined): SimpleEmail | null {
  const text = (html ?? '').trimStart();
  if (!text.startsWith(MARK)) return null;
  const end = text.indexOf(' -->', MARK.length);
  if (end < 0) return null;
  try {
    const parsed = JSON.parse(decodeURIComponent(text.slice(MARK.length, end))) as Partial<SimpleEmail>;
    const email: SimpleEmail = {
      heading: String(parsed.heading ?? ''),
      message: String(parsed.message ?? ''),
      button: String(parsed.button ?? ''),
      signoff: String(parsed.signoff ?? ''),
      color: safeColor(String(parsed.color ?? '')),
    };
    // Edited by hand since: it is HTML of someone's own now.
    return buildSimpleHtml(email) === text.trimEnd() ? email : null;
  } catch {
    return null;
  }
}

/** The HTML without the simple editor's note at its top: to go on by hand from it. */
export function withoutSimpleMark(html: string): string {
  const text = html.trimStart();
  if (!text.startsWith(MARK)) return html;
  const end = text.indexOf(' -->', MARK.length);
  return end < 0 ? html : text.slice(end + 4).replace(/^\n/, '');
}

export type EmailDesign = 'portal' | 'simple' | 'html';

/** Which editor an email opens in: the portal's design when there is no HTML, the simple one for its own. */
export function designOf(html: string | null | undefined): EmailDesign {
  if (!html?.trim()) return 'portal';
  return readSimple(html) ? 'simple' : 'html';
}
