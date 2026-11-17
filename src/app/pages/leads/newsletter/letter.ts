// The invitation written in plain words rather than HTML: a greeting, the message, the
// button to book, a sign-off and a colour. The HTML is made from them in the manner of the
// club's Founders' Letter (a cream page, a serif hand, one quiet colour), with the lead's
// booking button and code (RDV-XXXX) in it. The server fills in each lead's {{fields}} and
// renders it; nothing here is filled in.

import type { NewsletterLanguage } from '@/app/api/leadNewsletter';

export interface Letter {
  heading: string;
  message: string;
  button: string;
  signoff: string;
  color: string;
}

/** The letter's colours: Pavillon green first, then a few that sit on cream as well. */
export const LETTER_COLORS = ['#2c5545', '#1f2937', '#7c2d12', '#1e3a8a', '#6d28d9', '#9d174d'] as const;

export function letterDefaults(language: NewsletterLanguage): Letter {
  return language === 'fr'
    ? {
        heading: 'Bonjour {{firstName|à vous}},',
        message:
          'Nous serions ravis de vous accueillir au {{workspace}}, le temps d’une visite et d’un café, pour vous présenter le club et répondre à vos questions.\n\n'
          + 'Choisissez simplement un **jour de semaine**, puis **l’heure** qui vous convient : cela prend moins d’une minute.',
        button: 'Réserver mon rendez-vous',
        signoff: 'À très bientôt,\nL’équipe {{workspace}}',
        color: LETTER_COLORS[0],
      }
    : {
        heading: 'Dear {{firstName|friend}},',
        message:
          'We would love to welcome you at {{workspace}} for a visit and a coffee, to show you around and answer your questions.\n\n'
          + 'Just pick a **weekday**, then **a time** that suits you: it takes less than a minute.',
        button: 'Book my meeting',
        signoff: 'See you soon,\nThe {{workspace}} team',
        color: LETTER_COLORS[0],
      };
}

export function subjectDefault(language: NewsletterLanguage): string {
  return language === 'fr' ? '{{firstName}}, prenons rendez-vous' : '{{firstName}}, shall we meet?';
}

/** The words around the code, in the letter's language. */
const CODE_LABEL: Record<NewsletterLanguage, string> = { fr: 'Votre code', en: 'Your code' };

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Text as HTML: escaped, **bold** in bold, and a line break where there is one. Fields stay as written. */
const inline = (text: string) =>
  escape(text.trim()).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/\r?\n/g, '<br>');

export const safeColor = (color: string) => (/^#[0-9a-f]{6}$/i.test(color) ? color.toLowerCase() : LETTER_COLORS[0]);

const SERIF = "'Cormorant Garamond',Georgia,'Times New Roman',serif";
const SANS = 'Helvetica,Arial,sans-serif';

/** The email's HTML, made from what was written. Its {{fields}} are the server's to fill in, lead by lead. */
export function buildLetterHtml(letter: Letter, language: NewsletterLanguage): string {
  const color = safeColor(letter.color);
  const paragraphs = letter.message
    .split(/\r?\n\s*\r?\n/)
    .map((p) => p.trim())
    .filter(Boolean)
    .map((p) => `        <p style="margin:0 0 18px;font:16px/1.7 ${SANS};color:#3f3a35">${inline(p)}</p>`)
    .join('\n');
  return [
    '<!DOCTYPE html>',
    `<html lang="${language}">`,
    '<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title></title></head>',
    '<body style="margin:0;padding:0;background:#f5f0ea">',
    '  <div style="background:#f5f0ea;padding:40px 14px">',
    '    <div style="max-width:560px;margin:0 auto;background:#fffdf9;border:1px solid #e8dfd3;border-radius:6px">',
    '      <div style="padding:40px 36px 0;text-align:center">',
    `        <p style="margin:0;font:600 11px/1.4 ${SANS};letter-spacing:.24em;text-transform:uppercase;color:${color}">{{workspace}}</p>`,
    `        <div style="width:44px;height:1px;margin:18px auto 0;background:${color}"></div>`,
    '      </div>',
    '      <div style="padding:30px 36px 6px">',
    `        <h1 style="margin:0 0 22px;font:400 30px/1.25 ${SERIF};color:#1f2a24">${inline(letter.heading)}</h1>`,
    paragraphs,
    '      </div>',
    letter.button.trim()
      ? [
          '      <div style="padding:10px 36px 0;text-align:center">',
          `        <a href="{{bookingUrl}}" style="display:inline-block;padding:15px 30px;border-radius:999px;background:${color};color:#fffdf9;text-decoration:none;font:600 15px ${SANS};letter-spacing:.02em">${inline(letter.button)}</a>`,
          '      </div>',
        ].join('\n')
      : '',
    '      <div style="padding:22px 36px 0;text-align:center">',
    `        <div style="display:inline-block;padding:12px 24px;border:1px dashed ${color};border-radius:12px">`,
    `          <p style="margin:0 0 4px;font:600 10px/1.4 ${SANS};letter-spacing:.2em;text-transform:uppercase;color:#8a8178">${CODE_LABEL[language]}</p>`,
    `          <p style="margin:0;font:700 20px/1.2 'Courier New',Courier,monospace;letter-spacing:.14em;color:#1f2a24">{{bookingCode}}</p>`,
    '        </div>',
    '      </div>',
    letter.signoff.trim()
      ? `      <div style="padding:30px 36px 38px;font:italic 18px/1.6 ${SERIF};color:#4a443e">${inline(letter.signoff)}</div>`
      : '      <div style="height:34px"></div>',
    '    </div>',
    '  </div>',
    '</body>',
    '</html>',
  ].filter(Boolean).join('\n');
}

/** Whether the letter is still as it came for that language, so a change of language may rewrite it. */
export function isLetterDefault(letter: Letter, language: NewsletterLanguage): boolean {
  const d = letterDefaults(language);
  return letter.heading === d.heading && letter.message === d.message && letter.button === d.button && letter.signoff === d.signoff;
}
