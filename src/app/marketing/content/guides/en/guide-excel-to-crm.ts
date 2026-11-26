import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Getting organised',
  h1: 'Excel vs CRM: when to replace your spreadsheet, and how to move',
  lead: 'An honest comparison of Excel and a CRM, the signs that a spreadsheet has stopped working for your club or sales team, and a two-week plan to clean, import and switch without losing data or your team’s goodwill.',
  readingMinutes: 9,
  keyTakeaways: [
    'A spreadsheet is fine for one person and a few dozen contacts; it struggles once several people share it and follow-ups matter.',
    'The warning signs are duplicates, competing versions, no history of who changed what, and follow-ups that live in someone’s head.',
    'Clean before you import: one row per person, one column per fact, consistent formats and consent recorded explicitly.',
    'Import with a dry run first and make sure you can undo; move open leads and current members first, archives later.',
    'Plan two weeks, name one owner, and check that you can export everything back out before you commit.',
  ],
  sections: [
    {
      id: 'signs-you-have-outgrown-excel',
      heading: 'Signs you have outgrown Excel',
      paragraphs: [
        'Almost every club and sales team starts with a spreadsheet, and sensibly so: Excel is on every computer and everyone knows it. The trouble begins when the file becomes where the business lives rather than a list someone keeps.',
      ],
      bullets: [
        'Duplicates: the same person appears three times, spelled three ways, with three different phone numbers',
        'Versions: “Members_FINAL_v3_Anna.xlsx” and “Members_FINAL_new.xlsx” both exist, and nobody is sure which is right',
        'No history: you cannot tell who changed a phone number, when the last call was, or what was promised',
        'No reminders: follow-ups depend on someone remembering, so a warm lead goes cold over a busy fortnight',
        'Shared-file risk: one careless sort on a single column scrambles rows, a tab disappears, or the file is overwritten',
        'Privacy: the whole member list travels as an email attachment and sits on laptops, with no control over who sees what',
      ],
      tip: 'If three or more sound familiar, the spreadsheet is already costing you leads, payments or time. Plan the move now, before a lost file forces it.',
    },
    {
      id: 'what-a-crm-gives-you',
      heading: 'What a CRM gives you instead',
      paragraphs: [
        'A CRM keeps one record per person and per company, and attaches everything to it: calls, emails, meetings, contracts, payments and notes. Instead of a row you read across, you get a timeline you can trust, and anyone on the team can pick up where a colleague left off.',
        'Each lead has a phase and a dated next step, so the CRM reminds you instead of your memory. Duplicates are caught on entry and import, every change is logged with its author, and access follows each person’s role, so the bar staff do not see what the membership committee sees.',
        'In CadenceE, for example, the [sales CRM](page:sales) follows each lead through five phases from outreach to deposit, and [membership management](page:membership) keeps members, households and renewals on the same records. Emails go out from the CRM and are recorded on the person they were sent to.',
      ],
    },
    {
      id: 'excel-vs-crm',
      heading: 'Excel vs CRM, honestly',
      paragraphs: [
        'A CRM is not automatically better: it asks for set-up and discipline, and a team that never opens it gains nothing. The real question is whether your work has outgrown what a grid can hold.',
      ],
      subsections: [
        {
          heading: 'When a spreadsheet is still enough',
          paragraphs: [
            'One person managing a few dozen contacts, a one-off guest list for an event, a budget, or a quick analysis of numbers: Excel does all of these well. Many teams keep using it after the switch, on exports from the CRM, because pivot tables remain the fastest way to answer an unusual question.',
          ],
        },
        {
          heading: 'When a CRM is worth it',
          paragraphs: [
            'Several people work on the same contacts, follow-ups decide whether you win or lose, you need to know what happened before you call someone, or you hold personal data that must be protected and handed over on request. Once two of these apply, a shared spreadsheet becomes a liability rather than a tool.',
          ],
        },
      ],
    },
    {
      id: 'prepare-your-data',
      heading: 'How to prepare your data',
      paragraphs: [
        'Most failed migrations fail here, not in the software: an hour of cleaning saves a week of fixing records afterwards.',
      ],
      steps: [
        { title: 'Gather every list', text: 'The sales sheet, the member list, the newsletter export, the treasurer’s file, contacts from email: put them side by side before deciding anything.' },
        { title: 'Decide your columns', text: 'First name, last name, email, phone, company, language, status, source, owner, next step and its date. One fact per column, nothing merged.' },
        { title: 'Clean the formats', text: 'Split full names, trim stray spaces, keep one email per cell, write phone numbers as +41 79 123 45 67, use one date format, and separate street, number, postcode and town.' },
        { title: 'Remove duplicates', text: 'Sort by email, then by name and postcode. Decide which row wins and merge the notes, rather than deleting blindly.' },
        { title: 'Record consent', text: 'Add a column for newsletter consent with its date and source. If you do not know whether someone agreed, do not assume they did.' },
        { title: 'Leave out what you do not need', text: 'Old leads with no contact for years and data you cannot justify keeping are better left behind than migrated.' },
      ],
      tip: 'Before you clean anything, save an untouched copy of every original file and make it read-only. If a cleaning step goes wrong, you can always start again.',
    },
    {
      id: 'the-import',
      heading: 'The import: mapping, dry run and undo',
      paragraphs: [
        'Save each cleaned sheet as CSV in UTF-8, so accents such as é, ü and ç survive. Then map each column to a CRM field: “Tel. mobile” to phone, “Remarks” to a note. A column that matches no field is kept as a note or dropped on purpose.',
        'Never import a whole file blind. A dry run shows how many records will be created, updated or skipped as duplicates. Check ten records by hand against the original, including the one with the strangest name, then run the real import, knowing you can undo it.',
        'In CadenceE, leads, contacts, companies and members can be imported from spreadsheets. Every import starts with a dry run that shows what will happen, duplicate protection applies to imported leads, and an import can be undone.',
      ],
    },
    {
      id: 'what-to-migrate-first',
      heading: 'What to migrate first',
      paragraphs: [
        'Start with what the team needs on Monday morning, not with ten years of history. A good order is:',
      ],
      bullets: [
        'Open leads and deals, each with its owner and next step',
        'Current members or customers, with their household or company',
        'Companies and partners you work with',
        'A summary of past contact: last date and a short note, rather than every old call',
        'Archives, only if you really need them, and only once everything else works',
      ],
    },
    {
      id: 'getting-the-team-on-board',
      heading: 'Getting the team to adopt it',
      paragraphs: [
        'Software does not change habits by itself. Teams that switch well agree on a few rules and keep them for the first month.',
      ],
      bullets: [
        'Name one owner who decides on fields, answers questions and fixes mistakes',
        'Agree that if it is not in the CRM, it did not happen',
        'Make the old spreadsheet read-only on switch day, so there is one place to work',
        'Train people on their own leads and members, not on sample data',
        'Give each person a saved view of their own list and a daily look at what is due',
        'Let everyone work in their own language: CadenceE runs in French, German and English',
      ],
      tip: 'A short morning routine helps the habit stick. In CadenceE, the [AI assistant](page:ai) prepares a morning brief of what needs attention today, and good follow-up practice is covered in the guide to [sales follow-up emails](page:guide-sales-follow-up).',
    },
    {
      id: 'two-week-switch-plan',
      heading: 'A two-week plan to switch',
      paragraphs: [
        'Two weeks is enough for a club or a small sales team, if one person owns the project.',
      ],
      steps: [
        { title: 'Days 1 and 2: decide', text: 'Name the owner, list every spreadsheet in use and agree on the fields and phases you need.' },
        { title: 'Days 3 to 5: clean', text: 'Clean formats, remove duplicates, add consent and drop what you will not migrate.' },
        { title: 'Day 6: test', text: 'Import fifty rows with a dry run, check them by hand, adjust the mapping, and undo the test.' },
        { title: 'Day 7: set up', text: 'Invite colleagues with their roles, and prepare email templates and saved views.' },
        { title: 'Day 8: import', text: 'Run the full import, spot-check records and make the spreadsheets read-only.' },
        { title: 'Days 9 and 10: train', text: 'Walk each person through their own records and the daily routine.' },
        { title: 'Days 11 to 14: work and fix', text: 'Work only in the CRM, collect questions, correct mistakes and review together at the end of the week.' },
      ],
    },
    {
      id: 'exporting-back-out',
      heading: 'Exporting back out: no lock-in',
      paragraphs: [
        'Before you commit, test the way out. You should be able to export your lists to CSV at any time, hand your bookkeeper an accounting export, and give a person everything you hold about them if they ask, as the Swiss FADP requires. A system that makes leaving hard tells you something.',
        'In CadenceE, lists such as leads and activities export to CSV, the accounting export goes to Bexio or Abacus, and a privacy request exports everything held about a person. More on what to check before choosing in [how to choose a CRM](page:guide-choose-a-crm), and on the privacy side in the [FADP checklist for clubs](page:guide-fadp-checklist). Clubs that bill members will also want the [Swiss QR-bill](page:guide-qr-bill) guide, which depends on clean, structured addresses. To see how your own lists would look, [try the demo](/login?demo=1) or see all [features](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'Excel vs CRM: frequent questions',
    items: [
      { q: 'Is Excel a CRM?', a: 'No. Excel can store a contact list, but it has no history per person, no reminders, no duplicate protection and no control over who sees or changes what: the things a CRM is built for.' },
      { q: 'When should a small business replace its spreadsheet with a CRM?', a: 'When several people work on the same contacts, when follow-ups decide sales or renewals, or when you hold personal data you must protect and hand over on request. One person with a short list can stay on a spreadsheet.' },
      { q: 'How long does it take to move from Excel to a CRM?', a: 'For a club or a small sales team, about two weeks: a few days to clean the data, a test import, the full import, then a week of working in the CRM while one person fixes what comes up.' },
      { q: 'Will we lose data when we import a spreadsheet?', a: 'Not if you keep a read-only copy of the original, map every column on purpose and run a dry run first. Check a sample by hand, and choose a CRM where an import can be undone.' },
      { q: 'Can we keep using Excel alongside the CRM?', a: 'Yes, for analysis: export from the CRM and work on the copy. Do not keep editing contacts in both places, or you will have two versions of the truth again.' },
      { q: 'Is a shared Excel member list a problem under the Swiss FADP?', a: 'The law does not ban spreadsheets, but it requires appropriate security and the ability to answer requests. A file emailed around with no access control makes both hard. See the [FADP checklist for clubs](page:guide-fadp-checklist).' },
    ],
  },
  cta: {
    heading: 'Leave the spreadsheet, keep the data',
    text: 'CadenceE imports your leads, contacts and members from a spreadsheet with a dry run first and an undo afterwards, and exports your lists back to CSV whenever you need them.',
  },
};

export default guide;
