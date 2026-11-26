import type { GuideContent } from '../types';

// Sales follow-up emails: when to send them, what to write, templates, when to stop, the Swiss
// rules on marketing email (UWG art. 3(1)(o), FADP) and how a CRM keeps track.
const guide: GuideContent = {
  eyebrow: 'Sales',
  h1: 'How to write a sales follow-up email that gets a reply',
  lead: 'A first email often goes unanswered for reasons that have nothing to do with you. Here is when to follow up, what to write, six templates to adapt, when to stop, the Swiss rules on consent, and how to keep track of it all in a CRM.',
  readingMinutes: 9,
  keyTakeaways: [
    'Silence is rarely a no: plan three to five follow-ups before giving up on a new lead.',
    'Follow up the same day after a meeting and two to three working days after a proposal.',
    'Each message adds something new and asks for one easy next step.',
    'With no answer after four or five weeks, send a polite break-up email and stop.',
    'Marketing emails to a list need prior consent and an opt-out; personal follow-ups stop when the person asks.',
  ],
  sections: [
    {
      id: 'why-follow-up',
      heading: 'Why most deals need more than one email',
      paragraphs: [
        'Your prospect read your email between two meetings and forgot to reply, or is waiting for next quarter’s budget. Silence usually means bad timing, not a refusal.',
        'Memberships, private events and service contracts are decided by several people over several weeks. The seller who stays present, helpfully and without pressure, is still there when the decision is made; many deals are lost simply because nobody wrote again. The usual reasons for silence:',
      ],
      bullets: [
        'The email arrived at a bad moment.',
        'The decision depends on someone else, or on a budget.',
        'The next step was unclear, or looked like effort.',
      ],
    },
    {
      id: 'timing-and-cadence',
      heading: 'When to send a follow-up email: timing and cadence',
      paragraphs: [
        'Follow up quickly while the conversation is fresh, then leave longer gaps. Count in working days and remember Swiss holidays: school holidays vary by canton, and late December to early January is quiet almost everywhere.',
      ],
      subsections: [
        {
          heading: 'After a first contact',
          paragraphs: [
            'If your first email gets no answer, a five-week rhythm suits most clubs, venues and B2B teams: a short nudge on day 3, something useful on day 7 to 10, a different angle on day 17 to 21, and a break-up email around day 30. If the lead contacted you first, answer the same day.',
          ],
        },
        {
          heading: 'After a meeting',
          paragraphs: [
            'Send the follow-up email after a meeting the same day, or the next morning at the latest: what you understood, who does what, and the next step with a date. Never skip this one.',
          ],
        },
        {
          heading: 'After a proposal or a contract',
          paragraphs: [
            'Two to three working days after a proposal, check that it arrived and offer to go through it. When a contract awaits [online signature](page:e-signature), a reminder before a real deadline is a service, not pressure.',
          ],
        },
        {
          heading: 'After silence',
          paragraphs: [
            'When a warm lead goes quiet, slow down: one message after a week, another after two or three weeks, then a break-up email. Write again months later only if something has genuinely changed.',
          ],
        },
      ],
      tip: 'Before each follow-up, check the lead’s history: if they replied to a colleague or booked a meeting meanwhile, cancel the message.',
    },
    {
      id: 'what-to-write',
      heading: 'What to write: subject line, value and one next step',
      paragraphs: [
        'Every good follow-up has three parts: the context in one sentence, something new and useful, and one clear next step.',
      ],
      subsections: [
        {
          heading: 'Subject lines that get opened',
          paragraphs: [
            'Short and specific: “Your visit on Thursday: next steps”, “Proposal for the summer reception”. Reply in the same thread to your own message, and never fake a “Re:” on a first email.',
          ],
        },
        {
          heading: 'Something new in every message',
          paragraphs: [
            'An answer to a question from the meeting, an example from a similar client, a date you are holding, a simpler option. “Just checking in” gives the reader nothing; if you have nothing new, wait.',
          ],
        },
        {
          heading: 'One clear next step',
          paragraphs: [
            '“Would Tuesday at 10:00 or Wednesday at 14:00 suit you for a 20-minute call?” beats “Let me know if you have any questions.” One question, one link at most.',
          ],
        },
        {
          heading: 'A Swiss tone',
          paragraphs: [
            'Keep the formal address until the other person changes it, give exact dates and times, and leave out pressure and invented urgency.',
          ],
        },
      ],
    },
    {
      id: 'follow-up-email-templates',
      heading: 'Six follow-up email templates you can adapt',
      paragraphs: [
        'Replace the names, dates and details with your own, and keep the structure: context, something useful, one next step.',
      ],
      subsections: [
        {
          heading: '1. After a first contact with no reply',
          paragraphs: [
            'Subject: Re: Your team’s summer event',
            'Dear Ms Keller, I wrote to you last week about hosting your team’s summer event. June fills up quickly: shall I send you three possible dates and a sample menu? Kind regards, Laura Meier',
          ],
        },
        {
          heading: '2. The follow-up email after a meeting',
          paragraphs: [
            'Subject: Thank you for today: summary and next steps',
            'Dear Mr Rochat, Thank you for your time today. As I understood it, you are planning a dinner for 80 guests in late September. As agreed, I will send a proposal with two room layouts by Friday, and you will confirm the numbers with your management. Shall we speak on Tuesday at 10:00? Kind regards, Laura Meier',
          ],
        },
        {
          heading: '3. After sending a proposal',
          paragraphs: [
            'Subject: Your proposal for the September dinner',
            'Dear Mr Rochat, Did our proposal reach you, and does it match what you had in mind? I can go through it with you in 15 minutes by phone, on Thursday morning or Friday afternoon. Kind regards, Laura Meier',
          ],
        },
        {
          heading: '4. When a contract is waiting for signature',
          paragraphs: [
            'Subject: Your membership contract: one step left',
            'Dear Ms Favre, Your contract is ready to sign at the link I sent on Monday, and the joining deposit is paid in the same step. If anything is unclear, simply reply and I will answer today. Kind regards, Marc Bühler',
          ],
        },
        {
          heading: '5. Adding value after a silence',
          paragraphs: [
            'Subject: Three ideas for your client evening',
            'Dear Mr Steiner, I have attached three formats for client evenings we have hosted for companies of your size. If one of them is close to what you have in mind, I would be glad to adapt it for you. Kind regards, Laura Meier',
          ],
        },
        {
          heading: '6. The break-up email',
          paragraphs: [
            'Subject: Shall I stop writing?',
            'Dear Mr Steiner, I have not heard back, so I take it the timing is not right, and this will be my last message about your client evening. If things change, simply reply to this email: I will gladly pick it up again. Kind regards, Laura Meier',
          ],
        },
      ],
    },
    {
      id: 'when-to-stop',
      heading: 'When to stop: the break-up email and after',
      paragraphs: [
        'For a new lead, three to five follow-ups over four to six weeks is a sensible limit; beyond it, you harm your reputation and that of your sending address. The break-up email gives a busy person a last, easy chance to answer, and often brings back a useful “not now, try me in the autumn”. Stop at once when:',
      ],
      bullets: [
        'the person asks you to stop, or unsubscribes;',
        'they say no, or name someone else in charge;',
        'the email bounces, or they have left;',
        'they reply, book a meeting or sign.',
      ],
      tip: 'Record why a lead was closed (“no budget this year”): in six months, that note tells you whether to write again.',
    },
    {
      id: 'consent-and-unsubscribe',
      heading: 'Consent and unsubscribe rules in Switzerland',
      paragraphs: [
        'A personal message to someone you met, about a proposal they asked for, is business correspondence. A newsletter, or the same promotional text sent to a list, is marketing, and in Switzerland it falls under the Federal Act against Unfair Competition (UWG, or LCD in French).',
        'Under article 3(1)(o) UWG, mass advertising by email is unfair unless the recipients agreed beforehand, the sender is correctly identified and each message offers an easy opt-out at no cost. The exception: customers whose address you obtained in a sale, and whom you told they could refuse, may receive advertising for your own similar products. Business addresses are not exempt.',
        'The law does not draw an exact line between a personal follow-up and mass advertising: a message written for one person is generally correspondence, a template sent to a bought list is not. And under the FADP, anyone may object to the use of their data for marketing. In practice:',
      ],
      bullets: [
        'Newsletters and campaigns only to people who agreed, with the consent recorded.',
        'A named sender and a working unsubscribe link in every marketing email.',
        'One-to-one follow-ups that stop as soon as the person asks.',
        'Stricter rules for many EU recipients: Germany, for example, generally requires prior consent, even between businesses.',
      ],
      tip: 'This is general information, not legal advice. For personal data more broadly, see our [FADP checklist](page:guide-fadp-checklist).',
    },
    {
      id: 'track-follow-ups-in-a-crm',
      heading: 'Tracking follow-ups in a CRM with sequences and reminders',
      paragraphs: [
        'Follow-ups fail because nobody remembers to send them. A spreadsheet works for twenty leads, not for two hundred shared by three people. Use sequences for the standard steps and tasks for the personal ones, and make sure a sequence stops when the person replies.',
        'In CadenceE’s [sales CRM](page:sales), each lead sits in one of five phases, from outreach to deposit, with its tasks attached. [Sequences](page:email) send follow-ups on a schedule from one outbox that records each email on the person and respects consent, and a morning brief lists what needs attention today. Still on a spreadsheet? See [moving from Excel to a CRM](page:guide-excel-to-crm).',
      ],
      steps: [
        { title: 'A next step on every lead', text: 'A phase, an owner and a date for the next action.' },
        { title: 'Sequences written once', text: 'From the templates above, in each language you sell in.' },
        { title: 'Every email on the record', text: 'So nobody on the team writes twice.' },
        { title: 'Follow-ups first thing', text: 'Before the inbox takes over the day.' },
      ],
    },
    {
      id: 'ai-writer',
      heading: 'Using an AI writer responsibly',
      paragraphs: [
        'AI is good at the blank page: a draft recap, three subject lines, a shorter version of a long email. It does not know what you promised or what is true. CadenceE’s [AI sales writer](page:ai) drafts follow-ups in your brand’s tone, with the goal and length you choose, and a person reviews and sends each one. With any tool:',
      ],
      bullets: [
        'Give it your meeting notes and the next step you want.',
        'Check every fact, name, date, price and promise before sending.',
        'Cut the clichés so that it sounds like you.',
        'Never let it invent clients or figures, or email a list unsupervised.',
      ],
    },
  ],
  faq: {
    heading: 'Sales follow-up emails: frequent questions',
    items: [
      { q: 'How long should I wait before sending a follow-up email?', a: 'The same day after a meeting. After a first email or a proposal, two to three working days, then about a week, then two weeks.' },
      { q: 'How many follow-up emails should I send?', a: 'Three to five over four to six weeks for a new lead, ending with a break-up email. Stop at once if asked.' },
      { q: 'What should a follow-up email after a meeting say?', a: 'Thanks, a summary of what you understood, who does what by when, and the next step with a date.' },
      { q: 'How do I follow up on a proposal without being pushy?', a: 'Ask whether it arrived, offer a short call, and add something useful in the next message. Mention deadlines only if they are real.' },
      { q: 'Do I need consent to send follow-up emails in Switzerland?', a: 'Generally not for a personal follow-up to someone you are in contact with, but stop when they ask. Marketing emails to a list need prior consent under the UWG, except for existing customers, plus a clear sender and an opt-out.' },
      { q: 'Can AI write my follow-up emails?', a: 'It can draft them, but a person should check and send each one.' },
    ],
  },
  cta: {
    heading: 'Every follow-up on time, on one record',
    text: 'CadenceE gives every lead a phase and a next step, sends your follow-up sequences with consent built in, and drafts each email with an AI writer you review before it goes out.',
  },
};

export default guide;
