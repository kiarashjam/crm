// The invitation written in plain words rather than HTML, and made into the club's
// "Founders' Letter": the same page as the welcome letter members receive (the cover photo,
// the Cormorant hand, the coral and lavender on cream, the green footer with the logo), so a
// lead who becomes a member reads one house style from the first email to the last.
//
// What is written here is the letter's words and its images; the HTML is made from them. The
// server fills in each lead's {{fields}} (their name, their booking link and code) and renders
// it; nothing here is filled in.

import type { NewsletterLanguage } from '@/app/api/leadNewsletter';

export interface LetterImages {
  /** The photo across the top. */
  cover: string | null;
  /** Up to two photos side by side, before the booking. */
  photos: string[];
  /** The logo in the green footer. */
  logo: string | null;
}

export interface Letter {
  /** The small coral capitals above the title. */
  kicker: string;
  /** The title, and the words after it in coral italics, on their own line. */
  title: string;
  accent: string;
  /** "Bonjour {{firstName}},": in italics, before the message. */
  heading: string;
  message: string;
  /** A line set between two lavender rules; a second line is in lavender. Empty: none. */
  quote: string;
  button: string;
  /** What the booking box says of the meeting; an empty one is left out. */
  duration: string;
  place: string;
  signoff: string;
  signature: string;
  /** "How it works": choose a day, then a time, we welcome you. */
  steps: boolean;
  images: LetterImages;
}

export const NO_IMAGES: LetterImages = { cover: null, photos: [], logo: null };

export function letterDefaults(language: NewsletterLanguage, images: LetterImages = NO_IMAGES): Letter {
  return language === 'fr'
    ? {
        kicker: 'Rendez-vous privé',
        title: 'Venez découvrir',
        accent: '{{workspace}}.',
        heading: 'Bonjour {{firstName|à vous}},',
        message:
          'Vous nous avez fait part de votre intérêt pour {{workspace}}, et nous aimerions beaucoup vous rencontrer.\n\n'
          + 'Le temps d’une visite et d’un café, nous vous présenterons le club, son esprit et ses lieux, et répondrons à toutes vos questions.',
        quote: 'Les plus beaux moments de la vie\nsont ceux que l’on partage.',
        button: 'Réserver mon rendez-vous',
        duration: '45 minutes',
        place: '{{workspace}}',
        signoff: 'Au plaisir de vous rencontrer,',
        signature: 'L’équipe {{workspace}}',
        steps: true,
        images,
      }
    : {
        kicker: 'A private invitation',
        title: 'Come and discover',
        accent: '{{workspace}}.',
        heading: 'Dear {{firstName|friend}},',
        message:
          'You told us you were interested in {{workspace}}, and we would love to meet you.\n\n'
          + 'Over a visit and a coffee, we will show you around, tell you about the club and its spirit, and answer all your questions.',
        quote: 'The best moments in life\nare the ones we share.',
        button: 'Book my meeting',
        duration: '45 minutes',
        place: '{{workspace}}',
        signoff: 'We look forward to meeting you,',
        signature: 'The {{workspace}} team',
        steps: true,
        images,
      };
}

export function subjectDefault(language: NewsletterLanguage): string {
  return language === 'fr' ? '{{firstName}}, venez découvrir {{workspace}}' : '{{firstName}}, come and discover {{workspace}}';
}

/** The words the letter carries itself, in each language: the steps, the booking box, the footer. */
const WORDS: Record<NewsletterLanguage, {
  howKicker: string; howTitle: string; howAccent: string; steps: [string, string][];
  boxKicker: string; boxTitle: string; boxAccent: string; boxText: string;
  duration: string; place: string; code: string; note: string; footer: string;
}> = {
  fr: {
    howKicker: 'Comment ça se passe',
    howTitle: 'Trois gestes,',
    howAccent: 'une minute.',
    steps: [
      ['Choisissez un jour', 'Un jour de semaine qui vous convient, dans notre calendrier.'],
      ['Puis l’heure', 'Les créneaux encore libres s’affichent aussitôt.'],
      ['Nous vous accueillons', 'Une visite, un café, et toutes vos questions.'],
    ],
    boxKicker: 'Votre rendez-vous',
    boxTitle: 'Réservez',
    boxAccent: 'en une minute.',
    boxText: 'Choisissez le jour et l’heure qui vous conviennent : vous recevrez aussitôt une confirmation par e-mail.',
    duration: 'Durée',
    place: 'Lieu',
    code: 'Votre code',
    note: 'Ce lien vous est personnel : il vous reconnaît dès l’ouverture.',
    footer: 'Vous recevez cette invitation car vous vous êtes intéressé(e) à {{workspace}}.',
  },
  en: {
    howKicker: 'How it works',
    howTitle: 'Three steps,',
    howAccent: 'one minute.',
    steps: [
      ['Choose a day', 'A weekday that suits you, in our calendar.'],
      ['Then a time', 'The times still free appear at once.'],
      ['We welcome you', 'A visit, a coffee, and all your questions.'],
    ],
    boxKicker: 'Your meeting',
    boxTitle: 'Book',
    boxAccent: 'in a minute.',
    boxText: 'Choose the day and time that suit you: a confirmation follows by email at once.',
    duration: 'Duration',
    place: 'Place',
    code: 'Your code',
    note: 'This link is yours alone: it recognises you as soon as you open it.',
    footer: 'You are receiving this invitation because you showed an interest in {{workspace}}.',
  },
};

/** The Founders' Letter palette. */
const C = {
  page: '#E3DED8',
  card: '#F5F0EA',
  green: '#2C5545',
  sage: '#4A6C5E',
  coral: '#E8846A',
  lavender: '#9B8EB9',
  number: '#A99DC0',
  rule: '#DFDCD5',
  muted: '#7A9185',
  footerText: '#C0C3BC',
} as const;

const SERIF = "'Cormorant Garamond',Garamond,Baskerville,'Times New Roman',serif";

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Text as HTML: escaped, **bold** in bold, and a line break where there is one. Fields stay as written. */
const inline = (text: string) =>
  escape(text.trim()).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/\r?\n/g, '<br>');

/** Only a web address can be an image: anything else is left out. */
const imageUrl = (src: string | null | undefined): string | null =>
  src && /^https?:\/\/\S+$/i.test(src.trim()) ? escape(src.trim()) : null;

const font = (size: number, line: number, extra = '') =>
  `font-family:${SERIF};font-size:${size}px;line-height:${line}px;${extra}mso-line-height-rule:exactly;`;

const caps = (color: string, size = 10.5) =>
  font(size, 14, `font-weight:600;letter-spacing:2.73px;text-transform:uppercase;color:${color};`);

const rule = (color: string) =>
  `<tr><td height="1" bgcolor="${color}" style="height:1px;background-color:${color};font-size:1px;line-height:1px;">&#8203;</td></tr>`;

const STYLE = `<style>
  :root { color-scheme: light only; supported-color-schemes: light only; }
  html, body { margin: 0 !important; padding: 0 !important; width: 100% !important; }
  * { -ms-text-size-adjust: 100%; -webkit-text-size-adjust: 100%; }
  table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
  img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
  a { text-decoration: none; }
  @media screen and (max-width: 620px) {
    .pad-page  { padding: 0 !important; }
    .card      { width: 100% !important; }
    .fluid     { width: 100% !important; max-width: 100% !important; height: auto !important; }
    .px-letter { padding-left: 24px !important; padding-right: 24px !important; }
    .px-box    { padding-left: 16px !important; padding-right: 16px !important; }
    .px-in     { padding-left: 22px !important; padding-right: 22px !important; }
    .h1        { font-size: 40px !important; line-height: 43px !important; }
    .h2        { font-size: 30px !important; line-height: 34px !important; }
    .h3        { font-size: 30px !important; line-height: 36px !important; }
    .quote     { font-size: 24px !important; line-height: 30px !important; }
    .col       { width: 31% !important; }
    .col-gap   { width: 3.5% !important; }
    .step-k    { font-size: 9px !important; letter-spacing: 1.6px !important; }
    .step-t    { font-size: 13.5px !important; line-height: 20px !important; }
    .tbl-l     { width: 92px !important; padding-left: 16px !important; letter-spacing: 1.6px !important; }
    .tbl-v     { padding-right: 16px !important; }
    .full      { width: 100% !important; }
    .btn       { padding: 15px 18px !important; letter-spacing: 2px !important; }
  }
</style>`;

/** The site named in the footer: the booking page's, without "www.". */
export function siteOf(bookingPageUrl: string | null | undefined): string | null {
  try {
    return bookingPageUrl ? new URL(bookingPageUrl).host.replace(/^www\./i, '') : null;
  } catch {
    return null;
  }
}

/** The email's HTML, made from what was written. Its {{fields}} are the server's to fill in, lead by lead. */
export function buildLetterHtml(letter: Letter, language: NewsletterLanguage, site: string | null = null): string {
  const w = WORDS[language];
  const cover = imageUrl(letter.images.cover);
  const photos = letter.images.photos.map(imageUrl).filter((p): p is string => Boolean(p)).slice(0, 2);
  const logo = imageUrl(letter.images.logo);
  const paragraphs = letter.message.split(/\r?\n\s*\r?\n/).map((p) => p.trim()).filter(Boolean);
  const [quoteFirst, ...quoteRest] = letter.quote.trim() ? letter.quote.trim().split(/\r?\n/) : [];
  const preheader = escape((paragraphs[0] ?? '').replace(/\*\*/g, '').replace(/\s+/g, ' ')).slice(0, 180);
  const out: string[] = [];
  const add = (...lines: string[]) => out.push(...lines);

  add(
    '<!DOCTYPE html>',
    `<html lang="${language}">`,
    '<head>',
    '<meta charset="utf-8">',
    '<meta name="viewport" content="width=device-width, initial-scale=1">',
    '<meta name="x-apple-disable-message-reformatting">',
    '<meta name="format-detection" content="telephone=no, date=no, address=no, email=no, url=no">',
    '<meta name="color-scheme" content="light only">',
    `<title>${inline(`${letter.title} ${letter.accent}`)}</title>`,
    '<link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:ital,wght@0,400;0,600;1,400&amp;display=swap" rel="stylesheet">',
    STYLE,
    '</head>',
    `<body style="margin:0;padding:0;background-color:${C.page};">`,
    `<div role="article" aria-roledescription="email" lang="${language}" style="background-color:${C.page};">`,
    `<div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;color:${C.page};">${preheader}</div>`,
    `<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="${C.page}" style="background-color:${C.page};">`,
    '<tr><td align="center" class="pad-page" style="padding:60px 16px 96px;">',
    `<table role="presentation" class="card" width="600" cellpadding="0" cellspacing="0" border="0" bgcolor="${C.card}" style="width:600px;max-width:600px;margin:0 auto;background-color:${C.card};box-shadow:0 40px 90px -40px rgba(28,40,32,0.42),0 2px 6px rgba(28,40,32,0.05);">`,
  );

  // The cover photo.
  if (cover) {
    add(`<tr><td style="font-size:0;line-height:0;"><img src="${cover}" width="600" alt="" class="fluid" style="display:block;width:600px;max-width:100%;height:auto;border:0;"></td></tr>`);
  }

  // The letter: kicker, title, greeting, message.
  add(
    `<tr><td class="px-letter" style="padding:${cover ? 26 : 56}px 64px 0 64px;">`,
    '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">',
  );
  if (letter.kicker.trim()) add(`<tr><td align="center" style="${caps(C.coral)}">${inline(letter.kicker)}</td></tr>`);
  if (letter.title.trim() || letter.accent.trim()) {
    add(
      `<tr><td align="center" class="h1" style="padding-top:15px;${font(50, 52.5, `font-weight:400;letter-spacing:0.25px;color:${C.green};`)}">`
      + `${inline(letter.title)}${letter.accent.trim() ? `${letter.title.trim() ? '<br>' : ''}<em style="font-style:italic;color:${C.coral};">${inline(letter.accent)}</em>` : ''}</td></tr>`,
    );
  }
  if (letter.heading.trim()) {
    add(`<tr><td align="left" style="padding-top:43px;${font(22, 30, `font-style:italic;font-weight:400;color:${C.green};`)}">${inline(letter.heading)}</td></tr>`);
  }
  paragraphs.forEach((p, i) => {
    add(`<tr><td align="left" style="padding-top:${i === 0 ? 14 : 16}px;${font(17, 28.9, `font-weight:400;color:${C.green};`)}">${inline(p)}</td></tr>`);
  });
  add('</table>', '</td></tr>');

  // The highlighted line, between two lavender rules.
  if (quoteFirst) {
    add(
      '<tr><td class="px-letter" style="padding:30px 50px 0 50px;">',
      '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">',
      rule(C.lavender),
      `<tr><td align="center" class="quote" style="padding:26px 0 28px;${font(31, 36.5, `font-style:italic;font-weight:400;color:${C.green};`)}">`
      + `${inline(quoteFirst)}${quoteRest.length ? `<br><span style="color:${C.lavender};">${inline(quoteRest.join('\n'))}</span>` : ''}</td></tr>`,
      rule(C.lavender),
      '</table>',
      '</td></tr>',
    );
  }

  // How it works: three steps, side by side on a phone too.
  if (letter.steps) {
    add(
      `<tr><td align="center" style="padding:62px 40px 0 40px;${caps(C.coral)}">${escape(w.howKicker)}</td></tr>`,
      `<tr><td align="center" class="h2" style="padding:13px 40px 0 40px;${font(36, 40, `font-weight:400;color:${C.green};`)}">${escape(w.howTitle)} <em style="font-style:italic;color:${C.coral};">${escape(w.howAccent)}</em></td></tr>`,
      '<tr><td class="px-box" style="padding:29px 40px 0 40px;">',
      '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>',
    );
    w.steps.forEach(([key, text], i) => {
      if (i > 0) add('<td class="col-gap" width="22" style="width:22px;font-size:0;line-height:0;">&nbsp;</td>');
      add(
        '<td class="col" width="159" valign="top" style="width:158.67px;">',
        '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">',
        `<tr><td style="${font(36, 40, `font-style:italic;font-weight:400;color:${C.number};`)}">0${i + 1}</td></tr>`,
        `<tr><td class="step-k" style="padding-top:8px;${font(10.5, 13, `font-weight:600;letter-spacing:2.73px;text-transform:uppercase;color:${C.green};`)}">${escape(key)}</td></tr>`,
        `<tr><td class="step-t" style="padding-top:8px;${font(15, 22.5, `font-weight:400;color:${C.sage};`)}">${escape(text)}</td></tr>`,
        '</table>',
        '</td>',
      );
    });
    add('</tr></table>', '</td></tr>');
  }

  // The photos.
  if (photos.length) {
    add('<tr><td class="px-box" style="padding:40px 40px 0 40px;">', '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>');
    if (photos.length === 1) {
      add(`<td valign="top"><img src="${photos[0]}" width="520" alt="" class="fluid" style="display:block;width:520px;max-width:100%;height:auto;border:0;"></td>`);
    } else {
      add(
        `<td width="297" valign="top" style="width:57%;"><img src="${photos[0]}" width="297" alt="" class="fluid" style="display:block;width:100%;max-width:297px;height:auto;border:0;"></td>`,
        '<td width="10" style="width:2%;font-size:0;line-height:0;">&nbsp;</td>',
        `<td width="213" valign="top" style="width:41%;"><img src="${photos[1]}" width="213" alt="" class="fluid" style="display:block;width:100%;max-width:213px;height:auto;border:0;"></td>`,
      );
    }
    add('</tr></table>', '</td></tr>');
  }

  // The booking box: lavender, with the meeting's details, the lead's code and the button.
  const row = (label: string, value: string, italic = false) =>
    `<tr><td class="tbl-l" width="130" valign="top" style="width:130px;padding:17px 0 19px 22px;${font(9.75, 14, `font-weight:600;letter-spacing:2.6px;text-transform:uppercase;color:${C.coral};`)}">${escape(label)}</td>`
    + `<td class="tbl-v" valign="top" style="padding:15px 22px 15px 0;${font(16, 20, `${italic ? 'font-style:italic;' : ''}font-weight:400;color:${C.green};`)}">${value}</td></tr>`;
  const rows = [
    letter.duration.trim() ? row(w.duration, inline(letter.duration)) : null,
    letter.place.trim() ? row(w.place, inline(letter.place)) : null,
    row(w.code, `<span style="letter-spacing:3px;">{{bookingCode}}</span>`),
  ].filter((r): r is string => Boolean(r));
  add(
    `<tr><td class="px-box" style="padding:44px 40px 0 40px;">`,
    `<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="${C.lavender}" style="background-color:${C.lavender};">`,
    '<tr><td class="px-in" style="padding:38px 40px 38px 40px;">',
    '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">',
    `<tr><td align="left" style="${caps(C.card)}">${escape(w.boxKicker)}</td></tr>`,
    `<tr><td align="left" class="h3" style="padding-top:9px;${font(34, 36, `font-weight:400;color:${C.card};`)}">${escape(w.boxTitle)} <em style="font-style:italic;">${escape(w.boxAccent)}</em></td></tr>`,
    `<tr><td align="left" style="padding-top:14px;${font(16, 27.2, `font-weight:400;color:${C.card};`)}">${escape(w.boxText)}</td></tr>`,
    '<tr><td style="padding-top:22px;">',
    `<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="${C.card}" style="background-color:${C.card};">`,
    rows.join(`\n<tr><td colspan="2" height="1" bgcolor="${C.rule}" style="height:1px;background-color:${C.rule};font-size:1px;line-height:1px;">&#8203;</td></tr>\n`),
    '</table>',
    '</td></tr>',
  );
  if (letter.button.trim()) {
    add(
      '<tr><td align="left" style="padding-top:24px;">',
      '<table role="presentation" class="full" cellpadding="0" cellspacing="0" border="0"><tr>',
      `<td align="center" valign="middle" bgcolor="${C.green}" style="background-color:${C.green};">`,
      `<a href="{{bookingUrl}}" target="_blank" class="btn" style="display:block;padding:15px 34px;${font(12, 20, `font-weight:600;letter-spacing:3.35px;text-transform:uppercase;color:${C.card};text-decoration:none;text-align:center;`)}">${inline(letter.button)}</a>`,
      '</td>',
      '</tr></table>',
      '</td></tr>',
    );
  }
  add(
    `<tr><td align="left" style="padding-top:12px;${font(13, 20, 'font-style:italic;font-weight:400;color:#EAE8DF;')}">${escape(w.note)}</td></tr>`,
    '</table>',
    '</td></tr>',
    '</table>',
    '</td></tr>',
  );

  // The sign-off.
  if (letter.signoff.trim() || letter.signature.trim()) {
    add('<tr><td align="center" style="padding:48px 40px 51px 40px;">', '<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">');
    if (letter.signoff.trim()) add(`<tr><td align="center" style="${font(21, 30, `font-style:italic;font-weight:400;color:${C.green};`)}">${inline(letter.signoff)}</td></tr>`);
    if (letter.signature.trim()) add(`<tr><td align="center" style="padding-top:13px;${font(40, 44, `font-style:italic;font-weight:400;color:${C.sage};`)}">${inline(letter.signature)}</td></tr>`);
    add('</table>', '</td></tr>');
  } else {
    add('<tr><td style="height:48px;font-size:0;line-height:0;">&nbsp;</td></tr>');
  }

  // The footer: green, with the logo, the site and why they receive it.
  add(`<tr><td align="center" bgcolor="${C.green}" style="background-color:${C.green};padding:42px 30px 39px 30px;">`);
  if (logo) add(`<img src="${logo}" width="224" alt="{{workspace}}" style="display:block;width:224px;max-width:80%;height:auto;border:0;margin:0 auto;">`);
  else add(`<div style="${caps(C.card, 12)}letter-spacing:4px;">{{workspace}}</div>`);
  const siteLink = site ? ` &middot; <a href="https://${escape(site)}" target="_blank" style="color:${C.footerText};text-decoration:none;">${escape(site)}</a>` : '';
  add(
    `<div style="padding-top:20px;${font(12.5, 21.25, `font-weight:400;color:${C.footerText};`)}">{{workspace}}${siteLink}<br>${escape(w.footer)}</div>`,
    '</td></tr>',
    '</table>',
    '</td></tr>',
    '</table>',
    '</div>',
    '</body>',
    '</html>',
  );
  return out.join('\n');
}

/**
 * The images of the workspace's welcome letter, to dress the invitation in the same way: the
 * first is the cover, the last (a PNG, or a small one) the footer's logo, and up to two in
 * between the photos. A letter without images gives none.
 */
export function imagesFromHtml(html: string | null | undefined): LetterImages {
  const sources = [...(html ?? '').matchAll(/<img\b[^>]*?\bsrc\s*=\s*["']([^"']+)["'][^>]*>/gi)]
    .map((m) => ({ src: (m[1] ?? '').trim(), tag: m[0] }))
    .filter((i) => /^https?:\/\//i.test(i.src));
  const [cover, ...rest] = sources;
  if (!cover) return NO_IMAGES;
  const last = rest.length ? rest[rest.length - 1] : undefined;
  const height = last ? Number(/\bheight\s*=\s*["']?(\d+)/i.exec(last.tag)?.[1] ?? NaN) : NaN;
  const logo = last && (/\.png(\?|$)/i.test(last.src) || height <= 90) ? last.src : null;
  return {
    cover: cover.src,
    photos: (logo ? rest.slice(0, -1) : rest).slice(0, 2).map((i) => i.src),
    logo,
  };
}

const WORDING: (keyof Letter)[] = ['kicker', 'title', 'accent', 'heading', 'message', 'quote', 'button', 'duration', 'place', 'signoff', 'signature'];

/** Whether the letter's words are still as they came for that language, so a change of language may rewrite them. */
export function isLetterDefault(letter: Letter, language: NewsletterLanguage): boolean {
  const d = letterDefaults(language);
  return WORDING.every((k) => letter[k] === d[k]);
}

/** The same letter in the other language's words, keeping its images and its steps. */
export function inLanguage(letter: Letter, language: NewsletterLanguage): Letter {
  return { ...letterDefaults(language, letter.images), steps: letter.steps };
}
